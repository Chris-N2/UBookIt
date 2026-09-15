# delivery-api Specification

## Purpose

Defines the public, anonymous, versioned delivery API an alternative booking UI consumes — exposed only when a site turns it on: both of its directions, reads and placement, are off by default, and a disabled direction is absent rather than refused. It covers resource read models, availability and slot reads, booking placement, and the mapping of domain failures to RFC 7807 problem details. The contract is data (endpoints + strongly-typed view models), never widgets, so any *alternative* front-end (a separate-repo DevExpress UI, a SPA, a mobile client) is a symmetric consumer. The shipped default Razor front-end is deliberately **not** among them: it renders from the Core ports in-process, which `default-frontend` requires of it in as many words.
## Requirements

### Requirement: The delivery API is off until a site turns it on

The delivery API SHALL be exposed in two independently switchable directions — **reads**
(resource and service discovery, availability, slots, bookable-starts, and the retention
read) and **placement** (anonymous booking placement, direct and via service) — and both
SHALL be **off by default**: a fresh install, and an upgrade that changes no
configuration, SHALL serve no delivery endpoint at all.

**This deliberately reverses the original always-on registration**, and the reversal is a
breaking change for existing consumers of the API, accepted and documented as such: the
package's rule is that nothing is exposed or sent until a site asks, an anonymous public
API is the package's largest unasked-for surface, and no request-time validation can
substitute for absence — an anonymous API has no way to know who is calling.

Each direction SHALL be enabled by its own explicit configuration setting, read at
startup. Neither direction SHALL imply the other, and enabling either SHALL NOT change
what the shipped Razor front end does — it renders in-process from the Core ports, as
the `default-frontend` capability requires, and functions identically with the API off.

Every delivery endpoint SHALL belong to exactly one direction, and the classification
SHALL be total: an endpoint that has not been classified SHALL be a test failure, never
an exposed default.

#### Scenario: An untouched install serves nothing

- **WHEN** no delivery API configuration is present and any delivery endpoint is
  requested
- **THEN** the response is the host's ordinary not-found response

#### Scenario: Reads can be enabled without placement

- **WHEN** a site enables the read direction only
- **THEN** availability and discovery requests are served, and a placement request
  receives the host's ordinary not-found response

#### Scenario: Placement can be enabled without reads

- **WHEN** a site enables the placement direction only
- **THEN** a valid placement request is served, and an availability request receives the
  host's ordinary not-found response

#### Scenario: Both directions enabled is the full API

- **WHEN** a site enables both directions
- **THEN** every delivery endpoint behaves exactly as this capability's other
  requirements describe

#### Scenario: Every endpoint is classified

- **WHEN** the delivery API's actions are enumerated
- **THEN** each carries exactly one direction, and an action carrying none or both is
  reported as a failure naming it

### Requirement: A disabled direction is absent, not refused

A request to an endpoint of a disabled direction SHALL receive the host's ordinary
not-found response — the same status, shape and headers as a route that never existed —
and SHALL NOT receive any status, header, body member or timing signal that
distinguishes "disabled" from "never existed". The operations of a disabled direction
SHALL NOT appear in the delivery API's OpenAPI document.

**Because absence is the guarantee, refusal is information.** A 403 or a
problem-details body saying "disabled" tells an unauthenticated stranger that the
package is installed and the endpoint exists to be turned on; a plain 404 says nothing.
The OpenAPI document follows for the same reason: it is the API's public inventory, and
an inventory listing endpoints a site has switched off would advertise the surface the
switch exists to remove.

#### Scenario: Disabled and non-existent are indistinguishable

- **WHEN** a disabled direction's endpoint is requested and a genuinely non-existent
  route under the same path prefix is requested
- **THEN** the two responses are indistinguishable in status and shape

#### Scenario: The OpenAPI document lists only what is on

- **WHEN** the delivery OpenAPI document is generated on a site with only the read
  direction enabled
- **THEN** it contains the read operations and no placement operation

