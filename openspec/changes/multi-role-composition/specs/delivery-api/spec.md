## MODIFIED Requirements

### Requirement: Service read model
The delivery API SHALL expose a public service read model over `GET /services` (paged) and `GET /services/{id}`. The read model SHALL carry what a consumer needs to present and drive a service booking: id, name, the duration specification (its kind, and whichever bounds apply), and **every role**, each with its resource type key and its required capability keys. It SHALL NOT expose management-only or internal structure. Roles SHALL be returned in a deterministic order, and required capability keys SHALL be returned in a deterministic order; a role requiring none SHALL return an empty collection rather than a null or an omitted member. An unknown id SHALL yield a 404 problem-details response carrying `service-not-found`. The endpoints SHALL be anonymous, consistent with the delivery API's auth stance.

Roles SHALL be published as a collection even for a single-role service, so a consumer written against this contract does not need changing when a service gains a role.

The duration specification SHALL be rendered so a consumer can distinguish a fixed length from a booker-chosen range without re-deriving the rule from nullable fields: the kind SHALL be explicit, and lengths SHALL be whole minutes.

Publishing every role's required capabilities alongside each resource's capabilities SHALL make a service's candidate pools computable by an anonymous consumer, which is what keeps the `resource-not-eligible` failure free of any disclosure the public reads do not already make.

#### Scenario: List returns a page and total
- **WHEN** `GET /services` is requested with paging parameters
- **THEN** the response carries the page of public service models and the unpaged total

#### Scenario: Get by id returns the public model
- **WHEN** `GET /services/{id}` is requested for an existing service
- **THEN** the response carries its id, name, duration specification, and every role with its resource type key and required capabilities

#### Scenario: A multi-role service publishes every role
- **WHEN** a service requiring a `room` and a `therapist` is read
- **THEN** the response carries both roles in a deterministic order

#### Scenario: A single-role service still publishes a collection
- **WHEN** a service with one role is read
- **THEN** its roles are carried as a collection of one, not as a single inline role

#### Scenario: A role requiring no capabilities returns an empty collection
- **WHEN** a service one of whose roles requires no capabilities is read
- **THEN** that role carries an empty required-capability collection, not a null or absent member

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

### Requirement: Service booking placement
The delivery API SHALL place service bookings over `POST /services/{id}/bookings`. The request body SHALL carry the start instant, the requested length in whole minutes, booker contact details, and an optional preferred resource id. The requested length SHALL be required for every service, including a fixed-duration one, and SHALL never be replaced by a permitted length. As with direct placement, the request model SHALL NOT expose a member key.

Service placement SHALL be a distinct endpoint with its own request model rather than optional service fields added to the direct placement request model. A single model carrying a resource id, a service id, and a preferred resource id would admit combinations with no meaning and force every consumer to re-derive which are valid.

**BREAKING (unpublished):** on success the response SHALL carry the resources the service resolved to as a **collection**, one per role, rather than a single resource id. A service may claim several resources for one booking, and a single-valued member could only report one of them or none. The collection SHALL be present and of length one for a single-role service. Each entry SHALL carry at least the resource id, so a booker is told everything they got.

A preferred resource id SHALL constrain only the role whose pool contains it, and SHALL be rejected with `resource-not-eligible` when it is in no role's pool.

The existing `POST /bookings` endpoint SHALL remain unchanged in route, request model, response model, and semantics: direct placement claims exactly one resource and continues to report it as it always has.

#### Scenario: Valid service placement succeeds
- **WHEN** a valid service placement is posted for a start and length taken from the service availability response
- **THEN** the response carries the new booking id, a `Confirmed` status, the resolved resources, and the booked interval

#### Scenario: The resolved resources are reported
- **WHEN** a service requiring a `room` and a `therapist` is placed
- **THEN** the response reports both resolved resources

#### Scenario: A single-role placement reports a collection of one
- **WHEN** a single-role service is placed
- **THEN** the response carries exactly one resolved resource, in the collection member

#### Scenario: Requested length is required
- **WHEN** a service placement omits the requested length
- **THEN** the response is 400 problem details identifying the offending field, and no booking is placed

#### Scenario: An unpermitted length is rejected, not substituted
- **WHEN** a service placement for a fixed 60-minute service requests 90 minutes
- **THEN** the response is 400 problem details carrying `duration-too-long`, and no booking exists at 60 minutes or any other length

#### Scenario: Preferred resource is optional
- **WHEN** a service placement omits the preferred resource id
- **THEN** placement proceeds over every role's full candidate pool in its deterministic order

#### Scenario: Ineligible preferred resource is rejected
- **WHEN** a service placement names a preferred resource id outside every role's candidate pool
- **THEN** the response is 400 problem details carrying `resource-not-eligible`, and no booking is placed

#### Scenario: Unknown service is rejected
- **WHEN** a service placement names a service id that does not exist
- **THEN** the response is 404 problem details carrying the `service-not-found` code

#### Scenario: Request model carries no member key
- **WHEN** the service placement request model's public shape is inspected
- **THEN** it exposes name, email, and optional phone, but no member key field

#### Scenario: Direct placement is unchanged
- **WHEN** `POST /bookings` is used to book a resource directly
- **THEN** its request and response are exactly as before, carrying a single resource id
