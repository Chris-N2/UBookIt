using UBookIt.Core;
using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Core.Services;
using UBookIt.Core.Stores;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// Booking via a service: eligibility, union availability over a heterogeneous
/// candidate pool, and candidate-loop placement (service-booking spec).
/// <para>
/// Scenarios here are derived from the spec text rather than from the
/// implementation. Change ⑦-1 shipped a defect whose test asserted the bug
/// because the test was written from the code; tests written from the scenario
/// would have caught it.
/// </para>
/// </summary>
public class ServiceBookingTests
{
    private static readonly DateOnly Date = TestData.BaseDate;

    private static TimeSpan Mins(int minutes) => TimeSpan.FromMinutes(minutes);

    /// <summary>
    /// Ids that sort predictably: candidate order is ascending resource id, so a
    /// test asserting "the lowest-id candidate" needs to know which that is.
    /// </summary>
    private static Guid Id(int n) => new($"00000000-0000-0000-0000-{n:x12}");

    private static Resource Room(
        int id,
        int granularity = 30,
        int min = 30,
        int max = 480,
        string open = "09:00",
        string close = "17:00",
        string type = ResourceTypes.Room)
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

    private static Service Svc(ServiceDuration? duration = null, string type = ResourceTypes.Room)
        => Service.Create("Consultation", duration, [new ServiceRole(type, 1)]).Value;

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

    private static ServiceBookingRequest Request(
        Service service, string start, int durationMinutes, Guid? preferred = null)
        => new()
        {
            ServiceId = service.Id,
            Start = TestData.Utc(Date, start),
            Duration = Mins(durationMinutes),
            Booker = TestData.Booker(),
            PreferredResourceId = preferred,
        };

    private static string SingleCode(DomainResult result) => Assert.Single(result.Failures).Code;

    // ---------------------------------------------------------------- eligibility

    [Fact]
    public async Task Spec_scenario_pool_is_every_resource_of_the_roles_type()
    {
        var service = Svc();
        var harness = Wire(
            service,
            Room(1), Room(2), Room(3), Room(4),
            Room(5, type: "therapist"), Room(6, type: "therapist"), Room(7, type: "therapist"));

        var result = await harness.Services.ResolveCandidatesAsync(service.Id);

        Assert.Equal(
            new[] { Id(1), Id(2), Id(3), Id(4) },
            result.SingleRolePool().Select(c => c.ResourceId).ToArray());
    }

    [Fact]
    public async Task Spec_scenario_a_resource_whose_range_cannot_admit_the_service_is_excluded()
    {
        var service = Svc(ServiceDuration.Fixed(Mins(120)).Value);
        var harness = Wire(service, Room(1, max: 90), Room(2, max: 180));

        var result = await harness.Services.ResolveCandidatesAsync(service.Id);

        Assert.True(result.Succeeded, "exclusion is an answer about the resource, not a validation failure");
        Assert.Equal(Id(2), Assert.Single(result.SingleRolePool()).ResourceId);
    }

    [Fact]
    public async Task Spec_scenario_a_resource_whose_granularity_admits_no_permitted_length_is_excluded()
    {
        // 40–50 minutes against a 30-minute grid: no multiple of 30 lies inside.
        var service = Svc(ServiceDuration.Variable(Mins(40), Mins(50)).Value);
        var harness = Wire(service, Room(1, granularity: 30, min: 30), Room(2, granularity: 10, min: 10));

        var result = await harness.Services.ResolveCandidatesAsync(service.Id);

        Assert.Equal(Id(2), Assert.Single(result.SingleRolePool()).ResourceId);
    }

    [Fact]
    public async Task Spec_scenario_pool_is_not_truncated_by_paging()
    {
        // Above the read port's paged-list clamp of 500: a candidate pool that
        // silently loses members reports unavailability that does not exist.
        var service = Svc();
        var rooms = Enumerable.Range(1, 501).Select(n => Room(n)).ToArray();
        var harness = Wire(service, rooms);

        var result = await harness.Services.ResolveCandidatesAsync(service.Id);

        Assert.Equal(501, result.SingleRolePool().Count);
    }

