# approval-decline — tasks

## 1. Core: the setting and the placement branch

- [x] 1.1 Add `AutoConfirm` (bool, init, default `true`) to `SiteBookingSettings` with XML docs
      stating the default, the fallback direction, and why (silent-failure asymmetry — see
      design decision 1).
- [x] 1.2 Branch `BookingService.PlaceAsync` (the private core both public overloads and
      `PlaceForServiceAsync` funnel through) on `settings.AutoConfirm`:
      `Confirmed` when on, `Requested` when off. No other placement behaviour changes.
- [x] 1.3 Tests: placement under on → `Confirmed`; under off → `Requested`; service placement
      agrees with direct placement under the same setting; a `Requested` placement's claims
      block a second placement over the same interval (restates conflict semantics on the
      newly reachable path).

## 2. Core: confirm and decline operations

- [x] 2.1 Add `ConfirmAsync(Guid, CancellationToken)` and `DeclineAsync(Guid, CancellationToken)`
      to `IBookingService` and `BookingService`, mirroring `CancelAsync` exactly:
      load → `booking.Confirm()`/`Decline()` → `UpdateAsync` → observe → return. Reuse
      `booking-not-found` / `invalid-status-transition`; no new failure codes.
- [x] 2.2 Add `BookingConfirmedAsync`/`BookingDeclinedAsync` to `IBookingObserver`,
      `NullBookingObserver`, and every test double. **No default interface implementations**
      (spec: an observer silently deaf to declines is worse than a compile error).
- [x] 2.3 Tests: confirm/decline from `Requested` succeed, store, and are observed exactly once,
      after the store call; from any other status fail with `invalid-status-transition`, touch
      the store not at all, and observe nothing; unknown id → `booking-not-found`, nothing
      observed; a throwing observer does not change the caller's result or the stored status;
      a declined booking's interval is free again (decline releases the slot).

## 3. Persistence: settings resolution, notifications, observer

- [x] 3.1 Bind `UBookIt:AutoConfirm` in `UBookItPersistenceComposer.ResolveSettings`: absent →
      `true` silently; written-but-unreadable → `true` + error logged naming the setting
      (retention-period precedent); readable `false` → off. Tests for all three, including
      that the absent case logs nothing.
- [x] 3.2 Add `BookingConfirmedNotification` and `BookingDeclinedNotification` beside the
      existing pair, with remarks following the house pattern (no before-and-after needed;
      handler-throws caveat; package-sends-nothing caveat).
- [x] 3.3 Extend `UmbracoBookingObserver` with the two new publications through the existing
      swallow-and-log path (booking id only in the log line — the PII rule is unchanged).
- [x] 3.4 Tests: each notification published once on success, not on failure; the observer's
      catch still logs id-only.

## 4. Emails

- [x] 4.1 Add `BookingEvent.Confirmed` and `BookingEvent.Declined`; subscribe
      `BookingEmailHandler` to both new notifications in the composer.
