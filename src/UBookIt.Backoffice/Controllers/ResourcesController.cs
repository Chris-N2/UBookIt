using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UBookIt.Backoffice.Mapping;
using UBookIt.Backoffice.Models;
using UBookIt.Core.Stores;

namespace UBookIt.Backoffice.Controllers;

/// <summary>
/// Resource management endpoints. Depends only on the resource stores —
/// never on booking storage (resource-management spec, HTTP-caller
/// containment). Authorization comes from the shared base controller.
/// </summary>
[ApiVersion("1.0")]
[ApiExplorerSettings(GroupName = "UBookIt.Backoffice")]
public class ResourcesController(
    IResourceStore resourceStore,
    IResourceManagementStore managementStore) : UBookItBackofficeApiControllerBase
{
    [HttpGet("resources")]
    [ProducesResponseType<PagedResourcesModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListResources(
        int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var page = await managementStore.ListAsync(skip, take, cancellationToken);

        return Ok(new PagedResourcesModel
        {
            Total = page.Total,
            Items = page.Items.Select(ResourceModelMapper.ToModel).ToList(),
        });
    }

    /// <summary>
    /// The distinct resource type keys in use, with a count per type. Read-only
    /// projection over existing storage — no schema dependency of its own.
    /// The literal segment cannot collide with the id route below, which is
    /// constrained to a guid.
    /// </summary>
    [HttpGet("resources/types")]
    [ProducesResponseType<IEnumerable<ResourceTypeUsageModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListResourceTypes(CancellationToken cancellationToken = default)
    {
        var types = await managementStore.ListTypesAsync(cancellationToken);

        return Ok(types.Select(ResourceModelMapper.ToModel).ToList());
    }

    [HttpGet("resources/{id:guid}")]
    [ProducesResponseType<ResourceResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResource(Guid id, CancellationToken cancellationToken = default)
    {
        var resource = await resourceStore.GetAsync(id, cancellationToken);

        return resource is null
            ? NotFoundProblem(id)
            : Ok(ResourceModelMapper.ToModel(resource));
    }

    [HttpPost("resources")]
    [ProducesResponseType<ResourceResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateResource(
        ResourceRequestModel model, CancellationToken cancellationToken = default)
    {
        var resource = ResourceModelMapper.ToDomain(model);
        if (!resource.Succeeded)
        {
            return resource.Failures.ToProblemResult();
        }

        var created = await managementStore.CreateAsync(resource.Value, cancellationToken);

        return created.Succeeded
            ? Ok(ResourceModelMapper.ToModel(created.Value))
            : created.Failures.ToProblemResult();
    }

    [HttpPut("resources/{id:guid}")]
    [ProducesResponseType<ResourceResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateResource(
        Guid id, ResourceRequestModel model, CancellationToken cancellationToken = default)
    {
        var resource = ResourceModelMapper.ToDomain(model, id);
        if (!resource.Succeeded)
        {
            return resource.Failures.ToProblemResult();
        }

        var updated = await managementStore.UpdateAsync(resource.Value, cancellationToken);

        return updated.Succeeded
            ? Ok(ResourceModelMapper.ToModel(updated.Value))
            : updated.Failures.ToProblemResult();
    }

    [HttpDelete("resources/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteResource(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await managementStore.DeleteAsync(id, cancellationToken);

        return result.Succeeded ? Ok() : result.Failures.ToProblemResult();
    }

    private IActionResult NotFoundProblem(Guid id)
        => new List<Core.Common.DomainFailure>
        {
            new(Core.Common.FailureCodes.ResourceNotFound, $"No resource exists with id {id}."),
        }.ToProblemResult();
}
