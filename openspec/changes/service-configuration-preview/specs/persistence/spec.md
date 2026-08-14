## MODIFIED Requirements

### Requirement: Capability projections on the management store
The management store SHALL provide the distinct capability keys in use with their resource counts, as a projection over existing storage. It SHALL aggregate server-side and SHALL be ordered deterministically by key.

The management store SHALL NOT provide a projection that selects resources by required capabilities. Matching resources to a role is an eligibility question, and eligibility SHALL have exactly one implementation, in `UBookIt.Core`, over resources the read port hydrates. A storage-layer projection applying the same rule is a second implementation free to diverge — in the predicate itself, in its ordering, or in the test doubles that stand in for it — and a backoffice answer that diverges from the booking path's is worse than no answer.

This projection SHALL live on the management store only. The read port used by booking SHALL NOT gain it: the backoffice's needs are not the booking path's needs, and keeping the ports separate is what stopped one reaching across the other previously.

#### Scenario: Usage projection aggregates server-side
- **WHEN** the capability usage projection runs
- **THEN** the counts are computed by the database, and the result scales with the number of distinct keys rather than the number of resources

#### Scenario: Usage projection is deterministically ordered
- **WHEN** the capability usage projection is run twice against unchanged data
- **THEN** both results present the same keys in the same order

#### Scenario: No capability matching in storage
- **WHEN** the persistence layer is inspected
- **THEN** no projection or query selects resources by a required-capability set

#### Scenario: Projections stay off the read port
- **WHEN** the read port used by candidate resolution is inspected
- **THEN** it does not expose the usage projection
