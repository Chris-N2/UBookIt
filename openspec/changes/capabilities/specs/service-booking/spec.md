## MODIFIED Requirements

### Requirement: Eligible resource resolution
`UBookIt.Core` SHALL resolve a service's single role to its candidate pool: every resource whose type key equals the role's `ResourceType` **and whose capability set contains every capability the role requires**, further restricted to those whose booking constraints admit at least one length permitted by the service's duration specification (`ServiceDuration.TryResolveAgainst` succeeding). Eligibility SHALL be determined by resource type key and capability subset alone; no explicit service-to-resource association participates.

A role requiring no capabilities SHALL match every resource of its type, so that a service defined before capabilities were introduced, or defined without them, resolves exactly as it did previously.

The subset test SHALL be evaluated in `UBookIt.Core` through the shared capability value object, over capabilities hydrated by the read port. It SHALL NOT be reimplemented as a storage-layer query predicate: a second implementation could disagree with the first, and eligibility disagreeing with availability is a defect invisible to both.

The candidate pool SHALL be complete and SHALL NOT be paged or truncated: a partial pool would report unavailability that does not exist and place bookings on the wrong resource, without any error being raised. Resolution SHALL depend only on the read ports, never on a management store.

A resource excluded because its constraints admit no permitted length SHALL be excluded silently — that is an answer about that resource, not a validation failure of the service. A resource excluded because it lacks a required capability SHALL likewise be excluded silently.

#### Scenario: Pool is every resource of the role's type
- **WHEN** a service's role names resource type `room`, requires no capabilities, and four resources of type `room` and three of type `therapist` exist
- **THEN** the candidate pool is exactly the four `room` resources

#### Scenario: A required capability narrows the pool
- **WHEN** a role names type `therapist` and requires `cert-x`, and of three therapists only Mary carries `cert-x`
- **THEN** the candidate pool is exactly Mary, and no validation failure is raised for the others

#### Scenario: All required capabilities must be present
- **WHEN** a role requires both `cert-x` and `welsh`, and a therapist carries only `cert-x`
- **THEN** that therapist is not in the candidate pool

#### Scenario: Extra capabilities do not disqualify
- **WHEN** a role requires `cert-x` and a therapist carries `cert-x`, `welsh`, and `massage`
- **THEN** that therapist is in the candidate pool

#### Scenario: Capabilities do not cross type boundaries
- **WHEN** a role names type `room` and requires `cert-x`, and a `therapist` resource carries `cert-x` while no `room` does
- **THEN** the candidate pool is empty — the therapist is not eligible for a room role

#### Scenario: A role requiring no capabilities is unaffected
- **WHEN** a role requires no capabilities and resources of its type carry assorted capabilities, including none at all
- **THEN** every resource of that type is in the candidate pool

#### Scenario: Overlapping pools resolve independently per role
- **WHEN** one role requires `cert-x` (matching only Mary) and another role of the same type requires nothing (matching Mary and Frank)
- **THEN** each role resolves to its own complete pool, and the pools overlap rather than partitioning the resources

#### Scenario: A resource whose range cannot admit the service is excluded
- **WHEN** a service is fixed at 120 minutes and a candidate resource's maximum duration is 90 minutes
- **THEN** that resource is not in the candidate pool, and no validation failure is raised for it

#### Scenario: A resource whose granularity admits no permitted length is excluded
- **WHEN** a service permits lengths between 40 and 50 minutes and a candidate resource has 30-minute granularity, so no multiple of 30 lies in the intersection
- **THEN** that resource is not in the candidate pool

#### Scenario: Pool is not truncated by paging
- **WHEN** the candidate pool for a role's type contains more resources than the read port's default page size
- **THEN** every resource of that type is still considered

#### Scenario: Unknown service
- **WHEN** eligibility is resolved for a service id that does not exist
- **THEN** the operation fails with code `service-not-found`

## ADDED Requirements

### Requirement: Eligibility remains derivable from public reads
Every input to the eligibility rule SHALL be readable through the anonymous delivery API: a resource's type and capabilities, and a role's resource type and required capabilities. A caller SHALL therefore be able to compute a service's candidate pool from public reads alone, without probing.

This SHALL be treated as a standing constraint rather than a convenience. The `resource-not-eligible` failure returned for an out-of-pool `preferredResourceId` discloses pool membership; it is acceptable precisely because the same fact is already derivable. Any future change that constrains eligibility by data not published here SHALL either publish that data or revisit that failure, and SHALL NOT leave the two silently out of step.

#### Scenario: A pool is computable from public reads
- **WHEN** an anonymous caller reads a service and the resource list
- **THEN** the caller can determine which resources are eligible for the service's role without attempting a booking

#### Scenario: Probing an ineligible resource discloses nothing new
- **WHEN** an anonymous caller submits a `preferredResourceId` naming a resource outside the pool and receives `resource-not-eligible`
- **THEN** the disclosed fact was already derivable from the published resource and service reads

### Requirement: Overlapping eligibility pools are a known boundary
Capability-constrained eligibility SHALL be understood to produce eligibility pools that overlap without being identical — a role requiring a capability resolves to a strict subset of the pool of a role requiring none of the same type. Single-role resolution SHALL be unaffected by this, since each role resolves independently.

The specification SHALL record that assigning several roles across overlapping pools by taking each role's first available candidate can produce a wrong answer rather than a slow one, and that multi-role composition therefore requires real assignment rather than greedy selection. Multi-role composition remains out of scope; `Service.Create` continues to reject more than one role and any count other than 1.

#### Scenario: Multi-role composition remains rejected
- **WHEN** a service is created with two roles
- **THEN** creation is rejected with the `service-role-invalid` code

#### Scenario: Fixtures exercise overlapping pools
- **WHEN** the capability test fixtures are inspected
- **THEN** they contain at least one pair of roles whose eligible pools overlap without being equal, so that a later greedy assignment defect is detectable rather than invisible
