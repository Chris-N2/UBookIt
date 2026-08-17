# bookings Specification

## Purpose

Defines the booking domain for uBookIt: booking shape and resource claims, the status machine, booker identity, the placement validation pipeline with stable failure codes, conflict semantics, the atomic placement contract, and the service/store ports exposed by `UBookIt.Core`.
## Requirements
### Requirement: Booking shape
A booking SHALL have a `Guid` id, exactly one continuous interval `[start, end)` held as UTC instants plus the IANA zone id it was placed against, a creation timestamp (UTC), a booker, a status, and a collection of 1..N resource claims. Each `ResourceClaim` SHALL bind exactly one resource to the booking's interval.

A booking SHALL carry **one claim per role of the service it was placed for**, all over that one interval; direct placement, which names a single resource, SHALL continue to produce exactly one claim. The earlier rule that v1 behaviour enforces exactly one claim per booking is **lifted**: the model was always plural, and multi-role composition is what makes the plural case reachable.

A booking SHALL NOT claim the same resource twice.

#### Scenario: Valid single-claim booking
- **WHEN** a booking is placed for one room resource for a valid interval
- **THEN** the booking has exactly one resource claim, referencing that resource, covering the booking interval

#### Scenario: Claims collection is plural by design
- **WHEN** the domain model's public surface is inspected
- **THEN** a booking exposes a collection of resource claims (not a single resource reference)

#### Scenario: A service booking carries one claim per role
- **WHEN** a service requiring a `room` and a `therapist` is booked
- **THEN** the booking carries two claims, one for each, both covering the booking's single interval

### Requirement: Booking status machine
Booking status SHALL be one of `Requested`, `Confirmed`, `Declined`, `Cancelled`. Permitted transitions SHALL be exactly: `Requested → Confirmed`, `Requested → Declined`, `Requested → Cancelled`, and `Confirmed → Cancelled`. Any other transition SHALL be rejected with a stable failure code `invalid-status-transition`. In v1, successful placement SHALL yield a `Confirmed` booking (auto-confirm); no v1 pathway SHALL produce `Requested` or `Declined`, but both statuses and their transitions SHALL exist so approval behaviour can be added without breaking changes.

#### Scenario: v1 placement auto-confirms
- **WHEN** a booking is successfully placed
- **THEN** its status is `Confirmed`

#### Scenario: Cancelling a confirmed booking
- **WHEN** a `Confirmed` booking is cancelled
- **THEN** its status becomes `Cancelled`

#### Scenario: Cancelling twice is rejected
- **WHEN** a `Cancelled` booking is cancelled again
- **THEN** the operation fails with code `invalid-status-transition` and the status remains `Cancelled`

### Requirement: Booker identity
A booking's booker SHALL carry an optional opaque member key (`Guid?`, never an Umbraco type) and contact details: a non-empty name, a well-formed email address, and an optional phone number. Contact details SHALL be required regardless of whether a member key is present.

#### Scenario: External booker without member record
- **WHEN** a booking is placed with no member key but with name and valid email
- **THEN** the booking is accepted

#### Scenario: Missing email is rejected
- **WHEN** a booking is placed with a name but no email address
- **THEN** placement fails with a validation failure identifying the email field

### Requirement: Placement validation pipeline
Booking placement SHALL validate a request against an ordered rule pipeline, producing a structured result: success, or failure carrying one stable machine-readable code per failed rule. The rules and codes SHALL be, in order: `interval-invalid` (end not after start, malformed, or not representable), `granularity` (start or duration not aligned), `duration-too-short`, `duration-too-long`, `lead-time` (starts sooner than minimum lead), `horizon` (starts beyond booking horizon), `outside-open-hours` (interval not fully inside open hours with exceptions applied), `conflict` (overlaps a blocking claim). Start alignment is defined relative to the start of the coalesced open window containing the interval; when the interval lies outside open hours, start alignment cannot be evaluated and only `outside-open-hours` is reported. Failures SHALL NOT be signalled by exceptions.

A request SHALL fail with `interval-invalid`, ahead of every other rule, when the requested interval cannot be represented: when the start added to the duration would exceed the last representable instant, or fall before the first. Both directions SHALL be covered — a far-future start overflows, and a large negative duration underflows.

