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
/// requirement. The shared base controller supplies the section gate; each action names
/// its own verb policy on top (see <see cref="Constants.VerbPolicies"/>).
/// </para>
/// <para>
/// Reads through the management port and changes a booking through the Core booking service.
/// The status verbs are <b>cancel</b>, and — for a booking placed while the site's
/// <c>AutoConfirm</c> setting is off, so that it awaits a decision — <b>confirm</b> and
/// <b>decline</b>. <b>Move</b> changes a booking's time and nothing else: its reference, status,
/// booker, service and resources are what they were, and the new interval runs the placement
/// rules on an operator's terms.
/// </para>
/// <para>
/// <b>Three gates, answering different questions.</b> The base controller's section policy
/// decides whether this user may reach uBookIt at all; each action's verb policy decides
/// whether they may see bookings (or, for the status verbs, act on them); Umbraco's
/// sensitive-data access decides whether the rows they get carry the booker's contact
/// details. A user holding the section and the see-bookings permission gets every booking,
/// without the people; the section alone gets a <c>403</c>.
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
    [Authorize(Policy = Constants.VerbPolicies.BookingsRead)]
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
    [Authorize(Policy = Constants.VerbPolicies.BookingsManage)]
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
    /// Confirms a requested booking.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The status machine is the rule, not the screen</b>, on exactly the terms
    /// <see cref="CancelBooking"/> records: no judgement of its own, so a stale list cannot
    /// talk this into confirming a booking that is not <c>Requested</c>, and a second attempt
    /// is refused rather than reported as success.
    /// </para>
    /// <para>
    /// Returns identity and new status only, for the reason
    /// <see cref="CancelledBookingModel"/> states — this path cannot honestly fill a list row.
    /// </para>
    /// </remarks>
    /// <param name="id">The booking to confirm.</param>
    [Authorize(Policy = Constants.VerbPolicies.BookingsManage)]
    [HttpPost("bookings/{id:guid}/confirm")]
    [ProducesResponseType<ConfirmedBookingModel>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConfirmBooking(Guid id, CancellationToken cancellationToken = default)
    {
        var confirmed = await bookingService.ConfirmAsync(id, cancellationToken);

        if (!confirmed.Succeeded)
        {
            return confirmed.Failures.ToProblemResult();
        }

        return Ok(new ConfirmedBookingModel
        {
            BookingId = confirmed.Value.Id,
            Status = confirmed.Value.Status.ToString(),
        });
    }

    /// <summary>
    /// Declines a requested booking.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>POST rather than DELETE, because a declined booking is not gone.</b> It keeps its
    /// row, its interval, its booker and the service it was placed for; it stops holding its
    /// time; the management list still returns it when declined bookings are asked for; and
    /// its booker is still erasable — a person the site turned away holds their details
    /// exactly as firmly as one it served.
    /// </para>
    /// <para>
    /// Otherwise on <see cref="ConfirmBooking"/>'s terms: the status machine is the rule, and
    /// the response is identity and new status only.
    /// </para>
    /// </remarks>
    /// <param name="id">The booking to decline.</param>
    [Authorize(Policy = Constants.VerbPolicies.BookingsManage)]
    [HttpPost("bookings/{id:guid}/decline")]
    [ProducesResponseType<DeclinedBookingModel>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeclineBooking(Guid id, CancellationToken cancellationToken = default)
    {
        var declined = await bookingService.DeclineAsync(id, cancellationToken);

        if (!declined.Succeeded)
        {
            return declined.Failures.ToProblemResult();
        }

        return Ok(new DeclinedBookingModel
        {
            BookingId = declined.Value.Id,
            Status = declined.Value.Status.ToString(),
        });
    }

    /// <summary>
    /// Moves a booking to a new start and length.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The domain is the rule, not this endpoint.</b> Which statuses permit a move, which
    /// placement rules the new interval runs and on whose terms, and the refusal of an interval
    /// the booking already holds are all decided by the booking service; nothing here adds a
    /// rule or relaxes one. A refusal comes back as the domain's stable code — a scheduler
    /// dragging a booking needs the code to say why a drop was refused, not only that it was.
    /// </para>
    /// <para>
    /// <b>The start is read in the site's zone</b>, on the list window's convention, and
    /// converted once here. The response carries identity, the unchanged status and the new
    /// interval — not a list row, for the reason <see cref="CancelledBookingModel"/> states.
    /// </para>
    /// </remarks>
    /// <param name="id">The booking to move.</param>
    /// <param name="model">Where it should now be.</param>
    [Authorize(Policy = Constants.VerbPolicies.BookingsManage)]
    [HttpPost("bookings/{id:guid}/move")]
    [ProducesResponseType<MovedBookingModel>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MoveBooking(
        Guid id, MoveBookingRequestModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        // Shape before substance: a start that was never supplied binds as the default
        // DateTime, and a zero or negative length is not an interval. Both are refused with
        // the domain's own code against the field, before the domain is asked anything.
        var shapeFailures = new List<DomainFailure>();

        if (model.Start == default)
        {
            shapeFailures.Add(new DomainFailure(
                FailureCodes.IntervalInvalid, "A start is required.", nameof(model.Start)));
        }

        if (model.LengthMinutes <= 0)
        {
            shapeFailures.Add(new DomainFailure(
                FailureCodes.IntervalInvalid, "The length must be a positive number of minutes.", nameof(model.LengthMinutes)));
        }

        if (shapeFailures.Count > 0)
        {
            return shapeFailures.ToProblemResult();
        }

        var start = BookingWindow.ResolveSiteLocal(model.Start, settings, nameof(model.Start));

        if (!start.Succeeded)
        {
            return start.Failures.ToProblemResult();
        }

        var moved = await bookingService.MoveAsync(
            id, start.Value.Utc, TimeSpan.FromMinutes(model.LengthMinutes), cancellationToken);

        if (!moved.Succeeded)
        {
            return moved.Failures.ToProblemResult();
        }

        return Ok(new MovedBookingModel
        {
            BookingId = moved.Value.Id,
            Status = moved.Value.Status.ToString(),
            StartUtc = moved.Value.Interval.StartUtc,
            EndUtc = moved.Value.Interval.EndUtc,
            TimeZoneId = moved.Value.Interval.TimeZoneId,
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
    [Authorize(Policy = Constants.VerbPolicies.BookingsRead)]
    [HttpPost("bookings/find-by-booker")]
    [Authorize(Policy = Constants.SensitiveDataAccessPolicy)]
    [ProducesResponseType<PagedBookingsModel>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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

        // Resolved once, not per row — the same user gives the same answer, and hoisting it
        // matches how the list endpoint above does it. Two adjacent endpoints calling the same
        // decision differently invites a reader to wonder which is right.
        var bookerVisibility = ResolveBookerVisibility();

        // ASKED, not assumed from having got here.
        //
        // Reaching this action means the policy held, so `Shown` would be correct — and it
        // would be correct because of a registration in a composer, one file away, rather than
        // because anybody checked. The `sensitive-data` capability says the package determines
        // this "by asking Umbraco whether the user belongs to the built-in Sensitive data user
        // group", and that is a cheap call on a class this controller already has.
        //
        // The cost is one method call; what it buys is that a mis-composed policy makes this
        // endpoint withhold, exactly as the list does, instead of being the one place in the
        // package that discloses on the strength of an attribute.
        return Ok(new PagedBookingsModel
        {
            Total = page.Total,
            Items = [.. page.Items.Select(summary => BookingModelMapper.ToModel(summary, bookerVisibility))],
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
    [Authorize(Policy = Constants.VerbPolicies.BookingsRead)]
    [HttpPost("bookings/{id:guid}/erase-booker")]
    [Authorize(Policy = Constants.SensitiveDataAccessPolicy)]
    [ProducesResponseType<ErasedBookerModel>(StatusCodes.Status200OK)]
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
