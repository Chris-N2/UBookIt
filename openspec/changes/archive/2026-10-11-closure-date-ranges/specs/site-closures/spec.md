## MODIFIED Requirements

### Requirement: Closures are not disclosed publicly

The delivery API and the shipped front end SHALL NOT name closures, their labels, or the fact that a
date's unavailability is due to one. A closed date SHALL be indistinguishable from any other date
carrying no availability, **with one stated exception: where the booking page's date window starts
after today because a closure or closed period covers today, a visitor can infer that the dates
before the window's start are closed rather than merely booked.** This is the price of not
answering a visitor with "nothing in the next few weeks" for the length of a closure, and it is
paid knowingly. The shifted window SHALL still say nothing about which closure, what it is called,
why, or for what reason any individual date in the window is unavailable. Every date INSIDE the
window remains indistinguishable as before.

This matches the existing rule that the public resource read model exposes neither the weekly
open-hours pattern nor date exceptions. A closure label is the site's own operational note — "Staff
training", "Stocktake" — and publishing it to anonymous callers discloses more about the
organisation than the booking flow needs. The same holds for a resource's closed-period note, which
may concern a person.

#### Scenario: The public read model carries no closures
- **WHEN** the public resource read model is inspected
- **THEN** it exposes no closure, no closure label, no closed period, no note, and no opt-out

#### Scenario: A closed date looks like any unavailable date
- **WHEN** an anonymous caller queries availability over a range containing a closure date
- **THEN** the date carries no availability, with no reason given and nothing distinguishing it from a date the resource is simply not open on

#### Scenario: A shifted window names dates, never a reason
- **WHEN** the booking page's window has moved past a closure covering today
- **THEN** the page names the dates the window spans, and nothing it renders names the closure, its label, a closed-period note, or why the window moved


### Requirement: The site closure list

The package SHALL hold a site-level list of closures. Each closure SHALL carry a **start date, an
end date and a label**, and SHALL be identified by a stable id that does not change when any of
them is edited. **A closure covers every date from its start to its end, both inclusive.** A
closure of a single day is one whose start and end are the same date; it is not a separate kind of
closure.

**The end SHALL NOT be before the start**, rejected with the stable code `closure-range-invalid`.

**The label SHALL be required**, non-empty after trimming, and SHALL be rejected with the stable
code `closure-label-invalid` when it is absent, blank, or longer than the package's name-column
length. A label is what makes an inherited closure legible where it is inherited: a resource editor
offering an opt-out for a bare date asks an operator to consent to something they cannot identify.

**No date SHALL be covered by more than one closure.** A closure whose range shares any date with
another closure's SHALL be refused with the stable code `duplicate-closure-date`, whose meaning is
"another closure already covers a date in this range". A second closure on a date could only say
the same thing twice or contradict itself, and an operator who wants a longer closure extends the
existing one. **Editing a closure SHALL NOT be refused for overlapping its own previous range.**

A closure SHALL be **full-day** on every date it covers. Partial-day closing remains the per-resource
exception's job, and SHALL NOT be expressible here.

Closures SHALL be returned in a deterministic order, by start date.

#### Scenario: A closure is created with a date and a label
- **WHEN** a closure is created for 2026-12-25 to 2026-12-25 with the label "Christmas Day"
- **THEN** it is stored, carries a stable id, and is returned by the closure list

#### Scenario: A closure is created over a range
- **WHEN** a closure is created from 2027-02-01 to 2027-03-31 with the label "Refurbishment"
- **THEN** it is stored as one closure with one id, covering every date from 1 February to 31 March inclusive

#### Scenario: An inverted range is refused
- **WHEN** a closure is submitted whose end date is before its start date
- **THEN** the write fails with the code `closure-range-invalid` and nothing is stored

#### Scenario: A closure without a label is refused
- **WHEN** a closure is submitted with a blank or whitespace-only label
- **THEN** the write fails with the code `closure-label-invalid` and nothing is stored

#### Scenario: An over-long label is refused as validation, not as a storage error
- **WHEN** a closure is submitted with a label longer than the permitted length
- **THEN** the write fails with the code `closure-label-invalid`, and no database error is surfaced

#### Scenario: A second closure on the same date is refused
- **WHEN** a closure is created for a date that already carries one
- **THEN** the write fails with the code `duplicate-closure-date` and the existing closure is unchanged

#### Scenario: A closure overlapping another's range is refused
- **WHEN** a closure exists from 2027-02-01 to 2027-03-31 and another is submitted from 2027-03-25 to 2027-04-05
- **THEN** the write fails with the code `duplicate-closure-date` and the existing closure is unchanged