A request SHALL likewise fail with `interval-invalid` when the interval is representable but the surrounding window the open-hours rule needs cannot be. Evaluating open hours inspects the day either side of the requested start, so a start within **one day of either end** of the calendar SHALL be rejected — the first two and the last two representable dates.

Two days are needed at each end, for different reasons. Above, the day after the start must exist and the day-by-day walk then steps once past it. Below, the day before the start must exist and must itself be mappable to UTC, which the first representable date is not for a site zone far enough east. Such a request SHALL be rejected as an unrepresentable interval rather than raising an exception.

No placement request SHALL raise an exception for any start instant or duration the caller can express, whatever its magnitude or sign.

When placement runs over a service's candidate pool, an `interval-invalid` failure SHALL be reported as itself rather than translated into an all-candidates-failed outcome. It describes the request, not any candidate's answer to it, so every candidate reports it identically; answering "no resource can fulfil this" would blame the pool for a fault it has nothing to do with, and would send a caller whose date is simply unrepresentable looking for a different time slot.

#### Scenario: Inverted interval
- **WHEN** placement is requested with an end instant not after its start
- **THEN** the result is failure with code `interval-invalid`

#### Scenario: Request outside open hours
- **WHEN** placement is requested for 07:00–08:00 on a resource open from 08:00
- **THEN** the result is failure with code `outside-open-hours`

#### Scenario: Valid request succeeds with structured result
- **WHEN** placement is requested for an aligned, correctly sized interval in free open time
- **THEN** the result is success and carries the created `Confirmed` booking

#### Scenario: An interval that would overflow is rejected
- **WHEN** placement is requested with a start close enough to the last representable instant that adding the duration would exceed it
- **THEN** the result is failure with code `interval-invalid` and no exception is raised

#### Scenario: An interval that would underflow is rejected
- **WHEN** placement is requested with a large negative duration, such that the interval end would fall before the first representable instant
- **THEN** the result is failure with code `interval-invalid` and no exception is raised

#### Scenario: A start at a calendar boundary is rejected
- **WHEN** placement is requested for a start on either of the first two, or either of the last two, representable dates, so the open-hours rule's surrounding day window cannot be formed
- **THEN** the result is failure with code `interval-invalid` and no exception is raised

#### Scenario: A service placement echoes the request-level failure
- **WHEN** a service placement is requested with a start at a calendar edge, so every candidate rejects it as unrepresentable
- **THEN** the result carries `interval-invalid`, not `service-unavailable`

#### Scenario: Ordinary far-future placement is unaffected
- **WHEN** placement is requested for a start many years ahead but far from the representable limit
- **THEN** the request is evaluated by the normal pipeline and fails on `horizon`, not on `interval-invalid`

### Requirement: Conflict semantics
Two claims SHALL conflict iff they reference the same resource and their intervals overlap, where intervals are half-open `[start, end)` — a booking ending at instant T does not conflict with one starting at T. Claims SHALL block (participate in conflicts and reduce free time) iff their booking's status is `Requested` or `Confirmed`; `Cancelled` and `Declined` bookings SHALL NOT block.

#### Scenario: Back-to-back bookings do not conflict
- **WHEN** a resource has a confirmed booking 09:00–10:00 and placement is requested for 10:00–11:00
- **THEN** no conflict is reported

#### Scenario: One-minute overlap conflicts
- **WHEN** a resource has a confirmed booking 09:00–10:00 and placement is requested for 09:59–11:00
- **THEN** the result is failure with code `conflict`

#### Scenario: Cancelled booking does not block
- **WHEN** a resource's only booking 09:00–10:00 is `Cancelled` and placement is requested for 09:00–10:00
- **THEN** no conflict is reported and placement can succeed

### Requirement: Atomic placement contract
The booking store port (`IBookingStore`) SHALL define placement as atomic with respect to conflict detection: between the conflict check and the persistence of a new booking's claims, no other placement for an overlapping interval on **any** of the booking's claimed resources may succeed. Under concurrent placement of conflicting requests, exactly one SHALL succeed and the others SHALL fail with code `conflict`.

A booking carrying several claims SHALL be placed all-or-nothing: either every claim is persisted, or none is. A placement that fails for one claimed resource SHALL NOT leave claims persisted for the others, since service placement attempts assignments in sequence and depends on a failed attempt leaving no state.

