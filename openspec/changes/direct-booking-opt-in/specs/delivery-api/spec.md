## ADDED Requirements

### Requirement: Direct placement is refused for a resource not offered on its own
`POST /bookings` SHALL fail with the stable code `resource-not-directly-bookable`
when the named resource withholds permission to be booked on its own, mapping to
400 through the existing rule that every domain code other than the conflict and
not-found families takes that status.

The code SHALL be distinct from every unavailability code. A caller SHALL be able
to tell "this resource is not offered by itself" from "this resource has no free
time then" without inspecting a message, because the two have different remedies:
the first is answered by booking a service, the second by choosing another time.

The availability endpoints SHALL be unaffected. `GET /resources/{id}/free-time`,
`/slots` and `/bookable-starts` SHALL answer for a resource that withholds the
permission exactly as for one that grants it. Those reads describe when a resource
is free, which a composite booking flow needs in order to offer it, and they were
never an offer to book it directly.

A conforming client SHALL be able to avoid the refusal entirely, because the
permission is published on the resource read model. The refusal therefore behaves
as a drift signal in the same way `service-unavailable` does: a client that reads
before it writes cannot legitimately provoke it.

#### Scenario: Direct placement on a withholding resource is refused
- **WHEN** `POST /bookings` names a resource that withholds direct booking, for a time it is free and open
- **THEN** the response is 400 problem details carrying `resource-not-directly-bookable`, and no booking is created

#### Scenario: The refusal is distinguishable from unavailability
- **WHEN** a client compares the refusal with a placement that failed because the resource was outside its open hours
- **THEN** the two carry different stable codes

#### Scenario: Availability reads answer for a withholding resource
- **WHEN** `GET /resources/{id}/bookable-starts` is requested for a resource that withholds direct booking
- **THEN** the response carries its bookable starts exactly as it would for any other resource

#### Scenario: Service placement on a withholding resource succeeds
- **WHEN** `POST /services/{id}/bookings` places a service whose role resolves to a resource that withholds direct booking
- **THEN** the booking is created

## MODIFIED Requirements

### Requirement: Resource read model
The delivery API SHALL expose a public resource read model over `GET /resources` (paged) and `GET /resources/{id}`. The read model SHALL carry only what a consumer needs to drive a booking UI: id, resource type key, display name, description, the resource's capability keys, whether it may be booked on its own, booking constraints (granularity, minimum duration, maximum duration, lead time, horizon days), and the site time-zone id. It SHALL NOT expose management or internal configuration (raw weekly open-hours pattern, date exceptions). Capability keys SHALL be returned in a deterministic order, and a resource carrying none SHALL return an empty collection rather than a null or an omitted member. An unknown id SHALL yield a 404 problem-details response.

Capabilities are published deliberately: they are an input to eligibility, and publishing them keeps a service's candidate pool derivable from public reads. Capability keys are therefore visible to anonymous callers and SHALL NOT be used to record information that is not intended to be public.

Whether a resource may be booked on its own is published for the same reason and
SHALL be carried on every read, as a value rather than by omission. It is the one
fact that decides whether `POST /bookings` will accept the resource at all, so a
consumer that cannot read it can only discover it by being refused — which is both
a poor contract and the same disclosure-by-probing the capability publication
exists to avoid. It says nothing about availability and SHALL NOT be read as an
assertion that the resource is free.

#### Scenario: List returns a page and total
- **WHEN** `GET /resources` is requested with paging parameters
- **THEN** the response carries the page of public resource models and the unpaged total

#### Scenario: Get by id returns the public model
- **WHEN** `GET /resources/{id}` is requested for an existing resource
- **THEN** the response carries its id, type, display name, description, capabilities, whether it may be booked on its own, constraints, and site zone id

#### Scenario: A resource with no capabilities returns an empty collection
- **WHEN** a resource carrying no capabilities is read
- **THEN** the response carries an empty capability collection, not a null or absent member

#### Scenario: Read model omits management configuration
- **WHEN** a resource read model is inspected
- **THEN** it exposes no weekly open-hours pattern and no date exceptions

#### Scenario: Unknown resource id
- **WHEN** `GET /resources/{id}` is requested for an id that does not exist
- **THEN** the response is 404 problem details carrying the `resource-not-found` code

#### Scenario: Direct bookability is readable before it is needed
- **WHEN** a resource that withholds direct booking is read
- **THEN** the response states that it may not be booked on its own, so a client can omit it from a direct-booking UI without attempting a placement
