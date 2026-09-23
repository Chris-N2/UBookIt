# bookings Specification

## Purpose

Defines the booking domain for uBookIt: booking shape and resource claims, the status machine, booker identity, the placement validation pipeline with stable failure codes, conflict semantics, the atomic placement contract, and the service/store ports exposed by `UBookIt.Core`.

## Requirements

### Requirement: Booking shape
A booking SHALL have a `Guid` id, **a quotable reference**, exactly one continuous interval `[start, end)` held as UTC instants plus the IANA zone id it was placed against, a creation timestamp (UTC), a booker, a status, an **optional service attribution**, and a collection of 1..N resource claims. Each `ResourceClaim` SHALL bind exactly one resource to the booking's interval.

A booking SHALL carry **one claim per role of the service it was placed for**, all over that one interval; direct placement, which names a single resource, SHALL continue to produce exactly one claim. The earlier rule that v1 behaviour enforces exactly one claim per booking is **lifted**: the model was always plural, and multi-role composition is what makes the plural case reachable.

A booking SHALL NOT claim the same resource twice.

**The service attribution SHALL be absent for a booking placed directly, and its absence
SHALL mean exactly that** — placed directly — rather than "not recorded". A resource
carries permission to be booked on its own per resource, and a resource withholding that
permission remains fully usable as part of a service, so both kinds of booking coexist
permanently on any site. A booking placed directly has no service and never will.

#### Scenario: Valid single-claim booking
- **WHEN** a booking is placed for one room resource for a valid interval
- **THEN** the booking has exactly one resource claim, referencing that resource, covering the booking interval

#### Scenario: Claims collection is plural by design
- **WHEN** the domain model's public surface is inspected
- **THEN** a booking exposes a collection of resource claims (not a single resource reference)

#### Scenario: A service booking carries one claim per role
- **WHEN** a service requiring a `room` and a `therapist` is booked
- **THEN** the booking carries two claims, one for each, both covering the booking's single interval

#### Scenario: A directly placed booking carries no service
- **WHEN** a resource that permits being booked on its own is booked directly
- **THEN** the resulting booking carries no service attribution, and that absence is the recorded fact rather than a missing value

### Requirement: Booking status machine
Booking status SHALL be one of `Requested`, `Confirmed`, `Declined`, `Cancelled`. Permitted transitions SHALL be exactly: `Requested → Confirmed`, `Requested → Declined`, `Requested → Cancelled`, and `Confirmed → Cancelled`. Any other transition SHALL be rejected with a stable failure code `invalid-status-transition`.

The status a successful **visitor** placement yields SHALL be derived from the site's `AutoConfirm`
setting: `Confirmed` when it is on, `Requested` when it is off. `AutoConfirm` SHALL default to
**on**, so a site that has configured nothing gets exactly the auto-confirm behaviour every
prior version shipped. The derivation SHALL happen at the single placement site the
direct, the service and the operator paths all run through, so they cannot disagree about what a
new booking is.

**A placement on a booker's behalf SHALL yield `Confirmed` whatever `AutoConfirm` says**, per
*Placing a booking on a booker's behalf*. The single derivation site therefore reads the terms
the placement was made under as well as the site's setting; it SHALL NOT be duplicated, and no
caller SHALL choose a status of its own. Approval is a gate on a stranger's request, and the
operator placing this one passed it by placing it.

`Declined` SHALL be produced only by the decline operation. `Requested` SHALL be produced only
by a **visitor's** placement under `AutoConfirm` off — an operator's placement never produces it,
under either setting. No other pathway SHALL produce either status.

#### Scenario: Placement under auto-confirm
- **WHEN** a booking is successfully placed while `AutoConfirm` is on
- **THEN** its status is `Confirmed`

#### Scenario: Placement under approval
- **WHEN** a visitor's booking is successfully placed while `AutoConfirm` is off
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

#### Scenario: An operator's placement under approval is confirmed
- **WHEN** a booking is successfully placed on a booker's behalf while `AutoConfirm` is off
- **THEN** its status is `Confirmed`, not `Requested`

#### Scenario: An operator's placement under auto-confirm is confirmed too
- **WHEN** a booking is successfully placed on a booker's behalf while `AutoConfirm` is on
- **THEN** its status is `Confirmed`, the same status the setting would have produced anyway

#### Scenario: The status is still decided in one place
- **WHEN** the direct, service and operator placement paths are each followed to where a new booking's status is chosen
- **THEN** all three reach the same single site, and none of them carries a status chosen by its caller

### Requirement: Booker identity
A booking's booker SHALL carry an optional opaque member key (`Guid?`, never an Umbraco type) and contact details: a non-empty name, a well-formed email address, and an optional phone number. Contact details SHALL be required regardless of whether a member key is present.

**BREAKING — published API.** The members carrying those details change shape: `Booker.Name`,
`Booker.Email` and `Booker.Phone`, all public in `0.1.0`, are replaced by a single nullable
`Booker.Contact`. The behaviour this requirement describes is unchanged for every booking a
placement creates; what changes is how a caller reads it. The break is stated here rather than
left to be discovered, because `UBookIt.Core` is published and its surface is a compatibility
promise.

**A booker SHALL be in one of exactly two states: carrying contact details, or erased.** An
erased booker carries no name, no email address, no phone number and no member key, and records
the instant at which the erasure happened. There is no third state, and in particular no state
in which contact details are present but empty.

