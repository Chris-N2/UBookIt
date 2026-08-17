using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Core.Services;
using UBookIt.Core.Stores;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// Whether a resource may be booked on its own (resources spec, "A resource states
/// whether it may be booked on its own"; bookings spec, "Booking a single resource
/// requires that resource to permit it").
/// <para>
/// <b>Every fixture here states the permission explicitly.</b> The other suites'
/// helpers grant it, because they stand for ordinary bookable resources — which
/// means a guard placed on the wrong overload would go unnoticed there, since all
/// their resources permit everything. This file is where that cannot happen: the
/// resources withhold, and the same withholding resource is booked <em>both
/// ways</em> with opposite outcomes expected.
/// </para>
/// </summary>
public class DirectBookingTests
{
    private const string Therapist = "therapist";

    private static readonly DateOnly Date = TestData.BaseDate;

    private static Guid Id(int n) => new($"00000000-0000-0000-0000-{n:x12}");

    /// <summary>
    /// A resource open 09:00–17:00 on the fixture's day, stating the permission
    /// one way or the other. Never defaulted — the whole suite is about which
    /// answer was given.
    /// </summary>
    private static Resource Res(int id, bool directlyBookable, string type = ResourceTypes.Room, string open = "09:00")
        => Resource.Create(
            type,
            $"Resource {id}",
            directlyBookable: directlyBookable,
            availability: TestData.Config(
                TestData.Weekly(open, "17:00", Date.DayOfWeek),
                constraints: BookingConstraints.Create(
                    granularity: TimeSpan.FromMinutes(30),
                    minDuration: TimeSpan.FromMinutes(30),
                    maxDuration: TimeSpan.FromMinutes(480)).Value),
            id: Id(id)).Value;

    private sealed class Harness
    {
        public required ServiceBookingService Services { get; init; }

        public required IBookingService Bookings { get; init; }

        public required InMemoryBookingStore Store { get; init; }
    }

    private static Harness Wire(Service? service, params Resource[] resources)
    {
        var resourceStore = new InMemoryResourceStore();
        foreach (var resource in resources)
        {
            resourceStore.Add(resource);
        }

        var serviceStore = new InMemoryServiceStore();
        if (service is not null)
        {
            serviceStore.Add(service);
        }

        var wired = TestData.ServiceBookingWith(serviceStore, resourceStore);

        return new Harness { Services = wired.Services, Bookings = wired.Bookings, Store = wired.Store };
    }

    private static BookingRequest Direct(Guid resourceId, string start = "10:00", int minutes = 60)
        => new()
        {
            ResourceId = resourceId,
            Start = TestData.Utc(Date, start),
            Duration = TimeSpan.FromMinutes(minutes),
            Booker = TestData.Booker(),
        };

    private static Service ServiceOf(params ServiceRole[] roles)
        => Service.Create("Treatment", ServiceDuration.Variable(null, null).Value, roles).Value;

    private static ServiceBookingRequest ViaService(Service service, string start = "10:00", int minutes = 60)
        => new()
        {
            ServiceId = service.Id,
            Start = TestData.Utc(Date, start),
            Duration = TimeSpan.FromMinutes(minutes),
            Booker = TestData.Booker(),
        };

    private static async Task<IReadOnlyList<ClaimInfo>> Claims(Harness harness)
        => await harness.Store.GetClaimsAsync(
            [Id(1), Id(2)], TestData.Utc(Date, "00:00"), TestData.Utc(Date.AddDays(1), "00:00"));

    // ------------------------------------------------------------------
    // The domain property
    // ------------------------------------------------------------------

    [Fact]
    public void Spec_scenario_a_new_resource_withholds_the_permission()
    {
        // Not stated, therefore not granted. A caller that says nothing is saying
        // no, and this is the sentence the whole change rests on.
        Assert.False(Resource.Create(ResourceTypes.Room, "Meeting Room A").Value.DirectlyBookable);
    }

