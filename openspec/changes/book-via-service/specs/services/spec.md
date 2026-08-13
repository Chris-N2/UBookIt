## MODIFIED Requirements

### Requirement: Service duration semantics
A service's duration specification SHALL narrow, and SHALL NEVER widen, the range the fulfilling resource already permits. The effective bookable range for a service on a given resource SHALL be the intersection of the service's range with the resource's `[MinDuration, MaxDuration]`, where a fixed duration is the degenerate range whose minimum and maximum are equal and an absent bound contributes no narrowing. A resource's maximum duration SHALL therefore be a hard ceiling regardless of service configuration.

Service bounds SHALL NOT be required to align to any resource's granularity; they are bounds, not lengths. The lengths actually bookable SHALL be the granularity multiples lying within the effective range.

When the intersection is empty, or when no granularity multiple lies within it, that resource SHALL be treated as unable to fulfil that service. This SHALL NOT be a validation failure at service-definition time, because a service spanning many resources cannot know each resource's limits.

This resolution governs booking behaviour: it is what decides a service's candidate pool and the lengths each candidate offers (see `service-booking`).

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

#### Scenario: The resolution drives the candidate pool
- **WHEN** a service's availability or placement is requested
- **THEN** a resource whose effective range is empty is excluded from the candidate pool, and each remaining candidate offers exactly the granularity multiples within its own effective range
