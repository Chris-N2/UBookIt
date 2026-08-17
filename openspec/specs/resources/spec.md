# resources Specification

## Purpose

Defines the bookable resource domain model for uBookIt: resource identity, extensible resource type keys, the capability keys a resource carries and the value object that holds them, single-occupancy semantics, domain purity of `UBookIt.Core`, and the split between booking behaviour and presentation concerns.

## Requirements

### Requirement: Resource definition
A bookable resource SHALL have a `Guid` identifier, a resource type key, a non-empty display name, an optional description, a set of capability keys, and whether it may be booked on its own. The v1 shipped resource type SHALL be `room`, provided as a constant. The capability set SHALL default to empty, and an empty set SHALL mean the resource carries no capabilities — never that it carries all of them. Whether it may be booked on its own SHALL default to withheld, and SHALL mean only that: a resource withholding it remains fully usable as part of a service.

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

#### Scenario: Direct bookability defaults to withheld
- **WHEN** a resource is created without stating whether it may be booked on its own
- **THEN** the resource is valid and does not permit direct booking

### Requirement: Resource type is an extensible normalized key
The resource type SHALL be a normalized string key (lower-case, kebab-case, non-empty), not an enum, so that future types (e.g. `person`, `equipment`) can be introduced without schema or breaking API changes. Type keys that do not match the normalized form SHALL be rejected at creation.

A type key SHALL also be bounded in length by the domain, under the same limit as capability keys and for the same reason: a well-formed key longer than storage accepts would otherwise pass domain and API validation and fail at write time as an unhandled storage error, where this requirement promises a stable validation code. Over-long type keys SHALL be rejected with `type-key-invalid`, the code that already identifies a malformed type key, since both describe the same field.

#### Scenario: Non-normalized type key is rejected
- **WHEN** a resource is created with type key `"Meeting Room"` (contains upper-case and a space)
- **THEN** creation is rejected with a validation failure identifying the type key

#### Scenario: Future type keys are not rejected for being unknown
- **WHEN** a resource is created with a well-formed type key that is not `room` (e.g. `person`)
- **THEN** creation succeeds — the domain does not hard-code the set of valid types

#### Scenario: An over-long type key is a validation failure
- **WHEN** a resource is created with a well-formed type key longer than storage accepts
- **THEN** creation is rejected with the `type-key-invalid` code rather than failing later as a storage error

#### Scenario: A type key at the length limit is accepted
- **WHEN** a resource is created with a well-formed type key exactly at the maximum length
- **THEN** creation succeeds

#### Scenario: A service role's resource type is bounded the same way
- **WHEN** a service role names a well-formed resource type key longer than storage accepts
- **THEN** creation is rejected with the `type-key-invalid` code

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

### Requirement: A resource states whether it may be booked on its own
A resource SHALL carry whether it may be booked **on its own**, independently of
its type, its capabilities and its availability. The value SHALL default to
**withheld**: a resource that has not been given the permission does not have it.

This is a statement about what the business offers, not about what the system can
compute. A resource may be perfectly available, perfectly eligible and still
meaningless alone — a therapist with no room to work in — and no rule over type,
capability or calendar can distinguish that case from a room that is genuinely
lettable. Only the editor knows, so only the editor may say.

The permission SHALL constrain **direct** booking alone. A resource that withholds
it SHALL remain fully usable as part of a service: it resolves into candidate
pools, contributes to composite availability, and is claimed by a service booking
exactly as before. Withholding it makes a resource unbookable *by itself*, never
unbookable.

The permission SHALL NOT participate in eligibility. Candidate resolution is type,
then required capabilities, then a duration the service permits; adding a fourth
term would make a service's pool depend on whether its members happen to be
separately lettable, which is unrelated to whether they can fulfil the service.

The permission SHALL NOT be a validation rule. No configuration becomes invalid by
withholding it and none becomes valid by granting it, so nothing SHALL be rejected
on its account at save time.

#### Scenario: A new resource withholds the permission
- **WHEN** a resource is created without stating whether it may be booked on its own
- **THEN** it does not permit direct booking

#### Scenario: The permission is independent of availability and capability
- **WHEN** a resource that permits direct booking has its opening hours, capabilities or constraints changed
- **THEN** it still permits direct booking, because the permission describes what is offered rather than what is possible

#### Scenario: A withholding resource is still a service candidate
- **WHEN** a service role resolves over resources of a type, one of which withholds direct booking
- **THEN** that resource appears in the candidate pool exactly as it would have done, and the pool is unchanged by the permission

#### Scenario: Withholding is not a validation failure
- **WHEN** a resource withholding the permission is created or updated
- **THEN** the operation succeeds, because the permission is an offer rather than a rule