#### Scenario: Everything off is an empty inventory

- **WHEN** the delivery OpenAPI document is generated on a site with no delivery
  configuration
- **THEN** it contains no operations

### Requirement: Anonymous access and auth stance
Delivery API endpoints, **where their direction is enabled**, SHALL be reachable anonymously — they SHALL NOT be protected by a backoffice authorization policy. They SHALL NOT require, issue, or validate a cookie-based anti-forgery token. Write endpoints SHALL NOT derive booker or member identity from an ambient authentication cookie; all booker identity SHALL come from the request body. This keeps every *alternative* UI (a separate-repo DevExpress UI, a SPA, a mobile client) a symmetric consumer of the same contract. Whether a direction is exposed at all is the exposure requirement's concern; this requirement governs how an exposed endpoint behaves, and **anonymity is a property of the enabled API, never a promise that the API is enabled**.

#### Scenario: Anonymous read succeeds
- **WHEN** an unauthenticated caller requests availability for a resource on a site that has enabled the read direction
- **THEN** the request is served (no authentication challenge)

#### Scenario: Anonymous placement succeeds
- **WHEN** an unauthenticated caller posts a valid booking with body-only contact details and no anti-forgery token, on a site that has enabled the placement direction
- **THEN** the booking is placed and the request is not rejected for missing authentication or a missing token

#### Scenario: Ambient identity is not trusted for writes
- **WHEN** a placement request arrives carrying an ambient authentication cookie, on a site that has enabled the placement direction
- **THEN** the placed booking's booker reflects only the request body, and no member key is inferred from the ambient context

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
The delivery API SHALL place bookings over `POST /bookings`. The request body SHALL carry the resource id, the start instant, the duration, and booker contact details (name, email, optional phone). The request model SHALL NOT expose a member key — booker identity is contact details only in v1. Placement SHALL run the Core placement pipeline unchanged. On success the response SHALL carry the created booking's id, **its quotable reference**, its status, the resource id, the booked interval as ISO-8601 UTC, and the echoed booker contact details.

**The id is not the confirmation reference, and this requirement used to say it was.** They are
two identifiers with two readers: the id is opaque and is what routes and payloads carry; the
reference is what a person quotes. The sentence corrected here is the same false equation that
`default-frontend` carried, and it is what made a Guid appear on a confirmation under the
label "Reference".

**The status member SHALL report the stored booking's status, and `Requested` is a value a
consumer can receive.** On a site whose `AutoConfirm` setting is off, a successful placement
yields a `Requested` booking, and the response says so. A consumer SHALL be able to
distinguish the two from the response alone; presenting a `Requested` placement to a customer
as confirmed is the consumer's misstatement, not this API's. The member's shape is unchanged —
this is a contract statement about its values, not its type.

#### Scenario: Valid placement succeeds
- **WHEN** a valid placement request is posted for a free, correctly sized, aligned interval on a site whose `AutoConfirm` setting is on
- **THEN** the response carries the new booking id, its reference, a `Confirmed` status, the resource id, and the booked interval

#### Scenario: Placement under approval reports the pending status
- **WHEN** a valid placement request is posted on a site whose `AutoConfirm` setting is off
- **THEN** the response carries a `Requested` status, and every other member exactly as a confirmed placement carries it

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

### Requirement: Service booking placement
The delivery API SHALL place service bookings over `POST /services/{id}/bookings`. The request body SHALL carry the start instant, the requested length in whole minutes, booker contact details, and an optional pinned resource id. The requested length SHALL be required for every service, including a fixed-duration one, and SHALL never be replaced by a permitted length. As with direct placement, the request model SHALL NOT expose a member key.

Service placement SHALL be a distinct endpoint with its own request model rather than optional service fields added to the direct placement request model. A single model carrying a resource id, a service id, and a pinned resource id would admit combinations with no meaning and force every consumer to re-derive which are valid.

