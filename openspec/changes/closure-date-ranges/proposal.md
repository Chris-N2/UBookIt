## Why

A closure today is one date. Closing for longer means one record per day, on the Closures screen
for the whole organisation or in each resource's exception list for one resource:

- "we're refurbishing the gym from February to March";
- "Room A is being painted for a fortnight";
- "Tom has broken his leg and can't work for six weeks";
- the maintainer's own case: "take bookings now for consultancy that starts in January".

A three-month closure is about ninety records, and each has to be read, changed and deleted one at
a time. A resource that should stay open needs ninety opt-outs. That isn't practical, and it blocks
the maintainer's own site going live this week.

## What Changes

- **A site closure becomes a date range**, from a start date to an end date inclusive. A closure
  of one day is a range whose start and end are the same date, so every existing closure keeps
  its meaning unchanged.
  - **Overlapping closures are refused.** To close for longer, extend the existing closure. The
    existing code `duplicate-closure-date` keeps its meaning, "another closure already covers a
    date in this range", so a client that handles it today goes on handling it. An end before the
    start is refused with a new code, `closure-range-invalid`.
  - **A resource's opt-out still names the closure's id**, so one opt-out now covers the whole
    range.
  - The Closures screen takes **From** and **To** dates. The upcoming filter includes a closure
    that is in progress today.
- **A resource gains closed periods**, edited in the resource editor. A closed period runs from a
  start date to an end date inclusive, with an optional note shown only in the backoffice.
  - A closed period only closes the resource. Different hours for a day stay with the single-day
    exception.
  - A closed period may not overlap another of the resource's closed periods
    (`closed-period-overlap`), or a single-day exception the resource carries
    (`closed-period-conflicts-with-exception`). The editor resolves the contradiction rather than
    the package choosing silently. An end before the start, or an over-long note, is refused with
    `closed-period-invalid`.
  - In the order of precedence a closed period sits with the resource's own exceptions: a site
    closure still comes first, and a site closure that the resource opts out of falls through to
    the closed period.
- **Availability treats every date a range covers as closed**, by every route that reads
  availability or validates placement. Expanding a range costs nothing outside the dates a
  question asks about, so a closure lasting years costs nothing extra.
- **The booking page's date list starts after a closed period that covers today.** Today, a
  visitor during a closure is told nothing in the next 30 days is available. The list now starts
  on the first day after the closure.
  - Only closures and closed periods move the start. Moving it is a cheap check over ranges, not
    a search for availability.
  - The list's wording names the dates it spans, and never says why.
- **The management API changes additively.**
  - A closure body takes an optional `endDate` (absent means a single day), and responses always
    carry it.
  - A resource body takes an optional `closedPeriods` list. **Absent means "leave the resource's
    closed periods unchanged"**, so a caller written before this version cannot erase periods it
    has never heard of.
  - **The two absences are treated differently, and that is a trade-off, not an oversight** (QA
    round 1). An absent `endDate` on a closure UPDATE means one day. So a caller written before
    ranges, that renames a range closure an operator created in the backoffice, shrinks it to its
    start date. The alternative was to read an absent `endDate` as "keep the stored end". It was
    rejected because it would make one body mean different things on create and update, and a
    pre-range caller creating a closure must keep getting exactly one day. The hazard is narrow:
    such a caller has to edit a closure it did not create. The changelog states it.
- **Holiday import:** a holiday on a date that any closure covers is reported as already closed.
- **Release:** a minor, `17.3.0` on `main` and then `18.2.0` on `dev/v18`. It adds a feature and
  an additive migration, and it breaks nothing at the binary level either.
  - Every published signature it extends is kept exactly. The new behaviour is in new members:
    `SiteClosure.CreateRange`, `AvailabilityConfiguration.CreateWithClosedPeriods`, an
    `AvailableDateWindow.Compute` overload taking the first open date, and `BuildForWindow` on
    both form builders. `PublishedSignatureTests` pins the old signatures, and
    `PublishedCallShapeTests` is a compile-time guard that the call shapes a 17.2.x caller could
    have written still bind to them. *(QA round 2: an overload of `Create` named `Create` had
    made `Create(hours, null)` and `Create(hours, [])` ambiguous, so they are now separate
    names.)*
  - Two published interfaces gain a member each, and each has a default implementation:
    - `IResourceManagementStore.UpdateKeepingClosedPeriodsAsync`;
    - `IBookingFormView.ShiftedWindowFrom`.

    An existing implementer still compiles and loads. *(QA round 1 found the first version had
    widened five signatures in place: source-compatible, but `MissingMethodException` for an
    assembly built against 17.2.x.)*
- **One narrowing of what the booking page discloses**, stated in `site-closures`: a window that
  starts after today reveals that the dates before it are closed rather than booked. It never
  reveals which closure, its name, or why.

## Capabilities

### New Capabilities

(none). Closed periods belong to the existing `availability`, `resource-management` and
`persistence` capabilities.

### Modified Capabilities

- `site-closures`: a closure is a range, overlaps are refused, the view takes From and To, the
  upcoming filter includes an in-progress closure, inheritance shows ranges, and the management
  contract gains `endDate`.
- `availability`: the resource's closed periods, with their precedence and overlap rules.
- `resource-management`: closed periods in the resource contract (absent means unchanged) and in
  the workspace editor; Global closures show ranges.
- `persistence`: the closure table gains an end date. Overlaps are refused under a lock, and the
  unique start-date index is kept as the backstop. Closed periods are held in a new additive table
  and written in the resource's own transaction.
- `public-holidays`: "already closed" means covered by any closure.
- `default-frontend`: the listed window starts after a closed period that covers today.

## Impact

- **Core:** `SiteClosure` (start and end), a new resource closed-period type, the overlap rules,
  and availability configuration resolving ranges into dates.
- **Persistence:** an additive migration. It adds an end-date column to `uBookItSiteClosure`,
  filled from the existing date, and a new `uBookItResourceClosedPeriod` table. Nothing is altered
  or dropped. The unique index on the closure's date stays and now indexes its start: under
  refused overlaps, two closures can never share a start date, so the index remains a true
  storage backstop. Overlap across a range is checked under an app lock. The read store still reads
  the whole closure list once per call (design D2): it is short, and the configuration scans it.
- **Backoffice:** `ClosuresController` and `ResourcesController` models (additive), and the
  generated client. The closures view gains From and To; the resource editor gains a Closed
  periods section.
- **Web:** the start of the available-dates window.
- **No change** to the delivery API's shape. Closures and closed periods stay undisclosed, as
  every closure is today.
