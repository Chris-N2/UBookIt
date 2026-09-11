# approval-decline (roadmap 0.6.0)

## Why

uBookIt can currently only accept a booking instantly: `BookingService` hard-codes
`Confirmed` at placement, so `Booking.Confirm()` and `Booking.Decline()` — built, tested,
and transitioning from `Requested` — are unreachable, and `BookingStatus.Declined` has no
producer. Not every site wants auto-confirm; a booking system that can only ever accept
instantly is a strange constraint to launch with, and the first public release (17.0.0)
should ship the full lifecycle so every headless consumer and every document is written
against the real state machine from day one. Slotted before email templates deliberately:
approval adds new message types, and the template scheme (0.7.0) should be designed against
the complete message catalogue rather than retrofitted.

## What Changes

- **`AutoConfirm` setting on `SiteBookingSettings`, defaulting to `true`.** The default is
  exactly today's behaviour: nothing changes for any existing site. Site-wide only in this
  change; a per-service override later is purely additive (the site setting becomes the
  default).
- **Placement branches on it.** With `AutoConfirm` off, placement stores the booking as
  `Requested` instead of `Confirmed`. Everything else about placement — validation,
  conflict semantics, atomicity, the placed notification — is unchanged. A `Requested`
  booking already blocks availability: `Booking.IsBlocking` is `Requested or Confirmed`,
  every availability path filters on it, and the `bookings` spec's *Conflict semantics*
  requirement already says so — the delta restates it as scenarios on the paths that now
  reach it, and adds that a decline releases the slot.
- **Operator confirm and decline.** New `IBookingService.ConfirmAsync`/`DeclineAsync`
  driving the existing domain transitions, exposed as authorized management API endpoints
  and as affordances on the backoffice bookings screen, mirroring cancel.
- **The booker is told either way.** New `BookingConfirmedNotification` and
  `BookingDeclinedNotification` (observer methods + Umbraco notifications), with the 0.5.0
  email handler subscribing. `BookingMessageComposer` already carries the wording for all
  four statuses — this change makes the `Requested`/`Declined` branches reachable.
- **The internal placement message says when action is needed.** A placement that produces
  `Requested` tells `InternalRecipients` the booking awaits approval (it already carries
  the backoffice link). Confirm and decline do **not** additionally notify the internal
  list — they performed the action.
- **The shipped confirmation views derive their wording from `Booking.Status`.**
  `Confirmation.cshtml` and `ServiceConfirmation.cshtml` currently say "confirmed"
  unconditionally, which becomes a lie the moment `AutoConfirm` can be off — the same
  foresight 0.5.0 applied to email wording, applied to the pages.
- **Delivery API: documentation only.** Both placement responses already carry `Status` as
  a string; the contract change is that `Requested` is now a value a consumer can receive,
  stated in the spec and docs. No shape change.
- **Correction rider: the bookings screen's cancel dialog tells the truth again.** The
  dialog still asserts flatly that "uBookIt does not tell the person who booked", and the
  `booking-management` spec still requires the view to state "cancelling notifies nobody by
  itself" — both falsified by 0.5.0 on any site that enables booking emails, and missed by
  that change's sweep (the docs were corrected; the dialog and this requirement were not).
  Since this change adds sibling dialogs to the same screen, it corrects the sentence to
  the truthful conditional — the package writes to the booker only where booking emails
  are configured — rather than shipping a new dialog beside a false one.

## Non-goals

- **No per-service or per-resource `AutoConfirm`.** Site-wide only; the finer grain is
  additive later. (Open question recorded for that future change: how directly bookable
  resources participate in a per-service model.)
- **No request expiry.** A pending booking holds its slot until acted on or until its time
  passes; that it is the operator's responsibility is documented, not automated. An
  auto-decline timer silently rejects customers — a worse failure mode than a slow
  operator.
- **No operator-supplied decline reason.** Free text into a customer's inbox is
  template-shaped work (0.7.0) and a liability besides; the decline message says the
  booking could not be accepted, full stop.
- **No changes to who may confirm/decline beyond existing section access.** The
  permissions model is 0.10.0.
- **No pre-submission "this booking will require approval" wording on the booking form.**
  The form today promises nothing about confirmation timing, so nothing it says becomes
  false; the status-derived confirmation page and emails carry the truth. A form-side
  notice can ride with a later change if wanted.

## Capabilities

### New Capabilities

None — approval is a mode of placement plus an operator action, and both homes exist.

### Modified Capabilities

- `bookings`: placement status derives from the new `AutoConfirm` setting; `ConfirmAsync`/
  `DeclineAsync` service operations and their failure semantics; observer gains
  confirmed/declined observations; the requirement that `Requested` blocks availability is
  stated.
- `booking-management`: operator confirm and decline — endpoints, authorization, failure
  mapping, and the backoffice screen affordances — alongside the existing cancel.
- `booking-emails`: the confirmed and declined messages (booker), the internal placement
  message flagging a booking that awaits approval, and the subscription to the new
  notifications; the existing guarantees (no PII to internal recipients, off by default,
  conjunction gating) carried forward explicitly.
- `delivery-api`: placement response `Status` may now be `Requested`; consumer guidance.
- `default-frontend`: the confirmation views' wording derives from the placed booking's
  status.
- `persistence`: the composer binds `UBookIt:AutoConfirm`; resolution falls back to on
  (today's behaviour), with a written-but-unreadable value logged as an error on the
  retention-period precedent.

## Impact

- `UBookIt.Core`: `SiteBookingSettings` (additive property), `BookingService` placement
  branch, `IBookingService` + implementation (additive members), `IBookingObserver`
  (additive members — **note**: adding members to a published interface is breaking for
  external implementors; pre-17.0.0 this is acceptable and is called out here explicitly).
- `UBookIt.Persistence`: settings binding, `UmbracoBookingObserver`, new notification
  types, `BookingEmailHandler` + `BookingMessageComposer` (internal-message pending flag).
- `UBookIt.Backoffice`: `BookingsController` confirm/decline endpoints, models, client
  bookings screen actions + localization.
- `UBookIt.Web`: confirmation view models/views wording.
- `docs/notifications.md` ("No v1 pathway produces those statuses" and the notification
  table change), `docs/backoffice.md`, roadmap already updated.
- No schema change: `Status` is already persisted; no migration needed.
