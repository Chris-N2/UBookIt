## Why

Nine requests to the **anonymous** delivery API return HTTP 500 instead of a
validation failure. Every one is a date or time calculation performed *before*
the validation that would have rejected the input, so .NET throws
`ArgumentOutOfRangeException` and the request dies as an unhandled exception
rather than as the structured domain failure the pipeline already knows how to
produce.

All are trivially reachable by any unauthenticated caller with a hand-written
URL or body. None requires an unusual resource, a race, or any prior state. The
result is a 500 in the response, an exception in the logs, and — depending on
host configuration — a stack trace on the wire.

Confirmed empirically against the running TestSite on 2026-08-13; each row below
was observed, not inferred.

| Request | Result |
| --- | --- |
| `GET /resources/{id}/free-time?from=9999-12-31&to=9999-12-31` | 500 |
| `GET /resources/{id}/slots?…&durationMinutes=60` (same dates) | 500 |
| `GET /resources/{id}/bookable-starts` (same dates) | 500 |
| `GET /services/{id}/bookable-starts` (same dates) | 500 |
| `POST /bookings` with `start=9999-12-31T23:00:00Z` | 500 |
| `POST /services/{id}/bookings` (same start) | 500 |
| `POST /bookings` with `durationMinutes=-2147483648` | 500 |
| `POST /bookings` with `start=0001-01-01T00:00:00Z` | 500 |
| `POST /services/{id}/bookings` (same start) | 500 |

The lower date bound is clean for every availability query (`0001-01-01`
returns 200), and a large positive `durationMinutes` is **not** a trigger:
`int.MaxValue` minutes is roughly 4,084 years, which lands short of the ceiling
from any realistic start, and both placement routes correctly answer 400.

## What Changes

Three distinct root causes, all of the same shape — arithmetic reaching a
representable-range boundary ahead of validation:

- **Day-by-day iteration past the calendar's end.**
  `FreeTimeCalculator.OpenIntervals` walks `date = date.AddDays(1)` while
  `date <= toDate`; when `toDate` is `DateOnly.MaxValue` the final increment
  throws. `ServiceBookingService` has the same shape independently, deriving its
  batched-claims window from `toDate.AddDays(1)`. The existing bounded-range
  guard does not help: `from = to = 9999-12-31` is a one-day span and passes
  validation cleanly.
- **Interval arithmetic that can overflow *and* underflow.** `BookingService`
  computes `request.Start + request.Duration` before calling
  `BookingInterval.Create`. A far-future start overflows; a hugely negative
  duration underflows below `DateTimeOffset.MinValue`. `BookingInterval.Create`
  already rejects both correctly with `interval-invalid` — the addition simply
  runs first.
- **The ±1-day open-hours window expansion.** `BookingService` evaluates the
  outside-open-hours rule over `localStartDate.AddDays(-1)` to
  `localStartDate.AddDays(1)`. At either `DateOnly` bound one of those throws.
  This is why a placement at `0001-01-01` fails even though its interval
  arithmetic is perfectly representable.

Each becomes an ordinary structured failure using an **existing** stable code —
`date-range-invalid` for a query range that cannot be walked,
`interval-invalid` for a placement whose interval cannot be represented. No new
failure code is introduced.

All three fixes land in `UBookIt.Core`, at or before the validation boundary, so
one fix per cause covers both the direct-resource and via-service paths. No
controller changes.

## Capabilities

### New Capabilities
_None._ This corrects behaviour already governed by existing capabilities.

### Modified Capabilities
- `availability`: the bounded-query-range requirement gains the rule that a
  range must also be *walkable*, so a range ending at the last representable
  date is rejected rather than throwing.
- `bookings`: the placement validation pipeline gains the rule that a request
  whose interval or whose open-hours evaluation window cannot be represented
  fails with `interval-invalid`, ahead of every other rule.
- `delivery-api`: the failure-mapping requirement records that these inputs
  produce a documented 400, and that no delivery endpoint answers an unhandled
  exception for an in-range-typed but unrepresentable date or duration.

## Non-goals

- **No new failure codes.** Existing codes describe these outcomes accurately;
  inventing one would add contract surface for an input nobody sends
  deliberately.
- **No change to the accepted input range.** This is not a tightening of what
  callers may book — every date and duration accepted today stays accepted. Only
  the failure *mode* at the boundary changes, from 500 to 400.
- **No controller-level or middleware-level exception handling.** A global
  exception filter would convert these into 500-with-nicer-body, or mask them
  entirely; the point is that the domain should never have thrown. A catch-all
  handler is a separate concern and remains out of scope.
- **No schema change, migration, new endpoint, or new DTO.**
- **No audit of non-date input.** The sweep in this change covers date and time
  arithmetic reachable from delivery input. A general fuzz of every field is
  worth doing but is its own piece of work.

## Impact

- **`UBookIt.Core`**: `FreeTimeCalculator` (bounded iteration),
  `AvailabilityService` (range validation), `BookingService` (interval
  construction and the open-hours window), `ServiceBookingService` (claims
  window). No public contract change beyond failures replacing exceptions.
- **`UBookIt.Web`**: unaffected — the failures map through the existing
  problem-details projection, which already renders both codes as 400.
- **`UBookIt.Persistence`**: unaffected. No schema change, no migration.
- **`UBookIt.Tests`**: boundary coverage at `DateOnly.MinValue`/`MaxValue` and
  near `DateTimeOffset.MinValue`/`MaxValue`, for all six affected endpoints and
  both placement paths.
- **Risk of regression is concentrated in `FreeTimeCalculator`**, which every
  availability and placement path depends on; its existing behaviour for normal
  ranges must be provably unchanged.
