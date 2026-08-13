## Context

Nine anonymous delivery requests return 500 because a date or time calculation
runs before the validation that would have rejected its input. The domain
already knows how to reject every one of them — `BookingInterval.Create` returns
`interval-invalid` for a bad interval, and the query service returns
`date-range-invalid` for a bad range. In each case the arithmetic simply
executes first and throws.

The affected code is small and central:

- `FreeTimeCalculator.OpenIntervals` — the day-by-day loop every availability
  query and the placement open-hours rule depend on.
- `AvailabilityService.ValidateQueryPreconditions` — the shared range/zone guard.
- `BookingService.PlaceAsync` — interval construction, and the ±1-day window it
  builds for the open-hours rule.
- `ServiceBookingService.GetBookableStartsAsync` — its own `toDate.AddDays(1)`
  claims window, an independent instance of the same mistake.

Everything here was observed against the running TestSite, not deduced. Two
things I expected to be triggers were not, and the proposal records both: the
lower date bound is clean for availability queries, and a large *positive*
`durationMinutes` cannot overflow from any realistic start.

## Goals / Non-Goals

**Goals:**

- Every expressible date, instant, and duration produces a structured failure
  rather than an exception, on both the direct and via-service paths.
- Reuse existing failure codes; add none.
- Fix in Core so one change per cause covers every caller.
- Leave the accepted input range exactly as it is — only the boundary's failure
  *mode* changes.

**Non-Goals:**

- A global exception filter (see D4). New failure codes. Any change to what
  callers may legitimately book. A general input fuzz beyond date and time
  arithmetic.

## Decisions

### D1 — Reject the unwalkable range, rather than making the loop tolerate it

`OpenIntervals` could be rewritten to iterate by day-number and stop before
overflowing, which would let `to = 9999-12-31` succeed. Rejecting it instead,
with `date-range-invalid`, is preferable:

- The value is meaningless as a booking query. Nobody legitimately asks for
  availability on the last representable day; a request for it is a typo, a
  fuzzer, or a probe.
- A tolerant loop leaves the *next* caller of `AddDays` to rediscover the
  problem — and this change already found three independent instances of the
  same mistake, one of them written the same day. A guard at the validation
  boundary is a single place that protects all of them.
- It composes with the existing `MaxQueryRangeDays` check: both are properties
  of the range, both evaluated before any load, both returning an existing code.

The check belongs in `ValidateQueryPreconditions`, which every availability
query and the service query already share.

### D2 — Guard the interval arithmetic, not just its result

`BookingInterval.Create` cannot help: the overflow happens computing the
argument. So the addition itself must be guarded, and the guard must be
bidirectional — a far-future start overflows, and a large negative duration
underflows. Probing found both: `durationMinutes = int.MinValue` is a live 500
on the direct route today.

Interestingly the *service* route already answers 400 for that input, because
its pool-wide duration pre-check runs first. That is luck rather than design —
it protects the negative-duration case and not the far-future-start case — so
the fix belongs in `BookingService`, which both routes funnel through, not in
the service layer that happens to shadow one instance of it.

### D3 — The open-hours window is a third, separate cause

A placement at `0001-01-01` has a perfectly representable interval and still
throws, because the outside-open-hours rule inspects `localStartDate.AddDays(-1)`
through `localStartDate.AddDays(1)`. This is why the fix cannot be "guard the
interval and be done": the two placement causes are independent and need
independent tests. It also means the guard must consider the *local* date of the
start, not just the instant — the conversion to local time can move the date
across the boundary.

Treating this as `interval-invalid` rather than a new code is a judgement call:
the request is not literally an invalid interval, it is an interval too close to
the edge of the calendar to evaluate. But `interval-invalid` is documented as
"end not after start, **or malformed**", the caller's remedy is identical
(choose a sane date), and a dedicated code would add permanent contract surface
for an input nobody sends on purpose.

### D4 — No global exception filter

Wrapping the delivery pipeline in a catch-all would turn these into tidy 500s or
hide them entirely, and would remove the pressure to fix the domain. The
contract is that the domain returns failures rather than throwing
(`bookings` spec: *"Failures SHALL NOT be signalled by exceptions"*) — these
requests violate an existing requirement, and the fix is to honour it. Whether
the host should also have a backstop filter is a real question, but a separate
one, and adding it here would make it harder to tell whether the domain fix
actually worked.

### D5 — Verify live, not only in unit tests

The defect class is an unhandled exception crossing the HTTP boundary. Unit
tests can assert a `DomainResult` and still miss a throw somewhere in the
controller or serialization path, and the original bugs sat behind a fully green
suite. Every one of the nine requests is re-issued against the running site
after the fix, and the pre-fix 500s are recorded so the comparison is real.

## Risks / Trade-offs

- **[`FreeTimeCalculator` is on every hot path]** → A regression there breaks
  availability and placement together. Mitigated by changing the *validation*
  rather than the loop wherever possible (D1), and by the existing suite: 311
  unit and 35 integration tests already cover free-time computation, and all
  must stay green with no test edits.

- **[Rejecting `to = 9999-12-31` is technically a narrowing]** → A caller who
  today gets a 500 will get a 400; a caller who today gets useful data is
  unaffected, since the value only ever threw. No behaviour that worked stops
  working.

- **[`interval-invalid` now covers three distinct situations]** → Slight loss of
  diagnostic precision in exchange for no new contract surface (D3). The message
  distinguishes them for a human; the code does not, deliberately.

- **[The sweep may not be exhaustive]** → Nine instances were found by probing
  the boundaries I could think of; the class is "arithmetic before validation",
  and a fourth instance could exist behind an input I did not try. The sweep
  task is written to search the code for the *pattern* — `AddDays`, `AddMinutes`,
  and `+`/`-` on dates and instants reachable from delivery input — rather than
  only to re-test the known nine.

## Migration Plan

No schema change, no migration, no data touched. Every change is a guard added
ahead of existing logic; rollback is removing the guards, which restores the
current (throwing) behaviour. No published contract changes: two endpoints'
worth of inputs move from an undocumented 500 to a documented 400.

## Open Questions

- Should the host gain a catch-all exception filter as a backstop, so a future
  domain throw degrades to a clean 500 with no stack trace rather than whatever
  the environment renders? Out of scope here (D4), but worth deciding before the
  package ships publicly.
