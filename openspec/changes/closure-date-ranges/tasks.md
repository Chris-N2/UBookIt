## 0. Start

- [x] 0.1 Branch `closure-date-ranges` from `origin/main` (at `c63dbe6`), with the upstream unset.
  Verify: the branch exists, and `openspec validate closure-date-ranges --strict` passes.
  *Done:* `closure-date-ranges` from `origin/main` at `c63dbe6`, with the upstream unset. Proposal
  `21a6269`.
- [x] 0.2 Diff the guarantees of every requirement this change replaces wholesale. For each one,
  check every SHALL and scenario in `openspec/specs/` against the delta, and record what was
  carried, superseded or dropped:
  - `site-closures`:
    - "The site closure list"
    - "A closure closes every resource for its date"
    - "A resource opts out of an individual closure by id"
    - "The closures view"
    - "Inheritance is visible where a resource is edited"
    - "Closures are managed through versioned management endpoints"
    - "Closures are not disclosed publicly" (added in QA round 1)
  - `availability`: "Date exceptions"
  - `resource-management`: "Resource CRUD endpoints" and "Workspace editor for a resource"
  - `persistence`: "Site closures and their opt-outs are held in their own additive tables"
  - `public-holidays`: "The preview is a selection, not a confirmation"

  Verify: every original scenario is accounted for here, and `ChangeDeltaIntegrityTests` passes.
  *Done 2026-10-11, at propose time, and re-checked here. Nothing was dropped:*
  - *"Closures are not disclosed publicly" (QA round 1): both scenarios carried; the public read
    model scenario now also names closed periods and notes. The SHALL "indistinguishable from any
    other date" is NARROWED, stated as an exception with its reason: a shifted window reveals that
    the dates before it are closed. Added scenario: a shifted window names dates, never a reason.*
  - *"The site closure list": all 6 scenarios carried.*
    - *"one per date" is superseded by the stronger "no date covered twice", and its scenario is
      kept verbatim.*
    - *"Editing preserves identity" now covers dates (plural).*
    - *Added: range, inverted range, overlap, extension of itself.*
  - *"A closure closes every resource for its date": all 5 scenarios carried.*
    - *"Saving does not absorb" now also says no closed period is written.*
    - *Added: every date in a range; shortening releases dates.*
  - *"Opts out … by id": all 8 carried.*
    - *"Editing a closure's date" now says dates.*
    - *Added: the opt-out covers the whole range; opting out reveals the resource's own closed
      period.*
  - *"The closures view": all 5 carried. Upcoming is now judged by END date, which changes no
    existing closure because all are single days. Added: in progress, From/To, single day.*
  - *"Inheritance is visible": all 5 carried, with "date" → "range".*
  - *"Managed through versioned endpoints": all 6 carried.*
    - *The upcoming-filter scenario says "end date today or later" instead of "future", a
      deliberate supersession for in-progress closures.*
    - *Added: no endDate means a single day, both on create and update; overlap is problem
      details.*
  - *"Date exceptions": all 5 carried. The precedence ladder names closed periods at the
    exception rung.*
  - *"Resource CRUD endpoints": all 11 carried. Added: closed periods round-trip, replace, omitted
    means unchanged, empty clears, applied closures carry their range.*
  - *"Workspace editor": all 9 carried. Added: closed period end to end, its failure, keyboard.*
  - *Persistence "Site closures … additive tables": all 5 carried.*
    - *The first scenario is pinned to the migration that created the tables, since a later
      migration now adds a column.*
    - *"Date uniquely indexed" stays, as the start-date index.*
    - *Added: end-date migration preserves closures; concurrent overlapping ranges.*
  - *"The preview is a selection": all 5 carried.*
    - *"Already closed" now means covered by any closure.*
    - *Added: a date inside a range.*
    - *"Only the selected rows" now says each is one day.*

## 1. Core: ranges and closed periods

