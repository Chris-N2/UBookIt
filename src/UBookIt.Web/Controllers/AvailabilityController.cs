using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using UBookIt.Core;
using UBookIt.Core.Availability;
using UBookIt.Web.Mapping;
using UBookIt.Web.Models;

namespace UBookIt.Web.Controllers;

/// <summary>
/// Public availability queries: free-time intervals and projected slots for a
/// resource over a bounded date range. Instants are ISO-8601 UTC and the
/// display zone is carried once per response. The bounded-range guard lives in
/// Core (availability spec) — an over-wide range fails with
/// <c>date-range-too-large</c>.
/// </summary>
[ApiVersion("1.0")]
[ApiExplorerSettings(GroupName = "UBookIt.Delivery")]
public sealed class AvailabilityController(
    IAvailabilityQueryService availability,
    SiteBookingSettings settings) : UBookItDeliveryApiControllerBase
{
    [DeliveryRead]
    [HttpGet("resources/{resourceId:guid}/free-time")]
    [ProducesResponseType<FreeTimeResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFreeTime(
        Guid resourceId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var result = await availability.GetFreeTimeAsync(resourceId, from, to, cancellationToken);

        return result.Succeeded
            ? Ok(new FreeTimeResponseModel
            {
                ResourceId = resourceId,
                ZoneId = settings.TimeZoneId,
                Intervals = result.Value.Select(DeliveryModelMapper.ToIntervalModel).ToList(),
            })
            : result.Failures.ToProblemResult();
    }

    [DeliveryRead]
    [HttpGet("resources/{resourceId:guid}/slots")]
    [ProducesResponseType<SlotsResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSlots(
        Guid resourceId, DateOnly from, DateOnly to, [BindRequired] int durationMinutes,
        CancellationToken cancellationToken = default)
    {
        var result = await availability.GetSlotsAsync(
            resourceId, from, to, TimeSpan.FromMinutes(durationMinutes), cancellationToken);

        return result.Succeeded
            ? Ok(new SlotsResponseModel
            {
                ResourceId = resourceId,
                ZoneId = settings.TimeZoneId,
                DurationMinutes = durationMinutes,
                Slots = result.Value.Select(DeliveryModelMapper.ToSlotModel).ToList(),
            })
            : result.Failures.ToProblemResult();
    }

    /// <summary>
    /// Every bookable start over the range with how long may be booked from
    /// each. Deliberately takes no duration — a client filters the response for
    /// whichever length it needs, so one call answers every length and the
    /// longest available is discoverable rather than guessed.
    /// </summary>
    [DeliveryRead]
    [HttpGet("resources/{resourceId:guid}/bookable-starts")]
    [ProducesResponseType<BookableStartsResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBookableStarts(
        Guid resourceId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var result = await availability.GetBookableStartsAsync(resourceId, from, to, cancellationToken);

        return result.Succeeded
            ? Ok(new BookableStartsResponseModel
            {
                ResourceId = resourceId,
                ZoneId = settings.TimeZoneId,
                Starts = result.Value.Select(DeliveryModelMapper.ToBookableStartModel).ToList(),
            })
            : result.Failures.ToProblemResult();
    }
}
