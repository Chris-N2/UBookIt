# default-frontend Specification

## Purpose

Defines the shipped, dependency-free default front-end: server-rendered, no-JavaScript booking flows exposed as Umbraco ViewComponents. It renders a single-resource flow, a service flow, and a catalogue of the bookable things a site offers — services and directly-bookable resources together — dispatched by a `BookingFlow` view component from whichever entry point a site author chooses, while the original `Booking` component continues to render the single-resource flow when invoked directly with a resource id. It renders availability by calling the Core resource, service and availability ports in-process (never the anonymous delivery API over HTTP), places bookings through the Core booking services behind an anti-forgery-protected same-origin POST, and confirms via Post-Redirect-Get. Accessibility is a first-class requirement: every flow meets WCAG 2.2 AA, uses semantic HTML, and preserves input while reporting failures accessibly. This is the default rendering that any alternative UI (a separate-repo DevExpress front-end, a SPA, a mobile client) may replace by consuming the delivery API instead.

## Requirements

### Requirement: No-JavaScript single-resource booking flow
The default front-end SHALL provide a complete booking flow for a single resource that functions with JavaScript disabled, using only server-rendered pages and standard form submissions. A visitor SHALL be able to choose a date and a booking length, see the available start times for that date and length, select one, provide contact details, submit, and receive a confirmation. The flow SHALL be rendered by an Umbraco ViewComponent invoked with a resource id, so a site author can place it in a template.

#### Scenario: Booking completes without JavaScript
- **WHEN** a visitor with JavaScript disabled chooses an open date, selects an available time, enters a valid name and email, and submits
- **THEN** the booking is placed and a confirmation is shown

#### Scenario: Choosing a date reveals that date's times
- **WHEN** a visitor chooses a date on which the resource is open and has free time
- **THEN** the page shows the available start times for that date at the selected length

#### Scenario: A date with no availability says so explicitly
- **WHEN** a visitor chooses a date on which the resource has no free time
- **THEN** the page shows an explicit "no times available" message rather than an empty list

### Requirement: Visitor-chosen booking length
The default front-end SHALL let a visitor choose the booking length rather than fixing it at the resource's minimum duration. The length control SHALL be presented alongside the date control in the same date-selection step, so that the start times subsequently listed are only those that admit the chosen length and no invalid start-and-length combination can be submitted.

The control SHALL offer only lengths the resource actually permits — the granularity multiples between its minimum and maximum duration — and SHALL default to the resource's minimum duration, so a visitor who does not touch it experiences the previous behaviour. The control SHALL be a labelled native form control operable with the keyboard and without JavaScript.

The chosen length SHALL be submitted with the booking and SHALL be validated server-side irrespective of what the control offered; the control is an affordance, not a trust boundary.

#### Scenario: Choosing a longer length filters the offered times
- **WHEN** a visitor chooses a length of 2 hours on a date whose free time is 09:00–12:00 with 1-hour granularity
- **THEN** the offered start times are only those from which 2 hours can be booked

#### Scenario: The default length preserves previous behaviour
- **WHEN** a visitor chooses a date and submits without altering the length control
- **THEN** the booking is placed for the resource's minimum duration

#### Scenario: The chosen length is booked
- **WHEN** a visitor chooses a 90-minute length, selects an available start, provides valid details, and submits
- **THEN** the placed booking's interval is 90 minutes long

#### Scenario: An unpermitted length is rejected server-side
- **WHEN** a submission carries a length the resource does not permit, regardless of what the form offered
- **THEN** placement is rejected and the form is redrawn with the failure reported, rather than a booking being placed

#### Scenario: The length control works without JavaScript
- **WHEN** a visitor with JavaScript disabled changes the length and submits the date-selection step
- **THEN** the page reloads showing the start times available for that length

### Requirement: An unavailable length explains the longest that is available
When a visitor chooses a length for which no start time is available on the selected date, the page SHALL state explicitly that no times are available for that length and SHALL report the longest length that is available on that date. When no length at all is available on that date, the page SHALL fall back to the existing explicit "no times available" message.

This message SHALL be composed in the front-end's view model from availability data rather than produced by Core, so no presentation string enters the domain.

