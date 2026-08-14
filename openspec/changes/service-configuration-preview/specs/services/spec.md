## MODIFIED Requirements

### Requirement: The requirement row reports how many resources match
The editor SHALL report, as a service is edited, the resolution chain for the configuration on screen: how many resources have the role's resource type, how many of those carry all its required capabilities, and how many of those can provide the service's duration. The report SHALL refresh when the type, the capabilities, or the duration change, and SHALL be informational: it SHALL NOT block saving.

The report SHALL be presented at **form level**, not inside the Service Requirements group. Its inputs span both the requirement and the duration, and an editor who narrows a duration must not have to return to another group to discover that doing so emptied the pool.

Reporting a single surviving count SHALL NOT be sufficient. The chain SHALL make each stage's count derivable, so that an empty or narrowed pool is attributable to the filter responsible: a mistyped resource type, an over-narrow capability set, and a duration no resource can provide are three different faults corrected in three different places. In particular, a resource type matching nothing SHALL NOT be reported as a capability problem.

Because the report now evaluates every filter candidate resolution applies, it MAY state that resources can provide the service. It SHALL NOT state or imply that the service is *available* — the chain says nothing about opening hours, lead time, booking horizon, or existing bookings, and wording that suggests a bookable slot would over-claim exactly as the earlier capability-only wording would have.

Where a stage's count is not known — because the configuration is too incomplete to resolve, or the request failed — the report SHALL say nothing for that stage rather than reporting zero. Zero is the answer that tells an editor their configuration is wrong, so reporting it because a request failed sends them to correct something that is correct.

The report's wording SHALL be derived from state captured with the response it describes, never from the live form, so that it cannot momentarily assert a sentence that is false for the configuration it is reporting on.

#### Scenario: The chain is reported
- **WHEN** ten `room` resources exist, three carry `projector`, one of those can provide a fixed four-hour duration, and the service is configured that way
- **THEN** the report shows ten, three, and one for the three stages

#### Scenario: The report refreshes as the requirement changes
- **WHEN** a user adds a further required capability that only one resource carries
- **THEN** the report updates without the user saving

#### Scenario: The report refreshes when the duration changes
- **WHEN** a user changes the duration to a length no resource can provide
- **THEN** the report updates to show the duration stage at zero, without the user saving or leaving the Duration group

#### Scenario: A mistyped resource type is not reported as a capability problem
- **WHEN** the requirement names a resource type no resource uses and also requires capabilities
- **THEN** the report attributes the empty pool to the resource type, not to the capabilities

#### Scenario: An over-narrow capability set is attributed to the capabilities
- **WHEN** resources of the type exist but none carries a required capability
- **THEN** the report shows the type stage populated and the capability stage at zero

#### Scenario: A duration no resource can provide is attributed to the duration
- **WHEN** resources carry the required capabilities but none can provide the configured length
- **THEN** the report shows the capability stage populated and the duration stage at zero, and identifies the resources excluded and the bound that excluded them

#### Scenario: The report does not claim availability
- **WHEN** the report is displayed for any configuration
- **THEN** its wording describes what resources can provide, and does not state that the service is available, free, or bookable at any particular time

#### Scenario: A stage that is not known says nothing
- **WHEN** the resource type is empty, or the chain could not be retrieved
- **THEN** the report says nothing rather than reporting zero

#### Scenario: An unsaved service is reported
- **WHEN** a user edits the type, capabilities, or duration of a service that has never been saved and has no name
- **THEN** the report still reports the chain for the configuration as currently entered

#### Scenario: The report never asserts a stale phrasing
- **WHEN** a user changes the configuration and the previous chain has not yet been replaced
- **THEN** the report continues to describe the configuration it was computed for, or says nothing, and never describes the new configuration using the old counts