    [Fact]
    public void Spec_scenario_the_permission_is_independent_of_availability_and_capability()
    {
        // It describes what is offered, not what is possible, so nothing about the
        // calendar or the capability set may move it.
        var resource = Resource.Create(
            ResourceTypes.Room,
            "Meeting Room A",
            capabilities: ["projector"],
            availability: TestData.Config(TestData.Weekly("09:00", "17:00", Date.DayOfWeek)),
            directlyBookable: true).Value;

        Assert.True(resource.DirectlyBookable);

        var closed = Resource.Create(
            ResourceTypes.Room,
            "Meeting Room A",
            availability: AvailabilityConfiguration.Closed,
            directlyBookable: true).Value;

        Assert.True(closed.DirectlyBookable);
    }

    [Fact]
    public void Spec_scenario_withholding_is_not_a_validation_failure()
    {
        // Neither answer makes a resource invalid, in either direction.
        Assert.True(Resource.Create(ResourceTypes.Room, "A", directlyBookable: false).Succeeded);
        Assert.True(Resource.Create(ResourceTypes.Room, "A", directlyBookable: true).Succeeded);
    }

    [Fact]
    public async Task Spec_scenario_a_withholding_resource_is_still_a_service_candidate()
    {
        // The permission must not become a fourth eligibility term. A pool that
        // shrank when a resource stopped being separately lettable would make a
        // service's candidates depend on something unrelated to fulfilling it.
        var service = ServiceOf(ServiceRole.Create(Therapist, null).Value);
        var harness = Wire(service, Res(1, directlyBookable: false, type: Therapist));

        var pools = await harness.Services.ResolveCandidatesAsync(service.Id);

        Assert.True(pools.Succeeded);
        Assert.Equal([Id(1)], pools.Value.Single().Candidates.Select(c => c.ResourceId));
    }

    [Fact]
    public async Task Spec_scenario_withholding_does_not_remove_a_candidate()
    {
        // The middle clause of the scenario, which resolution and placement do not
        // between them cover: a withholding resource must also CONTRIBUTE
        // AVAILABILITY. The mechanism cannot see the permission — it appears
        // nowhere in Core's services or availability code — so this is a guard
        // against someone later deciding it should.
        var service = ServiceOf(ServiceRole.Create(Therapist, null).Value);
        var harness = Wire(service, Res(1, directlyBookable: false, type: Therapist));

        var starts = await harness.Services.GetBookableStartsAsync(service.Id, Date, Date);

        Assert.True(starts.Succeeded);
        Assert.NotEmpty(starts.Value);
    }

    // ------------------------------------------------------------------
    // The guard, and where it sits
    // ------------------------------------------------------------------

    [Fact]
    public async Task Spec_scenario_a_withholding_resource_cannot_be_booked_alone()
    {
        var harness = Wire(null, Res(1, directlyBookable: false));

        var placed = await harness.Bookings.PlaceAsync(Direct(Id(1)));

        Assert.False(placed.Succeeded);
        Assert.Equal(FailureCodes.ResourceNotDirectlyBookable, Assert.Single(placed.Failures).Code);

        // Nothing persisted on the way to deciding that.
        Assert.Empty(await Claims(harness));
    }

    [Fact]
    public async Task Spec_scenario_the_same_resource_is_bookable_as_part_of_a_service()
    {
        // THE test of this change, and the one an obvious suite omits.
        //
        // A test that only asserts "a withholding resource cannot be booked
        // directly" passes an implementation that put the guard in the multi-claim
        // overload — where it would refuse every service booking in the product.
        // So the SAME resource, at the SAME instant, is booked both ways here, and
        // the two outcomes must be opposite.
        var service = ServiceOf(ServiceRole.Create(Therapist, null).Value);
        var harness = Wire(service, Res(1, directlyBookable: false, type: Therapist));

        var direct = await harness.Bookings.PlaceAsync(Direct(Id(1)));
        Assert.False(direct.Succeeded);
        Assert.Equal(FailureCodes.ResourceNotDirectlyBookable, Assert.Single(direct.Failures).Code);

        var viaService = await harness.Services.PlaceAsync(ViaService(service));
        Assert.True(viaService.Succeeded, "a withholding resource must still be bookable through a service");
        Assert.Equal([Id(1)], viaService.Value.Claims.Select(c => c.ResourceId));
    }

