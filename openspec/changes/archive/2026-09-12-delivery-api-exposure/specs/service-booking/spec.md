# Delta for service-booking

Found by the sync-time outward sweep (task 6.1): a sentence stated the delivery API as
unconditionally available, which the off-by-default flip falsified. The requirement is
replaced with its body verbatim except the amendment described in tasks 6.1.

## MODIFIED Requirements

### Requirement: Eligibility remains derivable from public reads
Where the delivery API's read direction is exposed, every input to the eligibility
rule SHALL be readable through it: a resource's type and capabilities, and, for
**every one of a service's roles**, its resource type and required capabilities. A
caller SHALL therefore be able to compute each role's candidate pool from public
reads alone, without probing.

A role's **count** SHALL also be published. It is not an input to eligibility, but it
determines how many distinct resources a role consumes, so without it a caller can
compute the pools and still not know what the service requires of them.

**On a site exposing placement without reads, the parity this rests on is absent by
that site's own choice.** The placement failures below still disclose what they
disclose — pool membership, and occupancy at an instant — while the reads that made
those facts "already derivable" are switched off. That combination is legal and the
disclosure is accepted, because the site chose the asymmetry knowingly and the
alternative — changing failure codes by configuration — would make the API's contract
unstable; but the justification below SHALL be read as holding in full only where
reads are exposed.

This SHALL be treated as a standing constraint rather than a convenience. The
`resource-not-eligible` failure returned for an out-of-pool `pinnedResourceId`
discloses pool membership; it is acceptable precisely because the same fact is
already derivable. Any future change that constrains eligibility by data not
published here SHALL either publish that data or revisit that failure, and SHALL NOT
leave the two silently out of step.

The `pinned-resource-unavailable` failure SHALL be held to the same standard, and
SHALL be understood to disclose a resource's occupancy at an instant rather than its
pool membership. It is acceptable on the same grounds and no others: a resource's
free time is already published, so the failure tells a caller nothing a bookable-
starts read would not. Any future change that hides per-resource availability
SHALL revisit this failure, which would otherwise become an occupancy oracle for a
resource whose calendar the API had stopped publishing.

#### Scenario: A pool is computable from public reads
- **WHEN** an anonymous caller reads a service and the resource list
- **THEN** the caller can determine which resources are eligible for each of the service's roles without attempting a booking

#### Scenario: What a service requires is computable from public reads
- **WHEN** an anonymous caller reads a service whose role has a count greater than one
- **THEN** the published role states that count

#### Scenario: Probing an ineligible resource discloses nothing new
- **WHEN** an anonymous caller submits a `pinnedResourceId` naming a resource outside every pool and receives `resource-not-eligible`
- **THEN** the disclosed fact was already derivable from the published resource and service reads

#### Scenario: A refused pin discloses nothing not already published
- **WHEN** an anonymous caller submits a `pinnedResourceId` naming an eligible resource and receives `pinned-resource-unavailable`
- **THEN** the disclosed fact — that the resource is not free at that instant — was already derivable from that resource's published bookable starts
