## ADDED Requirements

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

## MODIFIED Requirements

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

### Requirement: Availability and placement service ports
`UBookIt.Core` SHALL expose an availability query service (free-time and slot projection for a resource and date range, per `availability`) and a booking service (placement running the validation pipeline, and cancellation applying the status machine).

**Both SHALL depend only on ports `UBookIt.Core` itself defines**, so implementations can be swapped without changing Core and `UBookIt.Core` continues to carry **no package reference of any kind**. The availability service SHALL depend only on the two store ports (`IResourceStore`, `IBookingStore`). The booking service SHALL depend on those two and, additionally, on the **observation port** through which it reports what it has done — see *Placement and cancellation are observable* — and on the **reference-generation port** from which it draws the quotable reference it assigns at placement.

**The generation port SHALL be a Core-defined port a host may substitute**, and it SHALL NOT be the thing that makes a reference unique — the store guarantees that, so the port promises only a well-formed reference drawn unpredictably. It exists as a port rather than as a helper so that a caller can hand placement a reference already in use and observe what it does; at 28⁸ values, waiting for a real collision is not a test strategy. Core MAY ship a default implementation, since drawing a random value needs no package reference and Core already generates identity inline.

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
