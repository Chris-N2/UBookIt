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
    /// A window no other test's fixtures reach into. Each caller takes its own day, so
    /// two tests running against the same shared database cannot see each other's rows.
    /// </summary>
    private static (DateTimeOffset From, DateTimeOffset To) WindowOn(int dayOffset)
    {
        var from = new DateTimeOffset(2031, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(dayOffset);
        return (from, from.AddDays(1));
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
        var first = await Seed.EveryDayRoomAsync(fixture, Ct);
        var second = await Seed.EveryDayRoomAsync(fixture, Ct);

        await PlaceTwoResourceAsync(first, second, from.AddHours(9), TimeSpan.FromHours(1));

        // Names read back from the resources themselves, so a join pairing a booking
        // with the wrong resource is visible — a single-resource fixture could not tell.
        await using var context = fixture.CreateContext();
        var expected = await new SqlResourceStore(context).GetAsync(first, Ct);

        var page = await ListAsync(Query(from, to, ResourceIds: [first]));
        var only = Assert.Single(page.Items);

        Assert.Contains(
            only.Resources,
            resource => resource.ResourceId == first && resource.DisplayName == expected!.DisplayName);
        Assert.Equal(2, only.Resources.Count);
        Assert.All(only.Resources, resource => Assert.False(string.IsNullOrWhiteSpace(resource.DisplayName)));

        Assert.Equal("Integration Tester", only.BookerName);
        Assert.Equal("integration@example.com", only.BookerEmail);
        Assert.Equal(BookingStatus.Confirmed, only.Status);
        Assert.Equal(from.AddHours(9), only.Interval.StartUtc);
    }
}
