using UBookIt.Core.Common;
using UBookIt.Core.Services;

namespace UBookIt.Tests;

/// <summary>
/// The `Service` aggregate factory: valid single-role services across both
/// duration kinds, and each validation rule with its stable code. Duration
/// validity itself belongs to <see cref="ServiceDurationTests"/> — an invalid
/// `ServiceDuration` cannot be constructed, so it cannot reach this factory.
/// </summary>
public class ServiceTests
{
    private static ServiceRole Role(string type = "person", int count = 1) => new(type, count);

    private static ServiceRole WithCapabilities(ServiceRole role, params string[] keys)
        => role with { RequiredCapabilities = CapabilitySet.Create(keys, CapabilitySet.RequiredField).Value };

    private static ServiceDuration Fixed(int minutes)
        => ServiceDuration.Fixed(TimeSpan.FromMinutes(minutes)).Value;

    [Fact]
    public void Valid_service_with_fixed_duration()
    {
        var result = Service.Create("Massage", Fixed(60), [Role("person")]);

        Assert.True(result.Succeeded);
        Assert.Equal("Massage", result.Value.Name);
        Assert.Equal(ServiceDurationKind.Fixed, result.Value.Duration.Kind);
        Assert.Equal(TimeSpan.FromMinutes(60), result.Value.Duration.FixedLength);
        var role = Assert.Single(result.Value.Roles);
        Assert.Equal("person", role.ResourceType);
        Assert.Equal(1, role.Count);
    }

    [Fact]
    public void Valid_service_with_bounded_variable_duration()
    {
        var duration = ServiceDuration.Variable(TimeSpan.FromMinutes(45), TimeSpan.FromMinutes(120)).Value;

        var result = Service.Create("Room hire", duration, [Role("room")]);

        Assert.True(result.Succeeded);
        Assert.Equal(ServiceDurationKind.Variable, result.Value.Duration.Kind);
        Assert.Equal(TimeSpan.FromMinutes(45), result.Value.Duration.Min);
        Assert.Equal(TimeSpan.FromMinutes(120), result.Value.Duration.Max);
    }

    [Fact]
    public void Omitted_duration_is_unbounded_variable()
    {
        // The unconfigured default defers entirely to the resource's own range,
        // rather than pinning every booking to the resource minimum.
        var result = Service.Create("Consultation", null, [Role("person")]);

        Assert.True(result.Succeeded);
        Assert.Equal(ServiceDurationKind.Variable, result.Value.Duration.Kind);
        Assert.Null(result.Value.Duration.Min);
        Assert.Null(result.Value.Duration.Max);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_name_is_rejected(string? name)
    {
        var result = Service.Create(name, Fixed(30), [Role()]);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures, f => f.Code == FailureCodes.ServiceNameRequired);
    }

