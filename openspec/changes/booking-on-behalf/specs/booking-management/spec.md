## ADDED Requirements

### Requirement: An operator can place a booking on a booker's behalf
The package SHALL expose a versioned backoffice endpoint that places a booking on a booker's
behalf, in the same swagger group and under the same section authorization as every other
uBookIt management endpoint, gated by the same verb that gates cancelling, confirming,
declining and moving, **and additionally by sensitive-data access**, because it accepts a
booker's contact details as input (`sensitive-data`, *Withheld data SHALL NOT be reachable by
asking about it*).

**The sensitive-data gate SHALL be the endpoint's own authorization**, carried on the action
rather than evaluated inside a handler that would otherwise proceed.

**It SHALL apply the domain's operator placement rather than its own rule**, through the
service booking service, so that a booking placed for a service has the service's length rules
applied. Everything *Placing a booking on a booker's behalf* decides — which rules the request
runs and on whose terms, the status it produces, how the booker is validated — is decided
there; the endpoint adds no rule of its own and relaxes none.

**The request SHALL name exactly one of a service or a resource.** Naming both, or neither, SHALL
be refused as a malformed request rather than resolved by a precedence rule: a caller that
supplied both did not mean one of them, and choosing for them would place a booking nobody asked
for.

**A start carrying an offset or a `Z` SHALL be refused** with `interval-invalid` against the
start field, rather than reinterpreted, on exactly the terms the move endpoint states: the
contract is wall-clock time in the site's zone.

**The new start SHALL be expressed in the site's own time**, as the list's window and the
move's start are. The length SHALL be expressed as a duration.

The response SHALL carry the booking's identity, **its reference**, its status and the interval
it holds, so an operator can quote the reference to the person on the telephone without reading
the booking back. **It SHALL NOT carry the shape the list carries**, on the same grounds the
cancellation and move responses do not.

**The response SHALL NOT carry the booker's name, email address or telephone number.** The
endpoint accepts them; it does not report them. A write that echoed them back would be a read of
personal data wearing a write's authorization, and the guarantee SHALL hold structurally — the
response model carries no member for them — rather than by the caller happening to hold
sensitive-data access.

**A refused placement SHALL carry the domain's stable failure code** and a reason an operator
can act on, distinguishing at least: the service or resource was not found; the booker's details
were rejected; and each rule of the pipeline that refused the request. An operator holding
somebody on the telephone needs to know whether to change the time or correct an address.

#### Scenario: An operator places a booking for a resource
- **WHEN** an authorized operator posts a resource, a start and length that resource would accept, and a booker's name and email
- **THEN** a booking is placed and the response names it and reports its reference, status and interval

#### Scenario: An operator places a booking for a service
- **WHEN** an authorized operator posts a service, a start and a length its candidates admit, and a booker's name and email
- **THEN** a booking is placed claiming one resource per role, and the response reports its reference, status and interval

#### Scenario: The start is read in the site's zone
- **WHEN** an operator submits a start of 09:00 on a date while the site's zone is one hour ahead of UTC
- **THEN** the booking is placed at 08:00 UTC on that date

#### Scenario: A start with an offset is refused
- **WHEN** a caller submits a start of `2026-06-02T09:00:00Z`, or one carrying `+05:00`
- **THEN** the request fails with `interval-invalid` against the start field, and no booking is placed

#### Scenario: Naming both a service and a resource is refused
- **WHEN** a caller submits a request carrying both a service id and a resource id, or neither
- **THEN** the request is refused as malformed, and no booking is placed

#### Scenario: The response does not carry the booker back
- **WHEN** the placement response model is inspected
- **THEN** it carries no member for a booker's name, email address or telephone number

#### Scenario: The response does not imitate a list row
- **WHEN** the placement response model is inspected
- **THEN** it carries no resource collection, rather than one whose names this path cannot fill

#### Scenario: A refused placement names its reason
- **WHEN** an operator submits a start the domain refuses
- **THEN** the response carries the domain's stable code for the refusal, and no booking is placed

#### Scenario: A malformed booker address is distinguishable from a refused time
- **WHEN** an operator submits an email address that is not well formed, at a time that would have been accepted
- **THEN** the failure identifies the booker's details as the cause, distinctly from a pipeline refusal

#### Scenario: Read without Manage cannot place
- **WHEN** a user whose groups hold only the bookings read verb calls the endpoint
- **THEN** the request is refused and no booking is placed

#### Scenario: Manage without sensitive-data access cannot place
- **WHEN** a user holding the manage verb but lacking sensitive-data access calls the endpoint
- **THEN** the request is refused and no booking is placed

