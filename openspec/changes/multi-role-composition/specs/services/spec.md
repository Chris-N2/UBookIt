## RENAMED Requirements

- FROM: `### Requirement: Service requirements are edited as a list of one`
- TO: `### Requirement: Service requirements are edited as a list of roles`

- FROM: `### Requirement: The requirement row reports how many resources match`
- TO: `### Requirement: The editor reports the resolution chain for each role`

## MODIFIED Requirements

### Requirement: Service definition
A `Service` SHALL have a `Guid` id, a non-empty display name, a duration specification, and at least one required role. The duration specification SHALL be a value object with exactly two kinds — **fixed** (a single length) and **variable** (an optional minimum and an optional maximum bound) — constructible only through validating factories, so a service can never hold a combination of duration fields that has no meaning. A `ServiceRole` SHALL name a resource **type** key (normalized lower-case kebab-case, as for resources), a set of required capability keys, and a count, which SHALL be 1. The required-capability set SHALL default to empty, and an empty set SHALL mean the role constrains only by type. Domain purity holds: `Service`, `ServiceRole`, the duration value object, and the capability value object reference no Umbraco, EF, or third-party types.

Every role of a service SHALL name a **distinct** resource type. This is a deliberate restriction of this version and not a property of services: two roles of the same type draw from eligibility pools that overlap, and assigning them by taking each role's first available candidate can report a service unavailable when it was bookable. Distinct types make the pools provably disjoint, since a resource has exactly one type, which is what makes independent per-role assignment correct. The restriction SHALL be lifted by the change that implements real assignment; nothing SHALL be built that depends on roles being disjoint by definition rather than by this rule.

Roles SHALL be an unordered set as far as behaviour is concerned: no requirement depends on their order, and reordering a service's roles SHALL change nothing observable.

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

#### Scenario: A service may require several roles of different types
- **WHEN** a service is created with a role for type `room` and a role for type `therapist`
- **THEN** the service is valid and reports both roles, each with count 1

#### Scenario: Role order is not observable
- **WHEN** two services are created with the same two roles supplied in opposite orders
- **THEN** they resolve to the same candidates and offer the same availability

### Requirement: Service validation with stable failure codes
Service creation and update SHALL be validated through a Core factory, producing a structured result carrying one stable machine-readable code per failed rule (never exceptions for expected rejections). A blank name, a role whose resource-type key is not normalized, a role whose required-capability key is not normalized, a role count other than 1, and two roles naming the same resource type SHALL each be rejected with a stable code. A role's malformed capability key SHALL be rejected with `capability-key-invalid`, distinct from the `type-key-invalid` used for its resource type, so a consumer can associate each message with the correct control. Invalid duration configuration SHALL be rejected with the stable `service-duration-invalid` code, carrying a field identifying which duration input is at fault so a consumer can associate the message with the correct control. Invalid duration configuration comprises: a non-positive fixed length or bound, a fixed length or bound that is not a whole number of minutes, and a variable minimum that exceeds its maximum. Codes are contract — consumers map them to messages.

A duplicate resource type across roles SHALL be rejected with its own stable code, distinct from the code used for a malformed type key, because the two faults are corrected differently: one is a typo in a key, the other a composition this version does not support. The failure SHALL identify the duplicated type.

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

#### Scenario: Two roles of the same type are rejected
- **WHEN** a service is created with two roles both naming resource type `therapist`, one requiring `cert-x` and the other requiring nothing
- **THEN** creation is rejected with the duplicate-type code, distinct from `type-key-invalid`, and the failure identifies `therapist`

#### Scenario: Differing capabilities do not make two roles distinct
- **WHEN** two roles name the same resource type and differ only in their required capabilities
- **THEN** creation is still rejected — the restriction is on the type, because that is what determines the eligibility pool

### Requirement: Service requirements are edited as a list of roles
The editor SHALL present the service's required resource composition in a "Service Requirements" group rendered as a list of one or more entries, with affordances to add and remove entries. A service SHALL always retain at least one role; removing the last entry SHALL NOT be offered. Each entry SHALL carry its own resource type control and its own required-capability control. The role's count SHALL NOT be surfaced in the UI and SHALL always be sent as 1. The presentation SHALL be structured such that supporting a count greater than one is an addition to this UI rather than a restructuring of it.

The editor SHALL NOT prevent a user selecting a resource type another role already uses. The duplicate-type rule SHALL be enforced by the server and its failure rendered against the offending entry, so that relaxing the rule later is a server change rather than a change in two places.

#### Scenario: Roles can be added and removed
- **WHEN** a user opens a service in the editor and adds a second requirement
- **THEN** two requirement rows are shown, each with its own type and capability controls, and each can be removed except when only one remains

#### Scenario: The last role cannot be removed
- **WHEN** a service has exactly one requirement row
- **THEN** no remove control is offered for it

#### Scenario: Count is sent as one and never shown
- **WHEN** a user saves any service from the editor
- **THEN** every role in the request body carries a count of 1, and no count field was displayed for any row

