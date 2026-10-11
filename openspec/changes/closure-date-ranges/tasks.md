## 0. Start

- [ ] 0.1 Branch `closure-date-ranges` from `origin/main` (at `c63dbe6`), with the upstream unset.
  Verify: the branch exists, and `openspec validate closure-date-ranges --strict` passes.

## 1. Core: ranges and closed periods

- [ ] 1.1 `SiteClosure`: add `EndDate`. `Create` takes an optional end that defaults to the date,
  and refuses an end before the start with the new code `closure-range-invalid`. Add a `Covers(date)`
  method. Verify: unit tests for a single day, a range, an inverted range and the defaults.
- [ ] 1.2 A new `ClosedPeriod` (start, end, optional note) with `closed-period-invalid` for an
  inverted range or a note over 512 characters. Add the failure codes `closed-period-overlap` and
  `closed-period-conflicts-with-exception` to `FailureCodes`. Verify: unit tests for each code.
- [ ] 1.3 `AvailabilityConfiguration` (design D2–D4):
  - hold closures and closed periods as sorted range lists, not dictionaries;
  - `ClosureFor` scans the ranges;
  - add `ClosedPeriodFor(date)`;
  - `EffectiveWindows` closes a date inside a closed period at the exception rung;
  - `IsExceptionSuperseded` is unchanged in meaning;
  - `Create` takes closed periods and validates overlap and exception conflicts over the full set.

  Verify: the existing availability and site-closure tests pass unchanged, plus new tests for every
  scenario in the `availability` delta, and a test that a ten-year closure produces no per-day
  allocation in the configuration.
- [ ] 1.4 `ClosedRanges.FirstUncovered(today, ranges)` (design D7). It follows adjoining and
  overlapping ranges, and returns today when today is uncovered. Verify: unit tests for an
  uncovered today, one range, adjoining ranges, overlapping ranges, and a gap of one day.
- [ ] 1.5 `Resource` carries closed periods through its factory and rehydration. Verify:
  resource-factory tests round-trip them.

## 2. Persistence

- [ ] 2.1 Add `SiteClosureRow.EndDate` and `ClosedPeriodRow`, with the DbContext configuration
  (FK cascade on the resource, index on ResourceId, `Note` max 512).
- [ ] 2.2 Generate the `ClosureDateRanges` migration. Hand-check that it only adds: the new column
  filled from `Date` (default plus `UPDATE`), and the new table. No `AlterColumn` or `DropIndex`.
  Verify: read the migration and its Designer; the migration tests or a guard confirm nothing is
  dropped or altered.
- [ ] 2.3 `SqlSiteClosureStore` create, update and import, under the `ubookit:site-closures` app
  lock, with the range-overlap check excluding the closure's own id on update. Map overlaps to
  `duplicate-closure-date`. The upcoming list uses `EndDate >= today`. Verify: integration tests
  for an overlap, an extension of the closure itself, concurrent overlapping writes (exactly one
  stored), an in-progress closure counting as upcoming, and an import onto a date inside a range
  reported as already closed.
- [ ] 2.4 The resource stores read closed periods once per call (no N+1) and write them wholesale
  in the resource transaction. They leave the rows untouched when the collection is omitted, and
  then re-check the submitted exceptions against the stored periods inside the lock (design D6).
  Verify: integration tests for each `persistence` delta scenario, including the omitted-collection
  race check and that a resource delete removes its periods.
- [ ] 2.5 Migration preservation: an integration test seeds single-date closures and opt-outs on
  the pre-migration schema, migrates, and asserts each `EndDate == Date` and availability is
  unchanged. Verify: the test passes against SQL Server.

## 3. Management API and client

- [ ] 3.1 DTOs (design D6): `EndDate` on `SiteClosureModel` and `ResourceClosureModel`;
  `ClosedPeriodModel`; nullable `ClosedPeriods` on the request and populated on the response.
  Update the mappers so an omitted request collection loads the stored periods for validation.
  Verify: controller and integration tests for every `site-closures` endpoint scenario and every
  `resource-management` CRUD scenario in the deltas.
- [ ] 3.2 Regenerate the backoffice client against the running TestSite. Verify: the diff is read,
  and it contains only the additive fields and the new model.

## 4. Backoffice UI

- [ ] 4.1 Closures view: From and To inputs (To defaults to From), range rendering, and failures on
  the right control. Verify: client unit tests for the To default, the range label, the
  single-day label and failure mapping, plus a live keyboard pass.
- [ ] 4.2 Resource editor Closed periods section: rows with From, To, Note and Remove; the
  "not shown to visitors" hint; section-level failure announcement. Global closures rows show
  ranges. Verify: client unit tests for building the request (including that an untouched section
  still sends its periods), plus a live keyboard pass.

## 5. Booking page window

- [ ] 5.1 `AvailableDateWindow.Compute` takes a first-open date. Both flows compute it with
  `ClosedRanges.FirstUncovered` (a resource's own ranges; the earliest across a service's pool),
  falling back to today beyond the horizon. Verify: unit tests for every `default-frontend` delta
  scenario.
- [ ] 5.2 The view model carries the window's start and end. `_AvailableDates.cshtml` names the
  dates when the start isn't today and keeps the current wording when it is. Verify: rendering
  tests for both wordings, and the existing rendering suite passes unchanged.

## 6. Docs

- [ ] 6.1 Update `docs/backoffice.md` (closures and closed periods), `docs/booking-page.md` (the
  window rule), and any README sentence describing closures as dates. Verify: the documentation
  guards pass, and a sweep of `docs/`, `README.md` and `openspec/specs` for "closure" sentences
  describing a single date finds none left false.

## 7. Verify on `main`

- [ ] 7.1 Clean `CI=true --no-incremental` Release build with 0 warnings, then the four suites per
  project and `openspec validate --all --strict`. Verify: record each total.
- [ ] 7.2 Live pass on the TestSite:
  - create a site closure range over today and see the booking page list start after it;
  - add a resource closed period and see its dates unavailable;
  - opt a resource out of the range;
  - try an overlap and an inverted range and see them refused;
  - run a holiday preview over a range.

  Verify: each result recorded.

## 8. QA and port

- [ ] 8.1 QA (`qa-review`) in one subagent, reused across rounds. Hand each round's fixes over as
  new code. Verify: APPROVE.
- [ ] 8.2 Port to `dev/v18` on `closure-date-ranges-v18`: cherry-pick, then **regenerate** the
  migration Designer and model snapshot for the 18 line, and the generated client against the v18
  TestSite. Re-run 7.1 and 7.2 there. Verify: records match `main`'s, and the reviewer from 8.1 has
  checked the port.
- [ ] 8.3 Chris pushes both branches and merges them by PR. Verify: CI is green on both PRs and on
  both merge commits.
- [ ] 8.4 Archive on both lines after an outward sweep. Then the releases `17.3.0` and `18.2.0`,
  each as its own release change.
