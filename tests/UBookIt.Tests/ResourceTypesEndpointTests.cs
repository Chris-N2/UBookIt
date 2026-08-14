using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using UBookIt.Backoffice.Controllers;
using UBookIt.Backoffice.Models;
using UBookIt.Core.Stores;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The resource type usage endpoint: DTO mapping, the empty case, and the
/// authorization guarantee. The projection itself is EF-translated, so its
/// correctness is proven against a real database in the integration suite
/// (<c>ResourceManagementStoreTests</c>) rather than here.
/// </summary>
public class ResourceTypesEndpointTests
{
    /// <summary>
    /// A management-store double supporting only <see cref="IResourceManagementStore.ListTypesAsync"/>.
    /// The write members throw rather than pretending to implement semantics
    /// (notably delete's in-use rule) that this double cannot honour.
    /// </summary>
    private sealed class TypesOnlyManagementStore(params ResourceTypeUsage[] types) : IResourceManagementStore
    {
        public Task<IReadOnlyList<ResourceTypeUsage>> ListTypesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ResourceTypeUsage>>(types);

        public Task<IReadOnlyList<CapabilityUsage>> ListCapabilitiesAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<UBookIt.Core.Common.DomainResult<UBookIt.Core.Resources.Resource>> CreateAsync(
            UBookIt.Core.Resources.Resource resource, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<UBookIt.Core.Common.DomainResult<UBookIt.Core.Resources.Resource>> UpdateAsync(
            UBookIt.Core.Resources.Resource resource, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<UBookIt.Core.Common.DomainResult> DeleteAsync(
            Guid resourceId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ResourcePage> ListAsync(int skip, int take, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private static ResourcesController Wire(params ResourceTypeUsage[] types)
        => new(new InMemoryResourceStore(), new TypesOnlyManagementStore(types));

    private static List<ResourceTypeUsageModel> Ok(IActionResult result)
        => Assert.IsType<List<ResourceTypeUsageModel>>(Assert.IsType<OkObjectResult>(result).Value);

    [Fact]
    public async Task Reports_each_type_in_use_with_its_count()
    {
        var controller = Wire(new ResourceTypeUsage("masseur", 1), new ResourceTypeUsage("room", 3));

        var types = Ok(await controller.ListResourceTypes());

        Assert.Collection(
            types,
            first => { Assert.Equal("masseur", first.Type); Assert.Equal(1, first.Count); },
            second => { Assert.Equal("room", second.Type); Assert.Equal(3, second.Count); });
    }

    [Fact]
    public async Task No_resources_yields_an_empty_success_not_a_failure()
    {
        var controller = Wire();

        var result = await controller.ListResourceTypes();

        Assert.IsType<OkObjectResult>(result);
        Assert.Empty(Ok(result));
    }

    [Fact]
    public async Task Preserves_the_order_the_store_returns()
    {
        // The store orders by type key; the endpoint must not re-sort or
        // regroup, so the picker sees a stable order across calls.
        var controller = Wire(
            new ResourceTypeUsage("a-type", 1),
            new ResourceTypeUsage("b-type", 1),
            new ResourceTypeUsage("c-type", 1));

        var types = Ok(await controller.ListResourceTypes());

        Assert.Equal(["a-type", "b-type", "c-type"], types.Select(t => t.Type));
    }

    [Fact]
    public void Endpoint_inherits_the_authorized_backoffice_base()
    {
        Assert.True(typeof(UBookItBackofficeApiControllerBase).IsAssignableFrom(typeof(ResourcesController)));
        Assert.NotNull(
            typeof(UBookItBackofficeApiControllerBase)
                .GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>(inherit: true));
    }

    [Fact]
    public void Types_route_is_a_literal_segment_that_cannot_collide_with_the_guid_route()
    {
        var route = typeof(ResourcesController)
            .GetMethod(nameof(ResourcesController.ListResourceTypes))!
            .GetCustomAttribute<HttpGetAttribute>()!;

        Assert.Equal("resources/types", route.Template);

        var byId = typeof(ResourcesController)
            .GetMethod(nameof(ResourcesController.GetResource))!
            .GetCustomAttribute<HttpGetAttribute>()!;

        // The guid constraint is what keeps "types" from binding as an id.
        Assert.Contains(":guid", byId.Template);
    }
}
