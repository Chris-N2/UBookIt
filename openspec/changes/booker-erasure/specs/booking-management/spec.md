## MODIFIED Requirements

### Requirement: Bookings can be enumerated for management
The package SHALL provide a read port that **lists** bookings, distinct from the
availability reads that serve the front end. It SHALL be a Core port with a persistence
implementation, on the same terms as the existing resource and service management stores,
so that an alternative implementation is possible and has a stated contract to satisfy.

The port SHALL return, for each booking, everything a management list row displays
**without a further read per booking**: the booking's identity, **its quotable reference**,
its interval and the time zone it was made in, its status, when it was created, **the booker's
contact details or the fact and instant of their erasure**, each claimed resource's id **and
name**, and **the service it was placed for — id and name — or nothing, for a booking placed
directly**.

**The booker SHALL cross this port in exactly the two states the domain permits**: contact
details present, or erased with the instant recorded. The port SHALL NOT report an erasure by
supplying empty contact details, and a consumer SHALL NOT be able to read a name or an email
without having established that they are present. Whether a *caller* may see present details
is not this port's question — it lists what is stored, and the decision about who may read it
belongs to the endpoint.

Resource names are part of the guarantee rather than a convenience. A claim records only a
resource id, so a caller given ids alone must perform a lookup per claim, which is the
cost this port exists to avoid. **The service name is carried for the same reason and one
more**: it is the name recorded at placement time, so it remains answerable for a service
that has since been renamed or deleted, which a read-time join could not do.

**The reference is carried for a third reason: somebody is on the telephone.** The case this
port exists to serve is an operator finding the booking a caller is describing, and the caller
has a reference in front of them and not a machine identifier. A row that cannot be matched
against what the customer is reading out fails at the moment it is most needed. **An erased
booking is precisely the case this matters most for**, since the reference is then the only
thing left to call it by.

**This port SHALL remain read-only.** It lists; it does not change what it lists. That is
what makes it substitutable and what keeps a caller's reads free of side effects — the
cancel and erase endpoints in this same capability reach the domain, never this port.

#### Scenario: A booking is listed with everything a row shows
- **WHEN** bookings are listed
- **THEN** each result carries its reference, interval, time zone, status, creation time, its booker's contact details or its erasure, every claimed resource as an id and a name, and its service attribution as an id and a name where it has one

#### Scenario: Resource names come back with the list
- **WHEN** a listed booking claims a resource
- **THEN** that resource's name is present in the result, without the caller reading the resource separately

#### Scenario: The service comes back with the list
- **WHEN** a listed booking was placed through a service
- **THEN** that service's id and its name as recorded at placement are present in the result, without the caller reading the service separately

#### Scenario: A directly placed booking reports no service
- **WHEN** a listed booking was placed directly
- **THEN** it carries no service attribution, distinguishable from a service whose name is empty

#### Scenario: An operator can match what a caller reads out
- **WHEN** a listed booking is displayed to an operator
- **THEN** its quotable reference is among what is shown, so a booking can be identified from what the customer has in front of them

#### Scenario: The front-end reads are unaffected
- **WHEN** this port is added
- **THEN** the availability and placement reads behave exactly as before, and no booking is placed, cancelled or altered **by this port**

#### Scenario: An erased booking is listed as erased
- **WHEN** a booking whose booker was erased is listed
- **THEN** it appears in the results carrying the fact and instant of erasure, no name or email, and every other field it always carried

#### Scenario: The port does not blank an erased booker
- **WHEN** an erased booking crosses the port
- **THEN** no empty or placeholder name or email is supplied in place of the erased values

#### Scenario: The port stays read-only
- **WHEN** the port's surface is inspected
- **THEN** it offers no operation that changes a booking, erasure included

*The front-end scenario above said "by any operation in this capability" until cancellation
joined it. The narrowing is to what the scenario was always guarding — that adding a read port
changes nothing — and the guarantee it protected is now carried explicitly by the read-only
SHALL above, which is stronger than an aside in a scenario's THEN.*

### Requirement: Bookings are readable over an authorized management endpoint
The package SHALL expose a **versioned** backoffice endpoint, **in the same swagger group as
its other management endpoints**, returning the **windowed, paged, filtered** list of bookings
the management read port provides.

The endpoint SHALL require backoffice authorization on the same terms as every other
uBookIt management endpoint. **It SHALL NOT be reachable anonymously under any configuration
the package ships.**

**Request and response bodies SHALL be purpose-built models.** **Domain types SHALL NOT appear
in the HTTP contract**, on the same terms as the resource and service endpoints.

