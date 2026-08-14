## MODIFIED Requirements

### Requirement: Eligible resource resolution
`UBookIt.Core` SHALL resolve a role and a duration specification to a candidate pool: every resource whose type key equals the role's `ResourceType` **and whose capability set contains every capability the role requires**, further restricted to those whose booking constraints admit at least one length permitted by the duration specification (`ServiceDuration.TryResolveAgainst` succeeding). Eligibility SHALL be determined by resource type key and capability subset alone; no explicit service-to-resource association participates.

Resolution SHALL take the role and duration directly, not a service identifier or a constructed `Service`. A service being edited may not yet be valid — most obviously it may have no name — and requiring an aggregate to be constructible in order to ask which resources a role resolves to would let validation of unrelated fields decide whether the question can be asked at all. Resolving a saved service by id SHALL be a thin wrapper that loads it and delegates.

Resolution SHALL be **one computation**. The candidate pool and any diagnostic view of the same resolution SHALL be projections of a single evaluation rather than separate implementations, so that a report about which resources a service resolves to cannot disagree with the resolution the booking path performs. A diagnostic that computes eligibility independently would be confidently wrong in exactly the case it exists to detect.

A role requiring no capabilities SHALL match every resource of its type, so that a service defined before capabilities were introduced, or defined without them, resolves exactly as it did previously.

The subset test SHALL be evaluated in `UBookIt.Core` through the shared capability value object, over capabilities hydrated by the read port. It SHALL NOT be reimplemented as a storage-layer query predicate: a second implementation could disagree with the first, and eligibility disagreeing with availability is a defect invisible to both.

The candidate pool SHALL be complete and SHALL NOT be paged or truncated: a partial pool would report unavailability that does not exist and place bookings on the wrong resource, without any error being raised. Resolution SHALL depend only on the read ports, never on a management store. Callers on the management surface MAY invoke resolution; that constrains what resolution depends on, not who may ask it.

A resource excluded because its constraints admit no permitted length SHALL be excluded silently — that is an answer about that resource, not a validation failure of the service. A resource excluded because it lacks a required capability SHALL likewise be excluded silently.

#### Scenario: Pool is every resource of the role's type
- **WHEN** a role names resource type `room`, requires no capabilities, and four resources of type `room` and three of type `therapist` exist
- **THEN** the candidate pool is exactly the four `room` resources

#### Scenario: Resolution does not require a valid service
- **WHEN** a role and a duration are resolved for a service that has no name and has never been saved
- **THEN** the candidate pool is returned, and no name-related validation failure is raised

#### Scenario: The pool is the same however resolution is reached
- **WHEN** the same role and duration are resolved directly and by loading a saved service carrying them
- **THEN** both produce the same candidate pool

#### Scenario: The diagnostic view and the candidate pool agree
- **WHEN** a resolution's diagnostic view and its candidate pool are compared for the same role and duration
- **THEN** the diagnostic's final stage contains exactly the resources in the candidate pool

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
- **WHEN** a duration is fixed at 120 minutes and a candidate resource's maximum duration is 90 minutes
- **THEN** that resource is not in the candidate pool, and no validation failure is raised for it

#### Scenario: A resource whose granularity admits no permitted length is excluded
- **WHEN** a duration permits lengths between 40 and 50 minutes and a candidate resource has 30-minute granularity, so no multiple of 30 lies in the intersection
- **THEN** that resource is not in the candidate pool

#### Scenario: Pool is not truncated by paging
- **WHEN** the candidate pool for a role's type contains more resources than the read port's default page size
- **THEN** every resource of that type is still considered

#### Scenario: Unknown service
- **WHEN** eligibility is resolved for a service id that does not exist
- **THEN** the operation fails with code `service-not-found`

## ADDED Requirements

### Requirement: Resolution reports why each resource was excluded
A resolution SHALL be observable as an ordered chain of three stages — resources of the role's type, those of them satisfying the role's required capabilities, and those of them whose constraints admit a length the duration specification permits — so that a consumer can attribute an empty or narrowed pool to the filter responsible for it.

Reporting only the surviving count SHALL NOT be sufficient. A single number cannot distinguish a mistyped resource type from an over-narrow capability set from a duration no resource can provide, and those three faults are corrected in three different places. The count entering each stage and the count leaving it SHALL both be derivable.

For resources excluded at the duration stage, the resolution SHALL identify which resources were excluded and the bound that excluded them, since that is the fault the editor must act on.

#### Scenario: The chain attributes a narrowed pool
- **WHEN** ten `room` resources exist, three carry `projector`, and only one of those three can provide four hours
- **THEN** the resolution reports ten at the type stage, three at the capability stage, and one at the duration stage

#### Scenario: An empty pool names the stage that emptied it
- **WHEN** no resource carries a required capability
- **THEN** the capability stage reports zero while the type stage reports the resources of that type, so the fault is attributable to the capabilities rather than the type

#### Scenario: A mistyped resource type is distinguishable from a capability problem
- **WHEN** the role names a resource type no resource uses and also requires capabilities
- **THEN** the type stage reports zero, and the capability stage is not reported as the cause

#### Scenario: Duration exclusions name the resources and their limiting bound
- **WHEN** two resources carry the required capabilities but their maximum duration is shorter than a fixed service duration
- **THEN** the resolution identifies those two resources and the bound that excluded them

#### Scenario: A healthy configuration excludes nothing
- **WHEN** every resource of the role's type carries the required capabilities and can provide the duration
- **THEN** all three stages report the same count and no exclusions are listed
