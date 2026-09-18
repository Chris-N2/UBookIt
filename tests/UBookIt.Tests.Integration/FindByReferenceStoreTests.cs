using Microsoft.EntityFrameworkCore;
using UBookIt.Core;
using UBookIt.Core.Bookings;
using UBookIt.Core.Stores;
using UBookIt.Persistence.Stores;
using UBookIt.Tests.Integration.Support;

namespace UBookIt.Tests.Integration;

/// <summary>
/// The by-reference read against a real SQL Server (booking-management spec, "A booking can be
/// found by its reference").
/// </summary>
/// <remarks>
/// <para>
/// <b>Integration rather than unit, for the same reason the address search is.</b> The claims
/// worth testing are properties of the SQL that runs — that it is an equality on the unique
/// index and never a scan — and the in-memory double cannot see any of that. The one claim this
/// file makes that a unit test could is parity with the list's row, and it is made here anyway,
/// against the same store, so the two projections are compared on real data.
/// </para>
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class FindByReferenceStoreTests(SqlServerFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset Now = new(2026, 9, 5, 9, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Start = new(2026, 9, 10, 10, 0, 0, TimeSpan.Zero);

    private static readonly SiteBookingSettings Settings = new() { TimeZoneId = "UTC", MaxQueryRangeDays = 31 };

    private async Task<Booking> PlaceAsync(
        BookingStatus status = BookingStatus.Confirmed, DateTimeOffset? start = null)
    {
        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);
        var at = start ?? Start;
        var booking = Booking.Rehydrate(
            Guid.NewGuid(),
            new RandomBookingReferenceFactory().Next(),
            BookingInterval.Create(at, at.AddHours(1), "UTC").Value,
            Booker.Create(null, "Integration Tester", $"ref-{Guid.NewGuid():N}@example.com", "01234 567890").Value,
            [new ResourceClaim(resourceId)],
            status,
            Now.AddDays(-1)).Value;

        await using var context = fixture.CreateContext();
        var placed = await new SqlBookingStore(context).PlaceAsync(booking, Ct);
        Assert.True(placed.Succeeded);

        return placed.Value;
    }

    private async Task<BookingSummary?> FindAsync(BookingReference reference)
    {
        await using var context = fixture.CreateContext();
        return await new SqlBookingManagementStore(context).FindByReferenceAsync(reference, Ct);
    }

    [Fact]
    public async Task Spec_scenario_a_booking_is_found_by_its_reference()
    {
        fixture.EnsureAvailable();
        var booking = await PlaceAsync();

        var found = await FindAsync(booking.Reference);

        Assert.NotNull(found);
        Assert.Equal(booking.Id, found!.BookingId);
        Assert.Equal(booking.Reference, found.Reference);
    }

    [Fact]
    public async Task Spec_scenario_the_reference_is_accepted_as_typed()
    {
        // The parser owns tolerance — lower case, the display separator, stray spaces — and the
        // store compares the canonical value it produces. So the three forms below are one
        // reference by the time they reach the store, and this proves the pair rather than
        // either half.
        fixture.EnsureAvailable();
        var booking = await PlaceAsync();
        var canonical = booking.Reference.Value;

        foreach (var typed in new[] { canonical.ToLowerInvariant(), booking.Reference.Display, $" {booking.Reference.Display.ToLowerInvariant()} " })
        {
            Assert.True(BookingReference.TryParse(typed, out var parsed), typed);
            var found = await FindAsync(parsed);
            Assert.NotNull(found);
            Assert.Equal(booking.Id, found!.BookingId);
        }
    }

    [Fact]
    public async Task Spec_scenario_a_partial_reference_finds_nothing()
    {
        // A prefix of a reference is not a reference: TryParse refuses it before the store is
        // reached, which is the whole of the guarantee. The store's own comparison is exact too,
        // and that is asserted separately by the SQL test below.
        fixture.EnsureAvailable();
        var booking = await PlaceAsync();

        Assert.False(BookingReference.TryParse(booking.Reference.Value[..4], out _));
        Assert.False(BookingReference.TryParse(booking.Reference.Value[..7], out _));
    }

    [Fact]
    public async Task A_reference_nobody_holds_returns_null_rather_than_throwing()
    {
        fixture.EnsureAvailable();

        var found = await FindAsync(new RandomBookingReferenceFactory().Next());

        Assert.Null(found);
    }

    [Fact]
    public async Task Spec_scenario_a_cancelled_booking_is_still_found()
    {
        // No status filter: the caller quoted a reference and wants THAT booking.
        fixture.EnsureAvailable();
        var booking = await PlaceAsync(BookingStatus.Cancelled);

        var found = await FindAsync(booking.Reference);

        Assert.NotNull(found);
        Assert.Equal(BookingStatus.Cancelled, found!.Status);
    }

    [Fact]
    public async Task Spec_scenario_an_erased_booking_is_still_found()
    {
        // Erasure removes the person, not the booking. booker-erasure guarantees the reference
        // survives "so an operator can match what a caller reads out" — this read is what that
        // sentence was written for, and it must show the erased state rather than nothing.
        fixture.EnsureAvailable();
        var booking = await PlaceAsync();
        var (bookings, _) = fixture.CreateServices(Now);
        Assert.True((await bookings.EraseBookerAsync(booking.Id, Ct)).Succeeded);

        var found = await FindAsync(booking.Reference);

        Assert.NotNull(found);
        Assert.Equal(booking.Id, found!.BookingId);
        Assert.NotNull(found.Booker.ErasedUtc);
        Assert.Null(found.Booker.Contact);
    }

    [Fact]
    public async Task A_found_booking_is_the_lists_own_row()
    {
        // Parity, on real data: the same booking through the list's read and through this one
        // must be the same summary, member for member. Asserting the RECORD rather than a few
        // fields, so that a projection which drifted in any member fails here.
        fixture.EnsureAvailable();
        var booking = await PlaceAsync();

        var found = await FindAsync(booking.Reference);

        var query = BookingQuery.Create(Start.AddDays(-1), Start.AddDays(1), Settings, take: 200);
        Assert.True(query.Succeeded);
        await using var context = fixture.CreateContext();
        var page = await new SqlBookingManagementStore(context).ListAsync(query.Value, Ct);
        var listed = Assert.Single(page.Items, row => row.BookingId == booking.Id);

        // Record equality compares `Resources` by reference — two lists holding the same
        // resources are "different" to it — so that member is compared element-wise and the
        // record is compared with it held equal. Every other member still goes through the
        // record's own equality, so a projection that drifted anywhere else fails here.
        Assert.NotNull(found);
        Assert.Equal(listed.Resources, found!.Resources);
        Assert.Equal(listed, found with { Resources = listed.Resources });
    }

    [Fact]
    public async Task The_lookup_emits_an_equality_on_the_reference_and_never_a_LIKE()
    {
        // The claim that makes bypassing the window legitimate: this is a seek on the unique
        // index. A Contains would compile to LIKE '%…%' and a scan, and would answer "which
        // references contain this fragment" — a lookup would have become a search without the
        // signature changing.
        fixture.EnsureAvailable();
        var booking = await PlaceAsync();

        var interceptor = new CommandRecordingInterceptor();
        await using var context = fixture.CreateContext(interceptor);

        await new SqlBookingManagementStore(context).FindByReferenceAsync(booking.Reference, Ct);

        var commands = interceptor.Commands.Where(c => c.Contains("uBookItBooking", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(commands);

        foreach (var command in commands)
        {
            Assert.DoesNotContain("LIKE", command, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains(commands, c => c.Contains("[Reference] = ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_reference_column_is_unique_indexed()
    {
        // The seek is only a seek because of this index; if a migration ever dropped it the
        // read above would still pass every behavioural test while scanning the table.
        fixture.EnsureAvailable();
        await using var context = fixture.CreateContext();

        var indexes = await context.Database
            .SqlQueryRaw<IndexRow>(
                "SELECT i.name AS Name, i.is_unique AS IsUnique "
                + "FROM sys.indexes i "
                + "JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id "
                + "JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id "
                + "WHERE i.object_id = OBJECT_ID('uBookItBooking') AND c.name = 'Reference'")
            .ToListAsync(Ct);

        Assert.Contains(indexes, index => index.IsUnique);
    }

    /// <summary>One index over a column, and whether it enforces uniqueness.</summary>
    private sealed record IndexRow(string Name, bool IsUnique);
}
