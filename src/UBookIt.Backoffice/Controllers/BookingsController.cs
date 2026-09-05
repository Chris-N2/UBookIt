using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using UBookIt.Backoffice.Mapping;
using UBookIt.Backoffice.Models;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
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
/// Reads through the management port and cancels through the Core booking service. Cancelling
/// is the second and last of v1's management verbs; approving, declining and amending are
/// each domain changes rather than endpoints, and none of them is here.
/// </para>
/// <para>
/// <b>Two gates, answering different questions.</b> The base controller's section policy
/// decides whether this user may reach uBookIt at all; Umbraco's sensitive-data access decides
/// whether the rows they get carry the booker's contact details. A user holding the section
/// alone gets every booking, without the people.
/// </para>
/// </remarks>
[ApiVersion("1.0")]
[ApiExplorerSettings(GroupName = "UBookIt.Backoffice")]
public class BookingsController(
    IBookingManagementStore bookingStore,
    IBookingService bookingService,
    SiteBookingSettings settings,
    IBackOfficeSecurityAccessor backOfficeSecurityAccessor) : UBookItBackofficeApiControllerBase
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

        var bookerVisibility = ResolveBookerVisibility();

        return Ok(new PagedBookingsModel
        {
            Total = page.Total,
            Items = [.. page.Items.Select(summary => BookingModelMapper.ToModel(summary, bookerVisibility))],
        });
    }

    /// <summary>
    /// Whether this caller may be shown booker contact details, per Umbraco's Sensitive data
    /// group.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Membership of a built-in group with a fixed key, tested through Umbraco rather than
    /// against the key directly, and deliberately not a uBookIt setting of its own: Umbraco
    /// already ships the group and applies the same concept to content properties marked
    /// sensitive, so a site meets one idea rather than two that are free to disagree.
    /// </para>
    /// <para>
    /// <b>An unresolvable user withholds.</b> The endpoint is already authorized, so a null
    /// here should not occur — and "should not occur" is how a defaulted <c>true</c> ships. The
    /// branch is written rather than assumed away for the same reason the query result above is
    /// mapped rather than asserted impossible, and with more at stake: the failure mode of
    /// guessing wrong is disclosing personal data to somebody the site excluded, where the
    /// failure mode of withholding is a support question.
    /// </para>
    /// </remarks>
    private BookerVisibility ResolveBookerVisibility()
    {
        IUser? currentUser = backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser;

        return currentUser?.HasAccessToSensitiveData() is true
            ? BookerVisibility.Shown
            : BookerVisibility.Withheld;
    }

    /// <summary>
    /// Cancels a booking.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>POST rather than DELETE, because a cancelled booking is not gone.</b> It keeps its
    /// row, its interval, its booker and the service it was placed for; it stops holding its
    /// time; and the management list still returns it when cancelled bookings are asked for.
    /// <c>DELETE</c> would say the opposite of all of that.
    /// </para>
    /// <para>
    /// <b>The status machine is the rule, not the screen.</b> This applies no judgement of its
    /// own about whether a booking can be cancelled — it asks the domain, which permits the
    /// transition only from <c>Requested</c> or <c>Confirmed</c>. A screen showing a stale
    /// list therefore cannot talk this into an invalid transition, and a second attempt is
    /// refused rather than quietly reported as success: a caller told "cancelled" when nothing
    /// changed cannot tell a completed action from a rejected one.
    /// </para>
    /// <para>
    /// Returns the booking's identity and its new status rather than a whole
    /// <see cref="BookingModel"/> — this path cannot honestly fill one, because a
    /// <c>Booking</c> knows its resources by id and the list model carries their names. See
    /// <see cref="CancelledBookingModel"/>.
    /// </para>
    /// </remarks>
    /// <param name="id">The booking to cancel.</param>
    [HttpPost("bookings/{id:guid}/cancel")]
    [ProducesResponseType<CancelledBookingModel>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelBooking(Guid id, CancellationToken cancellationToken = default)
    {
        var cancelled = await bookingService.CancelAsync(id, cancellationToken);

        if (!cancelled.Succeeded)
        {
            return cancelled.Failures.ToProblemResult();
        }

        return Ok(new CancelledBookingModel
        {
            BookingId = cancelled.Value.Id,
            Status = cancelled.Value.Status.ToString(),
        });
    }

    /// <summary>
    /// Finds every booking whose booker holds a given email address.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This exists so an erasure request can be honoured.</b> A data subject writes in with
    /// an address, not a date and not a booking id — and the management list is windowed, so
    /// without this an operator has to guess when somebody booked. The erase verb was shipped
    /// before the means of finding what to erase; this is the other half.
    /// </para>
    /// <para>
    /// <b>Gated on sensitive-data access, as the endpoint's own authorization.</b> The same
    /// policy the erase endpoint carries, which composes the section requirement too, so naming
    /// it can only narrow. Expressed as a policy rather than a check inside the handler for the
    /// reason the <c>sensitive-data</c> capability now states outright: a filter whose gate is a
    /// condition in a handler is correct only while everybody remembers to write it, and leaves
    /// a route that reaches the query having established nothing.
    /// </para>
    /// <para>
    /// <b>A separate endpoint rather than a filter on the list, for two independent reasons.</b>
    /// The query must be unwindowed, and <c>BookingQuery</c> cannot express that by
    /// construction — relaxing its window to serve this would remove a guarantee from every
    /// caller of the list. And the authorization differs, which belongs in the endpoint rather
    /// than in one of its parameters.
    /// </para>
    /// <para>
    /// <b>POST, for a read.</b> See <see cref="FindBookingsByBookerModel"/>: an address in a
    /// query string is logged by the web server, by every proxy, and by the browser, none of
    /// which this endpoint's gate controls.
    /// </para>
    /// <para>
    /// <b>The refusal does not depend on the answer.</b> A caller who may not read contact
    /// details is refused by the policy before any query runs, so they learn nothing from
    /// asking — not from a count, not from an error, and not from how long it took.
    /// </para>
    /// </remarks>
    [HttpPost("bookings/find-by-booker")]
    [Authorize(Policy = Constants.SensitiveDataAccessPolicy)]
    [ProducesResponseType<PagedBookingsModel>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> FindBookingsByBooker(
        [FromBody] FindBookingsByBookerModel request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = BookerEmailQuery.Create(
            request.Email,
            request.Skip ?? BookerEmailQuery.DefaultSkip,
            request.Take ?? BookerEmailQuery.DefaultTake);

        if (!query.Succeeded)
        {
            return query.Failures.ToProblemResult();
        }

        var page = await bookingStore.FindByBookerEmailAsync(query.Value, cancellationToken);

        // The caller reached this at all, so they hold sensitive-data access and the rows carry
        // their details. Passed explicitly rather than assumed: the mapper requires the decision
        // as an argument, which is what stops a future caller composing a row without making it.
        return Ok(new PagedBookingsModel
        {
            Total = page.Total,
            Items = [.. page.Items.Select(summary => BookingModelMapper.ToModel(summary, BookerVisibility.Shown))],
        });
    }

    /// <summary>
    /// Erases a booking's booker contact details and member key.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Anonymisation, not deletion — so POST, not DELETE.</b> The booking keeps its row,
    /// its reference, its interval, its claims and its status, and goes on blocking the time
    /// it always blocked. What leaves is the person. <c>DELETE</c> would say the opposite of
    /// all of that, exactly as it would for a cancellation.
    /// </para>
    /// <para>
    /// <b>Gated on sensitive-data access as well as the section</b>, and by policy rather than
    /// by a check in this method: a caller who may not be shown a booker's name should not be
    /// able to destroy it, and expressing that as a condition inside the handler would leave a
    /// route that reaches the operation having established nothing. The policy carries the
    /// section requirement too, so this attribute only ever narrows what the base controller
    /// already demands.
    /// </para>
    /// <para>
    /// <b>Idempotent, unlike cancelling.</b> Erasing an already-erased booking succeeds and
    /// changes nothing, returning the original instant. Cancellation refuses a second attempt
    /// because it is a transition whose starting state matters; erasure is absorbing, its
    /// outcome is identical either way, and the retention job that will call it must be safe
    /// to retry.
    /// </para>
    /// <para>
    /// <b>The response echoes nothing that was erased.</b> See
    /// <see cref="ErasedBookerModel"/>.
    /// </para>
    /// </remarks>
    /// <param name="id">The booking whose booker to erase.</param>
    [HttpPost("bookings/{id:guid}/erase-booker")]
    [Authorize(Policy = Constants.SensitiveDataAccessPolicy)]
    [ProducesResponseType<ErasedBookerModel>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EraseBooker(Guid id, CancellationToken cancellationToken = default)
    {
        var erased = await bookingService.EraseBookerAsync(id, cancellationToken);

        if (!erased.Succeeded)
        {
            return erased.Failures.ToProblemResult();
        }

        // Not asserted away. The domain guarantees a booking is erased once EraseBooker has
        // run, so a null here would mean that guarantee had broken — and reporting a
        // successful erasure with a fabricated instant would be the worst available answer to
        // that, because it says the data is gone when nothing established that it is.
        if (erased.Value.Booker.ErasedUtc is not { } erasedUtc)
        {
            return Problem(
                title: "The booking was not erased.",
                detail: "The erase operation reported success but the booking still carries "
                        + "booker contact details.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        return Ok(new ErasedBookerModel
        {
            BookingId = erased.Value.Id,
            ErasedUtc = erasedUtc,
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
