## MODIFIED Requirements

### Requirement: Bookings can be enumerated for management
The package SHALL provide a read port that **lists** bookings, distinct from the
availability reads that serve the front end. It SHALL be a Core port with a persistence
implementation, on the same terms as the existing resource and service management stores,
so that an alternative implementation is possible and has a stated contract to satisfy.

The port SHALL return, for each booking, everything a management list row displays
**without a further read per booking**: the booking's identity, its interval and the time
zone it was made in, its status, when it was created, the booker's name and email, each
claimed resource's id **and name**, and **the service it was placed for — id and name —
or nothing, for a booking placed directly**.

Resource names are part of the guarantee rather than a convenience. A claim records only a
resource id, so a caller given ids alone must perform a lookup per claim, which is the
cost this port exists to avoid. **The service name is carried for the same reason and one
more**: it is the name recorded at placement time, so it remains answerable for a service
that has since been renamed or deleted, which a read-time join could not do.

**This port SHALL remain read-only.** It lists; it does not change what it lists. That is
what makes it substitutable and what keeps a caller's reads free of side effects — the
cancel endpoint in this same capability reaches the domain, never this port.

#### Scenario: A booking is listed with everything a row shows
- **WHEN** bookings are listed
- **THEN** each result carries its interval, time zone, status, creation time, booker name and email, every claimed resource as an id and a name, and its service attribution as an id and a name where it has one

#### Scenario: Resource names come back with the list
- **WHEN** a listed booking claims a resource
- **THEN** that resource's name is present in the result, without the caller reading the resource separately

#### Scenario: The service comes back with the list
- **WHEN** a listed booking was placed through a service
- **THEN** that service's id and its name as recorded at placement are present in the result, without the caller reading the service separately

#### Scenario: A directly placed booking reports no service
- **WHEN** a listed booking was placed directly
- **THEN** it carries no service attribution, distinguishable from a service whose name is empty

#### Scenario: The front-end reads are unaffected
- **WHEN** this port is added
- **THEN** the availability and placement reads behave exactly as before, and no booking is placed, cancelled or altered **by this port**

*The scenario above said "by any operation in this capability" until cancellation joined it.
The narrowing is to what the scenario was always guarding — that adding a read port changes
nothing — and the guarantee it protected is now carried explicitly by the read-only SHALL
above, which is stronger than an aside in a scenario's THEN.*

## ADDED Requirements

### Requirement: An operator can cancel a booking
The package SHALL expose a versioned backoffice endpoint that cancels a booking, in the same
swagger group and under the same section authorization as every other uBookIt management
endpoint.

**It SHALL apply the domain's status machine rather than its own rule.** Cancellation
succeeds from `Requested` and `Confirmed` and from nothing else; a second attempt on an
already-cancelled booking SHALL be refused with the domain's stable code rather than treated
as a no-op success. A caller that is told "cancelled" when nothing changed cannot tell a
completed action from a rejected one.

**A cancelled booking SHALL remain.** It keeps its row, its interval, its booker and the
service it was placed for; it stops holding its time; and it is still returned by the
management list when asked for. The endpoint SHALL therefore not be a deletion, in verb or in
effect.

The response SHALL carry the booking's identity and its new status, so a caller knows what
happened without reading it back.

**It SHALL NOT carry the shape the list carries.** A list row includes each claimed
resource's *name*, which the management port supplies by joining; cancellation goes through
the domain, which knows a booking's resource *ids* and nothing more. A response shaped like a
list row but with those names blank would be quietly less true than the thing it resembles,
and there is no by-id read on the management port to fill them from. What a caller needs
next — whether the booking still belongs in the filter it is looking at, and what the total is
now — are properties of the query rather than of the booking, so they come from asking again.

*This requirement said "the booking as it now stands" until implementation showed that could
not be honoured on this path. The sentence was written before the shape was known; it is
corrected rather than satisfied with blanks.*

An unknown booking SHALL be reported as not found, distinctly from a booking that exists and
cannot be cancelled — they call for different actions from the operator.

#### Scenario: A confirmed booking is cancelled
- **WHEN** an authorized operator cancels a confirmed booking
- **THEN** it becomes cancelled, stops holding its time, and the response names it and reports its new status

#### Scenario: The response does not imitate a list row
- **WHEN** a cancellation's response model is inspected
- **THEN** it carries no resource collection, rather than one whose names this path cannot fill

#### Scenario: Cancelling twice is refused, not silently accepted
- **WHEN** an operator cancels a booking that is already cancelled
- **THEN** the request fails with the domain's invalid-transition code, and the booking is unchanged

#### Scenario: An unknown booking is distinguishable from an uncancellable one
- **WHEN** cancellation is requested for an id no booking has
- **THEN** the failure says the booking was not found, distinctly from the failure a booking that cannot be cancelled produces

#### Scenario: Cancelling is not deleting
- **WHEN** a booking has been cancelled
- **THEN** it is still returned by the management list for its window when cancelled bookings are asked for, carrying the same interval, booker and service it always had

#### Scenario: The endpoint is not anonymous
- **WHEN** the endpoint is called without backoffice authentication
- **THEN** the response is 401 and no booking changes

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

**The view SHALL state that cancelling notifies nobody by itself**, where the operator can see
it at the moment they are deciding. A customer who is not told is the predictable consequence
of the button, and an operator who assumes the package sends something will not find out until
somebody arrives for a booking that no longer exists.

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

#### Scenario: The operator is told the customer is not
- **WHEN** an operator is deciding whether to cancel
- **THEN** the view states that the package notifies nobody by itself
