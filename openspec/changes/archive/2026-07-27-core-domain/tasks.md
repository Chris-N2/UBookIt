# Tasks: core-domain

## 1. Primitives and value objects

- [x] 1.1 Booking interval value object: `[start, end)` UTC instants + IANA zone id, well-formedness validation (`interval-invalid` code)
- [x] 1.2 Wall-clock window primitives: `TimeOnly`-based `[start, end)` day window; weekly open-hours pattern with per-day windows and overlap/zero-length rejection
- [x] 1.3 Date exception value object (closure or full-day override), one-per-date semantics
- [x] 1.4 Booking constraints value object with spec defaults (15 min granularity, 30 min–8 h duration, zero lead, 90-day horizon) and coherence validation
- [x] 1.5 Booker value object: optional `Guid` member key + contact details (name, validated email, optional phone)
- [x] 1.6 Structured result types and the stable failure-code set from the `bookings` spec

## 2. Entities

- [x] 2.1 `Resource`: id, normalized extensible type key (with `room` constant), display name, description, availability configuration; creation validation per `resources` spec
- [x] 2.2 `Booking` + `ResourceClaim` (plural claims collection; v1 exactly-one enforced behaviourally) + `BookingStatus` enum with transition methods enforcing the status machine (`invalid-status-transition`)

## 3. Time zone mapping

- [x] 3.1 Wall-clock ↔ UTC mapping for the site zone: spring-forward gap times excluded, ambiguous fall-back times resolve to first occurrence (earlier offset), booking instants immutable once placed

## 4. Availability computation

- [x] 4.1 Effective daily windows: weekly pattern with date exceptions applied
- [x] 4.2 Free-time computation: effective windows minus blocking claims, producing ordered disjoint intervals; `Cancelled`/`Declined` never subtract
- [x] 4.3 Slot projection: granularity-stepped start times where `[start, start+duration)` fits one free interval and satisfies lead time and horizon; pure computation, nothing persisted

## 5. Ports and services

- [x] 5.1 `IResourceStore` and `IBookingStore` ports; document the atomic-placement contract on `IBookingStore` verbatim from the `bookings` spec
- [x] 5.2 Availability query service (free time + slot projection for resource and date range) depending only on ports
- [x] 5.3 Booking service: placement running the validation pipeline in spec order with structured results, auto-confirm on success; cancellation via the status machine
- [x] 5.4 In-memory store implementations for tests that honour the atomic-placement contract

## 6. Tests (each spec scenario becomes at least one test)

- [x] 6.1 Value-object and validation tests (intervals, patterns, exceptions, constraints, booker)
- [x] 6.2 Status-machine tests: every permitted transition, every rejected transition
- [x] 6.3 Availability tests: open-hours evaluation, exception precedence, free-time splitting, cancelled-claim non-blocking
- [x] 6.4 Slot-projection tests including the exact 09:00/09:30/10:00 scenario and no-fit cases
- [x] 6.5 DST tests pinned to `Europe/London`: spring-forward gap exclusion and fall-back first-occurrence with exact UTC assertions
- [x] 6.6 Placement pipeline tests: one test per failure code, plus the success path
- [x] 6.7 Conflict semantics tests: half-open back-to-back, one-minute overlap, cancelled-does-not-block
- [x] 6.8 Concurrency contract test: racing conflicting placements against the in-memory store — exactly one succeeds
- [x] 6.9 Remove the placeholder `SmokeTests`

## 7. Wrap-up

- [x] 7.1 Public-surface audit: everything `internal` unless a spec or downstream change needs it public; `InternalsVisibleTo("UBookIt.Tests")`
- [x] 7.2 `dotnet build` and `dotnet test` green with zero warnings; verify no new package references were introduced — note: the full-solution build carries the 28 pre-existing NU1903 transitive-advisory warnings from Umbraco.Cms 17.5.3 (documented at scaffold time, to be addressed in the CI change); `UBookIt.Core` itself builds with zero warnings and zero package references

## 8. QA remediation (first review: REJECT — 1 MAJOR, 3 MINOR, 4 NIT)

- [x] 8.1 MAJOR: tests covering the untested blocking-rule halves — `Requested` blocks (free time + `conflict`), `Declined` does not block (BlockingStatusTests, 4 tests)
- [x] 8.2 MINOR: duration rules accumulate one code per failed rule instead of short-circuiting (`ValidateDuration` returns all applicable of `granularity`/`duration-too-short`/`duration-too-long`; 2 covering tests)
- [x] 8.3 MINOR: touching open-hours windows coalesce into continuous bookable time (`OpenIntervals` merges adjacent/overlapping UTC intervals; availability spec clarified with a new scenario; 4 covering tests incl. gapped-windows negative case)
- [x] 8.4 NIT: bookings spec clarified — start alignment is relative to the containing coalesced window; `outside-open-hours` suppresses separate start-alignment evaluation
- [x] 8.5 NIT: boundary tests — start exactly at now+leadTime allowed; start on horizon day 90 allowed, day 91 rejected
Deferred obligations (QA-accepted; not tasks of this change — they bind future proposals):

- **Change ② (persistence) proposal MUST include**: public `Booking` rehydration surface (per design D9, downstream changes add surface via their own specs)
- **Change ④ (delivery API) proposal MUST include**: a cap on availability query date-range span (no uncontrolled caller exists until then; ④'s QA should treat an uncapped delivery endpoint as a hard finding)