- [x] 1.1 `SiteClosure`: add `EndDate`. `Create` takes an optional end that defaults to the date,
  and refuses an end before the start with the new code `closure-range-invalid`. Add a `Covers(date)`
  method. Verify: unit tests for a single day, a range, an inverted range and the defaults.
  *Done:* `SiteClosure.EndDate`, plus `Covers` and `Overlaps`. `Create(date, label, id, endDate)`
  defaults the end to the date and refuses an inverted range with `closure-range-invalid`.
  Covered by `ClosureRangeTests` (single day, range, inverted) and `ClosureRangeEndpointTests`.
- [x] 1.2 A new `ClosedPeriod` (start, end, optional note) with `closed-period-invalid` for an
  inverted range or a note over 512 characters. Add the failure codes `closed-period-overlap` and
  `closed-period-conflicts-with-exception` to `FailureCodes`. Verify: unit tests for each code.
  *Done:* `ClosedPeriod` (trimmed note, blank becomes null, over 512 refused) and the four new
  `FailureCodes`. `ClosureRangeTests` covers each code and pins their spellings.
- [x] 1.3 `AvailabilityConfiguration` (design D2–D4):
  - hold closures and closed periods as sorted range lists, not dictionaries;
  - `ClosureFor` scans the ranges;
  - add `ClosedPeriodFor(date)`;
  - `EffectiveWindows` closes a date inside a closed period at the exception rung;
  - `IsExceptionSuperseded` is unchanged in meaning;
  - `Create` takes closed periods and validates overlap and exception conflicts over the full set.

  Verify: the existing availability and site-closure tests pass unchanged, plus new tests for every
  scenario in the `availability` delta, and a test that a ten-year closure produces no per-day
  allocation in the configuration.
  *Done:*
  - Ranges are held as sorted lists, and `ClosureFor` and `ClosedPeriodFor` scan them.
  - `EffectiveWindows` checks closure, then exception, then closed period, then weekly hours.
  - Overlap is judged with a running furthest end, so a short range swallowed by a long earlier
    one is caught (tested).
  - Exception conflicts are checked by date.

  The existing availability and closure suites pass unchanged. The allocation test measures a
  ten-year closure against a one-day one: no per-date cost.
- [x] 1.4 `ClosedRanges.FirstUncovered(today, ranges)` (design D7). It follows adjoining and
  overlapping ranges, and returns today when today is uncovered. Verify: unit tests for an
  uncovered today, one range, adjoining ranges, overlapping ranges, and a gap of one day.
  *Done:* `ClosedRanges.FirstUncovered` and `ClosedRanges.Of`. Tests cover:
  - an uncovered day;
  - one range;
  - adjoining ranges;
  - overlapping ranges;
  - a one-day gap;
  - a range ending on `DateOnly.MaxValue`.
- [x] 1.5 `Resource` carries closed periods through its factory and rehydration. Verify:
  resource-factory tests round-trip them.

  *Done:* closed periods live in `AvailabilityConfiguration`, which `Resource` already carries,
  so they need no new member on `Resource`. The row mapper rehydrates them through
  `ClosedPeriod.Create`.
## 2. Persistence

- [x] 2.1 Add `SiteClosureRow.EndDate` and `ClosedPeriodRow`, with the DbContext configuration
  (FK cascade on the resource, index on ResourceId, `Note` max 512).
  *Done:*
  - `SiteClosureRow.EndDate` and `ClosedPeriodRow`.
  - The FK cascades from the resource.
  - The `ResourceId` index, and `Note` capped at `ClosedPeriod.MaxNoteLength`.
- [x] 2.2 Generate the `ClosureDateRanges` migration. Hand-check that it only adds: the new column
  filled from `Date` (default plus `UPDATE`), and the new table. No `AlterColumn` or `DropIndex`.
  Verify: read the migration and its Designer; the migration tests or a guard confirm nothing is
  dropped or altered.
  *Done:* `20261011182653_ClosureDateRanges`, generated, plus a hand-added
  `UPDATE … SET [EndDate] = [Date]`. `ClosureDateRangesMigrationShapeTests` asserts:
  - the operation kinds (add, fill, create, index);
  - the one added column;
  - the fill, and that it runs after the column is added;
  - that the index is on the new table only.

  `MultiClaimConcurrencyTests`' migration list records the migration and its reasoning.
