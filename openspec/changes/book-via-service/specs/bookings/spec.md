## MODIFIED Requirements

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