The response SHALL carry, per booking: the booking's identity, **its quotable reference**, its
interval and the time zone it was made in, its status, when it was created, **the booker as one
of three stated conditions — shown, withheld, or erased**, each claimed resource's id and name,
and **the service it was placed for, or null**. It SHALL report the unpaged total alongside the
page, so a caller can render a pager.

**The response SHALL carry what the read port supplies, less what the caller may not see, and
never more.** The subtraction is the whole of the difference: no field is added here that the
port cannot fill, and no field the port fills reaches a caller who is not permitted it. A
field the port does not carry is one the screen must obtain another way, which is how a
second read path into bookings begins.

**BREAKING (unpublished): the booker member SHALL NOT be nullable, and SHALL state its own
condition.** It SHALL always be present and SHALL say which of *shown*, *withheld* or *erased*
applies; contact details SHALL be carried only in the first case, and the erasure instant only
in the last. This replaces the nullable member whose null meant "withheld from you", which was
added after `0.1.0` and never published.

**The reason is that absence now has two causes and they demand different actions.** A null
could mean the caller may not see the details or that nobody can; *"ask a colleague who has
access"* and *"this is gone permanently"* are different next steps, and a client made to
distinguish them by inference would be inferring something the server already knows. A member
whose absence is already meaningful must not be overloaded to carry withholding as well, per
the `sensitive-data` capability — so the null is spent, and the condition is stated instead.

**Contact details SHALL remain clumped in a single member**, on the same terms and for the same
reason as the service: two parallel nullable strings could be observed half-populated and leave
a client with no correct reading. **A booking without a booker SHALL remain inexpressible** —
the domain requires a booker of every booking, and erasure removes the person's details, not
the booker.

**The condition SHALL cross the boundary by name rather than as a domain enum**, on the same
terms as the status, because an enum's members are a versioning commitment and domain types do
not appear in this contract.

**Erasure SHALL take precedence over withholding.** Where details have been erased there is
nothing to withhold, so a caller without sensitive-data access SHALL be told the booking is
erased. That a record was erased is a fact about the record and not about the person, and it
discloses nothing.

**Whether the caller may see present details SHALL be decided from Umbraco's sensitive-data
access**, per the `sensitive-data` capability, and SHALL be settled before the response is
composed. The endpoint SHALL NOT return the details and rely on a client to suppress them.

**The reference SHALL cross the boundary in canonical form.** How it is grouped for reading is
the client's decision, and a client that searches or compares needs the value exactly as it is
stored — the same reasoning that keeps widgets out of every other contract this package
publishes. **The reference is not personal data and SHALL NOT be withheld or erased**: it
identifies a booking, not a person, and withholding it would leave a row with nothing to call
it by.

**The service SHALL cross the boundary as a single nullable object carrying both id and
name**, rather than as two parallel nullable fields. "A booking has a service, or it does
not" is then expressed in the shape, rather than as a rule that two fields must be null
together — which a client can observe violated and has no way to interpret.

#### Scenario: A page of bookings is returned with its total
- **WHEN** an authorized caller with sensitive-data access requests bookings for a window
- **THEN** the matching page is returned with the unpaged total, each booking carrying its reference, interval, time zone, status, creation time, a booker stated as shown with its name and email, its resources with their names, and its service where it has one

#### Scenario: A caller without sensitive-data access receives the same rows without contact details
- **WHEN** an authorized caller lacking sensitive-data access requests bookings for a window
- **THEN** the same bookings are returned with the same total, each carrying its reference, interval, time zone, status, creation time, resources and service, and a booker stated as withheld

#### Scenario: A withheld booker leaves nothing behind in the payload
- **WHEN** a booking's booker is withheld
- **THEN** neither the name nor the email appears anywhere in the response, including as an empty or masked value

#### Scenario: An erased booking is reported as erased, to anyone
- **WHEN** a booking whose booker was erased is returned to a caller with sensitive-data access, and to a caller without it
- **THEN** both receive a booker stated as erased, carrying the erasure instant and no name or email

#### Scenario: Erased is not reported as withheld
- **WHEN** a caller without sensitive-data access receives a booking whose booker was erased
- **THEN** its booker is stated as erased rather than as withheld

#### Scenario: The booker member is never absent
- **WHEN** any booking is returned by the endpoint, in any of the three conditions
- **THEN** the booker member is present and states its condition, rather than being null

#### Scenario: An erased booker leaves nothing behind in the payload
- **WHEN** a booking's booker was erased
- **THEN** no name, email, phone or member key appears anywhere in the response, including as an empty or masked value

#### Scenario: A directly placed booking carries a null service
- **WHEN** a returned booking was placed directly
- **THEN** its service member is null, rather than an object with empty or placeholder values

#### Scenario: The endpoint is not anonymous
- **WHEN** the endpoint is called without backoffice authentication
- **THEN** the response is 401 and no handler logic executes

