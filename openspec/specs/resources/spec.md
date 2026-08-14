# resources Specification

## Purpose

Defines the bookable resource domain model for uBookIt: resource identity, extensible resource type keys, the capability keys a resource carries and the value object that holds them, single-occupancy semantics, domain purity of `UBookIt.Core`, and the split between booking behaviour and presentation concerns.

## Requirements

### Requirement: Resource definition
A bookable resource SHALL have a `Guid` identifier, a resource type key, a non-empty display name, an optional description, and a set of capability keys. The v1 shipped resource type SHALL be `room`, provided as a constant. The capability set SHALL default to empty, and an empty set SHALL mean the resource carries no capabilities — never that it carries all of them.

#### Scenario: Creating a valid room resource
- **WHEN** a resource is created with type `room` and display name "Meeting Room A"
- **THEN** the resource is valid, has a non-empty `Guid` id, and reports type `room`

#### Scenario: Display name is required
- **WHEN** a resource is created with an empty or whitespace display name
- **THEN** creation is rejected with a validation failure

#### Scenario: Capabilities default to empty
- **WHEN** a resource is created without specifying capabilities
- **THEN** the resource is valid and reports an empty capability set

#### Scenario: A resource carries the capabilities it is given
- **WHEN** a resource is created with capabilities `massage` and `cert-x`
- **THEN** the resource reports both capabilities and no others

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

### Requirement: Capability keys are extensible normalized keys
A capability key SHALL be a normalized string key (lower-case, kebab-case, non-empty), under the same rule as resource type keys, so that a site may introduce any capability vocabulary without schema or breaking API changes. A key that does not match the normalized form SHALL be rejected at creation with the stable code `capability-key-invalid` — distinct from `type-key-invalid`, so a consumer can tell which field is at fault when both are present. Keys SHALL be rejected rather than silently normalized: the value stored SHALL be the value supplied.

The domain SHALL NOT hold a closed list of valid capability keys.

A key SHALL also be bounded in length by the domain, not only by the storage
column. A well-formed key longer than storage accepts would otherwise pass
domain and API validation and fail at write time as an unhandled storage error —
a 500 where this requirement promises a stable validation code.

#### Scenario: Non-normalized capability key is rejected
- **WHEN** a resource is created with the capability key `"Cert X"` (upper-case and a space)
- **THEN** creation is rejected with the `capability-key-invalid` code

#### Scenario: Keys are not silently normalized
- **WHEN** a resource is created with the capability key `Massage`
- **THEN** creation is rejected rather than accepted as `massage`

#### Scenario: Unknown capability keys are not rejected for being unknown
- **WHEN** a resource is created with a well-formed capability key the package has never seen
- **THEN** creation succeeds

#### Scenario: An over-long capability key is a validation failure
- **WHEN** a resource is created with a well-formed capability key longer than storage accepts
- **THEN** creation is rejected with the `capability-key-invalid` code rather than failing later as a storage error

#### Scenario: A key at the length limit is accepted
- **WHEN** a resource is created with a well-formed capability key exactly at the maximum length
- **THEN** creation succeeds

### Requirement: A capability set is a value object with structural equality
Capability sets SHALL be represented by a dedicated value object, constructible only through validating factories, that normalizes its contents (deduplicated, deterministically ordered) and compares by value. Two sets carrying the same keys SHALL be equal regardless of the order or duplication in which those keys were supplied, so that types containing a capability set retain value-equality semantics.

The value object SHALL own the subset test used to decide eligibility, so that no consumer re-implements it.

#### Scenario: Sets with the same keys are equal
- **WHEN** one set is built from `["cert-x", "massage"]` and another from `["massage", "cert-x", "massage"]`
- **THEN** the two sets are equal and both report exactly two capabilities

#### Scenario: A record carrying a capability set compares by value
- **WHEN** two otherwise-identical records each carry a separately constructed set of the same capabilities
- **THEN** the two records are equal

#### Scenario: Subset test is satisfied by a superset
- **WHEN** a required set of `["cert-x"]` is tested against a held set of `["cert-x", "massage"]`
- **THEN** the requirement is satisfied

#### Scenario: Subset test fails on a missing capability
- **WHEN** a required set of `["cert-x", "welsh"]` is tested against a held set of `["cert-x", "massage"]`
- **THEN** the requirement is not satisfied

#### Scenario: An empty requirement is satisfied by anything
- **WHEN** an empty required set is tested against any held set, including an empty one
- **THEN** the requirement is satisfied