#### Scenario: Extending a closure is not an overlap with itself
- **WHEN** a closure from 2027-02-01 to 2027-03-31 is edited to end on 2027-04-30, and no other closure covers April
- **THEN** the edit is stored

#### Scenario: Editing a closure preserves its identity
- **WHEN** a closure's dates and label are all changed
- **THEN** its id is unchanged

#### Scenario: Listing is deterministically ordered
- **WHEN** the closure list is read twice against unchanged data
- **THEN** both reads present the same closures in the same order

### Requirement: A closure closes every resource for its date

A site closure SHALL close every resource **for every date it covers**, taking precedence over the
weekly open-hours pattern, any date exception the resource carries of its own, and any closed
period the resource carries of its own. A resource that has not opted out SHALL have no available
time on a date a closure covers, whatever its own configuration says.

**The resource's own exception is not silently discarded — it is superseded for that date and
resumes its full effect if the closure is removed, shortened past it, or opted out of.** The
closure layer SHALL be held separately from the resource's own exceptions and closed periods and
SHALL NOT be written into them, so that no save of a resource can convert an inherited closure into
a date the resource owns.

A resource created after a closure exists SHALL inherit it on the same terms as any other, without
anything being copied onto it at creation.

#### Scenario: A closure closes an otherwise-open resource
- **WHEN** a resource open Friday 09:00–17:00 is queried for a Friday carrying a site closure
- **THEN** the availability projection for that date offers no free time

#### Scenario: Every date in a range is closed
- **WHEN** a resource open every weekday is queried across a closure from Monday to the Friday of the following week
- **THEN** no date from that Monday to that Friday offers free time, and the days either side of the range are unaffected

#### Scenario: A closure supersedes the resource's own override exception
- **WHEN** a resource carries an override exception of 10:00–14:00 on a date that also carries a site closure, and has not opted out
- **THEN** the availability projection for that date offers no free time

#### Scenario: The superseded exception is not destroyed
- **WHEN** the site closure on that date is deleted
- **THEN** the resource's own 10:00–14:00 override exception takes effect again, without being re-entered

#### Scenario: Shortening a closure releases the dates it no longer covers
- **WHEN** a closure covering a date on which a resource carries an override exception is edited so that it no longer covers that date
- **THEN** the override exception takes effect on that date again, and the dates still covered stay closed

#### Scenario: Saving a resource does not absorb the closure
- **WHEN** a resource inheriting a site closure is loaded, saved unchanged, and the closure is then deleted
- **THEN** the resource has no exception and no closed period for any date the closure covered, and those dates resolve from its weekly pattern

#### Scenario: A newly created resource inherits existing closures
- **WHEN** a resource is created while closures already exist
- **THEN** it is closed on every date they cover, without any exception, closed period or opt-out having been written for it

### Requirement: A resource opts out of an individual closure by id

A resource SHALL be able to opt out of any individual closure. An opt-out SHALL name the **closure
id**, not a date, so that editing a closure's dates carries the opt-out with it rather than
stranding it on dates nobody chose. **An opt-out SHALL cover every date the closure covers**; there
SHALL be no opting out of part of a closure.

**Opting out SHALL mean the closure does not reach that resource at all.** Each date it covers SHALL
then resolve exactly as it would if the closure did not exist: the resource's own closed period or
date exception where it has one, otherwise its weekly pattern. There SHALL be no third outcome
peculiar to opted-out dates.

Opt-outs SHALL be per resource and SHALL NOT affect any other resource. A write of a resource SHALL
replace its opt-out set wholesale rather than merging into it, consistent with the full-replacement
semantics of the rest of the resource contract. An opt-out naming a closure that does not exist
SHALL be refused with the stable code `closure-not-found`.

#### Scenario: Opting out restores the weekly pattern
- **WHEN** a resource open 09:00–17:00 opts out of a closure on one of its open days
- **THEN** the availability projection for that date offers 09:00–17:00

#### Scenario: An opt-out covers the whole range
- **WHEN** a resource open every weekday opts out of a closure covering two weeks
- **THEN** every open day in those two weeks offers its weekly hours for that resource

#### Scenario: Opting out restores the resource's own exception
- **WHEN** a resource carrying an override exception of 10:00–14:00 on a closure date opts out of that closure
- **THEN** the availability projection for that date offers 10:00–14:00, and not the weekly pattern

#### Scenario: Opting out reveals the resource's own closed period
- **WHEN** a resource with its own closed period inside a site closure's range opts out of that closure
- **THEN** the dates of its closed period remain closed, by the resource's own rule, and its other dates in the closure's range resolve from its weekly pattern

