using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Core.Services;
using UBookIt.Web.Mapping;

namespace UBookIt.Tests;

/// <summary>
/// Capabilities on the public delivery contract (delivery-api spec, "Resource
/// read model" and "Service read model").
/// <para>
/// Publication is not decoration here — it is what discharges ⑦-2 design D9.
/// The <c>resource-not-eligible</c> failure discloses pool membership, and it is
/// acceptable only while membership is derivable from public reads. Capabilities
/// constrain eligibility, so if they were not published the probe would start
/// disclosing something new. The last test is the one that actually pins that
/// property: a consumer computing the pool from the two published reads must get
/// the same answer Core resolves.
/// </para>
/// </summary>
public class CapabilityPublicationTests
{
    private static Resource Person(string name, params string[] capabilities)
        => Resource.Create("therapist", name, capabilities: capabilities).Value;

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

    [Fact]
    public void The_resource_read_model_publishes_capabilities()
    {
        var model = DeliveryModelMapper.ToReadModel(Person("Mary", "cert-x", "massage"), "Europe/London");

        Assert.Equal(["cert-x", "massage"], model.Capabilities);
    }

    [Fact]
    public void A_resource_with_no_capabilities_publishes_an_empty_collection()
    {
        // Empty, never null or absent: a consumer must be able to treat the
        // member as a list without a null check, and "absent" would be
        // indistinguishable from "not published".
        var model = DeliveryModelMapper.ToReadModel(Person("Joan"), "Europe/London");

        Assert.NotNull(model.Capabilities);
        Assert.Empty(model.Capabilities);
    }

    [Fact]
    public void Capabilities_are_published_in_a_deterministic_order()
    {
        var one = DeliveryModelMapper.ToReadModel(Person("Mary", "massage", "cert-x"), "Europe/London");
        var other = DeliveryModelMapper.ToReadModel(Person("Mary", "cert-x", "massage"), "Europe/London");

        Assert.Equal(one.Capabilities, other.Capabilities);
    }

    [Fact]
    public void The_service_read_model_publishes_required_capabilities()
    {
        var model = DeliveryModelMapper.ToReadModel(Svc("cert-x"));

        Assert.Equal(["cert-x"], Assert.Single(model.Roles).RequiredCapabilities);
    }

    [Fact]
    public void A_role_requiring_nothing_publishes_an_empty_collection()
    {
        var model = DeliveryModelMapper.ToReadModel(Svc());

        Assert.NotNull(Assert.Single(model.Roles).RequiredCapabilities);
        Assert.Empty(Assert.Single(model.Roles).RequiredCapabilities);
    }

    [Fact]
    public void A_consumer_can_compute_the_candidate_pool_from_the_published_reads_alone()
    {
        // The derivability property D9 depends on. Everything this computation
        // uses comes off the wire; nothing is read from the domain.
        var resources = new[]
        {
            Person("Mary", "cert-x", "massage"),
            Person("Frank", "massage"),
            Person("Joan"),
        };

        var published = resources.Select(r => DeliveryModelMapper.ToReadModel(r, "Europe/London")).ToList();
        var service = DeliveryModelMapper.ToReadModel(Svc("cert-x"));

        var derived = published
            .Where(r => r.Type == Assert.Single(service.Roles).ResourceType
                && Assert.Single(service.Roles).RequiredCapabilities.All(required => r.Capabilities.Contains(required)))
            .Select(r => r.DisplayName)
            .ToList();

        Assert.Equal(["Mary"], derived);
    }

    [Fact]
    public void The_derived_pool_matches_what_the_domain_resolves()
    {
        // Same question asked of the published contract and of the domain rule.
        // If these ever diverge, the oracle argument fails and D9 must be
        // reopened — which is precisely the drift this test exists to catch.
        var resources = new[]
        {
            Person("Mary", "cert-x", "massage"),
            Person("Frank", "massage"),
            Person("Joan"),
        };

        var service = Svc("massage");
        var publishedService = DeliveryModelMapper.ToReadModel(service);

        var derived = resources
            .Select(r => DeliveryModelMapper.ToReadModel(r, "Europe/London"))
            .Where(r => r.Type == Assert.Single(publishedService.Roles).ResourceType
                && Assert.Single(publishedService.Roles).RequiredCapabilities
                    .All(required => r.Capabilities.Contains(required)))
            .Select(r => r.DisplayName)
            .OrderBy(name => name)
            .ToList();

        var domainResolved = resources
            .Where(r => r.Type == service.Roles[0].ResourceType
                && service.Roles[0].RequiredCapabilities.IsSatisfiedBy(r.Capabilities))
            .Select(r => r.DisplayName)
            .OrderBy(name => name)
            .ToList();

        Assert.Equal(domainResolved, derived);
        Assert.Equal(["Frank", "Mary"], derived);
    }
}