    [Fact]
    public async Task Spec_scenario_a_single_role_service_of_count_one_is_still_a_service()
    {
        // The case most likely to be "fixed" into a refusal by someone reading the
        // rule as being about claim-set size rather than about which entry point
        // was used. One role, one resource, one claim — and it is a service.
        var service = ServiceOf(ServiceRole.Create(Therapist, null, 1).Value);
        var harness = Wire(service, Res(1, directlyBookable: false, type: Therapist));

        var placed = await harness.Services.PlaceAsync(ViaService(service));

        Assert.True(placed.Succeeded);
        Assert.Single(placed.Value.Claims);
    }

    [Fact]
    public async Task Spec_scenario_a_resource_granting_the_permission_is_unaffected()
    {
        var harness = Wire(null, Res(1, directlyBookable: true));

        var placed = await harness.Bookings.PlaceAsync(Direct(Id(1)));

        Assert.True(placed.Succeeded);
        Assert.Equal(Id(1), Assert.Single(placed.Value.Claims).ResourceId);
    }

    [Fact]
    public async Task Spec_scenario_the_refusal_precedes_the_rule_pipeline()
    {
        // 07:00 is outside the resource's open hours as well as being refused by
        // the permission. The permission answers first: the request was never one
        // this resource accepts, so `outside-open-hours` would send a booker
        // looking for a better time that does not exist.
        var harness = Wire(null, Res(1, directlyBookable: false));

        var placed = await harness.Bookings.PlaceAsync(Direct(Id(1), start: "07:00"));

        Assert.Equal(FailureCodes.ResourceNotDirectlyBookable, Assert.Single(placed.Failures).Code);
    }

    [Fact]
    public async Task Spec_scenario_the_refusal_is_not_a_conflict_and_invites_no_retry()
    {
        var harness = Wire(null, Res(1, directlyBookable: false));

        var first = await harness.Bookings.PlaceAsync(Direct(Id(1)));
        var second = await harness.Bookings.PlaceAsync(Direct(Id(1)));

        Assert.Equal(FailureCodes.ResourceNotDirectlyBookable, Assert.Single(first.Failures).Code);
        Assert.Equal(FailureCodes.ResourceNotDirectlyBookable, Assert.Single(second.Failures).Code);
        Assert.NotEqual(FailureCodes.Conflict, first.Failures[0].Code);
    }

    [Fact]
    public async Task An_unknown_resource_is_still_reported_as_unknown()
    {
        // The guard refuses only a resource that exists and withholds. A missing
        // one falls through, so the pipeline reports `resource-not-found` as it
        // always has rather than this rule inventing a second opinion about a
        // resource it could not read.
        var harness = Wire(null, Res(1, directlyBookable: true));

        var placed = await harness.Bookings.PlaceAsync(Direct(Id(99)));

        Assert.Equal(FailureCodes.ResourceNotFound, Assert.Single(placed.Failures).Code);
    }

    [Fact]
    public async Task The_refusal_is_not_in_the_deterministic_refusal_whitelist()
    {
        // Service placement never reaches the code, so it must not appear among the
        // per-candidate refusals the all-fail classification treats as
        // deterministic. If it ever did, it would mean a service had been refused
        // for a reason services are not subject to.
        var service = ServiceOf(ServiceRole.Create(Therapist, null).Value);
        var harness = Wire(service, Res(1, directlyBookable: false, type: Therapist));

        // Placed at an hour outside the resource's open hours, so the service DOES
        // fail — and must fail for the open-hours reason, never the permission.
        var placed = await harness.Services.PlaceAsync(ViaService(service, start: "07:00"));

        Assert.False(placed.Succeeded);
        Assert.DoesNotContain(
            placed.Failures,
            f => f.Code == FailureCodes.ResourceNotDirectlyBookable);
    }
}
