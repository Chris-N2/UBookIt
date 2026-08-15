## MODIFIED Requirements

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

### Requirement: Atomic placement contract
The booking store port (`IBookingStore`) SHALL define placement as atomic with respect to conflict detection: between the conflict check and the persistence of a new booking's claims, no other placement for an overlapping interval on **any** of the booking's claimed resources may succeed. Under concurrent placement of conflicting requests, exactly one SHALL succeed and the others SHALL fail with code `conflict`.

A booking carrying several claims SHALL be placed all-or-nothing: either every claim is persisted, or none is. A placement that fails for one claimed resource SHALL NOT leave claims persisted for the others, since service placement attempts combinations in sequence and depends on a failed attempt leaving no state.

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
