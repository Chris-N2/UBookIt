## ADDED Requirements

### Requirement: No-JavaScript service booking flow
The default front-end SHALL provide a complete booking flow for a **service** that
functions with JavaScript disabled, using only server-rendered pages and standard
form submissions. A visitor SHALL be able to choose a date and a booking length, see
the start times available for that service at that length, select one, provide
contact details, submit, and receive a confirmation.

The flow SHALL obtain the service, its available starts, and its placement from the
Core service-booking and availability ports in-process, and SHALL NOT call the
anonymous delivery API over HTTP to render or submit itself — the same rule the
resource flow follows, for the same reason.

The flow SHALL be renderable from a template given a service id, so a site author can
place it on a page.

A service booking claims **several** resources. The flow SHALL be written for that
case rather than for the single-resource case with extras: nothing in it may assume
exactly one resource, including a single-role service, whose resolved set is a
collection of one.

#### Scenario: A service booking completes without JavaScript
- **WHEN** a visitor with JavaScript disabled chooses an open date, selects an available time, enters a valid name and email, and submits
- **THEN** the service booking is placed and a confirmation is shown

#### Scenario: Choosing a date reveals that service's times
- **WHEN** a visitor chooses a date on which the service can be fulfilled
- **THEN** the page shows the start times available for that service on that date at the selected length

#### Scenario: The times offered are the service's, not one resource's
- **WHEN** a service requires a room and a therapist, and rooms are free at a start where no therapist is
- **THEN** that start is not offered

#### Scenario: A date with no availability says so explicitly
- **WHEN** a visitor chooses a date on which the service has no available starts
- **THEN** the page shows an explicit "no times available" message rather than an empty list

### Requirement: The confirmation reports every resource a service resolved to
The confirmation for a placed service booking SHALL report the booking reference, the
booked interval, the booker's contact details, and **every** resource the service
resolved to, not one of them and not a count.

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

### Requirement: A service that cannot be booked says which kind of cannot
When a service cannot be booked, the flow SHALL distinguish a **transient** refusal —
the times are taken, and another date or time may work — from a **deterministic** one,
where the service as configured can never be fulfilled. Rendering them alike discards
a distinction the domain went to trouble to make.

The two SHALL be told apart by **which question was asked**, and not by the failure
code alone. A placement refusal — whatever its code — is an answer about **one
instant**: `service-unavailable` is raised identically for a structurally impossible
service and for one whose resources merely happen to be busy, so it cannot support a
permanent claim. Only the **configuration-time** check, asked over the resolved
candidate pools rather than over a moment, may establish that a service can never be
fulfilled.

The permanent claim SHALL therefore be made in exactly one place: the flow's
configuration-time refusal, rendered **before any form is offered**. No message
derived from a placement failure may assert it.

A transient refusal SHALL invite the visitor to try another time. A deterministic
refusal SHALL NOT, because it would be inviting them to fail again. A refusal SHALL
NOT be rendered in a shape that contradicts its own wording — a page stating that a
service cannot be booked while offering bookable times for it is the same conflation
arriving from the other direction.

This is the same failure the withholding-resource requirement exists to prevent,
arriving through a third door: a correctly configured thing that yields nothing and
says nothing about why. "No times available" sends a visitor back tomorrow when
tomorrow cannot help, and tells the site owner their opening hours are wrong when
they are not.

#### Scenario: A busy service invites a retry
- **WHEN** every resource able to fulfil a service is booked at the chosen time, but the service is fulfillable in general
- **THEN** the page reports that the time is unavailable and invites the visitor to choose another

#### Scenario: An unfulfillable service does not invite a retry
- **WHEN** a service can never be fulfilled as configured — a role with no eligible resource at all
- **THEN** the page reports that the service is not currently available for booking, and does not invite the visitor to try another time

#### Scenario: The two refusals are distinguishable on the page
- **WHEN** a transient refusal and a deterministic refusal are each rendered
- **THEN** their messages differ, and the difference is carried in text rather than by styling alone

#### Scenario: A bookable service refused at one instant still invites another
- **WHEN** a service that can be fulfilled is refused for the instant submitted, whatever the failure code
- **THEN** the visitor is invited to choose another time, and is not told the service is unavailable for booking

#### Scenario: A refusal is never rendered above the times it denies
- **WHEN** a refusal message is shown on a page that also lists bookable start times for that service
- **THEN** that message invites the visitor to choose one of them, rather than stating that the service cannot be booked

### Requirement: A visitor-facing refusal discloses no configuration detail
A refusal rendered to a visitor SHALL state what it means for them and SHALL NOT
carry the service's configuration: it SHALL NOT name a role, a resource type, a
required capability, a count, or how many eligible resources exist.

The pool-sufficiency diagnostic built for the backoffice answers "which role is short
of what" for the person who can fix it. A visitor cannot fix it, is not owed the
site's staffing, and would be told by any such message how many therapists the
business employs.

This SHALL hold for both the transient and the deterministic refusal, and SHALL NOT
be achieved by suppressing the diagnostic at its source — the backoffice report is
unchanged.

#### Scenario: A deterministic refusal names no role
- **WHEN** a service cannot be fulfilled because a role has no eligible resource
- **THEN** the visitor-facing message names neither the role, the resource type, nor the capability it required

#### Scenario: The backoffice diagnostic is unaffected
- **WHEN** the same misconfigured service is inspected in the backoffice
- **THEN** the shortfall report still names the role and its requirements, exactly as before

