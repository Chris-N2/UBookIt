using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Core.Services;

namespace UBookIt.Tests;

/// <summary>
/// The capability value object and the domain validation around it (resources
/// spec, "Capability keys are extensible normalized keys" and "A capability set
/// is a value object with structural equality").
/// <para>
/// Scenarios are derived from the spec text, not from the implementation.
/// </para>
/// </summary>
public class CapabilitySetTests
{
    private static CapabilitySet Set(params string[] keys) => CapabilitySet.Create(keys).Value;

    [Fact]
    public void Sets_with_the_same_keys_are_equal_regardless_of_order_or_duplication()
    {
        var one = Set("cert-x", "massage");
        var other = Set("massage", "cert-x", "massage");

        Assert.Equal(one, other);
        Assert.Equal(one.GetHashCode(), other.GetHashCode());
        Assert.Equal(2, one.Count);
        Assert.Equal(2, other.Count);
    }

    [Fact]
    public void A_record_carrying_a_capability_set_compares_by_value()
    {
        // The reason this type exists rather than a bare set: ServiceRole is a
        // record, and a set member would give the generated Equals reference
        // semantics, so two roles requiring the same capabilities would compare
        // unequal and look like a persistence bug.
        var one = new ServiceRole("therapist", 1) { RequiredCapabilities = Set("cert-x") };
        var other = new ServiceRole("therapist", 1) { RequiredCapabilities = Set("cert-x") };

        Assert.Equal(one, other);
    }

    [Fact]
    public void Records_differing_only_by_capabilities_are_not_equal()
    {
        var one = new ServiceRole("therapist", 1) { RequiredCapabilities = Set("cert-x") };
        var other = new ServiceRole("therapist", 1) { RequiredCapabilities = Set("welsh") };

        Assert.NotEqual(one, other);
    }

    [Fact]
    public void A_superset_satisfies_the_requirement()
        => Assert.True(Set("cert-x").IsSatisfiedBy(Set("cert-x", "massage")));

    [Fact]
    public void A_missing_capability_fails_the_requirement()
        => Assert.False(Set("cert-x", "welsh").IsSatisfiedBy(Set("cert-x", "massage")));

    [Fact]
    public void An_empty_requirement_is_satisfied_by_anything()
    {
        Assert.True(CapabilitySet.Empty.IsSatisfiedBy(Set("cert-x")));
        Assert.True(CapabilitySet.Empty.IsSatisfiedBy(CapabilitySet.Empty));
    }

    [Fact]
    public void A_requirement_is_not_satisfied_by_an_empty_held_set()
        => Assert.False(Set("cert-x").IsSatisfiedBy(CapabilitySet.Empty));

    [Fact]
    public void The_subset_test_is_directional()
    {
        // Both assertions together are what pin the direction. Either one alone
        // passes against an implementation with the operands swapped, and a
        // swapped implementation is silently wrong in the dangerous direction:
        // it would admit under-qualified resources whenever the requirement is
        // the larger set.
        var smaller = Set("cert-x");
        var larger = Set("cert-x", "massage");

        Assert.True(smaller.IsSatisfiedBy(larger));
        Assert.False(larger.IsSatisfiedBy(smaller));
    }

    [Fact]
    public void The_subset_test_is_not_an_intersection_test()
    {
        // An "any overlap" implementation passes every satisfied case above.
        // Only a partial overlap distinguishes it from containment.
        Assert.False(Set("cert-x", "welsh").IsSatisfiedBy(Set("cert-x")));
    }

