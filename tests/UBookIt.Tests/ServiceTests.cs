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
    public void Two_roles_of_the_same_type_are_rejected()
    {
        // The pair the restriction exists for: same type, different capabilities,
        // so the pools overlap without being equal.
        var result = Service.Create(
            "X",
            null,
            [WithCapabilities(Role("therapist"), "cert-x"), Role("therapist")]);

        Assert.False(result.Succeeded);

        var failure = Assert.Single(result.Failures, f => f.Code == FailureCodes.ServiceRoleDuplicateType);
        Assert.Contains("therapist", failure.Message, StringComparison.Ordinal);

        // Distinct from the malformed-key code: one is a typo, the other a
        // composition this version does not support, and they are corrected
        // differently.
        Assert.DoesNotContain(result.Failures, f => f.Code == FailureCodes.TypeKeyInvalid);
    }

    [Fact]
    public void Differing_capabilities_do_not_make_two_roles_distinct()
    {
        // Every dimension of a role other than its type varied at once: if the
        // rule ever compared more than the type, this is the case that catches it.
        var result = Service.Create(
            "X",
            null,
            [
                WithCapabilities(Role("therapist"), "cert-x", "cert-y"),
                WithCapabilities(Role("therapist"), "cert-z"),
            ]);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures, f => f.Code == FailureCodes.ServiceRoleDuplicateType);
    }

    [Fact]
    public void Two_roles_requiring_no_capabilities_of_the_same_type_are_rejected()
    {
        // The degenerate instance of the same rule — identical roles. Kept
        // alongside the differing-capability case so neither can pass alone.
        var result = Service.Create("X", null, [Role("therapist"), Role("therapist")]);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures, f => f.Code == FailureCodes.ServiceRoleDuplicateType);
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

        // The same set of roles either way round: nothing downstream may depend
        // on the order they were supplied in.
        Assert.Equal(
            forwards.Value.Roles.OrderBy(r => r.ResourceType, StringComparer.Ordinal),
            backwards.Value.Roles.OrderBy(r => r.ResourceType, StringComparer.Ordinal));
    }

    [Fact]
    public void Role_count_other_than_one_is_rejected_in_v1()
    {
        var result = Service.Create("X", null, [Role("person", 2)]);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures, f => f.Code == FailureCodes.ServiceRoleInvalid);
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
