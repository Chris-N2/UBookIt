# resources

## ADDED Requirements

### Requirement: Resource definition
A bookable resource SHALL have a `Guid` identifier, a resource type key, a non-empty display name, and an optional description. The v1 shipped resource type SHALL be `room`, provided as a constant.

#### Scenario: Creating a valid room resource
- **WHEN** a resource is created with type `room` and display name "Meeting Room A"
- **THEN** the resource is valid, has a non-empty `Guid` id, and reports type `room`

#### Scenario: Display name is required
- **WHEN** a resource is created with an empty or whitespace display name
- **THEN** creation is rejected with a validation failure

### Requirement: Resource type is an extensible normalized key
The resource type SHALL be a normalized string key (lower-case, kebab-case, non-empty), not an enum, so that future types (e.g. `person`, `equipment`) can be introduced without schema or breaking API changes. Type keys that do not match the normalized form SHALL be rejected at creation.

#### Scenario: Non-normalized type key is rejected
- **WHEN** a resource is created with type key `"Meeting Room"` (contains upper-case and a space)
- **THEN** creation is rejected with a validation failure identifying the type key

#### Scenario: Future type keys are not rejected for being unknown
- **WHEN** a resource is created with a well-formed type key that is not `room` (e.g. `person`)
- **THEN** creation succeeds — the domain does not hard-code the set of valid types

### Requirement: Single-occupancy semantics
A v1 resource SHALL be single-occupancy: at any instant, at most one blocking booking claim (see `bookings`) may exist for the resource. The domain model SHALL NOT expose a capacity concept in v1; introducing capacity later MUST be possible as an additive change.

#### Scenario: Occupied time is not free
- **WHEN** a resource has a confirmed booking claim from 10:00 to 11:00 on a given date
- **THEN** the availability projection for that date (see `availability`) reports no free time between 10:00 and 11:00

### Requirement: Domain purity
`UBookIt.Core` SHALL NOT reference Umbraco packages, Entity Framework, or any third-party package. This change introduces no new dependencies. References to Umbraco concepts (such as a member) SHALL be expressed as opaque BCL types.

#### Scenario: Core has no external references
- **WHEN** the `UBookIt.Core` project's package and project references are inspected
- **THEN** it references no Umbraco, EF Core, or third-party packages

### Requirement: Behaviour/presentation split
The domain SHALL NOT model presentation concerns (images, rich descriptions, URLs, culture variants). Content nodes presenting a resource are a later concern that references the resource by id; nothing in `UBookIt.Core` SHALL depend on how or whether a resource is presented.

#### Scenario: Resource carries no presentation fields
- **WHEN** the `Resource` public surface is inspected
- **THEN** it exposes identity, type, display name, description, and availability configuration only