#### Scenario: Opting out of a closure the resource would close anyway changes nothing
- **WHEN** a resource whose own exception for that date is itself a closure opts out of the site closure
- **THEN** the date remains closed, by the resource's own rule

#### Scenario: An opt-out reaches only its own resource
- **WHEN** one resource opts out of a closure
- **THEN** every other resource remains closed on every date it covers

#### Scenario: Editing a closure's date carries opt-outs with it
- **WHEN** a closure a resource has opted out of is moved to different dates
- **THEN** that resource is open on the new dates on the same terms, and the old dates resolve normally for every resource

#### Scenario: An update replaces the opt-out set
- **WHEN** a resource holding two opt-outs is updated with a body naming only one
- **THEN** it holds only that one, and is closed again on every date the other covers

#### Scenario: An opt-out naming no closure is refused
- **WHEN** a resource is saved with an opt-out naming an id no closure carries
- **THEN** the write fails with the code `closure-not-found` and the resource is unchanged

#### Scenario: Deleting a closure removes its opt-outs
- **WHEN** a closure that resources have opted out of is deleted
- **THEN** the closure and its opt-outs are gone, and no resource retains a reference to it

### Requirement: The closures view

The package SHALL present closures in a backoffice view of their own within the uBookIt section,
listing each closure's **range** and label with actions to create, edit and delete. A closure of one
day SHALL be shown as that date rather than as a range from a date to itself. **Creating and editing
SHALL take a From date and a To date**, each a labelled date input; To SHALL default to From, so
that closing a single day asks for one date. **Where the site has registered a public holiday source
and the user holds the verb that reaches it, the view SHALL also carry the import control**; where
either is missing, it SHALL carry no such control — the enumeration of the view's actions is
therefore conditional on the site, not fixed.

**The view SHALL show upcoming closures by default and SHALL offer past ones on request.** A closure
is upcoming while **its end date** is today or later, so a closure in progress is upcoming. A past
closure is the record of why dates were shut and SHALL NOT be deleted automatically; the default
filter keeps a list that grows every year usable without destroying anything.

The view's markup SHALL meet the project's accessibility bar on the same terms as the rest of the
section: every input labelled, failures programmatically associated with the control they concern
and announced on failed save, full keyboard operability with visible focus, and semantic headings
and controls.

#### Scenario: Upcoming closures are shown by default
- **WHEN** the closures view is opened on a site holding both past and future closures
- **THEN** the future closures are listed, and the past ones are not, until they are asked for

#### Scenario: A closure in progress is upcoming
- **WHEN** the closures view is opened on a date inside a closure's range
- **THEN** that closure is listed by default

#### Scenario: Past closures remain available
- **WHEN** past closures are requested
- **THEN** they are listed, and nothing has been deleted

#### Scenario: A range is created with From and To
- **WHEN** an operator enters a From date, a later To date and a label, and saves
- **THEN** one closure covering that range is created and listed with its range

#### Scenario: A single day needs one date
- **WHEN** an operator enters a From date and a label and leaves To at its default
- **THEN** a closure of that one day is created and listed as that date

#### Scenario: Keyboard-only management
- **WHEN** a user operates the closures view using only a keyboard
- **THEN** creating, editing, deleting and revealing past closures are all reachable and operable with visible focus, as are the import control and every row's selection where the import is present

#### Scenario: A failed save is announced
- **WHEN** a closure save fails validation, including an inverted range or an overlap with another closure
- **THEN** the failure is announced to assistive technology and associated with the control it concerns, and no entered data is lost

#### Scenario: The import control is part of the view's accessibility bar
- **WHEN** an import preview is displayed and operated using only a keyboard
- **THEN** the window inputs, the fetch, every selectable row and the confirm are labelled, reachable and operable with visible focus, on the same terms as the rest of the view

### Requirement: Inheritance is visible where a resource is edited

The resource editor SHALL show every site closure the resource inherits, with a per-closure control
opting this resource out. Each entry SHALL show the closure's **range** and its label, so that the
operator is exempting something they can identify.

**Where a resource carries an override exception on a date covered by a closure it has not opted out
of, the editor SHALL state that the exception is currently superseded.** The statement SHALL be
programmatically associated with the exception it concerns.

**The statement SHALL be made only where the outcome differs.** A resource whose own exception for
that date is itself a closure is closed either way, so describing it as superseded would report a
difference that does not exist. This distinction SHALL be decided by the server and presented to
the client, so that the precedence rule has exactly one implementation.