#### Scenario: No domain type crosses the HTTP boundary
- **WHEN** the endpoint's request and response models are inspected
- **THEN** they are purpose-built models, and no domain or store type appears in the contract

#### Scenario: The reference crosses the boundary unformatted
- **WHEN** a booking's reference is returned by the endpoint
- **THEN** it is the canonical stored value, leaving the client to group it for display

#### Scenario: The reference survives withholding
- **WHEN** a booking's booker is withheld from the caller
- **THEN** its reference is still returned, in canonical form

#### Scenario: The reference survives erasure
- **WHEN** a booking's booker has been erased
- **THEN** its reference is still returned, in canonical form

### Requirement: Bookings have a backoffice collection view
The package SHALL register a **Bookings** view in the uBookIt backoffice section, beside the
resource and service views and under the same section condition, listing the bookings the
management endpoint returns.

The list SHALL be a semantic table built from the backoffice UI library, showing per booking:
**its quotable reference**, when it runs, **the booker — their contact details, or a statement
of why the row carries none**, the
resources it claims by name, the service it was placed for, and its status. **The reference
SHALL come first**, because it is the column an operator scans while somebody reads it out —
which is the case the whole identifier exists for. It SHALL show the unpaged total and page
through results, in the same idiom the section's existing lists use.

**A withheld booker SHALL be rendered as a stated absence, not as an empty cell.** The view
SHALL show a localized indication that contact details are hidden, for the same reason a
booking with no service says so rather than leaving its cell blank: a blank cell reads as
missing data, and here it would read as a defect in the package.

**Where any row's booker is withheld, the view SHALL explain why**, once, where an operator
reading the list will see it — naming Umbraco's Sensitive data group as what grants access.
An operator who has just been made an administrator and finds every contact detail hidden has
no way to reach that explanation from the screen otherwise, and the most likely next action is
a defect report. The explanation SHALL be exposed to assistive technology on the same terms as
the view's other messages, rather than only rendered.

**The view SHALL NOT compute anything the endpoint does not return.** A value the endpoint
cannot supply is a finding about the endpoint, not a calculation to add to a screen — the
same rule the endpoint already carries about fields the read port cannot supply, pointed one
layer further out. **It SHALL NOT ask any other source whether contact details may be shown**:
the response already says so by what it carries, and a second source could disagree with the
first while looking authoritative.

**Formatting a value the endpoint did return is not computing one.** The reference crosses
the boundary canonical and is grouped for reading on the way to the screen, exactly as an
instant crosses as UTC and is rendered in the booking's zone. The rule above is about the
view inventing data; presentation of data it was given is the view's own business, and
stating that here keeps a reader of this requirement alone from seeing a prohibition being
broken.

No third-party widget framework SHALL be used, and every string the view displays SHALL come
from the package's localization with `en-US` provided.

#### Scenario: The section lists bookings
- **WHEN** a backoffice user with access to the uBookIt section and to sensitive data opens the Bookings view
- **THEN** bookings in the default window are listed with their reference, time, booker, resources, service and status, and the unpaged total is shown

#### Scenario: A withheld booker says so
- **WHEN** a listed booking's booker was withheld by the endpoint
- **THEN** the cell states that contact details are hidden, rather than being blank, and the row is still identifiable by its reference

#### Scenario: The operator is told what grants access
- **WHEN** any row on the page has a withheld booker
- **THEN** the view explains once that contact details require membership of Umbraco's Sensitive data group, exposed to assistive technology rather than only rendered

#### Scenario: A page with nothing withheld carries no explanation
- **WHEN** no row's booker was withheld from the caller
- **THEN** the explanation is not shown, whether the rows carry contact details or record an erasure

#### Scenario: Visibility is read from the response, not asked elsewhere
- **WHEN** the view's decision to show or hide contact details is inspected
- **THEN** it follows from what the endpoint returned, with no separate query about the current user's permissions

#### Scenario: Paging reaches the rest
- **WHEN** more bookings match than fit on one page
- **THEN** the remaining bookings are reachable, and the total reported is the number matching the query rather than the number on the page

#### Scenario: A booking placed directly says so
- **WHEN** a listed booking has no service
- **THEN** the view states that it was booked directly, rather than leaving the cell blank as though the value were missing

*Two clauses above were restated for the three-condition contract. "The booker where the
endpoint supplied one" described a member that could be absent; it is now always supplied and
states its own condition, so the sentence described nothing. And the no-explanation scenario
said "every row's booker was supplied", which after erasure is vacuously true of every page
including an all-erased one — it no longer selected the case it was written to guard. Both are
restatements of the same guarantees against the new shape, not narrowings.*

## ADDED Requirements

