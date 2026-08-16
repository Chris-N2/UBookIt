using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Resources;
using UBookIt.Core.Services;
using UBookIt.Core.Stores;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// Composite availability across the roles of a multi-role service: the starts
/// every role can fulfil, and at each of those the lengths every role can
/// provide (service-booking spec, "Composite availability across roles").
/// <para>
/// The distinct-type cases below are now the <em>easy</em> half: their pools are
/// disjoint, so a saturating assignment exists exactly when each role
/// independently has a candidate, and the answer must be the one the intersection
/// gave before assignment existed. They are kept unchanged for that reason —
/// every one of them is a differential test (design D3).
/// </para>
/// <para>
/// The overlapping-pool cases at the bottom are the new ground: two roles of one
/// resource type, and counts above 1, where "every role has a candidate here" is
/// necessary and no longer sufficient.
/// </para>
/// </summary>
public class CompositeAvailabilityTests
{
    private const string Therapist = "therapist";

    private static readonly DateOnly Date = TestData.BaseDate;

    private static TimeSpan Mins(int minutes) => TimeSpan.FromMinutes(minutes);

    private static Guid Id(int n) => new($"00000000-0000-0000-0000-{n:x12}");

    private static Resource Res(
        int id,
        string type,
        int granularity,
        int min,
        int max,
        string open = "09:00",
        string close = "17:00")
        => Resource.Create(
            type,
            $"Resource {id}",
            availability: TestData.Config(
                TestData.Weekly(open, close, Date.DayOfWeek),
                constraints: BookingConstraints.Create(
                    granularity: Mins(granularity),
                    minDuration: Mins(min),
                    maxDuration: Mins(max)).Value),
            id: Id(id)).Value;

    private static Service Svc(ServiceDuration? duration, params string[] types)
        => Service.Create(
            "Massage",
            duration,
            types.Select(t => new ServiceRole(t, 1))).Value;

    /// <summary>The same, carrying capabilities, so two roles of one type can be told apart.</summary>
    private static Resource ResWith(
        int id,
        string type,
        int granularity,
        int min,
        int max,
        string[] capabilities,
        string open = "09:00",
        string close = "17:00")
        => Resource.Create(
            type,
            $"Resource {id}",
            capabilities: capabilities,
            availability: TestData.Config(
                TestData.Weekly(open, close, Date.DayOfWeek),
                constraints: BookingConstraints.Create(
                    granularity: Mins(granularity),
                    minDuration: Mins(min),
                    maxDuration: Mins(max)).Value),
            id: Id(id)).Value;

    /// <summary>Two roles of one resource type, distinguished by required capabilities.</summary>
    private static Service SameType(ServiceDuration? duration, string type, string[] first, string[] second)
        => Service.Create(
            "Joint session",
            duration,
            [ServiceRole.Create(type, first).Value, ServiceRole.Create(type, second).Value]).Value;

    /// <summary>One role requiring several distinct resources of one type.</summary>
    private static Service Counted(ServiceDuration? duration, string type, int count)
        => Service.Create("Workshop", duration, [new ServiceRole(type, count)]).Value;

    private sealed class Harness
    {
        public required ServiceBookingService Services { get; init; }

        public required IAvailabilityQueryService Availability { get; init; }

        public required IBookingService Bookings { get; init; }

        public required InMemoryBookingStore Store { get; init; }
    }

    private static Harness Wire(Service service, params Resource[] resources)
    {
        var resourceStore = new InMemoryResourceStore();
        foreach (var resource in resources)
        {
            resourceStore.Add(resource);
        }

        var serviceStore = new InMemoryServiceStore().Add(service);
        var bookingStore = new InMemoryBookingStore();
        var time = new FixedTimeProvider(TestData.Now);
        var settings = TestData.Settings;

        var availability = new AvailabilityService(resourceStore, bookingStore, time, settings);
        var bookings = new BookingService(resourceStore, bookingStore, time, settings);

        return new Harness
        {
            Services = new ServiceBookingService(
                serviceStore, resourceStore, bookingStore, availability, bookings, settings),
            Availability = availability,
            Bookings = bookings,
            Store = bookingStore,
        };
    }

    private static ServiceBookableStart At(IReadOnlyList<ServiceBookableStart> starts, string time)
        => Assert.Single(starts, s => s.StartUtc == TestData.Utc(Date, time));

