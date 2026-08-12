## MODIFIED Requirements

### Requirement: Service definition
A `Service` SHALL have a `Guid` id, a non-empty display name, a duration specification, and exactly one required role in v1. The duration specification SHALL be a value object with exactly two kinds — **fixed** (a single length) and **variable** (an optional minimum and an optional maximum bound) — constructible only through validating factories, so a service can never hold a combination of duration fields that has no meaning. A `ServiceRole` SHALL name a resource **type** key (normalized lower-case kebab-case, as for resources) and a count, which SHALL be 1 in v1. The model SHALL NOT structurally prevent multiple roles or a role carrying required capabilities — those are additive later — but v1 behaviour SHALL enforce a single role of count 1 and no capability requirements. Domain purity holds: `Service`, `ServiceRole`, and the duration value object reference no Umbraco, EF, or third-party types.

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

### Requirement: Service validation with stable failure codes
Service creation and update SHALL be validated through a Core factory, producing a structured result carrying one stable machine-readable code per failed rule (never exceptions for expected rejections). A blank name, a role whose resource-type key is not normalized, and a role count other than 1 SHALL each be rejected with a stable code. Invalid duration configuration SHALL be rejected with the stable `service-duration-invalid` code, carrying a field identifying which duration input is at fault so a consumer can associate the message with the correct control. Invalid duration configuration comprises: a non-positive fixed length or bound, a fixed length or bound that is not a whole number of minutes, and a variable minimum that exceeds its maximum. Codes are contract — consumers map them to messages.

#### Scenario: Non-normalized role type key is rejected
- **WHEN** a service role is defined with resource type `"Meeting Room"` (upper-case and a space)
- **THEN** creation is rejected with a validation failure identifying the role's type key

#### Scenario: Non-positive fixed duration is rejected
- **WHEN** a service is created with a fixed duration of zero or negative
- **THEN** creation is rejected with the `service-duration-invalid` code

#### Scenario: Inverted variable bounds are rejected
- **WHEN** a service is created with a variable duration whose minimum is 120 minutes and whose maximum is 45 minutes
- **THEN** creation is rejected with the `service-duration-invalid` code and a field identifying the offending bound

#### Scenario: Sub-minute bounds are rejected
- **WHEN** a service is created with a variable duration bound that is not a whole number of minutes
- **THEN** creation is rejected with the `service-duration-invalid` code

### Requirement: Service duration semantics
A service's duration specification SHALL narrow, and SHALL NEVER widen, the range the fulfilling resource already permits. The effective bookable range for a service on a given resource SHALL be the intersection of the service's range with the resource's `[MinDuration, MaxDuration]`, where a fixed duration is the degenerate range whose minimum and maximum are equal and an absent bound contributes no narrowing. A resource's maximum duration SHALL therefore be a hard ceiling regardless of service configuration.

Service bounds SHALL NOT be required to align to any resource's granularity; they are bounds, not lengths. The lengths actually bookable SHALL be the granularity multiples lying within the effective range.

When the intersection is empty, or when no granularity multiple lies within it, that resource SHALL be treated as unable to fulfil that service. This SHALL NOT be a validation failure at service-definition time, because a service spanning many resources cannot know each resource's limits.

This requirement fixes the contract and the resolution is implemented and unit-tested here; it governs booking behaviour when booking-via-service ships.

#### Scenario: Service bounds narrow the resource range
- **WHEN** a service with a variable duration of 45–180 minutes is resolved against a resource permitting 30–120 minutes
- **THEN** the effective range is 45–120 minutes

#### Scenario: A resource maximum cannot be exceeded
- **WHEN** a service with a fixed 90-minute duration is resolved against a resource whose maximum duration is 60 minutes
- **THEN** the intersection is empty and the resource is reported as unable to fulfil the service

#### Scenario: Unbounded variable duration defers entirely to the resource
- **WHEN** a service with a variable duration and no bounds is resolved against a resource permitting 30 minutes to 8 hours
- **THEN** the effective range is 30 minutes to 8 hours

