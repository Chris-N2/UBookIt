## Purpose

A site-level list of dates on which the organisation itself is closed, inherited by every resource
unless that resource explicitly opts out of an individual closure. It exists so that "we are shut
on Boxing Day" is one decision with one record, rather than the same date re-typed into every
resource's exception list, and so that a later public-holiday feed has somewhere to write.

## ADDED Requirements

### Requirement: The site closure list

The package SHALL hold a site-level list of closures. Each closure SHALL carry a calendar date and
a label, and SHALL be identified by a stable id that does not change when either is edited.

**The label SHALL be required**, non-empty after trimming, and SHALL be rejected with the stable
code `closure-label-invalid` when it is absent, blank, or longer than the package's name-column
length. A label is what makes an inherited closure legible where it is inherited: a resource editor
offering an opt-out for a bare date asks an operator to consent to something they cannot identify.

**At most one closure SHALL exist per date**, rejected with the stable code
`duplicate-closure-date`. A second closure on a date could only say the same thing twice or
contradict itself.

A closure SHALL be **full-day**. Partial-day closing remains the per-resource exception's job, and
SHALL NOT be expressible here.

Closures SHALL be returned in a deterministic order.

#### Scenario: A closure is created with a date and a label
- **WHEN** a closure is created for 2026-12-25 with the label "Christmas Day"
- **THEN** it is stored, carries a stable id, and is returned by the closure list

#### Scenario: A closure without a label is refused
- **WHEN** a closure is submitted with a blank or whitespace-only label
- **THEN** the write fails with the code `closure-label-invalid` and nothing is stored

#### Scenario: An over-long label is refused as validation, not as a storage error
- **WHEN** a closure is submitted with a label longer than the permitted length
- **THEN** the write fails with the code `closure-label-invalid`, and no database error is surfaced

#### Scenario: A second closure on the same date is refused
- **WHEN** a closure is created for a date that already carries one
- **THEN** the write fails with the code `duplicate-closure-date` and the existing closure is unchanged

#### Scenario: Editing a closure preserves its identity
- **WHEN** a closure's date and label are both changed
- **THEN** its id is unchanged

#### Scenario: Listing is deterministically ordered
- **WHEN** the closure list is read twice against unchanged data
- **THEN** both reads present the same closures in the same order

### Requirement: A closure closes every resource for its date

A site closure SHALL close every resource for its date, **taking precedence over both the weekly
open-hours pattern and any date exception the resource carries of its own**. A resource that has
not opted out SHALL have no available time on a closure date, whatever its own configuration says.

**The resource's own exception is not silently discarded — it is superseded for that date and
resumes its full effect if the closure is removed or opted out of.** The closure layer SHALL be
held separately from the resource's own exceptions and SHALL NOT be written into them, so that no
save of a resource can convert an inherited closure into a date the resource owns.

A resource created after a closure exists SHALL inherit it on the same terms as any other, without
anything being copied onto it at creation.

#### Scenario: A closure closes an otherwise-open resource
- **WHEN** a resource open Friday 09:00–17:00 is queried for a Friday carrying a site closure
- **THEN** the availability projection for that date offers no free time

#### Scenario: A closure supersedes the resource's own override exception
- **WHEN** a resource carries an override exception of 10:00–14:00 on a date that also carries a site closure, and has not opted out
- **THEN** the availability projection for that date offers no free time

#### Scenario: The superseded exception is not destroyed
- **WHEN** the site closure on that date is deleted
- **THEN** the resource's own 10:00–14:00 override exception takes effect again, without being re-entered

#### Scenario: Saving a resource does not absorb the closure
- **WHEN** a resource inheriting a site closure is loaded, saved unchanged, and the closure is then deleted
- **THEN** the resource has no exception for that date, and the date resolves from its weekly pattern

#### Scenario: A newly created resource inherits existing closures
- **WHEN** a resource is created while closures already exist
- **THEN** it is closed on those dates without any exception or opt-out having been written for it

