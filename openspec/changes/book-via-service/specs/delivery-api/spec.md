## ADDED Requirements

### Requirement: Service read model
The delivery API SHALL expose a public service read model over `GET /services` (paged) and `GET /services/{id}`. The read model SHALL carry what a consumer needs to present and drive a service booking: id, name, the duration specification (its kind, and whichever bounds apply), and the role's resource type key. It SHALL NOT expose management-only or internal structure. An unknown id SHALL yield a 404 problem-details response carrying `service-not-found`. The endpoints SHALL be anonymous, consistent with the delivery API's auth stance.

The duration specification SHALL be rendered so a consumer can distinguish a fixed length from a booker-chosen range without re-deriving the rule from nullable fields: the kind SHALL be explicit, and lengths SHALL be whole minutes.

#### Scenario: List returns a page and total
- **WHEN** `GET /services` is requested with paging parameters
- **THEN** the response carries the page of public service models and the unpaged total

#### Scenario: Get by id returns the public model
- **WHEN** `GET /services/{id}` is requested for an existing service
- **THEN** the response carries its id, name, duration specification, and role resource type key

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

The query SHALL NOT take a requested duration. The response SHALL NOT collapse a start's runs into a single minimum/maximum pair, because a candidate pool of differing granularities and minimums does not offer a contiguous band of lengths and a collapsed pair would advertise unbookable lengths. The response SHALL NOT identify which resource backs any start or run.

A range wider than the configured maximum SHALL yield `date-range-too-large`; a `from` after `to` SHALL yield `date-range-invalid`; an unknown service SHALL yield `service-not-found`. The endpoint SHALL be anonymous.

The existing per-resource `GET /resources/{id}/bookable-starts` endpoint SHALL remain unchanged in route, shape, and semantics.

#### Scenario: Service bookable-start read
- **WHEN** bookable starts are requested for a service over a valid date range
- **THEN** the response carries an ordered list of entries, each with an ISO-8601 UTC start instant and one or more length runs in whole minutes, plus the site zone id

#### Scenario: Heterogeneous pool yields multiple runs
- **WHEN** a start is backed by candidates of differing granularity
- **THEN** that entry carries one run per contributing candidate, rather than a single widened minimum/maximum pair

#### Scenario: Response names no resource
- **WHEN** a service bookable-start response body is inspected
- **THEN** no resource id appears anywhere in it

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

### Requirement: Service booking placement
The delivery API SHALL place service bookings over `POST /services/{id}/bookings`. The request body SHALL carry the start instant, the requested length in whole minutes, booker contact details, and an optional preferred resource id. The requested length SHALL be required for every service, including a fixed-duration one, and SHALL never be replaced by a permitted length. As with direct placement, the request model SHALL NOT expose a member key.

Service placement SHALL be a distinct endpoint with its own request model rather than optional service fields added to the direct placement request model. A single model carrying a resource id, a service id, and a preferred resource id would admit combinations with no meaning and force every consumer to re-derive which are valid.

On success the response SHALL use the same placement response shape as direct placement, whose resource id SHALL carry the resource the service resolved to — the booker is told which resource they got.

The existing `POST /bookings` endpoint SHALL remain unchanged in route, request model, and semantics.

#### Scenario: Valid service placement succeeds
- **WHEN** a valid service placement is posted for a start and length taken from the service availability response
- **THEN** the response carries the new booking id, a `Confirmed` status, the resolved resource id, and the booked interval

#### Scenario: The resolved resource is reported
- **WHEN** a service placement resolves to one of several candidates
- **THEN** the response's resource id is the resource actually booked

#### Scenario: Requested length is required
- **WHEN** a service placement omits the requested length
- **THEN** the response is 400 problem details identifying the offending field, and no booking is placed

#### Scenario: An unpermitted length is rejected, not substituted
- **WHEN** a service placement for a fixed 60-minute service requests 90 minutes
- **THEN** the response is 400 problem details carrying `duration-too-long`, and no booking exists at 60 minutes or any other length

#### Scenario: Preferred resource is optional
- **WHEN** a service placement omits the preferred resource id
- **THEN** placement proceeds over the full candidate pool in its deterministic order

#### Scenario: Ineligible preferred resource is rejected
- **WHEN** a service placement names a preferred resource id outside the service's candidate pool
- **THEN** the response is 400 problem details carrying `resource-not-eligible`, and no booking is placed

#### Scenario: Unknown service is rejected
- **WHEN** a service placement names a service id that does not exist
- **THEN** the response is 404 problem details carrying the `service-not-found` code

#### Scenario: Request model carries no member key
- **WHEN** the service placement request model's public shape is inspected
- **THEN** it exposes name, email, and optional phone, but no member key field

#### Scenario: Direct placement is untouched
- **WHEN** `POST /bookings` is posted with the direct placement model
- **THEN** its route, request model, and behaviour are exactly as before this change

## MODIFIED Requirements

### Requirement: Failure-to-problem-details mapping
The delivery API SHALL map domain failures to RFC 7807 problem-details responses with a stable HTTP status per code: `conflict` → 409; `resource-not-found`, `booking-not-found`, and `service-not-found` → 404; every other domain/validation code (including `interval-invalid`, `granularity`, `duration-too-short`, `duration-too-long`, `lead-time`, `horizon`, `outside-open-hours`, `date-range-invalid`, `date-range-too-large`, `name-required`, `email-invalid`, `time-zone-invalid`, `service-unavailable`, `resource-not-eligible`) → 400. Every failed rule SHALL be echoed as `{ code, message, field }` in an `errors` array, with codes verbatim from the domain. Codes are contract; the mapping SHALL NOT invent or rename them.

`service-unavailable` takes the default 400 deliberately: it reports that no candidate can fulfil the request as stated, which is the same category as `outside-open-hours`, and introducing a distinct status for it would add a second axis of meaning that the stable code already carries.

#### Scenario: Conflict maps to 409
- **WHEN** placement fails because the interval conflicts with a blocking claim
- **THEN** the response is 409 problem details whose `errors` array carries the `conflict` code

#### Scenario: Every failed rule is reported
- **WHEN** placement fails multiple pipeline rules at once
- **THEN** the `errors` array carries one entry per failed rule, each with its verbatim domain code

#### Scenario: Not-found maps to 404
- **WHEN** a query or placement targets a resource id that does not exist
- **THEN** the response is 404 problem details carrying the `resource-not-found` code

#### Scenario: Unknown service maps to 404
- **WHEN** a delivery query or placement targets a service id that does not exist
- **THEN** the response is 404 problem details carrying the `service-not-found` code, not 400

#### Scenario: Service-unavailable maps to 400
- **WHEN** a service placement fails because every candidate rejected it deterministically
- **THEN** the response is 400 problem details carrying the `service-unavailable` code
