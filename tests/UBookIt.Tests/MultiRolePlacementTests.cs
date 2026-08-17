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
/// Placing a booking for a service of several roles: one resource per role, one
/// booking, one interval (service-booking spec, "Booking a service resolves a
/// resource by candidate loop").
/// <para>
/// The preference scenarios are carried forward from the single-role
/// requirement rather than assumed to still hold: a preferred resource now has
/// to constrain its own role and leave the others alone, which is a claim the
/// single-role tests could not make.
/// </para>
/// </summary>
public class MultiRolePlacementTests
{
    private const string Therapist = "therapist";

    private static readonly DateOnly Date = TestData.BaseDate;

    private static TimeSpan Mins(int minutes) => TimeSpan.FromMinutes(minutes);

    private static Guid Id(int n) => new($"00000000-0000-0000-0000-{n:x12}");

    private static Resource Res(int id, string type, string open = "09:00", string close = "17:00")
        => Resource.Create(
            type,
            $"Resource {id}",
            // Offered for direct booking. The domain default is the opposite;
            // these fixtures stand for ordinary bookable resources, and the
            // permission itself is exercised explicitly in DirectBookingTests.
            directlyBookable: true,
            availability: TestData.Config(
                TestData.Weekly(open, close, Date.DayOfWeek),
                constraints: BookingConstraints.Create(
                    granularity: Mins(30), minDuration: Mins(30), maxDuration: Mins(480)).Value),
            id: Id(id)).Value;

    private static Service Svc(params string[] types)
        => Service.Create("Massage", null, types.Select(t => new ServiceRole(t, 1))).Value;

    /// <summary>A resource of the given type carrying capabilities.</summary>
    private static Resource ResWith(int id, string type, params string[] capabilities)
        => Resource.Create(
            type,
            $"Resource {id}",
            directlyBookable: true,
            capabilities: capabilities,
            availability: TestData.Config(
                TestData.Weekly("09:00", "17:00", Date.DayOfWeek),
                constraints: BookingConstraints.Create(
                    granularity: Mins(30), minDuration: Mins(30), maxDuration: Mins(480)).Value),
            id: Id(id)).Value;

    /// <summary>One role of one type, requiring <paramref name="count"/> resources.</summary>
    private static Service Counted(string type, int count)
        => Service.Create("Workshop", null, [new ServiceRole(type, count)]).Value;

    /// <summary>
    /// Two roles of one resource type, told apart by the capabilities each
    /// requires — so their eligibility pools overlap without being equal.
    /// </summary>
    private static Service SameType(string type, string[] first, string[] second)
        => Service.Create(
            "Joint session",
            null,
            [ServiceRole.Create(type, first).Value, ServiceRole.Create(type, second).Value]).Value;