- [x] 2.3 `SqlSiteClosureStore` create, update and import, under the `ubookit:site-closures` app
  lock, with the range-overlap check excluding the closure's own id on update. Map overlaps to
  `duplicate-closure-date`. The upcoming list uses `EndDate >= today`. Verify: integration tests
  for an overlap, an extension of the closure itself, concurrent overlapping writes (exactly one
  stored), an in-progress closure counting as upcoming, and an import onto a date inside a range
  reported as already closed.
  *Done:*
  - Create and update take an app lock on `ubookit:site-closures` inside a transaction, then run
    the overlap query, excluding the closure's own id.
  - The upcoming filter is `EndDate >= from`.
  - The import's "already closed" covers every date in the window that any closure covers.

  `ClosureDateRangesStoreTests` covers:
  - round-trip;
  - overlap refused, with the existing closure unchanged;
  - extending a closure is not an overlap with itself;
  - **8 racers with different starts and overlapping ranges: exactly 1 stored**;
  - in progress counts as upcoming;
  - every date in a range is closed on a read.

  **Correction (QA round 1):** the "import onto a date inside a range" test this task promised was
  never written. It now exists as `HolidayPreviewRangeTests`, at the service where "already
  closed" is decided. See 7.2.
- [x] 2.4 The resource stores read closed periods once per call (no N+1) and write them wholesale
  in the resource transaction. They leave the rows untouched when the collection is omitted, and
  then re-check the submitted exceptions against the stored periods inside the lock (design D6).
  Verify: integration tests for each `persistence` delta scenario, including the omitted-collection
  race check and that a resource delete removes its periods.
  *Done:* reads include `ClosedPeriods` on all four read paths. `UpdateKeepingClosedPeriodsAsync`
  is a default interface member, overridden by the SQL store, which:
  - re-judges the exceptions against the stored periods under the lock;
  - leaves the period rows untouched.

  Tests:
  - the row ids are identical after a kept update;
  - a stale writer is refused;
  - a failed update (FK violation after the deletes) leaves the periods unchanged;
  - listing 5 resources issues exactly 1 closed-period query;
  - a resource delete removes its periods.
- [x] 2.5 Migration preservation: an integration test seeds single-date closures and opt-outs on
  the pre-migration schema, migrates, and asserts each `EndDate == Date` and availability is
  unchanged. Verify: the test passes against SQL Server.

  *Done:* `The_end_date_migration_keeps_every_existing_closure_to_its_one_date`. It uses its own
  database, migrated to `AddSiteClosures`, then seeded with raw SQL (two closures and an opt-out),
  then migrated up. Afterwards:
  - both closures have EndDate = Date;
  - the opt-out is intact;
  - Christmas is closed, Boxing Day is open (opted out), and the 27th is unaffected.

  Integration total **207** (194 + 13), all passing.
## 3. Management API and client

- [x] 3.1 DTOs (design D6): `EndDate` on `SiteClosureModel` and `ResourceClosureModel`;
  `ClosedPeriodModel`; nullable `ClosedPeriods` on the request and populated on the response.
  Update the mappers so an omitted request collection loads the stored periods for validation.
  Verify: controller and integration tests for every `site-closures` endpoint scenario and every
  `resource-management` CRUD scenario in the deltas.
  *Done:*
  - The DTOs and mappers.
  - The controller loads the stored periods when the request omits them, and calls
    `UpdateKeepingClosedPeriodsAsync`.
  - `ClosureRangeEndpointTests` has 15 tests covering every endpoint and CRUD scenario in the
    deltas. They use the hydrating double, which now carries closed periods: it had been dropping
    them, which would have hidden exactly this class of fault.
