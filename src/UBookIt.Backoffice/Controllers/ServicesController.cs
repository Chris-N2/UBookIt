using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UBookIt.Backoffice.Mapping;
using UBookIt.Backoffice.Models;
using UBookIt.Core.Services;
using UBookIt.Core.Stores;

namespace UBookIt.Backoffice.Controllers;

/// <summary>
/// Service management endpoints. Depends only on the service read/management
/// ports and on Core resolution — never on booking storage (HTTP-caller
/// containment). Authorization comes from the shared base controller.
/// </summary>
[ApiVersion("1.0")]
[ApiExplorerSettings(GroupName = "UBookIt.Backoffice")]
public class ServicesController(
    IServiceStore serviceStore,
    IServiceManagementStore managementStore,
    IServiceBookingService resolution) : UBookItBackofficeApiControllerBase
{
    /// <summary>
    /// The resolution chain for each role of a service configuration: the
    /// resources of that role's type, those of them carrying its required
    /// capabilities, those of them whose own constraints admit a length the
    /// duration permits, and what the last filter excluded with the bound that
    /// excluded it. Chains come back in the order the roles were supplied.
    /// <para>
    /// Accepts a configuration no saved service holds and does not require it to
    /// be a valid service — in particular, no name. Its whole purpose is to
    /// report on one being edited (design D2).
    /// </para>
    /// <para>
    /// The answer comes from Core resolution, not from a filter of its own, so
    /// the backoffice cannot report a different pool from the one the booking
    /// path will act on (design D1). It is a POST because a duration
    /// specification is a structured value — a kind plus whichever bounds apply
    /// — and flattening it into query parameters would reproduce the ambiguity
    /// the duration value object exists to prevent (design D3).
    /// </para>
    /// <para>
    /// The literal segment cannot collide with the guid-constrained id route
    /// below, and there is no POST on <c>services/{id}</c> in any case.
    /// </para>
    /// </summary>
    [HttpPost("services/preview")]
    [ProducesResponseType<ServicePreviewResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PreviewServiceConfiguration(
        ServicePreviewRequestModel model, CancellationToken cancellationToken = default)
    {
        var configuration = ServiceModelMapper.ToDomain(model);
        if (!configuration.Succeeded)
        {
            return configuration.Failures.ToProblemResult();
        }

        var (roles, duration) = configuration.Value;

        var chains = new List<(ServiceRole Role, ServiceResolution Chain)>(roles.Count);

        foreach (var role in roles)
        {
            // A well-formed type key no resource uses is a chain whose first stage
            // is empty, not an error: naming a type before creating resources of it
            // is a legitimate setup order, and the empty first stage is precisely
            // the signal that distinguishes it from a capability or duration
            // exclusion.
            //
            // Each role is reported independently and exactly as supplied,
            // including when two of them name the same type — a configuration that
            // cannot be saved but is one an editor is in the middle of correcting.
            chains.Add((role, await resolution.ResolveAsync(role, duration, cancellationToken)));
        }

        return Ok(ServiceModelMapper.ToModel(chains));
    }

    [HttpGet("services")]
    [ProducesResponseType<PagedServicesModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListServices(
        int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var page = await managementStore.ListAsync(skip, take, cancellationToken);

        return Ok(new PagedServicesModel
        {
            Total = page.Total,
            Items = page.Items.Select(ServiceModelMapper.ToModel).ToList(),
        });
    }

    [HttpGet("services/{id:guid}")]
    [ProducesResponseType<ServiceResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetService(Guid id, CancellationToken cancellationToken = default)
    {
        var service = await serviceStore.GetAsync(id, cancellationToken);

        return service is null
            ? NotFoundProblem(id)
            : Ok(ServiceModelMapper.ToModel(service));
    }

    [HttpPost("services")]
    [ProducesResponseType<ServiceResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateService(
        ServiceRequestModel model, CancellationToken cancellationToken = default)
    {
        var service = ServiceModelMapper.ToDomain(model);
        if (!service.Succeeded)
        {
            return service.Failures.ToProblemResult();
        }

        var created = await managementStore.CreateAsync(service.Value, cancellationToken);

        return created.Succeeded
            ? Ok(ServiceModelMapper.ToModel(created.Value))
            : created.Failures.ToProblemResult();
    }

    [HttpPut("services/{id:guid}")]
    [ProducesResponseType<ServiceResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateService(
        Guid id, ServiceRequestModel model, CancellationToken cancellationToken = default)
    {
        var service = ServiceModelMapper.ToDomain(model, id);
        if (!service.Succeeded)
        {
            return service.Failures.ToProblemResult();
        }

        var updated = await managementStore.UpdateAsync(service.Value, cancellationToken);

        return updated.Succeeded
            ? Ok(ServiceModelMapper.ToModel(updated.Value))
            : updated.Failures.ToProblemResult();
    }

    [HttpDelete("services/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteService(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await managementStore.DeleteAsync(id, cancellationToken);

        return result.Succeeded ? Ok() : result.Failures.ToProblemResult();
    }

    private IActionResult NotFoundProblem(Guid id)
        => new List<Core.Common.DomainFailure>
        {
            new(Core.Common.FailureCodes.ServiceNotFound, $"No service exists with id {id}."),
        }.ToProblemResult();
}