#### Scenario: A too-long request explains itself
- **WHEN** a visitor chooses a 3-hour length on a date whose longest available run is 90 minutes
- **THEN** the page states that no 3-hour times are available and that the longest available that day is 90 minutes

#### Scenario: A fully unavailable date keeps the existing message
- **WHEN** a visitor chooses a date on which the resource has no free time at all
- **THEN** the page shows the explicit "no times available" message rather than a longest-available figure

### Requirement: Availability rendered from Core in-process
The front-end SHALL obtain the resource and its available start times by calling the Core resource and availability ports directly (in-process), and SHALL NOT call the delivery API over HTTP to render itself. Start times SHALL be displayed as wall-clock times in the site time zone. The value submitted for a selected time SHALL identify the exact instant, so placement does not depend on re-deriving the time from a display string.

#### Scenario: Times display in the site zone
- **WHEN** the site zone is `Europe/London` and a resource is free from 09:00 local
- **THEN** the earliest offered start time is shown as 09:00

#### Scenario: The selected time is submitted as an exact instant
- **WHEN** a visitor selects a start time and submits
- **THEN** the submitted request carries the exact UTC instant of that start, not only its displayed local text

### Requirement: Anti-forgery-protected submission
Booking submission SHALL be a same-origin POST carrying a valid anti-forgery token. A submission with a missing or invalid token SHALL be rejected and SHALL NOT create a booking. Placement SHALL run the Core booking service in-process; the anonymous delivery API SHALL NOT be used for the default form submission.

#### Scenario: Submission without a valid token is rejected
- **WHEN** a booking POST arrives without a valid anti-forgery token
- **THEN** it is rejected and no booking is created

#### Scenario: Valid submission places via Core
- **WHEN** a booking POST arrives with a valid token and a valid, available selection
- **THEN** the booking is placed through the Core booking service

### Requirement: Post-Redirect-Get confirmation
A successful placement SHALL respond with a redirect (HTTP 303) to a confirmation page, not by rendering the POST result directly. The confirmation SHALL show the booking reference (its id) and the booked resource, time, and booker contact details. Reloading or refreshing the confirmation page SHALL NOT create another booking.

#### Scenario: Success redirects to confirmation
- **WHEN** a placement succeeds
- **THEN** the response is a 303 redirect to a confirmation page showing the booking reference and details

#### Scenario: Refreshing the confirmation does not re-submit
- **WHEN** a visitor refreshes the confirmation page after a successful booking
- **THEN** no additional booking is created

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

### Requirement: Accessible failure handling with input preservation
When a submission fails validation or placement, the form SHALL be redrawn with an error summary that lists each problem in text and is associated with the offending fields, and the visitor's entered contact details SHALL be preserved. Stable domain failure codes SHALL be mapped to user-facing messages. A time that became unavailable between rendering and submission (a `conflict`) SHALL produce a clear "no longer available" message with refreshed availability, not a raw error.

#### Scenario: Validation failure redraws accessibly and preserves input
- **WHEN** a visitor submits with a missing email
- **THEN** the form is redrawn with an error summary identifying the email field and the previously entered name is preserved

#### Scenario: A now-unavailable time is reported clearly
- **WHEN** the selected time was taken by another booking between page load and submission
- **THEN** the visitor sees a clear "no longer available" message and refreshed availability, and no booking is created

### Requirement: A resource not offered on its own is reported as such
The no-JavaScript booking flow, rendered for a resource that withholds permission
to be booked on its own, SHALL state that the resource is not offered for booking
by itself, and SHALL NOT report that it has no available times.

The two are different facts with different remedies, and they are indistinguishable
to a visitor once rendered the same way. "No times available" invites someone to
come back tomorrow, which will not help, and tells the site owner their opening
hours are wrong, which they are not. This is the failure mode the start-grid and
pool-sufficiency reports exist to prevent, arriving through a new door: a correctly
configured thing that yields nothing and says nothing about why.

The flow SHALL NOT offer a submission for such a resource. Rendering a form that
placement will always refuse would invite a visitor to fill it in and lose their
input to a failure that was knowable before they started.

The statement SHALL meet the same accessibility baseline as the rest of the flow —
semantic markup, no meaning carried by colour alone, and announced as the page's
other outcome messages are.

