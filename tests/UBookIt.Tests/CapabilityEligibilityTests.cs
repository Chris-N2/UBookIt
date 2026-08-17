using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Core.Services;
using UBookIt.Core.Stores;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// Capability-constrained eligibility (service-booking spec, "Eligible resource
/// resolution").
/// <para>
/// The fixture pools deliberately <strong>overlap without being equal</strong>:
/// Mary alone holds <c>cert-x</c>, while Mary and Frank both hold
/// <c>massage</c>. Single-role resolution cannot be wrong about that, but change
/// ⑨'s multi-role assignment can — greedily giving Mary to the unconstrained
/// role leaves the constrained one unfillable and reports "unavailable" for a
/// combination that was available. Fixtures built from disjoint or
/// single-capability pools would make that defect invisible to every test
/// inherited from here (design D9).
/// </para>
/// </summary>
public class CapabilityEligibilityTests
{
    private static readonly DateOnly Date = TestData.BaseDate;

    /// <summary>Ids sort predictably: candidates resolve in ascending id order.</summary>
    private static Guid Id(int n) => new($"00000000-0000-0000-0000-{n:x12}");

    private static Resource Person(int id, string name, params string[] capabilities)
        => Resource.Create(
            "therapist",
            name,
            capabilities: capabilities,
            availability: TestData.Config(TestData.Weekly("09:00", "17:00", Date.DayOfWeek)),
            id: Id(id)).Value;

    private static Resource Room(int id, string name, params string[] capabilities)
        => Resource.Create(
            ResourceTypes.Room,
            name,
            capabilities: capabilities,
            availability: TestData.Config(TestData.Weekly("09:00", "17:00", Date.DayOfWeek)),
            id: Id(id)).Value;

    // The overlapping pool. Mary ⊂ {Mary, Frank} for `massage`; Joan holds nothing.
    private static Resource Mary => Person(1, "Mary", "cert-x", "massage");

    private static Resource Frank => Person(2, "Frank", "massage");

    private static Resource Joan => Person(3, "Joan");

    private static Service Svc(params string[] required)
        => Service.Create(
            "Massage",
            null,
            [
                new ServiceRole("therapist", 1)
                {
                    RequiredCapabilities = CapabilitySet.Create(required).Value,
                }
            ]).Value;

    private static ServiceBookingService Wire(Service service, params Resource[] resources)
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

        return new ServiceBookingService(
            serviceStore, resourceStore, bookingStore, availability, bookings, settings);
    }

    private static async Task<string[]> PoolOf(Service service, params Resource[] resources)
    {
        var result = await Wire(service, resources).ResolveCandidatesAsync(service.Id);

        return [.. result.SingleRolePool().Select(c => c.Resource.DisplayName)];
    }

    [Fact]
    public async Task Spec_scenario_a_required_capability_narrows_the_pool()
    {
        var pool = await PoolOf(Svc("cert-x"), Mary, Frank, Joan);

        Assert.Equal(["Mary"], pool);
    }

    [Fact]
    public async Task Spec_scenario_all_required_capabilities_must_be_present()
    {
        // Frank holds massage but not cert-x; a partial match is not a match.
        var pool = await PoolOf(Svc("cert-x", "massage"), Mary, Frank, Joan);

        Assert.Equal(["Mary"], pool);
    }

    [Fact]
    public async Task Spec_scenario_extra_capabilities_do_not_disqualify()
    {
        // Mary holds cert-x as well as massage, and is still eligible for a role
        // that asks only for massage.
        var pool = await PoolOf(Svc("massage"), Mary, Frank, Joan);

        Assert.Equal(["Mary", "Frank"], pool);
    }

    [Fact]
    public async Task Spec_scenario_capabilities_do_not_cross_type_boundaries()
    {
        // A room holding cert-x is not eligible for a therapist role, however
        // well its capabilities match.
        var pool = await PoolOf(Svc("cert-x"), Room(4, "Blue Room", "cert-x"), Frank, Joan);

        Assert.Empty(pool);
    }

    [Fact]
    public async Task Spec_scenario_a_role_requiring_no_capabilities_is_unaffected()
    {
        // The ⑤/⑥/⑦ path: a service defined without capabilities matches every
        // resource of its type, including ones carrying capabilities and ones
        // carrying none.
        var pool = await PoolOf(Svc(), Mary, Frank, Joan);

        Assert.Equal(["Mary", "Frank", "Joan"], pool);
    }

    [Fact]
    public async Task Spec_scenario_overlapping_pools_resolve_independently_per_role()
    {
        // The two pools overlap without being equal. Each role resolves to its
        // own complete pool — resolution never partitions the resources between
        // roles, which is exactly why ⑨ needs real assignment rather than a
        // greedy first-available pick.
        var constrained = await PoolOf(Svc("cert-x"), Mary, Frank, Joan);
        var unconstrained = await PoolOf(Svc("massage"), Mary, Frank, Joan);

        Assert.Equal(["Mary"], constrained);
        Assert.Equal(["Mary", "Frank"], unconstrained);
        Assert.Contains("Mary", unconstrained);
    }

    [Fact]
    public async Task A_capability_no_resource_carries_yields_an_empty_pool_not_a_failure()
    {
        var service = Svc("never-tagged");

        var result = await Wire(service, Mary, Frank, Joan).ResolveCandidatesAsync(service.Id);

        Assert.Empty(result.SingleRolePool());
    }

    [Fact]
    public async Task Placement_resolves_to_an_eligible_resource()
    {
        var service = Svc("cert-x");
        var services = Wire(service, Mary, Frank, Joan);

        var placed = await services.PlaceAsync(new ServiceBookingRequest
        {
            ServiceId = service.Id,
            Start = TestData.Utc(Date, "10:00"),
            Duration = TimeSpan.FromMinutes(60),
            Booker = TestData.Booker(),
        });

        Assert.True(placed.Succeeded);
        Assert.Equal(Mary.Id, Assert.Single(placed.Value.Claims).ResourceId);
    }

    [Fact]
    public async Task Placement_with_no_eligible_resource_reports_service_unavailable()
    {
        // Not an exception, and not `conflict`: nothing was taken, so retrying
        // cannot help.
        var service = Svc("never-tagged");
        var services = Wire(service, Mary, Frank, Joan);

        var placed = await services.PlaceAsync(new ServiceBookingRequest
        {
            ServiceId = service.Id,
            Start = TestData.Utc(Date, "10:00"),
            Duration = TimeSpan.FromMinutes(60),
            Booker = TestData.Booker(),
        });

        Assert.False(placed.Succeeded);
        Assert.Equal(FailureCodes.ServiceUnavailable, Assert.Single(placed.Failures).Code);
    }

    [Fact]
    public async Task An_ineligible_preferred_resource_is_still_rejected_when_ineligibility_is_by_capability()
    {
        // D9's failure now fires for a capability-based exclusion as well as a
        // type-based one. It stays acceptable because both inputs are published,
        // so the caller could have computed the pool themselves.
        var service = Svc("cert-x");
        var services = Wire(service, Mary, Frank, Joan);

        var placed = await services.PlaceAsync(new ServiceBookingRequest
        {
            ServiceId = service.Id,
            Start = TestData.Utc(Date, "10:00"),
            Duration = TimeSpan.FromMinutes(60),
            Booker = TestData.Booker(),
            PinnedResourceId = Frank.Id,
        });

        Assert.False(placed.Succeeded);
        Assert.Equal(FailureCodes.ResourceNotEligible, Assert.Single(placed.Failures).Code);
    }
}
