using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UBookIt.Core;
using UBookIt.Core.Common;
using UBookIt.Core.Stores;
using UBookIt.Web.Mapping;
using UBookIt.Web.Models;

namespace UBookIt.Web.Controllers;

/// <summary>
/// Public resource read/discovery. Depends only on the read port
/// <see cref="IResourceStore"/> — never the management store (design D2,
/// HTTP-caller containment).
/// </summary>
[ApiVersion("1.0")]
[ApiExplorerSettings(GroupName = "UBookIt.Delivery")]
public sealed class ResourcesController(
    IResourceStore resourceStore,
    SiteBookingSettings settings) : UBookItDeliveryApiControllerBase
{
    [DeliveryRead]
    [HttpGet("resources")]
    [ProducesResponseType<PagedResourcesModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListResources(
        int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var page = await resourceStore.ListAsync(skip, take, cancellationToken);

        return Ok(new PagedResourcesModel
        {
            Total = page.Total,
            Items = page.Items.Select(r => DeliveryModelMapper.ToReadModel(r, settings.TimeZoneId)).ToList(),
        });
    }

    [DeliveryRead]
    [HttpGet("resources/{id:guid}")]
    [ProducesResponseType<ResourceReadModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResource(Guid id, CancellationToken cancellationToken = default)
    {
        var resource = await resourceStore.GetAsync(id, cancellationToken);

        return resource is null
            ? new DomainFailure(FailureCodes.ResourceNotFound, $"No resource exists with id {id}.").ToProblemResult()
            : Ok(DeliveryModelMapper.ToReadModel(resource, settings.TimeZoneId));
    }
}
