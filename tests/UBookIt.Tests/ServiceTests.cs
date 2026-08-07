using UBookIt.Core.Common;
using UBookIt.Core.Services;

namespace UBookIt.Tests;

/// <summary>
/// The `Service` aggregate factory: valid single-role services (with/without
/// duration) and each validation rule with its stable code.
/// </summary>
public class ServiceTests
{
    private static ServiceRole Role(string type = "person", int count = 1) => new(type, count);

    [Fact]
    public void Valid_service_with_duration()
    {
        var result = Service.Create("Massage", TimeSpan.FromMinutes(60), [Role("person")]);

        Assert.True(result.Succeeded);
        Assert.Equal("Massage", result.Value.Name);
        Assert.Equal(TimeSpan.FromMinutes(60), result.Value.Duration);
        var role = Assert.Single(result.Value.Roles);
        Assert.Equal("person", role.ResourceType);
        Assert.Equal(1, role.Count);
    }

    [Fact]
    public void Duration_is_optional()
    {
        var result = Service.Create("Consultation", null, [Role("person")]);

        Assert.True(result.Succeeded);
        Assert.Null(result.Value.Duration);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_name_is_rejected(string? name)
    {
        var result = Service.Create(name, TimeSpan.FromMinutes(30), [Role()]);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures, f => f.Code == FailureCodes.ServiceNameRequired);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-15)]
    public void Non_positive_duration_is_rejected(int minutes)
    {
        var result = Service.Create("X", TimeSpan.FromMinutes(minutes), [Role()]);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures, f => f.Code == FailureCodes.ServiceDurationInvalid);
    }

    [Fact]
    public void Sub_minute_duration_is_rejected()
    {
        // Duration persists/round-trips as whole minutes; 90 seconds must not
        // be silently truncated to 1 minute.
        var result = Service.Create("X", TimeSpan.FromSeconds(90), [Role()]);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures, f => f.Code == FailureCodes.ServiceDurationInvalid);
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
