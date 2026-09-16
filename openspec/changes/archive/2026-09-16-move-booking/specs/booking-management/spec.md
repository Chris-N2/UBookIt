<!--
No wholesale replacement in this file: both requirements are ADDED. The capability's Purpose
paragraph states that amending a booking's time "is still not here"; that sentence is corrected
in openspec/specs/booking-management/spec.md directly at sync time (Purpose is not a delta
operation), on the precedent approval-decline and booker-erasure set — see tasks.
-->

## ADDED Requirements

### Requirement: An operator can move a booking
The package SHALL expose a versioned backoffice endpoint that moves a booking to a new start
and length, in the same swagger group and under the same section authorization as every other
uBookIt management endpoint, and gated by the same verb that gates cancelling, confirming and
declining.

**It SHALL apply the domain's move operation rather than its own rule**, through the service
booking service's move, so that a booking placed for a service has the service's length rule
applied (`service-booking`, *Moving a booking placed for a service applies the service's length
rules*). Everything *Moving a booking* decides — which statuses permit a move, which rules the
new interval runs and on whose terms, the refusal of an unchanged interval — is decided there;
the endpoint adds no rule of its own and relaxes none.

**A start carrying an offset or a `Z` SHALL be refused** with `interval-invalid` against the
start field, rather than reinterpreted. The contract is wall-clock time in the site's zone; a
scheduler sending an instant would otherwise be given a booking at a time nobody typed, with no
error.

**The new start SHALL be expressed in the site's own time**, on the same terms as the list's
window: the operator is looking at a screen in the site's zone, and a start typed there means
that wall-clock time in that zone. The length SHALL be expressed as a duration.

The response SHALL carry the booking's identity, its status, and the interval it now holds, so
a caller knows what happened without reading it back. **It SHALL NOT carry the shape the list
carries**, on the same grounds the cancellation response does not: this path goes through the
domain, which knows resource ids and nothing more, and a response shaped like a list row with
blank names would be quietly less true than the thing it resembles.

**A refused move SHALL carry the domain's stable failure code** and a reason an operator can
act on, distinguishing at least: the booking was not found; its status does not permit a move;
the interval is the one it already holds; and each rule of the pipeline that refused the new
interval. They call for different actions from the operator, and a scheduler dragging a
booking needs the code to say why a drop was refused.

#### Scenario: A confirmed booking is moved
- **WHEN** an authorized operator moves a confirmed booking to a new start and length that every claimed resource would accept
- **THEN** it holds the new interval, keeps its reference and status, and the response names it and reports its status and new interval

#### Scenario: The start is read in the site's zone
- **WHEN** an operator submits a start of 09:00 on a date while the site's zone is one hour ahead of UTC
- **THEN** the booking is moved to 08:00 UTC on that date

#### Scenario: A start with an offset is refused
- **WHEN** a caller submits a start of `2026-06-02T09:00:00Z`, or one carrying `+05:00`
- **THEN** the request fails with `interval-invalid` against the start field, and no booking changes

#### Scenario: A service booking's length is bound by its service
- **WHEN** an operator moves a booking placed for a 45–120 minute service to a length of 30 minutes
- **THEN** the response carries `duration-too-short`, and the booking is unchanged

#### Scenario: A refused move names its reason
- **WHEN** an operator moves a booking onto an interval the domain refuses
- **THEN** the response carries the domain's stable code for the refusal, and the booking is unchanged

#### Scenario: An unknown booking is distinguishable from an immovable one
- **WHEN** a move is requested for an id no booking has
- **THEN** the failure says the booking was not found, distinctly from the failure a booking in the wrong status produces

#### Scenario: The response does not imitate a list row
- **WHEN** a move's response model is inspected
- **THEN** it carries no resource collection, rather than one whose names this path cannot fill

#### Scenario: Read without Manage cannot move
- **WHEN** a user whose groups hold only the bookings read verb calls the endpoint
- **THEN** the request is refused and no booking changes

