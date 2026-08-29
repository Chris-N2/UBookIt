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
