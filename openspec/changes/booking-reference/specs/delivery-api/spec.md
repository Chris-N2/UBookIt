## ADDED Requirements

### Requirement: A placement response identifies the booking in a form a person can use
Every successful placement response — direct and service alike — SHALL carry the booking's
**quotable reference** alongside its identifier.

A consumer of this API builds its own confirmation screen, and that screen is read by the
person who just booked. Returning only an opaque identifier leaves that consumer with exactly
two options: show a value nobody can quote, or invent a reference of its own that the site
owner's backoffice will not recognise. The package already assigns one; withholding it makes
every headless consumer solve a problem that has been solved.

This is stated once, over both endpoints, because the guarantee is about placement rather than
about either route.

#### Scenario: A direct placement returns something quotable
- **WHEN** a booking is placed over the direct placement endpoint
- **THEN** the response carries the booking's reference as well as its identifier

#### Scenario: A service placement returns something quotable
- **WHEN** a booking is placed over the service placement endpoint
- **THEN** the response carries the booking's reference as well as its identifier

## MODIFIED Requirements

### Requirement: Booking placement
The delivery API SHALL place bookings over `POST /bookings`. The request body SHALL carry the resource id, the start instant, the duration, and booker contact details (name, email, optional phone). The request model SHALL NOT expose a member key — booker identity is contact details only in v1. Placement SHALL run the Core placement pipeline unchanged. On success the response SHALL carry the created booking's id, **its quotable reference**, its status, the resource id, the booked interval as ISO-8601 UTC, and the echoed booker contact details.

**The id is not the confirmation reference, and this requirement used to say it was.** They are
two identifiers with two readers: the id is opaque and is what routes and payloads carry; the
reference is what a person quotes. The sentence corrected here is the same false equation that
`default-frontend` carried, and it is what made a Guid appear on a confirmation under the
label "Reference".

#### Scenario: Valid placement succeeds
- **WHEN** a valid placement request is posted for a free, correctly sized, aligned interval
- **THEN** the response carries the new booking id, its reference, a `Confirmed` status, the resource id, and the booked interval

#### Scenario: Request model carries no member key
- **WHEN** the placement request model's public shape is inspected
- **THEN** it exposes name, email, and optional phone, but no member key field

#### Scenario: Malformed body is rejected as validation
- **WHEN** a placement request is posted with a missing or malformed required field (for example, no email)
- **THEN** the response is 400 problem details identifying the offending field

### Requirement: Service booking placement
The delivery API SHALL place service bookings over `POST /services/{id}/bookings`. The request body SHALL carry the start instant, the requested length in whole minutes, booker contact details, and an optional pinned resource id. The requested length SHALL be required for every service, including a fixed-duration one, and SHALL never be replaced by a permitted length. As with direct placement, the request model SHALL NOT expose a member key.

Service placement SHALL be a distinct endpoint with its own request model rather than optional service fields added to the direct placement request model. A single model carrying a resource id, a service id, and a pinned resource id would admit combinations with no meaning and force every consumer to re-derive which are valid.

**BREAKING (unpublished):** on success the response SHALL carry the resources the service resolved to as a **collection**, one per role, rather than a single resource id. A service may claim several resources for one booking, and a single-valued member could only report one of them or none. The collection SHALL be present and of length one for a single-role service. Each entry SHALL carry at least the resource id, so a booker is told everything they got.

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