**BREAKING, made before the first release reached a feed:** on success the response SHALL carry the resources the service resolved to as a **collection**, one per role, rather than a single resource id. A service may claim several resources for one booking, and a single-valued member could only report one of them or none. The collection SHALL be present and of length one for a single-role service. Each entry SHALL carry at least the resource id, so a booker is told everything they got.

A pinned resource id SHALL name the **booking**, not a role: a resource may be
eligible for several of a service's roles, so the pin requires only that it appear
somewhere in the resulting assignment, and the assignment chooses which slot it
fills. It SHALL be rejected with `resource-not-eligible` when it is in no role's
pool.

**Corrects a stale sentence.** This requirement previously said a preferred resource
id "SHALL constrain only the role whose pool contains it", which stopped being true
when a resource became able to belong to several pools at once, and which
`service-booking` has contradicted since. The rule stated there — that it names the
booking — has always been the one the code implements.

A pin SHALL be honoured or reported, never substituted. When no assignment including
it can be made **but one exists without it**, the endpoint SHALL fail with
`pinned-resource-unavailable`, mapping
to 400 through the existing rule that every code outside the conflict and not-found
families takes that status. The code SHALL be distinct from `conflict`, so a
consumer can tell "the person you chose is not free then" from "nothing could be
booked", and distinct from `resource-not-eligible`, which reports a resource that
could never fulfil this service at all.

When nothing could have been assigned with or without the pin, the endpoint SHALL
answer as it does for an unpinned request — `conflict` or `service-unavailable` —
rather than blaming the pin. Reporting the pin there would be true but misleading:
it invites a consumer to offer the resources that were free, and there were none.

The existing `POST /bookings` endpoint SHALL remain unchanged in route, request model, and semantics: direct placement claims exactly one resource and continues to report it as it always has. **Its response model gains the booking's quotable reference, and nothing else.** This sentence used to say the response model was unchanged as well, and that stopped being true when both placement responses gained the reference — but what it was guarding is untouched: service placement introduces nothing into the direct endpoint, which still reports a single resource id rather than a collection.

**The status member SHALL report the stored booking's status on the same terms as direct
placement**: on a site whose `AutoConfirm` setting is off, a successful service placement
reports `Requested`, with every other member as it would otherwise be.

#### Scenario: Valid service placement succeeds
- **WHEN** a valid service placement is posted for a start and length taken from the service availability response, on a site whose `AutoConfirm` setting is on
- **THEN** the response carries the new booking id, its reference, a `Confirmed` status, the resolved resources, and the booked interval

#### Scenario: Service placement under approval reports the pending status
- **WHEN** a valid service placement is posted on a site whose `AutoConfirm` setting is off
- **THEN** the response carries a `Requested` status, and every other member exactly as a confirmed placement carries it

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

#### Scenario: The pin is optional
- **WHEN** a service placement omits the pinned resource id
- **THEN** placement proceeds over every role's full candidate pool in its deterministic order

#### Scenario: Ineligible pinned resource is rejected
- **WHEN** a service placement names a pinned resource id outside every role's candidate pool
- **THEN** the response is 400 problem details carrying `resource-not-eligible`, and no booking is placed

#### Scenario: Unknown service is rejected
- **WHEN** a service placement names a service id that does not exist
- **THEN** the response is 404 problem details carrying the `service-not-found` code

#### Scenario: Request model carries no member key
- **WHEN** the service placement request model's public shape is inspected
- **THEN** it exposes name, email, and optional phone, but no member key field

#### Scenario: Direct placement still claims one resource
- **WHEN** `POST /bookings` is used to book a resource directly
- **THEN** its request is exactly as before and its response carries a single resource id rather than a collection — the reference it now also carries is the only addition

#### Scenario: A pin that cannot be honoured is reported, not substituted
- **WHEN** a service placement names a pinned resource id that is eligible but cannot be included in any assignment at that instant, while an assignment exists without it
- **THEN** the response is 400 problem details carrying `pinned-resource-unavailable`, and no booking is created

