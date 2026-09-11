# bookings — delta for approval-decline

## ADDED Requirements

### Requirement: Confirming and declining a booking are service operations
The booking service SHALL expose an operation that confirms a booking and an operation that
declines one. Each SHALL load the booking, apply the domain transition, persist the change,
and report it through the observation port — the same shape as cancellation, and for the same
reasons.

An unknown booking SHALL fail with `booking-not-found`. A booking whose status does not permit
the transition SHALL fail with `invalid-status-transition`, and the store SHALL NOT be touched.
No new failure codes are introduced: both outcomes already have stable codes, and a caller
distinguishes them because they call for different actions.

**Being observed at all means the transition just happened.** Both operations succeed only from
`Requested`, so — like cancellation — a report needs no before-and-after: a second attempt
fails above the store and reports nothing.

A declined booking SHALL stop blocking, per *Conflict semantics*: its claims no longer
participate in conflicts and no longer reduce free time, so the slot a decline releases is
immediately bookable again.

There SHALL NOT be a general "set status" operation. Publishing one would invite exactly the
transitions the status machine exists to refuse; one operation per verb is the precedent
cancellation set, and confirm and decline follow it.

#### Scenario: A requested booking is confirmed
- **WHEN** the confirm operation is called for a `Requested` booking
- **THEN** the result is success, the stored booking is `Confirmed`, and the confirmation is reported once, after the status is stored, carrying that booking

#### Scenario: A requested booking is declined
- **WHEN** the decline operation is called for a `Requested` booking
- **THEN** the result is success, the stored booking is `Declined`, and the decline is reported once, after the status is stored, carrying that booking

#### Scenario: Confirming a booking that is not requested is refused
- **WHEN** the confirm operation is called for a booking that is `Confirmed`, `Declined` or `Cancelled`
- **THEN** it fails with `invalid-status-transition`, the stored status is unchanged, and nothing is reported

#### Scenario: Declining a booking that is not requested is refused
- **WHEN** the decline operation is called for a booking that is `Confirmed`, `Declined` or `Cancelled`
- **THEN** it fails with `invalid-status-transition`, the stored status is unchanged, and nothing is reported

#### Scenario: An unknown booking is reported as not found
- **WHEN** the confirm or decline operation is called with an id no booking has
- **THEN** it fails with `booking-not-found` and nothing is reported

#### Scenario: A decline releases the slot
- **WHEN** a `Requested` booking is declined and availability is then computed over its interval
- **THEN** the interval it held is free again, exactly as if the booking had been cancelled

## MODIFIED Requirements

### Requirement: Booking status machine
Booking status SHALL be one of `Requested`, `Confirmed`, `Declined`, `Cancelled`. Permitted transitions SHALL be exactly: `Requested → Confirmed`, `Requested → Declined`, `Requested → Cancelled`, and `Confirmed → Cancelled`. Any other transition SHALL be rejected with a stable failure code `invalid-status-transition`.

The status a successful placement yields SHALL be derived from the site's `AutoConfirm`
setting: `Confirmed` when it is on, `Requested` when it is off. `AutoConfirm` SHALL default to
**on**, so a site that has configured nothing gets exactly the auto-confirm behaviour every
prior version shipped. The derivation SHALL happen at the single placement site both the
direct and the service paths run through, so the two cannot disagree about what a new booking
is.

`Declined` SHALL be produced only by the decline operation. `Requested` SHALL be produced only
by placement under `AutoConfirm` off. No other pathway SHALL produce either status.

#### Scenario: Placement under auto-confirm
- **WHEN** a booking is successfully placed while `AutoConfirm` is on
- **THEN** its status is `Confirmed`

#### Scenario: Placement under approval
- **WHEN** a booking is successfully placed while `AutoConfirm` is off
- **THEN** its status is `Requested`, and it holds its slot exactly as a `Confirmed` booking would

#### Scenario: Service placement agrees with direct placement
- **WHEN** a booking is successfully placed for a service while `AutoConfirm` is off
- **THEN** its status is `Requested`, the same status a direct placement yields under the same setting

#### Scenario: Cancelling a confirmed booking
- **WHEN** a `Confirmed` booking is cancelled
- **THEN** its status becomes `Cancelled`

#### Scenario: Cancelling a requested booking
- **WHEN** a `Requested` booking is cancelled
- **THEN** its status becomes `Cancelled` — a booker who withdraws does not need the request approved first

#### Scenario: Cancelling twice is rejected
- **WHEN** a `Cancelled` booking is cancelled again
- **THEN** the operation fails with code `invalid-status-transition` and the status remains `Cancelled`

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
- **THEN** the result is success and carries the created booking, in the status the *Booking status machine* requirement derives from the site's `AutoConfirm` setting

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

