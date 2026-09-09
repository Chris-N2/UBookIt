using Microsoft.EntityFrameworkCore;
using UBookIt.Core.Bookings;
using UBookIt.Core.Stores;
using UBookIt.Persistence.Stores;
using UBookIt.Tests.Integration.Support;

namespace UBookIt.Tests.Integration;

/// <summary>
/// The retention due-read against a real database: what its SQL actually selects, and the index
/// the sweep depends on.
/// </summary>
/// <remarks>
/// <para>
/// <b>Status-blindness is only meaningful here.</b> The in-memory double mirrors the production
/// predicate by hand, so the unit tests establish that the SWEEP is status-blind given a
/// status-blind store — they cannot establish that the store is one. A <c>WHERE Status = 1</c>
/// added to <see cref="SqlBookingStore"/> would leave every unit test green.
/// </para>
/// <para>
/// Each test seeds its own bookings with a marker in the booker's email so that the shared
/// database's other rows cannot make an assertion pass or fail by accident.
/// </para>
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class BookerRetentionStoreTests(SqlServerFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset Cutoff = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    private async Task<Booking> PlaceAsync(
        DateTimeOffset endUtc, BookingStatus status = BookingStatus.Confirmed, string? email = null)
    {
        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);

        var booking = Booking.Rehydrate(
            Guid.NewGuid(),
            new RandomBookingReferenceFactory().Next(),
            BookingInterval.Create(endUtc.AddHours(-1), endUtc, "UTC").Value,
            Booker.Create(null, "Integration Tester", email ?? $"retention-{Guid.NewGuid():N}@example.com").Value,
            [new ResourceClaim(resourceId)],
            status,
            endUtc.AddDays(-30)).Value;

        await using var context = fixture.CreateContext();
        var placed = await new SqlBookingStore(context).PlaceAsync(booking, Ct);

        Assert.True(placed.Succeeded);
        return placed.Value;
    }

    private async Task<IReadOnlyList<Guid>> DueAsync(int take = 500)
    {
        await using var context = fixture.CreateContext();
        return await new SqlBookingStore(context).GetBookingIdsDueForErasureAsync(Cutoff, take, Ct);
    }

    [Fact]
    public async Task A_booking_that_ended_before_the_cutoff_is_due()
    {
        fixture.EnsureAvailable();

        var old = await PlaceAsync(Cutoff.AddDays(-10));

        Assert.Contains(old.Id, await DueAsync());
    }

    [Fact]
    public async Task A_booking_that_ended_after_the_cutoff_is_not_due()
    {
        fixture.EnsureAvailable();

        var recent = await PlaceAsync(Cutoff.AddDays(10));

        Assert.DoesNotContain(recent.Id, await DueAsync());
    }

    [Fact]
    public async Task The_cutoff_is_compared_against_the_END_and_not_the_start()
    {
        fixture.EnsureAvailable();

        // Straddles the cutoff: starts before it, ends after it. Comparing on StartUtc — which is
        // the column the EXISTING index leads on, so the tempting one to reach for — would call
        // this due and erase somebody a day early.
        var straddling = await PlaceAsync(Cutoff.AddMinutes(30));

        Assert.DoesNotContain(straddling.Id, await DueAsync());
    }

    [Theory]
    [InlineData(BookingStatus.Requested)]
    [InlineData(BookingStatus.Confirmed)]
    [InlineData(BookingStatus.Cancelled)]
    [InlineData(BookingStatus.Declined)]
    public async Task The_query_is_blind_to_status(BookingStatus status)
    {
        // THE ASSERTION THAT ONLY EXISTS HERE. One case per status, because a fixture set of a
        // single status cannot see a status filter that excludes the others.
        fixture.EnsureAvailable();

        var old = await PlaceAsync(Cutoff.AddDays(-10), status);

        Assert.Contains(old.Id, await DueAsync());
    }

    [Fact]
    public async Task An_already_erased_booking_is_never_due_again()
    {
        // What makes the sweep terminate: erasing removes a booking from this set. If it did not,
        // the sweep's "take the head repeatedly" loop would select the same rows for ever.
        fixture.EnsureAvailable();

        var old = await PlaceAsync(Cutoff.AddDays(-10));

        await using (var context = fixture.CreateContext())
        {
            Assert.True(await new SqlBookingStore(context)
                .EraseBookerAsync(old.Id, Cutoff, Ct));
        }

        Assert.DoesNotContain(old.Id, await DueAsync());
    }

    [Fact]
    public async Task The_read_returns_identifiers_and_therefore_carries_no_person()
    {
        // The security property that lets an erasure with no caller exist. Asserted against the
        // real store because "it returns Guids" is a claim about what SQL sends back, and the
        // in-memory double could satisfy it while the production projection selected whole rows.
        fixture.EnsureAvailable();

        await PlaceAsync(Cutoff.AddDays(-10));

        var due = await DueAsync();

        Assert.NotEmpty(due);
        Assert.All(due, id => Assert.IsType<Guid>(id));
    }

    [Fact]
    public async Task Take_is_honoured_and_the_order_is_stable_across_calls()
    {
        // The sweep takes the head of this set repeatedly and relies on making progress. An
        // unordered TOP (n) is free to return different rows each call, which would let a
        // non-terminating sweep look like a terminating one.
        fixture.EnsureAvailable();

        for (var i = 0; i < 5; i++)
        {
            await PlaceAsync(Cutoff.AddDays(-100 + i));
        }

        var first = await DueAsync(take: 3);
        var second = await DueAsync(take: 3);

        Assert.Equal(3, first.Count);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task The_index_the_migration_created_covers_the_end_column_and_is_filtered()
    {
        // Read from the database's own catalogue rather than from the EF model, so it observes
        // what the migration actually created rather than what EF intended.
        //
        // The FILTER is asserted, not just the index. An unfiltered index on EndUtc would serve
        // the query and pass a presence check, while growing an entry for every booking the site
        // has ever taken — including all the erased ones this query exists to exclude, which are
        // exactly the rows it would be largest for.
        fixture.EnsureAvailable();

        await using var context = fixture.CreateContext();

        var indexes = await context.Database
            .SqlQuery<FilteredIndexRow>($@"
                SELECT i.name AS [Name], i.has_filter AS [HasFilter], ISNULL(i.filter_definition, '') AS [FilterDefinition]
                FROM sys.indexes i
                JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                JOIN sys.columns c ON c.object_id = i.object_id AND c.column_id = ic.column_id
                WHERE i.object_id = OBJECT_ID('uBookItBooking')
                  AND c.name = 'EndUtc'
                  AND ic.key_ordinal = 1")
            .ToListAsync(Ct);

        var retention = Assert.Single(indexes, index => index.HasFilter);

        Assert.Equal("IX_uBookItBooking_EndUtc_Unerased", retention.Name);
        Assert.Contains("BookerErasedUtc", retention.FilterDefinition, StringComparison.Ordinal);
        Assert.Contains("IS NULL", retention.FilterDefinition, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_due_query_emits_the_end_and_erasure_predicate_and_selects_only_the_id()
    {
        // What the store SENDS, which is where both properties actually live: a projection that
        // selected whole rows would satisfy every behavioural assertion above while reading every
        // booker's name and address out of the database on the one path with no user accountable
        // for it.
        // OBSERVED FROM THE STORE, not rebuilt beside it.
        //
        // This test first restated the query inline and asserted over `ToQueryString()` of the
        // copy. It passed — and it passed against a production store carrying a `WHERE Status =
        // 1`, which is the exact defect the last assertion here exists to catch. A guard that
        // watches a reconstruction of the thing it guards watches nothing: it can only ever
        // report that the test's own LINQ says what the test's own LINQ says.
        //
        // So the real store is called through a recording interceptor, and the assertions are
        // made against what SQL Server was actually sent.
        fixture.EnsureAvailable();

        await PlaceAsync(Cutoff.AddDays(-10));

        var interceptor = new CommandRecordingInterceptor();
        await using var context = fixture.CreateContext(interceptor);

        await new SqlBookingStore(context).GetBookingIdsDueForErasureAsync(Cutoff, 100, Ct);

        var command = Assert.Single(interceptor.Commands);

        Assert.StartsWith("SELECT", command.TrimStart(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("EndUtc", command, StringComparison.Ordinal);
        Assert.Contains("BookerErasedUtc", command, StringComparison.Ordinal);

        // The columns a booker lives in must not be selected. Asserted by name, one at a time, so
        // that a projection widened to a row fails here and names which column brought it back.
        Assert.DoesNotContain("BookerName", command, StringComparison.Ordinal);
        Assert.DoesNotContain("BookerEmail", command, StringComparison.Ordinal);
        Assert.DoesNotContain("BookerPhone", command, StringComparison.Ordinal);
        Assert.DoesNotContain("MemberKey", command, StringComparison.Ordinal);

        // And no status filter, which is the other half of the requirement and is invisible in a
        // behavioural test that seeds one status.
        Assert.DoesNotContain("[Status]", command, StringComparison.Ordinal);
    }

    private sealed record FilteredIndexRow(string Name, bool HasFilter, string FilterDefinition);
}