    private sealed class Harness
    {
        public required ServiceBookingService Services { get; init; }

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
            Bookings = bookings,
            Store = bookingStore,
        };
    }

    /// <summary>Two rooms (1, 2) and two therapists (3, 4), all alike and all free.</summary>
    private static Harness TwoOfEach(Service service)
        => Wire(
            service,
            Res(1, ResourceTypes.Room),
            Res(2, ResourceTypes.Room),
            Res(3, Therapist),
            Res(4, Therapist));

    private static ServiceBookingRequest Request(Service service, string start, int minutes, Guid? preferred = null)
        => new()
        {
            ServiceId = service.Id,
            Start = TestData.Utc(Date, start),
            Duration = Mins(minutes),
            Booker = TestData.Booker(),
            PreferredResourceId = preferred,
        };

    private static async Task<IReadOnlyList<ClaimInfo>> AllClaims(Harness harness)
        => await harness.Store.GetClaimsAsync(
            [Id(1), Id(2), Id(3), Id(4)],
            TestData.Utc(Date, "00:00"),
            TestData.Utc(Date.AddDays(1), "00:00"));

    [Fact]
    public async Task Spec_scenario_one_resource_is_claimed_per_role()
    {
        var service = Svc(ResourceTypes.Room, Therapist);
        var harness = TwoOfEach(service);

        var placed = await harness.Services.PlaceAsync(Request(service, "09:00", 60));

        Assert.True(placed.Succeeded);

        // Exactly one booking, carrying one claim of each type, both over the
        // booking's single interval.
        Assert.Equal(2, placed.Value.Claims.Count);
        Assert.Contains(placed.Value.Claims, c => c.ResourceId == Id(1));
        Assert.Contains(placed.Value.Claims, c => c.ResourceId == Id(3));

        var claims = await AllClaims(harness);
        Assert.Equal(2, claims.Count);
        Assert.All(claims, claim => Assert.Equal(placed.Value.Id, claim.BookingId));
        Assert.All(claims, claim => Assert.Equal(placed.Value.Interval, claim.Interval));
    }

    [Fact]
    public async Task Spec_scenario_a_role_with_no_free_candidate_prevents_the_booking()
    {
        var service = Svc(ResourceTypes.Room, Therapist);
        var harness = TwoOfEach(service);

        // Every therapist is busy at 09:00; both rooms are free.
        foreach (var therapist in new[] { Id(3), Id(4) })
        {
            Assert.True((await harness.Bookings.PlaceAsync(new BookingRequest
            {
                ResourceId = therapist,
                Start = TestData.Utc(Date, "09:00"),
                Duration = Mins(60),
                Booker = TestData.Booker(),
            })).Succeeded);
        }

        var placed = await harness.Services.PlaceAsync(Request(service, "09:00", 60));

        Assert.False(placed.Succeeded);
        Assert.Equal(FailureCodes.Conflict, Assert.Single(placed.Failures).Code);

        // No claim was left on either room: a failed attempt persists nothing,
        // which is what makes attempting combinations in sequence safe.
        var claims = await AllClaims(harness);
        Assert.Equal(2, claims.Count);
        Assert.All(claims, claim => Assert.Contains(claim.ResourceId, new[] { Id(3), Id(4) }));
    }

    [Fact]
    public async Task A_role_whose_pool_is_empty_is_reported_as_unavailable()
    {
        // No therapist exists at all — a configuration answer rather than a race.
        var service = Svc(ResourceTypes.Room, Therapist);
        var harness = Wire(service, Res(1, ResourceTypes.Room), Res(2, ResourceTypes.Room));

        var placed = await harness.Services.PlaceAsync(Request(service, "09:00", 60));

        Assert.False(placed.Succeeded);
        Assert.Equal(FailureCodes.ServiceUnavailable, Assert.Single(placed.Failures).Code);
    }

    [Fact]
    public async Task A_service_of_several_roles_produces_exactly_one_booking()
    {
        var service = Svc(ResourceTypes.Room, Therapist);
        var harness = TwoOfEach(service);

        var placed = await harness.Services.PlaceAsync(Request(service, "09:00", 60));

        var claims = await AllClaims(harness);

        Assert.Single(claims.Select(c => c.BookingId).Distinct());
        Assert.Equal(placed.Value.Id, claims[0].BookingId);
    }

    [Fact]
    public async Task A_preferred_resource_constrains_the_booking_not_one_role()
    {
        var service = Svc(ResourceTypes.Room, Therapist);
        var harness = TwoOfEach(service);

        // Preferring the second therapist must not disturb the room role, which
        // still takes its lowest-id candidate.
        var placed = await harness.Services.PlaceAsync(Request(service, "09:00", 60, preferred: Id(4)));

        Assert.True(placed.Succeeded);
        Assert.Equal(
            [Id(1), Id(4)],
            placed.Value.Claims.Select(c => c.ResourceId).OrderBy(id => id));
    }

    [Fact]
    public async Task Spec_scenario_preferred_resource_falls_through_when_unavailable()
    {
        var service = Svc(ResourceTypes.Room, Therapist);
        var harness = TwoOfEach(service);

        Assert.True((await harness.Bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = Id(4),
            Start = TestData.Utc(Date, "09:00"),
            Duration = Mins(60),
            Booker = TestData.Booker(),
        })).Succeeded);

        var placed = await harness.Services.PlaceAsync(Request(service, "09:00", 60, preferred: Id(4)));

        // Preference is an ordering hint: the other therapist still gets a turn.
        Assert.True(placed.Succeeded);
        Assert.Equal(
            [Id(1), Id(3)],
            placed.Value.Claims.Select(c => c.ResourceId).OrderBy(id => id));
    }

    [Fact]
    public async Task Spec_scenario_preferred_resource_outside_every_pool_is_rejected()
    {
        var service = Svc(ResourceTypes.Room, Therapist);
        var harness = Wire(
            service,
            Res(1, ResourceTypes.Room),
            Res(3, Therapist),
            Res(9, "equipment"));

        var placed = await harness.Services.PlaceAsync(Request(service, "09:00", 60, preferred: Id(9)));

        Assert.False(placed.Succeeded);
        Assert.Equal(FailureCodes.ResourceNotEligible, Assert.Single(placed.Failures).Code);
        Assert.Empty(await AllClaims(harness));
    }

    [Fact]
    public async Task Spec_scenario_an_ineligible_preference_is_reported_even_when_a_pool_is_empty()
    {
        // No therapist exists, and the preference names a resource in no pool.
        // The caller's own mistake is the more useful thing to report.
        var service = Svc(ResourceTypes.Room, Therapist);
        var harness = Wire(service, Res(1, ResourceTypes.Room), Res(9, "equipment"));

        var placed = await harness.Services.PlaceAsync(Request(service, "09:00", 60, preferred: Id(9)));

        Assert.False(placed.Succeeded);
        Assert.Equal(FailureCodes.ResourceNotEligible, Assert.Single(placed.Failures).Code);
    }

    [Fact]
    public async Task Assignments_are_resolved_in_a_deterministic_order()
    {
        var service = Svc(ResourceTypes.Room, Therapist);

        var first = await TwoOfEach(service).Services.PlaceAsync(Request(service, "09:00", 60));
        var second = await TwoOfEach(service).Services.PlaceAsync(Request(service, "09:00", 60));

        Assert.Equal(
            first.Value.Claims.Select(c => c.ResourceId).OrderBy(id => id),
            second.Value.Claims.Select(c => c.ResourceId).OrderBy(id => id));
    }

    [Fact]
    public async Task An_assignment_is_retried_around_a_single_busy_resource()
    {
        // Room 1 is busy, so the first combination fails; the loop must go on to
        // room 2 rather than reporting the service unavailable.
        var service = Svc(ResourceTypes.Room, Therapist);
        var harness = TwoOfEach(service);

        Assert.True((await harness.Bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = Id(1),
            Start = TestData.Utc(Date, "09:00"),
            Duration = Mins(60),
            Booker = TestData.Booker(),
        })).Succeeded);

        var placed = await harness.Services.PlaceAsync(Request(service, "09:00", 60));

        Assert.True(placed.Succeeded);
        Assert.Equal(
            [Id(2), Id(3)],
            placed.Value.Claims.Select(c => c.ResourceId).OrderBy(id => id));
    }

    /// <summary>
    /// Counts placement attempts so a test can assert how many combinations the
    /// loop tried, not merely what it returned.
    /// </summary>
    private sealed class CountingBookingService(IBookingService inner) : IBookingService
    {
        public int Attempts { get; private set; }

        public Task<DomainResult<Booking>> PlaceAsync(
            BookingRequest request, CancellationToken cancellationToken = default)
        {
            Attempts++;
            return inner.PlaceAsync(request, cancellationToken);
        }

        public Task<DomainResult<Booking>> PlaceAsync(
            MultiClaimBookingRequest request, CancellationToken cancellationToken = default)
        {
            Attempts++;
            return inner.PlaceAsync(request, cancellationToken);
        }

        public DomainResult CheckPlacementRules(Resource resource, DateTimeOffset start, TimeSpan duration)
            => inner.CheckPlacementRules(resource, start, duration);

        public Task<DomainResult<Booking>> CancelAsync(
            Guid bookingId, CancellationToken cancellationToken = default)
            => inner.CancelAsync(bookingId, cancellationToken);
    }

    [Fact]
    public async Task A_fully_booked_service_does_not_attempt_every_assignment()
    {
        // The cost this bounds is quadratic in the pool sizes: without the
        // claims pre-filter, six rooms against six therapists all busy is 36
        // sequential locking transactions for one anonymous request, and a
        // realistic pool of sixty each is 3,600. The loop must not attempt a
        // combination it already knows is occupied.
        var service = Svc(ResourceTypes.Room, Therapist);

        var resourceStore = new InMemoryResourceStore();
        var rooms = Enumerable.Range(1, 6).Select(n => Res(n, ResourceTypes.Room)).ToList();
        var therapists = Enumerable.Range(11, 6).Select(n => Res(n, Therapist)).ToList();
        foreach (var resource in rooms.Concat(therapists))
        {
            resourceStore.Add(resource);
        }

        var serviceStore = new InMemoryServiceStore().Add(service);
        var bookingStore = new InMemoryBookingStore();
        var time = new FixedTimeProvider(TestData.Now);
        var availability = new AvailabilityService(resourceStore, bookingStore, time, TestData.Settings);
        var real = new BookingService(resourceStore, bookingStore, time, TestData.Settings);
        var counting = new CountingBookingService(real);
        var services = new ServiceBookingService(
            serviceStore, resourceStore, bookingStore, availability, counting, TestData.Settings);

        // Occupy every resource of both types at the requested start.
        foreach (var resource in rooms.Concat(therapists))
        {
            Assert.True((await real.PlaceAsync(new BookingRequest
            {
                ResourceId = resource.Id,
                Start = TestData.Utc(Date, "09:00"),
                Duration = Mins(60),
                Booker = TestData.Booker(),
            })).Succeeded);
        }

        var attemptsBefore = counting.Attempts;

        var placed = await services.PlaceAsync(Request(service, "09:00", 60));

        Assert.False(placed.Succeeded);

        // Still a race rather than a configuration answer: those resources are
        // occupied now, and retrying later may succeed.
        Assert.Equal(FailureCodes.Conflict, Assert.Single(placed.Failures).Code);

        // Nothing was attempted at all: every combination was known occupied
        // before any lock was taken. The old cartesian loop made 36 attempts here.
        Assert.Equal(0, counting.Attempts - attemptsBefore);
    }

    [Fact]
    public async Task A_partially_booked_service_attempts_only_the_free_assignment()
    {
        // The counterpart that stops the test above passing by never attempting
        // anything: with one free resource per role, exactly one attempt is made
        // and it succeeds.
        var service = Svc(ResourceTypes.Room, Therapist);

        var resourceStore = new InMemoryResourceStore();
        foreach (var resource in new[] { Res(1, ResourceTypes.Room), Res(2, ResourceTypes.Room), Res(3, Therapist), Res(4, Therapist) })
        {
            resourceStore.Add(resource);
        }

        var serviceStore = new InMemoryServiceStore().Add(service);
        var bookingStore = new InMemoryBookingStore();
        var time = new FixedTimeProvider(TestData.Now);
        var availability = new AvailabilityService(resourceStore, bookingStore, time, TestData.Settings);
        var real = new BookingService(resourceStore, bookingStore, time, TestData.Settings);
        var counting = new CountingBookingService(real);
        var services = new ServiceBookingService(
            serviceStore, resourceStore, bookingStore, availability, counting, TestData.Settings);

        // Busy the lowest-id candidate of each role, so the free combination is
        // the *second* of each — a combination the loop only reaches by skipping.
        foreach (var busy in new[] { Id(1), Id(3) })
        {
            Assert.True((await real.PlaceAsync(new BookingRequest
            {
                ResourceId = busy,
                Start = TestData.Utc(Date, "09:00"),
                Duration = Mins(60),
                Booker = TestData.Booker(),
            })).Succeeded);
        }

        var attemptsBefore = counting.Attempts;

        var placed = await services.PlaceAsync(Request(service, "09:00", 60));

        Assert.True(placed.Succeeded);
        Assert.Equal([Id(2), Id(4)], placed.Value.Claims.Select(c => c.ResourceId).OrderBy(id => id));
        Assert.Equal(1, counting.Attempts - attemptsBefore);
    }

    /// <summary>
    /// A candidate can be busy <em>and</em> refusable on its own rules. The
    /// pipeline evaluates those rules before the conflict check, so such a
    /// candidate contributed a deterministic refusal, never a conflict — and
    /// excluding it from the attempt list must not change that answer.
    /// <para>
    /// This is the class of case that made the claims pre-filter a regression
    /// the first time: "was any candidate dropped" is not the same question as
    /// "would any dropped candidate have reached the conflict check".
    /// </para>
    /// </summary>
    [Theory]
    // Off the resource's grid: 09:30 against an hourly grid opening at 09:00.
    [InlineData("09:00", "17:00", 60, "09:30", 60)]
    // Outside its open hours: a 120-minute booking against a one-hour window.
    [InlineData("09:00", "10:00", 30, "09:00", 120)]
    public async Task A_busy_candidate_that_would_have_been_refused_anyway_is_not_a_race(
        string open, string close, int granularity, string start, int minutes)
    {
        var service = Svc(ResourceTypes.Room);

        var room = Resource.Create(
            ResourceTypes.Room,
            "Only room",
            directlyBookable: true,
            availability: TestData.Config(
                TestData.Weekly(open, close, Date.DayOfWeek),
                // The minimum has to be a multiple of the granularity, so it is
                // derived rather than fixed at 30.
                constraints: BookingConstraints.Create(
                    granularity: Mins(granularity),
                    minDuration: Mins(granularity),
                    maxDuration: Mins(480)).Value),
            id: Id(1)).Value;

        var harness = Wire(service, room);

        // Occupy it at a start that IS on its grid, so the resource is genuinely
        // claimed — the pre-filter will drop it.
        Assert.True((await harness.Bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = Id(1),
            Start = TestData.Utc(Date, open),
            Duration = Mins(60),
            Booker = TestData.Booker(),
        })).Succeeded);

        var placed = await harness.Services.PlaceAsync(Request(service, start, minutes));

        Assert.False(placed.Succeeded);

        // Retrying cannot help: the request is refused by the resource's own
        // configuration whatever its calendar looks like.
        Assert.Equal(FailureCodes.ServiceUnavailable, Assert.Single(placed.Failures).Code);
    }

    [Fact]
    public async Task A_busy_candidate_is_not_a_race_when_a_slot_can_never_be_filled()
    {
        // The cross-role case, and the reason the classification is about
        // COMBINATIONS rather than candidates: placement accumulates every
        // claimed resource's rules before the conflict check, so a combination
        // reaches that check only when every role contributes a candidate whose
        // rules admit. Here the therapist is merely busy — its rules admit — but
        // the room can never take a 09:30 start on its hourly grid, so no
        // combination could ever have raced and retrying is pointless.
        var service = Svc(ResourceTypes.Room, Therapist);

        var room = Resource.Create(
            ResourceTypes.Room,
            "Hourly room",
            directlyBookable: true,
            availability: TestData.Config(
                TestData.Weekly("09:00", "17:00", Date.DayOfWeek),
                constraints: BookingConstraints.Create(
                    granularity: Mins(60), minDuration: Mins(60), maxDuration: Mins(480)).Value),
            id: Id(1)).Value;

        var harness = Wire(service, room, Res(3, Therapist));

        // Busy the therapist across the requested interval; the room is free but
        // permanently off-grid for it.
        Assert.True((await harness.Bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = Id(3),
            Start = TestData.Utc(Date, "09:30"),
            Duration = Mins(60),
            Booker = TestData.Booker(),
        })).Succeeded);

        var placed = await harness.Services.PlaceAsync(Request(service, "09:30", 60));

        Assert.False(placed.Succeeded);
        Assert.Equal(FailureCodes.ServiceUnavailable, Assert.Single(placed.Failures).Code);
    }

    [Fact]
    public async Task A_request_level_failure_is_reported_even_when_every_candidate_was_excluded()
    {
        // A broken site time zone is a property of the site, not of any pool.
        // The attempt loop echoes it — but when every candidate is excluded the
        // loop never runs, so the classification has to carry it rather than
        // collapsing the rule check to a yes/no and reporting an all-fail code
        // that blames the resources.
        var service = Svc(ResourceTypes.Room);
        var room = Res(1, ResourceTypes.Room);

        var resourceStore = new InMemoryResourceStore().Add(room);
        var serviceStore = new InMemoryServiceStore().Add(service);
        var bookingStore = new InMemoryBookingStore();
        var time = new FixedTimeProvider(TestData.Now);

        // Placed through a sound configuration first, so the resource really is
        // claimed at the requested interval.
        var sound = TestData.Settings;
        var soundBookings = new BookingService(resourceStore, bookingStore, time, sound);
        Assert.True((await soundBookings.PlaceAsync(new BookingRequest
        {
            ResourceId = Id(1),
            Start = TestData.Utc(Date, "09:00"),
            Duration = Mins(60),
            Booker = TestData.Booker(),
        })).Succeeded);

        var broken = new SiteBookingSettings { TimeZoneId = "Not/AZone" };
        var services = new ServiceBookingService(
            serviceStore,
            resourceStore,
            bookingStore,
            new AvailabilityService(resourceStore, bookingStore, time, broken),
            new BookingService(resourceStore, bookingStore, time, broken),
            broken);

        var placed = await services.PlaceAsync(Request(service, "09:00", 60));

        Assert.False(placed.Succeeded);
        Assert.Equal(FailureCodes.TimeZoneInvalid, Assert.Single(placed.Failures).Code);
    }

    [Fact]
    public async Task A_busy_candidate_that_would_have_been_bookable_is_a_race()
    {
        // The pair that keeps the test above honest: same shape, but the request
        // is one the resource would have accepted, so being busy is the whole
        // reason it failed and `conflict` is the right answer.
        var service = Svc(ResourceTypes.Room);
        var harness = Wire(service, Res(1, ResourceTypes.Room));

        Assert.True((await harness.Bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = Id(1),
            Start = TestData.Utc(Date, "09:00"),
            Duration = Mins(60),
            Booker = TestData.Booker(),
        })).Succeeded);

        var placed = await harness.Services.PlaceAsync(Request(service, "09:00", 60));

        Assert.False(placed.Succeeded);
        Assert.Equal(FailureCodes.Conflict, Assert.Single(placed.Failures).Code);
    }

    [Fact]
    public async Task A_length_no_candidate_of_one_role_can_provide_is_rejected()
    {
        // The rooms permit up to 8 hours; the therapists cap at 60 minutes. A
        // 120-minute request is out of bounds for the service even though one
        // role could take it.
        var service = Svc(ResourceTypes.Room, Therapist);
        var therapist = Resource.Create(
            Therapist,
            "Short shifts",
            directlyBookable: true,
            availability: TestData.Config(
                TestData.Weekly("09:00", "17:00", Date.DayOfWeek),
                constraints: BookingConstraints.Create(
                    granularity: Mins(30), minDuration: Mins(30), maxDuration: Mins(60)).Value),
            id: Id(3)).Value;

        var harness = Wire(service, Res(1, ResourceTypes.Room), therapist);

        var placed = await harness.Services.PlaceAsync(Request(service, "09:00", 120));

        Assert.False(placed.Succeeded);
        Assert.Equal(FailureCodes.DurationTooLong, Assert.Single(placed.Failures).Code);
        Assert.Empty(await AllClaims(harness));
    }

    // --- assignment over overlapping pools (⑨-2) ---

    [Fact]
    public async Task Spec_scenario_a_count_claims_that_many_distinct_resources()
    {
        var service = Counted(Therapist, 2);
        var harness = Wire(service, Res(3, Therapist), Res(4, Therapist));

        var placed = await harness.Services.PlaceAsync(Request(service, "09:00", 60));

        Assert.True(placed.Succeeded);

        // One booking, two claims, two *different* resources.
        Assert.Equal(2, placed.Value.Claims.Count);
        Assert.Equal([Id(3), Id(4)], placed.Value.Claims.Select(c => c.ResourceId).Order());
    }

    [Fact]
    public async Task Spec_scenario_a_count_exceeding_the_free_resources_prevents_the_booking()
    {
        // Two candidates, one of them already booked. The count cannot be met by
        // the survivor counted twice.
        var service = Counted(Therapist, 2);
        var harness = Wire(service, Res(3, Therapist), Res(4, Therapist));

        Assert.True((await harness.Bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = Id(3),
            Start = TestData.Utc(Date, "09:00"),
            Duration = Mins(60),
            Booker = TestData.Booker(),
        })).Succeeded);

        var placed = await harness.Services.PlaceAsync(Request(service, "09:00", 60));

        Assert.False(placed.Succeeded);

        // The free one is not claimed — a partial booking would be worse than none.
        var claims = await AllClaims(harness);
        Assert.DoesNotContain(claims, c => c.ResourceId == Id(4));
    }

    [Fact]
    public async Task Spec_scenario_no_resource_is_claimed_twice()
    {
        // Both roles name `therapist`; resource 3 holds `cert-x` and so sits in both
        // pools. Before assignment, `Combinations` generated (3, 3) — and because a
        // preferred resource was ordered to the head of *every* pool containing it,
        // that duplicate was the FIRST attempt, which `Booking.Create` throws on
        // rather than refusing. The preference is supplied here for exactly that
        // reason.
        var service = SameType(Therapist, [], ["cert-x"]);
        var harness = Wire(service, ResWith(3, Therapist, "cert-x"), Res(4, Therapist));

        var placed = await harness.Services.PlaceAsync(Request(service, "09:00", 60, preferred: Id(3)));

        Assert.True(placed.Succeeded);

        var claimed = placed.Value.Claims.Select(c => c.ResourceId).ToList();
        Assert.Equal(2, claimed.Count);
        Assert.Equal(2, claimed.Distinct().Count());

        // The preference is honoured — in whichever slot it fits, which is the
        // capability-constrained one, since nothing else can fill it.
        Assert.Contains(Id(3), claimed);
        Assert.Contains(Id(4), claimed);
    }

    [Fact]
    public async Task Spec_scenario_a_greedy_choice_that_strands_a_slot_does_not_lose_a_bookable_service()
    {
        // Resource 3 holds `cert-x` and is eligible for both roles; resource 4 holds
        // nothing and can fill only the unconstrained one. A walk that gives the
        // unconstrained role its first candidate takes 3 and strands the `cert-x`
        // role, reporting a service unavailable that is plainly bookable.
        var service = SameType(Therapist, [], ["cert-x"]);
        var harness = Wire(service, ResWith(3, Therapist, "cert-x"), Res(4, Therapist));

        var placed = await harness.Services.PlaceAsync(Request(service, "09:00", 60));

        Assert.True(placed.Succeeded);

        // The shared resource takes the slot the other cannot fill.
        Assert.Equal([Id(3), Id(4)], placed.Value.Claims.Select(c => c.ResourceId).Order());
    }

    [Fact]
    public async Task An_assignment_that_reuses_a_resource_is_never_attempted()
    {
        // The stronger half of "no resource is claimed twice": not merely that no
        // such booking exists, but that no such attempt is made. `Booking.Create`
        // throws `ArgumentException` on duplicate claims, so an attempt would be an
        // unhandled exception rather than a refusal — the injectivity has to be
        // structural, not a filter after the fact.
        //
        // Both roles resolve to resource 3 alone, so the only combination the
        // cartesian walk could form is (3, 3).
        var service = SameType(Therapist, [], ["cert-x"]);
        var harness = Wire(service, ResWith(3, Therapist, "cert-x"));

        var placed = await harness.Services.PlaceAsync(Request(service, "09:00", 60));

        // A refusal, not an exception.
        Assert.False(placed.Succeeded);
        Assert.Equal(FailureCodes.ServiceUnavailable, placed.Failures[0].Code);

        // And nothing was persisted on the way to deciding that.
        Assert.Empty(await AllClaims(harness));
    }

    [Fact]
    public async Task A_count_of_two_over_a_single_candidate_is_unavailable_rather_than_a_duplicate_claim()
    {
        // The same structural guarantee reached through a count instead of two
        // roles: one free candidate cannot fill two slots.
        var service = Counted(Therapist, 2);
        var harness = Wire(service, Res(3, Therapist));

        var placed = await harness.Services.PlaceAsync(Request(service, "09:00", 60));

        Assert.False(placed.Succeeded);
        Assert.Equal(FailureCodes.ServiceUnavailable, placed.Failures[0].Code);
        Assert.Empty(await AllClaims(harness));
    }

    [Fact]
    public async Task A_preference_is_honoured_in_whichever_slot_admits_it()
    {
        // Resource 4 has no capabilities, so it can fill only the unconstrained
        // role. Preferring it must not be read as "fill role 1 with it" — the
        // assignment places it where it fits and gives the other slot to 3
        // (design D4).
        var service = SameType(Therapist, [], ["cert-x"]);
        var harness = Wire(service, ResWith(3, Therapist, "cert-x"), Res(4, Therapist));

        var placed = await harness.Services.PlaceAsync(Request(service, "09:00", 60, preferred: Id(4)));

        Assert.True(placed.Succeeded);
        Assert.Contains(Id(4), placed.Value.Claims.Select(c => c.ResourceId));
    }

    [Fact]
    public async Task Repeated_identical_requests_claim_the_same_resources()
    {
        // Determinism over an overlapping pool, where there is a real choice to
        // make — the single-role ordering guarantee could not exercise this.
        var service = SameType(Therapist, [], ["cert-x"]);

        async Task<IReadOnlyList<Guid>> Once()
        {
            var harness = Wire(
                service,
                ResWith(3, Therapist, "cert-x"),
                ResWith(4, Therapist, "cert-x"),
                Res(5, Therapist));

            var placed = await harness.Services.PlaceAsync(Request(service, "09:00", 60));

            Assert.True(placed.Succeeded);
            return [.. placed.Value.Claims.Select(c => c.ResourceId)];
        }

        Assert.Equal(await Once(), await Once());
    }

    // ---------------------------------------------------------------------
    // The richer `service-unavailable` message (pool-sufficiency design D2).
    // The code is contract and unchanged; the message becomes specific.
    // ---------------------------------------------------------------------

    /// <summary>
    /// A therapist whose own opening hours start late, so a 09:00 request is a
    /// <em>deterministic</em> refusal from it rather than a busy calendar. That is
    /// what makes the shortfall a property of the instant: the resource is
    /// eligible, and it cannot take this request.
    /// </summary>
    private static Resource LateOpening(int id)
        => Res(id, Therapist, open: "13:00");

    [Fact]
    public async Task Spec_scenario_the_message_names_what_was_short()
    {
        // Two therapists needed; two exist and are eligible; only one of them can
        // take a 09:00 request, because the other does not open until 13:00.
        var service = Counted(Therapist, 2);
        var harness = Wire(service, Res(3, Therapist), LateOpening(4));

        var placed = await harness.Services.PlaceAsync(Request(service, "09:00", 60));

        Assert.False(placed.Succeeded);

        var failure = Assert.Single(placed.Failures);

        Assert.Equal(FailureCodes.ServiceUnavailable, failure.Code);
        Assert.Contains(Therapist, failure.Message);
        Assert.Contains("2 distinct resources", failure.Message);
        Assert.Contains("only 1 can provide it then", failure.Message);
    }

    [Fact]
    public async Task Spec_scenario_the_code_is_unchanged_by_the_richer_message()
    {
        // A consumer matching on the code sees exactly what it saw before, and the
        // failure still carries no field — codes are contract, messages are not.
        var service = Counted(Therapist, 2);
        var harness = Wire(service, Res(3, Therapist), LateOpening(4));

        var placed = await harness.Services.PlaceAsync(Request(service, "09:00", 60));

        var failure = Assert.Single(placed.Failures);

        Assert.Equal(FailureCodes.ServiceUnavailable, failure.Code);
        Assert.Null(failure.Field);
    }

    [Fact]
    public async Task Spec_scenario_a_placement_time_shortfall_is_not_a_configuration_fault()
    {
        // Structurally sufficient — three eligible therapists for a count of two —
        // and short at this one instant, because two of them open at 13:00.
        //
        // The pairing is the test. The configuration check reports nothing, and the
        // placement message must not claim what the configuration check declined
        // to: no "never", no "cannot be fulfilled", nothing about the service as
        // configured.
        var service = Counted(Therapist, 2);
        var harness = Wire(service, Res(3, Therapist), LateOpening(4), LateOpening(5));

        var pools = await harness.Services.ResolveCandidatesAsync(service.Id);
        Assert.Null(PoolSufficiency.FindShortfall(pools.Value));

        var placed = await harness.Services.PlaceAsync(Request(service, "09:00", 60));
        var message = Assert.Single(placed.Failures).Message;

        Assert.Contains("at that time", message);
        Assert.DoesNotContain("never", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exist", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Both_surfaces_report_the_same_shortfall_from_one_computation()
    {
        // Design D2's guarantee, in the only form a test can hold it to: over a
        // configuration whose structural shortfall and instant shortfall coincide,
        // the two surfaces produce the same roles and the same two numbers.
        //
        // What this cannot see is a second implementation that happens to agree
        // today — for that, the deficiency detection is broken by hand and BOTH
        // surfaces are confirmed to move (task 3.5, mutation-checked at apply).
        var service = SameType(Therapist, [], ["cert-x"]);
        var harness = Wire(service, ResWith(3, Therapist, "cert-x"));

        var pools = await harness.Services.ResolveCandidatesAsync(service.Id);
        var configured = PoolSufficiency.FindShortfall(pools.Value);

        Assert.NotNull(configured);
        Assert.Equal(2, configured.Required);
        Assert.Equal(1, configured.Eligible);

        var placed = await harness.Services.PlaceAsync(Request(service, "09:00", 60));
        var message = Assert.Single(placed.Failures).Message;

        Assert.Contains($"{configured.Required} distinct resources", message);
        Assert.Contains($"only {configured.Eligible} can provide it then", message);

        // Both roles are named on both surfaces, and the capability that tells them
        // apart is in the message — "therapist and therapist" would be useless.
        Assert.Equal(2, configured.Roles.Count);
        Assert.Contains("'therapist' with cert-x", message);
    }

    [Fact]
    public async Task A_single_resource_service_keeps_the_message_it_always_had()
    {
        // QA's MAJOR, and the reason it mattered: this is the commonest
        // `service-unavailable` in the product — one role, count 1, a start the
        // resource's own configuration refuses. Before the guard it read "needs 1
        // distinct resources for 'therapist' at that time, and only 0 were
        // available", which is ungrammatical and tells a booker nothing.
        //
        // Counting to one is not a shortfall worth describing, so the richer
        // message is not used. The spec permits it; it does not require it.
        var service = Svc(Therapist);
        var harness = Wire(service, LateOpening(3));

        var placed = await harness.Services.PlaceAsync(Request(service, "09:00", 60));

        var failure = Assert.Single(placed.Failures);

        Assert.Equal(FailureCodes.ServiceUnavailable, failure.Code);
        Assert.Equal("This service cannot be booked at that time.", failure.Message);

        // Specifically: no arithmetic at all, and above all no "1 distinct
        // resources".
        Assert.DoesNotContain("distinct resources", failure.Message);
    }

    [Fact]
    public async Task A_shortfall_of_two_still_names_what_was_short()
    {
        // The pair that makes the guard above non-vacuous: the same fixture with
        // a count of 2 does produce the richer message, so the suppression is a
        // property of `Required == 1` rather than of this configuration.
        var service = Counted(Therapist, 2);
        var harness = Wire(service, Res(3, Therapist), LateOpening(4));

        var message = Assert.Single(
            (await harness.Services.PlaceAsync(Request(service, "09:00", 60))).Failures).Message;

        Assert.Contains("2 distinct resources", message);
    }

    [Fact]
    public async Task A_shortfall_naming_no_resource_at_all_reads_as_words()
    {
        // Zero gets its own phrasing rather than "only 0 can provide it then",
        // which reads like the "not known" zero the configuration surfaces are
        // careful never to print. Two slots, and the one eligible resource
        // refuses the request outright, so nothing admits it.
        var service = Counted(Therapist, 2);
        var harness = Wire(service, LateOpening(3), LateOpening(4));

        var message = Assert.Single(
            (await harness.Services.PlaceAsync(Request(service, "09:00", 60))).Failures).Message;

        Assert.Contains("none can provide it then", message);
        Assert.DoesNotContain("only 0", message);
    }

    [Fact]
    public async Task A_role_with_no_eligible_resource_at_all_keeps_its_own_message()
    {
        // The empty-pool guard fires before any of this and says something
        // different — and better — than a shortfall would: no resource can fulfil
        // the service, rather than an arithmetic about how many were free.
        var service = Svc(ResourceTypes.Room, Therapist);
        var harness = Wire(service, Res(1, ResourceTypes.Room));

        var placed = await harness.Services.PlaceAsync(Request(service, "09:00", 60));

        Assert.Equal(FailureCodes.ServiceUnavailable, Assert.Single(placed.Failures).Code);
        Assert.DoesNotContain("distinct resources", placed.Failures[0].Message);
    }
}
