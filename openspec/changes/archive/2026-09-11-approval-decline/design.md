# approval-decline — design

## Context

The status machine is complete and tested: `Booking.Confirm()` and `Decline()` transition
from `Requested`, `Cancel()` from `Requested or Confirmed`, and `Booking.IsBlocking` is
`Requested or Confirmed` — so a pending booking already holds its slot everywhere
availability is computed (`AvailabilityService`, `ServiceBookingService`, the store
contract). What is missing is every route in: `BookingService` hard-codes
`BookingStatus.Confirmed` at the single placement site both the direct and service paths
funnel through, no service operation drives the transitions, and no notification reports
them.

The 0.5.0 email path anticipated this change: `BookingMessageComposer` derives booker
wording from `Booking.Status` and already carries `Requested` → "We have received your
booking" and `Declined` → "Your booking could not be accepted". The delivery API's two
placement responses already carry `Status` as a string. The debts are elsewhere: the
shipped confirmation views say "Booking confirmed" unconditionally, and
`docs/notifications.md` states "No v1 pathway produces those statuses".

Decisions fixed in explore (with Chris, 2026-09-11): site-wide setting only; no request
expiry; no decline reason; internal recipients are flagged at placement when action is
needed but are not additionally notified of confirm/decline; delivery API is docs-only.

## Goals / Non-Goals

**Goals:**

- `AutoConfirm` on `SiteBookingSettings`, default `true`; placement stores `Requested`
  when it is `false`. No other placement behaviour changes.
- Operator confirm/decline: Core service operations, management API endpoints, backoffice
  bookings-screen actions — each mirroring cancel's existing shape.
- The booker is told on confirm and on decline through the existing email path, under the
  existing gating (site setting AND host can send; nothing by default).
- The shipped confirmation pages and the internal placement message tell the truth about
  a pending booking.

**Non-Goals:** per-service/per-resource AutoConfirm, request expiry, decline reasons,
internal notifications for confirm/decline, form-side pre-submission wording, permissions
beyond existing section access, any schema migration (status is already persisted).

## Decisions

### 1. The setting resolves like `MaxQueryRangeDays`, not like `RetentionDays`

`UBookItPersistenceComposer.ResolveSettings` gains `AutoConfirm` bound from
`UBookIt:AutoConfirm`. An absent value resolves to `true`, silently. A value that was
written and cannot be read as a boolean resolves to `true` **and logs an error naming the
setting** — the retention-period precedent: the site wrote something and is not getting
what it wrote. Rationale for the fallback direction: neither misreading is safe, and only
one is silent — a site accidentally *on* sends confirmations it can see and correct, while
a site accidentally *off* parks customers' bookings in a state nobody is watching for. So
*off* must be explicit and readable. (Contrast `RetentionDays`, where the dangerous
direction was *on* and the fallback is therefore null/off.)

### 2. One branch at the single placement site

`BookingService.PlaceAsync` (private core, `BookingService.cs:381`) chooses
`settings.AutoConfirm ? Confirmed : Requested`. Both public overloads and
`PlaceForServiceAsync` funnel through it, so direct and service placement cannot diverge.
`BookingService` already takes `SiteBookingSettings`; no new dependency.

### 3. `ConfirmAsync`/`DeclineAsync` mirror `CancelAsync` exactly

Load → domain transition (`booking.Confirm()`/`Decline()`) → `UpdateAsync` → observer →
return the booking. Not-found and invalid-transition failures reuse the existing
`FailureCodes.BookingNotFound` / `FailureCodes.InvalidStatusTransition` — no new failure
codes. The same property cancel documents holds here and the spec states it: **being
observed at all means the transition just happened**, because both transitions succeed
only from `Requested` and a second attempt fails before the store is touched.

Alternative considered: a single `TransitionAsync(id, target)` generic operation.
Rejected — it would publish "set a booking's status" as API surface, inviting exactly the
transitions the status machine exists to refuse, and cancel already set the
one-operation-per-verb precedent.

### 4. Observer and notifications grow additively — with the break named

`IBookingObserver` gains `BookingConfirmedAsync`/`BookingDeclinedAsync`;
`NullBookingObserver` and `UmbracoBookingObserver` implement them;
`BookingConfirmedNotification`/`BookingDeclinedNotification` join the existing pair in
`UBookIt.Persistence.Notifications`, published through the same swallow-and-log path.
Like `BookingCancelledNotification`, neither needs before-and-after: both transitions
come only from `Requested`, so being told at all says everything.

**Adding members to a published interface breaks external `IBookingObserver`
implementors.** Called out per the compatibility convention: pre-17.0.0, accepted — this
is precisely why approval ships before the promise starts. (No default interface
implementations: an observer silently deaf to declines would be worse than a compile
error.)