    private static async Task<IReadOnlyList<ServiceBookableStart>> Starts(Harness harness, Service service)
    {
        var result = await harness.Services.GetBookableStartsAsync(service.Id, Date, Date);

        Assert.True(result.Succeeded);
        return result.Value;
    }

    // ------------------------------------------------------------ start intersection

    [Fact]
    public async Task Spec_scenario_a_start_is_offered_only_when_every_role_can_fulfil_it()
    {
        // The room can start at 10:00 and 11:00; the only therapist can start at
        // 11:00 alone. One booking, one start — so 10:00 is not a start of the
        // service however free the room is.
        var service = Svc(ServiceDuration.Fixed(Mins(60)).Value, ResourceTypes.Room, Therapist);
        var harness = Wire(
            service,
            Res(1, ResourceTypes.Room, granularity: 60, min: 60, max: 60, open: "10:00", close: "12:00"),
            Res(2, Therapist, granularity: 60, min: 60, max: 60, open: "11:00", close: "12:00"));

        var starts = await Starts(harness, service);

        Assert.Equal([TestData.Utc(Date, "11:00")], starts.Select(s => s.StartUtc));
    }

    [Fact]
    public async Task A_role_whose_pool_is_empty_makes_the_service_unbookable()
    {
        // No therapist exists at all. The room's own availability is untouched,
        // and says nothing about whether the service can be booked.
        var service = Svc(ServiceDuration.Fixed(Mins(60)).Value, ResourceTypes.Room, Therapist);
        var harness = Wire(service, Res(1, ResourceTypes.Room, granularity: 60, min: 60, max: 120));

        var starts = await Starts(harness, service);

        Assert.Empty(starts);
    }

    // ----------------------------------------------------------- length intersection

    [Fact]
    public async Task Spec_scenario_lengths_intersect_to_the_common_multiple()
    {
        // {30, 120, 30} ∩ {20, 120, 20} = {60, 120, 60}: the multiples of 60 in
        // the overlap. 30, 40, 80 and 90 are each bookable on one role and not
        // the other, so none of them is bookable for the service.
        var service = Svc(null, ResourceTypes.Room, Therapist);
        var harness = Wire(
            service,
            Res(1, ResourceTypes.Room, granularity: 30, min: 30, max: 120, open: "09:00", close: "11:00"),
            Res(2, Therapist, granularity: 20, min: 20, max: 120, open: "09:00", close: "11:00"));

        var nine = At(await Starts(harness, service), "09:00");

        Assert.Equal(new LengthRun(Mins(60), Mins(120), Mins(60)), Assert.Single(nine.Runs));

        foreach (var notOffered in new[] { 20, 30, 40, 80, 90, 100 })
        {
            Assert.False(nine.Admits(Mins(notOffered)), $"{notOffered} minutes is not bookable on both roles");
        }
    }

    [Fact]
    public async Task Spec_scenario_a_length_only_one_role_can_provide_is_not_offered()
    {
        // 30–60 against 90–120: the ranges do not overlap at all, so the start
        // goes rather than being offered with nothing bookable at it.
        var service = Svc(null, ResourceTypes.Room, Therapist);
        var harness = Wire(
            service,
            Res(1, ResourceTypes.Room, granularity: 30, min: 30, max: 60, open: "09:00", close: "11:00"),
            Res(2, Therapist, granularity: 30, min: 90, max: 120, open: "09:00", close: "11:00"));

        var starts = await Starts(harness, service);

        Assert.Empty(starts);
    }

    [Fact]
    public async Task Spec_scenario_differing_grids_narrow_rather_than_merge()
    {
        // 30 and 45 over the same range: only multiples of 90 are on both grids.
        var service = Svc(null, ResourceTypes.Room, Therapist);
        var harness = Wire(
            service,
            Res(1, ResourceTypes.Room, granularity: 30, min: 30, max: 450, open: "09:00", close: "16:30"),
            Res(2, Therapist, granularity: 45, min: 45, max: 450, open: "09:00", close: "16:30"));

        var nine = At(await Starts(harness, service), "09:00");

        Assert.Equal(new LengthRun(Mins(90), Mins(450), Mins(90)), Assert.Single(nine.Runs));
        Assert.All(
            nine.Runs.SelectMany(r => r.Lengths()),
            length => Assert.Equal(0, length.Ticks % Mins(90).Ticks));
    }

