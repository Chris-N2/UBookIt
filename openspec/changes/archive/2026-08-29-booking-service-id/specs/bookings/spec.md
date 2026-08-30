## MODIFIED Requirements

### Requirement: Booking shape
A booking SHALL have a `Guid` id, exactly one continuous interval `[start, end)` held as UTC instants plus the IANA zone id it was placed against, a creation timestamp (UTC), a booker, a status, an **optional service attribution**, and a collection of 1..N resource claims. Each `ResourceClaim` SHALL bind exactly one resource to the booking's interval.

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
`UBookIt.Core` SHALL expose a public, additive rehydration factory (`Booking.Rehydrate`) that materializes a `Booking` from stored state: id, interval, booker, claims, status, created timestamp, and **the optional service attribution**. Rehydration SHALL enforce structural invariants (at least one claim; no duplicate resource per booking) and SHALL accept any `BookingStatus` without applying transition rules — the stored status is historical fact, not a transition. Rehydration SHALL NOT be usable to bypass placement validation: it is documented as a persistence-boundary API, and placement remains the only pathway that creates new bookings. (Discharges the deferred obligation recorded at core-domain archive, per design decision D9: downstream changes add Core surface via their own specs.)

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

## ADDED Requirements

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
