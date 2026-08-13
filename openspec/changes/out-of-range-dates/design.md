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

Everything here was observed against the running TestSite, not deduced. One
thing I expected to be a trigger was not, and the proposal records it: a large
*positive* `durationMinutes` cannot overflow from any realistic start.

A second expectation — that the lower date bound was clean — turned out to be an
artefact of the site zone, and is corrected in D6 below.

## Goals / Non-Goals

**Goals:**

- Every expressible date, instant, and duration produces a structured failure
  rather than an exception, on both the direct and via-service paths.
- Reuse existing failure codes; add none.
- Fix in Core so one change per cause covers every caller.
- Preserve every input that legitimately works today. One boundary date is
  narrowed deliberately (D6); nothing else changes what callers may book.

**Non-Goals:**

- A global exception filter (see D4). New failure codes. A general input fuzz
  beyond date and time arithmetic.

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

### D2 — Guard the interval arithmetic, on the clock, not just its result

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

**Corrected during QA.** The first version measured headroom by comparing the
duration against `DateTimeOffset.MaxValue - start`. That is the wrong headroom:
the subtraction is a UTC difference, while `start + duration` moves the *clock*
component and keeps the offset. For an eastern offset the clock can overflow
while UTC still has room, so the guard passed and the addition threw — inside
the method written to prevent exactly that. Three anonymous requests remained
live 500s, and no test caught it because every case used a zero offset. Both
limits are now checked explicitly: the clock must stay in range, and the
resulting UTC must too, since the `DateTimeOffset` constructor requires both.

The lesson generalises past this change: a guard expressed in terms of one
representation of a value cannot be trusted to protect an operation defined in
terms of another.

### D3 — The open-hours window is a third, separate cause

A placement at `0001-01-01` has a perfectly representable interval and still
throws, because the outside-open-hours rule inspects `localStartDate.AddDays(-1)`
through `localStartDate.AddDays(1)`. This is why the fix cannot be "guard the
interval and be done": the two placement causes are independent and need
independent tests. It also means the guard must consider the *local* date of the
start, not just the instant — the conversion to local time can move the date
across the boundary.

**Corrected during QA.** The first version left a one-day margin below and a
two-day margin above, so the window's earlier day could be the first
representable date — which is precisely the date the query path rejects, for the
same eastern-zone reason. The margins are now symmetric.

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

### D6 — The lower bound is narrowed, and the sweep record

Probing said the lower date bound was fine. It is fine *in London*. Mapping a
wall-clock time on the first representable date into UTC subtracts the site
zone's offset, and when that offset exceeds the time of day being mapped the
result falls before the calendar starts and `DateTimeOffset` refuses it. A
midnight opening window overflows for any eastern zone; a 09:00 window needs an
offset above +09:00. So the pre-existing behaviour was not "works" but "works
for this site's configuration".

Both edges are therefore rejected, which narrows one input that previously
returned an empty result. Taken knowingly: a zone-independent rule is worth more
than a query nobody issues, and the alternative — rejecting only the
combinations that actually overflow — makes the rule depend on each resource's
opening times.

**Sweep record** (task 5.2), so a reader can see what was examined rather than
only what was changed:

| Site | Verdict |
| --- | --- |
| `FreeTimeCalculator.OpenIntervals` day loop | Guarded at all three call sites; loop left honest rather than silently truncating |
| `ServiceBookingService` claims window | Protected by the shared precondition; ordering noted in a comment |
| `BookingService` interval addition | Guarded, both directions, and for offset-carrying starts (D2) |
| `BookingService` open-hours window | Guarded, with a two-day margin at each end (D3) |
| `HorizonDays` in placement, slot projection, and the Razor date picker | Saturating; only validated as positive, so an administrator can overflow it |
| `WallClockMapper` DST gap-walk (`probe.AddMinutes(-1)`) | Examined, not guarded: it runs only for a wall-clock time inside a DST gap, which requires a modern-era transition, so it is unreachable at the calendar's edges |
| `SlotProjector.Walk` `start += Granularity` | Examined, not guarded: needs an administrator-set granularity of order `int.MaxValue` minutes *and* a far-future query. Same class as the horizon case but far more contrived; recorded rather than fixed, and the natural home for it is a bound on granularity at configuration time |

### D7 — Only the saturating helper is public

`CalendarBounds` is public so `UBookIt.Web` can reach `AddDaysSaturating` for the
date picker's upper bound; the other members are `internal`. Public API is a
compatibility promise, and a helper that exists to centralise reasoning inside
Core should not export the parts of that reasoning nobody outside Core uses.

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