#### Scenario: The endpoint is not anonymous
- **WHEN** the endpoint is called without backoffice authentication
- **THEN** the response is 401 and no booking is placed

### Requirement: The bookings view can place a booking on a booker's behalf
The bookings view SHALL offer placing a booking on a booker's behalf from the view itself
rather than from a row, since the booking does not yet exist. The control SHALL be offered
only to a user whose verbs and sensitive-data access permit it, and **the screen's judgement
SHALL NOT be the rule** — the endpoint SHALL refuse independently, so a client that shows the
control cannot talk the server into a placement.

**The placement SHALL be taken through an accessible in-page modal provided by the backoffice**,
never a native browser dialog, offering what to book, the date, the start time, the length and
the booker's name, email address and optional telephone number. Its controls SHALL be labelled,
keyboard operable and programmatically associated with their labels and any error, on the same
terms as the rest of the section. Dismissing or cancelling the modal SHALL place nothing and
issue no request. A modal that fails to appear SHALL NOT be treated as a refusal.

**What to book SHALL be chosen explicitly**, as a service or as a resource, so that the operator
names one and the request carries one. The resources offered SHALL include those a site
withholds from direct booking by visitors, since *Booking a single resource requires that
resource to permit it* does not bind an operator — and the view SHALL NOT present that as an
error condition.

**A refused placement SHALL be shown to the operator, inside the modal, in words derived from
the domain's stable code**, so that an operator whose chosen time is outside open hours, taken
or in the past is told which, and can change it without re-entering the booker's details. No row
SHALL appear to change.

**The modal SHALL tell the operator the truthful conditional about notification**, on the same
terms as the cancellation, decline and move wording: the package writes to the person who booked
only where the site has configured booking emails, so an operator on an unconfigured site knows
to read the reference out rather than leave the customer expecting an email.

**A successful placement SHALL report the booking's reference to the operator**, which is what
they will quote to the person who asked for it.

**Where the new booking falls outside the window the list is showing, the view SHALL say so
rather than appear to have done nothing.** An operator taking a booking for next month on a
screen showing this week would otherwise see an unchanged table and reasonably conclude the
placement failed. Where it falls inside the window, the list SHALL show it without the operator
reloading the page, and focus SHALL be placed deliberately rather than lost to the document.

**There is no availability picker in this view, and the documentation SHALL say so**, on exactly
the terms the move requirement states: an operator chooses a time and is told whether it can be
taken. The view SHALL NOT imply one by presenting a time as available before the domain has said
so.

#### Scenario: An operator places a booking from the view
- **WHEN** an operator opens the modal, chooses a resource, enters a time and the booker's details, and submits
- **THEN** the booking is placed and the operator is shown its reference

#### Scenario: A booking inside the window appears without a reload
- **WHEN** an operator places a booking whose interval falls inside the window the list is showing
- **THEN** the row appears in the list without the page being reloaded

#### Scenario: A booking outside the window is not silently invisible
- **WHEN** an operator places a booking whose interval falls outside the window the list is showing
- **THEN** the view states that the booking was placed and lies outside the dates being shown, rather than leaving the table unchanged with no explanation

#### Scenario: Dismissing the modal places nothing
- **WHEN** an operator opens the modal and then dismisses or cancels it
- **THEN** no request is issued and no booking is placed

#### Scenario: A refused placement keeps what was typed
- **WHEN** the endpoint refuses a placement because the chosen interval is outside open hours
- **THEN** the modal stays open, states that the time is outside the resource's open hours, and the booker's details the operator entered are still there

#### Scenario: A resource withheld from visitors is offered to the operator
- **WHEN** an operator opens the modal on a site where a resource withholds direct booking from visitors
- **THEN** that resource is offered as something the operator may book, and choosing it produces no error on that ground

#### Scenario: The modal states the truthful conditional
- **WHEN** an operator is entering a booking on somebody's behalf
- **THEN** the modal states that the package writes to the person who booked only where booking emails are configured

#### Scenario: A user who may not place is not offered the control
- **WHEN** a user holding only the bookings read verb, or lacking sensitive-data access, opens the bookings view
- **THEN** no control offering to place a booking is shown, and the endpoint refuses the request if one is issued anyway

#### Scenario: The modal is operable by keyboard
- **WHEN** a keyboard operator opens the placement modal
- **THEN** focus lands inside it, every control is reachable and labelled, an error is announced in association with the control it concerns, and closing it returns focus to the control that opened it
