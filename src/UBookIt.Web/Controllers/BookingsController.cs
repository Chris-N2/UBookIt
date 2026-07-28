using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UBookIt.Core.Bookings;
using UBookIt.Web.Mapping;
using UBookIt.Web.Models;

namespace UBookIt.Web.Controllers;

/// <summary>
/// Public booking placement. Anonymous and body-only (delivery-api spec): the
/// booker comes from the request body with no member key, no ambient identity,
/// and no anti-forgery token. Placement runs the Core pipeline unchanged; the
/// response returns the booking id as the confirmation reference.
/// </summary>
[ApiVersion("1.0")]
[ApiExplorerSettings(GroupName = "UBookIt.Delivery")]
public sealed class BookingsController(IBookingService bookingService) : UBookItDeliveryApiControllerBase
{
    [HttpPost("bookings")]
    [ProducesResponseType<PlacementResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PlaceBooking(
        PlacementRequestModel model, CancellationToken cancellationToken = default)
    {
        var request = DeliveryModelMapper.ToBookingRequest(model);
        if (!request.Succeeded)
        {
            return request.Failures.ToProblemResult();
        }

        var placed = await bookingService.PlaceAsync(request.Value, cancellationToken);

        return placed.Succeeded
            ? Ok(DeliveryModelMapper.ToPlacementResponse(placed.Value))
            : placed.Failures.ToProblemResult();
    }
}