#### Scenario: The endpoint is not anonymous
- **WHEN** the endpoint is called without backoffice authentication
- **THEN** the response is 401 and no booking changes

### Requirement: The bookings view can move a booking
The bookings view SHALL offer a move for a `Requested` or `Confirmed` booking, and SHALL NOT
offer it for a booking in any other status — a control that is always refused teaches an
operator to ignore failures.

**The screen's judgement SHALL NOT be the rule.** The endpoint SHALL refuse a move the domain
does not permit independently, so a screen showing a stale list cannot talk the domain into
one.

**The move SHALL be taken through an accessible in-page modal provided by the backoffice**,
never a native browser dialog, offering the new date, the new start time and the new length,
pre-filled with what the booking holds now. Its controls SHALL be labelled, keyboard operable
and programmatically associated with their labels and any error, on the same terms as the
rest of the section. Dismissing or cancelling the modal SHALL leave the booking untouched and
issue no request. A modal that fails to appear SHALL NOT be treated as a refusal.

**A refused move SHALL be shown to the operator, inside the modal, in words derived from the
domain's stable code**, so that an operator whose chosen time is outside open hours, taken, in
the past, or unchanged is told which, and can change the time without starting over. The row
SHALL NOT appear to change.

**The modal SHALL tell the operator the truthful conditional about notification**, on the
same terms as the cancellation and decline wording: the package writes to the person who
booked only where the site has configured booking emails, so an operator on an unconfigured
site knows the customer will not otherwise find out the time has changed.

After a successful move the list SHALL show the booking's new interval without the operator
reloading the page. Where the moved booking leaves the current window, the row SHALL leave
the list, and the paging and focus behaviour SHALL be the cancellation requirement's: step
back rather than reset when the last row of a page goes, and place focus deliberately rather
than losing it to the document.

**There is no availability picker in this view, and the documentation SHALL say so.** An
operator chooses a time and is told whether it can be taken; where a time cannot, the reason
is shown and the operator chooses again. A read showing where a booking could go is not built
here, and the view SHALL NOT imply one by, for instance, presenting a time as available before
the domain has said so.

#### Scenario: An operator moves from the list
- **WHEN** an operator activates move on a confirmed booking, changes the start in the modal, and submits
- **THEN** the booking is moved and the list shows its new interval without a page reload

#### Scenario: The modal opens on what the booking holds
- **WHEN** an operator activates move on a booking
- **THEN** the modal's date, start time and length are pre-filled with the booking's current interval in the site's zone

#### Scenario: Dismissing the modal changes nothing
- **WHEN** an operator activates move and then dismisses or cancels the modal
- **THEN** no request is issued and the booking is unchanged

#### Scenario: Only movable bookings offer the control
- **WHEN** the list shows a declined or cancelled booking
- **THEN** no move control is offered for it

#### Scenario: A refused move is explained in place
- **WHEN** the endpoint refuses a move because the chosen interval is outside open hours
- **THEN** the modal stays open, states that the time is outside the resource's open hours, and the row is unchanged

#### Scenario: An unchanged interval is refused in place
- **WHEN** an operator submits the modal without changing the date, time or length
- **THEN** the modal states that the booking already holds that time, and no row changes

#### Scenario: The modal states the truthful conditional
- **WHEN** an operator is deciding where to move a booking
- **THEN** the modal states that the package writes to the person who booked only where booking emails are configured

#### Scenario: Moving the last row of a page out of the window does not strand the operator
- **WHEN** an operator moves the only booking shown on a later page to a date outside the current window
- **THEN** the list shows the preceding page rather than an empty table, and never reports a range beyond its own total

#### Scenario: The modal is operable by keyboard
- **WHEN** a keyboard operator opens the move modal
- **THEN** focus lands inside it, every control is reachable and labelled, an error is announced in association with the control it concerns, and closing it returns focus to the control that opened it
