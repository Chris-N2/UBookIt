## 1. Placement terms — refactor first, prove nothing moved

The pipeline gains an explicit terms value before any move code exists, so the visitor path can be
shown unchanged on its own.

- [ ] 1.1 Introduce `PlacementTerms` (lead time, optional horizon) with `Visitor(constraints)` and `Operator` constructors; thread it through `ValidateAgainst` so rules 5 and 6 read the terms and rules 2–4 and 7 still read the resource. Verify: the full existing suite passes unchanged, with no test edited
- [ ] 1.2 Test: visitor terms for a resource equal that resource's configured lead time and horizon; operator terms are zero lead and no horizon. Verify: both tests pass, and a mutant that hard-codes zero lead in `ValidateAgainst` fails the visitor test
- [ ] 1.3 Test: under operator terms a start one hour ahead on a resource requiring 24 hours passes rule 5, a start in the past fails rule 5 with `lead-time`, and a start 120 days out on a 90-day horizon passes rule 6. Verify: all three pass

## 2. Domain

- [ ] 2.1 Add `FailureCodes.IntervalUnchanged = "interval-unchanged"` and `Booking.MoveTo(BookingInterval)`: refuses with `invalid-status-transition` unless Requested or Confirmed, refuses with `interval-unchanged` when equal to the current interval, otherwise sets the interval and nothing else. Verify: unit tests for each status × changed/unchanged, asserting reference, status, booker, service and claims are untouched
- [ ] 2.2 Add `IBookingStore.MoveAsync(bookingId, newInterval, permittedFrom, ct)` with remarks on the same terms as `UpdateAsync` (fourth narrow write, own columns, condition in the statement). Verify: the solution compiles with the in-memory double updated (2.3)
- [ ] 2.3 Implement the move in `InMemoryBookingStore`: lock, conflict check excluding the booking's own claims, status predicate, interval write. Verify: the *Atomic move contract* scenarios in 2.5 pass against the double
- [ ] 2.4 Add `IBookingObserver.BookingMovedAsync(booking, previousInterval, ct)` with no default, and implement it in `NullBookingObserver` and every test recorder. Verify: compiles; `BookingObservationTests` recorder captures the previous interval
- [ ] 2.5 Add `IBookingService.MoveAsync(id, newStart, newLength, ct)` per design Decision 2: load, window, per-resource `ValidateAgainst` under operator terms, `MoveTo`, store move, observe. Verify: tests for every scenario of *Moving a booking* against the in-memory store — free interval, requested stays requested, cancelled refused, inside lead time succeeds, past refused with `lead-time`, beyond horizon succeeds, outside open hours, conflict, unchanged, service booking keeps claims, busy resource is not swapped, erased booker moves, unknown id
- [ ] 2.6 Tests for *Placement and status changes are observable*: a successful move reports once with the previous interval; every refusal reports nothing; a throwing observer does not break the move. Verify: all pass, and a mutant that reports before the store write fails the "after storage" assertion
- [ ] 2.7 Tests for *Atomic move contract* against the in-memory double: move-vs-placement race on the new interval, placement on the old interval only after commit, self-overlap succeeds, cancel between read and write wins, two moves of one booking, stale aggregate leaves status and booker alone. Verify: all pass

## 3. Persistence