Two placements SHALL be treated as conflicting when their intervals overlap and their claim sets share **at least one** resource. A store that detected conflicts only for wholly identical claim sets would let two bookings each take a resource the other also claimed.

Locks SHALL be acquired in a deterministic order across a booking's claimed resources, so that concurrent multi-claim placements sharing resources cannot deadlock.

Core defines this contract and SHALL honour it in its in-memory test double, including for several claims: a double that conflicted only on a single claimed resource would let unit tests agree with an implementation the SQL store rejects.

#### Scenario: Racing conflicting placements
- **WHEN** two placements for overlapping intervals on the same resource are executed concurrently against a conforming store
- **THEN** exactly one succeeds and the other fails with code `conflict`

#### Scenario: Racing multi-claim placements that share a resource
- **WHEN** one placement claims resources A and B and another claims B and C over overlapping intervals, executed concurrently
- **THEN** exactly one succeeds and the other fails with code `conflict`

#### Scenario: A failed multi-claim placement leaves nothing
- **WHEN** a placement claiming several resources fails because one of them conflicts
- **THEN** no claim is persisted for any of its resources, and no booking row remains

#### Scenario: Multi-claim placements that share nothing both succeed
- **WHEN** one placement claims resources A and B and another claims C and D over overlapping intervals, executed concurrently
- **THEN** both succeed, because their claim sets are disjoint

#### Scenario: Deadlock-free under crossing claim sets
- **WHEN** two placements whose claim sets share resources are executed concurrently in opposite claim orders
- **THEN** both complete — one succeeding and one failing with `conflict` — and neither blocks indefinitely

### Requirement: Availability and placement service ports
`UBookIt.Core` SHALL expose an availability query service (free-time and slot projection for a resource and date range, per `availability`) and a booking service (placement running the validation pipeline, and cancellation applying the status machine). Both SHALL depend only on the two store ports (`IResourceStore`, `IBookingStore`) so implementations can be swapped without changing Core.

Core services concerned with services (per `service-booking`) MAY additionally depend on the service read port (`IServiceStore`). No Core service SHALL depend on a management store: the read ports are the only pathway anonymous delivery traffic reaches storage through.

The read port SHALL additionally expose a listing of every resource of a given type key, unpaged. It is unpaged deliberately — its consumer is a candidate pool, and a truncated pool silently produces a wrong answer rather than an error. This is distinct from the management port's type-key listing, which reports type keys with usage counts for a picker and SHALL remain management-only.

The booking store port SHALL additionally expose a claims read spanning several resource ids in one call, with the same half-open overlap and status-neutral semantics as the single-resource read. It exists so a query over a candidate pool does not issue one round trip per candidate; it SHALL return the same claims the single-resource read would return for each of those resources.

The availability query service SHALL additionally expose a pure bookable-start projection taking an already-loaded resource aggregate together with already-read claims, for callers that must issue no reads of their own. It SHALL produce results identical to the id-based query for the same resource, date range, and stored state — it changes only who performs the reads, never what is computed. Claims belonging to other resources SHALL be ignored rather than rejected, so one batched read can be passed for every candidate in turn.

Only one such member SHALL be added. An additional overload taking the resource but reading claims itself would leave the batched claims read without a caller on the path it exists to serve, and would ship untested public surface on a port interface.

#### Scenario: Services are testable with in-memory stores
- **WHEN** the availability and booking services are constructed with in-memory store implementations
- **THEN** all placement, cancellation, free-time, and slot-projection behaviour in these specs is exercisable without a database

#### Scenario: Type listing returns every resource of the type
- **WHEN** the read port is asked for resources of a type key whose population exceeds any default page size
- **THEN** every resource of that type is returned, with no paging parameters available to truncate the result

#### Scenario: Management type listing stays management-only
- **WHEN** the read port's surface is inspected
- **THEN** it exposes no type-key-with-usage-count listing; that remains on the management port

#### Scenario: Batched claims match single-resource reads
- **WHEN** claims are read for three resource ids in one call over a range
- **THEN** the result is exactly the union of what the single-resource read returns for each of those three ids over the same range

#### Scenario: The pure projection matches the id-based query
- **WHEN** the availability query is issued for a resource id that has bookings in range, and the pure projection is issued for that same resource with the claims read separately
- **THEN** both produce identical results, entry for entry, including each entry's minimum and maximum