    [Fact]
    public void A_non_normalized_key_is_rejected()
    {
        var result = CapabilitySet.Create(["Cert X"]);

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.CapabilityKeyInvalid, Assert.Single(result.Failures).Code);
    }

    [Fact]
    public void Keys_are_rejected_rather_than_silently_normalized()
    {
        // Accepting "Massage" as "massage" would store a value the editor did
        // not type, which the type key rule does not do either.
        var result = CapabilitySet.Create(["Massage"]);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Every_malformed_key_is_reported_not_only_the_first()
    {
        var result = CapabilitySet.Create(["Cert X", "ok-key", "another bad"]);

        Assert.False(result.Succeeded);
        Assert.Equal(2, result.Failures.Count);
    }

    [Fact]
    public void An_unknown_but_well_formed_key_is_accepted()
        => Assert.True(CapabilitySet.Create(["some-capability-never-seen-before"]).Succeeded);

    [Fact]
    public void A_null_collection_yields_the_empty_set()
    {
        var result = CapabilitySet.Create(null);

        Assert.True(result.Succeeded);
        Assert.True(result.Value.IsEmpty);
    }

    [Fact]
    public void Keys_are_exposed_in_a_deterministic_order()
    {
        Assert.Equal(["a-key", "m-key", "z-key"], Set("z-key", "a-key", "m-key").Keys);
        Assert.Equal(["a-key", "m-key", "z-key"], Set("m-key", "z-key", "a-key").Keys);
    }

    [Fact]
    public void The_failure_field_identifies_which_control_is_at_fault()
    {
        var required = CapabilitySet.Create(["Bad Key"], CapabilitySet.RequiredField);

        Assert.Equal(CapabilitySet.RequiredField, Assert.Single(required.Failures).Field);
    }
}

/// <summary>
/// Capability validation through the aggregate factories (resources spec,
/// services spec).
/// </summary>
public class CapabilityValidationTests
{
    [Fact]
    public void A_resource_created_without_capabilities_carries_none()
    {
        var resource = Resource.Create(ResourceTypes.Room, "Meeting Room A");

        Assert.True(resource.Succeeded);
        Assert.True(resource.Value.Capabilities.IsEmpty);
    }

    [Fact]
    public void A_resource_carries_the_capabilities_it_is_given()
    {
        var resource = Resource.Create(ResourceTypes.Room, "Meeting Room A", capabilities: ["projector", "step-free"]);

        Assert.True(resource.Succeeded);
        Assert.Equal(["projector", "step-free"], resource.Value.Capabilities.Keys);
    }

    [Fact]
    public void A_resource_with_a_malformed_capability_is_rejected()
    {
        var resource = Resource.Create(ResourceTypes.Room, "Meeting Room A", capabilities: ["Cert X"]);

        Assert.False(resource.Succeeded);
        Assert.Contains(resource.Failures, f => f.Code == FailureCodes.CapabilityKeyInvalid);
    }

    [Fact]
    public void Capability_and_type_failures_are_separately_reported()
    {
        // Distinct codes are what let a consumer associate each message with the
        // control it belongs to, rather than showing both against one field.
        var resource = Resource.Create("Meeting Room", "Meeting Room A", capabilities: ["Cert X"]);

        Assert.False(resource.Succeeded);
        Assert.Contains(resource.Failures, f => f.Code == FailureCodes.TypeKeyInvalid);
        Assert.Contains(resource.Failures, f => f.Code == FailureCodes.CapabilityKeyInvalid);
    }

    [Fact]
    public void A_role_created_without_capabilities_requires_none()
    {
        var service = Service.Create("Massage", null, [new ServiceRole("therapist", 1)]);

        Assert.True(service.Succeeded);
        Assert.True(service.Value.Roles[0].RequiredCapabilities.IsEmpty);
    }

    [Fact]
    public void A_role_carries_the_capabilities_it_requires()
    {
        var service = Service.Create(
            "Massage",
            null,
            [new ServiceRole("therapist", 1) { RequiredCapabilities = CapabilitySet.Create(["cert-x", "welsh"]).Value }]);

        Assert.True(service.Succeeded);
        Assert.Equal(["cert-x", "welsh"], service.Value.Roles[0].RequiredCapabilities.Keys);
    }
}
