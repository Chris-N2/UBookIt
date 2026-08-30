## MODIFIED Requirements

### Requirement: Availability and placement service ports
`UBookIt.Core` SHALL expose an availability query service (free-time and slot projection for a resource and date range, per `availability`) and a booking service (placement running the validation pipeline, and cancellation applying the status machine).

**Both SHALL depend only on ports `UBookIt.Core` itself defines**, so implementations can be swapped without changing Core and `UBookIt.Core` continues to carry **no package reference of any kind**. The availability service SHALL depend only on the two store ports (`IResourceStore`, `IBookingStore`). The booking service SHALL depend on those two and, additionally, on the **observation port** through which it reports what it has done — see *Placement and cancellation are observable*.

*The enumeration is widened rather than dropped, and what it protected is unchanged: the constraint was never about the number two. It was that Core owns its own dependencies, that a host can substitute any of them, and that nothing drags a framework into the domain — all of which a Core-defined observation port satisfies, and which an Umbraco type in this assembly would not.*

Core services concerned with services (per `service-booking`) MAY additionally depend on the service read port (`IServiceStore`). No Core service SHALL depend on a management store: the read ports are the only pathway anonymous delivery traffic reaches storage through.

The read port SHALL additionally expose a listing of every resource of a given type key, unpaged. It is unpaged deliberately — its consumer is a candidate pool, and a truncated pool silently produces a wrong answer rather than an error. This is distinct from the management port's type-key listing, which reports type keys with usage counts for a picker and SHALL remain management-only.

The booking store port SHALL additionally expose a claims read spanning several resource ids in one call, with the same half-open overlap and status-neutral semantics as the single-resource read. It exists so a query over a candidate pool does not issue one round trip per candidate; it SHALL return the same claims the single-resource read would return for each of those resources.

The availability query service SHALL additionally expose a pure bookable-start projection taking an already-loaded resource aggregate together with already-read claims, for callers that must issue no reads of their own. It SHALL produce results identical to the id-based query for the same resource, date range, and stored state — it changes only who performs the reads, never what is computed. Claims belonging to other resources SHALL be ignored rather than rejected, so one batched read can be passed for every candidate in turn.

Only one such member SHALL be added. An additional overload taking the resource but reading claims itself would leave the batched claims read without a caller on the path it exists to serve, and would ship untested public surface on a port interface.

#### Scenario: Services are testable with in-memory stores
- **WHEN** the availability and booking services are constructed with in-memory store implementations
- **THEN** all placement, cancellation, free-time, and slot-projection behaviour in these specs is exercisable without a database

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

## ADDED Requirements

### Requirement: Placement and cancellation are observable
`UBookIt.Core` SHALL report, through a port it declares itself, that a booking has been
**placed** and that a booking has been **cancelled**. A host SHALL be able to observe both
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
and no computed value to keep in step.

**The cancellation report SHALL NOT need to describe what changed.** The status machine
permits cancellation only from `Requested` or `Confirmed`, so a report is by construction "this
booking has just become cancelled"; cancelling an already-cancelled booking fails and reports
nothing.

#### Scenario: A placed booking is reported
- **WHEN** a booking is placed successfully
- **THEN** the placement is reported once, after the booking is stored, carrying that booking

#### Scenario: A cancelled booking is reported
- **WHEN** a booking is cancelled successfully
- **THEN** the cancellation is reported once, after the status is stored, carrying that booking

#### Scenario: A failed placement reports nothing
- **WHEN** placement fails for any reason
- **THEN** nothing is reported

#### Scenario: Cancelling an already-cancelled booking reports nothing
- **WHEN** cancellation is attempted on a booking that is already cancelled
- **THEN** it fails with `invalid-status-transition` and nothing is reported

#### Scenario: A throwing observer does not break the booking
- **WHEN** an observer throws while being told a booking was placed
- **THEN** the placement still reports success to its caller, and the booking remains stored and unchanged

#### Scenario: Observation is substitutable
- **WHEN** the booking service is constructed with an observer that only records what it was told
- **THEN** placement and cancellation behaviour is unchanged and every report is exercisable without a database
