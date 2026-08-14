## MODIFIED Requirements

### Requirement: Service definition
A `Service` SHALL have a `Guid` id, a non-empty display name, a duration specification, and exactly one required role in v1. The duration specification SHALL be a value object with exactly two kinds — **fixed** (a single length) and **variable** (an optional minimum and an optional maximum bound) — constructible only through validating factories, so a service can never hold a combination of duration fields that has no meaning. A `ServiceRole` SHALL name a resource **type** key (normalized lower-case kebab-case, as for resources), a set of required capability keys, and a count, which SHALL be 1 in v1. The required-capability set SHALL default to empty, and an empty set SHALL mean the role constrains only by type. The model SHALL NOT structurally prevent multiple roles — that is additive later — but v1 behaviour SHALL enforce a single role of count 1. Domain purity holds: `Service`, `ServiceRole`, the duration value object, and the capability value object reference no Umbraco, EF, or third-party types.

#### Scenario: Valid service with a fixed duration
- **WHEN** a service is created with name "Massage", a fixed 60-minute duration, and one role requiring resource type `person`
- **THEN** the service is valid, has a non-empty id, reports a fixed duration of 60 minutes, and has one role for type `person` with count 1

#### Scenario: Valid service with bounded variable duration
- **WHEN** a service is created with a name, one role, and a variable duration bounded between 45 and 120 minutes
- **THEN** the service is valid and reports a variable duration with a 45-minute minimum and a 120-minute maximum

#### Scenario: Variable duration with no bounds is the unconfigured default
- **WHEN** a service is created with a name and a single role and no duration configuration
- **THEN** the service is valid and reports a variable duration with no minimum and no maximum, deferring entirely to the fulfilling resource's range

#### Scenario: Name is required
- **WHEN** a service is created with an empty or whitespace name
- **THEN** creation is rejected with a validation failure identifying the name

#### Scenario: Required capabilities default to empty
- **WHEN** a service is created with a role naming only a resource type
- **THEN** the service is valid and its role reports an empty required-capability set

#### Scenario: A role carries the capabilities it requires
- **WHEN** a service is created with a role requiring `cert-x` and `welsh`
- **THEN** the role reports both required capabilities and no others

### Requirement: Service validation with stable failure codes
Service creation and update SHALL be validated through a Core factory, producing a structured result carrying one stable machine-readable code per failed rule (never exceptions for expected rejections). A blank name, a role whose resource-type key is not normalized, a role whose required-capability key is not normalized, and a role count other than 1 SHALL each be rejected with a stable code. A role's malformed capability key SHALL be rejected with `capability-key-invalid`, distinct from the `type-key-invalid` used for its resource type, so a consumer can associate each message with the correct control. Invalid duration configuration SHALL be rejected with the stable `service-duration-invalid` code, carrying a field identifying which duration input is at fault so a consumer can associate the message with the correct control. Invalid duration configuration comprises: a non-positive fixed length or bound, a fixed length or bound that is not a whole number of minutes, and a variable minimum that exceeds its maximum. Codes are contract — consumers map them to messages.

#### Scenario: Non-normalized role type key is rejected
- **WHEN** a service role is defined with resource type `"Meeting Room"` (upper-case and a space)
- **THEN** creation is rejected with a validation failure identifying the role's type key

#### Scenario: Non-normalized required capability key is rejected
- **WHEN** a service role is defined requiring the capability `"Cert X"` (upper-case and a space)
- **THEN** creation is rejected with the `capability-key-invalid` code

#### Scenario: Type and capability failures are distinguishable
- **WHEN** a service role is defined with both a malformed resource type key and a malformed required-capability key
- **THEN** the result carries `type-key-invalid` and `capability-key-invalid` as separate failures

#### Scenario: Non-positive fixed duration is rejected
- **WHEN** a service is created with a fixed duration of zero or negative
- **THEN** creation is rejected with the `service-duration-invalid` code

#### Scenario: Inverted variable bounds are rejected
- **WHEN** a service is created with a variable duration whose minimum is 120 minutes and whose maximum is 45 minutes
- **THEN** creation is rejected with the `service-duration-invalid` code and a field identifying the offending bound

#### Scenario: Sub-minute bounds are rejected
- **WHEN** a service is created with a variable duration bound that is not a whole number of minutes
- **THEN** creation is rejected with the `service-duration-invalid` code

