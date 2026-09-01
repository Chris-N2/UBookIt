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
