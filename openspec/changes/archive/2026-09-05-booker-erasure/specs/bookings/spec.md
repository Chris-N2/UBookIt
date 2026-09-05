## MODIFIED Requirements

### Requirement: Availability and placement service ports
`UBookIt.Core` SHALL expose an availability query service (free-time and slot projection for a resource and date range, per `availability`) and a booking service (placement running the validation pipeline, cancellation applying the status machine, and **erasure of a booking's booker contact details**).

**Both SHALL depend only on ports `UBookIt.Core` itself defines**, so implementations can be swapped without changing Core and `UBookIt.Core` continues to carry **no package reference of any kind**. The availability service SHALL depend only on the two store ports (`IResourceStore`, `IBookingStore`). The booking service SHALL depend on those two and, additionally, on the **observation port** through which it reports what it has done — see *Placement and cancellation are observable* — and on the **reference-generation port** from which it draws the quotable reference it assigns at placement.

**The generation port SHALL be a Core-defined port a host may substitute**, and it SHALL NOT be the thing that makes a reference unique — the store guarantees that, so the port promises only a well-formed reference drawn unpredictably. It exists as a port rather than as a helper so that a caller can hand placement a reference already in use and observe what it does; at 27⁸ values, waiting for a real collision is not a test strategy. Core MAY ship a default implementation, since drawing a random value needs no package reference and Core already generates identity inline.

*The enumeration is widened rather than dropped — for the second time, and on the same reasoning. What it protected is unchanged: the constraint was never about the number two. It was that Core owns its own dependencies, that a host can substitute any of them, and that nothing drags a framework into the domain — all of which a Core-defined observation port satisfies, and which an Umbraco type in this assembly would not.*

Core services concerned with services (per `service-booking`) MAY additionally depend on the service read port (`IServiceStore`). No Core service SHALL depend on a management store: the read ports are the only pathway anonymous delivery traffic reaches storage through.

The read port SHALL additionally expose a listing of every resource of a given type key, unpaged. It is unpaged deliberately — its consumer is a candidate pool, and a truncated pool silently produces a wrong answer rather than an error. This is distinct from the management port's type-key listing, which reports type keys with usage counts for a picker and SHALL remain management-only.

The booking store port SHALL additionally expose a claims read spanning several resource ids in one call, with the same half-open overlap and status-neutral semantics as the single-resource read. It exists so a query over a candidate pool does not issue one round trip per candidate; it SHALL return the same claims the single-resource read would return for each of those resources.

The availability query service SHALL additionally expose a pure bookable-start projection taking an already-loaded resource aggregate together with already-read claims, for callers that must issue no reads of their own. It SHALL produce results identical to the id-based query for the same resource, date range, and stored state — it changes only who performs the reads, never what is computed. Claims belonging to other resources SHALL be ignored rather than rejected, so one batched read can be passed for every candidate in turn.

Only one such member SHALL be added. An additional overload taking the resource but reading claims itself would leave the batched claims read without a caller on the path it exists to serve, and would ship untested public surface on a port interface.

#### Scenario: Services are testable with in-memory stores
- **WHEN** the availability and booking services are constructed with in-memory store implementations
- **THEN** all placement, cancellation, free-time, and slot-projection behaviour in these specs is exercisable without a database

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

## ADDED Requirements

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
