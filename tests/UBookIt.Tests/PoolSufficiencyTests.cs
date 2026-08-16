using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Resources;
using UBookIt.Core.Services;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The configuration-time pool-sufficiency check (service-booking spec, "A
/// structurally insufficient pool is detectable", and design D1/D2/D3).
/// <para>
/// <b>The fixture trap this suite is built around</b> (task 2.4). A check
/// implemented as "is any role's count greater than its own pool size" passes
/// every fixture whose pools are disjoint, and disjoint is the shape most test
/// data takes. <see cref="Spec_scenario_overlapping_pools_are_judged_together"/>
/// and <see cref="Two_roles_of_one_type_over_one_resource_are_reported_together"/>
/// are therefore not decoration: in both, every role considered alone has enough
/// candidates and the roles together do not. Without them the suite cannot tell
/// an assignment from a per-role count comparison.
/// </para>
/// <para>
/// Every pool is resolved through <see cref="ServiceBookingService"/> rather than
/// assembled by hand, so the candidates are the ones the booking path acts on and
/// a fixture cannot quietly describe a pool resolution would never produce.
/// </para>
/// </summary>
public class PoolSufficiencyTests
{
    private const string Therapist = "therapist";

    private const string CertX = "cert-x";

    private const string Welsh = "welsh";

    private static Guid Id(int n) => new($"00000000-0000-0000-0000-{n:x12}");

    /// <summary>
    /// A therapist open on the fixture's day, optionally carrying capabilities so
    /// two roles of one type can be given pools that overlap — or that do not — on
    /// purpose.
    /// </summary>
    private static Resource Therapy(int id, string name, params string[] capabilities)
        => Resource.Create(
            Therapist,
            name,
            capabilities: capabilities,
            availability: TestData.Config(TestData.Weekly("09:00", "18:00", TestData.BaseDate.DayOfWeek)),
            id: Id(id)).Value;

    /// <summary>A resource of a second type, for the roles that must not overlap.</summary>
    private static Resource Room(int id, string name, string open = "09:00", string close = "18:00")
        => Resource.Create(
            ResourceTypes.Room,
            name,
            availability: TestData.Config(TestData.Weekly(open, close, TestData.BaseDate.DayOfWeek)),
            id: Id(id)).Value;

    private static Service ServiceOf(params ServiceRole[] roles)
        => Service.Create("Treatment", ServiceDuration.Variable(null, null).Value, roles).Value;

    private static ServiceRole Role(string type, int count = 1, params string[] capabilities)
        => ServiceRole.Create(type, capabilities, count).Value;

    private static (ServiceBookingService Booking, Service Service, BookingService Bookings) Wire(
        Service service, params Resource[] resources)
    {
        var resourceStore = new InMemoryResourceStore();
        foreach (var resource in resources)
        {
            resourceStore.Add(resource);
        }

        var wired = TestData.ServiceBookingWith(new InMemoryServiceStore().Add(service), resourceStore);

        return (wired.Services, service, wired.Bookings);
    }

    /// <summary>
    /// The finding for a saved service, computed from the pools the booking path
    /// resolves — never from a second eligibility filter of this test's own
    /// (⑧a design D1).
    /// </summary>
    private static async Task<RoleShortfall?> Check(ServiceBookingService booking, Guid serviceId)
    {
        var pools = await booking.ResolveCandidatesAsync(serviceId);

        Assert.True(pools.Succeeded, "resolution failed");

        return PoolSufficiency.FindShortfall(pools.Value);
    }

    [Fact]
    public async Task Spec_scenario_a_count_exceeding_the_eligible_pool_is_reported()
    {
        var (booking, service, _) = Wire(
            ServiceOf(Role(Therapist, count: 2)),
            Therapy(1, "Mary"));

        var finding = await Check(booking, service.Id);

        Assert.NotNull(finding);
        Assert.Equal([Therapist], finding.Roles.Select(r => r.ResourceType));
        Assert.Equal(2, finding.Required);
        Assert.Equal(1, finding.Eligible);
    }

    [Fact]
    public async Task Two_roles_of_one_type_over_one_resource_are_reported_together()
    {
        // The case ⑨-2 left deliberately silent: the start-grid check skips a
        // resource paired with itself, so it correctly reports no misalignment —
        // these roles are short of resources rather than misaligned, and this is
        // the finding that says so.
        //
        // Note what the naive check misses. Each role alone has a candidate; only
        // the pair is short.
        var (booking, service, _) = Wire(
            ServiceOf(Role(Therapist, 1, CertX), Role(Therapist)),
            Therapy(1, "Mary", CertX));

        var finding = await Check(booking, service.Id);

        Assert.NotNull(finding);
        Assert.Equal(2, finding.Roles.Count);
        Assert.Equal(2, finding.Required);
        Assert.Equal(1, finding.Eligible);

        // Both rows are named, in the order the pools were supplied — here the
        // aggregate's canonical order, which puts the capability-free role first —
        // and the one carrying a capability is distinguishable from the one that
        // does not. "therapist and therapist" would leave an editor guessing.
        Assert.Empty(finding.Roles[0].RequiredCapabilities.Keys);
        Assert.Equal([CertX], finding.Roles[1].RequiredCapabilities.Keys);

        // And the neighbouring signal really is silent here, which is what makes
        // this finding the only thing standing between the editor and a service
        // that can never be booked.
        var pools = await booking.ResolveCandidatesAsync(service.Id);
        Assert.Null(StartAlignment.FindMisalignment(pools.Value));
    }

    [Fact]
    public async Task A_healthy_role_beside_a_deficient_pair_is_in_neither_number()
    {
        // The finding is a claim about a *subset* of the configuration, and this is
        // the fixture that holds both halves of that to account: the room role is
        // healthy and must appear in neither the named roles nor the eligible
        // count. A report that named every role, or that counted every resource the
        // service can reach, would say "3 roles need 3 resources and 2 are
        // eligible" — arithmetic that is true of nothing and sends an editor to a
        // row that is already correct.
        var (booking, service, _) = Wire(
            ServiceOf(Role(ResourceTypes.Room), Role(Therapist, 1, CertX), Role(Therapist)),
            Room(1, "Red Room"),
            Therapy(2, "Mary", CertX));

        var finding = await Check(booking, service.Id);

        Assert.NotNull(finding);
        Assert.Equal([Therapist, Therapist], finding.Roles.Select(r => r.ResourceType));
        Assert.Equal(2, finding.Required);
        Assert.Equal(1, finding.Eligible);
    }

    [Fact]
    public async Task Spec_scenario_overlapping_pools_are_judged_together()
    {
        // Two roles of one type, each of count 2, over three resources of which
        // both roles can use only two: Mary and Non carry `cert-x`, Gwen carries
        // `welsh` alone.
        //
        //   role 0 — {cert-x}, count 2 → {Mary, Non}
        //   role 1 — {},       count 2 → {Mary, Non, Gwen}
        //
        // Four distinct resources are needed and three exist, so the pair is
        // deficient — but role 0 alone has exactly the two it asks for, and role 1
        // alone has three. A per-role count comparison reports nothing here.
        var (booking, service, _) = Wire(
            ServiceOf(Role(Therapist, 2, CertX), Role(Therapist, 2)),
            Therapy(1, "Mary", CertX),
            Therapy(2, "Non", CertX),
            Therapy(3, "Gwen", Welsh));

        var finding = await Check(booking, service.Id);

        Assert.NotNull(finding);
        Assert.Equal(2, finding.Roles.Count);
        Assert.Equal(4, finding.Required);
        Assert.Equal(3, finding.Eligible);
    }

    [Fact]
    public async Task Spec_scenario_distinct_resources_satisfying_one_role_each_is_sufficient()
    {
        // Incomparable capability sets, so neither pool contains the other and a
        // greedy walk in role order would strand the second role. An assignment
        // exists — Mary to the first role, Gwen to the second — so nothing is
        // reported.
        var (booking, service, _) = Wire(
            ServiceOf(Role(Therapist, 1, CertX), Role(Therapist, 1, Welsh)),
            Therapy(1, "Mary", CertX),
            Therapy(2, "Gwen", Welsh));

        Assert.Null(await Check(booking, service.Id));
    }

    [Fact]
    public async Task Spec_scenario_a_sufficient_pool_is_not_reported_as_a_positive_finding()
    {
        // Silence, not a record reporting a shortfall of zero. The distinction is
        // the one-directional stance: a positive finding would be read as a claim
        // that the service can be booked, which this check evaluates nothing to
        // support.
        var (booking, service, _) = Wire(
            ServiceOf(Role(ResourceTypes.Room), Role(Therapist, count: 2)),
            Room(1, "Red Room"),
            Therapy(2, "Mary"),
            Therapy(3, "Gwen"));

        Assert.Null(await Check(booking, service.Id));
    }

    [Fact]
    public async Task Spec_scenario_the_deficient_set_names_roles_rather_than_slots()
    {
        // A role of count 3 is three slots internally and one row on screen. The
        // report names the row and the three distinct resources it needs — never
        // the expansion, which an editor has no way to act on.
        var (booking, service, _) = Wire(
            ServiceOf(Role(Therapist, count: 3)),
            Therapy(1, "Mary"),
            Therapy(2, "Gwen"));

        var finding = await Check(booking, service.Id);

        Assert.NotNull(finding);
        Assert.Single(finding.Roles);
        Assert.Equal(3, finding.Roles[0].Count);
        Assert.Equal(3, finding.Required);
        Assert.Equal(2, finding.Eligible);
    }

    [Fact]
    public async Task Spec_scenario_bookings_do_not_change_the_answer()
    {
        // ⑨-1a design D2, restated for this check: a structural report computed
        // from free time would appear and disappear as bookings came and went, and
        // would withdraw itself precisely while an editor was performing the repair
        // it asked for.
        var (booking, service, bookings) = Wire(
            ServiceOf(Role(Therapist, count: 2)),
            Therapy(1, "Mary"));

        var before = await Check(booking, service.Id);

        // Eight hours of a nine-hour window — the resource's maximum, so this is as
        // full as one booking can make it.
        var placed = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = Id(1),
            Start = TestData.Utc(TestData.BaseDate, "09:00"),
            Duration = TimeSpan.FromHours(8),
            Booker = TestData.Booker(),
        });

        Assert.True(placed.Succeeded, "the fixture booking did not place");

        // The calendar really did change, which is what makes the equality below
        // worth asserting: a fixture whose booking quietly failed would prove
        // nothing about where the check reads from.
        var overlapping = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = Id(1),
            Start = TestData.Utc(TestData.BaseDate, "10:00"),
            Duration = TimeSpan.FromMinutes(60),
            Booker = TestData.Booker(),
        });

        Assert.False(overlapping.Succeeded);

        var after = await Check(booking, service.Id);

        Assert.NotNull(before);
        Assert.NotNull(after);
        Assert.Equal(before.Required, after.Required);
        Assert.Equal(before.Eligible, after.Eligible);
    }

    [Fact]
    public async Task Spec_scenario_a_sufficient_pool_says_nothing_about_bookability()
    {
        // Sufficiency is necessary and not sufficient, and this is the fixture that
        // proves the report knows it: two roles, two resources, an assignment
        // plainly exists — and the resources are never open at the same time, so
        // there is no bookable start at all.
        var (booking, service, _) = Wire(
            ServiceOf(Role(ResourceTypes.Room), Role(Therapist)),
            Room(1, "Red Room", "09:00", "12:00"),
            Resource.Create(
                Therapist,
                "Mary",
                availability: TestData.Config(
                    TestData.Weekly("13:00", "18:00", TestData.BaseDate.DayOfWeek)),
                id: Id(2)).Value);

        Assert.Null(await Check(booking, service.Id));

        var starts = await booking.GetBookableStartsAsync(
            service.Id, TestData.BaseDate, TestData.BaseDate);

        Assert.True(starts.Succeeded);
        Assert.Empty(starts.Value);
    }

    [Fact]
    public async Task Spec_scenario_detection_changes_nothing_else()
    {
        // The check reports; it must not reject, block or alter anything. A count
        // above the pool remains a valid service — ⑨-2 design D8 decided that
        // deliberately, because resources may be added later.
        var service = ServiceOf(Role(Therapist, count: 2));
        var (booking, _, _) = Wire(service, Therapy(1, "Mary"));

        var pools = await booking.ResolveCandidatesAsync(service.Id);

        Assert.True(pools.Succeeded);
        Assert.Single(pools.Value.Single().Candidates);

        // Saving one is still a valid service: Service.Create accepted it above,
        // and nothing about the finding changes that.
        Assert.True(Service.Create(
            "Treatment",
            ServiceDuration.Variable(null, null).Value,
            [Role(Therapist, count: 2)]).Succeeded);

        var starts = await booking.GetBookableStartsAsync(
            service.Id, TestData.BaseDate, TestData.BaseDate);

        Assert.True(starts.Succeeded);
        Assert.Empty(starts.Value);

        Assert.NotNull(await Check(booking, service.Id));
    }

    [Fact]
    public async Task A_role_whose_pool_is_empty_is_reported_on_its_own()
    {
        // The degenerate shortfall, and worth pinning: one role needs one resource
        // and nothing is eligible. The other role is not dragged into the finding,
        // because it is not part of what cannot be satisfied.
        var (booking, service, _) = Wire(
            ServiceOf(Role(ResourceTypes.Room), Role(Therapist, 1, CertX)),
            Room(1, "Red Room"),
            Therapy(2, "Mary"));

        var finding = await Check(booking, service.Id);

        Assert.NotNull(finding);
        Assert.Equal([Therapist], finding.Roles.Select(r => r.ResourceType));
        Assert.Equal(1, finding.Required);
        Assert.Equal(0, finding.Eligible);
    }
}