The flow for a resource that does permit direct booking SHALL be entirely unchanged,
including its length choice, its anti-forgery protection, its Post-Redirect-Get
confirmation, and its failure handling with input preservation.

#### Scenario: A withholding resource explains itself
- **WHEN** the booking flow is rendered for a resource that withholds direct booking
- **THEN** it states that the resource is not offered for booking on its own, and does not state that no times are available

#### Scenario: No form is offered
- **WHEN** the booking flow is rendered for a resource that withholds direct booking
- **THEN** no booking submission is presented

#### Scenario: A genuinely empty calendar still says so
- **WHEN** the booking flow is rendered for a resource that permits direct booking but has no bookable times in range
- **THEN** it reports that there are no available times, as it did before

#### Scenario: The permitted flow is unchanged
- **WHEN** the booking flow is rendered and submitted for a resource that permits direct booking
- **THEN** it behaves exactly as it did before this change, through to the redirect-and-confirm

#### Scenario: The statement is accessible
- **WHEN** the statement is rendered
- **THEN** it is semantic text, not conveyed by colour alone, and announced on the same terms as the flow's other outcome messages

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

### Requirement: A visitor may choose who fulfils a service
Where a service has a visitor-selectable role, the service flow SHALL offer a control
for choosing which resource fills it, and SHALL offer **no such control** for a
service that has none. The control SHALL default to expressing no choice — "any" —
and choosing nothing SHALL leave the flow behaving exactly as it does today.

The control SHALL sit in the same step as the date and the length, and the start times
shown SHALL be those at which the chosen resource can actually be assigned. Choosing
who before when SHALL NOT add a step: the choice joins an existing submission rather
than introducing a page between the times and the contact details.

The choices SHALL be the resources resolved for that role — every resource whose type,
capabilities and duration range let it fulfil the service — listed by display name in
a stable order. The list SHALL NOT be filtered by the chosen date. A resource with no
free time on the selected date SHALL still be offered, and the start list SHALL then
report that there are no times, because filtering the people by date would make the
control's contents change under the visitor as they change the date, and would answer
with the control a question the times already answer.

Where the selectable role has a count greater than one, the control SHALL state that
the visitor is choosing **one** of that many resources and that the remainder are
assigned. A control that implies a choice and then books resources the visitor never
chose is a substitution, however friendly its wording.

The chosen resource SHALL be carried through to placement as the pinned resource, so
the booking that is placed is the booking that was offered. It SHALL NOT be treated as
a preference: no other resource SHALL be substituted for it.

#### Scenario: A service offering a choice shows the control
- **WHEN** a visitor opens the flow for a service whose therapist role is visitor-selectable
- **THEN** a labelled control offers each eligible therapist by name, defaulting to no particular choice

#### Scenario: A service offering no choice shows no control
- **WHEN** a visitor opens the flow for a service with no visitor-selectable role
- **THEN** no such control is rendered and the flow is exactly as it was

#### Scenario: Choosing a person narrows the times
- **WHEN** a visitor chooses a therapist who is booked all morning
- **THEN** the start times shown are only those at which that therapist can be assigned

#### Scenario: Choosing nobody offers the service's own times
- **WHEN** a visitor leaves the choice at "any"
- **THEN** the start times shown are the service's, as they are for a service offering no choice

#### Scenario: The choice is honoured at placement
- **WHEN** a visitor chooses a therapist, selects an offered time, and submits valid details
- **THEN** the booking placed claims that therapist, and the confirmation reports them

#### Scenario: A person with no times that day is still offered
- **WHEN** a visitor selects a date on which one eligible therapist is fully booked
- **THEN** that therapist still appears in the control, and choosing them shows the no-times message

#### Scenario: Choosing one of several says so
- **WHEN** a service's selectable role has a count of 2
- **THEN** the control states that the visitor chooses one of the two and the other is assigned

#### Scenario: No control can pin a resource for a non-selectable role
- **WHEN** the rendered flow for a service with no visitor-selectable role is inspected
- **THEN** it contains no control, hidden field, or other means by which a pinned resource could be submitted

### Requirement: A refused choice is distinguished from no availability
Where a visitor's chosen resource cannot fulfil the booking at the time they chose,
the flow SHALL say **that**, and SHALL NOT report it as the service having no
availability. The two are different facts with different next steps: one is answered
by choosing another time or another person, the other by choosing another date.