### Requirement: A resource opts out of an individual closure by id

A resource SHALL be able to opt out of any individual closure. An opt-out SHALL name the **closure
id**, not the date, so that editing a closure's date carries the opt-out with it rather than
stranding it on a date nobody chose.

**Opting out SHALL mean the closure does not reach that resource at all.** The date SHALL then
resolve exactly as it would if the closure did not exist: the resource's own date exception where
it has one, otherwise its weekly pattern. There SHALL be no third outcome peculiar to opted-out
dates.

Opt-outs SHALL be per resource and SHALL NOT affect any other resource. A write of a resource SHALL
replace its opt-out set wholesale rather than merging into it, consistent with the full-replacement
semantics of the rest of the resource contract. An opt-out naming a closure that does not exist
SHALL be refused with the stable code `closure-not-found`.

#### Scenario: Opting out restores the weekly pattern
- **WHEN** a resource open 09:00–17:00 opts out of a closure on one of its open days
- **THEN** the availability projection for that date offers 09:00–17:00

#### Scenario: Opting out restores the resource's own exception
- **WHEN** a resource carrying an override exception of 10:00–14:00 on a closure date opts out of that closure
- **THEN** the availability projection for that date offers 10:00–14:00, and not the weekly pattern

#### Scenario: Opting out of a closure the resource would close anyway changes nothing
- **WHEN** a resource whose own exception for that date is itself a closure opts out of the site closure
- **THEN** the date remains closed, by the resource's own rule

#### Scenario: An opt-out reaches only its own resource
- **WHEN** one resource opts out of a closure
- **THEN** every other resource remains closed on that date

#### Scenario: Editing a closure's date carries opt-outs with it
- **WHEN** a closure a resource has opted out of is moved to a different date
- **THEN** that resource is open on the new date on the same terms, and the old date resolves normally for every resource

#### Scenario: An update replaces the opt-out set
- **WHEN** a resource holding two opt-outs is updated with a body naming only one
- **THEN** it holds only that one, and is closed again on the other date

#### Scenario: An opt-out naming no closure is refused
- **WHEN** a resource is saved with an opt-out naming an id no closure carries
- **THEN** the write fails with the code `closure-not-found` and the resource is unchanged

#### Scenario: Deleting a closure removes its opt-outs
- **WHEN** a closure that resources have opted out of is deleted
- **THEN** the closure and its opt-outs are gone, and no resource retains a reference to it

### Requirement: Closures never alter bookings already placed

Creating, editing or deleting a closure SHALL change no booking. A booking already placed on a date
that becomes closed SHALL keep its interval, status and reference, and SHALL continue to block that
time against further placement.

**This SHALL be stated by the closures view unconditionally**, without querying bookings. It is true
of every site whatever its data, so a count is unnecessary to make the statement accurate — and a
number rendered beside an action can be stale between the render and the reading of it.

#### Scenario: An existing booking survives a closure
- **WHEN** a closure is created on a date that already carries a confirmed booking
- **THEN** the booking is unchanged, and the time it holds remains claimed

#### Scenario: The statement reads no booking data
- **WHEN** the closures view produces its statement about existing bookings
- **THEN** it is the same on every site, and producing it reads no booking data

#### Scenario: A closed date refuses new placement
- **WHEN** placement is requested on a closure date for a resource that has not opted out
- **THEN** the placement is refused as outside open hours

### Requirement: Closures are read and written by different verbs

**Writing the closure list SHALL require `UBookIt.Settings`.** Closing the whole organisation is a
site-level act, and the grant that means "may add a meeting room" SHALL NOT carry it.

**Reading the closure list, and setting a resource's opt-outs, SHALL require `UBookIt.Configure` or
`UBookIt.Settings`.** An operator editing a resource must be able to see what that resource is
inheriting and exempt it, and cannot do either without reading the list.

Both SHALL be enforced by the server, whatever the client rendered.

