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

### Requirement: Bookable-start read
The delivery API SHALL expose a bookable-start query for a resource over an inclusive `[from, to]` date range, delegating to the Core availability query service. Each returned entry SHALL carry the start instant as ISO-8601 UTC together with the shortest and longest bookable length from that start, expressed as whole minutes. The response SHALL carry the site time-zone id once at the top level, consistent with the existing free-time and slot responses.

The query SHALL NOT require a requested duration — answering "how long can I book from here" is its purpose. The existing slot query SHALL remain unchanged and SHALL continue to require a duration; the two SHALL be separate endpoints rather than one endpoint whose response shape varies with the presence of a parameter.

A range wider than the configured maximum SHALL yield the `date-range-too-large` failure; a `from` after `to` SHALL yield `date-range-invalid`; an unknown resource SHALL yield `resource-not-found`. Failures SHALL be rendered as problem details on the same terms as every other delivery endpoint. The endpoint SHALL be anonymous, consistent with the delivery API's auth stance.

#### Scenario: Bookable-start read
- **WHEN** bookable starts are requested for a resource over a valid date range
- **THEN** the response carries an ordered list of entries, each with an ISO-8601 UTC start instant and its minimum and maximum bookable lengths in whole minutes, plus the site zone id

#### Scenario: Any length is answerable from one response
- **WHEN** a client filters a bookable-start response to entries whose minimum is at most 90 minutes and whose maximum is at least 90 minutes
- **THEN** the resulting start instants are exactly those the slot endpoint returns for a 90-minute duration over the same resource and range

#### Scenario: Over-wide range is rejected
- **WHEN** a bookable-start query requests a range wider than the configured maximum
- **THEN** the response is 400 problem details carrying the `date-range-too-large` code

#### Scenario: Inverted range is rejected
- **WHEN** a bookable-start query is requested with `from` after `to`
- **THEN** the response is 400 problem details carrying the `date-range-invalid` code

#### Scenario: Unknown resource is rejected
- **WHEN** a bookable-start query names a resource id that does not exist
- **THEN** the response is 404 problem details carrying the `resource-not-found` code

#### Scenario: Anonymous access is permitted
- **WHEN** a bookable-start query is made with no credentials
- **THEN** the request succeeds, consistent with the rest of the delivery API

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

### Requirement: Service bookable-start read
The delivery API SHALL expose a bookable-start query for a service over an inclusive `[from, to]` date range at `GET /services/{id}/bookable-starts`, delegating to the Core service availability query. Each entry SHALL carry the start instant as ISO-8601 UTC together with the lengths bookable at that start, expressed as a list of arithmetic runs, each `{ minDurationMinutes, maxDurationMinutes, stepMinutes }` in whole minutes. The response SHALL carry the site time-zone id once at the top level, consistent with the other availability responses.

The query SHALL NOT take a requested duration. The response SHALL NOT collapse a start's runs into a single minimum/maximum pair, because a candidate pool of differing granularities and minimums does not offer a contiguous band of lengths and a collapsed pair would advertise unbookable lengths. The response SHALL NOT identify which resource backs any start or run.

Within a role, a run arises from one contributing candidate. Across roles a run is **rebuilt from the lengths a saturating assignment of distinct resources can provide**, so a run in a multi-role service's response is not attributable to any single candidate — and after subset elimination a candidate whose lengths another run already offers contributes no run at all. The response therefore SHALL NOT be read as one run per candidate.

A range wider than the configured maximum SHALL yield `date-range-too-large`; a `from` after `to` SHALL yield `date-range-invalid`; an unknown service SHALL yield `service-not-found`. The endpoint SHALL be anonymous.

The existing per-resource `GET /resources/{id}/bookable-starts` endpoint SHALL remain unchanged in route, shape, and semantics.

#### Scenario: Service bookable-start read
- **WHEN** bookable starts are requested for a service over a valid date range
- **THEN** the response carries an ordered list of entries, each with an ISO-8601 UTC start instant and one or more length runs in whole minutes, plus the site zone id

#### Scenario: Heterogeneous pool yields multiple runs
- **WHEN** a start of a single-role service is backed by candidates of differing granularity, none of whose lengths another already offers
- **THEN** that entry carries one run per contributing candidate, rather than a single widened minimum/maximum pair

#### Scenario: Composite runs are not per candidate
- **WHEN** a multi-role service's bookable starts are read
- **THEN** each run denotes the lengths every role can provide, and no run corresponds to a single candidate's own grid

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

### Requirement: Boundary inputs fail as validation, never as an exception
No delivery endpoint SHALL answer with an unhandled exception for any date, instant, or duration a caller can express in the request's own types. An input at or near the limit of what a date or instant can represent SHALL be rejected by the domain as a structured failure and rendered as problem details on the same terms as any other validation failure — `date-range-invalid` for a query range that cannot be walked, `interval-invalid` for a placement interval or open-hours window that cannot be represented. Both are already 400 under the failure mapping, so no new code and no new status is introduced.

This SHALL hold for every endpoint that accepts a date, an instant, or a duration, on both the direct-resource and via-service paths, and SHALL be verified against the running site rather than only in unit tests: the failure mode being corrected is an unhandled exception, which is exactly the class that in-process tests can miss and a live request cannot.

#### Scenario: Availability queries at the calendar's end
- **WHEN** free-time, slots, bookable-starts, or service bookable-starts are requested with `from` and `to` both set to the last representable date
- **THEN** each responds 400 problem details carrying `date-range-invalid`, and none responds 500

#### Scenario: Placement at the representable limits
- **WHEN** a booking is placed, on either the direct or the service route, with a start at the last representable date, a start at the first representable date, or a duration large and negative enough to underflow
- **THEN** each responds 400 problem details carrying `interval-invalid`, and none responds 500

#### Scenario: Ordinary requests are unchanged
- **WHEN** any availability query or placement is made with everyday dates and durations
- **THEN** its status, body, and failure codes are exactly as before this change
