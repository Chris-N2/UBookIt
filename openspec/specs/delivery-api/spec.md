# delivery-api Specification

## Purpose

Defines the public, anonymous, versioned delivery API that every booking UI consumes: resource read models, availability and slot reads, booking placement, and the mapping of domain failures to RFC 7807 problem details. The contract is data (endpoints + strongly-typed view models), never widgets, so any front-end (default Razor, a separate-repo DevExpress UI, a SPA, a mobile client) is a symmetric consumer.

## Requirements

### Requirement: Anonymous access and auth stance
Delivery API endpoints SHALL be reachable anonymously — they SHALL NOT be protected by a backoffice authorization policy. They SHALL NOT require, issue, or validate a cookie-based anti-forgery token. Write endpoints SHALL NOT derive booker or member identity from an ambient authentication cookie; all booker identity SHALL come from the request body. This keeps every UI (the default front-end, a separate-repo DevExpress UI, a SPA, a mobile client) a symmetric consumer of the same contract.

#### Scenario: Anonymous read succeeds
- **WHEN** an unauthenticated caller requests availability for a resource
- **THEN** the request is served (no authentication challenge)

#### Scenario: Anonymous placement succeeds
- **WHEN** an unauthenticated caller posts a valid booking with body-only contact details and no anti-forgery token
- **THEN** the booking is placed and the request is not rejected for missing authentication or a missing token

#### Scenario: Ambient identity is not trusted for writes
- **WHEN** a placement request arrives carrying an ambient authentication cookie
- **THEN** the placed booking's booker reflects only the request body, and no member key is inferred from the ambient context

### Requirement: Resource read model
The delivery API SHALL expose a public resource read model over `GET /resources` (paged) and `GET /resources/{id}`. The read model SHALL carry only what a consumer needs to drive a booking UI: id, resource type key, display name, description, booking constraints (granularity, minimum duration, maximum duration, lead time, horizon days), and the site time-zone id. It SHALL NOT expose management or internal configuration (raw weekly open-hours pattern, date exceptions). An unknown id SHALL yield a 404 problem-details response.

#### Scenario: List returns a page and total
- **WHEN** `GET /resources` is requested with paging parameters
- **THEN** the response carries the page of public resource models and the unpaged total

#### Scenario: Get by id returns the public model
- **WHEN** `GET /resources/{id}` is requested for an existing resource
- **THEN** the response carries its id, type, display name, description, constraints, and site zone id

#### Scenario: Read model omits management configuration
- **WHEN** a resource read model is inspected
- **THEN** it exposes no weekly open-hours pattern and no date exceptions

#### Scenario: Unknown resource id
- **WHEN** `GET /resources/{id}` is requested for an id that does not exist
- **THEN** the response is 404 problem details carrying the `resource-not-found` code

### Requirement: Availability and slot read
The delivery API SHALL expose free-time and slot queries for a resource over an inclusive `[from, to]` date range, delegating to the Core availability query service. Instants SHALL be serialized as ISO-8601 UTC and each response SHALL carry the site time-zone id once at the top level. Slot duration SHALL be expressed as whole minutes. The slot query SHALL require a requested duration. A range wider than the configured maximum SHALL yield the `date-range-too-large` failure; a `from` after `to` SHALL yield `date-range-invalid`; an unknown resource SHALL yield `resource-not-found`.

#### Scenario: Free-time read
- **WHEN** free-time is requested for a resource over a valid date range
- **THEN** the response carries an ordered list of disjoint `[startUtc, endUtc)` intervals as ISO-8601 UTC and the site zone id

#### Scenario: Slot read
- **WHEN** slots are requested for a resource, date range, and duration
- **THEN** the response carries the projected start instants (ISO-8601 UTC), the duration in whole minutes, and the site zone id

#### Scenario: Over-wide range is rejected
- **WHEN** a free-time or slot query requests a range wider than the configured maximum
- **THEN** the response is 400 problem details carrying the `date-range-too-large` code

#### Scenario: Inverted range is rejected
- **WHEN** a query is requested with `from` after `to`
- **THEN** the response is 400 problem details carrying the `date-range-invalid` code