### Requirement: Resource type is chosen from types already in use
The requirement row SHALL let the user choose a resource type from the types currently in use — sourced from the resource type usage endpoint — while still permitting a type key that no resource currently uses, since a service may legitimately be defined before its resources exist. Choosing a type with no matching resources SHALL NOT prevent saving; the fact that nothing matches SHALL be reported through the matching-resource readout rather than as a separate type-specific hint, so that one control reports one answer. An invalid type key SHALL still be rejected by the server's existing `type-key-invalid` validation and surfaced like any other failure.

#### Scenario: Choosing an existing type
- **WHEN** resources of types `room` and `masseur` exist and the user opens the requirement's type control
- **THEN** both `room` and `masseur` are offered as choices

#### Scenario: Naming a type that does not exist yet
- **WHEN** a user enters the type `physiotherapist` while no resource has that type
- **THEN** the matching-resource readout reports that no resources match, saving still succeeds, and no blocking error is shown

#### Scenario: Invalid type key is rejected by the server
- **WHEN** a user enters a type key the domain rejects and saves
- **THEN** the editor surfaces the server's `type-key-invalid` failure and no service is created

## ADDED Requirements

### Requirement: Required capabilities are edited on the requirement row
The requirement row SHALL let a user add and remove required capability keys, offering the capability keys already in use — sourced from the capability usage endpoint — while still permitting a key no resource currently carries, for the same reason a not-yet-used resource type is permitted. Removing every capability SHALL be permitted and SHALL restore type-only matching. A malformed key SHALL be surfaced from the server's `capability-key-invalid` failure, associated with the capability control rather than the type control.

#### Scenario: Adding a required capability
- **WHEN** a user adds the capability `cert-x` to the requirement and saves
- **THEN** the save succeeds and reopening the service shows `cert-x` as required

#### Scenario: Capabilities in use are offered
- **WHEN** resources carry the capabilities `cert-x` and `massage` and the user opens the capability control
- **THEN** both are offered as choices

#### Scenario: A capability nothing carries is still permitted
- **WHEN** a user requires a capability no resource currently carries and saves
- **THEN** the save succeeds and the matching-resource readout reports that no resources match

#### Scenario: Removing all capabilities restores type-only matching
- **WHEN** a user removes every required capability from a service and saves
- **THEN** the service's role requires no capabilities and matches every resource of its type

#### Scenario: Malformed capability key is surfaced on its own control
- **WHEN** the server rejects a capability key with `capability-key-invalid`
- **THEN** the editor associates the message with the capability control and no data is lost from the form

### Requirement: The requirement row reports how many resources match
The editor SHALL report, as the requirement row is edited, how many resources carry the role's resource type and all of its required capabilities, so that an over-narrow or mistyped requirement is visible at configuration time rather than surfacing later as ordinary unavailability. The readout SHALL refresh when the type or the capabilities change and SHALL be informational: it SHALL NOT block saving.

The readout SHALL claim only what it verifies. It SHALL describe capability matching — for example "3 rooms have these capabilities" — and SHALL NOT state or imply that the service can be booked on those resources, because candidate resolution additionally excludes resources whose duration range cannot admit the service and that exclusion is not evaluated here. A readout that implied bookability would give false reassurance in exactly the case it exists to expose.

#### Scenario: Matching resources are counted
- **WHEN** four resources of type `room` exist and three of them carry `projector`, and the requirement names type `room` requiring `projector`
- **THEN** the readout reports that three resources match

#### Scenario: The readout refreshes as the requirement changes
- **WHEN** a user adds a further required capability that only one resource carries
- **THEN** the readout updates to report one matching resource without the user saving

#### Scenario: Nothing matches
- **WHEN** the requirement names a type and capability combination no resource satisfies
- **THEN** the readout reports that no resources match, and saving is still permitted

#### Scenario: The readout does not claim bookability
- **WHEN** the readout is displayed for any requirement
- **THEN** its wording describes which resources hold the capabilities, and does not state that the service can be booked on them

#### Scenario: An unsaved requirement is reported
- **WHEN** a user edits the type or capabilities of a service that has never been saved
- **THEN** the readout still reports a count for the requirement as currently entered
