using Microsoft.EntityFrameworkCore;
using UBookIt.Core;
using UBookIt.Core.Bookings;
using UBookIt.Core.Stores;
using UBookIt.Persistence.Stores;
using UBookIt.Tests.Integration.Support;

namespace UBookIt.Tests.Integration;

/// <summary>
/// Finding a subject's bookings by their email address, against a real database.
/// </summary>
/// <remarks>
/// <para>
/// The two properties that matter here cannot be established anywhere else. <b>Exactness</b> is a
/// property of the SQL the store emits — an in-memory double that filtered with <c>==</c> would
/// pass while the real query used <c>LIKE</c>, and the difference between them is a lookup and an
/// enumeration tool. <b>Unwindowedness</b> is only meaningful against a store that could have
/// windowed it.
/// </para>
/// <para>
/// Each test seeds its own resource, because the fixture's database is shared across the
/// collection and every test here books the same window.
/// </para>
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class FindByBookerStoreTests(SqlServerFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset Now = new(2026, 9, 5, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Start = new(2026, 9, 10, 10, 0, 0, TimeSpan.Zero);

    private async Task<(Guid ResourceId, Booking Booking)> PlaceAsync(
        string email, DateTimeOffset? start = null, string name = "Integration Tester")
    {
        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);
        var at = start ?? Start;

        var booking = Booking.Rehydrate(
            Guid.NewGuid(),
            new RandomBookingReferenceFactory().Next(),
            BookingInterval.Create(at, at.AddHours(1), "UTC").Value,
            Booker.Create(null, name, email, "01234 567890").Value,
            [new ResourceClaim(resourceId)],
            BookingStatus.Confirmed,
            Now.AddDays(-1)).Value;

        await using var context = fixture.CreateContext();
        var placed = await new SqlBookingStore(context).PlaceAsync(booking, Ct);

        Assert.True(placed.Succeeded);
        return (resourceId, placed.Value);
    }

    private async Task<BookingPage> FindAsync(string email, int skip = 0, int take = 50)
    {
        var query = BookerEmailQuery.Create(email, skip, take);
        Assert.True(query.Succeeded);

        await using var context = fixture.CreateContext();
        return await new SqlBookingManagementStore(context).FindByBookerEmailAsync(query.Value, Ct);
    }

    [Fact]
    public async Task Every_booking_a_person_made_is_found()
    {
        fixture.EnsureAvailable();

        var address = $"subject-{Guid.NewGuid():N}@example.com";
        var (_, first) = await PlaceAsync(address);
        var (_, second) = await PlaceAsync(address, Start.AddDays(400));

        var page = await FindAsync(address);

        // Both, and the total counts both — an operator honouring an erasure request needs to
        // know there are two, not to erase the one they happened to see.
        Assert.Equal(2, page.Total);
        Assert.Contains(page.Items, row => row.BookingId == first.Id);
        Assert.Contains(page.Items, row => row.BookingId == second.Id);
    }

    [Fact]
    public async Task A_booking_far_outside_any_acceptable_window_is_still_found()
    {
        // THE REASON THIS IS A SEPARATE READ. The management list refuses a window wider than
        // MaxQueryRangeDays (31), so a booking made 400 days out is unreachable through it
        // without knowing roughly when it is — which a data subject's request never says.
        fixture.EnsureAvailable();

        var address = $"faraway-{Guid.NewGuid():N}@example.com";
        var (_, booking) = await PlaceAsync(address, Start.AddDays(400));

        var page = await FindAsync(address);

        Assert.Equal(booking.Id, Assert.Single(page.Items).BookingId);
    }

    [Fact]
    public async Task An_address_nobody_holds_returns_an_empty_page()
    {
        fixture.EnsureAvailable();

        var page = await FindAsync($"nobody-{Guid.NewGuid():N}@example.com");

        Assert.Empty(page.Items);
        Assert.Equal(0, page.Total);
    }

    [Fact]
    public async Task Matching_is_exact_and_a_fragment_finds_nothing()
    {
        // THE NARROWING THAT KEEPS THIS A LOOKUP. Run against real SQL because that is where
        // the difference lives: `Contains` compiles to LIKE '%…%' and would answer "which of
        // your bookers are at this domain", which is an enumeration facility rather than a
        // lookup and which the sensitive-data capability forbids.
        fixture.EnsureAvailable();

        var domain = $"exact-{Guid.NewGuid():N}.example.com";
        await PlaceAsync($"ada@{domain}");

        Assert.Empty((await FindAsync($"ada@{domain}".Replace("ada@", "ad@"))).Items);
        Assert.Empty((await FindAsync($"bob@{domain}")).Items);

        // A strict PREFIX of a stored address finds nothing — the case that separates equality
        // from StartsWith, and the one the earlier version of this test did not actually cover
        // (`a@domain` is not a prefix of `ada@domain`). And a longer address CONTAINING a
        // stored one finds nothing either, which separates equality from Contains.
        await PlaceAsync($"grace@{domain}.uk");
        Assert.Empty((await FindAsync($"grace@{domain}")).Items);
        Assert.Empty((await FindAsync($"xada@{domain}")).Items);

        // The exact value still does — so the four negatives above are narrowing, not a broken
        // query that would have returned nothing whatever it was asked.
        Assert.Single((await FindAsync($"ada@{domain}")).Items);
    }

    [Fact]
    public async Task A_domain_shared_by_two_people_returns_only_the_one_asked_for()
    {
        // The enumeration case stated positively: two bookers at one domain, and a search for
        // either must not surface the other. This is the query somebody would reach for if the
        // match were ever loosened.
        fixture.EnsureAvailable();

        var domain = $"shared-{Guid.NewGuid():N}.example.com";
        var (_, ada) = await PlaceAsync($"ada@{domain}", name: "Ada");
        await PlaceAsync($"grace@{domain}", Start.AddDays(1), name: "Grace");

        var page = await FindAsync($"ada@{domain}");

        Assert.Equal(ada.Id, Assert.Single(page.Items).BookingId);
        Assert.Equal(1, page.Total);
    }

    [Fact]
    public async Task An_erased_booking_is_not_found_by_the_address_it_once_held()
    {
        // Falls out of erasure rather than being filtered for: the column is NULL, so it
        // matches nothing. The consequence worth stating is that a completed erasure is NOT
        // verifiable by searching for the person again — an empty result means either "erased"
        // or "never booked", and the operator cannot tell which.
        fixture.EnsureAvailable();

        var address = $"erased-{Guid.NewGuid():N}@example.com";
        var (_, booking) = await PlaceAsync(address);

        Assert.Single((await FindAsync(address)).Items);

        var (bookings, _) = fixture.CreateServices(Now);
        Assert.True((await bookings.EraseBookerAsync(booking.Id, Ct)).Succeeded);

        var page = await FindAsync(address);

        Assert.Empty(page.Items);
        Assert.Equal(0, page.Total);

        // The booking itself is still there, still holding its slot — it is the address that
        // is gone, not the record.
        await using var context = fixture.CreateContext();
        Assert.True(await context.Bookings.AnyAsync(b => b.Id == booking.Id, Ct));
    }

    [Fact]
    public async Task Results_page_with_the_unpaged_total()
    {
        fixture.EnsureAvailable();

        var address = $"prolific-{Guid.NewGuid():N}@example.com";
        await PlaceAsync(address);
        await PlaceAsync(address, Start.AddDays(1));
        await PlaceAsync(address, Start.AddDays(2));

        var first = await FindAsync(address, skip: 0, take: 2);
        var second = await FindAsync(address, skip: 2, take: 2);

        Assert.Equal(2, first.Items.Count);
        Assert.Single(second.Items);

        // The total is what matched, not what fitted — a pager told "2" never offers page two,
        // and an operator would erase two of three bookings believing they were done.
        Assert.Equal(3, first.Total);
        Assert.Equal(3, second.Total);

        // No row appears on both pages: ordering is total (start, then id), so paging is stable.
        Assert.Empty(first.Items.Select(r => r.BookingId).Intersect(second.Items.Select(r => r.BookingId)));

        // A page of ZERO returns no rows and the real total — which the requirement states is
        // NOT the count-only form it forbids elsewhere. The prohibition is on offering a count
        // to somebody who may not see what is counted; this caller may read every row it would
        // have returned, so a page size of nothing discloses nothing new.
        var none = await FindAsync(address, skip: 0, take: 0);

        Assert.Empty(none.Items);
        Assert.Equal(3, none.Total);
    }

    [Fact]
    public async Task Pages_do_not_overlap_or_lose_rows_when_bookings_share_a_start_time()
    {
        // THE FIXTURE THE REQUIREMENT NAMES, and the one this change failed to use.
        //
        // "Results are paged in a stable order" says, in a sentence this change carried forward
        // and then widened to reach every paged read: "A test for this SHALL include bookings
        // that share a start time, because a fixture of distinct start times passes against an
        // ordering that has no tiebreak at all." The first version of the by-address paging
        // test seeded three DISTINCT starts and passed with `ThenBy(Id)` deleted.
        //
        // THREE bookings at ONE instant, read as two pages of two. Without a total order the
        // page boundary falls somewhere the database has not committed to, and a row is
        // repeated on both pages while another is never returned — which for an operator
        // working a search to honour an erasure request means erasing one booking twice and
        // never seeing the third.
        fixture.EnsureAvailable();

        var address = $"tied-{Guid.NewGuid():N}@example.com";
        var at = Start.AddDays(700);

        await PlaceAsync(address, at);
        await PlaceAsync(address, at);
        await PlaceAsync(address, at);

        var first = await FindAsync(address, skip: 0, take: 2);
        var second = await FindAsync(address, skip: 2, take: 2);

        Assert.Equal(3, first.Total);
        Assert.Equal(2, first.Items.Count);
        Assert.Single(second.Items);

        var seen = first.Items.Concat(second.Items).Select(row => row.BookingId).ToList();

        // Every booking exactly once across the two pages — no repeat, nothing lost.
        Assert.Equal(3, seen.Distinct().Count());
        Assert.Equal(3, seen.Count);
    }

    [Fact]
    public void The_search_orders_by_start_then_identity()
    {
        // The tiebreak has no observable effect on a database where Id happens to be the
        // clustered key — SQL Server's incidental order already matches it — so the shared-start
        // test above can pass against an ordering with no tiebreak at all, on some data, by
        // luck. The only way to catch its removal is to read the emitted ORDER BY, which is why
        // the store exposes the ordering as a seam and why the search now goes through it
        // instead of duplicating it inline.
        fixture.EnsureAvailable();

        var query = BookerEmailQuery.Create("ada@example.com", 0, 20);
        Assert.True(query.Succeeded);

        using var context = fixture.CreateContext();
        var sql = new SqlBookingManagementStore(context).OrderedPage(query.Value).ToQueryString();

        var offset = sql.IndexOf("OFFSET", StringComparison.Ordinal);

        Assert.True(
            offset >= 0,
            $"The search no longer emits OFFSET/FETCH, so there is no paging clause to check "
            + $"and this guard is not testing what it claims. SQL was: {sql}");

        var paging = sql[..offset];
        var orderBy = paging[paging.LastIndexOf("ORDER BY", StringComparison.Ordinal)..];

        Assert.Contains("[StartUtc]", orderBy, StringComparison.Ordinal);
        Assert.Contains("[Id]", orderBy, StringComparison.Ordinal);
        Assert.True(
            orderBy.IndexOf("[StartUtc]", StringComparison.Ordinal)
                < orderBy.IndexOf("[Id]", StringComparison.Ordinal),
            $"Start time must be the primary sort key. ORDER BY was: {orderBy}");
    }

    [Fact]
    public async Task The_rows_are_the_same_shape_the_list_returns()
    {
        // One projection serves both reads. A second description of a booking, free to disagree
        // about a resource name or the booker's condition, is what the shared projection exists
        // to prevent — and the backoffice renders both responses with the same code.
        fixture.EnsureAvailable();

        var address = $"shape-{Guid.NewGuid():N}@example.com";
        var (resourceId, booking) = await PlaceAsync(address);

        var found = Assert.Single((await FindAsync(address)).Items);

        var query = BookingQuery.Create(
            Start.AddDays(-1),
            Start.AddDays(1),
            new SiteBookingSettings { TimeZoneId = "UTC" },
            [BookingStatus.Confirmed],
            [resourceId],
            BookingQuery.DefaultSkip,
            BookingQuery.DefaultTake);

        Assert.True(query.Succeeded);

        await using var context = fixture.CreateContext();
        var listed = Assert.Single(
            (await new SqlBookingManagementStore(context).ListAsync(query.Value, Ct)).Items);

        Assert.Equal(listed.BookingId, found.BookingId);
        Assert.Equal(listed.Reference.Value, found.Reference.Value);
        Assert.Equal(listed.Status, found.Status);
        Assert.Equal(listed.Interval, found.Interval);
        Assert.Equal(
            listed.Resources.Select(r => (r.ResourceId, r.DisplayName)),
            found.Resources.Select(r => (r.ResourceId, r.DisplayName)));
        Assert.Equal(listed.Booker.Contact?.Email, found.Booker.Contact?.Email);
        Assert.Equal(listed.Booker.IsErased, found.Booker.IsErased);
    }

    [Fact]
    public async Task The_index_the_migration_created_covers_the_column_the_search_filters_on()
    {
        // The index is a PRECONDITION, not an optimisation: unwindowed and unindexed, this is a
        // scan of a table that grows without limit — reintroducing the cost the list's window
        // guard exists to bound.
        //
        // Read from the database's own catalogue rather than from the model, so it observes what
        // the migration actually created rather than what EF intended.
        fixture.EnsureAvailable();

        await using var context = fixture.CreateContext();

        var indexes = await context.Database
            .SqlQuery<IndexRow>($@"
                SELECT i.name AS [Name], i.is_unique AS [IsUnique]
                FROM sys.indexes i
                JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                JOIN sys.columns c ON c.object_id = i.object_id AND c.column_id = ic.column_id
                WHERE i.object_id = OBJECT_ID('uBookItBooking')
                  AND c.name = 'BookerEmail'")
            .ToListAsync(Ct);

        // EVERY index over the column, then assert about all of them — filtering to the
        // non-unique ones and asserting NotEmpty would pass with a unique index sitting beside
        // a non-unique one, which is the state that would actually refuse a second booking.
        Assert.NotEmpty(indexes);
        Assert.All(indexes, index => Assert.False(index.IsUnique));
    }

    [Fact]
    public async Task The_search_emits_an_equality_comparison_and_never_a_LIKE()
    {
        // WHERE EXACTNESS ACTUALLY LIVES. `Contains` and `StartsWith` compile to LIKE, and the
        // behavioural tests above cannot tell the difference on the data they seed — a LIKE
        // '%ada@…%' matches the same single row. The shapes are distinguishable by what is
        // SENT, so that is what is asserted, and it is the assertion that would fail the moment
        // somebody makes the match "more helpful".
        fixture.EnsureAvailable();

        var address = $"sql-{Guid.NewGuid():N}@example.com";
        await PlaceAsync(address);

        var query = BookerEmailQuery.Create(address, 0, 50);
        Assert.True(query.Succeeded);

        var interceptor = new CommandRecordingInterceptor();
        await using var context = fixture.CreateContext(interceptor);

        await new SqlBookingManagementStore(context).FindByBookerEmailAsync(query.Value, Ct);

        var commands = interceptor.Commands.Where(c => c.Contains("uBookItBooking", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(commands);

        foreach (var command in commands)
        {
            Assert.DoesNotContain("LIKE", command, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains(commands, c => c.Contains("[BookerEmail] = ", StringComparison.Ordinal));
    }

    /// <summary>One index over a column, and whether it enforces uniqueness.</summary>
    private sealed record IndexRow(string Name, bool IsUnique);
}
