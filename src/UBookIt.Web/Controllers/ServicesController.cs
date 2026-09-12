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
    [DeliveryRead]
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

    [DeliveryRead]
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
    /// <para>
    /// <paramref name="pinnedResourceId"/> narrows the answer to the starts and
    /// lengths at which an assignment <b>including</b> that resource exists: the
    /// question "when can I book this service with this person". Omitted, the
    /// query and its response are exactly as they were. Supplied, the response
    /// body still names no resource — the caller already named it — and the answer
    /// is honoured by placing with the same pin.
    /// </para>
    /// <para>
    /// A pin naming a resource in no role's candidate pool yields
    /// <c>resource-not-eligible</c> rather than being ignored, the rule the
    /// placement endpoint already applies.
    /// </para>
    /// </summary>
    [DeliveryRead]
    [HttpGet("services/{id:guid}/bookable-starts")]
    [ProducesResponseType<ServiceBookableStartsResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetServiceBookableStarts(
        Guid id,
        DateOnly from,
        DateOnly to,
        Guid? pinnedResourceId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await serviceBooking.GetBookableStartsAsync(id, from, to, pinnedResourceId, cancellationToken);

        if (!result.Succeeded)
        {
            return result.Failures.ToProblemResult();
        }

        return Ok(new ServiceBookableStartsResponseModel
        {
            ServiceId = id,
            ZoneId = settings.TimeZoneId,
            Starts = result.Value.Select(DeliveryModelMapper.ToServiceBookableStartModel).ToList(),

            // Asked only of an empty answer, which is the only answer it can
            // describe: the two are exclusive, and a response carrying starts
            // cannot be structurally impossible.
            Reason = result.Value.Count == 0
                ? await StructuralReasonAsync(id, cancellationToken)
                : null,
        });
    }

    /// <summary>
    /// Whether an empty answer is <b>permanent</b>, derived from Core's one
    /// structural-unfulfillability function — never from a second evaluation of
    /// the rules here, which would be free to disagree with the in-process flow in
    /// exactly the case it exists to detect (design D6).
    /// <para>
    /// Null rather than a claim when the pools could not be resolved: the code is
    /// one-directional, so silence means "not known to be structurally
    /// impossible" and never "available".
    /// </para>
    /// <para>
    /// Deliberately blind to the pin. The question is about the service's
    /// configuration, and a resource the caller chose being busy is not a property
    /// of the configuration — reporting it as permanent would tell a consumer to
    /// stop asking about a service that is bookable all week.
    /// </para>
    /// </summary>
    private async Task<string?> StructuralReasonAsync(Guid serviceId, CancellationToken cancellationToken)
    {
        var pools = await serviceBooking.ResolveCandidatesAsync(serviceId, cancellationToken);

        return pools.Succeeded && ServiceFulfillability.IsPermanentlyUnfulfillable(pools.Value)
            ? ServiceFulfillability.NotFulfillableCode
            : null;
    }

    /// <summary>
    /// Books the service, resolving one eligible resource per role. The submitted
    /// length is required and is never substituted; the response reports every
    /// resource the booking landed on.
    /// </summary>
    [DeliveryPlacement]
    [HttpPost("services/{id:guid}/bookings")]
    [ProducesResponseType<ServicePlacementResponseModel>(StatusCodes.Status200OK)]
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
            ? Ok(DeliveryModelMapper.ToServicePlacementResponse(placed.Value))
            : placed.Failures.ToProblemResult();
    }

    private static IActionResult NotFound(Guid id)
        => new DomainFailure(FailureCodes.ServiceNotFound, $"No service exists with id {id}.").ToProblemResult();
}
