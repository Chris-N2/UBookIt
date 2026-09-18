## MODIFIED Requirements

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
