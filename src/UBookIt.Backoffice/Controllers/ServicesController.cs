using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UBookIt.Backoffice.Mapping;
using UBookIt.Backoffice.Models;
using UBookIt.Core.Stores;

namespace UBookIt.Backoffice.Controllers;

/// <summary>
/// Service management endpoints. Depends only on the service read/management
/// ports — never on booking storage (HTTP-caller containment). Authorization
/// comes from the shared base controller.
/// </summary>
[ApiVersion("1.0")]
[ApiExplorerSettings(GroupName = "UBookIt.Backoffice")]
public class ServicesController(
    IServiceStore serviceStore,
    IServiceManagementStore managementStore) : UBookItBackofficeApiControllerBase
{
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
