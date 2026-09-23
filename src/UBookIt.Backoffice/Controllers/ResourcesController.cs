using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UBookIt.Backoffice.Mapping;
using UBookIt.Backoffice.Models;
using UBookIt.Core.Availability;
using UBookIt.Core.Resources;
using UBookIt.Core.Common;
using UBookIt.Core.Stores;

namespace UBookIt.Backoffice.Controllers;

/// <summary>
/// Resource management endpoints. Depends only on the resource stores —
/// never on booking storage (resource-management spec, HTTP-caller
/// containment). The shared base controller supplies the section gate; each action names
/// the configuration verb policy on top (see <see cref="Constants.VerbPolicies"/>).
/// </summary>
[ApiVersion("1.0")]
[ApiExplorerSettings(GroupName = "UBookIt.Backoffice")]
public class ResourcesController(
    IResourceStore resourceStore,
    IResourceManagementStore managementStore,
    ISiteClosureStore closureStore) : UBookItBackofficeApiControllerBase
{
    /// <summary>
    /// Every site closure, read once per request. The response projects the whole list —
    /// including closures this resource is exempt from, which its availability does not
    /// carry — so an editor can un-exempt as well as exempt.
    /// </summary>
    private Task<IReadOnlyList<SiteClosure>> ClosuresAsync(CancellationToken cancellationToken)
        => closureStore.ListAsync(cancellationToken);

    /// <summary>
    /// Refuses an opt-out naming a closure that does not exist. Checked here because this is
    /// where the closures are readable: the domain never loads them, so a rule stated there
    /// would only appear to be enforced.
    /// </summary>
    private static DomainFailure[] UnknownClosures(
        ResourceRequestModel model, IReadOnlyList<SiteClosure> closures)
        => model.ClosureOptOuts
            .Where(id => closures.All(closure => closure.Id != id))
            .Distinct()
            .Select(id => new DomainFailure(
                FailureCodes.ClosureNotFound,
                $"No closure exists with id {id}.",
                nameof(ResourceRequestModel.ClosureOptOuts)))
            .ToArray();

    /// <summary>
    /// The saved resource as a subsequent read would report it.
    /// </summary>
    /// <remarks>
    /// <b>Re-read rather than mapped from what was written.</b> The aggregate a write produces
    /// was built from the REQUEST, and a request carries opt-outs but never closures — so the
    /// closure layer that decides whether an exception is superseded is absent from it, and a
    /// response mapped from it reported `superseded: false` on every save whatever the site had
    /// configured. The editor lost the statement the moment somebody saved and got it back on
    /// reload. Found in the running backoffice; no test here could see it, because each one
    /// handed the mapper a resource it had built WITH its closures.
    /// <para>
    /// Composing the closure layer here instead would mean a second implementation of "which
    /// closures apply to this resource", which is the thing the hydration seam exists to keep
    /// singular. One extra read is the cheaper mistake.
    /// </para>
    /// </remarks>
    private async Task<IActionResult> SavedResourceAsync(
        Guid id, IReadOnlyList<SiteClosure> closures, CancellationToken cancellationToken)
    {
        var stored = await resourceStore.GetAsync(id, cancellationToken);

        // NO FALLBACK TO THE WRITE AGGREGATE. Mapping that one is exactly the defect this
        // method exists to close — it carries no closures, so `superseded` would report false
        // whatever the site had configured, and the one path that did so would be the one
        // nobody could reproduce. A resource that has vanished between a committed write and
        // the read after it is reported as missing, which is what has happened.
        // The caller has already read the closures to validate the opt-outs; re-reading them
        // here would be a second query per write for the same answer.
        return stored is null
            ? NotFoundProblem(id)
            : Ok(ResourceModelMapper.ToModel(stored, closures));
    }

    [Authorize(Policy = Constants.VerbPolicies.Configure)]
    [HttpGet("resources")]
    [ProducesResponseType<PagedResourcesModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListResources(
        int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var page = await managementStore.ListAsync(skip, take, cancellationToken);
        var closures = await ClosuresAsync(cancellationToken);

        return Ok(new PagedResourcesModel
        {
            Total = page.Total,
            Items = page.Items.Select(resource => ResourceModelMapper.ToModel(resource, closures)).ToList(),
        });
    }

    /// <summary>
    /// The distinct resource type keys in use, with a count per type. Read-only
    /// projection over existing storage — no schema dependency of its own.
    /// The literal segment cannot collide with the id route below, which is
    /// constrained to a guid.
    /// </summary>
    [Authorize(Policy = Constants.VerbPolicies.Configure)]
    [HttpGet("resources/types")]
    [ProducesResponseType<IEnumerable<ResourceTypeUsageModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListResourceTypes(CancellationToken cancellationToken = default)
    {
        var types = await managementStore.ListTypesAsync(cancellationToken);

        return Ok(types.Select(ResourceModelMapper.ToModel).ToList());
    }

    /// <summary>
    /// The distinct capability keys resources currently carry, with a count per
    /// key. Mirrors the type usage endpoint, including the literal-segment
    /// routing that cannot collide with the guid-constrained id route.
    /// </summary>
    [Authorize(Policy = Constants.VerbPolicies.Configure)]
    [HttpGet("resources/capabilities")]
    [ProducesResponseType<IEnumerable<CapabilityUsageModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListCapabilities(CancellationToken cancellationToken = default)
    {
        var capabilities = await managementStore.ListCapabilitiesAsync(cancellationToken);

        return Ok(capabilities.Select(ResourceModelMapper.ToModel).ToList());
    }

    [Authorize(Policy = Constants.VerbPolicies.Configure)]
    [HttpGet("resources/{id:guid}")]
    [ProducesResponseType<ResourceResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResource(Guid id, CancellationToken cancellationToken = default)
    {
        var resource = await resourceStore.GetAsync(id, cancellationToken);

        return resource is null
            ? NotFoundProblem(id)
            : Ok(ResourceModelMapper.ToModel(resource, await ClosuresAsync(cancellationToken)));
    }

    [Authorize(Policy = Constants.VerbPolicies.Configure)]
    [HttpPost("resources")]
    [ProducesResponseType<ResourceResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    // A create can answer 404: the response is re-read after the write, and a resource that has
    // vanished in between is reported as missing rather than described from the write aggregate.
    // Unreachable in practice, declared because an undeclared status is absent from the generated
    // client and therefore unhandleable by any consumer that meets it.
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateResource(
        ResourceRequestModel model, CancellationToken cancellationToken = default)
    {
        var closures = await ClosuresAsync(cancellationToken);

        var resource = ResourceModelMapper.ToDomain(model);
        var unknown = UnknownClosures(model, closures);

        if (!resource.Succeeded || unknown.Length > 0)
        {
            // Both sets in one response, so a save reports every failed rule rather than
            // whichever check happened to run first.
            return resource.Failures.Concat(unknown).ToList().ToProblemResult();
        }

        var created = await managementStore.CreateAsync(resource.Value, cancellationToken);

        return created.Succeeded
            ? await SavedResourceAsync(created.Value.Id, closures, cancellationToken)
            : created.Failures.ToProblemResult();
    }

    [Authorize(Policy = Constants.VerbPolicies.Configure)]
    [HttpPut("resources/{id:guid}")]
    [ProducesResponseType<ResourceResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateResource(
        Guid id, ResourceRequestModel model, CancellationToken cancellationToken = default)
    {
        var closures = await ClosuresAsync(cancellationToken);

        var resource = ResourceModelMapper.ToDomain(model, id);
        var unknown = UnknownClosures(model, closures);

        if (!resource.Succeeded || unknown.Length > 0)
        {
            return resource.Failures.Concat(unknown).ToList().ToProblemResult();
        }

        var updated = await managementStore.UpdateAsync(resource.Value, cancellationToken);

        return updated.Succeeded
            ? await SavedResourceAsync(updated.Value.Id, closures, cancellationToken)
            : updated.Failures.ToProblemResult();
    }

    [Authorize(Policy = Constants.VerbPolicies.Configure)]
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
