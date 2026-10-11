## Context

See proposal.md for why this change exists. The current shape:

- **`SiteClosure`** holds an id, one `DateOnly`, and a label. `uBookItSiteClosure.Date` is uniquely
  indexed.
- **`AvailabilityConfiguration`** keeps the resource's own exceptions and its applicable closures as
  `IReadOnlyDictionary<DateOnly, …>`, and answers `ClosureFor(date)` / `ExceptionFor(date)` from
  them. The precedence ladder runs in `EffectiveWindows`.
- **`SqlResourceStore`** reads the whole closure list once per call (`ISiteClosureStore.ListAsync`)
  and hands each resource the closures it hasn't opted out of.
- **The resource write path** replaces open hours, exceptions and opt-outs wholesale in one
  transaction, with an app lock per resource config (`AppLock`).
- **`AvailableDateWindow.Compute(today, horizonDays, maxQueryRangeDays)`** is called by
  `ResourceBookingFlow` and `ServiceBookingFlow`, and always starts the window at today.

The maintainer's own site runs the 18 line and needs `18.2.0` live by Tuesday 2026-10-13 evening.
The order is `main`, then `dev/v18`, within that time.

## Goals / Non-Goals

**Goals**
- Ranges for site closures and per-resource closed periods, with no regression for any existing
  single-date closure or exception.
- A cost per availability question that doesn't depend on how long a range is.
- A purely additive schema change and API change.

**Non-goals**
- Ranges for **override** exceptions ("open 10:00–14:00 every day for three weeks"). Those stay
  single-day.
- Opting out of **part** of a site closure. A resource opts out of all of it or none of it.
- Disclosing closures or closed periods publicly, or giving a reason when the booking page's
  window has moved.
- Recurring closures ("every Christmas").

## Decisions

### D1. A closure is `StartDate`..`EndDate`; the existing `Date` becomes the start

`SiteClosure` gains `EndDate` and keeps `Date` as the start, so every existing caller of `Date`
still means "where it begins". `Create(date, label, id, endDate = null)` treats a null `endDate` as
`date`. Ranges are inclusive at both ends, because operators say "1 to 31 March" and mean both.

*Alternative rejected:* renaming `Date` to `StartDate` throughout. That is cleaner, but it touches
every closure call site, the DTO and the generated client for no behavioural gain, and the DTO
field `date` has to stay anyway.

### D2. Ranges are looked up, never expanded into a dictionary

`AvailabilityConfiguration` replaces both closure dictionaries with a short list of ranges, sorted
by start. It answers `ClosureFor(date)` by scanning that list, which a site holds few enough of
for this to be negligible. The resource's own closed periods are held the same way. Single-date
exceptions keep their dictionary.

Expanding a range into one dictionary entry per date would make a ten-year closure cost 3,650
entries per resource per read, which is exactly what *A range costs only the dates a question asks
about* forbids. The scan is O(closures), not O(days covered).

*Alternative rejected:* expanding only within the query's span. That would thread the span through
`AvailabilityConfiguration.Create`, which is built without one (the resource mapper, the read
port), so every construction site would have to know a span it doesn't have.

### D3. Precedence: closure → (exception | closed period) → weekly

The ladder in `EffectiveWindows` gains one rung, at the same level as the resource's own
exception. The two can never collide, because a closed period may not cover an exception's date
(D4). The rung order therefore can't change any answer, and the code checks the exception first
only so the existing branch stays untouched.

### D4. Closed-period rules are judged over the submitted set, in Core

Overlap between closed periods, and a closed period covering an exception's date, are validated in
`AvailabilityConfiguration.Create` (or the resource factory), alongside the existing
duplicate-exception rule. They run over the complete set the write submits. Because the resource
write is a full replacement under the per-resource app lock, nothing else can interleave, so no
storage index is needed.

The new failure codes are `closed-period-invalid`, `closed-period-overlap` and
`closed-period-conflicts-with-exception`, added to `FailureCodes`.

### D5. Site-closure overlap: the unique start index stays, the range check runs under a global lock

The migration adds `EndDate DATE NOT NULL`, filled with `UPDATE … SET EndDate = Date`. It
alters and drops nothing; the `Date` column and its unique index stay and now index the start.

`SqlSiteClosureStore` create and update take an app lock on a single resource name
(`ubookit:site-closures`) for the transaction. Inside it they look for any other closure where
`other.Date <= new.EndDate AND other.EndDate >= new.Date`, excluding the closure's own id on
update. A match fails with `duplicate-closure-date`, the code that already exists for this
meaning. The unique index stays as a backstop for the two-same-start race, which the lock already
prevents.

Holiday import runs the same check per row, inside the same lock, so "already closed" is decided
by coverage, not by date equality.

*Alternative rejected:* a SQL Server exclusion constraint. SQL Server has none for ranges, and a
trigger would put a second implementation of the rule in SQL.

