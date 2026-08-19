## MODIFIED Requirements

### Requirement: Service read model
The delivery API SHALL expose a public service read model over `GET /services`
(paged) and `GET /services/{id}`. The read model SHALL carry what a consumer needs to
present and drive a service booking: id, name, the duration specification (its kind,
and whichever bounds apply), and **every role**, each with its resource type key, its
required capability keys, its **count**, and whether a visitor may **choose** which
resource fills it. It SHALL NOT expose management-only or internal structure. Roles
SHALL be returned in a deterministic order, and required capability keys SHALL be
returned in a deterministic order; a role requiring none SHALL return an empty
collection rather than a null or an omitted member. An unknown id SHALL yield a 404
problem-details response carrying `service-not-found`. The endpoints SHALL be
anonymous, consistent with the delivery API's auth stance.

Roles SHALL be published as a collection even for a single-role service, so a
consumer written against this contract does not need changing when a service gains a
role.

A role's count SHALL be published because it states how many **distinct** resources
that role consumes at once. Without it a consumer can compute each role's candidate
pool and still not know what the service requires of it, and would present "one
therapist" for a service that needs two. The count SHALL always be present, carrying
1 for a role requiring a single resource, so a consumer never has to treat its
absence as a default.

A role's **visitor-selectability** SHALL be published for the same reason: it is what
tells a consumer which pool, if any, to offer as a choice. At most one role of a
service carries it. It SHALL always be present, carrying false for a role that does
not offer a choice, so a consumer never has to treat its absence as a default.

Publishing it SHALL NOT be read as an access rule. It states which choice the service
is configured to offer; it does not restrict which resource a placement request may
pin, and it conceals nothing, since every role's candidate pool is already computable
from these same reads.

The deterministic role order SHALL remain stable across reads for a service whose
roles share a resource type, since type alone no longer distinguishes them.

The duration specification SHALL be rendered so a consumer can distinguish a fixed
length from a booker-chosen range without re-deriving the rule from nullable fields:
the kind SHALL be explicit, and lengths SHALL be whole minutes.

Publishing every role's required capabilities alongside each resource's capabilities
SHALL make a service's candidate pools computable by an anonymous consumer, which is
what keeps the `resource-not-eligible` failure free of any disclosure the public
reads do not already make.

#### Scenario: List returns a page and total
- **WHEN** `GET /services` is requested with paging parameters
- **THEN** the response carries the page of public service models and the unpaged total

#### Scenario: Get by id returns the public model
- **WHEN** `GET /services/{id}` is requested for an existing service
- **THEN** the response carries its id, name, duration specification, and every role with its resource type key, required capabilities, count, and visitor-selectability

#### Scenario: A multi-role service publishes every role
- **WHEN** a service requiring a `room` and a `therapist` is read
- **THEN** the response carries both roles in a deterministic order

#### Scenario: A single-role service still publishes a collection
- **WHEN** a service with one role is read
- **THEN** its roles are carried as a collection of one, not as a single inline role

#### Scenario: A role requiring no capabilities returns an empty collection
- **WHEN** a service one of whose roles requires no capabilities is read
- **THEN** that role carries an empty required-capability collection, not a null or absent member

#### Scenario: A role's count is published
- **WHEN** a service with a role of count 2 is read
- **THEN** that role carries a count of 2

#### Scenario: A count of one is stated rather than omitted
- **WHEN** a service whose roles each require a single resource is read
- **THEN** every role carries a count of 1 explicitly

#### Scenario: A visitor-selectable role is identifiable
- **WHEN** a service with a `room` role and a visitor-selectable `therapist` role is read
- **THEN** the `therapist` role carries visitor-selectability true and the `room` role carries it false

#### Scenario: Visitor-selectability is stated rather than omitted
- **WHEN** a service offering no choice at all is read
- **THEN** every role carries visitor-selectability explicitly as false

