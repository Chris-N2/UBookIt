## MODIFIED Requirements

### Requirement: Service definition
A `Service` SHALL have a `Guid` id, a non-empty display name, a duration
specification, and at least one required role. The duration specification SHALL be a
value object with exactly two kinds — **fixed** (a single length) and **variable** (an
optional minimum and an optional maximum bound) — constructible only through
validating factories, so a service can never hold a combination of duration fields
that has no meaning. A `ServiceRole` SHALL name a resource **type** key (normalized
lower-case kebab-case, as for resources), a set of required capability keys, and a
count, which SHALL be at least 1 and at most a bounded maximum. The
required-capability set SHALL default to empty, and an empty set SHALL mean the role
constrains only by type. Domain purity holds: `Service`, `ServiceRole`, the duration
value object, and the capability value object reference no Umbraco, EF, or
third-party types.

A role's **count** SHALL mean that many **distinct** resources are required
simultaneously. A count of *N* SHALL be equivalent to *N* interchangeable slots drawn
from that role's eligibility pool, and no resource SHALL fill more than one of them.

Two roles MAY name the same resource type, provided their required capabilities
differ. Their eligibility pools then overlap without being equal, which is precisely
the case assignment exists to resolve.

Two roles naming the same resource type **and** requiring the same capabilities SHALL
be rejected. They are two spellings of one requirement, and a count expresses it
exactly once; allowing both would leave two representations that compare unequal and
publish differently while meaning the same thing, and silently merging them would
rewrite what the editor entered.

Roles SHALL be an unordered set as far as behaviour is concerned: no requirement
depends on their order, and reordering a service's roles SHALL change nothing
observable. The aggregate SHALL nonetheless hold them in a canonical order that is
**total** — resource type, then required capabilities, then count — so that a service
round-trips to an equal value and publishes its roles deterministically. Ordering by
type alone is no longer total now that two roles may share one, and an unstable sort
would let two same-type roles exchange places between saves.

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

#### Scenario: A service may require two roles of one type with different capabilities
- **WHEN** a service is created with two roles naming `therapist`, one requiring `cert-x` and the other requiring nothing
- **THEN** the service is valid and reports both roles

#### Scenario: A role may require several resources
- **WHEN** a service is created with one role naming `room` and a count of 2
- **THEN** the service is valid and reports that role with a count of 2

#### Scenario: Role order is not observable
- **WHEN** two services are created with the same two roles supplied in opposite orders
- **THEN** they resolve to the same candidates and offer the same availability

#### Scenario: Two same-type roles round-trip in a stable order
- **WHEN** a service with two roles of one resource type differing in capabilities is saved and re-read repeatedly
- **THEN** its roles are returned in the same order every time, and the re-read service compares equal to the saved one

### Requirement: Service validation with stable failure codes
Service creation and update SHALL be validated through a Core factory, producing a
structured result carrying one stable machine-readable code per failed rule (never
exceptions for expected rejections). A blank name, a role whose resource-type key is
not normalized, a role whose required-capability key is not normalized, a role count
below 1 or above the permitted maximum, and two roles naming the same resource type
with the same required capabilities SHALL each be rejected with a stable code. A
role's malformed capability key SHALL be rejected with `capability-key-invalid`,
distinct from the `type-key-invalid` used for its resource type, so a consumer can
associate each message with the correct control. Invalid duration configuration SHALL
be rejected with the stable `service-duration-invalid` code, carrying a field
identifying which duration input is at fault so a consumer can associate the message
with the correct control. Invalid duration configuration comprises: a non-positive
fixed length or bound, a fixed length or bound that is not a whole number of minutes,
and a variable minimum that exceeds its maximum. Codes are contract — consumers map
them to messages.

A duplicated role — same resource type **and** same required capabilities — SHALL be
rejected with its own stable code, distinct from the code used for a malformed type
key, because the two faults are corrected differently: one is a typo in a key, the
other a requirement stated twice where a count was meant. The failure SHALL identify
the duplicated type and SHALL name the count as the correction.

An out-of-range count SHALL be rejected with its own stable code carrying the
offending role, rather than being accepted and left for the assignment to refuse
slowly. The upper bound is a sanity limit on work, not a domain claim: each unit is a
slot the assignment must fill.

A count exceeding the number of eligible resources SHALL **NOT** be rejected. That is
a property of the pool rather than of the service — resources may be added later —
and refusing the save would block a configuration that is not wrong.

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

#### Scenario: Two identical roles are rejected in favour of a count
- **WHEN** a service is created with two roles both naming `therapist` and both requiring nothing
- **THEN** creation is rejected with the duplicate-role code, distinct from `type-key-invalid`, the failure identifies `therapist`, and its message names the count as the correction

#### Scenario: Differing capabilities make two roles of one type distinct
- **WHEN** two roles name the same resource type and differ in their required capabilities
- **THEN** creation succeeds — the pools overlap without being equal, which assignment resolves

#### Scenario: A count below one is rejected
- **WHEN** a service is created with a role whose count is zero or negative
- **THEN** creation is rejected with the count code, identifying the offending role

#### Scenario: A count above the permitted maximum is rejected
- **WHEN** a service is created with a role whose count exceeds the permitted maximum
- **THEN** creation is rejected with the count code, identifying the offending role

#### Scenario: A count larger than the pool is accepted
- **WHEN** a service is created with a role of count 3 while only one resource of that type exists
- **THEN** creation succeeds, because resources may be added later and nothing about the service is wrong

### Requirement: Service requirements are edited as a list of roles
The editor SHALL present the service's required resource composition in a "Service
Requirements" group rendered as a list of one or more entries, with affordances to
add and remove entries. A service SHALL always retain at least one role; removing the
last entry SHALL NOT be offered. Each entry SHALL carry its own resource type
control, its own required-capability control, and its own **count** control.

The count control SHALL default to 1, SHALL be labelled and associated with its entry
as the other controls are, and SHALL express how many distinct resources of that
requirement a booking needs at once. A count the server rejects SHALL have its failure
rendered against that entry.

The editor SHALL NOT prevent a user selecting a resource type another role already
uses. The duplicate-role rule SHALL be enforced by the server and its failure rendered
against the offending entry, so that changing the rule remains a server change rather
than a change in two places.

#### Scenario: Roles can be added and removed
- **WHEN** a user opens a service in the editor and adds a second requirement
- **THEN** two requirement rows are shown, each with its own type, capability and count controls, and each can be removed except when only one remains

#### Scenario: The last role cannot be removed
- **WHEN** a service has exactly one requirement row
- **THEN** no remove control is offered for it

#### Scenario: Count defaults to one
- **WHEN** a user adds a requirement row and saves without touching its count
- **THEN** that role is sent with a count of 1

#### Scenario: A count is editable and round-trips
- **WHEN** a user sets a requirement's count to 2, saves, and reopens the service
- **THEN** the row shows a count of 2

#### Scenario: A duplicate role is reported by the server against its row
- **WHEN** a user selects a resource type and capability set already used by another requirement and saves
- **THEN** the editor renders the server's duplicate-role failure against that requirement row, and no data is lost from the form

#### Scenario: An out-of-range count is reported against its row
- **WHEN** a user enters a count the server rejects and saves
- **THEN** the editor renders that failure against the offending requirement row, and no data is lost from the form