**Where a user may read but not write, the package SHALL say so** rather than presenting controls
whose use would be refused. `UBookIt.Settings` is never seeded, so on an upgraded site nobody holds
it until it is granted — a closures view that merely showed inert buttons would read as a feature
that failed to ship.

#### Scenario: Configure alone reads but cannot write
- **WHEN** a user whose groups hold only `UBookIt.Configure` reads the closure list and then attempts to create a closure
- **THEN** the list is served and the creation is refused

#### Scenario: Configure alone may still opt a resource out
- **WHEN** a user whose groups hold only `UBookIt.Configure` saves a resource with an opt-out
- **THEN** the write is served

#### Scenario: Settings reaches the writes
- **WHEN** a user whose groups hold `UBookIt.Settings` creates, edits and deletes a closure
- **THEN** each write is served

#### Scenario: Neither verb reaches nothing
- **WHEN** a user holding the section but neither `UBookIt.Configure` nor `UBookIt.Settings` requests the closure list
- **THEN** the request is refused

#### Scenario: The read-only state is explained
- **WHEN** a user who may read but not write opens the closures view
- **THEN** the view states that changing closures requires the settings grant, and where it is granted, rather than rendering controls that would be refused

#### Scenario: An administrator is not exempt
- **WHEN** an Umbraco administrator none of whose groups hold `UBookIt.Settings` attempts to create a closure
- **THEN** the request is refused

### Requirement: The closures view

The package SHALL present closures in a backoffice view of their own within the uBookIt section,
listing each closure's date and label with actions to create, edit and delete.

**The view SHALL show upcoming closures by default and SHALL offer past ones on request.** A past
closure is the record of why a date was shut and SHALL NOT be deleted automatically; the default
filter keeps a list that grows every year usable without destroying anything.

The view's markup SHALL meet the project's accessibility bar on the same terms as the rest of the
section: every input labelled, failures programmatically associated with the control they concern
and announced on failed save, full keyboard operability with visible focus, and semantic headings
and controls.

#### Scenario: Upcoming closures are shown by default
- **WHEN** the closures view is opened on a site holding both past and future closures
- **THEN** the future closures are listed, and the past ones are not, until they are asked for

#### Scenario: Past closures remain available
- **WHEN** past closures are requested
- **THEN** they are listed, and nothing has been deleted

#### Scenario: Keyboard-only management
- **WHEN** a user operates the closures view using only a keyboard
- **THEN** creating, editing, deleting and revealing past closures are all reachable and operable with visible focus

#### Scenario: A failed save is announced
- **WHEN** a closure save fails validation
- **THEN** the failure is announced to assistive technology and associated with the control it concerns, and no entered data is lost

### Requirement: Inheritance is visible where a resource is edited

The resource editor SHALL show every site closure the resource inherits, with a per-closure control
opting this resource out. Each entry SHALL show the closure's date **and its label**, so that the
operator is exempting something they can identify.

**Where a resource carries an override exception on a closure date it has not opted out of, the
editor SHALL state that the exception is currently superseded.** The statement SHALL be
programmatically associated with the exception it concerns.

**The statement SHALL be made only where the outcome differs.** A resource whose own exception for
that date is itself a closure is closed either way, so describing it as superseded would report a
difference that does not exist. This distinction SHALL be decided by the server and presented to
the client, so that the precedence rule has exactly one implementation.

#### Scenario: Inherited closures are listed with their labels
- **WHEN** a resource is opened for editing on a site holding closures
- **THEN** each closure is listed with its date and label, and with a control opting this resource out

#### Scenario: A superseded override exception is marked
- **WHEN** a resource carrying an override exception on a non-opted-out closure date is opened
- **THEN** the editor states that the exception is currently superseded, associated with that exception

#### Scenario: A closure exception on a closure date is not marked
- **WHEN** a resource whose own exception for a closure date is itself a closure is opened
- **THEN** no superseded statement is made for it, because the date is closed either way

