## MODIFIED Requirements

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

## ADDED Requirements

### Requirement: Capability keys are extensible normalized keys
A capability key SHALL be a normalized string key (lower-case, kebab-case, non-empty), under the same rule as resource type keys, so that a site may introduce any capability vocabulary without schema or breaking API changes. A key that does not match the normalized form SHALL be rejected at creation with the stable code `capability-key-invalid` — distinct from `type-key-invalid`, so a consumer can tell which field is at fault when both are present. Keys SHALL be rejected rather than silently normalized: the value stored SHALL be the value supplied.

The domain SHALL NOT hold a closed list of valid capability keys.

#### Scenario: Non-normalized capability key is rejected
- **WHEN** a resource is created with the capability key `"Cert X"` (upper-case and a space)
- **THEN** creation is rejected with the `capability-key-invalid` code

#### Scenario: Keys are not silently normalized
- **WHEN** a resource is created with the capability key `Massage`
- **THEN** creation is rejected rather than accepted as `massage`

#### Scenario: Unknown capability keys are not rejected for being unknown
- **WHEN** a resource is created with a well-formed capability key the package has never seen
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
