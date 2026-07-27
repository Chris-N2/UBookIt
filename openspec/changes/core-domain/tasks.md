# Tasks: core-domain

## 1. Primitives and value objects

- [ ] 1.1 Booking interval value object: `[start, end)` UTC instants + IANA zone id, well-formedness validation (`interval-invalid` code)
- [ ] 1.2 Wall-clock window primitives: `TimeOnly`-based `[start, end)` day window; weekly open-hours pattern with per-day windows and overlap/zero-length rejection
- [ ] 1.3 Date exception value object (closure or full-day override), one-per-date semantics
- [ ] 1.4 Booking constraints value object with spec defaults (15 min granularity, 30 min–8 h duration, zero lead, 90-day horizon) and coherence validation
- [ ] 1.5 Booker value object: optional `Guid` member key + contact details (name, validated email, optional phone)
- [ ] 1.6 Structured result types and the stable failure-code set from the `bookings` spec

## 2. Entities

- [ ] 2.1 `Resource`: id, normalized extensible type key (with `room` constant), display name, description, availability configuration; creation validation per `resources` spec
- [ ] 2.2 `Booking` + `ResourceClaim` (plural claims collection; v1 exactly-one enforced behaviourally) + `BookingStatus` enum with transition methods enforcing the status machine (`invalid-status-transition`)

## 3. Time zone mapping

- [ ] 3.1 Wall-clock ↔ UTC mapping for the site zone: spring-forward gap times excluded, ambiguous fall-back times resolve to first occurrence (earlier offset), booking instants immutable once placed

## 4. Availability computation

- [ ] 4.1 Effective daily windows: weekly pattern with date exceptions applied
- [ ] 4.2 Free-time computation: effective windows minus blocking claims, producing ordered disjoint intervals; `Cancelled`/`Declined` never subtract
- [ ] 4.3 Slot projection: granularity-stepped start times where `[start, start+duration)` fits one free interval and satisfies lead time and horizon; pure computation, nothing persisted

## 5. Ports and services

- [ ] 5.1 `IResourceStore` and `IBookingStore` ports; document the atomic-placement contract on `IBookingStore` verbatim from the `bookings` spec
- [ ] 5.2 Availability query service (free time + slot projection for resource and date range) depending only on ports
- [ ] 5.3 Booking service: placement running the validation pipeline in spec order with structured results, auto-confirm on success; cancellation via the status machine
- [ ] 5.4 In-memory store implementations for tests that honour the atomic-placement contract

## 6. Tests (each spec scenario becomes at least one test)

- [ ] 6.1 Value-object and validation tests (intervals, patterns, exceptions, constraints, booker)
- [ ] 6.2 Status-machine tests: every permitted transition, every rejected transition
- [ ] 6.3 Availability tests: open-hours evaluation, exception precedence, free-time splitting, cancelled-claim non-blocking
- [ ] 6.4 Slot-projection tests including the exact 09:00/09:30/10:00 scenario and no-fit cases
- [ ] 6.5 DST tests pinned to `Europe/London`: spring-forward gap exclusion and fall-back first-occurrence with exact UTC assertions
- [ ] 6.6 Placement pipeline tests: one test per failure code, plus the success path
- [ ] 6.7 Conflict semantics tests: half-open back-to-back, one-minute overlap, cancelled-does-not-block
- [ ] 6.8 Concurrency contract test: racing conflicting placements against the in-memory store — exactly one succeeds
- [ ] 6.9 Remove the placeholder `SmokeTests`

## 7. Wrap-up

- [ ] 7.1 Public-surface audit: everything `internal` unless a spec or downstream change needs it public; `InternalsVisibleTo("UBookIt.Tests")`
- [ ] 7.2 `dotnet build` and `dotnet test` green with zero warnings; verify no new package references were introduced