### Requirement: Availability and placement service ports
`UBookIt.Core` SHALL expose an availability query service (free-time and slot projection for a resource and date range, per `availability`) and a booking service (placement running the validation pipeline, cancellation, **confirmation and decline** applying the status machine, and **erasure of a booking's booker contact details**).

**Both SHALL depend only on ports `UBookIt.Core` itself defines**, so implementations can be swapped without changing Core and `UBookIt.Core` continues to carry **no package reference of any kind**. The availability service SHALL depend only on the two store ports (`IResourceStore`, `IBookingStore`). The booking service SHALL depend on those two and, additionally, on the **observation port** through which it reports what it has done — see *Placement and status changes are observable* — and on the **reference-generation port** from which it draws the quotable reference it assigns at placement.

**The generation port SHALL be a Core-defined port a host may substitute**, and it SHALL NOT be the thing that makes a reference unique — the store guarantees that, so the port promises only a well-formed reference drawn unpredictably. It exists as a port rather than as a helper so that a caller can hand placement a reference already in use and observe what it does; at 27⁸ values, waiting for a real collision is not a test strategy. Core MAY ship a default implementation, since drawing a random value needs no package reference and Core already generates identity inline.

*The enumeration is widened rather than dropped — for the second time, and on the same reasoning. What it protected is unchanged: the constraint was never about the number two. It was that Core owns its own dependencies, that a host can substitute any of them, and that nothing drags a framework into the domain — all of which a Core-defined observation port satisfies, and which an Umbraco type in this assembly would not.*

Core services concerned with services (per `service-booking`) MAY additionally depend on the service read port (`IServiceStore`). No Core service SHALL depend on a management store: the read ports are the only pathway anonymous delivery traffic reaches storage through.

The read port SHALL additionally expose a listing of every resource of a given type key, unpaged. It is unpaged deliberately — its consumer is a candidate pool, and a truncated pool silently produces a wrong answer rather than an error. This is distinct from the management port's type-key listing, which reports type keys with usage counts for a picker and SHALL remain management-only.

The booking store port SHALL additionally expose a claims read spanning several resource ids in one call, with the same half-open overlap and status-neutral semantics as the single-resource read. It exists so a query over a candidate pool does not issue one round trip per candidate; it SHALL return the same claims the single-resource read would return for each of those resources.

The availability query service SHALL additionally expose a pure bookable-start projection taking an already-loaded resource aggregate together with already-read claims, for callers that must issue no reads of their own. It SHALL produce results identical to the id-based query for the same resource, date range, and stored state — it changes only who performs the reads, never what is computed. Claims belonging to other resources SHALL be ignored rather than rejected, so one batched read can be passed for every candidate in turn.

Only one such member SHALL be added. An additional overload taking the resource but reading claims itself would leave the batched claims read without a caller on the path it exists to serve, and would ship untested public surface on a port interface.

#### Scenario: Services are testable with in-memory stores
- **WHEN** the availability and booking services are constructed with in-memory store implementations
- **THEN** all placement, cancellation, confirmation, decline, free-time, and slot-projection behaviour in these specs is exercisable without a database

#### Scenario: A host can substitute reference generation
- **WHEN** a caller constructs the booking service with its own implementation of the generation port
- **THEN** placement assigns the references that implementation returns, and a reference already in use is answered by drawing another rather than by failing the booking

#### Scenario: Core carries no framework dependency
- **WHEN** `UBookIt.Core`'s package references are inspected
- **THEN** there are none, and every port it depends on is a type it declares itself

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

**BREAKING — published port.** `IBookingStore` gains an operation that erases a booking's
booker. A host supplying its own store implementation must add it, and must satisfy the
absorption and irreversibility the port documents — those are guarantees the package makes to a
data subject, so they belong to the port rather than to one storage engine.

*The booking service's enumeration is widened a third time, on the reasoning already recorded
above: the constraint was never about how many verbs there are. Erasure is a domain operation
on a booking — it mutates the aggregate through a named method and persists through the store
port Core already defines — so it belongs to this service rather than beside it, and it drags
nothing new into Core. Listing it matters because this requirement is where a reader derives
what `UBookIt.Core`'s booking service IS; leaving it at two would describe a surface that no
longer exists.*

**BREAKING — published port, and the second such addition in this capability.** `IBookingStore`
gains a read returning the **ids** of bookings whose interval ended before a given instant and
whose booker has not been erased. A host supplying its own store implementation must add it.

**It returns identifiers rather than bookings, and that is a constraint on the port rather than a
convenience for its caller.** Its consumer is the unattended retention sweep, which per
`booker-erasure` must handle no contact detail at any point — that obligation is what allows an
erasure with no caller to exist without weakening the rule that only somebody permitted to read
contact details may destroy them. A substituted implementation that returned whole bookings would
satisfy the compiler and falsify the guarantee, so the port SHALL NOT be widened to carry a
booker, and a host implementing it inherits that restriction.

**It SHALL apply no status filter**, for the same reason the subject search carries none: a
booking that was cancelled, declined or never confirmed holds a real person's details exactly as
firmly as one that went ahead, and a filter here would be a way for the sweep to under-erase.

*The store port's enumeration grows for the same reason the service's did, and the note above
applies unchanged: the constraint was never about how many members there are. This one adds no
dependency to Core and no verb to the booking service — retention erases through the service
operation that already exists, so what is new here is only a way to find what the clock has
caught.*

*The booking service's enumeration is widened a **fourth** time, and the recorded reasoning
still holds: the constraint was never about how many verbs there are. Confirmation and decline
are domain operations on a booking — each drives a transition the status machine has declared
since v1 and persists through the store port Core already defines — so they belong to this
service rather than beside it, and they drag nothing new into Core.*

### Requirement: Placement and status changes are observable
`UBookIt.Core` SHALL report, through a port it declares itself, that a booking has been
**placed**, that one has been **confirmed**, that one has been **declined**, and that one has
been **cancelled**. A host SHALL be able to observe all four without the package sending
anything on its behalf.

**Each SHALL be reported only after storage has agreed, and only on success.** An event means
*this happened*; raising one before the store has committed would announce a booking that may
not exist, and raising one on failure would announce one that does not.

**Nothing an observer does SHALL affect the operation it is observing.** An observer that
throws SHALL NOT change what the caller is told, SHALL NOT undo the booking, and SHALL NOT
prevent the operation from reporting success. The booking is already stored by the time an
observer runs, so an exception escaping would tell a visitor their booking failed when it did
not — and a visitor told that books again. The failure this prevents is a double booking
caused by somebody else's handler, which is worse than any notification is valuable.

**The cost of that SHALL be stated rather than implied:** an observer that throws is a
notification nobody receives, and the package neither retries nor queues it.

A report SHALL carry the booking and nothing derived from it, so there is one source of truth
and no computed value to keep in step.

**No status-change report SHALL need to describe what changed.** The status machine permits
cancellation only from `Requested` or `Confirmed`, and confirmation and decline only from
`Requested`, so every report is by construction "this booking has just become what its status
says"; an attempt from any other status fails and reports nothing.

**BREAKING — published port.** The observation port gains a confirmed and a declined member. A
host supplying its own observer must add both. There SHALL be no default implementations: an
observer silently deaf to declines would be a worse outcome than a compile error, on a port
whose entire purpose is that a host hears what happened.

#### Scenario: A placed booking is reported
- **WHEN** a booking is placed successfully
- **THEN** the placement is reported once, after the booking is stored, carrying that booking

#### Scenario: A confirmed booking is reported
- **WHEN** a booking is confirmed successfully
- **THEN** the confirmation is reported once, after the status is stored, carrying that booking

#### Scenario: A declined booking is reported
- **WHEN** a booking is declined successfully
- **THEN** the decline is reported once, after the status is stored, carrying that booking

#### Scenario: A cancelled booking is reported
- **WHEN** a booking is cancelled successfully
- **THEN** the cancellation is reported once, after the status is stored, carrying that booking

#### Scenario: A failed placement reports nothing
- **WHEN** placement fails for any reason
- **THEN** nothing is reported

#### Scenario: A refused transition reports nothing
- **WHEN** confirmation, decline, or cancellation is attempted on a booking whose status does not permit it
- **THEN** it fails with `invalid-status-transition` and nothing is reported

#### Scenario: Cancelling an already-cancelled booking reports nothing
- **WHEN** cancellation is attempted on a booking that is already cancelled
- **THEN** it fails with `invalid-status-transition` and nothing is reported

#### Scenario: A throwing observer does not break the booking
- **WHEN** an observer throws while being told a booking was placed
- **THEN** the placement still reports success to its caller, and the booking remains stored and unchanged

#### Scenario: A throwing observer does not break a confirmation
- **WHEN** an observer throws while being told a booking was confirmed
- **THEN** the confirmation still reports success to its caller, and the stored status remains `Confirmed`

#### Scenario: Observation is substitutable
- **WHEN** the booking service is constructed with an observer that only records what it was told
- **THEN** placement, confirmation, decline, and cancellation behaviour is unchanged and every report is exercisable without a database

## RENAMED Requirements

- FROM: `### Requirement: Placement and cancellation are observable`
- TO: `### Requirement: Placement and status changes are observable`