#### Scenario: A duplicate type is reported by the server against its row
- **WHEN** a user selects a resource type already used by another requirement and saves
- **THEN** the editor renders the server's duplicate-type failure against that requirement row, and no data is lost from the form

### Requirement: The editor reports the resolution chain for each role
The editor SHALL report, as a service is edited, the resolution chain for **each role** of the configuration on screen: how many resources have that role's resource type, how many of those carry all its required capabilities, and how many of those can provide the service's duration. Each chain SHALL identify the role it describes. The report SHALL refresh when any role's type or capabilities, or the duration, change, and SHALL be informational: it SHALL NOT block saving.

The report SHALL be presented at **form level**, not inside the Service Requirements group. Its inputs span both the requirements and the duration, and an editor who narrows a duration must not have to return to another group to discover that doing so emptied a pool.

Reporting a single surviving count per role SHALL NOT be sufficient. Each chain SHALL make each stage's count derivable, so that an empty or narrowed pool is attributable to the filter responsible: a mistyped resource type, an over-narrow capability set, and a duration no resource can provide are three different faults corrected in three different places. In particular, a resource type matching nothing SHALL NOT be reported as a capability problem.

The report SHALL NOT refer to required capabilities for a role that names none. Such a stage filtered nothing by construction — an empty requirement matches every resource of its type — so its count remains derivable from the stage before it, and reporting it would describe the configuration in terms the editor never entered.

Because every role names a distinct type, each role's chain SHALL be independently true: no role can consume a resource another role's chain counted. A report combining several roles into one count SHALL NOT be produced, since the roles constrain different pools and a combined number would describe no filter that resolution applies.

Because the report evaluates every filter candidate resolution applies, it MAY state that resources can provide the service. It SHALL NOT state or imply that the service is *available* — the chains say nothing about opening hours, lead time, booking horizon, existing bookings, or whether the roles' start grids ever coincide, and wording that suggests a bookable slot would over-claim.

Where a stage's count is not known — because the configuration is too incomplete to resolve, or the request failed — the report SHALL say nothing for that stage rather than reporting zero. Zero is the answer that tells an editor their configuration is wrong, so reporting it because a request failed sends them to correct something that is correct.

The report's wording SHALL be derived from state captured with the response it describes, never from the live form, so that it cannot momentarily assert a sentence that is false for the configuration it is reporting on.

#### Scenario: A chain is reported for each role
- **WHEN** a service is configured with a `room` role and a `therapist` role
- **THEN** the report shows one chain per role, each labelled with its resource type

#### Scenario: The chain is reported
- **WHEN** ten `room` resources exist, three carry `projector`, one of those can provide a fixed four-hour duration, and a role is configured that way
- **THEN** that role's chain shows ten, three, and one for the three stages

#### Scenario: The report refreshes as a requirement changes
- **WHEN** a user adds a further required capability to one role that only one resource carries
- **THEN** that role's chain updates without the user saving, and the other role's chain is unchanged

#### Scenario: The report refreshes when the duration changes
- **WHEN** a user changes the duration to a length no resource can provide
- **THEN** every role's chain updates to show its duration stage at zero, without the user saving or leaving the Duration group

#### Scenario: A mistyped resource type is not reported as a capability problem
- **WHEN** a role names a resource type no resource uses and also requires capabilities
- **THEN** that role's chain attributes the empty pool to the resource type, not to the capabilities

#### Scenario: An over-narrow capability set is attributed to the capabilities
- **WHEN** resources of a role's type exist but none carries a required capability
- **THEN** that role's chain shows the type stage populated and the capability stage at zero

#### Scenario: A duration no resource can provide is attributed to the duration
- **WHEN** resources carry a role's required capabilities but none can provide the configured length
- **THEN** that role's chain shows the capability stage populated and the duration stage at zero, and identifies the resources excluded and the bound that excluded them

#### Scenario: The report describes the type when no capabilities are required
- **WHEN** a role names a resource type, requires no capabilities, and some resources of that type cannot provide the duration
- **THEN** that role's chain describes how many resources have that **type** and how many of those can provide the service, and says nothing about required capabilities that were never named

#### Scenario: The report does not claim availability
- **WHEN** the report is displayed for any configuration
- **THEN** its wording describes what resources can provide, and does not state that the service is available, free, or bookable at any particular time

#### Scenario: A stage that is not known says nothing
- **WHEN** a role's resource type is empty, or the chains could not be retrieved
- **THEN** the report says nothing for that stage rather than reporting zero

#### Scenario: An unsaved service is reported
- **WHEN** a user edits the roles or duration of a service that has never been saved and has no name
- **THEN** the report still reports a chain per role for the configuration as currently entered

#### Scenario: The report never asserts a stale phrasing
- **WHEN** a user changes the configuration and the previous chains have not yet been replaced
- **THEN** the report continues to describe the configuration it was computed for, or says nothing, and never describes the new configuration using the old counts