The message SHALL name the resource the visitor chose, because they chose it, and
SHALL offer the way forward — the times that choice can be honoured, or the option of
letting the service assign anyone.

Where the chosen resource is no longer able to fulfil the service at all — deleted,
no longer eligible, or its role no longer selectable — a request carrying it SHALL
NOT silently fall back to assigning someone else. Before anything is submitted the
flow SHALL reset the choice to "any" **and say that it has**, so a stale link does
not quietly become a different booking; on submission the placement failure for an
ineligible resource SHALL be reported rather than absorbed.

#### Scenario: A busy choice is reported as such
- **WHEN** a visitor's chosen therapist is claimed at the submitted time while other therapists are free
- **THEN** the page says that person is not available at that time, names them, and does not say the service has no availability

#### Scenario: A refused choice is not silently substituted
- **WHEN** a submission pinning a therapist cannot be honoured
- **THEN** no booking is placed with a different therapist

#### Scenario: A stale choice in a link resets and says so
- **WHEN** a visitor opens a link whose chosen resource no longer fulfils the service
- **THEN** the form is shown with the choice reset to "any" and a message saying the previous choice is no longer offered

#### Scenario: An ineligible choice at submission is reported
- **WHEN** a submission carries a resource that cannot fulfil the service
- **THEN** the failure is reported to the visitor rather than absorbed into a booking

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

A refusal concerning a resource the visitor **themselves chose** MAY name that
resource, and nothing more. Naming back a choice the visitor made discloses none of
the five facts above: not which role it fills, not what the role requires, not how
many others exist. This is a narrow permission and SHALL NOT be read as licence to
name resources a visitor did not choose — a deterministic refusal names none, whether
or not the service offers a choice.

Where a site has turned on a visitor-selectable role, the pool of that role is
disclosed by the picker as the site owner's deliberate configuration. That SHALL NOT
change what a **refusal** may say: the picker publishing a list is a choice the site
made, and a refusal remains bound by this requirement whether or not a picker is
rendered.

#### Scenario: A deterministic refusal names no role
- **WHEN** a service cannot be fulfilled because a role has no eligible resource
- **THEN** the visitor-facing message names neither the role, the resource type, nor the capability it required

#### Scenario: The backoffice diagnostic is unaffected
- **WHEN** the same misconfigured service is inspected in the backoffice
- **THEN** the shortfall report still names the role and its requirements, exactly as before

#### Scenario: A refused choice names only what the visitor chose
- **WHEN** a visitor's chosen resource cannot be assigned at the submitted time
- **THEN** the message names that resource and discloses no role, type, capability, count, or pool size

#### Scenario: A deterministic refusal names nothing even where a choice was offered
- **WHEN** a service with a visitor-selectable role becomes permanently unfulfillable
- **THEN** the refusal names no resource, including any the visitor had chosen, and no configuration detail

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
chosen date, the chosen length, and the chosen resource where one was offered — in the
request URL, so that a step is linkable, bookmarkable, and reachable by the browser's
back button without resubmitting anything.

State SHALL NOT be held in a way that makes going back produce a step inconsistent
with the one shown, which is the failure a server-side wizard state would introduce.

Contact details SHALL NOT appear in a URL. They are entered at the final step and
submitted by POST, and the existing anti-forgery and Post-Redirect-Get rules govern
them unchanged.

A resource identifier in the URL discloses nothing a public read does not already
carry, and a link naming a resource that no longer fulfils the service SHALL be
handled as a stale choice rather than as an error.

#### Scenario: A step is linkable
- **WHEN** a visitor copies the URL of a date-and-length step and opens it again
- **THEN** the same service, date and length are shown

#### Scenario: A chosen resource is linkable
- **WHEN** a visitor chooses a therapist, copies the URL, and opens it again
- **THEN** the same service, date, length and therapist are shown

#### Scenario: Going back does not resubmit
- **WHEN** a visitor uses the browser's back button from a later step
- **THEN** the earlier step is shown as it was, and nothing is resubmitted

#### Scenario: Contact details are not in the URL
- **WHEN** a visitor submits their name and email
- **THEN** those values are carried by the POST body and do not appear in any URL
