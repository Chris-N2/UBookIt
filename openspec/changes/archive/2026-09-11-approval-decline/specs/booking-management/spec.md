# booking-management — delta for approval-decline

## ADDED Requirements

### Requirement: An operator can confirm or decline a requested booking
The package SHALL expose versioned backoffice endpoints that confirm a booking and that
decline one, in the same swagger group and under the same section authorization as every
other uBookIt management endpoint.

**Each SHALL apply the domain's status machine rather than its own rule.** Confirmation and
decline succeed from `Requested` and from nothing else; an attempt on a booking in any other
status SHALL be refused with the domain's stable code rather than treated as a no-op success.
A caller told "confirmed" when nothing changed cannot tell a completed action from a rejected
one.

**A declined booking SHALL remain.** It keeps its row, its interval, its booker and the
service it was placed for; it stops holding its time; and it is still returned by the
management list when asked for. Neither endpoint SHALL be a deletion, in verb or in effect —
and a declined booking's booker SHALL still be erasable, because a person the site turned
away holds their details exactly as firmly as one it served.

The response SHALL carry the booking's identity and its new status, so a caller knows what
happened without reading it back. **It SHALL NOT carry the shape the list carries**, on the
same grounds the cancellation response does not: this path goes through the domain, which
knows resource ids and nothing more, and a response shaped like a list row with blank names
would be quietly less true than the thing it resembles.

An unknown booking SHALL be reported as not found, distinctly from a booking that exists and
cannot take the transition — they call for different actions from the operator.

#### Scenario: A requested booking is confirmed
- **WHEN** an authorized operator confirms a requested booking
- **THEN** it becomes confirmed, continues to hold its time, and the response names it and reports its new status

#### Scenario: A requested booking is declined
- **WHEN** an authorized operator declines a requested booking
- **THEN** it becomes declined, stops holding its time, and the response names it and reports its new status

#### Scenario: Confirming a booking that is not requested is refused
- **WHEN** an operator confirms a booking that is already confirmed, declined or cancelled
- **THEN** the request fails with the domain's invalid-transition code, and the booking is unchanged

#### Scenario: An unknown booking is distinguishable from an ineligible one
- **WHEN** confirmation or decline is requested for an id no booking has
- **THEN** the failure says the booking was not found, distinctly from the failure a booking in the wrong status produces

#### Scenario: Declining is not deleting
- **WHEN** a booking has been declined
- **THEN** it is still returned by the management list for its window when declined bookings are asked for, carrying the same interval, booker and service it always had

#### Scenario: The endpoints are not anonymous
- **WHEN** either endpoint is called without backoffice authentication
- **THEN** the response is 401 and no booking changes

### Requirement: The bookings view can confirm or decline a requested booking
The bookings view SHALL offer confirmation and decline for a `Requested` booking, and SHALL
NOT offer either for a booking in any other status — a control that is always refused teaches
an operator to ignore failures.

**The screen's judgement SHALL NOT be the rule.** The endpoints SHALL refuse an invalid
transition independently, so a screen showing a stale list cannot talk the domain into one.

**Decline SHALL require confirmation** through an accessible in-page modal provided by the
backoffice, never a native browser dialog, on the same terms as cancellation — a decline is
terminal for the booking and outward-facing for the customer. Dismissing the modal SHALL
leave the booking untouched. Confirmation of a booking SHALL NOT require a modal: it is the
expected disposition of a request, and it remains recoverable in the sense that a confirmed
booking can still be cancelled.

**The decline modal SHALL tell the operator the truthful conditional about notification**, on
the same terms as the corrected cancellation wording: the package writes to the person who
booked only where the site has configured booking emails, so an operator on an unconfigured
site knows the customer will not otherwise find out.

After a confirmation or decline the list SHALL show the booking's new state without the
operator reloading the page. Where the acted-on row leaves the current filter (the status
default excludes what is not booked, so a declined row leaves the default view), the paging
and focus behaviour SHALL be the cancellation requirement's: step back rather than reset when
the last row of a page goes, and place focus deliberately rather than losing it to the
document.

A refused transition SHALL be shown to the operator rather than the row appearing to change.

#### Scenario: An operator confirms from the list
- **WHEN** an operator activates confirm on a requested booking
- **THEN** the booking is confirmed and the list shows its new state without a page reload

#### Scenario: An operator declines from the list
- **WHEN** an operator activates decline on a requested booking and confirms the modal
- **THEN** the booking is declined and the list shows its new state without a page reload