#### Scenario: Two roles of one type are published separately and stably
- **WHEN** a service with two roles naming `therapist` and differing in required capabilities is read twice
- **THEN** both roles are published, distinguishable by their required capabilities, and in the same order both times

#### Scenario: A consumer can compute the candidate pools
- **WHEN** a consumer reads a service and the resource list
- **THEN** for each role, the resources whose type matches and whose capabilities include every required capability are identifiable without any further request

#### Scenario: Fixed and variable durations are distinguishable
- **WHEN** a fixed-duration service and an unbounded variable-duration service are both read
- **THEN** each response states its duration kind explicitly, and the consumer need not infer it from which bounds are present

#### Scenario: Unknown service id
- **WHEN** `GET /services/{id}` is requested for an id that does not exist
- **THEN** the response is 404 problem details carrying the `service-not-found` code

#### Scenario: Anonymous access is permitted
- **WHEN** a service read is made with no credentials
- **THEN** the request succeeds, consistent with the rest of the delivery API

### Requirement: Service bookable-start read
The delivery API SHALL expose a bookable-start query for a service over an inclusive `[from, to]` date range at `GET /services/{id}/bookable-starts`, delegating to the Core service availability query. Each entry SHALL carry the start instant as ISO-8601 UTC together with the lengths bookable at that start, expressed as a list of arithmetic runs, each `{ minDurationMinutes, maxDurationMinutes, stepMinutes }` in whole minutes. The response SHALL carry the site time-zone id once at the top level, consistent with the other availability responses.

The query SHALL accept an **optional pinned resource id**. Supplied, it narrows the answer to the starts and lengths at which an assignment including that resource exists — the question "when can I book this service with this person". Omitted, the query and its response are exactly as they were: the answer is about the service, and no resource is named or implied.

The query SHALL NOT take a requested duration. The response SHALL NOT collapse a start's runs into a single minimum/maximum pair, because a candidate pool of differing granularities and minimums does not offer a contiguous band of lengths and a collapsed pair would advertise unbookable lengths. The response SHALL NOT identify which resource backs any start or run, **pinned or not** — a pinned answer is conditional on a resource the caller already named, so the response body still carries no resource id, and an unpinned answer still makes no promise about which candidate a booker will get.

A pinned response SHALL be honoured by placing with the same pin, and SHALL NOT be read as a reservation: the starts it reports are subject to the same races as any other availability read.

A pinned resource id naming a resource in no role's candidate pool SHALL yield `resource-not-eligible`, rather than being ignored — the rule the placement endpoint already applies.

Within a role, a run arises from one contributing candidate. Across roles a run is **rebuilt from the lengths a saturating assignment of distinct resources can provide**, so a run in a multi-role service's response is not attributable to any single candidate — and after subset elimination a candidate whose lengths another run already offers contributes no run at all. The response therefore SHALL NOT be read as one run per candidate.

A range wider than the configured maximum SHALL yield `date-range-too-large`; a `from` after `to` SHALL yield `date-range-invalid`; an unknown service SHALL yield `service-not-found`. The endpoint SHALL be anonymous.

The existing per-resource `GET /resources/{id}/bookable-starts` endpoint SHALL remain unchanged in route, shape, and semantics.

#### Scenario: Service bookable-start read
- **WHEN** bookable starts are requested for a service over a valid date range
- **THEN** the response carries an ordered list of entries, each with an ISO-8601 UTC start instant and one or more length runs in whole minutes, plus the site zone id

#### Scenario: A pinned query narrows the answer
- **WHEN** bookable starts are requested for a service with a pinned resource id that is claimed for part of the range
- **THEN** the response omits the starts at which no assignment including that resource exists, and carries the rest

#### Scenario: An omitted pin leaves the response unchanged
- **WHEN** bookable starts are requested for a service with no pinned resource id
- **THEN** the response is identical to what it was before the parameter existed

#### Scenario: A pin outside every pool is rejected
- **WHEN** bookable starts are requested pinning a resource that fills no role of that service
- **THEN** the response is 400 problem details carrying the `resource-not-eligible` code

