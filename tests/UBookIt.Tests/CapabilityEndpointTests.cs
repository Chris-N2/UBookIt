using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using UBookIt.Backoffice.Controllers;
using UBookIt.Backoffice.Models;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Core.Stores;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The capability usage and role-match endpoints (resource-management spec).
/// The projections themselves are EF-translated and proven against a real
/// database in the integration suite; what is checked here is the DTO mapping,
/// the validation, and the authorization guarantee.
/// </summary>
public class CapabilityEndpointTests
{
    /// <summary>
    /// A management-store double supporting only the two projection reads. The
    /// match projection applies the real <see cref="CapabilitySet"/> test, so
    /// this double cannot accidentally answer a different question from the one
    /// the store answers.
    /// </summary>
    private sealed class ProjectionOnlyManagementStore(
        CapabilityUsage[]? capabilities = null,
        Resource[]? resources = null) : IResourceManagementStore
    {
        public Task<IReadOnlyList<CapabilityUsage>> ListCapabilitiesAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CapabilityUsage>>(capabilities ?? []);

        public Task<IReadOnlyList<ResourceMatch>> ListMatchingAsync(
            string type, CapabilitySet requiredCapabilities, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ResourceMatch> matches = (resources ?? [])
                .Where(r => r.Type == type && requiredCapabilities.IsSatisfiedBy(r.Capabilities))
                .Select(r => new ResourceMatch(r.Id, r.DisplayName))
                .ToList();

            return Task.FromResult(matches);
        }

        public Task<IReadOnlyList<ResourceTypeUsage>> ListTypesAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DomainResult<Resource>> CreateAsync(Resource resource, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DomainResult<Resource>> UpdateAsync(Resource resource, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DomainResult> DeleteAsync(Guid resourceId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ResourcePage> ListAsync(int skip, int take, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private static Resource Person(string name, params string[] capabilities)
        => Resource.Create("therapist", name, capabilities: capabilities).Value;

    private static ResourcesController Wire(
        CapabilityUsage[]? capabilities = null, Resource[]? resources = null)
        => new(new InMemoryResourceStore(), new ProjectionOnlyManagementStore(capabilities, resources));

    private static List<CapabilityUsageModel> OkUsage(IActionResult result)
        => Assert.IsType<List<CapabilityUsageModel>>(Assert.IsType<OkObjectResult>(result).Value);

    private static RoleMatchesModel OkMatches(IActionResult result)
        => Assert.IsType<RoleMatchesModel>(Assert.IsType<OkObjectResult>(result).Value);

    // ------------------------------------------------------------ usage endpoint

    [Fact]
    public async Task Capabilities_in_use_are_reported_with_counts()
    {
        var controller = Wire([new CapabilityUsage("cert-x", 3), new CapabilityUsage("massage", 1)]);

        var usage = OkUsage(await controller.ListCapabilities());

        Assert.Collection(
            usage,
            first => { Assert.Equal("cert-x", first.Key); Assert.Equal(3, first.Count); },
            second => { Assert.Equal("massage", second.Key); Assert.Equal(1, second.Count); });
    }

    [Fact]
    public async Task No_capabilities_yields_an_empty_success_not_a_failure()
    {
        var result = await Wire().ListCapabilities();

        Assert.IsType<OkObjectResult>(result);
        Assert.Empty(OkUsage(result));
    }

    [Fact]
    public async Task The_usage_endpoint_preserves_the_order_the_store_returns()
    {
        var controller = Wire(
            [new CapabilityUsage("a-key", 1), new CapabilityUsage("b-key", 1), new CapabilityUsage("c-key", 1)]);

        var usage = OkUsage(await controller.ListCapabilities());

        Assert.Equal(["a-key", "b-key", "c-key"], usage.Select(u => u.Key));
    }

    // ------------------------------------------------------------ match endpoint

    [Fact]
    public async Task Matching_resources_are_returned_with_a_total()
    {
        var controller = Wire(resources: [
            Person("Mary", "cert-x", "massage"),
            Person("Frank", "massage"),
            Person("Joan"),
        ]);

        var matches = OkMatches(await controller.ListMatchingResources("therapist", ["massage"]));

        Assert.Equal(2, matches.Total);
        Assert.Equal(["Mary", "Frank"], matches.Items.Select(m => m.DisplayName));
    }

    [Fact]
    public async Task An_unsaved_requirement_can_be_previewed()
    {
        // The whole point of a separate endpoint: this combination belongs to no
        // saved service, so nothing can be resolved by service id.
        var controller = Wire(resources: [Person("Mary", "cert-x")]);

        var matches = OkMatches(await controller.ListMatchingResources("therapist", ["cert-x"]));

        Assert.Equal(1, matches.Total);
    }

    [Fact]
    public async Task No_required_capabilities_matches_the_whole_type()
    {
        var controller = Wire(resources: [Person("Mary", "cert-x"), Person("Joan")]);

        var matches = OkMatches(await controller.ListMatchingResources("therapist", null));

        Assert.Equal(2, matches.Total);
    }

    [Fact]
    public async Task An_unused_type_key_yields_an_empty_success()
    {
        var controller = Wire(resources: [Person("Mary", "cert-x")]);

        var result = await controller.ListMatchingResources("physiotherapist", null);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(0, OkMatches(result).Total);
    }

    [Fact]
    public async Task A_capability_nothing_carries_yields_an_empty_success()
    {
        var controller = Wire(resources: [Person("Mary", "cert-x")]);

        var result = await controller.ListMatchingResources("therapist", ["never-tagged"]);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(0, OkMatches(result).Total);
    }

    [Fact]
    public async Task A_malformed_capability_key_is_rejected_rather_than_dropped()
    {
        // Silently discarding it would report a count for a requirement other
        // than the one the editor typed — a readout that is confidently wrong.
        var controller = Wire(resources: [Person("Mary", "cert-x")]);

        var result = await controller.ListMatchingResources("therapist", ["Cert X"]);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, problem.StatusCode);
    }

    [Fact]
    public async Task A_malformed_type_key_is_rejected()
    {
        var controller = Wire(resources: [Person("Mary", "cert-x")]);

        var result = await controller.ListMatchingResources("Meeting Room", null);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, problem.StatusCode);
    }

    // ------------------------------------------------------------ contract shape

    [Fact]
    public void Both_endpoints_inherit_the_authorized_backoffice_base()
    {
        Assert.True(typeof(UBookItBackofficeApiControllerBase).IsAssignableFrom(typeof(ResourcesController)));
        Assert.NotNull(
            typeof(UBookItBackofficeApiControllerBase)
                .GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>(inherit: true));
    }

    [Theory]
    [InlineData(nameof(ResourcesController.ListCapabilities), "resources/capabilities")]
    [InlineData(nameof(ResourcesController.ListMatchingResources), "resources/matching")]
    public void Literal_routes_cannot_collide_with_the_guid_route(string method, string expected)
    {
        var route = typeof(ResourcesController).GetMethod(method)!
            .GetCustomAttribute<HttpGetAttribute>()!;

        Assert.Equal(expected, route.Template);

        var byId = typeof(ResourcesController)
            .GetMethod(nameof(ResourcesController.GetResource))!
            .GetCustomAttribute<HttpGetAttribute>()!;

        // The guid constraint is what keeps these literals from binding as ids.
        Assert.Contains(":guid", byId.Template);
    }
}