#### Scenario: Dismissing the decline modal changes nothing
- **WHEN** an operator activates decline and then dismisses or cancels the modal
- **THEN** no request is issued and the booking is unchanged

#### Scenario: Only requested bookings offer the controls
- **WHEN** the list shows a confirmed, declined or cancelled booking
- **THEN** no confirm control and no decline control is offered for it

#### Scenario: A refused transition is reported
- **WHEN** an endpoint refuses a transition the screen believed was possible
- **THEN** the failure is shown to the operator rather than the row appearing to change

#### Scenario: Declining the last row of a page does not strand the operator
- **WHEN** an operator declines the only booking shown on a later page under a filter the declined booking leaves
- **THEN** the list shows the preceding page rather than an empty table, and never reports a range beyond its own total

#### Scenario: The decline modal states the truthful conditional
- **WHEN** an operator is deciding whether to decline
- **THEN** the modal states that the package writes to the person who booked only where booking emails are configured

## MODIFIED Requirements

### Requirement: The bookings view can cancel a booking
The bookings view SHALL offer cancellation for a booking that can be cancelled, and SHALL NOT
offer it for one that cannot — a control that is always refused teaches an operator to ignore
failures.

**The screen's judgement SHALL NOT be the rule.** The endpoint SHALL refuse an invalid
transition independently, so a screen showing a stale list cannot talk the domain into one.

Cancellation SHALL require confirmation through an accessible in-page modal provided by the
backoffice, never a native browser dialog, on the same terms as deleting a resource.
Dismissing or cancelling the confirmation SHALL leave the booking untouched, and a
confirmation that fails to appear SHALL NOT be treated as a refusal — those are different
outcomes and only one of them is something the operator chose.

After a cancellation the list SHALL show the booking's new state without the operator
reloading the page.

**A cancellation SHALL NOT leave the operator on a page that no longer exists.** Removing the
last row of a page leaves the list positioned past the end of its own results, which renders
as an empty table under a count that cannot be true. The view SHALL step back rather than
returning to the first page: a filter change is a different question and resets, while a
cancellation is the same question with one fewer answer, and an operator working through a
later page should not be thrown to the start each time.

**Focus SHALL NOT be lost when the cancelled row is removed.** The control the operator was
using goes with its row, so focus SHALL be placed somewhere deliberate rather than falling to
the document — a keyboard operator cancelling several bookings would otherwise restart their
traversal every time.

#### Scenario: Cancelling the last row of a page does not strand the operator
- **WHEN** an operator cancels the only booking shown on a later page
- **THEN** the list shows the preceding page rather than an empty table, and never reports a range beyond its own total

#### Scenario: Focus survives the cancelled row
- **WHEN** a keyboard operator confirms a cancellation and the row is removed
- **THEN** focus is placed deliberately within the view rather than lost to the document

**The view SHALL state the truthful conditional about notification, where the operator can
see it at the moment they are deciding**: the package writes to the person who booked only
where the site has configured booking emails. It SHALL NOT assert unconditionally that nobody
is told — that sentence was true when it was written and stopped being true when booking
emails shipped: on a site that has configured them, cancelling from this screen does send the
booker a cancellation notice. A customer who is not told remains the predictable consequence
of the button on an unconfigured site, and the operator deciding is the person who needs to
know which site they are on.

*This requirement previously demanded the view state "that cancelling notifies nobody by
itself", and the shipped dialog said so flatly. The `booking-emails` capability falsified
both; the docs were corrected at the time and this sentence was missed. It is corrected here,
in the change that adds sibling dialogs to the same screen, rather than left for a reader to
discover the spec demanding a falsehood.*

#### Scenario: An operator cancels from the list
- **WHEN** an operator confirms cancellation of a booking in the list
- **THEN** the booking is cancelled and the list shows its new state without a page reload

#### Scenario: Dismissing the confirmation changes nothing
- **WHEN** an operator activates cancel and then dismisses or cancels the confirmation
- **THEN** no request is issued and the booking is unchanged

#### Scenario: A booking that cannot be cancelled offers no control
- **WHEN** the list shows a cancelled or declined booking
- **THEN** no cancel control is offered for it

#### Scenario: A refused cancellation is reported
- **WHEN** the endpoint refuses a cancellation the screen believed was possible
- **THEN** the failure is shown to the operator rather than the row appearing to change

#### Scenario: The operator is told the truthful conditional
- **WHEN** an operator is deciding whether to cancel
- **THEN** the view states that the package writes to the person who booked only where booking emails are configured, and asserts nothing stronger in either direction
