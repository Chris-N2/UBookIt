using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Core.Services;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The service-level start-alignment check (service-booking spec, "A permanent
/// start-grid misalignment is detectable", and design D1/D2/D4/D5).
/// <para>
/// Every fixture here varies what the scenario claims to be about: pools hold
/// more than one resource, the two granularities are 30 and 20 — equal to
/// neither each other nor coprime, so <c>gcd</c> is doing real work at 10 — and
/// the resource that saves a pool is never the lowest-id one. A degenerate pool
/// would let "one aligning candidate is enough" pass without the quantifier
/// being right.
/// </para>
/// </summary>
public class StartAlignmentTests
{
    private const string Therapist = "therapist";

    private const string Chair = "chair";

    private static readonly DayOfWeek OpenDay = TestData.BaseDate.DayOfWeek;

    private static Guid Id(int n) => new($"00000000-0000-0000-0000-{n:x12}");

    /// <summary>
    /// A resource open from <paramref name="open"/> until 18:00 on the given days,
    /// stepping in <paramref name="granularityMinutes"/>.
    /// </summary>
    private static Resource Open(
        string type, int id, string name, string open, int granularityMinutes, params DayOfWeek[] days)
        => Resource.Create(
            type,
            name,
            availability: TestData.Config(
                TestData.Weekly(open, "18:00", days.Length == 0 ? new[] { OpenDay } : days),
                constraints: Constraints(granularityMinutes)),
            id: Id(id)).Value;

    private static BookingConstraints Constraints(int granularityMinutes)
        => BookingConstraints.Create(
            granularity: TimeSpan.FromMinutes(granularityMinutes),
            minDuration: TimeSpan.FromMinutes(granularityMinutes),
            maxDuration: TimeSpan.FromMinutes(granularityMinutes * 4)).Value;

    /// <summary>
    /// The finding for a saved service, computed from the candidate pools the
    /// booking path resolves — never from a second eligibility filter of this
    /// test's own (⑧a design D1).
    /// </summary>
    private static async Task<RoleMisalignment?> Check(ServiceBookingService booking, Guid serviceId)
    {
        var pools = await booking.ResolveCandidatesAsync(serviceId);

        Assert.True(pools.Succeeded, "resolution failed");

        return StartAlignment.FindMisalignment(pools.Value);
    }

    private static Service ServiceOf(params string[] resourceTypes)
        => Service.Create(
            "Treatment",
            ServiceDuration.Variable(null, null).Value,
            resourceTypes.Select(t => ServiceRole.Create(t, null).Value)).Value;

    private static (ServiceBookingService Booking, Service Service, BookingService Bookings) Wire(
        Service service, params Resource[] resources)
        => WireAt(null, service, resources);

    /// <summary>
    /// The same wiring with "now" moved, for the cases that query a date the
    /// default clock has already passed — lead time would otherwise empty the
    /// projection and a test about alignment would pass for the wrong reason.
    /// </summary>
    private static (ServiceBookingService Booking, Service Service, BookingService Bookings) WireAt(
        DateTimeOffset? nowUtc, Service service, params Resource[] resources)
    {
        var resourceStore = new InMemoryResourceStore();
        foreach (var resource in resources)
        {
            resourceStore.Add(resource);
        }

        var services = new InMemoryServiceStore().Add(service);
        var wired = TestData.ServiceBookingWith(services, resourceStore, nowUtc);

        return (wired.Services, service, wired.Bookings);
    }

    [Fact]
    public async Task Spec_scenario_grids_that_can_never_coincide_are_reported()
    {
        // The worked example: 09:00/30 against 09:15/20. Both roles resolve
        // healthily and the service can never be booked.
        var (booking, service, _) = Wire(
            ServiceOf(ResourceTypes.Room, Therapist),
            Open(ResourceTypes.Room, 1, "Red Room", "09:00", 30),
            Open(Therapist, 2, "Mary", "09:15", 20));

        var finding = await Check(booking, service.Id);

        Assert.NotNull(finding);

        // Named by role, by resource, and by the two numbers an editor changes.
        Assert.Equal(ResourceTypes.Room, finding.First.Role.ResourceType);
        Assert.Equal("Red Room", finding.First.Resource.DisplayName);
        Assert.Equal(new TimeOnly(9, 0), finding.First.WindowStart);
        Assert.Equal(TimeSpan.FromMinutes(30), finding.First.Granularity);

        Assert.Equal(Therapist, finding.Second.Role.ResourceType);
        Assert.Equal("Mary", finding.Second.Resource.DisplayName);
        Assert.Equal(new TimeOnly(9, 15), finding.Second.WindowStart);
        Assert.Equal(TimeSpan.FromMinutes(20), finding.Second.Granularity);
    }

    [Fact]
    public async Task Spec_scenario_grids_that_can_coincide_are_not_reported()
    {
        // gcd(30, 20) = 10 divides the 30-minute offset, so 09:00 + 30 + 30 =
        // 09:30 + 20 + 20 + 20 = 10:30 really is a shared start.
        var (booking, service, _) = Wire(
            ServiceOf(ResourceTypes.Room, Therapist),
            Open(ResourceTypes.Room, 1, "Red Room", "09:00", 30),
            Open(Therapist, 2, "Mary", "09:30", 20));

        Assert.Null(await Check(booking, service.Id));
    }

    [Fact]
    public async Task Spec_scenario_one_aligning_candidate_is_enough_to_stay_silent()
    {
        // A large pool with one awkward member. The awkward room holds the LOWEST
        // id, so a check that stopped at the first candidate — or that reported
        // the first pairing it disliked — would fire here.
        var (booking, service, _) = Wire(
            ServiceOf(ResourceTypes.Room, Therapist),
            Open(ResourceTypes.Room, 1, "Red Room", "09:15", 30),
            Open(ResourceTypes.Room, 2, "Blue Room", "09:00", 30),
            Open(ResourceTypes.Room, 3, "Green Room", "09:20", 30),
            Open(Therapist, 4, "Mary", "09:00", 20));

        Assert.Null(await Check(booking, service.Id));
    }

    [Fact]
    public async Task Spec_scenario_a_pool_where_every_pairing_fails_is_reported()
    {
        // Two candidates on each side, four pairings, none of which can meet:
        // every room opens 15 minutes past a multiple of 10 from every therapist.
        var (booking, service, _) = Wire(
            ServiceOf(ResourceTypes.Room, Therapist),
            Open(ResourceTypes.Room, 1, "Red Room", "09:15", 30),
            Open(ResourceTypes.Room, 2, "Blue Room", "09:45", 30),
            Open(Therapist, 3, "Mary", "09:00", 20),
            Open(Therapist, 4, "Frank", "09:10", 20));

        var finding = await Check(booking, service.Id);

        Assert.NotNull(finding);
        Assert.Equal("Red Room", finding.First.Resource.DisplayName);
        Assert.Equal("Mary", finding.Second.Resource.DisplayName);
    }

    [Fact]
    public async Task Spec_scenario_a_misalignment_on_one_day_only_is_not_permanent()
    {
        // The room misses the therapist on Mondays and meets her on Tuesdays. A
        // bookable start exists, so there is nothing permanent to report — and a
        // check that stopped at the first day it disliked would cry wolf every
        // week.
        var room = Resource.Create(
            ResourceTypes.Room,
            "Red Room",
            availability: TestData.Config(
                WeeklyOpenHours.Create(
                [
                    (DayOfWeek.Monday, TestData.Win("09:15", "18:00")),
                    (DayOfWeek.Tuesday, TestData.Win("09:00", "18:00")),
                ]).Value,
                constraints: Constraints(30)),
            id: Id(1)).Value;

        var (booking, service, _) = Wire(
            ServiceOf(ResourceTypes.Room, Therapist),
            room,
            Open(Therapist, 2, "Mary", "09:00", 20, DayOfWeek.Monday, DayOfWeek.Tuesday));

        Assert.Null(await Check(booking, service.Id));
    }

    [Fact]
    public async Task A_clash_on_every_day_both_are_open_is_still_reported()
    {
        // The pair that makes the test above non-vacuous: the same two-day shape,
        // with the aligning day removed rather than the arithmetic changed.
        var room = Resource.Create(
            ResourceTypes.Room,
            "Red Room",
            availability: TestData.Config(
                WeeklyOpenHours.Create(
                [
                    (DayOfWeek.Monday, TestData.Win("09:15", "18:00")),
                    (DayOfWeek.Tuesday, TestData.Win("09:45", "18:00")),
                ]).Value,
                constraints: Constraints(30)),
            id: Id(1)).Value;

        var (booking, service, _) = Wire(
            ServiceOf(ResourceTypes.Room, Therapist),
            room,
            Open(Therapist, 2, "Mary", "09:00", 20, DayOfWeek.Monday, DayOfWeek.Tuesday));

        Assert.NotNull(await Check(booking, service.Id));
    }

    [Fact]
    public async Task Spec_scenario_three_roles_are_checked_pairwise()
    {
        // `chair` aligns with both others — gcd(25, 30) = 5 divides the 15-minute
        // offset to the room, and gcd(25, 20) = 5 divides the zero offset to the
        // therapist — so the only clashing pair is room/therapist, and that pair
        // is what comes back. The third role is not required to be involved.
        var (booking, service, _) = Wire(
            ServiceOf(ResourceTypes.Room, Therapist, Chair),
            Open(ResourceTypes.Room, 1, "Red Room", "09:15", 30),
            Open(Therapist, 2, "Mary", "09:00", 20),
            Open(Chair, 3, "Recliner", "09:00", 25));

        var finding = await Check(booking, service.Id);

        Assert.NotNull(finding);
        Assert.Equal(
            new[] { ResourceTypes.Room, Therapist },
            new[] { finding.First.Role.ResourceType, finding.Second.Role.ResourceType });
    }

    [Fact]
    public async Task A_single_role_service_never_reports_a_misalignment()
    {
        // There is no second grid to miss. Asserted with a resource whose opening
        // time is exactly the awkward one from the worked example, so the answer
        // comes from there being one role rather than from the arithmetic.
        var (booking, service, _) = Wire(
            ServiceOf(ResourceTypes.Room),
            Open(ResourceTypes.Room, 1, "Red Room", "09:15", 30));

        Assert.Null(await Check(booking, service.Id));
    }

    [Fact]
    public async Task A_role_nothing_can_fill_is_not_reported_as_a_misalignment()
    {
        // An empty pool has no window to name and is a different fault, already
        // reported by the resolution chain. Saying "these two can never meet"
        // about a role with no resources would send an editor to fix opening
        // hours that do not exist.
        var (booking, service, _) = Wire(
            ServiceOf(ResourceTypes.Room, Therapist),
            Open(ResourceTypes.Room, 1, "Red Room", "09:15", 30));

        Assert.Null(await Check(booking, service.Id));
    }

    // ------------------------------------------------- the claim is one-directional

    [Fact]
    public async Task Spec_scenario_alignment_is_never_reported_as_a_positive_finding()
    {
        // Design D1. The roles' grids coincide, and the service still has no
        // bookable start because the only shared one is taken. Silence is
        // therefore not a promise — nothing may read the absence of a finding as
        // "this service can be booked".
        var (booking, service, _) = Wire(
            ServiceOf(ResourceTypes.Room, Therapist),
            OpenUntil(ResourceTypes.Room, 1, "Red Room", "09:00", "10:00", 30),
            OpenUntil(Therapist, 2, "Mary", "09:00", "10:00", 20));

        // The one start both offer, booked away.
        var placed = await booking.PlaceAsync(new ServiceBookingRequest
        {
            ServiceId = service.Id,
            Start = TestData.Utc(TestData.BaseDate, "09:00"),
            Duration = TimeSpan.FromMinutes(60),
            Booker = TestData.Booker(),
        });

        Assert.True(placed.Succeeded, string.Join("; ", placed.Failures.Select(f => f.Code)));

        Assert.Null(await Check(booking, service.Id));

        var starts = await booking.GetBookableStartsAsync(service.Id, TestData.BaseDate, TestData.BaseDate);

        Assert.True(starts.Succeeded);
        Assert.Empty(starts.Value);
    }

    [Fact]
    public async Task Spec_scenario_bookings_do_not_change_the_answer()
    {
        // Design D2: computed from open hours, not free time. A structural fault
        // must not appear and disappear as bookings come and go — and, just as
        // importantly, a busy week must not be reported as one.
        var (booking, service, bookings) = Wire(
            ServiceOf(ResourceTypes.Room, Therapist),
            OpenUntil(ResourceTypes.Room, 1, "Red Room", "09:00", "10:00", 30),
            OpenUntil(Therapist, 2, "Mary", "09:15", "10:15", 20));

        var before = await Check(booking, service.Id);
        Assert.NotNull(before);

        // The service itself can never be placed — that is the fault under test —
        // so the room's calendar is filled directly, exactly as an unrelated
        // booking of that room would.
        var filled = await bookings.PlaceAsync(new MultiClaimBookingRequest
        {
            ResourceIds = [Id(1)],
            Start = TestData.Utc(TestData.BaseDate, "09:00"),
            Duration = TimeSpan.FromMinutes(60),
            Booker = TestData.Booker(),
        });

        Assert.True(filled.Succeeded, string.Join("; ", filled.Failures.Select(f => f.Code)));

        Assert.Equal(before, await Check(booking, service.Id));
    }

    /// <summary>A resource with a bounded window, for the cases that fill it.</summary>
    private static Resource OpenUntil(
        string type, int id, string name, string open, string close, int granularityMinutes)
        => Resource.Create(
            type,
            name,
            availability: TestData.Config(
                TestData.Weekly(open, close, OpenDay),
                constraints: Constraints(granularityMinutes)),
            id: Id(id)).Value;

    // ------------------------------------------------------ nothing else changed

    [Fact]
    public async Task Spec_scenario_detection_changes_nothing_else()
    {
        // Resolution, saving, availability and placement all behave exactly as
        // they did before this check existed. Verified rather than assumed: the
        // save is performed, and availability's empty result is read back.
        var resources = new InMemoryResourceStore()
            .Add(Open(ResourceTypes.Room, 1, "Red Room", "09:00", 30))
            .Add(Open(Therapist, 2, "Mary", "09:15", 20));

        var services = new InMemoryServiceStore();
        var booking = TestData.ServiceBooking(services, resources);

        var created = Service.Create(
            "Treatment",
            ServiceDuration.Variable(null, null).Value,
            [ServiceRole.Create(ResourceTypes.Room, null).Value, ServiceRole.Create(Therapist, null).Value]);

        // A misaligned configuration is a legitimate service: it validates and it
        // saves.
        Assert.True(created.Succeeded);
        Assert.True((await services.CreateAsync(created.Value)).Succeeded);

        var service = created.Value;

        // Resolution still returns both pools, full.
        var pools = await booking.ResolveCandidatesAsync(service.Id);
        Assert.True(pools.Succeeded);
        Assert.Equal(2, pools.Value.Count);
        Assert.All(pools.Value, pool => Assert.Single(pool.Candidates));

        // And it is the check, not resolution, that knows about the clash.
        Assert.NotNull(StartAlignment.FindMisalignment(pools.Value));

        // Availability answers as it always did: an empty result, not a failure.
        var starts = await booking.GetBookableStartsAsync(service.Id, TestData.BaseDate, TestData.BaseDate);
        Assert.True(starts.Succeeded);
        Assert.Empty(starts.Value);

        // Placement refuses in the ordinary way rather than throwing or acquiring
        // a new code of its own.
        var placed = await booking.PlaceAsync(new ServiceBookingRequest
        {
            ServiceId = service.Id,
            Start = TestData.Utc(TestData.BaseDate, "09:00"),
            Duration = TimeSpan.FromMinutes(60),
            Booker = TestData.Booker(),
        });

        Assert.False(placed.Succeeded);
        Assert.Contains(placed.Failures, f => f.Code == FailureCodes.ServiceUnavailable);
    }

    // ------------------------------------------------- daylight saving (design D5)

    /// <summary>
    /// A resource open on one weekday only, from <paramref name="open"/> to
    /// <paramref name="close"/> — for the transition-date cases, which need
    /// windows long enough to straddle 01:00 local.
    /// </summary>
    private static Resource OnDay(
        string type, int id, string name, string open, string close, int granularityMinutes, DayOfWeek day)
        => Resource.Create(
            type,
            name,
            availability: TestData.Config(
                TestData.Weekly(open, close, day),
                constraints: Constraints(granularityMinutes)),
            id: Id(id)).Value;

    /// <summary>The Sunday Europe/London springs forward, 2026-03-29 at 01:00 local.</summary>
    private static readonly DateOnly SpringForward = new(2026, 3, 29);

    [Fact]
    public async Task A_daylight_saving_transition_between_two_windows_is_never_reported_as_a_clash()
    {
        // QA CRITICAL. The wall-clock offset between these two windows is 180
        // minutes, which gcd(8, 16) = 8 does not divide — so on wall-clock
        // arithmetic alone this pair reads as permanently misaligned. It is not:
        // the transition at 01:00 falls between the two window starts, so the
        // real UTC offset on this date is 120 minutes, which 8 divides exactly,
        // and the service really can be booked.
        //
        // Design D5 claimed two windows in one zone shift together. They do —
        // unless the transition falls between them, which is precisely this case.
        var (booking, service, _) = WireAt(
            new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
            ServiceOf(ResourceTypes.Room, Therapist),
            OnDay(ResourceTypes.Room, 1, "Red Room", "00:00", "18:00", 8, SpringForward.DayOfWeek),
            OnDay(Therapist, 2, "Mary", "03:00", "18:00", 16, SpringForward.DayOfWeek));

        // Silent — and the shared starts below are why that silence is required
        // rather than merely tolerable.
        Assert.Null(await Check(booking, service.Id));

        var starts = await booking.GetBookableStartsAsync(service.Id, SpringForward, SpringForward);

        Assert.True(starts.Succeeded);
        Assert.NotEmpty(starts.Value);
    }

    [Theory]
    [InlineData(20, 20, "09:10")]  // gcd 20 — two resources on the same ordinary grid
    [InlineData(60, 60, "09:30")]  // gcd 60 — the coarsest ordinary pair
    [InlineData(20, 60, "09:10")]  // gcd 20
    [InlineData(12, 12, "09:06")]  // gcd 12
    [InlineData(4, 8, "09:02")]    // gcd 4
    public async Task A_granularity_pair_in_ordinary_use_is_still_reported(
        int firstStep, int secondStep, string secondOpen)
    {
        // The guard's cost boundary, asserted THROUGH the real check rather than
        // against a restatement of its arithmetic.
        //
        // QA MAJOR (round 2): this test previously reimplemented gcd locally and
        // asserted that an hour divides it — an assertion about arithmetic, which
        // does not regress. Narrowing the guard's divisor from an hour to half of
        // one left all 529 tests green while silencing exactly these five pairs,
        // and the dead test's own failure message claimed to be guarding them.
        //
        // These are the gcds that divide an hour but not half of one — 4, 12, 20
        // and 60 minutes — so they are precisely what the chosen divisor buys.
        // Narrowing it makes every case here go silent, and this red.
        var (booking, service, _) = Wire(
            ServiceOf(ResourceTypes.Room, Therapist),
            Open(ResourceTypes.Room, 1, "Red Room", "09:00", firstStep),
            Open(Therapist, 2, "Mary", secondOpen, secondStep));

        Assert.NotNull(await Check(booking, service.Id));
    }

    [Fact]
    public async Task A_step_pair_the_wall_clock_offset_cannot_settle_is_reported_on_no_day()
    {
        // The other half of the guard, and the price of it: 8 against 16 has a gcd
        // of 8, which does not divide an hour, so this pair is never reported —
        // not even on a Tuesday in September with no transition anywhere near it,
        // where the wall-clock arithmetic would in fact have been right.
        //
        // Asserted deliberately, so the cost of the guard is visible and a future
        // change cannot narrow it without a test going red.
        var (booking, service, _) = Wire(
            ServiceOf(ResourceTypes.Room, Therapist),
            Open(ResourceTypes.Room, 1, "Red Room", "09:00", 8),
            Open(Therapist, 2, "Mary", "09:03", 16));

        Assert.Null(await Check(booking, service.Id));
    }

    [Fact]
    public async Task One_unsettleable_candidate_pairing_silences_the_whole_role_pair()
    {
        // The guard clears the pair rather than skipping that one pairing, on the
        // same reasoning as an aligning pairing: the report is about a pair of
        // ROLES, and a role is a pool. If some candidate combination cannot be
        // ruled out, the roles cannot be said to never meet — so a pool holding
        // one unsettleable candidate is silent even though its other candidate
        // clashes outright.
        // Red Room against Mary is settleable — gcd(30, 16) = 2 divides an hour —
        // and clashes: 2 does not divide the 15-minute offset. Blue Room against
        // Mary is not settleable, because gcd(8, 16) = 8 does not divide an hour.
        var (booking, service, _) = Wire(
            ServiceOf(ResourceTypes.Room, Therapist),
            Open(ResourceTypes.Room, 1, "Red Room", "09:15", 30),
            Open(ResourceTypes.Room, 2, "Blue Room", "09:15", 8),
            Open(Therapist, 3, "Mary", "09:00", 16));

        Assert.Null(await Check(booking, service.Id));

        // And without the unsettleable room the same configuration IS reported,
        // so the silence above comes from the guard rather than from the pool
        // happening to align.
        var (narrower, narrowerService, _) = Wire(
            ServiceOf(ResourceTypes.Room, Therapist),
            Open(ResourceTypes.Room, 1, "Red Room", "09:15", 30),
            Open(Therapist, 3, "Mary", "09:00", 16));

        Assert.NotNull(await Check(narrower, narrowerService.Id));
    }

    // --------------------------------------------- bookings placed under old hours

    [Fact]
    public async Task A_booking_predating_an_opening_hours_change_does_not_silence_the_report()
    {
        // QA MAJOR, resolved as a spec amendment rather than a code change.
        //
        // D2's superset argument assumes every booking was placed under the
        // configuration now in force. This one was not: the room was booked
        // 09:00–09:30 while it opened at 09:00, and the opening time then moved to
        // 09:20. The free interval now begins at the old booking's end, 09:30,
        // which is NOT on the new 09:20/30 window grid — and the therapist's
        // 09:15/15 grid hits 09:30 exactly, so a shared start exists today.
        //
        // The report fires anyway, and that is the decision: it describes the
        // CONFIGURATION, which is permanently broken the moment the stale booking
        // clears. Falling silent would couple the diagnostic to the booking
        // calendar and bring back the flicker D2 exists to prevent.
        var resources = new InMemoryResourceStore()
            .Add(OpenUntil(ResourceTypes.Room, 1, "Red Room", "09:00", "17:00", 30))
            .Add(OpenUntil(Therapist, 2, "Mary", "09:15", "17:00", 15));

        var services = new InMemoryServiceStore().Add(ServiceOf(ResourceTypes.Room, Therapist));
        var wired = TestData.ServiceBookingWith(services, resources);
        var serviceId = (await services.ListAsync(0, 1)).Items[0].Id;

        // Valid under the hours in force at the time.
        var placed = await wired.Bookings.PlaceAsync(new MultiClaimBookingRequest
        {
            ResourceIds = [Id(1)],
            Start = TestData.Utc(TestData.BaseDate, "09:00"),
            Duration = TimeSpan.FromMinutes(30),
            Booker = TestData.Booker(),
        });

        Assert.True(placed.Succeeded, string.Join("; ", placed.Failures.Select(f => f.Code)));

        // The room's opening time moves; the booking stays where it was.
        resources.Add(OpenUntil(ResourceTypes.Room, 1, "Red Room", "09:20", "17:00", 30));

        var finding = await Check(wired.Services, serviceId);

        Assert.NotNull(finding);
        Assert.Equal(new TimeOnly(9, 20), finding.First.WindowStart);

        // The transient start the stale booking leaves behind, asserted rather
        // than described — this is the fact that makes the report's scope a
        // deliberate choice instead of an oversight.
        var starts = await wired.Services.GetBookableStartsAsync(
            serviceId, TestData.BaseDate, TestData.BaseDate);

        Assert.True(starts.Succeeded);
        Assert.Contains(starts.Value, s => s.StartUtc == TestData.Utc(TestData.BaseDate, "09:30"));
    }

    // --------------------------------------------------------- exception dates

    [Fact]
    public async Task An_exception_date_that_opens_different_hours_can_clear_a_clash()
    {
        // Design D5: an exception that opens different hours is a window like any
        // other on its date, so a pair that never meets on the weekly pattern can
        // still meet on it. The check compares both resources' EFFECTIVE windows
        // for that date, which is what makes this a real shared start rather than
        // a window paired against a day the other resource is shut.
        var room = Resource.Create(
            ResourceTypes.Room,
            "Red Room",
            availability: TestData.Config(
                TestData.Weekly("09:15", "18:00", OpenDay),
                exceptions: [DateException.Override(TestData.BaseDate, [TestData.Win("09:00", "18:00")]).Value],
                constraints: Constraints(30)),
            id: Id(1)).Value;

        var (booking, service, _) = Wire(
            ServiceOf(ResourceTypes.Room, Therapist),
            room,
            Open(Therapist, 2, "Mary", "09:00", 20));

        Assert.Null(await Check(booking, service.Id));
    }

    [Fact]
    public async Task A_closure_neither_creates_nor_clears_a_clash()
    {
        // A closed date contributes no windows, so it cannot pair with anything.
        // The weekly pattern still recurs on every other date of that day, so the
        // clash — and the answer — is unchanged.
        var room = Resource.Create(
            ResourceTypes.Room,
            "Red Room",
            availability: TestData.Config(
                TestData.Weekly("09:15", "18:00", OpenDay),
                exceptions: [DateException.Closure(TestData.BaseDate)],
                constraints: Constraints(30)),
            id: Id(1)).Value;

        var (booking, service, _) = Wire(
            ServiceOf(ResourceTypes.Room, Therapist),
            room,
            Open(Therapist, 2, "Mary", "09:00", 20));

        Assert.NotNull(await Check(booking, service.Id));
    }
}
