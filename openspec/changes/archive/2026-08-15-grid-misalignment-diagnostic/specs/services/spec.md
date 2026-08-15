## ADDED Requirements

### Requirement: The editor reports roles whose start times can never coincide
The services editor SHALL report, as a service is edited, when its roles can
never share a bookable start — the case where every role resolves healthily and
the service is nonetheless unbookable forever.

The report SHALL be presented **separately from the resolution chains**, not as
part of any role's chain. The chains describe which resources can provide the
service; this describes whether their start times can ever meet, which is a
different claim about different data.

The report SHALL name what an editor acts on: the two roles, the two resources,
and their opening times and granularities. The fix is to edit a **resource** —
its opening time or its granularity — or to add one that aligns, so a report
that named only the service would send an editor to the wrong screen.

The report SHALL appear only for a permanent misalignment. It SHALL NOT appear
because a service is fully booked, because a range contains no free time, or for
any condition that a different day or a cancelled booking would resolve.

The report SHALL NOT block saving, and SHALL NOT be presented as a validation
failure. A misaligned service is legitimate: the resources it needs may be added
or adjusted later, and nothing about the service itself is wrong.

The report SHALL NOT state or imply that a service **is** bookable or available
when it is absent. Its absence means only that no permanent misalignment was
found.

Where the finding is not known — the configuration is too incomplete, or the
request failed — the editor SHALL say nothing rather than implying either
answer, consistent with how the resolution chains treat a stage they cannot
report.

#### Scenario: A permanent misalignment is reported
- **WHEN** a service is configured with a `room` role whose resource opens at 09:00 in 30-minute steps and a `therapist` role whose resource opens at 09:15 in 20-minute steps
- **THEN** the editor reports that those two roles can never share a start, naming both resources with their opening times and step sizes

#### Scenario: The report is separate from the resolution chains
- **WHEN** a misalignment is reported
- **THEN** it is rendered outside the per-role chains, and no chain line mentions opening hours or start times

#### Scenario: An alignable configuration says nothing about alignment
- **WHEN** a service's roles can share a start
- **THEN** the editor shows no alignment report, and does not state that the service is bookable or available

#### Scenario: Saving is unaffected
- **WHEN** a user saves a service whose roles are permanently misaligned
- **THEN** the save succeeds, and the report is informational rather than a validation failure

#### Scenario: The report refreshes with the configuration
- **WHEN** a user changes a role's resource type so that the roles can now align
- **THEN** the report disappears without the user saving

#### Scenario: A busy week is not reported as a misalignment
- **WHEN** a service's roles can share a start but every such start is already booked
- **THEN** the editor shows no alignment report, because nothing about the configuration is permanently wrong

#### Scenario: An unknown finding says nothing
- **WHEN** the configuration is too incomplete to resolve, or the request for it fails
- **THEN** the editor says nothing about alignment rather than implying that the roles do or do not align