#### Scenario: Claims for other resources are ignored
- **WHEN** the pure projection is passed a claim belonging to a different resource that would, if applied, remove all of this resource's free time
- **THEN** the result is unchanged from passing no claims at all

### Requirement: Booking rehydration
`UBookIt.Core` SHALL expose a public, additive rehydration factory (`Booking.Rehydrate`) that materializes a `Booking` from stored state: id, interval, booker, claims, status, and created timestamp. Rehydration SHALL enforce structural invariants (at least one claim; no duplicate resource per booking) and SHALL accept any `BookingStatus` without applying transition rules — the stored status is historical fact, not a transition. Rehydration SHALL NOT be usable to bypass placement validation: it is documented as a persistence-boundary API, and placement remains the only pathway that creates new bookings. (Discharges the deferred obligation recorded at core-domain archive, per design decision D9: downstream changes add Core surface via their own specs.)

#### Scenario: Rehydrated booking is faithful
- **WHEN** a booking is rehydrated with a `Declined` status and two claims on distinct resources
- **THEN** the resulting `Booking` reports exactly that status and those claims, and its transition methods still enforce the status machine from the current state

#### Scenario: Structural invariants still hold
- **WHEN** rehydration is attempted with zero claims or with two claims on the same resource
- **THEN** rehydration fails; no `Booking` is produced

### Requirement: Booking a single resource requires that resource to permit it
Placement of a booking that claims **one resource, named by the caller** SHALL be
refused when that resource withholds permission to be booked on its own, with the
stable code `resource-not-directly-bookable`.

The refusal SHALL be evaluated in `UBookIt.Core`, on the single-resource placement
entry point, **before** the request is composed into a claim set. Placing it in a
transport layer would leave every other caller open — the no-JavaScript booking
flow reaches placement in process without crossing an HTTP boundary at all — and
the rule is a property of booking, not of any one way of asking for one.

Placement that claims resources **derived from a service's roles** SHALL NOT be
subject to this rule, whatever those resources permit. The distinction SHALL be
structural rather than a flag on the request: the single-resource entry point *is*
the direct path, and service placement composes its own claim set without passing
through it. A parameter asserting "this is a direct booking" would restate what the
call site already means, in a form a caller can get wrong.

A service of one role and a count of one therefore books a resource that withholds
the permission, and SHALL succeed. That is not a loophole: the business has offered
that service, and the resource is being booked as part of it.

The refusal SHALL be reported as **its own** cause, distinct from unavailability.
It does not vary with the instant asked for, the length, the calendar or how busy
the resource is, so reporting it as no-availability would send a booker to try
another time that cannot help, and an editor to inspect opening hours that are not
wrong.

The refusal SHALL NOT be treated as a deterministic per-candidate refusal in
service placement's all-fail classification, because service placement never
reaches it.

#### Scenario: A resource withholding the permission cannot be booked alone
- **WHEN** a booking is placed naming a single resource that withholds direct booking, at a time it is otherwise free and open
- **THEN** placement fails with the stable code `resource-not-directly-bookable`, and no claim is persisted

#### Scenario: The same resource is bookable as part of a service
- **WHEN** the same resource, at the same instant, is claimed by a service booking whose role resolves to it
- **THEN** placement succeeds

#### Scenario: A single-role service of count one is still a service
- **WHEN** a service with one role of count 1 resolves to a resource that withholds direct booking, and is placed
- **THEN** placement succeeds, because the resource is being booked as part of a service rather than on its own

#### Scenario: A resource granting the permission is unaffected
- **WHEN** a booking is placed naming a single resource that permits direct booking
- **THEN** placement proceeds through the existing validation pipeline exactly as before

#### Scenario: The refusal precedes the rule pipeline
- **WHEN** a booking is placed naming a resource that withholds direct booking, for an interval that would also fail on open hours
- **THEN** the result carries `resource-not-directly-bookable` rather than `outside-open-hours`, because the request was never one this resource accepts

#### Scenario: The refusal is not a conflict and invites no retry
- **WHEN** a booking naming a withholding resource is refused
- **THEN** the code is `resource-not-directly-bookable`, never `conflict`, and the same request will be refused identically however many times it is retried
