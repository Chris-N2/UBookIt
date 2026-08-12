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
    public void Multiple_roles_are_rejected_in_v1()
    {
        var result = Service.Create("X", null, [Role("room"), Role("person")]);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures, f => f.Code == FailureCodes.ServiceRoleInvalid);
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
