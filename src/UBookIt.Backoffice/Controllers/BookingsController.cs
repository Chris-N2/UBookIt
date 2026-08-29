using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using UBookIt.Backoffice.Mapping;
using UBookIt.Backoffice.Models;
using UBookIt.Core;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Core.Stores;

namespace UBookIt.Backoffice.Controllers;

/// <summary>
/// Booking management endpoints.
/// </summary>
/// <remarks>
/// <para>
/// Depends on the booking <b>management</b> port and validated Core types only — never on
/// <c>IBookingStore</c> or <c>Booking.Rehydrate</c>, per the HTTP-caller containment
/// requirement. Authorization comes from the shared base controller.
/// </para>
/// <para>
/// Read-only. Cancelling a booking from the backoffice is its own change; the domain port
/// for it already exists and is deliberately not reached from here yet.
/// </para>
/// </remarks>
[ApiVersion("1.0")]
[ApiExplorerSettings(GroupName = "UBookIt.Backoffice")]
public class BookingsController(
    IBookingManagementStore bookingStore,
    SiteBookingSettings settings) : UBookItBackofficeApiControllerBase
{
    /// <summary>
    /// The bookings overlapping a window of site-local dates.
    /// </summary>
    /// <param name="from">First date in the window, in the site's time zone. Inclusive.</param>
    /// <param name="to">Last date in the window, in the site's time zone. Inclusive.</param>
    /// <param name="statuses">
    /// Which statuses to return, by name. Omitted, the read port's default applies: the
    /// statuses that block time. The default is deliberately NOT restated here — a default
    /// stated in two places is two defaults, and which one a caller meets depends on the
    /// layer they reach first.
    /// <para>
    /// Names rather than the domain enum, for the reason given on <see cref="BookingModel"/>.
    /// An unrecognised name is refused rather than ignored: silently dropping it would
    /// return a page filtered by something other than what was asked for.
    /// </para>
    /// </param>
    /// <param name="resourceIds">
    /// Return only bookings claiming any of these resources. Omitted means no filter.
    /// </param>
    [HttpGet("bookings")]
    [ProducesResponseType<PagedBookingsModel>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListBookings(
        [BindRequired] DateOnly from,
        [BindRequired] DateOnly to,
        [FromQuery] string[]? statuses = null,
        [FromQuery] Guid[]? resourceIds = null,
        int? skip = null,
        int? take = null,
        CancellationToken cancellationToken = default)
    {
        var parsedStatuses = ParseStatuses(statuses);

        if (!parsedStatuses.Succeeded)
        {
            return parsedStatuses.Failures.ToProblemResult();
        }

        var window = BookingWindow.Resolve(from, to, settings);

        if (!window.Succeeded)
        {
            return window.Failures.ToProblemResult();
        }

        // skip/take are passed through only when supplied, so the port's defaults stay the
        // port's. Declaring `= 0` and `= 50` here and always sending them would restate a
        // default the port already settles — two defaults, with the one a caller meets
        // decided by which layer they reach first, which is exactly what this capability
        // forbids. It also silently pins the endpoint if the port's default ever changes.
        var query = BookingQuery.Create(
            window.Value.FromUtc,
            window.Value.ToUtc,
            settings,
            parsedStatuses.Value,
            resourceIds,
            skip ?? BookingQuery.DefaultSkip,
            take ?? BookingQuery.DefaultTake);

        // Should not fail: BookingWindow has already refused every window Create would.
        // Mapped rather than assumed away, because "cannot happen" is how a 500 gets
        // shipped, and the guard belongs to the query type rather than to this caller.
        if (!query.Succeeded)
        {
            return query.Failures.ToProblemResult();
        }

        var page = await bookingStore.ListAsync(query.Value, cancellationToken);

        return Ok(new PagedBookingsModel
        {
            Total = page.Total,
            Items = [.. page.Items.Select(BookingModelMapper.ToModel)],
        });
    }

    /// <summary>
    /// Status names to domain statuses, refusing any name that is not one.
    /// <para>
    /// An empty or absent list stays empty, so the read port applies its own default
    /// rather than this layer inventing a second one.
    /// </para>
    /// </summary>
    private static DomainResult<IReadOnlyCollection<BookingStatus>> ParseStatuses(string[]? statuses)
    {
        if (statuses is null or { Length: 0 })
        {
            return DomainResult<IReadOnlyCollection<BookingStatus>>.Success([]);
        }

        var parsed = new List<BookingStatus>(statuses.Length);

        foreach (var status in statuses)
        {
            // Matched against the NAMES, not parsed. `Enum.TryParse` is the obvious tool
            // here and it is the wrong one, in three separate ways that a name match
            // closes at once:
            //
            //   "Confirmed,Cancelled" — it accepts comma-separated lists and combines
            //     them BITWISE, even on a non-flags enum. Confirmed(1) | Cancelled(3) is
            //     3, which is defined, so a caller asking for confirmed AND cancelled
            //     bookings silently received cancelled ONLY, with a 200. A generated
            //     client or a hand-written fetch comma-joining a repeated query parameter
            //     is an entirely ordinary thing to do.
            //   "1" — it accepts the underlying number, binding to a status by ordinal
            //     rather than by the name the response uses.
            //   " 1" — it trims whitespace before parsing, so a digit guard that does not
            //     trim identically is simply bypassed.
            //
            // Every one of those returns a page filtered by something other than what was
            // asked for, which is exactly what refusing an unrecognised name is meant to
            // prevent.
            var value = Enum.GetValues<BookingStatus>()
                .Cast<BookingStatus?>()
                .FirstOrDefault(candidate =>
                    string.Equals(candidate!.Value.ToString(), status, StringComparison.OrdinalIgnoreCase));

            if (value is null)
            {
                return DomainResult<IReadOnlyCollection<BookingStatus>>.Failure(
                    FailureCodes.BookingStatusInvalid,
                    $"'{status}' is not a booking status. Valid values are: "
                    + string.Join(", ", Enum.GetNames<BookingStatus>()) + ".",
                    nameof(statuses));
            }

            parsed.Add(value.Value);
        }

        return DomainResult<IReadOnlyCollection<BookingStatus>>.Success(parsed);
    }
}