#### Scenario: Opting out clears the marker
- **WHEN** the resource opts out of that closure and is saved
- **THEN** reopening it shows the exception in effect and no superseded statement

#### Scenario: The client does not decide precedence
- **WHEN** the backoffice client is inspected
- **THEN** it renders the superseded state the server reports rather than deriving it from the closure list and the exception set

### Requirement: Availability accounts for closures by every route

Every route that computes availability or validates placement SHALL account for site closures: the
free-time computation, fixed-duration slot projection, bookable-start projection, service
availability over a candidate pool, and placement validation. A resource handed to a pure
projection SHALL already carry the closures applicable to it, so that no caller can obtain a
projection that silently omits them.

**This SHALL be verified through the production entry point** rather than by a test on each side of
the join: a guard over the closure layer and a guard over the projection can both pass while the
two are never connected.

#### Scenario: Slot projection offers nothing on a closed date
- **WHEN** slots are projected over a range containing a closure date
- **THEN** no start is offered on that date, and the surrounding dates are unaffected

#### Scenario: Bookable starts omit a closed date
- **WHEN** bookable starts are projected over a range containing a closure date
- **THEN** no start is offered on that date

#### Scenario: Service availability accounts for closures
- **WHEN** a service's availability is computed over a range containing a closure date and none of its candidate resources has opted out
- **THEN** the service offers no start on that date

#### Scenario: A service whose candidate has opted out still offers the date
- **WHEN** one candidate resource capable of filling every role has opted out of the closure
- **THEN** the service offers starts on that date from that resource

#### Scenario: The pure projection sees closures
- **WHEN** a resource is obtained through the read port and handed to the pure bookable-start projection
- **THEN** the projection accounts for that resource's applicable closures

### Requirement: Closures are not disclosed publicly

The delivery API and the shipped front end SHALL NOT name closures, their labels, or the fact that a
date's unavailability is due to one. A closed date SHALL be indistinguishable from any other date
carrying no availability.

This matches the existing rule that the public resource read model exposes neither the weekly
open-hours pattern nor date exceptions. A closure label is the site's own operational note — "Staff
training", "Stocktake" — and publishing it to anonymous callers discloses more about the
organisation than the booking flow needs.

#### Scenario: The public read model carries no closures
- **WHEN** the public resource read model is inspected
- **THEN** it exposes no closure, no closure label, and no opt-out

#### Scenario: A closed date looks like any unavailable date
- **WHEN** an anonymous caller queries availability over a range containing a closure date
- **THEN** the date carries no availability, with no reason given and nothing distinguishing it from a date the resource is simply not open on

### Requirement: Closures are managed through versioned management endpoints

The Management API SHALL expose versioned endpoints for closures in the `ubookitbackoffice` swagger
group: list, create, update and delete. Request and response bodies SHALL be purpose-built DTO
models — domain types SHALL NOT appear in the HTTP contract, on the same terms as every other
management endpoint.

The list SHALL be filterable to upcoming closures or all closures, so that the view's default is
served by the server rather than by fetching everything and hiding some of it in the client.

Validation failures SHALL carry the stable codes this capability defines and SHALL be rendered as
problem details carrying a type member, on the same terms as the rest of the management surface. A
closure id that does not exist SHALL yield a 404 problem-details response.

#### Scenario: Closures round-trip through the API
- **WHEN** a closure is created and the list is then read
- **THEN** the closure appears with the date and label it was created with, and a stable id

#### Scenario: The list serves the upcoming filter
- **WHEN** the list is requested filtered to upcoming closures on a site holding past and future ones
- **THEN** only the future closures are returned, and the filtering is performed by the server

#### Scenario: A validation failure is problem details
- **WHEN** a closure is submitted with a blank label
- **THEN** the response is a problem-details body carrying a type member and the `closure-label-invalid` code

#### Scenario: An unknown closure id is a 404
- **WHEN** an update or delete names a closure id that does not exist
- **THEN** the response is a 404 problem-details body
