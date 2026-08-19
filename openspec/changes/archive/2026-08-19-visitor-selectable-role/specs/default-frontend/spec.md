## ADDED Requirements

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

## MODIFIED Requirements

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
