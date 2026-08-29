## MODIFIED Requirements

### Requirement: Direct-resource booking is unaffected
Introducing booking via a service SHALL NOT change how a resource is booked directly. The existing per-resource availability queries, the existing placement pipeline, and the existing placement endpoint SHALL behave identically whether or not any service exists, and whether or not a resource happens to be in some service's candidate pool. A booking placed through a service SHALL block the resolved resource for other bookings on exactly the same terms as a directly placed one.

**A service booking is an ordinary booking in every respect except that it records which
service placed it.** The earlier wording — "indistinguishable in shape from a directly
placed one" — is narrowed here rather than dropped, because recording the service is
precisely a difference in shape and the sentence would otherwise be false. What it was
written to guarantee is unchanged and restated: a service booking is not a second kind of
entity, holds no privileged position, uses one interval and the same claim rows, blocks and
is blocked identically, and is cancelled by the same operation. The one difference is a
recorded fact about where the booking came from, which changes nothing about how it
behaves.

What a resource **permits** is a separate question from what services do to it. Direct
booking is now offered only for a resource whose editor has said it may be booked on
its own, and that gate is unrelated to services: it applies identically whether the
resource belongs to a service's pool or to none, and it is the editor's answer rather
than any consequence of a service existing. The guarantee above is therefore unchanged
in substance — services still change nothing about direct booking — and the scenarios
below are stated for a resource that permits it, because a resource that does not is
answering a different question.

Membership of a service's candidate pool SHALL remain independent of the permission in
both directions: a resource that withholds it is still resolved, still contributes to
composite availability, and is still claimed by a service booking.

#### Scenario: Direct placement is unchanged by services
- **WHEN** a resource that permits direct booking, and that belongs to a service's candidate pool, is booked directly at a valid interval
- **THEN** placement succeeds exactly as it would if no service existed

#### Scenario: A service booking blocks direct booking
- **WHEN** a service booking resolves to a resource that permits direct booking, and a direct booking is then attempted for an overlapping interval on that resource
- **THEN** the direct booking fails with `conflict`

#### Scenario: A direct booking removes a candidate
- **WHEN** a resource in a candidate pool is booked directly and service availability is then queried over that interval
- **THEN** that resource contributes no runs for the affected starts

#### Scenario: Withholding direct booking does not remove a candidate
- **WHEN** a resource that withholds direct booking is in a service's candidate pool
- **THEN** it is resolved, contributes availability, and can be claimed by a service booking exactly as a permitting resource would

#### Scenario: The recorded service is the only difference
- **WHEN** a service booking and a direct booking are compared
- **THEN** they differ only in the recorded service, and are identical in how they claim resources, block other bookings, and are cancelled

## ADDED Requirements

### Requirement: Service placement records the service on the booking it produces
When a service booking resolves an assignment and places it, the resulting booking SHALL
record **that service** — its id, and its display name as it stands at placement time.

This is the requirement that makes the attribution *populated* rather than merely
available. A column that exists and is never written reads as "every booking was placed
directly", which is wrong and silent — the same class of failure as an attribution naming
the wrong service, arrived at from the other side.

The recorded name SHALL be a snapshot taken at placement, not a reference resolved later.
What the row states is what was sold at the time; a service renamed afterwards SHALL NOT
retitle bookings already placed for it.

Recording the service SHALL NOT change any placement outcome. The assignment that is
chosen, the conflicts that are detected, the failures that are reported and the atomic
all-or-nothing contract SHALL all behave exactly as before — this requirement records an
outcome that placement already produces.

#### Scenario: The placed booking names the service
- **WHEN** a service is booked and placement succeeds
- **THEN** the resulting booking reports that service's id and its display name

#### Scenario: The name is a snapshot, not a reference
- **WHEN** a service is renamed after a booking was placed for it
- **THEN** the existing booking still reports the name the service had when it was placed

#### Scenario: Placement behaviour is unchanged
- **WHEN** the same service booking request is placed as before this requirement existed
- **THEN** the same assignment, the same success or failure, and the same failure codes result — the only difference is the service recorded on a successful booking

#### Scenario: A failed placement records nothing
- **WHEN** a service booking fails to place
- **THEN** no booking is created, and no service attribution is stored
