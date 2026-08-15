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
            availability: TestData.Config(
                TestData.Weekly(open, close, Date.DayOfWeek),
                constraints: BookingConstraints.Create(
                    granularity: Mins(30), minDuration: Mins(30), maxDuration: Mins(480)).Value),
            id: Id(id)).Value;

    private static Service Svc(params string[] types)
        => Service.Create("Massage", null, types.Select(t => new ServiceRole(t, 1))).Value;

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
    public async Task Spec_scenario_a_preferred_resource_orders_only_its_own_role()
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
    public async Task Combinations_are_attempted_in_a_deterministic_order()
    {
        var service = Svc(ResourceTypes.Room, Therapist);

        var first = await TwoOfEach(service).Services.PlaceAsync(Request(service, "09:00", 60));
        var second = await TwoOfEach(service).Services.PlaceAsync(Request(service, "09:00", 60));

        Assert.Equal(
            first.Value.Claims.Select(c => c.ResourceId).OrderBy(id => id),
            second.Value.Claims.Select(c => c.ResourceId).OrderBy(id => id));
    }

    [Fact]
    public async Task A_combination_is_retried_around_a_single_busy_resource()
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
    public async Task A_fully_booked_service_does_not_attempt_every_combination()
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
    public async Task A_partially_booked_service_attempts_only_the_free_combination()
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
}
