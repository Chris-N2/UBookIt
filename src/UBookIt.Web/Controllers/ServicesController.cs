using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UBookIt.Core;
using UBookIt.Core.Common;
using UBookIt.Core.Services;
using UBookIt.Core.Stores;
using UBookIt.Web.Mapping;
using UBookIt.Web.Models;

namespace UBookIt.Web.Controllers;

/// <summary>
/// Public service read/discovery, availability, and placement. Reads depend only
/// on the read port <see cref="IServiceStore"/> — never the management store
/// (HTTP-caller containment, as for resources). Anonymous and body-only on the
/// same terms as the rest of the delivery API.
/// </summary>
[ApiVersion("1.0")]
[ApiExplorerSettings(GroupName = "UBookIt.Delivery")]
public sealed class ServicesController(
    IServiceStore serviceStore,
    IServiceBookingService serviceBooking,
    SiteBookingSettings settings) : UBookItDeliveryApiControllerBase
{
    [HttpGet("services")]
    [ProducesResponseType<PagedServicesModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListServices(
        int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var page = await serviceStore.ListAsync(skip, take, cancellationToken);

        return Ok(new PagedServicesModel
        {
            Total = page.Total,
            Items = page.Items.Select(DeliveryModelMapper.ToReadModel).ToList(),
        });
    }

    [HttpGet("services/{id:guid}")]
    [ProducesResponseType<ServiceReadModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetService(Guid id, CancellationToken cancellationToken = default)
    {
        var service = await serviceStore.GetAsync(id, cancellationToken);

        return service is null
            ? NotFound(id)
            : Ok(DeliveryModelMapper.ToReadModel(service));
    }

    /// <summary>
    /// Every start at which this service can be booked over the range, with the
    /// lengths available at each. Takes no duration — one response answers every
    /// length — and names no resource, because the resource is resolved when the
    /// booking is placed.
    /// </summary>
    [HttpGet("services/{id:guid}/bookable-starts")]
    [ProducesResponseType<ServiceBookableStartsResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetServiceBookableStarts(
        Guid id, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var result = await serviceBooking.GetBookableStartsAsync(id, from, to, cancellationToken);

        return result.Succeeded
            ? Ok(new ServiceBookableStartsResponseModel
            {
                ServiceId = id,
                ZoneId = settings.TimeZoneId,
                Starts = result.Value.Select(DeliveryModelMapper.ToServiceBookableStartModel).ToList(),
            })
            : result.Failures.ToProblemResult();
    }

    /// <summary>
    /// Books the service, resolving one of its eligible resources. The submitted
    /// length is required and is never substituted; the response reports which
    /// resource the booking landed on.
    /// </summary>
    [HttpPost("services/{id:guid}/bookings")]
    [ProducesResponseType<PlacementResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PlaceServiceBooking(
        Guid id, ServicePlacementRequestModel model, CancellationToken cancellationToken = default)
    {
        var request = DeliveryModelMapper.ToServiceBookingRequest(id, model);
        if (!request.Succeeded)
        {
            return request.Failures.ToProblemResult();
        }

        var placed = await serviceBooking.PlaceAsync(request.Value, cancellationToken);

        return placed.Succeeded
            ? Ok(DeliveryModelMapper.ToPlacementResponse(placed.Value))
            : placed.Failures.ToProblemResult();
    }

    private static IActionResult NotFound(Guid id)
        => new DomainFailure(FailureCodes.ServiceNotFound, $"No service exists with id {id}.").ToProblemResult();
}