#### Scenario: Nothing bookable at all is not reported as the pin's failure
- **WHEN** a service placement names an eligible pinned resource at an instant where no assignment can be made with or without it
- **THEN** the response carries the ordinary all-fail code — `conflict` when a race could have been lost — and not `pinned-resource-unavailable`

#### Scenario: A pin failure is distinguishable from a conflict
- **WHEN** a consumer compares a refused pin with a placement that failed because nothing could be booked
- **THEN** the two carry different stable codes, so a front end can offer the resources that were free

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

### Requirement: A placement response identifies the booking in a form a person can use
Every successful placement response — direct and service alike — SHALL carry the booking's
**quotable reference** alongside its identifier.

A consumer of this API builds its own confirmation screen, and that screen is read by the
person who just booked. Returning only an opaque identifier leaves that consumer with exactly
two options: show a value nobody can quote, or invent a reference of its own that the site
owner's backoffice will not recognise. The package already assigns one; withholding it makes
every headless consumer solve a problem that has been solved.

**The reference SHALL cross the boundary in canonical form** — upper case, no separator — as
it does on the management endpoint. How it is grouped for reading is the consumer's decision,
and a consumer that stores it or searches by it needs the value exactly as the package holds
it. Saying so matters more here than anywhere else: this is the package's most public
contract, and its own Razor views render the grouped form, so a consumer comparing the two
would otherwise have to guess which is canonical.

This is stated once, over both endpoints, because the guarantee is about placement rather than
about either route.

#### Scenario: A direct placement returns something quotable
- **WHEN** a booking is placed over the direct placement endpoint
- **THEN** the response carries the booking's reference as well as its identifier

#### Scenario: A service placement returns something quotable
- **WHEN** a booking is placed over the service placement endpoint
- **THEN** the response carries the booking's reference as well as its identifier

#### Scenario: The reference crosses the boundary unformatted
- **WHEN** a placement response carries a reference
- **THEN** it is the canonical stored value, leaving the consumer to group it for display

### Requirement: Retention period read

The delivery API SHALL expose the site's configured **retention period** over a read under its
versioned public route. The response SHALL carry the period in whole days where one is configured,
and SHALL distinguish *no retention period is configured* from any numeric value — it SHALL NOT
report the absence of a period as `0` or as any other number.

**It publishes the number, and no prose.** A consumer building its own booking UI writes its own
privacy wording in its own language; the package's English sentences would be of no use to it and
would make untranslatable prose part of a published contract. What such a consumer **cannot**
obtain by any other means is how long uBookIt keeps the data it is about to collect — and without
that it cannot state the period accurately, which is the whole reason the notice was sequenced
after retention. Publishing the number closes that gap and nothing else.

**It is a site-wide fact and SHALL be read as one**, rather than carried on the resource or
service read models. Retention is not a property of a resource; repeating one site-wide value on
every row of a paged read would invite a consumer to believe it varies by resource, and would
duplicate a value with one source.

**Anonymous, on the same terms as every other delivery endpoint, and that is not a disclosure.**
A retention period is a policy a site publishes to its visitors deliberately — the shipped Razor
front end already prints it on a public page. It says nothing about any person and identifies no
booking.

#### Scenario: A configured period is published
- **WHEN** a retention period is configured and a consumer reads it from the delivery API
- **THEN** the response carries that period in whole days

#### Scenario: No configured period is distinguishable from a period
- **WHEN** no retention period is configured and a consumer reads it
- **THEN** the response reports that none is set, in a form that cannot be read as a numeric period

#### Scenario: The read carries no prose
- **WHEN** the response is inspected
- **THEN** it carries the period and no rendered notice, sentence or markup

#### Scenario: The read is anonymous
- **WHEN** an unauthenticated consumer reads the retention period
- **THEN** the request is served without an authentication challenge

#### Scenario: Retention does not appear on the resource or service reads
- **WHEN** the resource and service read models are inspected
- **THEN** neither carries a retention period