#### Scenario: Bounds need not align to granularity
- **WHEN** a service with a variable minimum of 40 minutes is resolved against a resource with 15-minute granularity permitting 30–120 minutes
- **THEN** the effective range starts at 40 minutes and the shortest bookable length is 45 minutes

#### Scenario: Empty intersection is not a definition-time failure
- **WHEN** a service is defined with a fixed duration that no currently defined resource could satisfy
- **THEN** the service is created successfully and the mismatch surfaces only when resolving against a resource

### Requirement: Service CRUD endpoints
The Management API SHALL expose versioned endpoints in the `ubookitbackoffice` swagger group for services: paged list (`GET`, with skip/take and total count), get by id, create, full update, and delete. Request and response bodies SHALL be purpose-built DTO models — domain types SHALL NOT appear in the HTTP contract. The duration SHALL be carried as a single nested object naming its kind explicitly (`fixed` or `variable`) together with its length or bounds in whole minutes, so the wire contract admits no ambiguous combination; an absent bound SHALL be expressed as null. Validation failures SHALL produce a 400 problem-details response carrying every failed rule as `{code, message, field}` with the domain's stable codes verbatim; an unknown service id SHALL produce 404 with a stable `service-not-found` code.

#### Scenario: Fixed duration round-trips through the API
- **WHEN** a service is created with a name, a fixed 60-minute duration, and one role, then fetched by id
- **THEN** the response contains the same name, a duration of kind `fixed` with 60 minutes, and the same role

#### Scenario: Variable duration round-trips through the API
- **WHEN** a service is created with a variable duration bounded between 45 and 120 minutes, then fetched by id
- **THEN** the response contains a duration of kind `variable` with a 45-minute minimum and a 120-minute maximum

#### Scenario: Unbounded variable duration round-trips through the API
- **WHEN** a service is created with a variable duration and both bounds null, then fetched by id
- **THEN** the response contains a duration of kind `variable` with both bounds null

#### Scenario: Paged list
- **WHEN** 25 services exist and the list endpoint is called with skip 20, take 10
- **THEN** the response contains 5 items and reports a total of 25

#### Scenario: Unknown id
- **WHEN** get or update is called for a service id that does not exist
- **THEN** the response is 404 with code `service-not-found`

#### Scenario: Invalid service is rejected with codes
- **WHEN** a service is submitted with a blank name and a non-normalized role type key
- **THEN** the response is 400 and its errors carry the stable name and role-type-key codes

### Requirement: Service duration is an explicit choice
The editor SHALL NOT represent any duration mode as an empty input. It SHALL offer an explicit choice between a **fixed** duration in minutes and a **variable** duration, and SHALL state in the UI what the variable option means — that the visitor chooses the length, within the bounds given and within what each resource permits. The variable option SHALL offer optional minimum and maximum bound inputs, and SHALL state that leaving a bound empty defers that bound to the resource. Choosing fixed SHALL send a fixed duration carrying that value in minutes; choosing variable SHALL send a variable duration carrying whichever bounds were supplied.

Validation messages for duration inputs SHALL be associated with the specific input at fault using the field carried on the failure, so a screen-reader user is told which control to correct.

#### Scenario: Setting a fixed duration
- **WHEN** a user chooses a fixed duration, enters 90 minutes, and saves
- **THEN** the stored service reports a fixed duration of 90 minutes, and reopening the editor shows the fixed choice selected with 90 in the field

#### Scenario: Setting bounded variable duration
- **WHEN** a user chooses a variable duration, enters a 45-minute minimum and a 120-minute maximum, and saves
- **THEN** the stored service reports a variable duration with those bounds, and reopening the editor shows the variable choice selected with both bounds populated

#### Scenario: Leaving both bounds empty defers to the resource
- **WHEN** a user chooses a variable duration, leaves both bound inputs empty, and saves
- **THEN** the stored service reports a variable duration with no bounds and the editor states that each resource's own limits apply

#### Scenario: A bound error identifies its own input
- **WHEN** a user submits a variable duration whose minimum exceeds its maximum
- **THEN** the error message is rendered and programmatically associated with the offending bound input rather than with the duration group as a whole
