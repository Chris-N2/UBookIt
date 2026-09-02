## MODIFIED Requirements

### Requirement: Post-Redirect-Get confirmation
A successful placement SHALL respond with a redirect (HTTP 303) to a confirmation page, not by
rendering the POST result directly. The confirmation SHALL show the booking's **reference** —
the identifier a person can quote, not its machine identifier — and the booked resource, time,
and booker contact details. Reloading or refreshing the confirmation page SHALL NOT create
another booking.

**The value shown SHALL be usable by the person reading it.** A confirmation that labels a
field "Reference" and prints something nobody can read aloud, write down or type back has
labelled it accurately and filled it uselessly. This requirement previously said the reference
*was* the booking's id; that is what changed.

#### Scenario: Success redirects to confirmation
- **WHEN** a booking is successfully placed
- **THEN** the response is a 303 redirect to a confirmation page showing the booking's quotable reference and details

#### Scenario: Refreshing the confirmation does not re-submit
- **WHEN** a visitor refreshes the confirmation page after a successful booking
- **THEN** no further booking is created

#### Scenario: The confirmation shows a reference a person can use
- **WHEN** a visitor reads the reference on their confirmation
- **THEN** it is the booking's quotable reference, which they could dictate over a telephone or type into a search, rather than the identifier machines use

### Requirement: The confirmation reports every resource a service resolved to
The confirmation for a placed service booking SHALL report the booking's **quotable
reference — the identifier a person can quote, not its machine identifier** — the
booked interval, the booker's contact details, and **every** resource the service
resolved to, not one of them and not a count.

The disambiguation matters here for the same reason it does on the direct confirmation:
both views printed a `Guid` beneath a label reading "Reference", and a service booking
is no less likely to be the one somebody telephones about.

A visitor who booked a room and a therapist was given both, and a confirmation naming
one of them describes a different booking from the one that exists. For a single-role
service the report SHALL still be the resolved set, which has one member — the flow
does not special-case it.

#### Scenario: A multi-role confirmation names every resource
- **WHEN** a service requiring a room and a therapist is booked
- **THEN** the confirmation names both resolved resources

#### Scenario: A single-role confirmation names its one resource
- **WHEN** a single-role service is booked
- **THEN** the confirmation names that one resolved resource

#### Scenario: Refreshing the confirmation does not re-submit
- **WHEN** a visitor refreshes the confirmation page after a successful service booking
- **THEN** no additional booking is created