#### Scenario: Heterogeneous pool yields multiple runs
- **WHEN** a start of a single-role service is backed by candidates of differing granularity, none of whose lengths another already offers
- **THEN** that entry carries one run per contributing candidate, rather than a single widened minimum/maximum pair

#### Scenario: Composite runs are not per candidate
- **WHEN** a multi-role service's bookable starts are read
- **THEN** each run denotes the lengths every role can provide, and no run corresponds to a single candidate's own grid

#### Scenario: Response names no resource
- **WHEN** a service bookable-start response body is inspected
- **THEN** no resource id appears anywhere in it

#### Scenario: A pinned response names no resource either
- **WHEN** a pinned service bookable-start response body is inspected
- **THEN** no resource id appears anywhere in it, the pin having been supplied by the caller

#### Scenario: Over-wide range is rejected
- **WHEN** a service bookable-start query requests a range wider than the configured maximum
- **THEN** the response is 400 problem details carrying the `date-range-too-large` code

#### Scenario: Inverted range is rejected
- **WHEN** a service bookable-start query is requested with `from` after `to`
- **THEN** the response is 400 problem details carrying the `date-range-invalid` code

#### Scenario: Unknown service is rejected
- **WHEN** a service bookable-start query names a service id that does not exist
- **THEN** the response is 404 problem details carrying the `service-not-found` code

#### Scenario: The per-resource endpoint is untouched
- **WHEN** `GET /resources/{id}/bookable-starts` is requested
- **THEN** its route, response shape, and semantics are exactly as before this change

## ADDED Requirements

### Requirement: An empty service availability response says when it is permanent
When a service bookable-start query returns no starts, the response SHALL state
whether the service is **permanently unfulfillable as configured** or merely has no
availability in the queried range, as a stable machine-readable code carried on the
response rather than as prose.

An empty response is otherwise ambiguous in the way that matters most: a service whose
roles can never be filled together, whose start grids can never coincide, or for which
no length exists that every role can provide is indistinguishable from a fully booked
week, and a consumer cannot tell whether to offer another date or to stop asking.

The code SHALL derive from the Core structural-unfulfillability function, never from a
second evaluation of the rules in the mapping layer.

It SHALL be **one-directional**. It may report that the service can never be fulfilled
as configured; it SHALL NOT report that a service *is* available, or that it will be
available later, since the structural questions consult no calendar. Absence of the
code on an empty response therefore means "not structurally impossible", never "try
tomorrow and it will work".

It SHALL disclose no configuration detail: not the role, the resource type, the
required capability, the count, nor how many resources exist. The backoffice
diagnostics that name those are for the person who can fix them, and this response is
anonymous.

A response carrying starts SHALL NOT carry the code, and the code SHALL NOT be
accompanied by starts — the two answers are exclusive.

#### Scenario: A structurally unfulfillable service says so
- **WHEN** bookable starts are requested for a service whose two roles can never be filled by distinct resources
- **THEN** the response carries no starts and the stable permanent-unfulfillability code

#### Scenario: A busy week says nothing permanent
- **WHEN** bookable starts are requested for a correctly configured service that is fully booked across the range
- **THEN** the response carries no starts and no permanent-unfulfillability code

#### Scenario: Grid misalignment is reported as permanent
- **WHEN** bookable starts are requested for a service whose roles' start grids can never coincide
- **THEN** the response carries the permanent-unfulfillability code

#### Scenario: No common length is reported as permanent
- **WHEN** bookable starts are requested for a service for which no length exists that every role can provide
- **THEN** the response carries the permanent-unfulfillability code

#### Scenario: The code discloses no configuration
- **WHEN** a permanently unfulfillable service's response is inspected
- **THEN** it names no role, resource type, capability, count, or resource, and reports no pool size

#### Scenario: Starts and the code are exclusive
- **WHEN** any service bookable-start response carrying one or more starts is inspected
- **THEN** it carries no permanent-unfulfillability code