    [Fact]
    public void Zero_roles_is_rejected()
    {
        var result = Service.Create("X", null, []);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures, f => f.Code == FailureCodes.ServiceRoleInvalid);
    }

    [Fact]
    public void Several_roles_of_different_types_are_accepted()
    {
        var result = Service.Create("Massage", null, [Role("room"), Role("therapist")]);

        Assert.True(result.Succeeded);
        Assert.Equal(["room", "therapist"], result.Value.Roles.Select(r => r.ResourceType));
        Assert.All(result.Value.Roles, r => Assert.Equal(1, r.Count));
    }

    [Fact]
    public void Two_roles_of_one_type_with_differing_capabilities_are_accepted()
    {
        // The configuration this whole change exists for: "a senior therapist and
        // any therapist". The pools overlap without being equal, which is exactly
        // what the assignment resolves — so this is no longer a rejection.
        var result = Service.Create(
            "X",
            null,
            [WithCapabilities(Role("therapist"), "cert-x"), Role("therapist")]);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Value.Roles.Count);
        Assert.All(result.Value.Roles, r => Assert.Equal("therapist", r.ResourceType));
    }

    [Fact]
    public void Differing_capabilities_make_two_roles_of_one_type_distinct()
    {
        // Every dimension other than the type varied at once. Under the narrowed
        // rule this must be *accepted*: a rule still comparing type alone fails
        // here, and so does one comparing capability sets by size.
        var result = Service.Create(
            "X",
            null,
            [
                WithCapabilities(Role("therapist"), "cert-x", "cert-y"),
                WithCapabilities(Role("therapist"), "cert-z"),
            ]);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Value.Roles.Count);
    }

    [Fact]
    public void Capability_sets_are_compared_by_equality_not_by_containment()
    {
        // {cert-x} against {cert-x, cert-y}: they share a capability and one
        // contains the other, but they are not the same requirement, so this is
        // two roles rather than one repeated. A rule reaching for IsSatisfiedBy —
        // the eligibility test, which is a subset test — would wrongly reject it.
        var result = Service.Create(
            "X",
            null,
            [
                WithCapabilities(Role("therapist"), "cert-x"),
                WithCapabilities(Role("therapist"), "cert-x", "cert-y"),
            ]);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void The_same_capabilities_supplied_in_a_different_order_are_still_a_duplicate()
    {
        // Capability sets are ordinally sorted by their value object, so a
        // requirement's spelling cannot depend on typing order. If the duplicate
        // rule ever compared the keys as supplied, this pair would slip through and
        // produce two roles that mean one thing.
        var result = Service.Create(
            "X",
            null,
            [
                WithCapabilities(Role("therapist"), "cert-x", "welsh"),
                WithCapabilities(Role("therapist"), "welsh", "cert-x"),
            ]);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures, f => f.Code == FailureCodes.ServiceRoleDuplicateType);
    }

    [Fact]
    public void Two_identical_roles_are_rejected_in_favour_of_a_count()
    {
        // Two spellings of one requirement. A count expresses it exactly once, so
        // the failure names the count as the correction rather than leaving the fix
        // to be guessed at.
        var result = Service.Create("X", null, [Role("therapist"), Role("therapist")]);

        Assert.False(result.Succeeded);

        var failure = Assert.Single(result.Failures, f => f.Code == FailureCodes.ServiceRoleDuplicateType);
        Assert.Contains("therapist", failure.Message, StringComparison.Ordinal);
        Assert.Contains("count", failure.Message, StringComparison.OrdinalIgnoreCase);

        // Distinct from the malformed-key code: one is a typo in a key, the other a
        // requirement stated twice, and they are corrected differently.
        Assert.DoesNotContain(result.Failures, f => f.Code == FailureCodes.TypeKeyInvalid);
    }

    [Fact]
    public void A_duplicate_type_is_attributed_to_the_repeated_role()
    {
        var result = Service.Create("X", null, [Role("room"), Role("therapist"), Role("therapist")]);

        var failure = Assert.Single(result.Failures, f => f.Code == FailureCodes.ServiceRoleDuplicateType);

        // The row that repeated the type, not the row that introduced it.
        Assert.Equal("Roles[2].ResourceType", failure.Field);
    }

    [Fact]
    public void Role_order_is_not_observable()
    {
        var forwards = Service.Create("X", null, [WithCapabilities(Role("room"), "projector"), Role("therapist")]);
        var backwards = Service.Create("X", null, [Role("therapist"), WithCapabilities(Role("room"), "projector")]);

        Assert.True(forwards.Succeeded);
        Assert.True(backwards.Succeeded);

        // Compared as *sequences*, without sorting either side first. Sorting
        // both before comparing would assert only that the two contain the same
        // roles, which is true however the aggregate stores them — the claim
        // here is stronger: the aggregate itself is order-insensitive, so
        // nothing downstream can observe what order the caller supplied.
        Assert.Equal(forwards.Value.Roles, backwards.Value.Roles);
    }

    [Fact]
    public void A_role_may_require_several_resources()
    {
        var result = Service.Create("Workshop", null, [Role("room", 2)]);

        Assert.True(result.Succeeded);
        Assert.Equal(2, Assert.Single(result.Value.Roles).Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(ServiceRole.MaxCount + 1)]
    public void An_out_of_range_count_is_rejected_with_its_own_code(int count)
    {
        var result = Service.Create("X", null, [Role("person", count)]);

        Assert.False(result.Succeeded);

        var failure = Assert.Single(result.Failures, f => f.Code == FailureCodes.ServiceRoleCountInvalid);

        // Its own code, and against the count control of the offending row — not
        // the general role code, which a consumer cannot place.
        Assert.Equal("Roles[0].Count", failure.Field);
    }

    [Fact]
    public void The_maximum_count_itself_is_accepted()
    {
        // The boundary, stated: a test asserting only that MaxCount + 1 fails would
        // pass an implementation that rejected MaxCount too.
        Assert.True(Service.Create("X", null, [Role("room", ServiceRole.MaxCount)]).Succeeded);
    }

    [Fact]
    public void A_count_larger_than_the_pool_is_accepted()
    {
        // A count exceeding the eligible resources is a property of the *pool*, not
        // of the service: resources may be added later, and refusing the save would
        // block a configuration that is not wrong (design D8). The factory
        // deliberately knows nothing about which resources exist — asserted here so
        // nobody later "fixes" that into a rejection.
        Assert.True(Service.Create("X", null, [Role("unicorn", 3)]).Succeeded);
    }

    [Fact]
    public void A_role_validated_on_its_own_bounds_its_count_identically()
    {
        // The preview path validates a role with no service around it, and the rule
        // has one implementation, so the bound has to hold there too.
        Assert.False(ServiceRole.Create("room", null, 0).Succeeded);
        Assert.False(ServiceRole.Create("room", null, ServiceRole.MaxCount + 1).Succeeded);
        Assert.True(ServiceRole.Create("room", null, 2).Succeeded);
    }

    [Fact]
    public void Two_same_type_roles_hold_a_stable_canonical_order()
    {
        // Type alone no longer distinguishes these two, and `List.Sort` is unstable
        // — so without the capability tiebreak they could exchange places between
        // saves, breaking round-trip equality and the delivery contract's
        // deterministic role order (design D6).
        //
        // Supplied in both orders, because a test using one order passes on an
        // accident of insertion order.
        var forwards = Service.Create(
            "X",
            null,
            [WithCapabilities(Role("therapist"), "cert-x"), WithCapabilities(Role("therapist"), "welsh")]);

        var backwards = Service.Create(
            "X",
            null,
            [WithCapabilities(Role("therapist"), "welsh"), WithCapabilities(Role("therapist"), "cert-x")]);

        Assert.True(forwards.Succeeded);
        Assert.True(backwards.Succeeded);
        Assert.Equal(forwards.Value.Roles, backwards.Value.Roles);
        Assert.Equal(
            ["cert-x", "welsh"],
            forwards.Value.Roles.Select(r => r.RequiredCapabilities.Keys.Single()));
    }

    [Fact]
    public void The_canonical_order_falls_through_to_the_count()
    {
        // The third term. Two roles cannot tie on type and capabilities — that is
        // the duplicate rule — so the count term only ever separates roles already
        // separated by capabilities. It is asserted anyway, because a comparer
        // returning 0 for the first two terms and stopping is exactly the shape
        // that reintroduces the unstable sort.
        var forwards = Service.Create(
            "X", null, [Role("room", 3), WithCapabilities(Role("room", 1), "projector")]);

        var backwards = Service.Create(
            "X", null, [WithCapabilities(Role("room", 1), "projector"), Role("room", 3)]);

        Assert.True(forwards.Succeeded);
        Assert.Equal(forwards.Value.Roles, backwards.Value.Roles);

        // An empty capability set sorts before a non-empty one, so the count-3 role
        // leads.
        Assert.Equal([3, 1], forwards.Value.Roles.Select(r => r.Count));
    }

    [Fact]
    public void Non_normalized_role_type_is_rejected()
    {
        var result = Service.Create("X", null, [Role("Meeting Room")]);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures, f => f.Code == FailureCodes.TypeKeyInvalid);
    }

    [Fact]
    public void Multiple_failures_reported_together()
    {
        var result = Service.Create("  ", null, [Role("Bad Type")]);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures, f => f.Code == FailureCodes.ServiceNameRequired);
        Assert.Contains(result.Failures, f => f.Code == FailureCodes.TypeKeyInvalid);
    }
}
