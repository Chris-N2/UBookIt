using UBookIt.Core.Availability;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Core.Services;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The resolution chain (service-booking spec, "Resolution reports why each
/// resource was excluded"), and the entry point it is asked through
/// (service-booking spec, "Eligible resource resolution").
/// <para>
/// The fixture gives resources <strong>differing duration ranges</strong> as well
/// as differing capabilities, so the third stage has something to exclude. A pool
/// whose resources all shared one range would make the duration stage a copy of
/// the capability stage, and every attribution assertion below would pass
/// whichever stage the implementation blamed.
/// </para>
/// </summary>
public class ServiceResolutionTests
{
    private static readonly DateOnly Date = TestData.BaseDate;

    /// <summary>Ids sort predictably: stages are reported in ascending id order.</summary>
    private static Guid Id(int n) => new($"00000000-0000-0000-0000-{n:x12}");

    /// <summary>A room open on the fixture date, with its own duration ceiling.</summary>
    private static Resource Room(int id, string name, int maxMinutes, params string[] capabilities)
        => Resource.Create(
            ResourceTypes.Room,
            name,
            capabilities: capabilities,
            availability: TestData.Config(
                TestData.Weekly("08:00", "20:00", Date.DayOfWeek),
                constraints: BookingConstraints.Create(
                    granularity: TimeSpan.FromMinutes(30),
                    minDuration: TimeSpan.FromMinutes(30),
                    maxDuration: TimeSpan.FromMinutes(maxMinutes)).Value),
            id: Id(id)).Value;

    private static ServiceRole Role(params string[] required)
        => ServiceRole.Create(ResourceTypes.Room, required).Value;

    private static ServiceDuration Fixed(int minutes)
        => ServiceDuration.Fixed(TimeSpan.FromMinutes(minutes)).Value;

    private static ServiceBookingService Wire(params Resource[] resources)
        => Wire(new InMemoryServiceStore(), resources);

    private static ServiceBookingService Wire(InMemoryServiceStore services, params Resource[] resources)
    {
        var resourceStore = new InMemoryResourceStore();
        foreach (var resource in resources)
        {
            resourceStore.Add(resource);
        }

        return TestData.ServiceBooking(services, resourceStore);
    }

    /// <summary>
    /// Ten rooms. Three carry <c>projector</c>; of those three, only one has a
    /// ceiling high enough for four hours. The counts down the chain are
    /// therefore 10 → 3 → 1, and each drop has a different cause.
    /// </summary>
    private static Resource[] TenRooms() =>
    [
        Room(1, "Red Room", 120, "projector"),
        Room(2, "Blue Room", 120, "projector"),
        Room(3, "Green Room", 480, "projector"),
        Room(4, "Room D", 480),
        Room(5, "Room E", 480),
        Room(6, "Room F", 480),
        Room(7, "Room G", 480),
        Room(8, "Room H", 480),
        Room(9, "Room I", 480),
        Room(10, "Room J", 480),
    ];

    private static string[] Names(IEnumerable<Resource> resources)
        => [.. resources.Select(r => r.DisplayName)];

    // ----------------------------------------------- the chain reports each stage

    [Fact]
    public async Task Spec_scenario_the_chain_attributes_a_narrowed_pool()
    {
        // "ten `room` resources exist, three carry `projector`, and only one of
        // those three can provide four hours" → ten, three, one.
        var chain = await Wire(TenRooms()).ResolveAsync(Role("projector"), Fixed(240));

        Assert.Equal(10, chain.OfType.Count);
        Assert.Equal(3, chain.WithCapabilities.Count);
        Assert.Single(chain.Candidates);
        Assert.Equal("Green Room", chain.Candidates[0].Resource.DisplayName);
    }

    [Fact]
    public async Task Spec_scenario_an_empty_pool_names_the_stage_that_emptied_it()
    {
        // "no resource carries a required capability" → the capability stage
        // reports zero WHILE the type stage reports the resources of that type,
        // so the fault is attributable to the capabilities rather than the type.
        var chain = await Wire(TenRooms()).ResolveAsync(Role("never-tagged"), Fixed(60));

        Assert.Equal(10, chain.OfType.Count);
        Assert.Empty(chain.WithCapabilities);
        Assert.Empty(chain.Candidates);
    }

    [Fact]
    public async Task Spec_scenario_a_mistyped_resource_type_is_distinguishable_from_a_capability_problem()
    {
        // "the role names a resource type no resource uses and also requires
        // capabilities" → the type stage reports zero, and the capability stage
        // is NOT reported as the cause. This is the ⑧ defect: one number said
        // "no resources have these capabilities" and sent an editor hunting for
        // untagged resources when they had mistyped the type key.
        var role = ServiceRole.Create("rooom", ["projector"]).Value;

        var chain = await Wire(TenRooms()).ResolveAsync(role, Fixed(60));

        Assert.Empty(chain.OfType);
        Assert.Empty(chain.WithCapabilities);

        // The capability stage lost nothing — it had nothing to lose. What
        // distinguishes this from the capability fault above is that its input
        // was already empty.
        Assert.Equal(chain.OfType.Count, chain.WithCapabilities.Count);
    }

    [Fact]
    public async Task Spec_scenario_duration_exclusions_name_the_resources_and_their_limiting_bound()
    {
        // "two resources carry the required capabilities but their maximum
        // duration is shorter than a fixed service duration" → the resolution
        // identifies those two resources AND the bound that excluded them.
        var chain = await Wire(TenRooms()).ResolveAsync(Role("projector"), Fixed(240));

        Assert.Equal(["Red Room", "Blue Room"], Names(chain.DurationExclusions.Select(e => e.Resource)));

        Assert.All(chain.DurationExclusions, exclusion =>
        {
            Assert.Equal(DurationExclusionReason.ResourceMaximum, exclusion.Reason);
            Assert.Equal(TimeSpan.FromMinutes(120), exclusion.Bound);
        });
    }

    [Fact]
    public async Task Spec_scenario_a_healthy_configuration_excludes_nothing()
    {
        // "every resource of the role's type carries the required capabilities
        // and can provide the duration" → all three stages report the same count
        // and no exclusions are listed.
        var rooms = new[]
        {
            Room(1, "Red Room", 480, "projector"),
            Room(2, "Blue Room", 480, "projector"),
        };

        var chain = await Wire(rooms).ResolveAsync(Role("projector"), Fixed(60));

        Assert.Equal(2, chain.OfType.Count);
        Assert.Equal(2, chain.WithCapabilities.Count);
        Assert.Equal(2, chain.Candidates.Count);
        Assert.Empty(chain.DurationExclusions);
    }

    /// <summary>
    /// The other two ways a resource's own bounds can exclude it. Not in the spec
    /// scenarios, which name the common case, but the reported bound is what an
    /// editor changes — reporting a maximum when a minimum or a grid is at fault
    /// sends them to the wrong number.
    /// </summary>
    [Fact]
    public async Task A_resource_whose_minimum_exceeds_the_service_length_names_its_minimum()
    {
        var room = Resource.Create(
            ResourceTypes.Room,
            "Long Bookings Only",
            availability: TestData.Config(
                TestData.Weekly("08:00", "20:00", Date.DayOfWeek),
                constraints: BookingConstraints.Create(
                    granularity: TimeSpan.FromMinutes(30),
                    minDuration: TimeSpan.FromMinutes(180),
                    maxDuration: TimeSpan.FromMinutes(480)).Value),
            id: Id(1)).Value;

        var chain = await Wire(room).ResolveAsync(Role(), Fixed(60));

        var exclusion = Assert.Single(chain.DurationExclusions);
        Assert.Equal(DurationExclusionReason.ResourceMinimum, exclusion.Reason);
        Assert.Equal(TimeSpan.FromMinutes(180), exclusion.Bound);
    }

    [Fact]
    public async Task A_resource_whose_grid_admits_no_permitted_length_names_its_granularity()
    {
        // The ranges overlap — 40–50 sits inside 30–480 — but no multiple of 30
        // lies in the intersection, so the fault is the grid, not either bound.
        var room = Resource.Create(
            ResourceTypes.Room,
            "Half Hour Grid",
            availability: TestData.Config(
                TestData.Weekly("08:00", "20:00", Date.DayOfWeek),
                constraints: BookingConstraints.Create(
                    granularity: TimeSpan.FromMinutes(30),
                    minDuration: TimeSpan.FromMinutes(30),
                    maxDuration: TimeSpan.FromMinutes(480)).Value),
            id: Id(1)).Value;

        var duration = ServiceDuration.Variable(TimeSpan.FromMinutes(40), TimeSpan.FromMinutes(50)).Value;

        var chain = await Wire(room).ResolveAsync(Role(), duration);

        var exclusion = Assert.Single(chain.DurationExclusions);
        Assert.Equal(DurationExclusionReason.Granularity, exclusion.Reason);
        Assert.Equal(TimeSpan.FromMinutes(30), exclusion.Bound);
    }

    // ------------------------------------------- the entry point and the projection

    [Fact]
    public async Task Spec_scenario_resolution_does_not_require_a_valid_service()
    {
        // "a role and a duration are resolved for a service that has no name and
        // has never been saved" → the candidate pool is returned, and no
        // name-related validation failure is raised. Constructing a transient
        // Service to ask this would have failed on `service-name-required`, a
        // validator with no stake in the question.
        Assert.False(Service.Create(null, Fixed(60), [Role("projector")]).Succeeded);

        var chain = await Wire(TenRooms()).ResolveAsync(Role("projector"), Fixed(60));

        Assert.Equal(3, chain.Candidates.Count);
    }

    [Fact]
    public async Task Spec_scenario_the_pool_is_the_same_however_resolution_is_reached()
    {
        // "the same role and duration are resolved directly and by loading a
        // saved service carrying them" → both produce the same candidate pool.
        var role = Role("projector");
        var duration = Fixed(240);
        var service = Service.Create("Workshop", duration, [role]).Value;

        var resolution = Wire(new InMemoryServiceStore().Add(service), TenRooms());

        var direct = await resolution.ResolveAsync(role, duration);
        var byId = await resolution.ResolveCandidatesAsync(service.Id);

        Assert.True(byId.Succeeded);
        Assert.Equal(
            direct.Candidates.Select(c => c.ResourceId),
            byId.Value.Select(c => c.ResourceId));
    }

    [Fact]
    public async Task Spec_scenario_the_diagnostic_view_and_the_candidate_pool_agree()
    {
        // The property design D1 exists to guarantee: the chain's final stage
        // contains exactly the resources in the candidate pool the booking path
        // resolves. Asserted across configurations that empty the pool at each
        // stage in turn, because a funnel and a pool computed separately would
        // most plausibly diverge at a boundary rather than in the middle.
        var role = Role("projector");
        var rooms = TenRooms();

        foreach (var (configuredRole, duration) in new (ServiceRole, ServiceDuration)[]
        {
            (role, Fixed(240)),                                  // duration excludes two
            (role, Fixed(60)),                                   // nothing excluded past capabilities
            (Role("never-tagged"), Fixed(60)),                   // capabilities empty it
            (ServiceRole.Create("rooom", []).Value, Fixed(60)),  // the type empties it
            (role, Fixed(600)),                                  // the duration empties it
        })
        {
            var service = Service.Create("Workshop", duration, [configuredRole]).Value;
            var resolution = Wire(new InMemoryServiceStore().Add(service), rooms);

            var chain = await resolution.ResolveAsync(configuredRole, duration);
            var pool = await resolution.ResolveCandidatesAsync(service.Id);

            Assert.True(pool.Succeeded);
            Assert.Equal(
                chain.Candidates.Select(c => c.ResourceId).OrderBy(id => id),
                pool.Value.Select(c => c.ResourceId).OrderBy(id => id));
        }
    }

    [Fact]
    public async Task Spec_scenario_unknown_service()
    {
        var result = await Wire(TenRooms()).ResolveCandidatesAsync(Guid.NewGuid());

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.ServiceNotFound, Assert.Single(result.Failures).Code);
    }

    [Fact]
    public async Task Spec_scenario_no_required_capabilities_matches_the_whole_type()
    {
        var chain = await Wire(TenRooms()).ResolveAsync(Role(), Fixed(60));

        Assert.Equal(10, chain.OfType.Count);
        Assert.Equal(10, chain.WithCapabilities.Count);
    }

    [Fact]
    public async Task Each_stage_is_a_subset_of_the_one_before_it()
    {
        // What makes "the count entering a stage and the count leaving it are
        // both derivable" true: a stage is not an independently computed set.
        var chain = await Wire(TenRooms()).ResolveAsync(Role("projector"), Fixed(240));

        Assert.All(chain.WithCapabilities, r => Assert.Contains(r, chain.OfType));
        Assert.All(chain.Candidates, c => Assert.Contains(c.Resource, chain.WithCapabilities));

        // The duration stage accounts for everything the capability stage passed:
        // a resource is a candidate or it is an exclusion, never neither.
        Assert.Equal(
            chain.WithCapabilities.Count,
            chain.Candidates.Count + chain.DurationExclusions.Count);
    }
}
