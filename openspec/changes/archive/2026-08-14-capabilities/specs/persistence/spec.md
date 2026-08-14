## MODIFIED Requirements

### Requirement: Schema shape and naming
All uBookIt tables SHALL carry the `uBookIt` prefix. The schema SHALL comprise: `uBookItResource` (id, type key, display name, description, constraint values), `uBookItResourceOpenHours` (per weekly window: day of week, start time, end time), `uBookItResourceException` (per exception window: date, nullable start/end times where a closure is a single row with NULL times), `uBookItResourceCapability` (resource id and capability key, unique per pair), `uBookItServiceRoleCapability` (service role id and capability key, unique per pair), `uBookItBooking` (UTC start and end, IANA time zone id, status, created UTC, nullable member key, booker name, email, nullable phone), and `uBookItResourceClaim` (booking id, resource id, unique per pair). The mapping SHALL round-trip the Core value objects without loss.

Each capability table SHALL enforce uniqueness of its owner-and-key pair at the schema level, so that a duplicate capability is impossible in storage and not only in the domain. Capability rows SHALL be removed with their owning resource or service role.

#### Scenario: Resource availability round-trips
- **WHEN** a resource with weekly windows, a closure exception, an override exception, and non-default constraints is saved and reloaded through the store
- **THEN** the reloaded `AvailabilityConfiguration` is value-equal to the original

#### Scenario: Resource capabilities round-trip
- **WHEN** a resource carrying two capabilities is saved and reloaded through the store
- **THEN** the reloaded capability set is value-equal to the original

#### Scenario: Role required capabilities round-trip
- **WHEN** a service whose role requires two capabilities is saved and reloaded through the store
- **THEN** the reloaded required-capability set is value-equal to the original

#### Scenario: An empty capability set round-trips as empty
- **WHEN** a resource carrying no capabilities is saved and reloaded
- **THEN** the reloaded capability set is empty rather than null

#### Scenario: Deleting an owner removes its capability rows
- **WHEN** a resource carrying capabilities is deleted
- **THEN** its capability rows are removed and no orphan remains

#### Scenario: Booking round-trips
- **WHEN** a placed booking is reloaded through the store
- **THEN** its interval UTC instants, zone id, status, booker (member key and contact details), created timestamp, and claims are value-equal to what was placed

## ADDED Requirements

### Requirement: Capability hydration on the read path
Resource reads used by candidate resolution SHALL hydrate each resource's capability set, so that the eligibility subset test can be evaluated in `UBookIt.Core`. The storage layer SHALL NOT implement a capability-filtered query as an alternative route to eligibility: one implementation of the rule, in the domain, is what prevents storage and domain from disagreeing about which resources are eligible.

#### Scenario: Resources listed by type carry their capabilities
- **WHEN** resources of a given type are listed through the read port
- **THEN** each returned resource carries its capability set, hydrated from storage

#### Scenario: Eligibility is not evaluated in SQL
- **WHEN** the persistence layer is inspected
- **THEN** no query filters resources by required capabilities as a substitute for the domain's subset test

### Requirement: Capability projections on the management store
The management store SHALL provide the distinct capability keys in use with their resource counts, and the resources matching a given type key and required-capability set. Both SHALL be projections over existing storage. The usage projection SHALL aggregate server-side and SHALL be ordered deterministically by key.

These projections SHALL live on the management store only. The read port used by booking SHALL NOT gain them: the backoffice's needs are not the booking path's needs, and keeping the ports separate is what stopped one reaching across the other previously.

#### Scenario: Usage projection aggregates server-side
- **WHEN** the capability usage projection runs
- **THEN** the counts are computed by the database, and the result scales with the number of distinct keys rather than the number of resources

#### Scenario: Usage projection is deterministically ordered
- **WHEN** the capability usage projection is run twice against unchanged data
- **THEN** both results present the same keys in the same order

#### Scenario: Match projection returns matching resources
- **WHEN** the match projection is run for a type key and a set of required capabilities
- **THEN** it returns exactly the resources of that type carrying every required capability

#### Scenario: Projections stay off the read port
- **WHEN** the read port used by candidate resolution is inspected
- **THEN** it exposes neither the usage projection nor the match projection
