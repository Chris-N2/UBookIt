# Design: core-domain

## Context

`UBookIt.Core` is an empty class library. This change gives it the booking domain everything else builds on. The domain must be pure .NET (no Umbraco, no EF), because change ② adds persistence against these types, changes ③/④ expose them over HTTP, and a headless consumer must be able to reason about the same contracts. Decisions made here — names, shapes, cardinalities — become the package's public compatibility surface, and EF migrations derived from these shapes must remain additive, so cardinality mistakes are expensive later.

Explored and agreed with the project owner before this change: bookings claim resources in continuous intervals (not fixed slot menus); a booking may later require a person as well as a room; approval workflow and holds are out of v1 but must not require schema breaks; SQL Server is the required backend (relevant here only in that the conflict semantics must be enforceable transactionally in ②); one time zone per site.

## Goals / Non-Goals

**Goals:**

- A domain model where "add persons-as-resources", "add approval behaviour", and "add capacity/events" are additive changes.
- Deterministic, fully unit-testable availability computation, including DST transition days.
- Explicit, machine-readable validation outcomes (the delivery API and accessible front-end both need to say *why* a booking was rejected).
- Ports (interfaces) that let ② implement storage and the atomic placement guarantee without changing Core.

**Non-Goals:**

- Everything listed in the proposal's Non-goals: persistence, concurrency enforcement, UI/HTTP surface, approval behaviour, holds, capacity > 1, payments/notifications, multi-time-zone.
- DDD ceremony for its own sake — no event sourcing, no domain events bus in v1.

## Decisions

### D1: Booking claims resources through `ResourceClaim` (1..N), not a direct FK

A `Booking` owns a collection of `ResourceClaim`s; each claim binds one resource for the booking's interval. v1 *behaviour* enforces exactly one claim per booking.

- **Why**: "room + required person" later becomes a second claim — additive. A direct `Booking.ResourceId` would force a breaking migration or a bolted-on nullable column. Conflict detection is naturally per-claim, which is also what capacity-style claims would need.
- **Alternative considered**: single `ResourceId` FK now, refactor later — rejected: violates the additive-migrations convention at the first interesting feature.

### D2: Slots are projections, never entities

Free time = weekly open hours − exceptions − confirmed claims, computed on demand for a resource + date range. A "slot picker" is a presentation of this projection using the resource's granularity/duration constraints.

- **Why**: nothing to regenerate when rules change; no storage; no drift between rules and stored slots. Meeting-room booking is interval-shaped, not menu-shaped.
- **Alternative considered**: materialised slot rows with a uniqueness constraint (the classic double-booking trick) — rejected: forces a fixed grid, makes rule edits destructive, and pushes a generation job into the package. The double-booking guarantee moves to ②'s atomic placement instead.

### D3: BCL time types, not NodaTime

Wall-clock rules use `DateOnly`/`TimeOnly`/`DayOfWeek`; instants use UTC `DateTimeOffset`; the site zone is an IANA id string resolved via `TimeZoneInfo` (ICU gives IANA support on all platforms we target; the test site already opts into app-local ICU).

- **Why**: NodaTime is the better time library, but its types would appear in our public API and view models, forcing every consumer (including headless ones via serialization shapes) to adopt or map it. A booking package's contract should be expressible in BCL types. Our needs (one zone, weekly wall-clock windows, UTC instants, DST edge rules) are within BCL capability *if* the edge semantics are specified and tested, which the `availability` spec does.
- **Alternative considered**: NodaTime internally, BCL at the boundary — rejected: the mapping layer adds more surface for bugs than it removes, at this level of complexity.

### D4: DST edge semantics are specified, not emergent

On a spring-forward day, wall-clock window portions that fall in the nonexistent gap are skipped (do not exist as bookable time). On a fall-back day, ambiguous wall-clock times resolve to their **first** occurrence (earlier UTC offset). A booking's interval is fixed in UTC at placement; later zone-rule changes never move an existing booking.

- **Why**: any rule is defensible; an *unstated* rule is a bug generator. "First occurrence" matches user intuition ("9am" means the 9am that happens first) and `TimeZoneInfo`'s default mapping.

### D5: Validation returns structured results, not exceptions

Booking placement runs an ordered rule pipeline (interval well-formed → granularity → duration bounds → lead time → horizon → within open hours → no conflicting claim). The outcome is a result object carrying pass/fail plus a stable machine-readable failure code per rule.

- **Why**: the delivery API (④) must map failures to problem details, and the accessible front-end (⑤) must announce specific errors; both need codes, not exception strings. Exceptions remain for programming errors only.
- **Alternative considered**: throw domain exceptions — rejected: rejection is an expected outcome, not an exceptional one, and code-carrying results are cheaper to test.

### D6: Two ports; Core owns semantics, ② owns atomicity

Core defines `IResourceStore` (load resources + availability config) and `IBookingStore` (query claims overlapping an interval; atomically persist a validated booking). Core's `BookingService` orchestrates: validate via pure functions, then call `IBookingStore` for placement. The store contract explicitly states that placement must be atomic with respect to the conflict check (the "no double booking" requirement); ② implements that with SQL Server primitives.

- **Why**: keeps Core pure and testable with in-memory fakes, while making the concurrency obligation part of the *contract* rather than an implementation nicety.
- **Alternative considered**: conflict check purely in Core with the store as dumb CRUD — rejected: a check-then-write race is exactly the bug this package must not have; atomicity must live where the transaction lives.

### D7: Resource type is an extensible string key, not an enum

`Resource.Type` is a normalized string key (v1 ships the `room` constant). Composition rules ("a booking must include a claim on a room *and* a person") are future per-type configuration, not schema.

- **Why**: adding `person` (or `equipment`) later must not require a migration or an enum-breaking change.

### D8: Identity and booker shape

Entities use `Guid` ids generated by Core (sequential-guid concerns are ②'s problem). The booker is a value object: optional opaque member key (`Guid?` — Umbraco member keys are Guids, but no Umbraco type is referenced) plus captured contact details (name, email, optional phone). Contact details are required even for members at the domain level; ④ may prefill them.

### D9: Public surface starts minimal

Types are `public` only when a downstream change needs them; everything else starts `internal` (with `InternalsVisibleTo` for the test project). Namespace root is `UBookIt.Core`.

- **Why**: the public surface is a compatibility promise; the cheapest breaking change is the one you never shipped.

## Risks / Trade-offs

- [BCL time handling differs subtly across OS/ICU versions] → app-local ICU is already opted into by the test site; the DST test suite pins IANA ids (e.g. `Europe/London`) and asserts exact UTC instants, so a platform drift fails loudly in CI.
- [Interval model can't use a uniqueness constraint for double-booking] → accepted consciously (D2/D6); ② must implement atomic placement (e.g. per-resource app lock or serializable range check) and prove it with a concurrency test. The contract wording in the `bookings` spec is the guard.
- [Public API shaped before real consumers exist] → D9 (minimal surface) plus the rule that ③–⑤ may *add* surface via their own specs; QA reviews additions against the spec each time.
- [String-keyed resource types invite typos] → normalized keys + provided constants + validation on resource creation; a registry of known types can arrive with the management API change.
- [Skipping NodaTime may age badly if requirements grow (multi-zone, recurring bookings)] → recurrence of *bookings* is not planned; if multi-zone arrives, it lands behind the same wall-clock value objects, and revisiting D3 is contained to availability computation internals.

## Open Questions

- None blocking. Default constraint values (granularity 15 min, horizon 90 days, etc.) are set in the `availability` spec and are configuration, not contract.
