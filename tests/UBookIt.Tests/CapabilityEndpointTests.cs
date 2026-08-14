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
/// The capability usage endpoint (resource-management spec). The projection
/// itself is EF-translated and proven against a real database in the integration
/// suite; what is checked here is the DTO mapping and the authorization
/// guarantee.
/// </summary>
public class CapabilityEndpointTests
{
    /// <summary>A management-store double supporting only the usage projection.</summary>
    private sealed class ProjectionOnlyManagementStore(
        CapabilityUsage[]? capabilities = null) : IResourceManagementStore
    {
        public Task<IReadOnlyList<CapabilityUsage>> ListCapabilitiesAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CapabilityUsage>>(capabilities ?? []);

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

    private static ResourcesController Wire(CapabilityUsage[]? capabilities = null)
        => new(new InMemoryResourceStore(), new ProjectionOnlyManagementStore(capabilities));

    private static List<CapabilityUsageModel> OkUsage(IActionResult result)
        => Assert.IsType<List<CapabilityUsageModel>>(Assert.IsType<OkObjectResult>(result).Value);

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

    // ------------------------------------------------------------ contract shape

    [Fact]
    public void The_endpoint_inherits_the_authorized_backoffice_base()
    {
        Assert.True(typeof(UBookItBackofficeApiControllerBase).IsAssignableFrom(typeof(ResourcesController)));
        Assert.NotNull(
            typeof(UBookItBackofficeApiControllerBase)
                .GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>(inherit: true));
    }

    [Fact]
    public void The_literal_route_cannot_collide_with_the_guid_route()
    {
        var route = typeof(ResourcesController).GetMethod(nameof(ResourcesController.ListCapabilities))!
            .GetCustomAttribute<HttpGetAttribute>()!;

        Assert.Equal("resources/capabilities", route.Template);

        var byId = typeof(ResourcesController)
            .GetMethod(nameof(ResourcesController.GetResource))!
            .GetCustomAttribute<HttpGetAttribute>()!;

        // The guid constraint is what keeps this literal from binding as an id.
        Assert.Contains(":guid", byId.Template);
    }

    /// <summary>
    /// The superseded role-match preview is gone from the contract, not merely
    /// unused. A second endpoint applying the capability rule itself is the
    /// duplication the service configuration preview exists to remove, so its
    /// absence is asserted rather than assumed (design D4).
    /// </summary>
    [Fact]
    public void No_endpoint_answers_the_role_match_question_any_more()
    {
        var routes = typeof(ResourcesController)
            .GetMethods()
            .SelectMany(m => m.GetCustomAttributes<HttpGetAttribute>())
            .Select(a => a.Template);

        Assert.DoesNotContain("resources/matching", routes);
    }

    /// <summary>
    /// Persistence spec, "No capability matching in storage": the management
    /// store SHALL NOT provide a projection that selects resources by a
    /// required-capability set.
    /// <para>
    /// Asserted over the port rather than over one implementation, because the
    /// rule is about the contract: eligibility has exactly one implementation,
    /// in Core, over resources the read port hydrates. A storage-layer
    /// projection applying the same rule would be a second implementation free
    /// to diverge in its predicate, its ordering, or its test doubles (design
    /// D4). Deleting the old method is not the same as preventing the next one,
    /// which is why this is a test and not a comment.
    /// </para>
    /// </summary>
    [Fact]
    public void The_management_store_exposes_no_capability_matching_projection()
    {
        // Any collection of strings, not an enumerated list of the shapes a
        // matching projection has happened to use. Naming shapes explicitly made
        // this guard catch yesterday's signature and miss `IReadOnlyList<string>`
        // — which is this file's own house style — so the predicate asks the
        // structural question instead: does a parameter carry a set of keys?
        //
        // It is deliberately over-broad. A legitimate future member taking a
        // string collection for some unrelated purpose will fail this test; the
        // fix is to exclude that member by name, never to delete the test.
        //
        // It is also knowingly partial, and the gaps are semantic rather than
        // structural: a bespoke key-set type, or a member taking a service id and
        // reading the role's capabilities itself, would both slip past, because
        // no predicate over parameter types can tell "takes a set of keys" from
        // "fetches them". The persistence spec's source-level rule is what covers
        // those; see the deferred obligation.
        static bool CarriesCapabilityKeys(Type parameter)
            => parameter == typeof(CapabilitySet)
                || (parameter != typeof(string)
                    && typeof(IEnumerable<string>).IsAssignableFrom(parameter));

        var offenders = typeof(IResourceManagementStore)
            .GetMethods()
            .Where(m => m.GetParameters().Any(p => CarriesCapabilityKeys(p.ParameterType)))
            .Select(m => m.Name)
            .ToList();

        Assert.Empty(offenders);
    }

    /// <summary>
    /// The same rule for the read port, which candidate resolution depends on:
    /// the backoffice's needs are not the booking path's needs, and keeping the
    /// ports separate is what stopped one reaching across the other previously
    /// (persistence spec, "Projections stay off the read port").
    /// </summary>
    [Fact]
    public void The_read_port_exposes_neither_projection()
    {
        var names = typeof(IResourceStore).GetMethods().Select(m => m.Name).ToList();

        Assert.DoesNotContain(nameof(IResourceManagementStore.ListCapabilitiesAsync), names);
        Assert.DoesNotContain(nameof(IResourceManagementStore.ListTypesAsync), names);
    }
}