### D6. The management contract is additive, and an omitted `closedPeriods` means unchanged

- `SiteClosureModel` gains `EndDate`. On requests it is nullable, and null means `Date`; responses
  always carry it.
- `ResourceClosureModel` (closures applied to a resource) gains `EndDate`.
- `ResourceRequestModel` gains `List<ClosedPeriodModel>? ClosedPeriods`, and `ResourceResponseModel`
  gains `List<ClosedPeriodModel> ClosedPeriods`. `ClosedPeriodModel` carries `StartDate`, `EndDate`
  and a nullable `Note`.

**Null `ClosedPeriods` on an update leaves the stored periods as they are.** The store does this by
not touching the closed-period rows, not by reading and re-writing them. Validation (D4) still needs
the full set, so the controller loads the existing periods when the request omits them. This
departs from the opt-out and capability precedent (omitted means none) on purpose: those were in
the contract from the start, and this one was not.

**The race this opens, and how it is closed.** Suppose writer A omits periods and adds an exception
on 5 March, while writer B concurrently stores a period covering 1–14 March. A's controller-side
check saw no period, so last-writer-wins could store a contradiction. To close it, **the store
re-checks a submitted exception set against the stored closed periods inside the resource's
transaction and app lock** whenever the collection was omitted. A conflict found there fails with
`closed-period-conflicts-with-exception` and writes nothing.

### D7. The booking page's window start is a pure function over ranges

`AvailableDateWindow.Compute` gains a `firstOpenDate` input, so it stays a pure static. Both flows
compute that date by a new pure helper in Core, `ClosedRanges.FirstUncovered(today, ranges)`. The
helper walks the sorted ranges, following adjoining and overlapping ones, and returns the first
date none of them covers.

- **Resource flow:** the ranges are the resource's applicable closures and its own closed periods.
  The resource is already hydrated with both.
- **Service flow:** the earliest `FirstUncovered` across the candidate pool the flow already loads.

If the result is beyond `today + horizon`, the window starts at today (spec: *Beyond the horizon,
the window does not move*).

The view model that `_AvailableDates.cshtml` renders gains the window's start and end dates, an
additive change to the published theme contract. The legend and both empty-window sentences name
the dates when the start isn't today. They keep today's "in the next N days" wording when it is,
so the existing rendering tests and screenshots stay valid.

### D8. Backoffice

- **Closures view:** a From and a To `uui-input type="date"`, with To defaulting to From when it
  is left empty. Rows render "1 Feb 2027 – 31 Mar 2027", or one date when start equals end.
  A failure surfaces on the input its `field` names: To for an inverted range (`EndDate`), From
  for an overlap (`Date`, the field the overlap code has always carried), Name for the label.
  *(Changed in QA rounds 1 and 2: this first said both surface on To.)*
- **Resource editor:** a **Closed periods** section after Exceptions, with repeatable rows (From,
  To, Note, Remove) following the Exceptions section's own row pattern. It carries a hint that the
  note isn't shown to visitors. Its failures surface on the section, as the Exceptions section's do.
- **Global closures** rows show the range.

These are Lit and `uui-*` only (invariant 3), and they reuse the existing localisation file.

## Risks / Trade-offs

- **[The upcoming filter changes meaning for an in-progress closure]** → The server filters by
  `EndDate >= today`. For every existing closure that is the same as `Date >= today`, because they
  are all single days.
- **[A client that reads only `date` sees a range as one day]** → The only client is the
  backoffice, which is regenerated in this change. A third-party management caller sees an extra
  field, and the `date` field still means where the closure starts.
- **[The global closure lock serialises closure writes]** → Closure writes are rare operator
  actions. Reads take no lock.
- **[Time]** → This is the largest risk to Tuesday. Mitigation: Core and persistence first, with
  tests, so the riskiest logic is proven early; one QA reviewer covering both lines; and the v18
  port by cherry-pick, apart from the migration Designer files and the generated client, which are
  regenerated per line (the `a-ports-risk-lives-in-generated-artifacts` lesson).
- **[The window rule for services]** → It is based on closures only, so a service whose candidates
  are open but fully booked still starts today. That's correct per spec, and it keeps the window
  cheap.

## Migration Plan

- One EF migration per line, `ClosureDateRanges`:
  - add `EndDate` to `uBookItSiteClosure`, filled from `Date`;
  - create `uBookItResourceClosedPeriod` (Id, ResourceId FK cascade, StartDate, EndDate, Note
    nvarchar(512) null), indexed on ResourceId.
- **Rollback:** a site downgrading to `17.2.x` keeps the extra column and table, which the old
  version ignores. A range closure then reads as its start date only, so the downgrade is safe but
  lossy for ranges. That's acceptable for a downgrade, and the changelog states it.
- **Release:** `17.3.0` on `main` first, then the port and `18.2.0` on `dev/v18`. Each gets its own
  release change, run back to back.