    [Fact]
    public async Task Spec_scenario_unknown_service()
    {
        var harness = Wire(Svc(), Room(1));

        var result = await harness.Services.ResolveCandidatesAsync(Guid.NewGuid());

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.ServiceNotFound, SingleCode(result));
    }

    // ------------------------------------------------------ per-candidate narrowing

    [Fact]
    public async Task Spec_scenario_service_narrows_a_resources_range()
    {
        var service = Svc(ServiceDuration.Variable(Mins(60), Mins(180)).Value);
        var harness = Wire(service, Room(1, granularity: 30, min: 30, max: 120));

        var candidate = Assert.Single((await harness.Services.ResolveCandidatesAsync(service.Id)).SingleRolePool());

        Assert.Equal(new DurationRange(Mins(60), Mins(120)), candidate.Range);
    }

    [Fact]
    public async Task Spec_scenario_a_resource_ceiling_is_never_widened()
    {
        var service = Svc(ServiceDuration.Variable(null, Mins(240)).Value);
        var harness = Wire(service, Room(1, max: 90));

        var candidate = Assert.Single((await harness.Services.ResolveCandidatesAsync(service.Id)).SingleRolePool());

        Assert.Equal(Mins(90), candidate.Range.Max);
    }

    [Fact]
    public async Task Spec_scenario_candidates_of_one_type_differ()
    {
        var service = Svc();
        var harness = Wire(
            service,
            Room(1, granularity: 30, min: 30, max: 60),
            Room(2, granularity: 20, min: 20, max: 40));

        var candidates = (await harness.Services.ResolveCandidatesAsync(service.Id)).SingleRolePool();

        Assert.Equal(new DurationRange(Mins(30), Mins(60)), candidates[0].Range);
        Assert.Equal(new DurationRange(Mins(20), Mins(40)), candidates[1].Range);
    }

    // ------------------------------------------------------------ union availability

    [Fact]
    public async Task Spec_scenario_homogeneous_pool_yields_one_run_per_start()
    {
        // Divergent free time is the whole point: identically-configured
        // candidates only ever produce different runs once one of them is
        // booked. A pool with nothing booked (or one whose maximum clamps every
        // run to the same value) cannot fail this assertion, so it would not be
        // testing anything.
        var service = Svc();
        var harness = Wire(
            service,
            Room(1, granularity: 30, min: 30, max: 480, open: "09:00", close: "17:00"),
            Room(2, granularity: 30, min: 30, max: 480, open: "09:00", close: "17:00"));

        await harness.Bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = Id(1),
            Start = TestData.Utc(Date, "11:00"),
            Duration = Mins(60),
            Booker = TestData.Booker(),
        });

        var result = await harness.Services.GetBookableStartsAsync(service.Id, Date, Date);

        Assert.True(result.Succeeded);
        Assert.NotEmpty(result.Value);

        // At 09:00 the booked candidate offers 30..120 and the free one 30..480;
        // the first is a strict subset of the second, so only the wider survives.
        var nine = Assert.Single(result.Value, s => s.StartUtc == TestData.Utc(Date, "09:00"));
        Assert.Equal(new LengthRun(Mins(30), Mins(480), Mins(30)), Assert.Single(nine.Runs));

        Assert.All(result.Value, start => Assert.Single(start.Runs));
    }

    [Fact]
    public async Task A_subsumed_run_is_dropped_but_a_partially_overlapping_one_is_kept()
    {
        // Same grid, contained range  -> collapsed (nothing is lost).
        // Different grid, overlapping -> both kept (each carries lengths the
        // other lacks), which is the case D3 refuses to merge.
        var service = Svc();
        var harness = Wire(
            service,
            Room(1, granularity: 30, min: 30, max: 90, open: "09:00", close: "17:00"),
            Room(2, granularity: 30, min: 30, max: 480, open: "09:00", close: "17:00"),
            Room(3, granularity: 20, min: 20, max: 120, open: "09:00", close: "17:00"));

        var result = await harness.Services.GetBookableStartsAsync(service.Id, Date, Date);

        var nine = Assert.Single(result.Value, s => s.StartUtc == TestData.Utc(Date, "09:00"));

        Assert.Equal(
            new[]
            {
                new LengthRun(Mins(20), Mins(120), Mins(20)),
                new LengthRun(Mins(30), Mins(480), Mins(30)),
            },
            nine.Runs.ToArray());
    }

    [Fact]
    public async Task Collapsing_runs_never_removes_an_offered_length()
    {
        // The safety property behind the collapse: whatever it drops, the set of
        // lengths on offer at each start is unchanged.
        var service = Svc();
        Resource[] Pool() =>
        [
            Room(1, granularity: 30, min: 30, max: 90, open: "09:00", close: "17:00"),
            Room(2, granularity: 30, min: 30, max: 480, open: "09:00", close: "17:00"),
            Room(3, granularity: 20, min: 20, max: 120, open: "09:00", close: "17:00"),
            Room(4, granularity: 60, min: 120, max: 240, open: "09:00", close: "17:00"),
        ];

        var harness = Wire(service, Pool());

        await harness.Bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = Id(2),
            Start = TestData.Utc(Date, "12:00"),
            Duration = Mins(60),
            Booker = TestData.Booker(),
        });

        var collapsed = await harness.Services.GetBookableStartsAsync(service.Id, Date, Date);

        // Recompute the union the naive way — every candidate's own run at every
        // start, uncollapsed — and compare the length sets start by start.
        foreach (var start in collapsed.Value)
        {
            var expected = new SortedSet<TimeSpan>();

            foreach (var candidate in (await harness.Services.ResolveCandidatesAsync(service.Id)).SingleRolePool())
            {
                var perResource = harness.Availability.ProjectBookableStarts(
                    candidate.Resource, await harness.Store.GetClaimsAsync(
                        [candidate.ResourceId],
                        TestData.Utc(Date, "00:00"),
                        TestData.Utc(Date.AddDays(1), "00:00")),
                    Date,
                    Date);

                foreach (var bookable in perResource.Value.Where(b => b.StartUtc == start.StartUtc))
                {
                    var min = bookable.MinDuration > candidate.Range.Min ? bookable.MinDuration : candidate.Range.Min;
                    var max = bookable.MaxDuration < candidate.Range.Max ? bookable.MaxDuration : candidate.Range.Max;

                    for (var length = min; length <= max; length += candidate.Granularity)
                    {
                        expected.Add(length);
                    }
                }
            }

            Assert.Equal(
                expected.ToArray(),
                start.Runs.SelectMany(r => r.Lengths()).Distinct().OrderBy(l => l).ToArray());
        }
    }

    [Fact]
    public async Task Spec_scenario_differing_granularities_are_not_merged()
    {
        var service = Svc();
        var harness = Wire(
            service,
            Room(1, granularity: 30, min: 30, max: 90, open: "09:00", close: "10:30"),
            Room(2, granularity: 20, min: 20, max: 120, open: "09:00", close: "11:00"));

        var result = await harness.Services.GetBookableStartsAsync(service.Id, Date, Date);

        var nine = Assert.Single(result.Value, s => s.StartUtc == TestData.Utc(Date, "09:00"));

        Assert.Equal(
            new[]
            {
                new LengthRun(Mins(20), Mins(120), Mins(20)),
                new LengthRun(Mins(30), Mins(90), Mins(30)),
            },
            nine.Runs.ToArray());

        // The lengths a merged (20, 120) pair would have implied.
        Assert.False(nine.Admits(Mins(25)));
        Assert.False(nine.Admits(Mins(50)));
        Assert.False(nine.Admits(Mins(70)));
    }

    [Fact]
    public async Task Spec_scenario_a_gap_between_candidates_is_preserved()
    {
        var service = Svc();
        var harness = Wire(
            service,
            Room(1, granularity: 30, min: 30, max: 30, open: "09:00", close: "09:30"),
            Room(2, granularity: 30, min: 90, max: 120, open: "09:00", close: "11:00"));

        var result = await harness.Services.GetBookableStartsAsync(service.Id, Date, Date);

        var nine = Assert.Single(result.Value, s => s.StartUtc == TestData.Utc(Date, "09:00"));

        Assert.Equal(
            new[]
            {
                new LengthRun(Mins(30), Mins(30), Mins(30)),
                new LengthRun(Mins(90), Mins(120), Mins(30)),
            },
            nine.Runs.ToArray());

        Assert.False(nine.Admits(Mins(60)));
    }

    [Fact]
    public async Task Spec_scenario_free_time_truncates_a_run()
    {
        var service = Svc();
        var harness = Wire(service, Room(1, granularity: 30, min: 30, max: 120, open: "09:00", close: "10:00"));

        var result = await harness.Services.GetBookableStartsAsync(service.Id, Date, Date);

        var nine = Assert.Single(result.Value, s => s.StartUtc == TestData.Utc(Date, "09:00"));

        Assert.Equal(new LengthRun(Mins(30), Mins(60), Mins(30)), Assert.Single(nine.Runs));
    }

    [Fact]
    public async Task Spec_scenario_a_start_no_candidate_can_fulfil_is_omitted()
    {
        // 09:30 leaves only 30 minutes, below the resource's 60-minute minimum.
        var service = Svc();
        var harness = Wire(service, Room(1, granularity: 30, min: 60, max: 120, open: "09:00", close: "10:00"));

        var result = await harness.Services.GetBookableStartsAsync(service.Id, Date, Date);

        Assert.Equal(
            new[] { TestData.Utc(Date, "09:00") },
            result.Value.Select(s => s.StartUtc).ToArray());
    }

    [Fact]
    public async Task The_services_own_bounds_narrow_the_runs_not_only_the_candidate_range()
    {
        // Without the resolved range applied inside the availability projection,
        // this start would advertise the resource's own 30..480 instead — a
        // no-op for an unbounded service, which is why the other union tests
        // cannot catch it.
        var service = Svc(ServiceDuration.Variable(Mins(60), Mins(90)).Value);
        var harness = Wire(service, Room(1, granularity: 30, min: 30, max: 480, open: "09:00", close: "17:00"));

        var result = await harness.Services.GetBookableStartsAsync(service.Id, Date, Date);

        var nine = Assert.Single(result.Value, s => s.StartUtc == TestData.Utc(Date, "09:00"));

        Assert.Equal(new LengthRun(Mins(60), Mins(90), Mins(30)), Assert.Single(nine.Runs));
    }

    [Fact]
    public async Task A_fixed_duration_service_offers_exactly_one_length()
    {
        var service = Svc(ServiceDuration.Fixed(Mins(90)).Value);
        var harness = Wire(service, Room(1, granularity: 30, min: 30, max: 480, open: "09:00", close: "17:00"));

        var result = await harness.Services.GetBookableStartsAsync(service.Id, Date, Date);

        Assert.NotEmpty(result.Value);
        Assert.All(result.Value, start => Assert.True(Assert.Single(start.Runs).IsSingleLength));
    }

    [Fact]
    public async Task Spec_scenario_the_pure_projection_matches_the_id_based_query()
    {
        // The pure projection takes caller-supplied claims, so it *can* diverge
        // from the id-based query if the caller's window is wrong. This pins
        // that it does not — for a resource with bookings, not just an empty one.
        var service = Svc();
        var room = Room(1, granularity: 30, min: 30, max: 240, open: "09:00", close: "17:00");
        var harness = Wire(service, room);

        await harness.Bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = room.Id,
            Start = TestData.Utc(Date, "11:00"),
            Duration = Mins(90),
            Booker = TestData.Booker(),
        });

        var viaId = await harness.Availability.GetBookableStartsAsync(room.Id, Date, Date);

        var claims = await harness.Store.GetClaimsAsync(
            [room.Id], TestData.Utc(Date, "00:00"), TestData.Utc(Date.AddDays(1), "00:00"));
        var viaProjection = harness.Availability.ProjectBookableStarts(room, claims, Date, Date);

        Assert.True(viaId.Succeeded);
        Assert.True(viaProjection.Succeeded);
        Assert.NotEmpty(viaId.Value);

        // Whole entries, not just starts: minimum and maximum must match too.
        Assert.Equal(viaId.Value.ToArray(), viaProjection.Value.ToArray());
    }

    [Fact]
    public void The_pure_projection_ignores_claims_belonging_to_other_resources()
    {
        // One batched read is passed to every candidate in turn, so a claim on
        // a sibling must not subtract from this resource's free time.
        var service = Svc();
        var room = Room(1, granularity: 30, min: 30, max: 240, open: "09:00", close: "17:00");
        var harness = Wire(service, room);

        var foreign = new ClaimInfo(
            Id(99),
            Guid.NewGuid(),
            BookingInterval.Create(
                TestData.Utc(Date, "09:00"), TestData.Utc(Date, "17:00"), TestData.LondonZoneId).Value,
            BookingStatus.Confirmed);

        var withForeign = harness.Availability.ProjectBookableStarts(room, [foreign], Date, Date);
        var withNone = harness.Availability.ProjectBookableStarts(room, [], Date, Date);

        Assert.Equal(withNone.Value.ToArray(), withForeign.Value.ToArray());
        Assert.NotEmpty(withNone.Value);
    }

    [Fact]
    public async Task An_ineligible_preference_is_reported_even_when_the_pool_is_empty()
    {
        // The empty-pool guard must not mask the caller's own mistake.
        var service = Svc(type: "nothing-of-this-type");
        var harness = Wire(service, Room(1));

        var placed = await harness.Services.PlaceAsync(Request(service, "09:00", 60, preferred: Id(1)));

        Assert.False(placed.Succeeded);
        Assert.Equal(FailureCodes.ResourceNotEligible, SingleCode(placed));
    }

    [Fact]
    public async Task An_empty_pool_without_a_preference_is_service_unavailable()
    {
        var service = Svc(type: "nothing-of-this-type");
        var harness = Wire(service, Room(1));

        var placed = await harness.Services.PlaceAsync(Request(service, "09:00", 60));

        Assert.False(placed.Succeeded);
        Assert.Equal(FailureCodes.ServiceUnavailable, SingleCode(placed));
    }

    [Fact]
    public void Spec_scenario_availability_does_not_name_resources()
    {
        var carriers = new[] { typeof(ServiceBookableStart), typeof(LengthRun) };

        Assert.All(carriers, type => Assert.DoesNotContain(
            type.GetProperties(),
            p => p.Name.Contains("Resource", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task Spec_scenario_per_resource_semantics_are_reused_not_reimplemented()
    {
        var service = Svc();
        var room = Room(1, granularity: 30, min: 30, max: 120);
        var harness = Wire(service, room);

        var viaService = await harness.Services.GetBookableStartsAsync(service.Id, Date, Date);
        var viaResource = await harness.Availability.GetBookableStartsAsync(room.Id, Date, Date);

        Assert.Equal(
            viaResource.Value.Select(s => s.StartUtc).ToArray(),
            viaService.Value.Select(s => s.StartUtc).ToArray());
    }

    [Fact]
    public async Task An_over_wide_range_is_rejected_before_the_pool_is_touched()
    {
        var service = Svc();
        var harness = Wire(service, Room(1));

        var result = await harness.Services.GetBookableStartsAsync(service.Id, Date, Date.AddYears(2));

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.DateRangeTooLarge, SingleCode(result));
    }

    [Fact]
    public async Task An_inverted_range_is_rejected()
    {
        var service = Svc();
        var harness = Wire(service, Room(1));

        var result = await harness.Services.GetBookableStartsAsync(service.Id, Date, Date.AddDays(-1));

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.DateRangeInvalid, SingleCode(result));
    }

    // -------------------------------------------------------------------- placement

    [Fact]
    public async Task Spec_scenario_first_available_candidate_is_booked()
    {
        var service = Svc();
        var harness = Wire(service, Room(1), Room(2));

        // Occupy the lowest-id candidate.
        var first = await harness.Bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = Id(1),
            Start = TestData.Utc(Date, "09:00"),
            Duration = Mins(60),
            Booker = TestData.Booker(),
        });
        Assert.True(first.Succeeded);

        var placed = await harness.Services.PlaceAsync(Request(service, "09:00", 60));

        Assert.True(placed.Succeeded);
        Assert.Equal(Id(2), Assert.Single(placed.Value.Claims).ResourceId);
    }

    [Fact]
    public async Task Spec_scenario_deterministic_ordering()
    {
        var service = Svc();

        var firstRun = await Wire(service, Room(1), Room(2)).Services.PlaceAsync(Request(service, "09:00", 60));
        var secondRun = await Wire(service, Room(1), Room(2)).Services.PlaceAsync(Request(service, "09:00", 60));

        Assert.Equal(
            Assert.Single(firstRun.Value.Claims).ResourceId,
            Assert.Single(secondRun.Value.Claims).ResourceId);
    }

    [Fact]
    public async Task Spec_scenario_preferred_resource_is_tried_first()
    {
        var service = Svc();
        var harness = Wire(service, Room(1), Room(2));

        var placed = await harness.Services.PlaceAsync(Request(service, "09:00", 60, preferred: Id(2)));

        Assert.True(placed.Succeeded);
        Assert.Equal(Id(2), Assert.Single(placed.Value.Claims).ResourceId);
    }

    [Fact]
    public async Task Spec_scenario_preferred_resource_falls_through_when_unavailable()
    {
        var service = Svc();
        var harness = Wire(service, Room(1), Room(2));

        await harness.Bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = Id(2),
            Start = TestData.Utc(Date, "09:00"),
            Duration = Mins(60),
            Booker = TestData.Booker(),
        });

        var placed = await harness.Services.PlaceAsync(Request(service, "09:00", 60, preferred: Id(2)));

        Assert.True(placed.Succeeded);
        Assert.Equal(Id(1), Assert.Single(placed.Value.Claims).ResourceId);
    }

    [Fact]
    public async Task Spec_scenario_preferred_resource_outside_the_pool_is_rejected()
    {
        var service = Svc();
        var harness = Wire(service, Room(1), Room(2), Room(9, type: "therapist"));

        var placed = await harness.Services.PlaceAsync(Request(service, "09:00", 60, preferred: Id(9)));

        Assert.False(placed.Succeeded);
        Assert.Equal(FailureCodes.ResourceNotEligible, SingleCode(placed));
        Assert.Empty(await harness.Store.GetClaimsAsync(
            [Id(1), Id(2), Id(9)], TestData.Utc(Date, "00:00"), TestData.Utc(Date.AddDays(1), "00:00")));
    }

    // ------------------------------------------------------------ length validation

    [Fact]
    public async Task Spec_scenario_mismatched_length_against_a_fixed_service_is_rejected()
    {
        var service = Svc(ServiceDuration.Fixed(Mins(60)).Value);
        var harness = Wire(service, Room(1));

        var placed = await harness.Services.PlaceAsync(Request(service, "09:00", 90));

        Assert.False(placed.Succeeded);
        Assert.Equal(FailureCodes.DurationTooLong, SingleCode(placed));

        // Never substituted: no booking exists at 60 minutes either.
        Assert.Empty(await harness.Store.GetClaimsAsync(
            [Id(1)], TestData.Utc(Date, "00:00"), TestData.Utc(Date.AddDays(1), "00:00")));
    }

    [Fact]
    public async Task Spec_scenario_too_short_for_every_candidate()
    {
        var service = Svc();
        var harness = Wire(service, Room(1, granularity: 30, min: 30), Room(2, granularity: 30, min: 60));

        var placed = await harness.Services.PlaceAsync(Request(service, "09:00", 15));

        Assert.False(placed.Succeeded);
        Assert.Equal(FailureCodes.DurationTooShort, SingleCode(placed));
    }

    [Fact]
    public async Task Spec_scenario_a_length_one_candidate_permits_is_accepted()
    {
        // Only the 20-minute grid can do 40 minutes.
        var service = Svc();
        var harness = Wire(
            service,
            Room(1, granularity: 30, min: 30, max: 120),
            Room(2, granularity: 20, min: 20, max: 120));

        var placed = await harness.Services.PlaceAsync(Request(service, "09:00", 40));

        Assert.True(placed.Succeeded);
        Assert.Equal(Id(2), Assert.Single(placed.Value.Claims).ResourceId);
    }

    [Fact]
    public async Task A_length_in_a_gap_between_candidates_is_service_unavailable()
    {
        // 60 is inside the pool-wide bounds (30..120) but on no candidate's grid.
        var service = Svc();
        var harness = Wire(
            service,
            Room(1, granularity: 30, min: 30, max: 30, open: "09:00", close: "09:30"),
            Room(2, granularity: 90, min: 90, max: 90, open: "09:00", close: "11:00"));

        var placed = await harness.Services.PlaceAsync(Request(service, "09:00", 60));

        Assert.False(placed.Succeeded);
        Assert.Equal(FailureCodes.ServiceUnavailable, SingleCode(placed));
    }

    // ------------------------------------------------------------- all-fail outcomes

    [Fact]
    public async Task Spec_scenario_all_candidates_busy_reports_conflict()
    {
        var service = Svc();
        var harness = Wire(service, Room(1), Room(2));

        foreach (var id in new[] { Id(1), Id(2) })
        {
            await harness.Bookings.PlaceAsync(new BookingRequest
            {
                ResourceId = id,
                Start = TestData.Utc(Date, "09:00"),
                Duration = Mins(60),
                Booker = TestData.Booker(),
            });
        }

        var placed = await harness.Services.PlaceAsync(Request(service, "09:00", 60));

        Assert.False(placed.Succeeded);
        Assert.Equal(FailureCodes.Conflict, SingleCode(placed));
    }

    [Fact]
    public async Task Spec_scenario_deterministic_rejection_reports_service_unavailable()
    {
        var service = Svc();
        var harness = Wire(service, Room(1, open: "09:00", close: "17:00"), Room(2, open: "09:00", close: "17:00"));

        // 20:00 is outside every candidate's open hours.
        var placed = await harness.Services.PlaceAsync(Request(service, "20:00", 60));

        Assert.False(placed.Succeeded);
        Assert.Equal(FailureCodes.ServiceUnavailable, SingleCode(placed));
    }

    [Fact]
    public async Task Spec_scenario_a_mixed_outcome_favours_conflict()
    {
        var service = Svc();
        var harness = Wire(
            service,
            // 09:30 is off this candidate's hourly grid — a deterministic refusal.
            Room(1, granularity: 60, min: 60, max: 120),
            Room(2, granularity: 30, min: 30, max: 120));

        await harness.Bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = Id(2),
            Start = TestData.Utc(Date, "09:30"),
            Duration = Mins(60),
            Booker = TestData.Booker(),
        });

        var placed = await harness.Services.PlaceAsync(Request(service, "09:30", 60));

        Assert.False(placed.Succeeded);
        Assert.Equal(FailureCodes.Conflict, SingleCode(placed));
    }

    /// <summary>
    /// A resource that is in the candidate pool but has vanished by the time
    /// placement loads it — a candidate deleted mid-loop.
    /// </summary>
    private sealed class VanishingResourceStore(IResourceStore inner) : IResourceStore
    {
        public Task<Resource?> GetAsync(Guid resourceId, CancellationToken cancellationToken = default)
            => Task.FromResult<Resource?>(null);

        public Task<ResourcePage> ListAsync(int skip, int take, CancellationToken cancellationToken = default)
            => inner.ListAsync(skip, take, cancellationToken);

        public Task<IReadOnlyList<Resource>> ListByTypeAsync(string type, CancellationToken cancellationToken = default)
            => inner.ListByTypeAsync(type, cancellationToken);
    }

    [Fact]
    public async Task A_candidate_that_vanishes_mid_loop_is_reported_as_retryable_not_as_the_canary()
    {
        // resource-not-found is transient, not a property of the request, so it
        // must not arrive dressed as the deterministic drift signal.
        var service = Svc();
        var room = Room(1);

        var resources = new InMemoryResourceStore().Add(room);
        var serviceStore = new InMemoryServiceStore().Add(service);
        var bookingStore = new InMemoryBookingStore();
        var time = new FixedTimeProvider(TestData.Now);
        var settings = TestData.Settings;
        var vanishing = new VanishingResourceStore(resources);

        var booking = new ServiceBookingService(
            serviceStore,
            vanishing,
            bookingStore,
            new AvailabilityService(vanishing, bookingStore, time, settings),
            new BookingService(vanishing, bookingStore, time, settings),
            settings);

        var placed = await booking.PlaceAsync(Request(service, "09:00", 60));

        Assert.False(placed.Succeeded);
        Assert.Equal(FailureCodes.Conflict, SingleCode(placed));
        Assert.NotEqual(FailureCodes.ServiceUnavailable, SingleCode(placed));
    }

    [Fact]
    public async Task A_broken_site_time_zone_is_reported_as_itself()
    {
        var service = Svc();
        var room = Room(1);

        var resources = new InMemoryResourceStore().Add(room);
        var serviceStore = new InMemoryServiceStore().Add(service);
        var bookingStore = new InMemoryBookingStore();
        var time = new FixedTimeProvider(TestData.Now);
        var broken = new SiteBookingSettings { TimeZoneId = "Not/AZone" };

        var booking = new ServiceBookingService(
            serviceStore,
            resources,
            bookingStore,
            new AvailabilityService(resources, bookingStore, time, broken),
            new BookingService(resources, bookingStore, time, broken),
            broken);

        var placed = await booking.PlaceAsync(Request(service, "09:00", 60));

        Assert.False(placed.Succeeded);
        Assert.Equal(FailureCodes.TimeZoneInvalid, SingleCode(placed));
    }

    [Fact]
    public async Task Spec_scenario_availability_driven_requests_do_not_provoke_the_deterministic_code()
    {
        // The canary invariant: everything the availability query offers must be
        // placeable. If this fails, availability and placement have drifted.
        var service = Svc();
        var harness = Wire(
            service,
            Room(1, granularity: 30, min: 30, max: 90, open: "09:00", close: "12:00"),
            Room(2, granularity: 20, min: 20, max: 120, open: "10:00", close: "16:00"));

        var starts = await harness.Services.GetBookableStartsAsync(service.Id, Date, Date);
        Assert.True(starts.Succeeded);
        Assert.NotEmpty(starts.Value);

        foreach (var start in starts.Value)
        {
            foreach (var length in start.Runs.SelectMany(r => r.Lengths()))
            {
                var fresh = Wire(
                    service,
                    Room(1, granularity: 30, min: 30, max: 90, open: "09:00", close: "12:00"),
                    Room(2, granularity: 20, min: 20, max: 120, open: "10:00", close: "16:00"));

                var placed = await fresh.Services.PlaceAsync(new ServiceBookingRequest
                {
                    ServiceId = service.Id,
                    Start = start.StartUtc,
                    Duration = length,
                    Booker = TestData.Booker(),
                });

                Assert.True(
                    placed.Succeeded,
                    $"availability offered {length.TotalMinutes:0} minutes at {start.StartUtc:HH:mm} " +
                    $"but placement refused it: {string.Join(", ", placed.Failures.Select(f => f.Code))}");
            }
        }
    }

    // ------------------------------------------------------------ everything optional

    [Fact]
    public async Task Spec_scenario_direct_placement_is_unchanged_by_services()
    {
        var service = Svc();
        var harness = Wire(service, Room(1));

        var placed = await harness.Bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = Id(1),
            Start = TestData.Utc(Date, "09:00"),
            Duration = Mins(60),
            Booker = TestData.Booker(),
        });

        Assert.True(placed.Succeeded);
        Assert.Equal(BookingStatus.Confirmed, placed.Value.Status);
    }

    [Fact]
    public async Task Spec_scenario_a_service_booking_blocks_direct_booking()
    {
        var service = Svc();
        var harness = Wire(service, Room(1));

        var viaService = await harness.Services.PlaceAsync(Request(service, "09:00", 60));
        Assert.True(viaService.Succeeded);

        var direct = await harness.Bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = Id(1),
            Start = TestData.Utc(Date, "09:30"),
            Duration = Mins(60),
            Booker = TestData.Booker(),
        });

        Assert.False(direct.Succeeded);
        Assert.Equal(FailureCodes.Conflict, SingleCode(direct));
    }

    [Fact]
    public async Task Spec_scenario_a_direct_booking_removes_a_candidate()
    {
        var service = Svc();
        var harness = Wire(
            service,
            Room(1, granularity: 60, min: 60, max: 60, open: "09:00", close: "10:00"),
            Room(2, granularity: 30, min: 30, max: 30, open: "09:00", close: "09:30"));

        var before = await harness.Services.GetBookableStartsAsync(service.Id, Date, Date);
        Assert.Equal(2, Assert.Single(before.Value, s => s.StartUtc == TestData.Utc(Date, "09:00")).Runs.Count);

        await harness.Bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = Id(1),
            Start = TestData.Utc(Date, "09:00"),
            Duration = Mins(60),
            Booker = TestData.Booker(),
        });

        var after = await harness.Services.GetBookableStartsAsync(service.Id, Date, Date);

        Assert.Equal(
            new LengthRun(Mins(30), Mins(30), Mins(30)),
            Assert.Single(Assert.Single(after.Value, s => s.StartUtc == TestData.Utc(Date, "09:00")).Runs));
    }
}