**Placement SHALL only ever produce the first state.** The validation above is unchanged for
every booking the domain creates: a booking cannot be placed without a non-empty name and a
well-formed email. The erased state is reachable only by erasing an existing booking (see
"Erasing a booker's contact details") and by rehydrating one already erased from storage.

**The two states SHALL be distinguished by construction, not by inspecting values.** A booker
that neither carries contact details nor records an erasure SHALL NOT be constructible, and
reading a name or an email SHALL require having established that details are present.

#### Scenario: External booker without member record
- **WHEN** a booking is placed with no member key but with name and valid email
- **THEN** the booking is accepted

#### Scenario: Missing email is rejected
- **WHEN** a booking is placed with a name but no email address
- **THEN** placement fails with a validation failure identifying the email field

#### Scenario: Placement cannot produce an erased booker
- **WHEN** a booking is placed
- **THEN** its booker carries contact details and is not erased, whatever the request contained

#### Scenario: An erased booker carries nothing of the person
- **WHEN** a booker is erased
- **THEN** it reports its erasure and the instant of it, and carries no name, email, phone or member key

#### Scenario: Neither-state is not constructible
- **WHEN** a caller attempts to construct a booker with neither contact details nor an erasure instant
- **THEN** no such value can be produced

### Requirement: Placement validation pipeline
Booking placement SHALL validate a request against an ordered rule pipeline, producing a structured result: success, or failure carrying one stable machine-readable code per failed rule. The rules and codes SHALL be, in order: `interval-invalid` (end not after start, malformed, or not representable), `granularity` (start or duration not aligned), `duration-too-short`, `duration-too-long`, `lead-time` (starts sooner than minimum lead), `horizon` (starts beyond booking horizon), `outside-open-hours` (interval not fully inside open hours with the resource's own exceptions and any applicable site closures applied), `conflict` (overlaps a blocking claim). Start alignment is defined relative to the start of the coalesced open window containing the interval; when the interval lies outside open hours, start alignment cannot be evaluated and only `outside-open-hours` is reported. Failures SHALL NOT be signalled by exceptions.

A site closure the resource has not opted out of SHALL close its date for placement on the same terms as for availability, and SHALL be reported as `outside-open-hours` rather than as a code of its own. **The refusal SHALL NOT name the closure or its label**: placement is reachable anonymously, and the reason a date is shut is not a disclosure the booking path makes.

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

#### Scenario: Request on a site closure date
- **WHEN** placement is requested for an otherwise-valid interval on a date carrying a site closure the resource has not opted out of
- **THEN** the result is failure with code `outside-open-hours`, and the failure names neither the closure nor its label

#### Scenario: Request on a closure date the resource has opted out of
- **WHEN** placement is requested for an aligned, correctly sized interval inside open hours on a closure date the resource has opted out of
- **THEN** the result is success

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
`UBookIt.Core` SHALL expose an availability query service (free-time and slot projection for a resource and date range, per `availability`) and a booking service (placement running the validation pipeline, cancellation, **confirmation and decline** applying the status machine, **erasure of a booking's booker contact details**, and **moving a booking to a new interval** per *Moving a booking*, and **placing a booking on a booker's behalf** per *Placing a booking on a booker's behalf*, and **cancelling a booking on a visitor's terms** per `self-service-cancellation`).

**Both SHALL depend only on ports `UBookIt.Core` itself defines**, so implementations can be swapped without changing Core and `UBookIt.Core` continues to carry **no package reference of any kind**. The availability service SHALL depend only on the two store ports (`IResourceStore`, `IBookingStore`). The booking service SHALL depend on those two and, additionally, on the **observation port** through which it reports what it has done — see *Placement and status changes are observable* — and on the **reference-generation port** from which it draws the quotable reference it assigns at placement.

**The generation port SHALL be a Core-defined port a host may substitute**, and it SHALL NOT be the thing that makes a reference unique — the store guarantees that, so the port promises only a well-formed reference drawn unpredictably. It exists as a port rather than as a helper so that a caller can hand placement a reference already in use and observe what it does; at 27⁸ values, waiting for a real collision is not a test strategy. Core MAY ship a default implementation, since drawing a random value needs no package reference and Core already generates identity inline.

*The enumeration is widened rather than dropped — for the third time, and on the same reasoning. What it protected is unchanged: the constraint was never about the number two. It was that Core owns its own dependencies, that a host can substitute any of them, and that nothing drags a framework into the domain — all of which a Core-defined observation port satisfies, and which an Umbraco type in this assembly would not.*

Core services concerned with services (per `service-booking`) MAY additionally depend on the service read port (`IServiceStore`). No Core service SHALL depend on a management store: the read ports are the only pathway anonymous delivery traffic reaches storage through.

The read port SHALL additionally expose a listing of every resource of a given type key, unpaged. It is unpaged deliberately — its consumer is a candidate pool, and a truncated pool silently produces a wrong answer rather than an error. This is distinct from the management port's type-key listing, which reports type keys with usage counts for a picker and SHALL remain management-only.

The booking store port SHALL additionally expose a claims read spanning several resource ids in one call, with the same half-open overlap and status-neutral semantics as the single-resource read. It exists so a query over a candidate pool does not issue one round trip per candidate; it SHALL return the same claims the single-resource read would return for each of those resources.

The availability query service SHALL additionally expose a pure bookable-start projection taking an already-loaded resource aggregate together with already-read claims, for callers that must issue no reads of their own. It SHALL produce results identical to the id-based query for the same resource, date range, and stored state — it changes only who performs the reads, never what is computed. Claims belonging to other resources SHALL be ignored rather than rejected, so one batched read can be passed for every candidate in turn.

Only one such member SHALL be added. An additional overload taking the resource but reading claims itself would leave the batched claims read without a caller on the path it exists to serve, and would ship untested public surface on a port interface.

#### Scenario: Services are testable with in-memory stores
- **WHEN** the availability and booking services are constructed with in-memory store implementations
- **THEN** all placement, cancellation, confirmation, decline, free-time, and slot-projection behaviour in these specs is exercisable without a database

#### Scenario: Moving is testable with in-memory stores
- **WHEN** the booking service is constructed with an in-memory store implementation
- **THEN** every move behaviour in *Moving a booking* and *Atomic move contract* is exercisable without a database

#### Scenario: Operator placement is testable with in-memory stores
- **WHEN** the booking service is constructed with an in-memory store implementation
- **THEN** every behaviour in *Placing a booking on a booker's behalf* is exercisable without a database, and it needs no store member the other operations do not already use

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

**BREAKING — published port, the third such addition in this capability, and the first in a
release after 17.0.0.** `IBookingStore` gains the move write defined by *Atomic move contract*.
A host supplying its own store implementation must add it, and must satisfy that contract's
atomicity, self-exclusion and conditional-write obligations — they are what make a move safe
under concurrent placement, so they belong to the port rather than to one storage engine. It
lands in a minor release, which is the only place the versioning policy permits a break.

*The booking service's enumeration is widened a **fifth** time. Moving is a domain operation on
a booking — a named method on the aggregate, validated by the pipeline placement already runs,
persisted through the store port Core already defines — and it drags nothing new into Core.*

*The booking service's enumeration is widened a **sixth** time, and the recorded reasoning is
unchanged: the constraint was never about how many verbs there are. Operator placement is a
domain operation on a booking — it runs the pipeline placement already runs, assigns a
reference from the port Core already defines, and persists through the store's existing atomic
placement. **It adds no member to `IBookingStore`**, which is why this widening carries no
BREAKING note: what is new is a second set of terms to place under, not a second way to write.*

### Requirement: Placement and status changes are observable
`UBookIt.Core` SHALL report, through a port it declares itself, that a booking has been
**placed**, that one has been **confirmed**, that one has been **declined**, that one has
been **cancelled**, and that one has been **moved**. A host SHALL be able to observe all five
without the package sending anything on its behalf.

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
and no computed value to keep in step. **The move report additionally carries the interval the
booking held before the move.** That is not a derivation — it is a fact the booking no longer
holds and nothing else records — and it is carried so that whoever is told can say where the
booking moved *from*, which a message saying only where it now is cannot.

**No status-change report SHALL need to describe what changed, and the move report is the one
report that must.** The status machine permits cancellation only from `Requested` or
`Confirmed`, and confirmation and decline only from `Requested`, so each of those reports is by
construction "this booking has just become what its status says"; an attempt from any other
status fails and reports nothing. A move changes no status: the booking after is in every
respect the booking before except its interval, so "this booking has just moved" is meaningless
without the interval it left. The exception is named here, with its reason, so that it cannot be
read as licence for any other report to grow a before-and-after.

**BREAKING — published port.** The observation port gains a confirmed and a declined member. A
host supplying its own observer must add both. There SHALL be no default implementations: an
observer silently deaf to declines would be a worse outcome than a compile error, on a port
whose entire purpose is that a host hears what happened.

**BREAKING — published port, the second such addition, and the first after 17.0.0.** The
observation port gains a moved member, carrying the booking and its previous interval. A host
supplying its own observer must add it, and on the reasoning above there SHALL be no default
implementation. It lands in a minor release, the only place the versioning policy permits a
break.

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

#### Scenario: A moved booking is reported with where it came from
- **WHEN** a booking is moved successfully
- **THEN** the move is reported once, after the new interval is stored, carrying that booking as it now stands and the interval it held before

#### Scenario: A failed placement reports nothing
- **WHEN** placement fails for any reason
- **THEN** nothing is reported

#### Scenario: A refused transition reports nothing
- **WHEN** confirmation, decline, or cancellation is attempted on a booking whose status does not permit it
- **THEN** it fails with `invalid-status-transition` and nothing is reported

#### Scenario: A refused move reports nothing
- **WHEN** a move fails for any reason — a rule of the pipeline, an unchanged interval, a status that does not permit it, or a booking that does not exist
- **THEN** nothing is reported

#### Scenario: Cancelling an already-cancelled booking reports nothing
- **WHEN** cancellation is attempted on a booking that is already cancelled
- **THEN** it fails with `invalid-status-transition` and nothing is reported

#### Scenario: A throwing observer does not break the booking
- **WHEN** an observer throws while being told a booking was placed
- **THEN** the placement still reports success to its caller, and the booking remains stored and unchanged

#### Scenario: A throwing observer does not break a confirmation
- **WHEN** an observer throws while being told a booking was confirmed
- **THEN** the confirmation still reports success to its caller, and the stored status remains `Confirmed`

#### Scenario: A throwing observer does not break a move
- **WHEN** an observer throws while being told a booking was moved
- **THEN** the move still reports success to its caller, and the stored booking holds the new interval

#### Scenario: Observation is substitutable
- **WHEN** the booking service is constructed with an observer that only records what it was told
- **THEN** placement, confirmation, decline, cancellation and move behaviour is unchanged and every report is exercisable without a database

### Requirement: Booking rehydration
`UBookIt.Core` SHALL expose a public, additive rehydration factory (`Booking.Rehydrate`) that materializes a `Booking` from stored state: id, **reference**, interval, booker, claims, status, created timestamp, and **the optional service attribution**. The reference is **required**, not optional: a booking without one cannot be quoted, and accepting a default here would let storage produce one silently. Rehydration SHALL enforce structural invariants (at least one claim; no duplicate resource per booking) and SHALL accept any `BookingStatus` without applying transition rules — the stored status is historical fact, not a transition. Rehydration SHALL NOT be usable to bypass placement validation: it is documented as a persistence-boundary API, and placement remains the only pathway that creates new bookings. (Discharges the deferred obligation recorded at core-domain archive, per design decision D9: downstream changes add Core surface via their own specs.)

**Rehydration SHALL NOT revalidate the recorded service.** The stored attribution is
historical fact on the same terms as the stored status: the service may since have been
renamed, retired or deleted, and none of that changes what the booking was placed for.

#### Scenario: Rehydrated booking is faithful
- **WHEN** a booking is rehydrated with a `Declined` status and two claims on distinct resources
- **THEN** the resulting `Booking` reports exactly that status and those claims, and its transition methods still enforce the status machine from the current state

#### Scenario: Structural invariants still hold
- **WHEN** rehydration is attempted with zero claims or with two claims on the same resource
- **THEN** rehydration fails; no `Booking` is produced

#### Scenario: A recorded service survives its service being unavailable
- **WHEN** a booking is rehydrated carrying a service attribution that no longer resolves to an existing service
- **THEN** rehydration succeeds and reports that attribution unchanged

### Requirement: Booking a single resource requires that resource to permit it
**Visitor** placement of a booking that claims **one resource, named by the caller** SHALL be
refused when that resource withholds permission to be booked on its own, with the
stable code `resource-not-directly-bookable`.

The refusal SHALL be evaluated in `UBookIt.Core`, on the visitor's single-resource
placement entry point, **before** the request is composed into a claim set. Placing it in a
transport layer would leave every other caller open — the no-JavaScript booking
flow reaches placement in process without crossing an HTTP boundary at all — and
the rule is a property of booking, not of any one way of asking for one.

Placement that claims resources **derived from a service's roles** SHALL NOT be
subject to this rule, whatever those resources permit. The distinction SHALL be
structural rather than a flag on the request: a single-resource entry point *is*
the direct path, and service placement composes its own claim set without passing
through it. A parameter asserting "this is a direct booking" would restate what the
call site already means, in a form a caller can get wrong.

**`UBookIt.Core` SHALL offer more than one single-resource placement entry point, and which
one a caller reaches SHALL be what decides whether this rule applies.** The visitor's entry
point carries it. **Operator placement on a booker's behalf SHALL NOT**, per *Placing a
booking on a booker's behalf*: the permission exists so that a stranger cannot assemble a
combination the business cannot deliver — a therapist with no room — and an operator taking a
booking at the desk is the person the site trusts to make exactly that judgement. A rule that
refused them would be protecting the site from its own staff.

**That waiver SHALL be structural on the same terms as the service exemption, and for the same
reason.** A parameter asserting "this caller is an operator" would restate the call site in a
form a caller can get wrong, and the value that already carries an operator's terms SHALL NOT
acquire a member for it: the rule is not evaluated on the pipeline's terms, it is not reached
at all. Adding a third policy member to those terms would put a rule in two places and let them
disagree.

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

#### Scenario: An operator books a withholding resource directly
- **WHEN** an operator places a booking on a booker's behalf naming a single resource that withholds direct booking, at a time it is otherwise free and open
- **THEN** placement succeeds, and the booking claims that resource exactly as any other booking would

#### Scenario: The visitor path is unchanged by the operator path existing
- **WHEN** a visitor places a booking naming a resource that withholds direct booking, on an installation where operator placement is available
- **THEN** it fails with `resource-not-directly-bookable` exactly as before, and no visitor-reachable parameter, header or request field can select the operator's entry point

#### Scenario: The waiver is not a member of the operator's terms
- **WHEN** the value carrying an operator's placement terms is inspected
- **THEN** it carries no member concerning direct bookability, because that rule is decided by which entry point was called rather than evaluated on those terms

### Requirement: A booking records the service it was placed for
Placement SHALL record, on the booking it produces, the service that produced it — or
record no service, for a booking that was not placed through one.

**The recorded service SHALL NOT be settable by a caller of the general multi-claim
placement contract.** It SHALL be supplied only by the placement entry point that exists
for service placement, so that naming a service is inseparable from actually placing
through one. A field on the existing request would let any caller assert an association
the placement did not make — a booking attributed to a service whose roles its resources
do not satisfy — and that failure is silent, permanent, and indistinguishable later from
a real attribution. This is the same reasoning that already keeps direct placement a
separate entry point rather than a flag on a request.

A booking SHALL retain its recorded service regardless of what later happens to that
service. The attribution SHALL NOT be cleared, rewritten or cascaded when the service is
renamed, retired or deleted, because the booking does not stop having been placed for it.

#### Scenario: A service booking records its service
- **WHEN** a booking is placed through a service
- **THEN** the stored booking reports that service

#### Scenario: The general placement contract cannot name a service
- **WHEN** the multi-claim placement contract's request is inspected
- **THEN** it carries no service member, and a booking placed through it reports no service

#### Scenario: A booking outlives the service it names
- **WHEN** the service a stored booking was placed for is deleted
- **THEN** the booking still reports that it was placed for that service, and is neither removed nor re-reported as a direct booking

### Requirement: A booking carries an identifier a person can use
Every booking SHALL carry, in addition to its identifier, a **reference**: a short string that
a person can read aloud, write down, type back, and quote to somebody else.

The two identifiers are deliberate and SHALL NOT be collapsed. The existing identifier remains
what machines use — the primary key, and the value carried in routes and payloads — and SHALL
remain opaque. The reference exists for the reader who is holding a telephone.

**The reference SHALL be unambiguous when transcribed.** It SHALL NOT contain characters that
are confused with one another when spoken, handwritten or typed, and SHALL NOT be
case-sensitive: a reference read back in lower case is the same reference. A reference that has
to be spelled out twice has failed at the one job it has.

**The reference SHALL NOT be capable of spelling a word.** A booking system that emails a
customer a reference which happens to read as an obscenity has a problem it cannot apologise
its way out of, and the class is removed by construction rather than by a filter list.

**The alphabet SHALL therefore contain no vowel, and `Y` is a vowel.** Stating it explicitly
because omitting `AEIOU` alone does not deliver the guarantee: `Y` carries *myth*, *gym* and
*crypt*, and it is the substitution used to write offensive words where vowels are filtered.
An alphabet that keeps it satisfies a rule about five letters while failing the sentence above.

**The reference SHALL be unique within a site**, and that uniqueness SHALL be **guaranteed by
the store itself** rather than rest on a check performed beforehand — a check followed by a
write is a race, and two bookings sharing a reference makes both unquotable. A store MAY also
check first so that a collision can be reported and answered rather than thrown; what it MUST
NOT do is let that check be the thing uniqueness depends on.

**The reference SHALL be assigned when the booking is placed and SHALL never change
thereafter** — not when a booking is confirmed, declined or cancelled, and not if the booking's
time is later amended. The customer is holding the reference they were given; a system that
changes it denies all knowledge of the booking the person is asking about.

**A reference SHALL remain meaningful when the booker's details do not.** A booking whose
personal data has been removed still occupies its interval and still has to be discussable, so
the reference SHALL NOT be derived from, or depend on, anything about the person.

#### Scenario: A reference can be read out
- **WHEN** a booking is placed
- **THEN** it carries a reference composed only of characters that are unambiguous when spoken or typed, which a person could dictate over a telephone without spelling anything out

#### Scenario: Case does not change which booking is meant
- **WHEN** a reference is compared with one written in a different case
- **THEN** they identify the same booking

#### Scenario: A reference cannot spell a word
- **WHEN** references are generated
- **THEN** the alphabet they are drawn from cannot produce a word, so no reference can be an accidental obscenity

#### Scenario: Two bookings cannot share a reference
- **WHEN** a reference that already exists would be assigned to a new booking
- **THEN** the store refuses it, and the placement either receives a different reference or fails — it never succeeds with a duplicate

#### Scenario: The reference outlives every change to the booking
- **WHEN** a booking is confirmed, declined or cancelled
- **THEN** its reference is the one it was given when it was placed

#### Scenario: The machine identifier is unaffected
- **WHEN** a booking gains a reference
- **THEN** its existing identifier is unchanged, and remains what routes and payloads carry

### Requirement: Erasing a booker's contact details

`UBookIt.Core` SHALL expose an operation on a booking that replaces its booker with an erased
one, recording the instant of erasure, and a service-level verb that performs it against stored
state.

**The operation SHALL change nothing else about the booking.** Its id, reference, interval,
time zone, status, creation time, claims and service attribution SHALL be unaffected — in
particular, erasing a booker SHALL NOT change whether the booking blocks time.

**It SHALL mirror the status machine's shape.** Status is mutated by named transition methods
rather than by a setter; the booker is mutated the same way, by a single named operation on the
aggregate, so that there is one way a booking's booker can change and it is named after what it
does.

**The erasure instant SHALL come from the same clock as placement**, so that a booking's
creation time and its erasure time are comparable and neither is taken from ambient system
time at an arbitrary layer.

**Erasing an already-erased booking SHALL succeed and change nothing**, including the recorded
instant, which remains that of the first erasure. See the `booker-erasure` capability for why
this is deliberately unlike cancellation.

**Rehydration SHALL accept an erased booker** on the same terms it accepts any stored status:
stored state is historical fact, and a booking that was erased must remain readable. Its
structural invariants — at least one claim, no duplicate resource — are unchanged, and it
SHALL NOT become a route to producing an erased booker for a booking that was not erased.

#### Scenario: Erasing replaces the booker and nothing else
- **WHEN** a booking's booker is erased
- **THEN** the booking's id, reference, interval, time zone, status, creation time, claims and service attribution are unchanged, and its booker is erased

#### Scenario: An erased booking still blocks
- **WHEN** a booking with a blocking status has its booker erased
- **THEN** it still reports that it blocks, and still conflicts with an overlapping placement on a claimed resource

#### Scenario: The erasure instant comes from the injected clock
- **WHEN** a booking is erased with the clock set to a known instant
- **THEN** the recorded erasure instant is that instant

#### Scenario: A second erasure keeps the first instant
- **WHEN** an erased booking is erased again at a later instant
- **THEN** the operation succeeds and the recorded instant is still the earlier one

#### Scenario: An erased booking rehydrates
- **WHEN** a booking whose booker was erased is materialized from stored state
- **THEN** it rehydrates successfully, reporting an erased booker and the stored erasure instant

#### Scenario: Erasing a booking that does not exist fails
- **WHEN** the erase verb is called with an id no booking has
- **THEN** it fails with a stable failure code and nothing is changed

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

### Requirement: Moving a booking
The booking service SHALL expose an operation that moves a booking: given a booking's id, a
new start instant and a new length, it SHALL place that same booking at the new interval.
**A move is a placement of the booking it already is.** The booking's id, reference, status,
booker, service attribution and every resource claim SHALL be exactly what they were before;
only the interval changes, and the zone id the booking carries becomes the zone the new
interval was validated against, on the same terms as any placement.

**A move SHALL be permitted from `Requested` and `Confirmed` and from nothing else.** A
cancelled or declined booking holds no time to move. An attempt from any other status SHALL
fail with `invalid-status-transition`, the store SHALL NOT be touched, and nothing SHALL be
reported. The status machine's four transitions are unchanged: a move is not a transition,
and a moved booking's status is the status it had.

**The new interval SHALL run the placement validation pipeline** — the same rules, in the same
order, producing the same stable codes, against every resource the booking claims — with two
rules evaluated on an operator's terms rather than a visitor's:

- the **lead-time** rule SHALL be evaluated with a lead of **zero** rather than the resource's
  configured lead, so a booking can be moved to any instant that has not yet passed and to none
  that has. An operator moving a booking closer in is the person the site trusts to decide
  that; the guard that remains is the one nobody can be trusted to waive, since a booking in
  the past holds a slot that cannot be used and moves the booking into the retention sweep's
  window;
- the **horizon** rule SHALL NOT be applied.

**That relaxation is a property of operator placement, not of moving**, and it is stated here
so that recording a booking on a customer's behalf, when that arrives, inherits it rather than
inventing a third set of terms. Open hours, granularity, duration bounds and conflict are
facts about what is physically bookable and SHALL bind an operator exactly as they bind a
visitor.

**A move to the interval the booking already holds SHALL be refused** with a new stable code,
`interval-unchanged`, on the same grounds as cancelling twice: a caller told "moved" when
nothing changed cannot tell a completed action from a rejected one. The check SHALL run before
any store write — the booking and its resources are necessarily read first.

**Moving SHALL keep every claim.** A booking placed for a service moves with the resources it
was assigned; no assignment is re-run, and a claimed resource that is unavailable at the new
interval SHALL produce `conflict` (or the open-hours failure, as the pipeline decides) rather
than a substitution. Changing which resources a booking claims is a different operation, not
built here.

**The booking service's move knows resources and nothing about services**, on the same split
placement has. The length rule a service adds — the intersection of its duration specification
with each claimed resource's range — is applied by the service booking service's move, which
is the operator's entry point for both kinds of booking; see `service-booking`, *Moving a
booking placed for a service applies the service's length rules*. A caller reaching the
booking service's move directly for a service booking gets the resources' rules alone, and the
port documents that.

**An unknown booking SHALL fail with `booking-not-found`.** No other new failure code is
introduced.

**An erased booker's booking SHALL move on the same terms as any other.** Erasure removed the
person's details, not the booking's claim on its time.

#### Scenario: A confirmed booking is moved to a free interval
- **WHEN** the move operation is called for a `Confirmed` booking with a new start and length that every claimed resource would accept
- **THEN** the result is success, the stored booking holds the new interval and no longer holds the old one, and its reference, status, booker, service and claims are unchanged

#### Scenario: A requested booking moves without changing status
- **WHEN** the move operation is called for a `Requested` booking
- **THEN** the stored booking holds the new interval and remains `Requested`

#### Scenario: Moving a cancelled booking is refused
- **WHEN** the move operation is called for a `Cancelled` or `Declined` booking
- **THEN** it fails with `invalid-status-transition`, the stored booking is unchanged, and nothing is reported

#### Scenario: A move inside the resource's lead time succeeds
- **WHEN** a resource requires 24 hours' notice and a booking on it is moved to an instant one hour from now
- **THEN** the move succeeds, because operator placement evaluates lead time as zero

#### Scenario: A move into the past is refused
- **WHEN** a booking is moved to an interval whose start has already passed
- **THEN** it fails with `lead-time`, and the stored booking is unchanged

#### Scenario: A move beyond the horizon succeeds
- **WHEN** a resource's booking horizon is 90 days and a booking on it is moved to a date 120 days ahead
- **THEN** the move succeeds, because operator placement applies no horizon

#### Scenario: A move outside open hours is refused
- **WHEN** a booking is moved to an interval not fully inside the claimed resource's open hours
- **THEN** it fails with `outside-open-hours`, exactly as a placement there would

#### Scenario: A move onto another booking is refused
- **WHEN** a booking is moved to an interval that overlaps a blocking booking on one of its claimed resources
- **THEN** it fails with `conflict`, and the stored booking still holds its original interval

#### Scenario: A move to the interval already held is refused
- **WHEN** the move operation is called with the booking's own current start and length
- **THEN** it fails with `interval-unchanged` and nothing is written

#### Scenario: A service booking moves with its claims
- **WHEN** a booking placed for a two-role service is moved to an interval at which both claimed resources are free
- **THEN** it holds the new interval with the same two claims, and no other resource is considered

#### Scenario: A service booking whose resource is busy does not swap it
- **WHEN** a booking placed for a service is moved to an interval at which one of its claimed resources is already booked, while another eligible resource is free
- **THEN** it fails with `conflict` rather than claiming the free resource

#### Scenario: A booking with an erased booker can move
- **WHEN** the move operation is called for a booking whose booker has been erased
- **THEN** it succeeds on the same terms as any other, and the booker remains erased

#### Scenario: An unknown booking is reported as not found
- **WHEN** the move operation is called with an id no booking has
- **THEN** it fails with `booking-not-found` and nothing is reported

### Requirement: Atomic move contract
The booking store port SHALL define a move write: given a booking's id, its new interval, and
the statuses from which a move is permitted, it SHALL release the booking's claim on its old
interval and take its claim on the new one **in one atomic step** with respect to conflict
detection. At no instant SHALL a concurrent placement observe the booking holding both
intervals or neither.

**The conflict check SHALL exclude the booking being moved.** A booking's own claim rows
overlap its own new interval whenever the two intervals overlap; a check that counted them
would refuse every small shift.

**The check and the write SHALL run under the same locks placement takes**, for every resource
the booking claims, in the same deterministic order, so that a move and a placement on the
same resource cannot both succeed for overlapping intervals, and two moves sharing resources
cannot deadlock.

**The write SHALL be conditional on the stored status still permitting a move.** The service
reads the booking, decides, then writes; a cancellation committing between the two would
otherwise let a cancelled booking move. The condition SHALL be inside the write, and a write
that changed no row because the status had moved on SHALL be reported to the caller as
`invalid-status-transition` rather than as success.

**The write SHALL touch the interval columns and nothing else** — not the status, not the
booker — on the same disjoint-columns reasoning the status and erasure writes already follow.

Core defines this contract and SHALL honour it in its in-memory test double.

#### Scenario: A move and a placement race for the new interval
- **WHEN** a move to interval I and a placement at interval I on the same resource execute concurrently
- **THEN** exactly one succeeds and the other fails with `conflict`

#### Scenario: A placement takes the old interval only after the move commits
- **WHEN** a booking is moved from interval I to interval J and, concurrently, a placement is attempted at I on the same resource
- **THEN** the placement succeeds only if the move has committed, and never while the booking still holds I

#### Scenario: A move does not conflict with itself
- **WHEN** a booking holding 09:00–10:00 is moved to 09:30–10:30 on a resource with no other booking
- **THEN** the move succeeds

#### Scenario: A cancel landing between read and write wins
- **WHEN** the move operation has read a `Confirmed` booking and, before its write, another caller cancels that booking
- **THEN** the move fails with `invalid-status-transition`, the booking remains `Cancelled`, and its interval is unchanged

#### Scenario: Two moves of the same booking
- **WHEN** two moves of the same booking to different free intervals execute concurrently
- **THEN** both complete without deadlock, the booking ends holding exactly one of the two intervals, and no claim on the other remains

#### Scenario: The move write leaves the status and booker alone
- **WHEN** a booking is moved while a stale aggregate of it would have carried a different status or booker
- **THEN** the stored status and booker are what the store held, not what the aggregate carried

### Requirement: Placing a booking on a booker's behalf
The booking service SHALL expose placement **on a booker's behalf**: given a resource, a start
instant, a length and the booker's details, it SHALL place a booking for that booker exactly as
a visitor's placement would, save for the terms named below.

**It SHALL be TWO members, not one**, mirroring the two the visitor path already has: one
naming a resource, and one naming the service a booking was placed for. A host implementing
this port must add both, and the count is stated because a change that declares port breaks
must count them. The
booking it produces SHALL be an ordinary booking in every later respect — the same reference
format drawn from the same port, the same status machine, the same claims, and the same
cancellation, confirmation, decline, move and erasure operate on it unchanged.

**The request SHALL run the placement validation pipeline** — the same rules, in the same
order, producing the same stable codes — with the two policy rules evaluated on an operator's
terms rather than a visitor's, exactly as *Moving a booking* already defines them:

- the **lead-time** rule SHALL be evaluated with a lead of **zero** rather than the resource's
  configured lead, so a booking may be taken for any instant that has not yet passed and for
  none that has;
- the **horizon** rule SHALL NOT be applied.

**These SHALL be the same terms a move places under, not a second set.** The value that carries
them is shared, and the rule bodies SHALL be identical for both callers: a test that the
visitor path is unchanged is a test that the terms differ and that nothing else does.

**Open hours, granularity, duration bounds and conflict SHALL bind an operator exactly as they
bind a visitor.** They are facts about what is physically bookable rather than policy about who
is asking, and an operator who could override them would be recording a booking the business
cannot honour.

**A booking placed on a booker's behalf SHALL be `Confirmed`, whatever the site's `AutoConfirm`
setting says.** Approval exists so that a stranger's request can be reviewed before the time is
committed; the operator placing this one has reviewed it by placing it, and leaving it
`Requested` would ask them to approve their own action — a queue item that means nothing and
that the booker's message would describe as awaiting a decision nobody is waiting for.

**That decision SHALL be taken where a placement's status is already decided**, so that the two
kinds of placement cannot come to disagree about what a new booking is. It SHALL be expressed as
a property of the terms the placement is made under and SHALL NOT be a second reading of the
site's settings.

**The booker's details SHALL be validated exactly as a visitor's are.** A name and a
well-formed email address are required; a telephone number is optional. Operator placement
SHALL NOT create a booker in any state a visitor's placement could not create, and in
particular SHALL NOT produce one without contact details — the two states a booker has are
unchanged, and placement still reaches only the first of them.

**Operator placement SHALL NOT set a member key.** A booking taken at a desk is not evidence
that the person holding it is a member of the site, and recording one on the operator's say-so
would attach a booking to an identity nobody verified.

**An operator's placement SHALL be reported as its own observation.** The observation port
SHALL gain a member for it, for the reason confirming, declining and moving each have one: they
are distinct **acts**, not distinct kinds of booking. Because the booking carries no marker of
who placed it, who placed it is knowable at the moment of placing and at no other — an observer
told only "a booking was placed" could not recover it afterwards, and the package's own
notification adapter must, since the site's internal recipients are not written to.

**That member SHALL default to reporting an ordinary placement, so it is an addition and not a
break.** An implementation written before operator placement existed SHALL keep compiling and
SHALL keep being told that a booking was placed — which is true, and is what such an
implementation meant. The default SHALL err in the direction of under-reporting the
distinction: it SHALL never invent one, and SHALL never lose the placement itself.

**A refused placement SHALL report nothing at all**, exactly as a refused placement always has.

#### Scenario: An operator places a booking for a booker
- **WHEN** the operation is called with a resource, a start and length that resource would accept, and a booker's name and email
- **THEN** a booking is placed for that booker, carrying a reference, claiming that resource over that interval

#### Scenario: Placement inside the resource's lead time succeeds
- **WHEN** a resource requires 24 hours' notice and a booking is placed on a booker's behalf for an instant one hour from now
- **THEN** placement succeeds, because operator placement evaluates lead time as zero

#### Scenario: Placement in the past is refused
- **WHEN** a booking is placed on a booker's behalf for an interval whose start has already passed
- **THEN** it fails with `lead-time`, and nothing is persisted

#### Scenario: Placement beyond the horizon succeeds
- **WHEN** a resource's booking horizon is 90 days and a booking is placed on a booker's behalf for a date 120 days ahead
- **THEN** placement succeeds, because operator placement applies no horizon

#### Scenario: Open hours still bind an operator
- **WHEN** a booking is placed on a booker's behalf for an interval outside the resource's open hours
- **THEN** it fails with `outside-open-hours`, and nothing is persisted

#### Scenario: A conflict still binds an operator
- **WHEN** a booking is placed on a booker's behalf over an interval a blocking claim already holds
- **THEN** it fails with `conflict`, and nothing is persisted

#### Scenario: Duration bounds and granularity still bind an operator
- **WHEN** a booking is placed on a booker's behalf for a length below the resource's minimum, or for a start the resource's granularity does not admit
- **THEN** it fails with the pipeline's own stable code for that rule, and nothing is persisted

#### Scenario: An operator's booking is confirmed on an approval site
- **WHEN** a booking is placed on a booker's behalf while the site's `AutoConfirm` setting is off
- **THEN** the booking's status is `Confirmed`

#### Scenario: A visitor's booking is unaffected by that decision
- **WHEN** a visitor places a booking while the site's `AutoConfirm` setting is off
- **THEN** the booking's status is `Requested`, exactly as before

#### Scenario: A booker without an email is refused
- **WHEN** a booking is placed on a booker's behalf with no email address, or with one that is not well formed
- **THEN** placement is refused on the same terms a visitor's placement is refused, and nothing is persisted

#### Scenario: The operator's booking carries no member key
- **WHEN** a booking placed on a booker's behalf is read back
- **THEN** its booker holds contact details and no member key

#### Scenario: An operator's booking behaves as any other afterwards
- **WHEN** a booking placed on a booker's behalf is subsequently confirmed, declined, cancelled, moved or erased
- **THEN** each operation behaves exactly as it does for a booking a visitor placed, and the booking carries no marker distinguishing how it was taken

#### Scenario: An operator's placement is reported as its own observation
- **WHEN** a booking is placed on a booker's behalf and an observer is attached
- **THEN** the observer is told of an operator's placement, and not of an ordinary one

#### Scenario: A visitor's placement is reported as it always was
- **WHEN** a visitor places a booking and an observer is attached
- **THEN** the observer is told of a placement, exactly as before

#### Scenario: An observer written before this change still hears the placement
- **WHEN** a booking is placed on a booker's behalf and the attached observer implements only the members that existed before operator placement
- **THEN** it compiles unchanged and is told that a booking was placed

#### Scenario: A refused operator placement reports nothing
- **WHEN** an operator's placement is refused by any rule
- **THEN** no observation of any kind is made