### 5. The email handler subscribes to both; the composer gains two events

`BookingEvent` gains `Confirmed` and `Declined`; `BookingEmailHandler` handles the two
new notifications through the existing `SendAsync`, under the existing conjunction gating
and PII rules. Booker subject/body continue to derive from `Booking.Status`, which is
already correct for every event except `Cancelled` (which keeps its explicit branch — a
cancelled booking's status alone cannot say "we are telling you it was cancelled", and
that branch already exists).

Internal messages: confirm/decline events send **nothing** to `InternalRecipients`. The
placement internal message, when the stored booking is `Requested`, states that the
booking awaits approval — alongside the backoffice link it already carries, still with no
booker contact details. Derived from `Booking.Status`, not from the setting: the message
describes the booking it announces, so it cannot drift from what placement actually did.

A booker erased before confirm/decline (possible: retention erases after a booking's
end, and a `Requested` booking can outlive its end untouched) gets nothing, exactly as
cancellation already handles — no address, nothing sent, internal recipients unaffected.

### 6. Management API and backoffice mirror cancel

`UBookIt.Backoffice` `BookingsController` gains `POST …/{id}/confirm` and
`…/{id}/decline` beside the existing cancel endpoint: same authorization policy, same
`DomainResult` → problem-details mapping (`BookingNotFound` → 404,
`InvalidStatusTransition` → 400 with the stable code in the body — verified against
`ApiResults.ToProblemResult`, which reserves 409 for `resource-in-use`; cancel already
returns 400 here and the spec requires only that the two failures be distinct),
purpose-built response models. The
client bookings screen shows Confirm/Decline for `Requested` bookings only (the list
already carries and filters by status), with localization entries following the existing
pattern. Decline requires the accessible in-page modal (terminal and outward-facing, like
cancel); confirm does not (the expected disposition, still cancellable afterwards). Cancel
remains available for `Requested` — that is the booker-withdrew path and the domain
already permits it.

**Correction rider:** the cancel dialog's "uBookIt does not tell the person who booked"
(`en-us.ts` `confirmCancelContent`) and the spec sentence demanding it were falsified by
0.5.0 on email-configured sites; both are corrected here to the truthful conditional, and
the new decline modal states the same conditional rather than being born beside a false
sibling.

### 7. Confirmation views derive wording from status via an additive model property

`BookingConfirmationModel` (and its service-flow counterpart) gains the placed booking's
status — as a boolean-shaped "is pending" or the status itself, settled at apply; the
model is serialized through TempData and both additions are additive. The views branch:
heading, `aria-label`, and lead sentence say "Booking requested"/"We have received your
booking request" when pending. The model is part of the published theme contract
(`UBookItThemeContract`), so the addition is called out as additive there; what a theme's
own Confirmation view does with it is the theme author's, per the theming capability's
existing narrowing.

## Risks / Trade-offs

- **[Pending bookings can sit unactioned]** → Documented as the operator's
  responsibility in `docs/backoffice.md` and `docs/notifications.md`; the internal
  placement email flags "awaiting approval"; the bookings screen's status filter finds
  them. No timer, by decision.
- **[`IBookingObserver` break for external implementors]** → Named in proposal, design,
  and spec delta; shipped before 17.0.0 starts the compatibility promise.
- **[Wording drift between page, email, and status]** → Both page and email derive from
  `Booking.Status`; no string states "confirmed" on a path a `Requested` booking can
  reach. QA precedent from 0.5.0: guard the absence claim with wrap-safe matchers, and
  enumerate the class of "confirmed" wordings (views, emails, XML docs, docs/*.md), not a
  sample.
- **[Docs over-claim]** → `docs/notifications.md` asserts in several places that decline
  has no producer and the notification table lists exactly two notifications; the sweep
  must enumerate every such sentence (the falsified-sentence grep has fired on four
  consecutive changes and this change falsifies known prose deliberately).
- **[Composer wording for `Cancelled` event vs status]** → Unchanged branch; covered by
  existing tests. New tests pin subject/body for the two new events including the
  placed-while-Requested internal flag.

## Migration Plan

No schema change (`Status` already persisted; `Requested`/`Declined` are existing enum
values). Additive settings key. Rollback is package downgrade; bookings left `Requested`
by a downgrade would be invisible to the old cancel-only screen but remain blocking —
acceptable pre-release, noted here so it is a decision.

## Open Questions

None blocking. Deferred by decision: per-service AutoConfirm (and how directly bookable
resources participate in that model), request expiry, decline reasons — recorded in the
roadmap's Not-yet-slotted notes and proposal non-goals.