- [x] 4.2 Route the new events through `SendAsync` under the existing gating. **Booker only**:
      confirmed/declined events send nothing to `InternalRecipients` (spec: "Which events
      produce messages, and for whom").
- [x] 4.3 Internal placement message: when the announced booking is `Requested`, state that it
      awaits approval — derived from `Booking.Status`, never from the setting; still no booker
      name/address/phone; the backoffice link unchanged.
- [x] 4.4 Tests: confirm → booker message with confirmed wording, zero internal messages;
      decline → declined wording, zero internal; decline with booker emails disabled → zero
      messages total; auto-confirmed placement → exactly one booker message; erased booker on
      confirm/decline → nothing sent to anyone; requested placement's internal message states
      the wait and a confirmed placement's does not; the wait-flag message carries no PII
      (redact GUIDs from the haystack before any DoesNotContain on a name — the "Ada is three
      hex digits" lesson).

## 5. Management API

- [x] 5.1 Add `POST /bookings/{id}/confirm` and `POST /bookings/{id}/decline` to the backoffice
      `BookingsController`: same authorization policy and swagger group as cancel, same
      failure mapping (`booking-not-found` → 404; `invalid-status-transition` → 400 with the
      stable code, as ApiResults.ToProblemResult maps it for cancel — 409 is reserved for
      resource-in-use),
      purpose-built response models carrying identity + new status (no list-row imitation).
- [x] 5.2 Endpoint tests: success both verbs; wrong-status 400 carrying the stable
      invalid-status-transition code; unknown-id 404, distinct;
      unauthenticated 401 with no change; declined booking still enumerable by the list and
      its booker still erasable.

## 6. Backoffice client

- [ ] 6.1 Regenerate the API client; add Confirm/Decline row actions offered **only** for
      `Requested` rows; decline behind the accessible in-page modal, confirm without one;
      refused transitions surfaced, not swallowed; list refreshes in place; last-row paging
      step-back and deliberate focus placement reuse the cancel behaviour where a row leaves
      the current filter.
- [ ] 6.2 Localization: new entries following the existing commented style. The decline modal
      states the truthful notification conditional.
- [ ] 6.3 **Correction rider:** reword `confirmCancelContent` to the truthful conditional (the
      package writes to the booker only where booking emails are configured) and update its
      explanatory comment — the current sentence is false on email-configured sites.
- [ ] 6.4 Client tests: controls offered/withheld by status; modal dismiss issues no request;
      the corrected cancel wording and the decline wording each guarded by a wrap-safe
      absence check on the falsified claim ("does not tell" asserted unconditionally), not
      only a presence check on the correction — presence does not assert absence.

## 7. Front end

- [ ] 7.1 Add the placed booking's status to `BookingConfirmationModel` and the service
      confirmation model (additive members; theme contract note updated in
      `UBookItThemeContract` remarks if any).
- [ ] 7.2 Branch `Confirmation.cshtml` and `ServiceConfirmation.cshtml`: heading, `aria-label`,
      and lead sentence derive from status; everything else identical in both states.
- [ ] 7.3 Tests: both views in both states (the "every branch a view carries can be taken" and
      "a view renders every state its model can express" requirements now bind these views);
      the pending render contains no "confirmed" claim — wrap-safe absence assertion; the
      pending render still shows reference, interval, what was booked, booker details.

## 8. Documentation sweep — enumerate the class, not the sample

- [ ] 8.1 `docs/notifications.md`: grep for every sentence the change falsifies before editing —
      known members of the class: "No v1 pathway produces those statuses", the two-row
      notification table, "Approving or declining a booking" under *What does not raise a
      notification*, and any "placed or cancelled" enumeration that is now four events. Then
      grep the whole of `docs/` and all XML doc comments for `Requested`, `Declined`,
      `auto-confirm`, "placed or cancelled", "does not tell", "notifies nobody" and fix every
      hit or record why it stands.
- [ ] 8.2 `docs/notifications.md` + `docs/backoffice.md`: document `AutoConfirm` (key, default,
      fallback direction, malformed-value error log), the approval flow, that a pending
      booking holds its slot and is the operator's responsibility (no expiry), and the two
      new notifications with their audiences.
- [ ] 8.3 Update `BookingNotifications.cs` / `BookingCancelledNotification` remarks if any now
      over- or under-claim (the "permits cancellation only from Requested or Confirmed"
      sentence stays true — verify rather than assume).
- [ ] 8.4 Documentation guards: extend the existing `DocumentationAssert`-based tests — absence
      checks with `DoesNotSay` (never raw `DoesNotContain`) for the falsified claims; one
      assertion per guard, mutation-checked one at a time.

## 9. Sync obligations recorded now (executed at sync/archive)

- [ ] 9.1 Sibling-spec grep at sync: `Confirmed` / `no v1 pathway` / "placed or cancelled" /
      "notifies nobody" across every spec **not** touched by this change's deltas — the
      outward sweep that has fired on four consecutive changes. Known candidates to open:
      `booker-erasure` (erasure vs pending bookings), `booking-retention` (sweep has no status
      filter — should stay true), `service-booking`, `theming` (confirmation view contract),
      `sensitive-data`.
- [ ] 9.2 Capability Purpose prose check: `bookings` and `booking-emails` Purposes summarize
      auto-confirm-era behaviour — verify each Purpose still matches its requirements as the
      file stands after sync (consistency check, green pre-sync).

## 10. Verification

- [ ] 10.1 Full clean Release build: zero warnings; full test suite green (.NET + client).
- [ ] 10.2 Live check on the TestSite (pickup directory, cleared first — or you measure
      history): place with `AutoConfirm` off → form completes, confirmation page reads
      "received/awaiting", internal `.eml` flags awaiting approval, booker `.eml` says
      received; confirm from the backoffice → booker `.eml` says confirmed, no internal
      `.eml`; decline a second booking → declined `.eml`, slot free again on the front end;
      flip the setting off entirely → behaviour identical to today.
- [ ] 10.3 Update `roadmap/version_roadmap.md` row status if wording needs it, and bump the
      package version to 0.6.0 wherever the packaging change pinned it.