### Requirement: A catalogue of bookable things
The default front-end SHALL be able to render a catalogue from which a visitor
chooses what to book, listing the site's services and the resources that permit
direct booking **together**, as one set of choices.

The two kinds SHALL be presented alike. Whether a bookable thing is a service or a
resource is an implementation distinction; a visitor books "a massage" or "meeting
room A" and is not helped by being told which is which. Choosing an entry SHALL lead
to the flow appropriate to its kind.

A resource that withholds permission to be booked on its own SHALL NOT appear.
Listing it would offer a choice that leads only to the statement that it is not
offered on its own.

The catalogue SHALL be composed from the same resource and service reads the rest of
the front end uses, and SHALL NOT introduce a second notion of what is bookable.

#### Scenario: Services and directly-bookable resources are listed together
- **WHEN** a site has two services and one directly-bookable resource
- **THEN** the catalogue offers all three as choices

#### Scenario: A service-only resource is not offered
- **WHEN** a resource withholds permission to be booked on its own
- **THEN** it does not appear in the catalogue

#### Scenario: Choosing an entry reaches the right flow
- **WHEN** a visitor chooses a service from the catalogue and then a resource
- **THEN** each leads to the booking flow for that kind

### Requirement: A site may enter the flow at any point
The front-end SHALL let a site author start a visitor at the catalogue, at a
particular service, or at a particular resource, and SHALL NOT require the catalogue
to be traversed first.

A site offering one service should be able to render its booking flow directly. Making
a visitor choose that service from a list of one is a step that exists only because
the software has more than one shape, which is not the visitor's problem.

Each entry point SHALL be independently renderable and SHALL produce the same flow
from that point on as it would have if reached from the catalogue.

#### Scenario: A site starts at one service
- **WHEN** a site author renders the flow for a particular service
- **THEN** the visitor sees that service's date and length step, with no catalogue

#### Scenario: A site starts at the catalogue
- **WHEN** a site author renders the catalogue
- **THEN** a visitor may choose any bookable thing and continue into its flow

#### Scenario: The entry point does not change what follows
- **WHEN** a service flow is reached directly and by way of the catalogue
- **THEN** the steps, controls and outcomes from that point are the same

### Requirement: Flow state is carried in the URL
Each step of a flow SHALL carry its accumulated choices — what is being booked, the
chosen date, and the chosen length — in the request URL, so that a step is
linkable, bookmarkable, and reachable by the browser's back button without
resubmitting anything.

State SHALL NOT be held in a way that makes going back produce a step inconsistent
with the one shown, which is the failure a server-side wizard state would introduce.

Contact details SHALL NOT appear in a URL. They are entered at the final step and
submitted by POST, and the existing anti-forgery and Post-Redirect-Get rules govern
them unchanged.

#### Scenario: A step is linkable
- **WHEN** a visitor copies the URL of a date-and-length step and opens it again
- **THEN** the same service, date and length are shown

#### Scenario: Going back does not resubmit
- **WHEN** a visitor uses the browser's back button from a later step
- **THEN** the earlier step is shown as it was, and nothing is resubmitted

#### Scenario: Contact details are not in the URL
- **WHEN** a visitor submits their name and email
- **THEN** those values are carried by the POST body and do not appear in any URL

## MODIFIED Requirements

### Requirement: Accessible, semantic markup (WCAG 2.2 AA)
Every flow the default front-end renders — the single-resource flow, the service
flow, and the catalogue — SHALL meet WCAG 2.2 AA and use semantic HTML. Every form
control SHALL have a programmatically associated label. The selectable start times
SHALL be a grouped set of radio controls within a `fieldset` carrying a `legend` that
names the group. Hints and error text SHALL be associated with their controls (for
example via `aria-describedby`). Required inputs SHALL be indicated in text, not by
colour or placeholder alone. Each flow SHALL be fully operable by keyboard. Each page
SHALL remain usable, with a logical reading and focus order, when no author
stylesheet is applied.

This SHALL be **one bar, stated once**, rather than a bar per flow. Accessibility is
a differentiator for this package rather than a checkbox, and per-flow restatements
drift: the flow written second gets the attention, and the guarantee quietly becomes
"whichever flow was reviewed most recently".

Where a choice is offered as a set of related controls — the start times, and the
catalogue when it is rendered as a chooser — it SHALL be a grouped set with a
`legend` naming what is being chosen, on the same terms as the start times.

#### Scenario: Every control is labelled
- **WHEN** a booking form is rendered, in either flow
- **THEN** each input and select has a programmatically associated `label`

#### Scenario: Start times are a labelled radio group
- **WHEN** available start times are rendered, in either flow
- **THEN** they are radio inputs inside a `fieldset` whose `legend` names the group

#### Scenario: Usable without an author stylesheet
- **WHEN** any flow is rendered with no author CSS applied
- **THEN** the content order is logical and every control remains operable and labelled

#### Scenario: The catalogue meets the same bar
- **WHEN** the catalogue is rendered as a set of choices
- **THEN** it is a grouped set of labelled controls with a `legend` naming what is being chosen, operable by keyboard

#### Scenario: The service flow meets the bar the resource flow meets
- **WHEN** the service flow is rendered
- **THEN** it satisfies every clause of this requirement, with no clause holding only for the resource flow
