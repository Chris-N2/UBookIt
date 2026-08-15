## ADDED Requirements

### Requirement: The configuration preview reports a permanent start-grid misalignment
The service configuration preview endpoint SHALL carry, alongside its per-role
resolution chains, whether the configuration's roles can share a bookable start
at all — obtained from Core's alignment check rather than computed by the
backoffice, so the report cannot disagree with the rules the booking path acts
on.

The finding SHALL be a **member of its own** on the response, never folded into a
role's chain. The chains report type, capabilities and duration and say nothing
about opening hours; a stage that mentioned opening times would be the chain
claiming something about availability, which it is forbidden to do.

The finding SHALL be present only when the roles are permanently misaligned.
Alignment SHALL NOT be reported positively, because it would be read as a promise
that the service is bookable, which the endpoint cannot make.

When present, the finding SHALL identify the two roles by their resource types,
the two resources by id and display name, and the window start and granularity of
each — the numbers an editor changes.

The endpoint SHALL remain read-only, SHALL still accept a configuration no saved
service holds, and SHALL NOT reject a configuration because its roles are
misaligned. Its existing responses, codes and authorization are unchanged.

#### Scenario: A misaligned configuration reports the finding
- **WHEN** the endpoint is called for a `room` role whose only resource opens at 09:00 on a 30-minute granularity and a `therapist` role whose only resource opens at 09:15 on a 20-minute granularity
- **THEN** the response carries both role chains as before, plus a misalignment finding naming both roles, both resources, and their window starts and granularities

#### Scenario: An alignable configuration carries no finding
- **WHEN** the endpoint is called for a configuration whose roles can share a start
- **THEN** the response carries the chains and no misalignment finding

#### Scenario: The finding is separate from the chains
- **WHEN** a misalignment finding is returned
- **THEN** no role's chain stage mentions opening hours or start times, and the chains are exactly what they would have been without the finding

#### Scenario: A single-role configuration never reports a misalignment
- **WHEN** the endpoint is called for a configuration with one role
- **THEN** no misalignment finding is returned, because there is no second grid to miss

#### Scenario: A misaligned configuration is still previewed, not rejected
- **WHEN** the endpoint is called for a permanently misaligned configuration
- **THEN** the request succeeds with its chains, exactly as for any other configuration
