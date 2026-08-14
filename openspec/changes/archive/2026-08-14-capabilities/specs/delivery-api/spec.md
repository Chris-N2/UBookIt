## MODIFIED Requirements

### Requirement: Resource read model
The delivery API SHALL expose a public resource read model over `GET /resources` (paged) and `GET /resources/{id}`. The read model SHALL carry only what a consumer needs to drive a booking UI: id, resource type key, display name, description, the resource's capability keys, booking constraints (granularity, minimum duration, maximum duration, lead time, horizon days), and the site time-zone id. It SHALL NOT expose management or internal configuration (raw weekly open-hours pattern, date exceptions). Capability keys SHALL be returned in a deterministic order, and a resource carrying none SHALL return an empty collection rather than a null or an omitted member. An unknown id SHALL yield a 404 problem-details response.

Capabilities are published deliberately: they are an input to eligibility, and publishing them keeps a service's candidate pool derivable from public reads. Capability keys are therefore visible to anonymous callers and SHALL NOT be used to record information that is not intended to be public.

#### Scenario: List returns a page and total
- **WHEN** `GET /resources` is requested with paging parameters
- **THEN** the response carries the page of public resource models and the unpaged total

#### Scenario: Get by id returns the public model
- **WHEN** `GET /resources/{id}` is requested for an existing resource
- **THEN** the response carries its id, type, display name, description, capabilities, constraints, and site zone id

#### Scenario: A resource with no capabilities returns an empty collection
- **WHEN** a resource carrying no capabilities is read
- **THEN** the response carries an empty capability collection, not a null or absent member

#### Scenario: Read model omits management configuration
- **WHEN** a resource read model is inspected
- **THEN** it exposes no weekly open-hours pattern and no date exceptions

#### Scenario: Unknown resource id
- **WHEN** `GET /resources/{id}` is requested for an id that does not exist
- **THEN** the response is 404 problem details carrying the `resource-not-found` code

### Requirement: Service read model
The delivery API SHALL expose a public service read model over `GET /services` (paged) and `GET /services/{id}`. The read model SHALL carry what a consumer needs to present and drive a service booking: id, name, the duration specification (its kind, and whichever bounds apply), the role's resource type key, and the role's required capability keys. It SHALL NOT expose management-only or internal structure. Required capability keys SHALL be returned in a deterministic order, and a role requiring none SHALL return an empty collection rather than a null or an omitted member. An unknown id SHALL yield a 404 problem-details response carrying `service-not-found`. The endpoints SHALL be anonymous, consistent with the delivery API's auth stance.

The duration specification SHALL be rendered so a consumer can distinguish a fixed length from a booker-chosen range without re-deriving the rule from nullable fields: the kind SHALL be explicit, and lengths SHALL be whole minutes.

Publishing the role's required capabilities alongside each resource's capabilities SHALL make a service's candidate pool computable by an anonymous consumer, which is what keeps the `resource-not-eligible` failure free of any disclosure the public reads do not already make.

#### Scenario: List returns a page and total
- **WHEN** `GET /services` is requested with paging parameters
- **THEN** the response carries the page of public service models and the unpaged total

#### Scenario: Get by id returns the public model
- **WHEN** `GET /services/{id}` is requested for an existing service
- **THEN** the response carries its id, name, duration specification, role resource type key, and role required capabilities

#### Scenario: A role requiring no capabilities returns an empty collection
- **WHEN** a service whose role requires no capabilities is read
- **THEN** the response carries an empty required-capability collection, not a null or absent member

#### Scenario: A consumer can compute the candidate pool
- **WHEN** a consumer reads a service and the resource list
- **THEN** the resources whose type matches and whose capabilities include every required capability are identifiable without any further request

#### Scenario: Fixed and variable durations are distinguishable
- **WHEN** a fixed-duration service and an unbounded variable-duration service are both read
- **THEN** each response states its duration kind explicitly, and the consumer need not infer it from which bounds are present

#### Scenario: Unknown service id
- **WHEN** `GET /services/{id}` is requested for an id that does not exist
- **THEN** the response is 404 problem details carrying the `service-not-found` code

#### Scenario: Anonymous access is permitted
- **WHEN** a service read is made with no credentials
- **THEN** the request succeeds, consistent with the rest of the delivery API
