using Microsoft.EntityFrameworkCore;
using UBookIt.Core;
using UBookIt.Core.Bookings;
using UBookIt.Core.Stores;
using UBookIt.Persistence.Stores;
using UBookIt.Tests.Integration.Support;

namespace UBookIt.Tests.Integration;

/// <summary>
/// The management list against real SQL Server: the overlap window, the status default,
/// the resource filter, stable paging, and the resource-name join.
///
/// <para>
/// <b>Every test books its own window, and that is what makes the assertions absolute.</b>
/// The database is a shared collection fixture, so a suite asserting over "all bookings"
/// can only ever assert "at least the ones I seeded" — which cannot catch a query that
/// returns too much. Each test here uses a far-future window of its own, so within that
/// window the expected set is exactly known and "too many" fails as loudly as "too few".
/// </para>
/// </summary>
[Collection(SqlServerCollection.Name)]
public class BookingManagementStoreTests(SqlServerFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    /// How far apart two tests' windows sit. Tests deliberately seed bookings
    /// <b>outside</b> their own window — that is how "outside is excluded" is asserted at
    /// all — so the gap between windows must be wider than the furthest any fixture
    /// reaches. The furthest today is <c>to.AddDays(3)</c>; ten days leaves room.
    /// </summary>
    private const int WindowStrideDays = 10;

    /// <summary>
    /// A one-day window no other test in this class reaches into, including with the
    /// out-of-window bookings tests seed on purpose.
    /// <para>
    /// <b>The stride is load-bearing and was originally wrong.</b> With windows one day
    /// apart, <c>Matches_by_overlap_not_containment</c>'s "outside" booking at
    /// <c>to.AddDays(3)</c> landed exactly inside <c>WindowOn(4)</c> — the window whose
    /// unfiltered exact-set assertion then counted it. The suite passed only because
    /// xUnit happened to order the cases favourably; renaming a method flipped it.
    /// </para>
    /// </summary>
    private static (DateTimeOffset From, DateTimeOffset To) WindowOn(int index)
    {
        var from = new DateTimeOffset(2031, 1, 1, 0, 0, 0, TimeSpan.Zero)
            .AddDays(index * WindowStrideDays);

        return (from, from.AddDays(1));
    }

    /// <summary>
    /// Asserts a window is empty before a test seeds into it.
    /// <para>
    /// The isolation above is a claim about arithmetic, and the arithmetic was wrong
    /// once. Any test making an exact-set assertion over an <b>unfiltered</b> query calls
    /// this first, so a future collision fails here — naming the window — rather than as
    /// an off-by-one in an unrelated expected set.
    /// </para>
    /// </summary>
    private async Task AssertWindowIsUnusedAsync(DateTimeOffset from, DateTimeOffset to)
    {
        var existing = await ListAsync(Query(from, to, Statuses: Enum.GetValues<BookingStatus>()));

        Assert.True(
            existing.Total == 0,
            $"The window [{from:O}, {to:O}) already holds {existing.Total} booking(s) before "
            + "this test seeded anything. Another test's fixtures reach into it — widen "
            + $"{nameof(WindowStrideDays)} or move that test's out-of-window bookings.");
    }

    private async Task<Guid> PlaceAsync(Guid resourceId, DateTimeOffset startUtc, TimeSpan duration,
        BookingStatus status = BookingStatus.Confirmed)
    {
        await using var context = fixture.CreateContext();
        var store = new SqlBookingStore(context);

        var placed = await store.PlaceAsync(
            Seed.ConfirmedBooking(resourceId, startUtc, duration, status), Ct);

        Assert.True(placed.Succeeded);
        return placed.Value.Id;
    }

    /// <summary>A booking claiming two resources, built through the same rehydration surface Seed uses.</summary>
    private async Task<Guid> PlaceTwoResourceAsync(
        Guid firstResourceId, Guid secondResourceId, DateTimeOffset startUtc, TimeSpan duration)
    {
        var booking = Booking.Rehydrate(
            Guid.NewGuid(),
            new RandomBookingReferenceFactory().Next(),
            BookingInterval.Create(startUtc, startUtc + duration, "UTC").Value,
            Booker.Create(null, "Integration Tester", "integration@example.com", null).Value,
            [new ResourceClaim(firstResourceId), new ResourceClaim(secondResourceId)],
            BookingStatus.Confirmed,
            new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero)).Value;

        await using var context = fixture.CreateContext();
        var placed = await new SqlBookingStore(context).PlaceAsync(booking, Ct);

        Assert.True(placed.Succeeded);
        return placed.Value.Id;
    }

    /// <summary>
    /// A valid query, or a failed test. These suites all use windows well inside the
    /// guardrail, so a failure here means the fixture is wrong rather than the store —
    /// asserting it keeps that mistake from arriving later as an unexplained empty page.
    /// </summary>
    private static BookingQuery Query(
        DateTimeOffset from,
        DateTimeOffset to,
        IReadOnlyCollection<BookingStatus>? Statuses = null,
        IReadOnlyCollection<Guid>? ResourceIds = null,
        int Skip = 0,
        int Take = 50)
    {
        var result = BookingQuery.Create(
            from,
            to,
            new SiteBookingSettings { TimeZoneId = "UTC" },
            Statuses,
            ResourceIds,
            Skip,
            Take);

        Assert.True(result.Succeeded);
        return result.Value;
    }

    private async Task<BookingPage> ListAsync(BookingQuery query)
    {
        await using var context = fixture.CreateContext();
        return await new SqlBookingManagementStore(context).ListAsync(query, Ct);
    }

    [Fact]
    public async Task Matches_by_overlap_not_containment()
    {
        fixture.EnsureAvailable();

        var (from, to) = WindowOn(0);
        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);

        // Straddling the start, straddling the end, wholly inside, and wholly outside.
        // A naive `StartUtc >= from` predicate silently drops the first of these, which
        // is exactly the booking a person looking at "this week" most wants to see.
        var straddlesStart = await PlaceAsync(resourceId, from.AddHours(-2), TimeSpan.FromHours(4));
        var straddlesEnd = await PlaceAsync(resourceId, to.AddHours(-2), TimeSpan.FromHours(4));
        var inside = await PlaceAsync(resourceId, from.AddHours(9), TimeSpan.FromHours(1));
        var outside = await PlaceAsync(resourceId, to.AddDays(3), TimeSpan.FromHours(1));

        var page = await ListAsync(Query(from, to, ResourceIds: [resourceId]));
        var ids = page.Items.Select(item => item.BookingId).ToList();

        Assert.Contains(straddlesStart, ids);
        Assert.Contains(straddlesEnd, ids);
        Assert.Contains(inside, ids);
        Assert.DoesNotContain(outside, ids);
        Assert.Equal(3, page.Total);
    }

    [Fact]
    public async Task Touching_the_window_edge_is_outside_it()
    {
        fixture.EnsureAvailable();

        var (from, to) = WindowOn(1);
        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);

        // Half-open [from, to): a booking ending exactly at `from` and one starting
        // exactly at `to` touch without overlapping. Getting this wrong shows a booking
        // on two adjacent days at once.
        await PlaceAsync(resourceId, from.AddHours(-1), TimeSpan.FromHours(1));
        await PlaceAsync(resourceId, to, TimeSpan.FromHours(1));

        var page = await ListAsync(Query(from, to, ResourceIds: [resourceId]));

        Assert.Empty(page.Items);
        Assert.Equal(0, page.Total);
    }

    [Fact]
    public async Task Defaults_to_blocking_statuses_and_cancelled_are_reachable()
    {
        fixture.EnsureAvailable();

        var (from, to) = WindowOn(2);
        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);

        var confirmed = await PlaceAsync(resourceId, from.AddHours(9), TimeSpan.FromHours(1));
        var requested = await PlaceAsync(
            resourceId, from.AddHours(11), TimeSpan.FromHours(1), BookingStatus.Requested);
        var cancelled = await PlaceAsync(
            resourceId, from.AddHours(13), TimeSpan.FromHours(1), BookingStatus.Cancelled);

        // The fixture deliberately holds one of each kind, so a query returning
        // everything and a query returning the right thing are distinguishable.
        var byDefault = await ListAsync(Query(from, to, ResourceIds: [resourceId]));

        // Both sides ordered by the same comparer. Ordering only the actual side made
        // this pass or fail on which GUIDs the run happened to generate.
        Assert.Equal(
            new[] { confirmed, requested }.Order(),
            byDefault.Items.Select(item => item.BookingId).Order());
        Assert.Equal(2, byDefault.Total);

        var justCancelled = await ListAsync(
            Query(from, to, Statuses: [BookingStatus.Cancelled], ResourceIds: [resourceId]));

        Assert.Equal(cancelled, Assert.Single(justCancelled.Items).BookingId);
    }

    [Fact]
    public async Task A_booking_claiming_two_resources_is_returned_once_with_both()
    {
        fixture.EnsureAvailable();

        var (from, to) = WindowOn(3);
        var first = await Seed.EveryDayRoomAsync(fixture, Ct);
        var second = await Seed.EveryDayRoomAsync(fixture, Ct);

        var bookingId = await PlaceTwoResourceAsync(first, second, from.AddHours(9), TimeSpan.FromHours(1));

        // The join fanning out is the likeliest defect in this store, and neither a
        // single-claim fixture nor a filter naming one of the two resources can see it.
        // Filtering on BOTH is the case that duplicates if the query is rooted on claims.
        var page = await ListAsync(Query(from, to, ResourceIds: [first, second]));

        var only = Assert.Single(page.Items);
        Assert.Equal(bookingId, only.BookingId);
        Assert.Equal(1, page.Total);
        Assert.Equal(
            new[] { first, second }.Order(),
            only.Resources.Select(r => r.ResourceId).Order());

        // And unfiltered over the same window, still once.
        var unfiltered = await ListAsync(Query(from, to));
        Assert.Single(unfiltered.Items, item => item.BookingId == bookingId);
    }

    [Fact]
    public async Task Resource_filter_matches_any_named_resource_and_an_empty_set_filters_nothing()
    {
        fixture.EnsureAvailable();

        var (from, to) = WindowOn(4);

        // This test's last assertion is an exact set over an UNFILTERED query, so it is
        // the one that breaks if any other test's fixtures reach in here.
        await AssertWindowIsUnusedAsync(from, to);

        var first = await Seed.EveryDayRoomAsync(fixture, Ct);
        var second = await Seed.EveryDayRoomAsync(fixture, Ct);
        var third = await Seed.EveryDayRoomAsync(fixture, Ct);

        var onFirst = await PlaceAsync(first, from.AddHours(9), TimeSpan.FromHours(1));
        var onSecond = await PlaceAsync(second, from.AddHours(11), TimeSpan.FromHours(1));
        var onThird = await PlaceAsync(third, from.AddHours(13), TimeSpan.FromHours(1));

        var one = await ListAsync(Query(from, to, ResourceIds: [first]));
        Assert.Equal(onFirst, Assert.Single(one.Items).BookingId);

        var either = await ListAsync(Query(from, to, ResourceIds: [first, second]));
        Assert.Equal(
            new[] { onFirst, onSecond }.Order(),
            either.Items.Select(item => item.BookingId).Order());

        // Empty means "no filter", not "match nothing" — the reading that passes every
        // test written with a non-empty set.
        var empty = await ListAsync(Query(from, to, ResourceIds: []));
        Assert.Equal(
            new[] { onFirst, onSecond, onThird }.Order(),
            empty.Items.Select(item => item.BookingId).Order());
    }

    [Fact]
    public async Task Pages_stably_when_bookings_share_a_start_time()
    {
        fixture.EnsureAvailable();

        var (from, to) = WindowOn(5);
        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);
        var otherResourceId = await Seed.EveryDayRoomAsync(fixture, Ct);
        var thirdResourceId = await Seed.EveryDayRoomAsync(fixture, Ct);

        // THREE bookings at the SAME start time — on different resources, since the same
        // resource cannot be double-booked. A fixture of distinct start times passes
        // against an ordering with no tiebreak at all, which is the whole point of 3.1.
        var start = from.AddHours(9);
        var expected = new[]
        {
            await PlaceAsync(resourceId, start, TimeSpan.FromHours(1)),
            await PlaceAsync(otherResourceId, start, TimeSpan.FromHours(1)),
            await PlaceAsync(thirdResourceId, start, TimeSpan.FromHours(1)),
        }.Order().ToList();

        var seen = new List<Guid>();

        for (var skip = 0; skip < 3; skip++)
        {
            var page = await ListAsync(Query(from, to, Skip: skip, Take: 1));

            Assert.Equal(3, page.Total);
            seen.Add(Assert.Single(page.Items).BookingId);
        }

        // Read one at a time and concatenated: each exactly once, nothing repeated,
        // nothing skipped.
        Assert.Equal(expected, seen.Order());
        Assert.Equal(3, seen.Distinct().Count());

        // Paging agrees with a single unpaged read of the same window — the property
        // paging actually needs, and provider-agnostic in a way an absolute order is not.
        var whole = await ListAsync(Query(from, to, Take: 500));

        Assert.Equal(
            whole.Items.Where(item => expected.Contains(item.BookingId)).Select(item => item.BookingId),
            seen);

        // Two things learned here the hard way, both worth keeping.
        //
        // SQL Server orders `uniqueidentifier` by its LAST SIX BYTES, which is not
        // .NET's Guid.CompareTo order. So an assertion comparing this sequence against
        // ids sorted in memory fails even when the store is correct — the expectation is
        // wrong, not the query. That is why the set comparison above uses .Order() on
        // both sides and the sequence comparison is against the database's own answer.
        //
        // AND: dropping `.ThenBy(booking => booking.Id)` from the store does NOT fail
        // this test. Its determinism is not black-box observable here, because SQL
        // Server's incidental order for these rows is already stable. The tiebreak stays
        // because a total order is the contract and the incidental one is a plan detail
        // that an index, a parallel plan or another provider can change — but nobody
        // should believe this test is what protects it.
        Assert.Equal(3, whole.Items.Count(item => expected.Contains(item.BookingId)));
    }

    [Fact]
    public void The_generated_sql_orders_by_start_then_id()
    {
        fixture.EnsureAvailable();

        // The tiebreak's guard, and it has to be this rather than a behavioural one.
        //
        // `Id` is the clustered key, so SQL Server's incidental order for these rows
        // already equals `ORDER BY Id` — which means removing `.ThenBy(booking => b.Id)`
        // changes no result anywhere and every behavioural test stays green. Reading the
        // generated SQL is the one formulation that fails deterministically, needs no
        // fixture, and does not touch the database.
        var (from, to) = WindowOn(8);

        using var context = fixture.CreateContext();
        var store = new SqlBookingManagementStore(context);

        var sql = store.OrderedPage(Query(from, to)).ToQueryString();

        // Pin to the clause that orders the PAGE — the one immediately before OFFSET/FETCH
        // — rather than to whichever ORDER BY happens to come last.
        //
        // Taking the last one would be wrong in a way that reads as right. In the fully
        // projected query EF emits a second, outer ORDER BY to group the resource
        // collection, and it re-adds [Id] there as the parent identifier REGARDLESS of
        // whether this store asks for the tiebreak. A guard reading that clause would pass
        // in both directions. It reads correctly today only because this seam is
        // un-projected and carries exactly one ORDER BY — an accident of shape, not a
        // property anything guarantees.
        var offset = sql.IndexOf("OFFSET", StringComparison.Ordinal);

        Assert.True(
            offset >= 0,
            $"The paged query no longer emits OFFSET/FETCH, so there is no paging clause to "
            + $"check and this guard is not testing what it claims. SQL was: {sql}");

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
    public async Task Total_is_the_unpaged_count()
    {
        fixture.EnsureAvailable();

        var (from, to) = WindowOn(6);
        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);

        for (var hour = 9; hour < 13; hour++)
        {
            await PlaceAsync(resourceId, from.AddHours(hour), TimeSpan.FromMinutes(30));
        }

        // Take smaller than the match count, so returning Items.Count as the total fails.
        var page = await ListAsync(Query(from, to, ResourceIds: [resourceId], Take: 2));

        Assert.Equal(2, page.Items.Count);
        Assert.Equal(4, page.Total);
    }

    [Fact]
    public async Task Carries_the_resource_names_and_the_booker()
    {
        fixture.EnsureAvailable();

        var (from, to) = WindowOn(7);

        // Names that are NOT derivable from the ids, so a store fabricating a name out of
        // the id it already holds fails here. The default seed name is `Room {id:N}`,
        // which cannot tell a real join from that.
        var first = await Seed.EveryDayRoomAsync(fixture, Ct, displayName: "The Gilded Parlour");
        var second = await Seed.EveryDayRoomAsync(fixture, Ct, displayName: "Bricklayers Arms");

        await PlaceTwoResourceAsync(first, second, from.AddHours(9), TimeSpan.FromHours(1));

        var page = await ListAsync(Query(from, to, ResourceIds: [first]));
        var only = Assert.Single(page.Items);

        // Each id paired with ITS OWN name, so a join pairing a booking with the wrong
        // resource is visible too — two distinct names is what makes that observable.
        Assert.Equal(2, only.Resources.Count);
        Assert.Contains(
            only.Resources,
            resource => resource.ResourceId == first && resource.DisplayName == "The Gilded Parlour");
        Assert.Contains(
            only.Resources,
            resource => resource.ResourceId == second && resource.DisplayName == "Bricklayers Arms");

        Assert.Equal("Integration Tester", only.BookerName);
        Assert.Equal("integration@example.com", only.BookerEmail);
        Assert.Equal(BookingStatus.Confirmed, only.Status);
        Assert.Equal(from.AddHours(9), only.Interval.StartUtc);
    }
}
