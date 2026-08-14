## MODIFIED Requirements

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