#### Scenario: Inherited closures are listed with their labels
- **WHEN** a resource is opened for editing on a site holding closures
- **THEN** each closure is listed with its range and label, and with a control opting this resource out

#### Scenario: A superseded override exception is marked
- **WHEN** a resource carrying an override exception on a date inside a non-opted-out closure's range is opened
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

### Requirement: Closures are managed through versioned management endpoints

The Management API SHALL expose versioned endpoints for closures in the `ubookitbackoffice` swagger
group: list, create, update, delete, **a preview of what a registered holiday source offers for a
window, an import creating closures from chosen rows, and a probe reporting whether a source is
registered at all**. Request and response bodies SHALL be purpose-built DTO
models — domain types SHALL NOT appear in the HTTP contract, on the same terms as every other
management endpoint.

**A closure body SHALL carry `date` as its start and MAY carry `endDate`.** A body without `endDate`
SHALL mean a closure of that one day, so that a caller written before ranges existed creates and
updates exactly what it did before. **Every closure in a response SHALL carry both `date` and
`endDate`**, equal for a single day. The addition is additive: no field is removed or renamed, and
no value an existing caller sent changes meaning.

The list SHALL be filterable to upcoming closures or all closures, so that the view's default is
served by the server rather than by fetching everything and hiding some of it in the client. A
closure SHALL count as upcoming while its end date is today or later.

Validation failures SHALL carry the stable codes this capability defines and SHALL be rendered as
problem details carrying a type member, on the same terms as the rest of the management surface. A
closure id that does not exist SHALL yield a 404 problem-details response, **and so SHALL a preview
or import on a site that registered no source** — every 404 this capability returns is problem
details, so a client never has to tell one shape of absence from another.

#### Scenario: Closures round-trip through the API
- **WHEN** a closure is created and the list is then read
- **THEN** the closure appears with the dates and label it was created with, and a stable id

#### Scenario: A body without an end date is a single day
- **WHEN** a closure is created with `date` 2026-12-25 and no `endDate`
- **THEN** the response and the list carry `date` and `endDate` both 2026-12-25

#### Scenario: Updating without an end date makes a single day
- **WHEN** a closure covering a range is updated with a body carrying `date` and no `endDate`
- **THEN** the closure covers that one date

#### Scenario: The list serves the upcoming filter
- **WHEN** the list is requested filtered to upcoming closures on a site holding past and future ones
- **THEN** only the closures whose end date is today or later are returned, including one in progress, and the filtering is performed by the server

#### Scenario: A validation failure is problem details
- **WHEN** a closure is submitted with a blank label
- **THEN** the response is a problem-details body carrying a type member and the `closure-label-invalid` code

#### Scenario: An overlap is problem details
- **WHEN** a closure is submitted whose range overlaps another closure's
- **THEN** the response is a problem-details body carrying a type member and the `duplicate-closure-date` code

#### Scenario: An unknown closure id is a 404
- **WHEN** an update or delete names a closure id that does not exist
- **THEN** the response is a 404 problem-details body

#### Scenario: The import endpoints are absent without a source
- **WHEN** a preview or an import is requested on a site that has registered no holiday source
- **THEN** each responds 404 with a problem-details body, and no closure is created

#### Scenario: Absence is reported behind the verb, not in front of it
- **WHEN** any of the three holiday endpoints is called without backoffice authentication
- **THEN** the response is 401, whether or not a source is registered, because authentication precedes the question


## ADDED Requirements

### Requirement: A range costs only the dates a question asks about

Expanding a closure, or a resource's closed period, into the dates it covers SHALL be bounded by the
dates the question being answered spans. **A range's length SHALL NOT affect the cost of any
availability read, placement or listing.** A closure running for years SHALL cost an availability
read over one week the same as a closure of one day inside that week.

Every route named by *Availability accounts for closures by every route* SHALL treat each date a
range covers exactly as it treats a single-day closure on that date.

#### Scenario: A long range does not cost a short query
- **WHEN** availability is read for one week on a site holding a closure that runs for ten years across that week
- **THEN** the answer treats that week as closed, and the work done does not grow with the closure's length

#### Scenario: Placement inside a range is refused
- **WHEN** placement is requested for a resource that has not opted out, on the middle date of a closure's range
- **THEN** the placement is refused as outside open hours

#### Scenario: A range reaching past the query is clipped, not ignored
- **WHEN** availability is read from 2027-03-25 to 2027-04-05 and a closure runs from 2027-02-01 to 2027-03-31
- **THEN** 25 to 31 March are closed and 1 to 5 April resolve normally