    [Fact]
    public async Task Spec_scenario_intersection_distributes_over_each_roles_union()
    {
        // The room role offers two distinct runs at 09:00 — two candidates on
        // different grids, neither subsuming the other — and the therapist role
        // one. The composite is the union of both pairwise intersections, not
        // the intersection of the outermost bounds.
        var service = Svc(null, ResourceTypes.Room, Therapist);
        var harness = Wire(
            service,
            Res(1, ResourceTypes.Room, granularity: 30, min: 30, max: 90, open: "09:00", close: "11:00"),
            Res(2, ResourceTypes.Room, granularity: 20, min: 20, max: 120, open: "09:00", close: "11:00"),
            Res(3, Therapist, granularity: 60, min: 60, max: 120, open: "09:00", close: "11:00"));

        var nine = At(await Starts(harness, service), "09:00");

        // {30,90,30} ∩ {60,120,60} = {60,60,60}; {20,120,20} ∩ {60,120,60} =
        // {60,120,60}. The first is a subset of the second, so elimination
        // leaves one run — and 120 minutes, which only the second pairing
        // offers, is bookable.
        Assert.Equal(new LengthRun(Mins(60), Mins(120), Mins(60)), Assert.Single(nine.Runs));
        Assert.True(nine.Admits(Mins(120)));
    }

    [Fact]
    public async Task Spec_scenario_role_order_is_not_observable_in_availability()
    {
        // The half of "role order is not observable" that the aggregate test
        // cannot reach: two services with the same roles supplied in opposite
        // orders must resolve to the same candidates and offer the same
        // availability. The roles differ in grid and range, so a fold that
        // depended on order would produce different runs rather than the same
        // ones.
        Resource[] Pool() =>
        [
            Res(1, ResourceTypes.Room, granularity: 30, min: 30, max: 450, open: "09:00", close: "16:30"),
            Res(2, Therapist, granularity: 45, min: 45, max: 450, open: "09:00", close: "16:30"),
        ];

        var duration = ServiceDuration.Variable(Mins(25), Mins(400)).Value;
        var forwards = Svc(duration, ResourceTypes.Room, Therapist);
        var backwards = Svc(duration, Therapist, ResourceTypes.Room);

        var forwardStarts = await Starts(Wire(forwards, Pool()), forwards);
        var backwardStarts = await Starts(Wire(backwards, Pool()), backwards);

        Assert.NotEmpty(forwardStarts);
        Assert.Equal(
            forwardStarts.Select(s => s.StartUtc),
            backwardStarts.Select(s => s.StartUtc));

        Assert.Equal(
            forwardStarts.Select(s => s.Runs.ToArray()),
            backwardStarts.Select(s => s.Runs.ToArray()));

        // And the candidate pools themselves, role by role.
        var forwardPools = (await Wire(forwards, Pool()).Services.ResolveCandidatesAsync(forwards.Id)).Value;
        var backwardPools = (await Wire(backwards, Pool()).Services.ResolveCandidatesAsync(backwards.Id)).Value;

        Assert.Equal(
            forwardPools.Select(p => (p.Role.ResourceType, p.Candidates.Select(c => c.ResourceId).ToArray())),
            backwardPools.Select(p => (p.Role.ResourceType, p.Candidates.Select(c => c.ResourceId).ToArray())));
    }

    [Fact]
    public async Task A_run_subsumed_by_a_coarser_grid_is_dropped()
    {
        // Intersection across roles produces nested runs on different steps far
        // more often than a single role's union did: two of the room role's runs
        // paired against the therapist's 60-minute grid give {60,120,60} and
        // {60,120,120}, and the second says nothing the first does not. An
        // elimination rule that required equal steps would emit both.
        var service = Svc(null, ResourceTypes.Room, Therapist);
        var harness = Wire(
            service,
            Res(1, ResourceTypes.Room, granularity: 60, min: 60, max: 120, open: "09:00", close: "11:00"),
            Res(2, ResourceTypes.Room, granularity: 120, min: 120, max: 120, open: "09:00", close: "11:00"),
            Res(3, Therapist, granularity: 60, min: 60, max: 120, open: "09:00", close: "11:00"));

        var nine = At(await Starts(harness, service), "09:00");

        Assert.Equal(new LengthRun(Mins(60), Mins(120), Mins(60)), Assert.Single(nine.Runs));

        // The elimination lost nothing: both lengths are still on offer.
        Assert.True(nine.Admits(Mins(60)));
        Assert.True(nine.Admits(Mins(120)));
    }