### Requirement: An operator can erase a booking's booker

The package SHALL expose a **versioned** backoffice endpoint, in the same swagger group as its
other management endpoints, that erases one booking's booker contact details.

**It SHALL require sensitive-data access in addition to section access**, and that requirement
SHALL be a property of the endpoint rather than a condition evaluated inside it: a caller
without sensitive-data access SHALL be refused before any erasure occurs. A user the site has
decided may not read a booker's name SHALL NOT be able to destroy it.

**It SHALL be a POST, not a DELETE.** An erased booking is not gone: it keeps its row, its
reference, its interval, its claims and its status, and it still blocks time. `DELETE` would
say the opposite of all of that, exactly as it would for a cancellation.

**It SHALL reach the domain, not the read port.** The erase verb is a domain operation on the
same terms as cancellation, and the management read port stays read-only.

**It SHALL be idempotent.** Erasing a booking whose booker is already erased SHALL succeed and
change nothing, including the recorded instant. This is deliberately unlike the cancel
endpoint, which refuses a second attempt; the reasoning is in the `booker-erasure` capability.

**Its response SHALL carry what the operation knows** — the booking's identity and the fact and
instant of erasure — rather than a whole list row, on the same terms and for the same reason as
cancellation: this path reaches the booking through the domain, which knows resource ids and
not resource names, and a response shaped like the list's would be quietly less true than it.

**The response SHALL NOT echo what was erased.** Returning the removed name or address as
confirmation would hand back the data the operation exists to remove.

#### Scenario: A permitted operator erases a booking's booker
- **WHEN** a backoffice user with section access and sensitive-data access posts an erasure for a booking
- **THEN** the operation succeeds and the response carries the booking's id and the instant of erasure

#### Scenario: Section access alone cannot erase
- **WHEN** a backoffice user with section access but without sensitive-data access posts an erasure
- **THEN** the request is refused, and the booking's booker is unchanged

#### Scenario: The endpoint is not anonymous
- **WHEN** the endpoint is called without backoffice authentication
- **THEN** the response is 401 and no handler logic executes

#### Scenario: Erasing twice succeeds
- **WHEN** an erasure is posted for a booking whose booker is already erased
- **THEN** the response is a success and the recorded erasure instant is unchanged

#### Scenario: Erasing an unknown booking is refused
- **WHEN** an erasure is posted for an id no booking has
- **THEN** the response reports that it was not found, and nothing is changed

#### Scenario: The response does not echo the erased details
- **WHEN** an erasure succeeds
- **THEN** no name, email, phone or member key appears anywhere in the response

#### Scenario: The booking survives the erasure
- **WHEN** a booking's booker is erased through the endpoint and the booking is then listed
- **THEN** it is still returned with the same reference, interval, time zone, status, creation time, resources and service

### Requirement: The bookings view distinguishes an erased booker from a withheld one

The backoffice bookings view SHALL render a booking whose booker was erased as **its own stated
condition**, distinct from a withheld booker and from an empty cell, and SHALL NOT present it
with the explanation offered for withheld rows.

**The distinction is the point.** A withheld row tells an operator to ask a colleague who has
sensitive-data access; an erased row tells them there is nobody to ask. Rendering the second as
the first sends them on an errand that cannot succeed, and rendering either as a blank cell
reads as a defect in the package.

**The view SHALL read the condition from the response**, which states it, rather than inferring
it from which fields are absent and rather than asking any other source. **It SHALL NOT compute
anything the endpoint does not return**, on the same terms as the rest of this view.

**The explanation about the Sensitive data group SHALL be shown only where rows were actually
withheld.** A page on which every absent booker was erased SHALL NOT tell the operator that
membership of a group would reveal them, because it would not.

Every string SHALL come from the package's localization with `en-US` provided, and SHALL be
exposed to assistive technology on the same terms as the view's other messages.

#### Scenario: An erased booker says so
- **WHEN** a listed booking's booker was erased
- **THEN** the cell states that the contact details were erased, rather than being blank or stating that they are hidden

#### Scenario: An erased row does not offer the group explanation
- **WHEN** every row with no contact details on the page was erased rather than withheld
- **THEN** the explanation about Umbraco's Sensitive data group is not shown

#### Scenario: A page with both conditions distinguishes them
- **WHEN** a page carries one withheld booker and one erased booker
- **THEN** the two rows read differently, and the group explanation is shown for the withheld one

#### Scenario: The condition is read from the response
- **WHEN** the view's decision between erased and withheld is inspected
- **THEN** it follows from the condition the endpoint stated, with no inference from absent fields and no separate query

#### Scenario: An erased row is still identifiable
- **WHEN** a listed booking's booker was erased
- **THEN** the row still shows its reference, time, resources, service and status