- [ ] 3.1 Implement `SqlBookingStore.MoveAsync` per design Decision 3: claims read by booking id, app-locks ascending, conflict query with `existing.Id != bookingId`, single `ExecuteUpdateAsync` over `StartUtc`/`EndUtc`/`TimeZoneId` predicated on `permittedFrom.Contains(Status)`, zero rows → `invalid-status-transition`. Verify: the 2.7 scenarios re-run against SQL Server in `ConcurrencyTests` and pass
- [ ] 3.2 Concurrency proof: one move to interval I racing at least 10 placements at I on the same resource; exactly one succeeds and exactly one blocking booking holds I afterwards. Verify: passes repeatedly (run 5×)
- [ ] 3.3 Test: cancel committed between the move's read and its update statement yields `invalid-status-transition` and the stored row is Cancelled at its original interval. Verify: passes, and a mutant that checks status in a separate query before the update goes red
- [ ] 3.4 Test: an erasure between read and write leaves the booker erased with its original instant and the booking moved. Verify: passes
- [ ] 3.5 Source-level guard for *Store implementations honour Core semantics*: the SQL move write contains no load-then-save of the booking row and its status predicate is inside the update statement; the three writes touch disjoint columns. Verify: guard passes, and fails against a load-then-save mutant
- [ ] 3.6 Confirm no migration is added and the model snapshot is unchanged. Verify: `dotnet ef migrations has-pending-model-changes` (or the project's equivalent check) reports none

## 4. Notifications and emails

- [ ] 4.1 Add `BookingMovedNotification(booking, previousInterval)`, map it in `UmbracoBookingObserver`, and add `BookingEvent.Moved` to the handler's subscriptions. Verify: `UmbracoBookingObserverTests` sees the notification with the previous interval
- [ ] 4.2 Leave `siteEventApplies` as placement-or-cancellation and extend its comment to name the move with the responsibility wrinkle. Verify: test — a move with a responsibility assignment present and both directions enabled sends to the booker only
- [ ] 4.3 Add `BookingMessageKind.BookerMoved` and `BookerMessageModel.PreviousStart`/`PreviousEnd` (nullable, converted to the booking's zone). Verify: `BookingTemplateCompositionTests` — previous interval present for the move and null for the other four events; existing composition tests unchanged
- [ ] 4.4 Compose the moved message: subject "Your booking has moved", a line stating the previous time, the details block, the status-derived closing line. Verify: `BookingEmailTests` — the message carries reference, previous and new interval in the booking's zone, and for a Requested booking still states it is not yet confirmed
- [ ] 4.5 Tests for the gating: a move on a site without booker emails sends nothing; a move of an erased booker succeeds and sends nothing. Verify: both pass
- [ ] 4.6 Confirm the template boot check reports `BookerMoved` as a suppliable view and that a supplied `BookerMoved.cshtml` in the theme fixture is used. Verify: existing boot-check and theme-fixture tests extended and passing

## 5. Management endpoint

- [ ] 5.1 Add `POST bookings/{id:guid}/move` under `VerbPolicies.BookingsManage`, taking `{ start (site wall-clock, no offset), lengthMinutes }`, resolving the zone as the window does, returning `MovedBookingModel { bookingId, status, startUtc, endUtc, timeZoneId }`, and mapping failures through `ToProblemResult`. Verify: `BookingsEndpointTests` — success shape, 09:00 site-local becomes 08:00 UTC when the zone is +1, each refusal carries its code, 404 for unknown id distinct from 400 for wrong status, Read-only user gets 403, anonymous gets 401
- [ ] 5.2 `BookingsControllerContractTests`: the response carries no resource collection; the request body validates (missing start, non-positive length → 400 before the domain is asked). Verify: passes
- [ ] 5.3 Regenerate the client SDK (`scripts/generate-openapi.js`) and confirm `moveBooking` and `MovedBookingModel` appear in `sdk.gen.ts`/`types.gen.ts`. Verify: `npm run build` in the client succeeds

## 6. Backoffice client

- [ ] 6.1 Add `canMove(status)` to `booking-rows.ts` (Requested or Confirmed, by name) and a Move button in the actions cell when `_canManage`. Verify: `booking-rows.test.ts` — the four statuses and an unknown status ("Rescheduled") yield no button
- [ ] 6.2 Build the move modal element and its `modal` manifest + token per design Decision 6: date, time and length inputs with real `<label for>`, pre-filled from the booking in the site's zone, the conditional-notification sentence, Cancel and Move. Verify: `npm test` — renders pre-filled, labels are associated, submit is disabled while pending
- [ ] 6.3 Wire submission to `moveBooking`; on refusal read the problem code and show the mapped term in an element referenced by `aria-describedby` on the inputs, keeping the modal open; on success close with `{ moved: true }`. Verify: tests for `outside-open-hours`, `conflict`, `lead-time`, `interval-unchanged`, `invalid-status-transition` each show their sentence and leave the row unchanged
- [ ] 6.4 On success refetch the page through `#applyRowAction`'s path so step-back and focus rules apply, and show a notice naming the new date so an operator whose booking left the window knows where it went. Verify: test — moving the only row on page 2 out of the window lands on page 1 with focus placed deliberately
- [ ] 6.5 Add every localisation term to `en-us.ts`, including one per failure code. Verify: the existing term-coverage test (or a new one) confirms every term the element references exists
- [ ] 6.6 Keyboard and screen-reader pass in the browser: focus lands in the modal, all controls reachable, error announced with its control, Escape closes and returns focus to the Move button. Verify: recorded in the change's QA handover with what was checked

## 7. Documentation and the falsified-sentence sweep

- [ ] 7.1 Retire the eight statements that a booking cannot be moved: README not-yet list, `docs/backoffice.md` (two places), `docs/mvp.md`, `docs/notifications.md`, `openspec/specs/booking-management/spec.md` Purpose paragraph (edited directly, as approval-decline did), `BookingsController.cs` remarks, `bookings-list.element.ts` comment. Verify: `grep -rn "cancellation and a new booking"` over src, docs and README returns nothing
- [ ] 7.2 Document the move in `docs/backoffice.md` (what it changes, what it keeps, operator terms, no history, no availability picker, booker-only notification) and add `BookerMoved` to the message table in `docs/notifications.md` with the previous-interval model members. Verify: docs tests in 7.3
- [ ] 7.3 Turn `BackofficeDocumentationTests` around: assert the docs say a booking can be moved and name what a move does not do; add the retired sentence to `NotificationDocumentationTests`' falsified-claims sweep. Verify: both pass, and the retired sentence re-inserted anywhere fails the sweep
- [ ] 7.4 Write the upgrade note for the two port additions (`IBookingObserver.BookingMovedAsync`, `IBookingStore.MoveAsync`) in the place the settings change's scoped-lifetime note lives. Verify: `VersionTruthTests` still pass (no version bump in this change)

## 8. Verification

- [ ] 8.1 `dotnet build -c Release` with zero warnings, `dotnet test` green, `npm test` and `npm run build` green in the client, `openspec validate move-booking --type change --strict` valid. Verify: outputs recorded in the QA handover
- [ ] 8.2 Live check on the TestSite: move a Confirmed booking forward a day from the bookings list, see the row update, receive (or see logged) the booker message with both times, then move it to a taken slot and see the in-modal refusal. Verify: recorded in the QA handover with the reference used
- [ ] 8.3 Sibling-spec sweep at sync time: grep `openspec/specs/` for "four events", "four members", "exactly four", "cancellation and a new booking", "no report", "describe what changed" and reconcile each hit. Verify: list of hits and dispositions recorded in the handover
