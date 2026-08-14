## MODIFIED Requirements

### Requirement: The requirement row reports how many resources match
The editor SHALL report, as a service is edited, the resolution chain for the configuration on screen: how many resources have the role's resource type, how many of those carry all its required capabilities, and how many of those can provide the service's duration. The report SHALL refresh when the type, the capabilities, or the duration change, and SHALL be informational: it SHALL NOT block saving.

The report SHALL be presented at **form level**, not inside the Service Requirements group. Its inputs span both the requirement and the duration, and an editor who narrows a duration must not have to return to another group to discover that doing so emptied the pool.

Reporting a single surviving count SHALL NOT be sufficient. The chain SHALL make each stage's count derivable, so that an empty or narrowed pool is attributable to the filter responsible: a mistyped resource type, an over-narrow capability set, and a duration no resource can provide are three different faults corrected in three different places. In particular, a resource type matching nothing SHALL NOT be reported as a capability problem.

The report SHALL NOT refer to required capabilities when the configuration names none. Such a stage filtered nothing by construction — an empty requirement matches every resource of its type — so its count remains derivable from the stage before it, and reporting it would describe the configuration in terms the editor never entered. This carries forward a guarantee the capability-only readout made through separate type-phrased wording; the chain keeps the guarantee by omitting the stage rather than by phrasing it twice.

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

#### Scenario: The report describes the type when no capabilities are required
- **WHEN** the configuration names a resource type, requires no capabilities, and some resources of that type cannot provide the duration
- **THEN** the report describes how many resources have that **type** and how many of those can provide the service, and says nothing about required capabilities that were never named

#### Scenario: A stage that is not known says nothing
- **WHEN** the resource type is empty, or the chain could not be retrieved
- **THEN** the report says nothing rather than reporting zero

#### Scenario: An unsaved service is reported
- **WHEN** a user edits the type, capabilities, or duration of a service that has never been saved and has no name
- **THEN** the report still reports the chain for the configuration as currently entered

#### Scenario: The report never asserts a stale phrasing
- **WHEN** a user changes the configuration and the previous chain has not yet been replaced
- **THEN** the report continues to describe the configuration it was computed for, or says nothing, and never describes the new configuration using the old counts

### Requirement: Resource type is chosen from types already in use
The requirement row SHALL let the user choose a resource type from the types currently in use — sourced from the resource type usage endpoint — while still permitting a type key that no resource currently uses, since a service may legitimately be defined before its resources exist. Choosing a type with no matching resources SHALL NOT prevent saving; the fact that nothing has that type SHALL be reported through the resolution summary's first stage rather than as a separate type-specific hint, so that one report answers the question. An invalid type key SHALL still be rejected by the server's existing `type-key-invalid` validation and surfaced like any other failure.

#### Scenario: Choosing an existing type
- **WHEN** resources of types `room` and `masseur` exist and the user opens the requirement's type control
- **THEN** both `room` and `masseur` are offered as choices

#### Scenario: Naming a type that does not exist yet
- **WHEN** a user enters the type `physiotherapist` while no resource has that type
- **THEN** the summary reports the type stage as empty, saving still succeeds, and no blocking error is shown

#### Scenario: Invalid type key is rejected by the server
- **WHEN** a user enters a type key the domain rejects and saves
- **THEN** the editor surfaces the server's `type-key-invalid` failure and no service is created

### Requirement: Required capabilities are edited on the requirement row
The requirement row SHALL let a user add and remove required capability keys, offering the capability keys already in use — sourced from the capability usage endpoint — while still permitting a key no resource currently carries, for the same reason a not-yet-used resource type is permitted. Removing every capability SHALL be permitted and SHALL restore type-only matching, which the summary's capability stage then reports as equal to its type stage. A malformed key SHALL be surfaced from the server's `capability-key-invalid` failure, associated with the capability control rather than the type control.

#### Scenario: Adding a required capability
- **WHEN** a user adds the capability `cert-x` to the requirement and saves
- **THEN** the save succeeds and reopening the service shows `cert-x` as required

#### Scenario: Capabilities in use are offered
- **WHEN** resources carry the capabilities `cert-x` and `massage` and the user opens the capability control
- **THEN** both are offered as choices

#### Scenario: A capability nothing carries is still permitted
- **WHEN** a user requires a capability no resource currently carries and saves
- **THEN** the save succeeds and the summary reports the capability stage as empty while the type stage is populated

#### Scenario: Removing all capabilities restores type-only matching
- **WHEN** a user removes every required capability from a service and saves
- **THEN** the service's role requires no capabilities and matches every resource of its type

#### Scenario: Malformed capability key is surfaced on its own control
- **WHEN** the server rejects a capability key with `capability-key-invalid`
- **THEN** the editor associates the message with the capability control and no data is lost from the form
