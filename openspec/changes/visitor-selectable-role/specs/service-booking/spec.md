## ADDED Requirements

### Requirement: Availability conditional on a pinned resource
`UBookIt.Core`'s service bookable-starts query SHALL accept an **optional pinned
resource id**. When one is supplied, a start and a length SHALL be offered exactly
when a saturating assignment of distinct resources **including that resource** exists
there — the same feasibility test placement applies to a pinned request, asked over
the same candidate pools.

The answer SHALL be **conditional**, not a new claim about the service: it states
when the service can be booked *given* that resource, and it is honoured at placement
only when the same pin is supplied there. Supplying no pin SHALL leave the query's
behaviour, its result and its guarantees exactly as they are — in particular the
unpinned response still names no resource and still promises nothing about which
candidate a booker will get.

The pinned result SHALL be a **subset** of the unpinned result at every start and
length. Pinning constrains the assignment and can only remove possibilities, so a
pinned query offering a start the unpinned query does not is a fault, never a
richer answer.

This SHALL be one computation with two behaviours, not two computations. The
feasibility test at each start SHALL be the same code path in both cases, differing
only in whether the assignment is required to include the pinned resource — because a
separately derived pinned answer would be free to disagree with placement in exactly
the case it exists to serve.

**A single-slot service SHALL be filtered by the pin like any other.** A service of
one role with a count of 1 is the most common configuration there is, and its
feasibility is decided without consulting the assignment at all — a saturating
assignment exists there exactly when some candidate admits the length. The pin SHALL
narrow that decision to the pinned candidate. Offering a start at which only *another*
resource is free would be a promise the pinned placement then refuses.

#### Scenario: A pinned query offers only starts that resource can serve
- **WHEN** bookable starts are queried for a service pinning a resource that is claimed for part of the day
- **THEN** the starts overlapping that claim are absent, and the remaining starts are those at which an assignment including that resource exists

#### Scenario: A single-role service honours the pin
- **WHEN** a service has one role of count 1 with two eligible candidates, and bookable starts are queried pinning the one that is fully claimed that day
- **THEN** no starts are offered for that day, even though the other candidate is free throughout

#### Scenario: The pinned answer never exceeds the unpinned one
- **WHEN** the same range is queried for a service with and without a pin
- **THEN** every start and length offered by the pinned query is also offered by the unpinned query

#### Scenario: An unpinned query is unchanged
- **WHEN** bookable starts are queried without a pin
- **THEN** the response is exactly what it was before pinning existed, naming no resource

#### Scenario: A pinned start is bookable with the same pin
- **WHEN** a start and length offered by a pinned query are submitted for placement with that same pin
- **THEN** placement succeeds, absent a concurrent claim

#### Scenario: A resource eligible for several roles is pinned into one of them
- **WHEN** a service has two roles whose pools overlap, and a resource eligible for both is pinned
- **THEN** the starts offered are those at which some assignment places that resource in either role and fills the other from the remaining candidates

### Requirement: A pinned resource outside every pool is rejected by the availability query
The service bookable-starts query SHALL reject a pinned resource id that appears in no
role's candidate pool with the stable `resource-not-eligible` code, rather than
ignoring it and answering as though no pin had been given.

This is the rule placement already applies, moved one step earlier. A query that
silently discarded the pin would answer "here is when this service is available"
while the caller believed it had asked about a particular resource — and the caller
would then submit a start it had been told was good, to be refused at placement. The
disclosure position is unchanged: pool membership is already computable from public
reads, so rejecting rather than ignoring reveals nothing new.

An unknown service, an invalid date range and an over-wide range SHALL continue to be
reported as they are, and SHALL be reported in preference to the pin failure when both
apply — the range and the service bound whether the question can be asked at all.

#### Scenario: A pin naming an ineligible resource is rejected
- **WHEN** bookable starts are queried for a service pinning a resource whose type matches no role
- **THEN** the query fails with `resource-not-eligible` rather than returning starts

#### Scenario: A pin naming a resource excluded by capabilities is rejected
- **WHEN** bookable starts are queried pinning a resource of the right type that lacks a required capability
- **THEN** the query fails with `resource-not-eligible`

#### Scenario: A pin naming an unknown resource is rejected
- **WHEN** bookable starts are queried pinning a resource id that does not exist
- **THEN** the query fails with `resource-not-eligible`

#### Scenario: An unknown service is reported before the pin
- **WHEN** bookable starts are queried for a service id that does not exist, with a pin
- **THEN** the failure is `service-not-found`

### Requirement: Structural unfulfillability is answerable from Core
`UBookIt.Core` SHALL expose, as a single function over a service's resolved role
candidates, whether the service can **ever** be fulfilled as configured, and SHALL
answer it from all three structural questions together: whether the roles' slots can
be filled at once by distinct resources (which subsumes a role with no eligible
resource), whether the roles' start grids can ever coincide, and whether any length
exists that every role can provide.

Every surface that reports a service permanently unbookable SHALL derive its answer
from that one function. Answering only the first question would leave the other two
reporting ordinary unavailability forever, which is the exact confusion the
distinction exists to remove; and a second implementation of the rule elsewhere would
be free to disagree with the first in precisely the case it exists to detect.

The function SHALL be a **pure** function of the resolved candidates — no store, no
clock, no HTTP context — so it can be exercised directly rather than through a host,
and so no consumer needs to be constructible in order to ask the question.

It SHALL answer about **configuration only**. Its three questions concern pools,
grids and duration ranges; none of them consults the booking calendar, so a service
this function calls fulfillable MAY still have no availability today. The
complementary claim is not available and SHALL NOT be inferred: this function may
report that a service can never be fulfilled, and may never report that a service
*is* available.

#### Scenario: A shortfall is reported as permanent
- **WHEN** a service requires two therapists and only one exists
- **THEN** the function reports it permanently unfulfillable

#### Scenario: A permanent grid misalignment is reported as permanent
- **WHEN** a service's two roles have start grids that can never coincide
- **THEN** the function reports it permanently unfulfillable

#### Scenario: No common length is reported as permanent
- **WHEN** no length exists that every one of a service's roles can provide
- **THEN** the function reports it permanently unfulfillable

#### Scenario: A fulfillable service with a full calendar is not permanent
- **WHEN** a correctly configured service is fully booked for the queried range
- **THEN** the function does not report it permanently unfulfillable

#### Scenario: One function answers for every surface
- **WHEN** the same permanently unfulfillable service is reported by the in-process booking flow and by the public availability read
- **THEN** both derive their answer from this function rather than evaluating the rules themselves