- [x] 3.2 Regenerate the backoffice client against the running TestSite. Verify: the diff is read,
  and it contains only the additive fields and the new model.

  *Done:* regenerated against the TestSite at `localhost:44348`, running this branch.
  `types.gen.ts` gains 11 lines: `ClosedPeriodModel`, the two `endDate` fields, request `endDate?`
  and `closedPeriods?`, and response `closedPeriods`. `client.gen.ts` and `sdk.gen.ts` differed
  only in line endings and were restored.
## 4. Backoffice UI

- [x] 4.1 Closures view: From and To inputs (To defaults to From), range rendering, and failures on
  the right control. Verify: client unit tests for the To default, the range label, the
  single-day label and failure mapping, plus a live keyboard pass.
  *Done:*
  - From and To inputs, with To's hint referenced, To defaulting to From, and `min` set to From.
  - Rows and their action labels use `rangeText`. A single day opens for editing with To empty.
  - The Dates column is renamed.
  - Failures use the view's existing announced alert.

  `closure-ranges.test.ts` covers `rangeEnd` and `rangeText`. `ClosureAccessibilityTests` now pins
  the range-carrying labels and the labelled To field.
- [x] 4.2 Resource editor Closed periods section: rows with From, To, Note and Remove; the
  "not shown to visitors" hint; section-level failure announcement. Global closures rows show
  ranges. Verify: client unit tests for building the request (including that an untouched section
  still sends its periods), plus a live keyboard pass.

  *Done:*
  - The Closed periods group: indexed From, To and Note inputs, each labelled; To and Note hints
    referenced; group-level errors for the four codes plus a client-side "From required".
  - Global closures toggles name the range.
  - The request ALWAYS carries `closedPeriods`, so emptying the section clears them; tested.
  - A new accessibility guard covers the labels and hints.
## 5. Booking page window

- [x] 5.1 `AvailableDateWindow.Compute` takes a first-open date. Both flows compute it with
  `ClosedRanges.FirstUncovered` (a resource's own ranges; the earliest across a service's pool),
  falling back to today beyond the horizon. Verify: unit tests for every `default-frontend` delta
  scenario.
  *Done:*
  - `Compute(today, horizon, maxRange, firstOpenDate)`. The horizon still counts from today, and
    a first open date beyond the horizon is ignored.
  - Both flows compute the window before the selected date, so a visitor who picked no date is
    shown the window's first date, not a closed today.
  - Services use `FirstOpenDate`: the earliest across candidates.

  `ClosedWindowTests`: 5 pure tests and 5 through the flows (resource shifted, adjoining ranges,
  open today, closure past the horizon, service first-reopening).
- [x] 5.2 The view model carries the window's start and end. `_AvailableDates.cshtml` names the
  dates when the start isn't today and keeps the current wording when it is. Verify: rendering
  tests for both wordings, and the existing rendering suite passes unchanged.

  *Done:* `ShiftedWindowFrom` is a default interface member on `IBookingFormView`, so an existing
  implementer still compiles. The end is derived from `WindowDays`: the rendering guard rejected a
  second member that changed nothing on its own. The legend, both empty-window sentences and the
  other-date hint name the dates when shifted, and keep their exact wording otherwise. Rendering
  1168 unchanged.
## 6. Docs

- [x] 6.1 Update `docs/backoffice.md` (closures and closed periods), `docs/booking-page.md` (the
  window rule), and any README sentence describing closures as dates. Verify: the documentation
  guards pass, and a sweep of `docs/`, `README.md` and `openspec/specs` for "closure" sentences
  describing a single date finds none left false.

  *Done:* `docs/backoffice.md` covers:
  - closures as ranges, the overlap rule, and in-progress closures counting as upcoming;
  - opt-outs covering the whole range;
  - a new **Closed periods** section, including that the note is not guaranteed free of personal
    data and is the site's to manage;
  - the booking page looking past a closure, with the advice about the booking horizon;
  - the import's "Already closed" wording.

  `docs/booking-page.md` does not describe the date list, so nothing there was falsified. The
  stored-shape guard records `ClosedPeriodRow` with that same, honest decision about the note.