### Requirement: Booking placement
The delivery API SHALL place bookings over `POST /bookings`. The request body SHALL carry the resource id, the start instant, the duration, and booker contact details (name, email, optional phone). The request model SHALL NOT expose a member key — booker identity is contact details only in v1. Placement SHALL run the Core placement pipeline unchanged. On success the response SHALL carry the created booking's id (the confirmation reference), its status, the resource id, the booked interval as ISO-8601 UTC, and the echoed booker contact details.

#### Scenario: Valid placement succeeds
- **WHEN** a valid placement request is posted for a free, correctly sized, aligned interval
- **THEN** the response carries the new booking id, a `Confirmed` status, the resource id, and the booked interval

#### Scenario: Request model carries no member key
- **WHEN** the placement request model's public shape is inspected
- **THEN** it exposes name, email, and optional phone, but no member key field

#### Scenario: Malformed body is rejected as validation
- **WHEN** a placement request is posted with a missing or malformed required field (for example, no email)
- **THEN** the response is 400 problem details identifying the offending field

### Requirement: Failure-to-problem-details mapping
The delivery API SHALL map domain failures to RFC 7807 problem-details responses with a stable HTTP status per code: `conflict` → 409; `resource-not-found` and `booking-not-found` → 404; every other domain/validation code (including `interval-invalid`, `granularity`, `duration-too-short`, `duration-too-long`, `lead-time`, `horizon`, `outside-open-hours`, `date-range-invalid`, `date-range-too-large`, `name-required`, `email-invalid`, `time-zone-invalid`) → 400. Every failed rule SHALL be echoed as `{ code, message, field }` in an `errors` array, with codes verbatim from the domain. Codes are contract; the mapping SHALL NOT invent or rename them.

#### Scenario: Conflict maps to 409
- **WHEN** placement fails because the interval conflicts with a blocking claim
- **THEN** the response is 409 problem details whose `errors` array carries the `conflict` code

#### Scenario: Every failed rule is reported
- **WHEN** placement fails multiple pipeline rules at once
- **THEN** the `errors` array carries one entry per failed rule, each with its verbatim domain code

#### Scenario: Not-found maps to 404
- **WHEN** a query or placement targets a resource id that does not exist
- **THEN** the response is 404 problem details carrying the `resource-not-found` code

### Requirement: Versioned, self-contained public contract
Delivery endpoints SHALL be served under a versioned public route distinct from the backoffice route, and SHALL be described by an OpenAPI document separate from the backoffice API document. Request and response payloads SHALL be strongly-typed view models defined by the delivery API — never Core domain aggregates serialized directly — so the published contract is stable independently of internal model changes, and no widget-level abstraction is introduced.

#### Scenario: Public versioned route
- **WHEN** a delivery endpoint is addressed
- **THEN** its path is a public, versioned route, not the backoffice API route

#### Scenario: Separate API document
- **WHEN** the generated OpenAPI documents are inspected
- **THEN** the delivery operations appear in a document distinct from the backoffice API document

#### Scenario: View models, not aggregates
- **WHEN** a delivery response body is inspected
- **THEN** it is a delivery-defined view model, exposing no internal aggregate structure such as the booking's resource-claims collection

### Requirement: Delivery problem-details responses carry a type member
Problem-details responses from the delivery API SHALL populate the RFC 7807 `type` member alongside `title`, `status`, and the `errors` extension, for both domain failures and model-binding failures. This keeps the delivery envelope identical in shape to the management one, which requires the member because the backoffice client discards bodies without it. The addition is additive — the member was previously absent — and changes no status code, no failure code, and no existing member.

#### Scenario: A delivery failure response is typed
- **WHEN** the delivery API rejects a request with a validation, not-found, or conflict failure
- **THEN** the response carries a non-empty `type` member, and its status, `title`, and `errors` entries are exactly as before

#### Scenario: Transport failures are typed too
- **WHEN** a request fails model binding and is projected into the shared problem envelope
- **THEN** that response also carries a non-empty `type` member alongside its `invalid-request` error entries
