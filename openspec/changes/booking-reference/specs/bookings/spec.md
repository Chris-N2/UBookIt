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