## 7. Verify on `main`

- [x] 7.1 Clean `CI=true --no-incremental` Release build with 0 warnings, then the four suites per
  project and `openspec validate --all --strict`. Verify: record each total.
  *Done 2026-10-11 on `08a0f3d`:*
  - every `src/*` and `tests/*` `bin/Release` and `obj/Release`, plus `App_Plugins`, deleted; the
    client built; `CI=true --no-incremental`: **0 warnings, 0 errors**;
  - suites, 0 skipped:
    - client **342** (335 + 7, 16 files);
    - unit **2087** (2029 + 58);
    - integration **207** (194 + 13);
    - rendering **1168** (unchanged);
  - openspec **27/27**.
- [x] 7.2 Live pass on the TestSite:
  - create a site closure range over today and see the booking page list start after it;
  - add a resource closed period and see its dates unavailable;
  - opt a resource out of the range;
  - try an overlap and an inverted range and see them refused;
  - run a holiday preview over a range.

  Verify: each result recorded.
  *Done 2026-10-11, about 20:30 BST, on this branch's TestSite (Umbraco 17.7.1). Chris logged in to
  Chrome.*
  - **Migration on a real site's data.** The existing single-day closures, including imported
    holidays, list as single dates. Closure Live Room's Christmas Day opt-out survived.
  - **A range created through the UI**, 2026-10-11 to 2026-12-20, "Live test range":
    - it lists as `2026-10-11 – 2026-12-20` and counts as upcoming while in progress;
    - To's `min` follows From;
    - To's hint is referenced from To.
  - **Refused, each announced (`role="alert"`) and focused:**
    - an overlap: "Another closure ("Live test range") already covers 2026-12-15.";
    - an inverted range: "A closure cannot end (2026-12-01) before it starts (2026-12-10).".
  - **The booking page**, anonymous `curl` of the home page:
    - the legend reads "Dates with availability for 1 hour from Monday 21 December 2026 to
      Tuesday 19 January 2027";
    - the list starts on 21 December, and the 21st is selected by default.

    The 25th is listed because this room opts out of Christmas Day. That was confirmed in its
    editor and is correct.
  - **The resource editor:**
    - Global closures toggles name the range;
    - a closed period of 4–8 January 2027 with a note saved, and reloaded with From, To and the
      note;
    - From, To and Note are labelled, and the note's hint is shown;
    - the booking page then omitted 4–8 January;
    - a period of 24–26 December over the room's own 25 December exception was refused with
      `closed-period-conflicts-with-exception`, shown in the summary and in the group, and the
      fieldset's `aria-describedby` names `err-closed-periods`.
  - **Opting the room out of the range** returned its booking page to "in the next 30 days".
  - **Cleaned up:** the period, the opt-out and the closure were removed. No console errors.
  - *Not done live:* a holiday preview over a range.
  - **Correction (QA round 1):** this bullet first said the case was "covered by
    `HolidayPreviewService`'s window clipping and by the store test for an import onto a date
    inside a range". **That was false.** No such store test existed, and nothing exercised the
    clipping. It is now covered by `HolidayPreviewRangeTests`, through `HolidayPreviewService`:
    - a date inside a range;
    - both ends of a range;
    - a range clipped at the window's start;
    - a ten-year range.

    All four fail with the clipping mutated to compare start dates only.
  - *Observed, not a defect:* setting From and To from a script in the same tick loses the
    first, because each input handler spreads the draft captured at render. A person typing
    cannot produce it: there is a render between the two inputs. The Exceptions group has the
    same shape.

## 8. QA and port

