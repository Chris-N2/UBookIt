## 0. Wholesale replacements — diffed guarantee by guarantee at sync

Seven requirements are replaced in full by this change's deltas. Each delta file opens with the
guarantee diff; at sync time every line of it is re-read against `openspec/specs/` as it then
stands, because a MODIFIED requirement deletes whatever it forgets to restate.

- [ ] 0.1 `bookings` — "Availability and placement service ports" and "Placement and status changes are observable": verify each SHALL and scenario in the main spec is carried, superseded or explicitly dropped in `specs/bookings/spec.md`'s diff header
- [ ] 0.2 `booking-emails` — "Which events produce messages, and for whom": same check against `specs/booking-emails/spec.md`
- [ ] 0.3 `email-templates` — "Content is supplied as Razor views at a published path, one per message" and "What a view receives is published, typed, and fit to be frozen": same check against `specs/email-templates/spec.md`
- [ ] 0.4 `permissions` — "Access within the section is decided by four verbs": same check against `specs/permissions/spec.md`
- [ ] 0.5 `persistence` — "Store implementations honour Core semantics": same check against `specs/persistence/spec.md`

## 1. Placement terms — refactor first, prove nothing moved

The pipeline gains an explicit terms value before any move code exists, so the visitor path can be
shown unchanged on its own.

- [x] 1.1 Introduce `PlacementTerms` (lead time, optional horizon) with `Visitor(constraints)` and `Operator` constructors; thread it through `ValidateAgainst` so rules 5 and 6 read the terms and rules 2–4 and 7 still read the resource. Verify: the full existing suite passes unchanged, with no test edited
- [x] 1.2 Test: visitor terms for a resource equal that resource's configured lead time and horizon; operator terms are zero lead and no horizon. Verify: both tests pass, and a mutant that hard-codes zero lead in `ValidateAgainst` fails the visitor test
- [x] 1.3 Test: under operator terms a start one hour ahead on a resource requiring 24 hours passes rule 5, a start in the past fails rule 5 with `lead-time`, and a start 120 days out on a 90-day horizon passes rule 6. Verify: all three pass

## 2. Domain

- [x] 2.1 Add `FailureCodes.IntervalUnchanged = "interval-unchanged"` and `Booking.MoveTo(BookingInterval)`: refuses with `invalid-status-transition` unless Requested or Confirmed, refuses with `interval-unchanged` when equal to the current interval, otherwise sets the interval and nothing else. Verify: unit tests for each status × changed/unchanged, asserting reference, status, booker, service and claims are untouched
- [x] 2.2 Add `IBookingStore.MoveAsync(bookingId, newInterval, permittedFrom, ct)` with remarks on the same terms as `UpdateAsync` (fourth narrow write, own columns, condition in the statement). Verify: the solution compiles with the in-memory double updated (2.3)
- [x] 2.3 Implement the move in `InMemoryBookingStore`: lock, conflict check excluding the booking's own claims, status predicate, interval write. Verify: the *Atomic move contract* scenarios in 2.5 pass against the double
- [x] 2.4 Add `IBookingObserver.BookingMovedAsync(booking, previousInterval, ct)` with no default, and implement it in `NullBookingObserver` and every test recorder. Verify: compiles; `BookingObservationTests` recorder captures the previous interval
- [x] 2.5 Add `IBookingService.MoveAsync(id, newStart, newLength, ct)` per design Decision 2: load, window, per-resource `ValidateAgainst` under operator terms, `MoveTo`, store move, observe. Verify: tests for every scenario of *Moving a booking* against the in-memory store — free interval, requested stays requested, cancelled refused, inside lead time succeeds, past refused with `lead-time`, beyond horizon succeeds, outside open hours, conflict, unchanged, service booking keeps claims, busy resource is not swapped, erased booker moves, unknown id
- [x] 2.6 Tests for *Placement and status changes are observable*: a successful move reports once with the previous interval; every refusal reports nothing; a throwing observer does not break the move. Verify: all pass, and a mutant that reports before the store write fails the "after storage" assertion
- [x] 2.7 Tests for *Atomic move contract* against the in-memory double: move-vs-placement race on the new interval, placement on the old interval only after commit, self-overlap succeeds, cancel between read and write wins, two moves of one booking, stale aggregate leaves status and booker alone. Verify: all pass

## 3. Persistence

