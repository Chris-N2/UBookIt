# approval-decline — tasks

## 0. Modified requirements — the wholesale-replacement diff

Every `## MODIFIED Requirements` entry replaces its requirement wholesale, so each one below
was diffed guarantee-by-guarantee before the delta was written (CLAUDE.md, "Rewriting a
requirement destroys guarantees silently"). Named here so each replacement is a decision with
an owner:

- [x] 0.1 `bookings` / "Booking status machine" — the "no v1 pathway" sentence is
      deliberately dropped (that is the change); every transition, code and scenario carried
      forward, with new scenarios for the routes in.
- [x] 0.2 `bookings` / "Placement validation pipeline" — carried verbatim except one
      scenario's THEN, which asserted a `Confirmed` result and now defers to the status
      machine's derivation.
- [x] 0.3 `bookings` / "Availability and placement service ports" — carried verbatim
      including all three BREAKING blocks and widening notes; the service enumeration gains
      confirm/decline with a fourth widening note.
- [x] 0.4 `bookings` / "Placement and cancellation are observable" — renamed via the RENAMED
      entry to "Placement and status changes are observable" (kept on one line here: the
      guard that forces this list matches the name as a substring, and a wrapped name is the
      exact defeat the booking-emails rounds proved); every guarantee carried forward and
      extended to the two new reports; the observer-port widening called out as BREAKING
      with no default implementations.
- [x] 0.5 `booking-emails` / "The package sends nothing until a site asks it to" — carried
      verbatim; one scenario's WHEN widens "placed or cancelled" to all four events.
- [x] 0.6 `booking-management` / "The bookings view can cancel a booking" — carried verbatim
      except the notification sentence, corrected from the falsified unconditional to the
      truthful conditional (the correction rider).
- [x] 0.7 `persistence` / "Package composition registers persistence and Core services" —
      carried verbatim; gains the `AutoConfirm` resolution rule and its three scenarios.
- [x] 0.9 `privacy-notice` / "The package states only what it can keep true, and the site links its own policy" — added in QA round 1. Carried verbatim: the policy-link rules, the
      division-by-who-can-know, the say-it-where-sent / say-nothing-where-not pair, the
      condition-governs-both rule, the binds-every-surface clause and the historical note. Added:
      what it promises is messages rather than a confirmation, and that the condition stays a
      single one. One scenario's THEN changes from "the booking will be confirmed to that
      address" to messages-about-the-booking, and a scenario for the approval case is added; no
      other scenario is touched.
- [x] 0.8 `delivery-api` / "Booking placement" and "Service booking placement" — carried
      verbatim; each gains the status-values contract statement and a pending-placement
      scenario, and the success scenarios name the setting they assume.

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

- [x] 6.1 Regenerate the API client; add Confirm/Decline row actions offered **only** for
      `Requested` rows; decline behind the accessible in-page modal, confirm without one;
      refused transitions surfaced, not swallowed; list refreshes in place; last-row paging
      step-back and deliberate focus placement reuse the cancel behaviour where a row leaves
      the current filter.
- [x] 6.2 Localization: new entries following the existing commented style. The decline modal
      states the truthful notification conditional.
- [x] 6.3 **Correction rider:** reword `confirmCancelContent` to the truthful conditional (the
      package writes to the booker only where booking emails are configured) and update its
      explanatory comment — the current sentence is false on email-configured sites.
- [x] 6.4 Client tests: controls offered/withheld by status; modal dismiss issues no request;
      the corrected cancel wording and the decline wording each guarded by a wrap-safe
      absence check on the falsified claim ("does not tell" asserted unconditionally), not
      only a presence check on the correction — presence does not assert absence.

## 7. Front end

- [x] 7.1 Add the placed booking's status to `BookingConfirmationModel` and the service
      confirmation model (additive members; theme contract note updated in
      `UBookItThemeContract` remarks if any).
- [x] 7.2 Branch `Confirmation.cshtml` and `ServiceConfirmation.cshtml`: heading, `aria-label`,
      and lead sentence derive from status; everything else identical in both states.
- [x] 7.3 Tests: both views in both states (the "every branch a view carries can be taken" and
      "a view renders every state its model can express" requirements now bind these views);
      the pending render contains no "confirmed" claim — wrap-safe absence assertion; the
      pending render still shows reference, interval, what was booked, booker details.

## 8. Documentation sweep — enumerate the class, not the sample

- [x] 8.1 `docs/notifications.md`: grep for every sentence the change falsifies before editing —
      known members of the class: "No v1 pathway produces those statuses", the two-row
      notification table, "Approving or declining a booking" under *What does not raise a
      notification*, and any "placed or cancelled" enumeration that is now four events. Then
      grep the whole of `docs/` and all XML doc comments for `Requested`, `Declined`,
      `auto-confirm`, "placed or cancelled", "does not tell", "notifies nobody" and fix every
      hit or record why it stands.
- [x] 8.2 `docs/notifications.md` + `docs/backoffice.md`: document `AutoConfirm` (key, default,
      fallback direction, malformed-value error log), the approval flow, that a pending
      booking holds its slot and is the operator's responsibility (no expiry), and the two
      new notifications with their audiences.
- [x] 8.3 Update `BookingNotifications.cs` / `BookingCancelledNotification` remarks if any now
      over- or under-claim (the "permits cancellation only from Requested or Confirmed"
      sentence stays true — verify rather than assume).
- [x] 8.4 Documentation guards: extend the existing `DocumentationAssert`-based tests — absence
      checks with `DoesNotSay` (never raw `DoesNotContain`) for the falsified claims; one
      assertion per guard, mutation-checked one at a time.

## 9. Sync obligations recorded now (executed at sync/archive)

- [ ] 9.1 Sibling-spec grep at sync: `Confirmed` / `no v1 pathway` / "placed or cancelled" /
      "notifies nobody" across every spec **not** touched by this change's deltas — the
      outward sweep that has fired on four consecutive changes. Known candidates to open:
      `booker-erasure` (erasure vs pending bookings), `booking-retention` (sweep has no status
      filter — should stay true), `service-booking`, `theming` (confirmation view contract),
      `sensitive-data`, and **`privacy-notice`** — added after QA found the CRITICAL there: its
      omission from this list is the reason the outward sweep would not have caught it either.
*(Note for whoever syncs: `booking-management`'s Purpose was ALREADY corrected during apply —
`BackofficeDocumentationTests` demands parity between it and the controller's routes, so adding
the endpoints failed until the Purpose named confirm and decline. That is the guard designed
in ㉘ firing exactly as intended, one change later. 9.2 below still stands for the other two.)*

- [ ] 9.2 Capability Purpose prose check: `bookings` and `booking-emails` Purposes summarize
      auto-confirm-era behaviour — verify each Purpose still matches its requirements as the
      file stands after sync (consistency check, green pre-sync).

## 9a. QA round 1 — REJECT (one CRITICAL, four MAJOR, two MINOR, two NIT)

- [x] 9a.1 **CRITICAL** — the booking form promised a confirmation the package may not send.
      `_PrivacyNotice.cshtml` and `_YourDetails.cshtml` were gated on `SendsBookerEmail` alone
      and said "we will send your booking confirmation"; under `AutoConfirm` off the message is
      "we have received your booking", and a declined booking is never confirmed at all. Both
      now promise **messages about the booking**, true under every combination. The
      `privacy-notice` requirement is MODIFIED by delta, because its own scenario mandated the
      false noun — editing only the strings would have left the spec demanding a lie. The
      proposal's non-goal claiming "the form today promises nothing about confirmation timing"
      was simply wrong and is corrected there.
- [x] 9a.2 **CRITICAL follow-on** — the first absence guard for it was defeated by wrapping.
      Mutation-checked by ADDITION, wrapped across two source lines: it **passed**. Every
      assertion in `PrivacyNoticeTests` now flattens whitespace first, both directions, so one
      test cannot again carry two matchers. Re-mutated: fails. *(That mutation also re-taught
      the stale-build trap — restoring a file with `mv` preserves its mtime, MSBuild skips the
      view, and the run measures the mutant. Revert with a copy and `touch`.)*
- [x] 9a.3 **MAJOR** — `README.md` said "placement auto-confirms", "Nothing is sent by the
      package" and "no search by name, email or reference": three claims falsified by 0.6.0,
      0.5.0 and 0.3.0 respectively, in the file packed into every NuGet package, guarded by
      nothing. Corrected, and the README now states approval and the optional emails.
- [x] 9a.4 **MAJOR** — the falsified-claims sweep enumerated four files by hand. It now
      **discovers** every shipped `.md` (excluding `openspec/`, which is meant to be
      historical), with an anti-vacuity test asserting README is among them, and carries the
      three claims above. Mutation-checked: README + wrapped + by addition → fails.
- [x] 9a.5 **MAJOR** — `BookingsController`'s XML doc still said cancellation was "the second
      and last of v1's management verbs" and that approving and declining were "not here", in
      the class that had just gained both endpoints, shipped in the package's `.xml`.
- [x] 9a.6 **MAJOR** — the `delivery-api` delta's only deliverable was documentation and none
      landed. Both `Status` members now carry XML docs stating that `Requested` is a value a
      successful placement returns and what a consumer must do about it, and
      `docs/notifications.md` addresses headless consumers directly.
- [x] 9a.7 **MINOR** — the three-outcome prompt rule (`proceed` / `abort` / `report-failure`)
      is extracted as `actionFor`, shared by cancel and decline, and tested — including
      extensionally, that no two outcomes collapse. The element could not be tested (no DOM in
      the client suite); the rule now can.
- [x] 9a.8 **MINOR** — added `Declined_bookings_are_reachable_over_http`: "declining is not
      deleting" had no test naming the status that had only just become producible.
- [x] 9a.9 **NIT** — `"   "` added to the AutoConfirm error-log theory, matching its resolution
      theory.
- [x] 9a.10 **NIT** — `docs/mvp.md`'s opening sentence now scopes itself to v1 and names what
      0.5.0 and 0.6.0 widened.

## 10. Verification

- [x] 10.1 Full clean Release build: zero warnings; full test suite green (.NET + client).
- [x] 10.2 Live check on the TestSite (pickup directory, cleared first — or you measure
      history): place with `AutoConfirm` off → form completes, confirmation page reads
      "received/awaiting", internal `.eml` flags awaiting approval, booker `.eml` says
      received; confirm from the backoffice → booker `.eml` says confirmed, no internal
      `.eml`; decline a second booking → declined `.eml`, slot free again on the front end;
      flip the setting off entirely → behaviour identical to today.
- [x] 10.3 Roadmap wording checked (no change needed). **Version deliberately not bumped**:
      `Directory.Build.props` has said 0.1.0 since the packaging change and no roadmap
      milestone since (0.2.0-0.5.0) moved it — nothing is published yet, so the roadmap
      numbers are milestones and the package number moves when Chris publishes. Recorded
      here so the task's original instruction is a corrected assumption, not a skipped step.