    // --------------------------------------------------------------- the definition

    [Fact]
    public async Task The_composite_is_exactly_the_lengths_every_role_can_provide()
    {
        // The definition the run arithmetic is an optimisation of, computed
        // independently: enumerate every candidate's own lengths at every start,
        // union them within a role, intersect across roles. A wrong least common
        // multiple shows up here and nowhere else.
        var service = Svc(ServiceDuration.Variable(Mins(25), Mins(200)).Value, ResourceTypes.Room, Therapist);
        var harness = Wire(
            service,
            Res(1, ResourceTypes.Room, granularity: 30, min: 30, max: 480, open: "09:00", close: "17:00"),
            Res(2, ResourceTypes.Room, granularity: 20, min: 40, max: 240, open: "09:30", close: "16:00"),
            Res(3, Therapist, granularity: 45, min: 45, max: 450, open: "09:00", close: "17:00"),
            Res(4, Therapist, granularity: 60, min: 60, max: 480, open: "09:00", close: "15:00"));

        // One candidate booked, so free time — not configuration alone — decides
        // some of the runs.
        Assert.True((await harness.Bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = Id(1),
            Start = TestData.Utc(Date, "12:00"),
            Duration = Mins(60),
            Booker = TestData.Booker(),
        })).Succeeded);

        var composite = await Starts(harness, service);
        var expected = await LengthsEveryRoleCanProvide(harness, service);

        Assert.NotEmpty(expected);
        Assert.Equal(
            expected.Keys.OrderBy(k => k),
            composite.Select(s => s.StartUtc));

        foreach (var start in composite)
        {
            Assert.Equal(
                expected[start.StartUtc].OrderBy(l => l),
                start.Runs.SelectMany(r => r.Lengths()).Distinct().OrderBy(l => l));
        }
    }

    [Fact]
    public async Task A_single_role_services_composite_is_its_union_availability()
    {
        // The regression gate for every scenario written before roles could be
        // plural: with one role the composite is that role's union, unchanged.
        var service = Svc(ServiceDuration.Variable(Mins(25), Mins(200)).Value, ResourceTypes.Room);
        var harness = Wire(
            service,
            Res(1, ResourceTypes.Room, granularity: 30, min: 30, max: 480, open: "09:00", close: "17:00"),
            Res(2, ResourceTypes.Room, granularity: 20, min: 40, max: 240, open: "09:30", close: "16:00"),
            Res(5, ResourceTypes.Room, granularity: 45, min: 45, max: 450, open: "09:00", close: "12:00"));

        Assert.True((await harness.Bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = Id(2),
            // On resource 2's own grid: 20-minute steps from its 09:30 opening.
            Start = TestData.Utc(Date, "11:10"),
            Duration = Mins(60),
            Booker = TestData.Booker(),
        })).Succeeded);

        var composite = await Starts(harness, service);
        var union = await LengthsEveryRoleCanProvide(harness, service);

        Assert.Equal(union.Keys.OrderBy(k => k), composite.Select(s => s.StartUtc));

        foreach (var start in composite)
        {
            Assert.Equal(
                union[start.StartUtc].OrderBy(l => l),
                start.Runs.SelectMany(r => r.Lengths()).Distinct().OrderBy(l => l));
        }
    }

    /// <summary>
    /// The lengths bookable at each start, computed from the definition rather
    /// than from run arithmetic: within a role the union over its candidates,
    /// across roles the intersection. A start survives only where every role
    /// offers at least one common length.
    /// </summary>
    private static async Task<Dictionary<DateTimeOffset, SortedSet<TimeSpan>>> LengthsEveryRoleCanProvide(
        Harness harness, Service service)
    {
        var pools = (await harness.Services.ResolveCandidatesAsync(service.Id)).Value;

        Dictionary<DateTimeOffset, SortedSet<TimeSpan>>? composed = null;

        foreach (var pool in pools)
        {
            var byStart = new Dictionary<DateTimeOffset, SortedSet<TimeSpan>>();

            foreach (var candidate in pool.Candidates)
            {
                var claims = await harness.Store.GetClaimsAsync(
                    [candidate.ResourceId],
                    TestData.Utc(Date, "00:00"),
                    TestData.Utc(Date.AddDays(1), "00:00"));

                var projected = harness.Availability.ProjectBookableStarts(
                    candidate.Resource, claims, Date, Date);

                foreach (var bookable in projected.Value)
                {
                    var min = bookable.MinDuration > candidate.Range.Min ? bookable.MinDuration : candidate.Range.Min;
                    var max = bookable.MaxDuration < candidate.Range.Max ? bookable.MaxDuration : candidate.Range.Max;

                    for (var length = min; length <= max; length += candidate.Granularity)
                    {
                        if (!byStart.TryGetValue(bookable.StartUtc, out var lengths))
                        {
                            lengths = [];
                            byStart[bookable.StartUtc] = lengths;
                        }

                        lengths.Add(length);
                    }
                }
            }

            if (composed is null)
            {
                composed = byStart;
                continue;
            }

            var narrowed = new Dictionary<DateTimeOffset, SortedSet<TimeSpan>>();

            foreach (var (start, lengths) in composed)
            {
                if (!byStart.TryGetValue(start, out var other))
                {
                    continue;
                }

                var shared = new SortedSet<TimeSpan>(lengths.Where(other.Contains));
                if (shared.Count > 0)
                {
                    narrowed[start] = shared;
                }
            }

            composed = narrowed;
        }

        return composed ?? [];
    }

    // ------------------------------------------------------ assignment (⑨-2)

    [Fact]
    public async Task Spec_scenario_a_start_two_roles_cannot_both_fill_is_not_offered()
    {
        // Two `therapist` roles over a pool in which exactly one resource is free.
        // Each role considered alone has a candidate at every start, so the old
        // intersection kept every one of them — and none was bookable.
        var service = SameType(ServiceDuration.Fixed(Mins(60)).Value, Therapist, [], ["cert-x"]);
        var harness = Wire(
            service,
            ResWith(1, Therapist, granularity: 60, min: 60, max: 60, capabilities: ["cert-x"]));

        Assert.Empty(await Starts(harness, service));
    }

    [Fact]
    public async Task Spec_scenario_a_count_is_not_satisfied_by_one_resource_counted_twice()
    {
        var service = Counted(ServiceDuration.Fixed(Mins(60)).Value, Therapist, 2);
        var harness = Wire(service, Res(1, Therapist, granularity: 60, min: 60, max: 60));

        Assert.Empty(await Starts(harness, service));
    }

    [Fact]
    public async Task Spec_scenario_one_free_resource_each_is_enough_when_the_roles_can_be_told_apart()
    {
        // Two resources, each satisfying exactly one of the two roles. An
        // assignment exists, so the start is offered.
        var service = SameType(ServiceDuration.Fixed(Mins(60)).Value, Therapist, ["welsh"], ["cert-x"]);
        var harness = Wire(
            service,
            ResWith(1, Therapist, granularity: 60, min: 60, max: 60, capabilities: ["cert-x"]),
            ResWith(2, Therapist, granularity: 60, min: 60, max: 60, capabilities: ["welsh"]));

        Assert.NotEmpty(await Starts(harness, service));
    }

    [Fact]
    public async Task Spec_scenario_a_greedy_choice_that_strands_a_role_does_not_lose_a_bookable_start()
    {
        // Resource 1 holds `cert-x` and is eligible for both roles; resource 2 holds
        // nothing and can fill only the unconstrained one. The start is bookable
        // only if the shared resource goes to the role that has no alternative.
        var service = SameType(ServiceDuration.Fixed(Mins(60)).Value, Therapist, [], ["cert-x"]);
        var harness = Wire(
            service,
            ResWith(1, Therapist, granularity: 60, min: 60, max: 60, capabilities: ["cert-x"]),
            Res(2, Therapist, granularity: 60, min: 60, max: 60));

        Assert.NotEmpty(await Starts(harness, service));
    }

    [Fact]
    public async Task A_count_of_two_over_two_free_candidates_is_offered()
    {
        // The positive half of the count case, so the negative one above cannot
        // pass by the composition simply refusing every counted service.
        var service = Counted(ServiceDuration.Fixed(Mins(60)).Value, Therapist, 2);
        var harness = Wire(
            service,
            Res(1, Therapist, granularity: 60, min: 60, max: 60),
            Res(2, Therapist, granularity: 60, min: 60, max: 60));

        Assert.NotEmpty(await Starts(harness, service));
    }

    [Fact]
    public async Task Spec_scenario_a_length_is_offered_only_where_an_assignment_provides_it()
    {
        // Both roles are `therapist`. Resource 1 holds `cert-x` and offers 60–120 on
        // a 60 grid; resource 2 holds nothing and offers 60 only.
        //
        // The capability-constrained role can only be filled by resource 1, so the
        // unconstrained one must take resource 2 — and 120 dies with it, even though
        // resource 1 alone could have provided it and the union of what each role
        // "could" offer contains it.
        var service = SameType(null, Therapist, [], ["cert-x"]);
        var harness = Wire(
            service,
            ResWith(1, Therapist, granularity: 60, min: 60, max: 120, capabilities: ["cert-x"]),
            Res(2, Therapist, granularity: 60, min: 60, max: 60));

        var starts = await Starts(harness, service);

        Assert.NotEmpty(starts);
        Assert.All(starts, start => Assert.Equal([Mins(60)], Lengths(start)));
    }

    [Fact]
    public async Task Spec_scenario_a_feasible_length_set_that_is_not_one_anchored_run_is_expressed_exactly()
    {
        // Design D2's worked example, built for real. Two `therapist` roles:
        //
        //   role A ({cert-x}) → resource 1 only
        //   role B (nothing)  → resources 1, 2, 3
        //
        //   resource 1  30-grid, 30–90  → offers {30, 60, 90}
        //   resource 2  90-grid, 90–90  → offers {90}
        //   resource 3  30-grid, 30–30  → offers {30}
        //
        // Role A can only ever be filled by resource 1, so role B must take one of
        // the others:
        //
        //   30 → A takes 1, B takes 3        feasible
        //   60 → neither 2 nor 3 offers 60   NOT feasible
        //   90 → A takes 1, B takes 2        feasible
        //
        // {30, 90} is a run of step 60 whose minimum is not a multiple of 60, which
        // the anchored-at-step invariant forbids — so it cannot be one run, and a
        // composition that forced it into one would either advertise 60 or discard
        // 90. A start carries a set of runs, so the shape survives.
        var service = SameType(null, Therapist, [], ["cert-x"]);
        var harness = Wire(
            service,
            ResWith(1, Therapist, granularity: 30, min: 30, max: 90, capabilities: ["cert-x"]),
            Res(2, Therapist, granularity: 90, min: 90, max: 90),
            Res(3, Therapist, granularity: 30, min: 30, max: 30));

        var first = At(await Starts(harness, service), "09:00");

        Assert.Equal([Mins(30), Mins(90)], Lengths(first));

        // Stated separately, because it is the half that a run-merging bug breaks:
        // 60 is bookable by resource 1 and by nobody who could partner it.
        Assert.DoesNotContain(Mins(60), Lengths(first));
    }

    [Fact]
    public async Task Spec_scenario_composite_availability_costs_one_claims_read()
    {
        // Asking the assignment question per start and per length must not turn into
        // a read per start or per length. Asserted rather than assumed (task 2.6).
        var service = SameType(null, Therapist, [], ["cert-x"]);
        var harness = Wire(
            service,
            ResWith(1, Therapist, granularity: 30, min: 30, max: 480, capabilities: ["cert-x"]),
            Res(2, Therapist, granularity: 30, min: 30, max: 480));

        var before = harness.Store.BatchClaimReads;

        var starts = await Starts(harness, service);

        // A full day on a 30-minute grid, so there are many starts and many lengths
        // — the assertion says nothing unless the query is a large one.
        Assert.True(starts.Count > 4, $"expected a busy query, got {starts.Count} starts");
        Assert.Equal(1, harness.Store.BatchClaimReads - before);
    }

    [Fact]
    public async Task A_single_role_of_count_one_still_equals_that_roles_union_availability()
    {
        // The guarantee the spec keeps for count 1 and deliberately withdraws above
        // it: a single role is its role's union availability, run for run, not an
        // equivalent recomposition of the same lengths.
        var service = Svc(null, Therapist);
        var harness = Wire(
            service,
            Res(1, Therapist, granularity: 20, min: 20, max: 120),
            Res(2, Therapist, granularity: 30, min: 30, max: 90));

        var starts = await Starts(harness, service);
        var first = At(starts, "09:00");

        // Both candidates' runs survive, on their own grids — the union, unmerged.
        Assert.Contains(first.Runs, r => r.Step == Mins(20) && r.Min == Mins(20));
        Assert.Contains(first.Runs, r => r.Step == Mins(30) && r.Min == Mins(30));
    }

    [Fact]
    public async Task Distinct_type_services_answer_exactly_as_the_intersection_did()
    {
        // The differential guarantee (design D3), against an oracle written from the
        // *old* algorithm rather than from the new one: union the candidates' runs
        // within each role, then intersect pairwise across roles.
        //
        // Several configurations, because one would only prove the composition works
        // for the granularities it was written against. Compared on the lengths each
        // start denotes, which is the guarantee — a rebuilt run set may express the
        // same lengths more compactly than the pairwise intersection did.
        (int Granularity, int Min, int Max)[][] configurations =
        [
            [(60, 60, 120), (60, 60, 120)],
            [(30, 30, 120), (20, 20, 120)],
            [(30, 30, 90), (45, 45, 180)],
            [(20, 40, 200), (30, 30, 150)],
        ];

        foreach (var configuration in configurations)
        {
            var service = Svc(null, ResourceTypes.Room, Therapist);
            var harness = Wire(
                service,
                Res(1, ResourceTypes.Room, configuration[0].Granularity, configuration[0].Min, configuration[0].Max),
                Res(2, Therapist, configuration[1].Granularity, configuration[1].Min, configuration[1].Max));

            var actual = await Starts(harness, service);
            var expected = await Intersection(harness, service);

            Assert.Equal(
                expected.Select(e => e.Key),
                actual.Select(a => a.StartUtc));

            foreach (var start in actual)
            {
                Assert.Equal(expected[start.StartUtc], Lengths(start));
            }
        }
    }

    /// <summary>The lengths a start denotes, ascending and deduplicated.</summary>
    private static List<TimeSpan> Lengths(ServiceBookableStart start)
        => [.. start.Runs.SelectMany(run => run.Lengths()).Distinct().Order()];

    /// <summary>
    /// The answer the previous composition gave: within a role the union over its
    /// candidates, across roles the pairwise intersection of their runs.
    /// <para>
    /// Written out here rather than called, because an oracle that shared code with
    /// the thing it checks would agree with it by construction. It is correct only
    /// for roles of distinct types and count 1 — which is exactly the case the
    /// guarantee covers.
    /// </para>
    /// </summary>
    private static async Task<SortedDictionary<DateTimeOffset, List<TimeSpan>>> Intersection(
        Harness harness, Service service)
    {
        var pools = await harness.Services.ResolveCandidatesAsync(service.Id);
        Assert.True(pools.Succeeded);

        Dictionary<DateTimeOffset, HashSet<LengthRun>>? composite = null;

        foreach (var pool in pools.Value)
        {
            var byStart = new Dictionary<DateTimeOffset, HashSet<LengthRun>>();

            foreach (var candidate in pool.Candidates)
            {
                var projected = harness.Availability.ProjectBookableStarts(candidate.Resource, [], Date, Date);
                Assert.True(projected.Succeeded);

                foreach (var start in projected.Value)
                {
                    var min = start.MinDuration > candidate.Range.Min ? start.MinDuration : candidate.Range.Min;
                    var max = start.MaxDuration < candidate.Range.Max ? start.MaxDuration : candidate.Range.Max;

                    if (min > max)
                    {
                        continue;
                    }

                    if (!byStart.TryGetValue(start.StartUtc, out var runs))
                    {
                        runs = [];
                        byStart[start.StartUtc] = runs;
                    }

                    runs.Add(new LengthRun(min, max, candidate.Granularity));
                }
            }

            if (composite is null)
            {
                composite = byStart;
                continue;
            }

            var composed = new Dictionary<DateTimeOffset, HashSet<LengthRun>>();

            foreach (var (start, left) in composite)
            {
                if (!byStart.TryGetValue(start, out var right))
                {
                    continue;
                }

                var shared = new HashSet<LengthRun>();

                foreach (var a in left)
                {
                    foreach (var b in right)
                    {
                        if (a.TryIntersect(b, out var run))
                        {
                            shared.Add(run);
                        }
                    }
                }

                if (shared.Count > 0)
                {
                    composed[start] = shared;
                }
            }

            composite = composed;
        }

        var result = new SortedDictionary<DateTimeOffset, List<TimeSpan>>();

        foreach (var (start, runs) in composite ?? [])
        {
            result[start] = [.. runs.SelectMany(run => run.Lengths()).Distinct().Order()];
        }

        return result;
    }
}