- [x] 3.1 Implement `SqlBookingStore.MoveAsync` per design Decision 3: claims read by booking id, app-locks ascending, conflict query with `existing.Id != bookingId`, single `ExecuteUpdateAsync` over `StartUtc`/`EndUtc`/`TimeZoneId` predicated on `permittedFrom.Contains(Status)`, zero rows → `invalid-status-transition`. Verify: the 2.7 scenarios re-run against SQL Server in `ConcurrencyTests` and pass
- [x] 3.2 Concurrency proof: one move to interval I racing at least 10 placements at I on the same resource; exactly one succeeds and exactly one blocking booking holds I afterwards. Verify: passes repeatedly (run 5×)
- [x] 3.3 Test: cancel committed between the move's read and its update statement yields `invalid-status-transition` and the stored row is Cancelled at its original interval. Verify: passes, and a mutant that checks status in a separate query before the update goes red
- [x] 3.4 Test: an erasure between read and write leaves the booker erased with its original instant and the booking moved. Verify: passes
- [x] 3.5 Source-level guard for *Store implementations honour Core semantics*: the SQL move write contains no load-then-save of the booking row and its status predicate is inside the update statement; the three writes touch disjoint columns. Verify: guard passes, and fails against a load-then-save mutant
- [x] 3.6 Confirm no migration is added and the model snapshot is unchanged. Verify: `dotnet ef migrations has-pending-model-changes` (or the project's equivalent check) reports none

## 4. Notifications and emails

- [x] 4.1 Add `BookingMovedNotification(booking, previousInterval)`, map it in `UmbracoBookingObserver`, and add `BookingEvent.Moved` to the handler's subscriptions. Verify: `UmbracoBookingObserverTests` sees the notification with the previous interval
- [x] 4.2 Leave `siteEventApplies` as placement-or-cancellation and extend its comment to name the move with the responsibility wrinkle. Verify: test — a move with a responsibility assignment present and both directions enabled sends to the booker only
- [x] 4.3 Add `BookingMessageKind.BookerMoved` and `BookerMessageModel.PreviousStart`/`PreviousEnd` (nullable, converted to the booking's zone). Verify: `BookingTemplateCompositionTests` — previous interval present for the move and null for the other four events; existing composition tests unchanged
- [x] 4.4 Compose the moved message: subject "Your booking has moved", a line stating the previous time, the details block, the status-derived closing line. Verify: `BookingEmailTests` — the message carries reference, previous and new interval in the booking's zone, and for a Requested booking still states it is not yet confirmed
- [x] 4.5 Tests for the gating: a move on a site without booker emails sends nothing; a move of an erased booker succeeds and sends nothing. Verify: both pass
- [x] 4.6 Confirm the template boot check reports `BookerMoved` as a suppliable view and that a supplied `BookerMoved.cshtml` in the theme fixture is used. Verify: existing boot-check and theme-fixture tests extended and passing

## 5. Management endpoint

- [x] 5.1 Add `POST bookings/{id:guid}/move` under `VerbPolicies.BookingsManage`, taking `{ start (site wall-clock, no offset), lengthMinutes }`, resolving the zone as the window does, returning `MovedBookingModel { bookingId, status, startUtc, endUtc, timeZoneId }`, and mapping failures through `ToProblemResult`. Verify: `BookingsEndpointTests` — success shape, 09:00 site-local becomes 08:00 UTC when the zone is +1, each refusal carries its code, 404 for unknown id distinct from 400 for wrong status, Read-only user gets 403, anonymous gets 401
- [x] 5.2 `BookingsControllerContractTests`: the response carries no resource collection; the request body validates (missing start, non-positive length → 400 before the domain is asked). Verify: passes
- [x] 5.3 Regenerate the client SDK (`scripts/generate-openapi.js`) and confirm `moveBooking` and `MovedBookingModel` appear in `sdk.gen.ts`/`types.gen.ts`. Verify: `npm run build` in the client succeeds

## 6. Backoffice client

- [x] 6.1 Add `canMove(status)` to `booking-rows.ts` (Requested or Confirmed, by name) and a Move button in the actions cell when `_canManage`. Verify: `booking-rows.test.ts` — the four statuses and an unknown status ("Rescheduled") yield no button
- [x] 6.2 Build the move modal element and its `modal` manifest + token per design Decision 6: date, time and length inputs with real `<label for>`, pre-filled from the booking in the site's zone, the conditional-notification sentence, Cancel and Move. Verify: `npm test` — renders pre-filled, labels are associated, submit is disabled while pending
- [x] 6.3 Wire submission to `moveBooking`; on refusal read the problem code and show the mapped term in an element referenced by `aria-describedby` on the inputs, keeping the modal open; on success close with `{ moved: true }`. Verify: tests for `outside-open-hours`, `conflict`, `lead-time`, `interval-unchanged`, `invalid-status-transition` each show their sentence and leave the row unchanged
- [x] 6.4 On success refetch the page through `#applyRowAction`'s path so step-back and focus rules apply, and show a notice naming the new date so an operator whose booking left the window knows where it went. Verify: test — moving the only row on page 2 out of the window lands on page 1 with focus placed deliberately
- [x] 6.5 Add every localisation term to `en-us.ts`, including one per failure code. Verify: the existing term-coverage test (or a new one) confirms every term the element references exists
- [x] 6.6 Keyboard and screen-reader pass in the browser: focus lands in the modal, all controls reachable, error announced with its control, Escape closes and returns focus to the Move button. Verify: recorded in the change's QA handover with what was checked

## 7. Documentation and the falsified-sentence sweep

- [x] 7.1 Retire the eight statements that a booking cannot be moved: README not-yet list, `docs/backoffice.md` (two places), `docs/mvp.md`, `docs/notifications.md`, `openspec/specs/booking-management/spec.md` Purpose paragraph (edited directly, as approval-decline did), `BookingsController.cs` remarks, `bookings-list.element.ts` comment. Verify: `grep -rn "cancellation and a new booking"` over src, docs and README returns nothing
- [x] 7.2 Document the move in `docs/backoffice.md` (what it changes, what it keeps, operator terms, no history, no availability picker, booker-only notification) and add `BookerMoved` to the message table in `docs/notifications.md` with the previous-interval model members. Verify: docs tests in 7.3
- [x] 7.3 Turn `BackofficeDocumentationTests` around: assert the docs say a booking can be moved and name what a move does not do; add the retired sentence to `NotificationDocumentationTests`' falsified-claims sweep. Verify: both pass, and the retired sentence re-inserted anywhere fails the sweep
- [x] 7.4 Write the upgrade note for the two port additions (`IBookingObserver.BookingMovedAsync`, `IBookingStore.MoveAsync`) in the place the settings change's scoped-lifetime note lives. Verify: `VersionTruthTests` still pass (no version bump in this change)

## 8. Verification

- [x] 8.1 `dotnet build -c Release` with zero warnings, `dotnet test` green, `npm test` and `npm run build` green in the client, `openspec validate move-booking --type change --strict` valid. Verify: outputs recorded in the QA handover
- [x] 8.2 Live check on the TestSite: move a Confirmed booking forward a day from the bookings list, see the row update, receive (or see logged) the booker message with both times, then move it to a taken slot and see the in-modal refusal. Verify: recorded in the QA handover with the reference used
- [x] 8.3 Sibling-spec sweep at sync time: grep `openspec/specs/` for "four events", "four members", "exactly four", "cancellation and a new booking", "no report", "describe what changed" and reconcile each hit. Verify: list of hits and dispositions recorded in the handover

## 9. QA handover — what is claimed, and what is NOT

**Branch `move-booking`, seven commits on top of `main` `2b6e932`.** Treat every round's fixes as
new code. Verify each claim below rather than trusting it; this project has twice found a
handover claim false.

### Build and test state at handover (verify)

| What | Claim |
|---|---|
| `dotnet build -c Release` | 0 warnings, 0 errors (after fixing one xUnit2031 in `MoveBookingTests`) |
| `dotnet test` (Debug) | unit 1634 / integration 149 (LocalDB) / rendering 1086 — all green |
| `npm test` (Client) | 192 green, 11 files |
| `npm run build` (Client) | clean; bundle rebuilt into `wwwroot/App_Plugins/UBookItBackoffice` |
| `openspec validate move-booking --type change --strict` | valid, under CLI **1.13.0** (upgraded this session) |
| `openspec validate --all --strict` | 22/22 |
| `dotnet ef migrations has-pending-model-changes` | none — no migration, no snapshot change |

### Mutation results (verify by re-running if in doubt)

- **Load-then-save mutant** in `SqlBookingStore.MoveAsync` (read the row, decide, `SaveChangesAsync`):
  `MoveWriteShapeTests` 2 of 3 red, `MoveStorageTests.The_move_write_is_one_update_whose_predicate_is_the_status` red.
  `MoveStorageTests.A_cancel_committed_between_the_read_and_the_write_wins` stayed GREEN under that
  mutant — expected and worth knowing: it stages the cancel between the *service's* read and the
  store's write, and the mutant re-reads inside the store after that. The wire-shape guard is what
  covers the store-internal window; the interleaving test covers the service-level one.
- The five in-memory "between read and write" scenarios first FAILED against the real code, and the
  failure was a genuine design flaw: the service applied `MoveTo` to the aggregate *before* the store
  agreed, which the in-memory double's instance sharing exposed. Fixed by splitting `CanMoveTo`
  (check) from `MoveTo` (apply), applied only after the store write. Design.md Decision 2 was
  amended in code, not in the artifact — **the artifact still describes the pre-fix order** (step 4
  "calls MoveTo" before step 5 "calls the store"). Flagging rather than silently editing design.md.

### Live check (TestSite, Debug, LocalDB, booker emails NOT configured)

Done in the real backoffice via Chrome, operator logged in by Chris:

- Bookings list shows **Move** on every Requested/Confirmed row, beside Cancel.
- Opened Move on `QRX8-WFBD` (Sep 14 09:00–10:00, in the past): dialog pre-filled 14/09/2026, 09:00,
  60. Submitted unchanged → in-dialog refusal "That time has already passed" (lead-time wins over
  interval-unchanged, as the pipeline orders it). Set 17/09 10:00 → "outside opening hours" (the
  resource is open on Mondays only). Set 21/09 10:00 → **moved**; dialog closed; notice above the
  table "Booking QRX8-WFBD moved to Sep 21, 2026, 10:00 AM–11:00 AM (Europe/London)."; row left the
  14–20 Sep window; focus on the `<h2>`. Site log shows no errors.
- Opened Move on `CZWS-T72T`, set 21/09 10:00 → **conflict** refusal in place ("Something else is
  booked at that time"), `aria-invalid="true"` on all three inputs, `aria-describedby` grew to
  `ubookit-move-hint ubookit-move-refusal`, refusal is `role="alert"`, all three ids resolve in the
  modal's shadow root, every input has a real `<label for>`.
- **Focus, measured:** on open → `INPUT#ubookit-move-date`; after refusal → the same input; Escape →
  `UUI-BUTTON label="Move booking CZWS-T72T"`. The first bundle left focus on `<body>` in all three
  cases; the fix is the last commit (`22ca055`) and was re-verified live.
- **NOT verified live:** the `BookerMoved` email (no SMTP/pickup on the TestSite) — covered by
  `BookingEmailTests` and `BookingTemplateCompositionTests` only. A service (multi-claim) move — the
  TestSite has no service booking in the window; covered by `MoveBookingTests` and
  `MoveStorageTests.A_move_of_a_multi_claim_booking_locks_and_checks_every_resource`.
- Residue left for the reviewer: `QRX8-WFBD` now sits at Sep 21 10:00–11:00 on resource "DBO
  Granted"; the TestSite is still running on :44348.

### Sibling-spec sweep (task 8.3), done before sync

`grep` over `openspec/specs/` for "four events|four members|exactly four|cancellation and a new
booking|no report|describe what changed|four booking events|six messages": three hits.
`booking-emails:351` ("Four booking events") and `bookings:323` ("No status-change report SHALL
need to describe what changed") are inside requirements this change's deltas replace wholesale —
resolved at sync. `booking-emails:263` ("No report of a sending failure") is unrelated. The
`booking-management` Purpose paragraph was edited directly (Purpose is not a delta operation) on the
approval-decline precedent; `BackofficeDocumentationTests.The_capabilitys_own_summary_names_every_verb_the_capability_has`
now requires the word "move" there.

### Decisions made during apply that are not in the artifacts

1. `CanMoveTo` / `MoveTo` split (above).
2. `IBookingService.CheckPlacementRules(resource, start, duration, terms)` is `internal` — the public
   member is the visitor's; operator terms are reachable only through an operator operation.
3. The composer's `ForBookerAsync` gained an optional `previousInterval` parameter rather than a
   third method, so the "exactly two message-producing methods" guard still holds; it throws if
   asked for a move message without one.
4. `BookerMessageModel.PreviousLocalStart/End` (not `PreviousStart/End` as tasks.md said) — named to
   match `LocalStart/LocalEnd`.
5. The endpoint validates shape (default start, non-positive length) itself with `interval-invalid`
   against the field name, before the domain; a start inside a spring-forward gap is refused the same
   way; a fall-back overlap takes the first occurrence (`BookingWindow.ResolveSiteLocal`).
6. Three test guards had to be told about the new endpoint by hand: `PermissionsTests` classification,
   `SensitiveDataRedactionTests` KnownWrites + recorded snapshot, and the capability-summary route map.

## 10. QA round 2 — what changed since round 1 (treat as NEW code)

Round 1 verdict: REJECT (1 CRITICAL, 1 MAJOR, 3 MINOR, 2 NIT). Every finding acted on:

- **CRITICAL, composer signature.** `ForBookerAsync(Booking, BookingEvent, CancellationToken)` is
  back exactly as frozen at 17.0.0 and delegates to a new overload
  `ForBookerAsync(Booking, BookingEvent, BookingInterval? previousInterval, CancellationToken)`.
  The frozen shape throws `ArgumentException` for a move. The "exactly two methods" guard now
  counts DISTINCT names, with the reason stated; a new test asserts the frozen overload exists by
  exact parameter types and refuses a move. `BookingEmailTests:651` reverted to the positional call.
  No other public signature changed; the proposal and `docs/configuration.md` say so.
- **MAJOR, service length rule.** `IServiceBookingService.MoveAsync` is new (declared BREAKING,
  third port). For a booking placed for a service it computes the highest floor / lowest ceiling of
  `service.Duration.TryResolveAgainst(each claimed resource's constraints)` and refuses with
  `duration-too-short` / `duration-too-long`; an empty intersection refuses `service-unavailable`;
  a deleted service or a direct booking delegates straight to `IBookingService.MoveAsync`. The
  endpoint now calls the service booking service, and every move endpoint test wires it while the
  booking service stays the refusing double — so a regression to the short path fails loudly.
  New delta `specs/service-booking/spec.md` (ADDED requirement, five scenarios); `bookings` delta
  states the split; `booking-management` delta gains the scenario; proposal's "Deliberately
  unmodified: service-booking" removed and the capability listed as modified. Six new unit tests
  in `MoveBookingTests` (bounded service → too short; resource ceiling → too long; permitted
  length moves; deleted service no longer binds; direct booking delegated untouched; unknown id).
  **Not re-verified live** — the TestSite's `M97C-G9JJ` case from round 1 is the reviewer's to
  repeat; the endpoint now routes through the new path.
- **MINOR, offset-bearing start.** `Start.Kind != Unspecified` is refused with `interval-invalid`
  against `Start` before the domain is asked; theory test for Utc and Local kinds; scenario added.
- **MINOR, tasks 6.2–6.4 verify claims.** Disclosed here: there are NO element-rendering tests —
  the client suite has no DOM environment (a recorded deferred obligation). `move-fields.test.ts`
  and `booking-rows.test.ts` cover the pure functions only. The scenario *"Moving the last row of a
  page out of the window does not strand the operator"* has no instrument: not a test, and the live
  check moved a row off a 6-row page. The step-back logic is the shared `#settleAfterRowAction`,
  which cancel's existing coverage exercises, but that is an argument, not a measurement.
- **MINOR, design.md stale.** Decision 2 rewritten to the `CanMoveTo`-then-store-then-`MoveTo`
  order with the amendment recorded; Decision 6 states the modal's real value shape and the focus
  management; the write-count rule settled on "third narrow write over an existing row" in code
  comments, design and the persistence delta header alike.
- **NIT, "before any store access".** Reworded to "before any store write" in the `bookings` delta
  (SHALL and scenario).
- **NIT, aria-invalid.** Set only when the refusal concerns the fields (`refusalConcernsFields`
  in `move-fields.ts`, tested); status/not-found/generic refusals keep `aria-describedby` but do not
  mark the inputs invalid.

Build/test state after round 2 is recorded in the commit message of the round-2 commit; verify it.