- [x] 8.1 QA (`qa-review`) in one subagent, reused across rounds. Hand each round's fixes over as
  new code. Verify: APPROVE.
  *Round 1: REJECT*, six must-fix findings. QA re-verified every build, suite and migration claim
  by running them, and found the core logic sound. Each fix below was handled as new code.
  - **1. Binary break.** Five published signatures had been widened in place: source-compatible,
    but a `MissingMethodException` for an assembly built against 17.2.x. *Fix:* each signature is
    restored exactly, and the new behaviour lives in new members:
    - `SiteClosure.CreateRange`;
    - an `AvailabilityConfiguration.Create` overload that takes closed periods;
    - a `Compute` overload that takes the first open date;
    - `Build` overloads on both form builders that take the window's start.

    `PublishedSignatureTests` pins all five by exact parameter types. The proposal states it.
  - **2. Sibling spec contradicted.** *Fix:* a MODIFIED delta for *Closures are not disclosed
    publicly*, which states the narrowing; the default-frontend delta now points at it instead of
    contradicting it. `docs/theming.md` documents `ShiftedWindowFrom` and what a theme replacing
    `_AvailableDates` must do.
  - **3. Uncovered scenarios.** *Fix:*
    - `AvailableDatesShiftedWindowRenderingTests` (10 tests) renders, in each of three window
      states, the shifted and unshifted wording: legend, both empty-window sentences, the
      other-date hint, and no disclosure.
    - `ClosedPeriodPlacementTests` (4 tests), through `BookingService.PlaceAsync`: the middle of a
      range, inside a period and the day after, the refusal disclosing nothing, and an existing
      booking surviving and still blocking.
    - `ClosedWindowTests`: a real opt-out through the hydrating store, and a fully booked today.
  - **4. Holiday preview untested, plus a false claim in this record.** *Fix:*
    `HolidayPreviewRangeTests` (4 tests). The 2.3 and 7.2 records are corrected in place, saying
    what was false.
  - **5. Controller seam untested.** *Fix:* the in-memory store overrides
    `UpdateKeepingClosedPeriodsAsync` with the SQL store's semantics and records which path ran.
    `An_omitted_collection_goes_through_the_update_that_keeps_closed_periods` asserts the
    controller's choice both ways. It fails when the controller is mutated to always call
    `UpdateAsync`.
  - **6. Closure errors not associated with their input.** *Fix:* the failure's `field` maps to
    its input (`closureErrorControl`). That input gets `aria-invalid` and an `aria-describedby`
    naming `closure-error` after its hint (`closureDescribedBy`). The client-side "date required"
    marks From. Covered by 4 client tests and an accessibility guard.
  - **7.** *Fix:* the proposal states the `endDate` and `closedPeriods` asymmetry as a trade-off,
    with the rejected alternative and its reason.
  - **8.** *Fix:* the proposal's Impact sentence is corrected. `docs/delivery-api.md` says a
    custom `IResourceStore` must pass closed periods.
  - **Mutations run:** the controller seam; the holiday clipping (start date only); each failed
    as described above.

  *Round 2: REJECT*, one must-fix. QA re-verified every claim and ran the mutations I had not;
  all but R8 were caught.
  - **1. The round-1 fix introduced a source break.** An overload of `Create` named `Create`, whose
    second parameter was an unrelated collection type, made `Create(hours, null)`,
    `Create(hours, [])` and `Create(hours, null, null)` ambiguous (CS0121).
    `PublishedSignatureTests` could not see it: it proves the old signature exists, not that
    callers still bind to it.

    *Fix:* the new members have names of their own: `AvailabilityConfiguration.CreateWithClosedPeriods`
    and `BuildForWindow` on both form builders. The latter also removes QA's lesser ambiguity,
    `Build(..., windowDays, default)`.

    **New guard:** `PublishedCallShapeTests` makes exactly those calls, plus the other published
    shapes and `Build(..., 30, default)`, so an ambiguity fails the build. **Shown to fire:** with
    `CreateWithClosedPeriods` turned back into a `Create` overload, the guard file fails with
    CS0121 on exactly the three shapes QA named. Restored.
  - **2. Mutation R8 survived** (the service flow's selected date defaulting to today). *Fix:* the
    service window test asserts `SelectedDate` and `SelectedDateIsListed`. **Shown to fire:** R8
    reapplied, with the build checked clean first, fails the test. Restored.
  - **3 (nit).** The in-memory store's kept-periods update now re-judges the exceptions and returns
    `closed-period-conflicts-with-exception`, as its comment always claimed.
  - **4 (nit).** Design D8 now says a failure surfaces on the input its `field` names: To for an
    inverted range, From for an overlap.
  - **5 (nit).** The placement test's comment says what it can and cannot tell apart.

  *Round 3: APPROVE on `main` at `d79f5ce`. The port at `aa349e9` is approved on code and suites,
  with its live pass owed (8.2).* QA re-ran every build, suite and mutation (R8 now caught). It
  rebuilt its round-2 probe against the new `UBookIt.Core.dll`: all the published call shapes
  bind, and no same-named overload pair remains. No findings on either line. Three rounds, one
  reviewer.
- [x] 8.2 Port to `dev/v18` on `closure-date-ranges-v18`: cherry-pick, then **regenerate** the
  migration Designer and model snapshot for the 18 line, and the generated client against the v18
  TestSite. Re-run 7.1 and 7.2 there. Verify: records match `main`'s, and the reviewer from 8.1 has
  checked the port.
  *Done 2026-10-11, port branch `closure-date-ranges-v18` built in a separate worktree (`uBookIt-v18`).*
  - **Cherry-picked.** The only conflict was the generated `types.gen.ts`, and v18's own version
    was kept.
  - **Migration not regenerated, on evidence.** `src/UBookIt.Persistence` has no diff between
    `c63dbe6` and `882e6d9`, and `dotnet ef migrations has-pending-model-changes` on v18 reports
    "No changes have been made to the model since the last migration". The migration id is
    therefore the same on both lines.
  - **Client regenerated** against the running v18 TestSite (Umbraco 18.2.1,
    `/umbraco/openapi/ubookitbackoffice.json`) in `aa349e9`. It is additive only, in v18's
    generator style.
  - **7.1 on v18:** clean `CI=true --no-incremental` build with 0 warnings; client 346; unit 2120
    (2041 + 79); integration 207; rendering 1178; openspec 27/27; 0 skipped.
  - **7.2 on v18, live** (TestSite from the worktree, logged in by Chris):
    - a range of 2026-10-11 to 2026-12-20 saved and listed as a range;
    - the overlap was refused, with From marked `aria-invalid` and described by `closure-error`;
    - the inverted range was refused, with To marked and described by
      `closure-end-date-hint closure-error`;
    - both errors were focused;
    - the booking page (DBO Granted) read "from Monday 21 December 2026 to Saturday 9 January
      2027", ending at its 90-day horizon;
    - a one-day closed period (To empty) on 28 December saved, reloaded with To still empty, and
      removed the 28th from the booking page;
    - an exception on that date was refused with `closed-period-conflicts-with-exception`;
    - cleaned up afterwards, leaving no closures.

    Console errors were Umbraco core only: SignalR 1006 and token refresh 400 around restarts and
    login. One finding along the way, not a product fault: QA's clean builds in the shared
    worktree deleted the `App_Plugins` output under the running TestSite, causing a 500 on the
    backoffice until the client was rebuilt.
  - **QA:** reviewed in round 3 by the same reviewer: the content is identical to `main` (5591
    against 5591 changed lines, excluding `types.gen.ts`), and rounds 1–2's fixes hold on v18.
- [ ] 8.3 Chris pushes both branches and merges them by PR. Verify: CI is green on both PRs and on
  both merge commits.
- [ ] 8.4 Archive on both lines after an outward sweep. Then the releases `17.3.0` and `18.2.0`,
  each as its own release change.
