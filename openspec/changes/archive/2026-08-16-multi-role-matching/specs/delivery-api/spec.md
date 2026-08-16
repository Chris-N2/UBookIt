## MODIFIED Requirements

### Requirement: Service read model
The delivery API SHALL expose a public service read model over `GET /services`
(paged) and `GET /services/{id}`. The read model SHALL carry what a consumer needs to
present and drive a service booking: id, name, the duration specification (its kind,
and whichever bounds apply), and **every role**, each with its resource type key, its
required capability keys, and its **count**. It SHALL NOT expose management-only or
internal structure. Roles SHALL be returned in a deterministic order, and required
capability keys SHALL be returned in a deterministic order; a role requiring none
SHALL return an empty collection rather than a null or an omitted member. An unknown
id SHALL yield a 404 problem-details response carrying `service-not-found`. The
endpoints SHALL be anonymous, consistent with the delivery API's auth stance.

Roles SHALL be published as a collection even for a single-role service, so a
consumer written against this contract does not need changing when a service gains a
role.

A role's count SHALL be published because it states how many **distinct** resources
that role consumes at once. Without it a consumer can compute each role's candidate
pool and still not know what the service requires of it, and would present "one
therapist" for a service that needs two. The count SHALL always be present, carrying
1 for a role requiring a single resource, so a consumer never has to treat its
absence as a default.

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
- **THEN** the response carries its id, name, duration specification, and every role with its resource type key, required capabilities, and count

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
