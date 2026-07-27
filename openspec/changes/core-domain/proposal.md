# Proposal: core-domain

## Why

uBookIt exists because there is no good open-source booking system for Umbraco, and everything else in the package (persistence, management API, delivery API, rendering) depends on a sound domain model. This change establishes that foundation: a pure .NET domain in `UBookIt.Core` for resources that can be booked in continuous time intervals, designed so that later features (bookings requiring a person, approval workflows, capacity events) are additive rather than breaking.

## What Changes

- New domain model in `UBookIt.Core` (currently an empty project):
  - **Resources**: bookable entities (v1 archetype: a room) with a resource type, identity, and display metadata. Behaviour and presentation are split — content nodes will present resources later; the domain never depends on Umbraco content.
  - **Availability**: per-resource weekly open-hours pattern, date-specific exceptions (closures/overrides), and booking constraints (granularity, min/max duration, minimum lead time, booking horizon).
  - **Bookings**: a booking claims 1..N resources for one continuous interval via `ResourceClaim` rows (v1 behaviour: exactly one claim). Status machine `Requested → Confirmed | Declined`, plus `Cancelled`; v1 behaviour is auto-confirm only, but the full enum exists from day one.
  - **Free-time/slot projection**: available slots are computed from open hours − exceptions − existing claims; slots are never stored.
  - **Booker identity**: nullable member reference plus captured contact details, so both logged-in members and external visitors can book.
  - **Time handling**: availability rules are wall-clock in a single site time zone; booking instants are UTC plus the IANA zone id they were made against. DST edge behaviour is specified and tested, not incidental.
  - Service abstractions (interfaces) for availability queries and booking placement that changes ② (persistence) and ④ (delivery API) will implement/consume.
- New unit test suite in `UBookIt.Tests` covering availability computation, constraint validation, the status machine, and DST edges.
- No public API compatibility impact: nothing is published yet; this change creates the first public API surface, which becomes the compatibility baseline.

## Capabilities

### New Capabilities

- `resources`: what a bookable resource is — identity, type, display metadata, capacity semantics (v1: single-occupancy), and the behaviour/presentation split.
- `availability`: how a resource's free time is defined and computed — weekly open hours, exceptions, booking constraints, slot projection, and time-zone/DST semantics.
- `bookings`: how a booking is requested, validated, confirmed, and cancelled — the claims model, status machine, booker identity, and conflict rules.

### Modified Capabilities

None — these are the first capabilities in the project.

## Non-goals

- **Approval behaviour**: `Requested`/`Declined` exist in the enum but no v1 pathway sets them; all v1 bookings auto-confirm. Approval workflow is a later change.
- **Holds/reservations during checkout**: not needed for the target contention profile; explicitly out.
- **Capacity > 1 and events**: class/event-style booking with seat counts is out (likely a separate module/package); the claims model must not block it.
- **Persons as bookable resources**: the resource-type and claims design must leave room, but v1 ships only room-style resources and single-claim bookings.
- **Persistence**: no EF Core, no SQL Server, no repositories — that is change ②. This change defines abstractions only.
- **Concurrency guarantee**: the transactional double-booking guarantee is change ②'s responsibility; this change defines the *semantic* conflict rules it must enforce.
- **Any UI or HTTP surface**: management API (③), delivery API (④), and rendering (⑤) come later.
- **Payments, notifications, iCal**: out of scope for core.
- **Multiple time zones per site**: one zone per site is a stated v1 constraint.

## Impact

- `src/UBookIt.Core`: gains the entire domain model. Stays free of Umbraco and EF dependencies (the member reference is an opaque key, not an Umbraco type).
- `tests/UBookIt.Tests`: gains the domain unit test suite; the placeholder smoke test is replaced.
- No other projects change. No new package dependencies expected; if any prove necessary they must be named with licenses in the specs.
- Downstream: changes ② (persistence, SQL Server), ③ (management API/backoffice), ④ (delivery API), ⑤ (default front-end) all build on the types and interfaces introduced here, so naming and shape here set the package's public contract.
