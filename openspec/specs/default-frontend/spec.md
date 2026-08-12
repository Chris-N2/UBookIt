# default-frontend Specification

## Purpose

Defines the shipped, dependency-free default front-end: a server-rendered, no-JavaScript single-resource booking flow exposed as an Umbraco ViewComponent. It renders availability by calling the Core resource and availability ports in-process (never the anonymous delivery API over HTTP), places bookings through the Core booking service behind an anti-forgery-protected same-origin POST, and confirms via Post-Redirect-Get. Accessibility is a first-class requirement: the flow meets WCAG 2.2 AA, uses semantic HTML, and preserves input while reporting failures accessibly. This is the default rendering that any alternative UI (a separate-repo DevExpress front-end, a SPA, a mobile client) may replace by consuming the delivery API instead.

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
The rendered flow SHALL meet WCAG 2.2 AA and use semantic HTML. Every form control SHALL have a programmatically associated label. The selectable start times SHALL be a grouped set of radio controls within a `fieldset` carrying a `legend` that names the group. Hints and error text SHALL be associated with their controls (for example via `aria-describedby`). Required inputs SHALL be indicated in text, not by colour or placeholder alone. The flow SHALL be fully operable by keyboard. The page SHALL remain usable, with a logical reading and focus order, when no author stylesheet is applied.

#### Scenario: Every control is labelled
- **WHEN** the booking form is rendered
- **THEN** each input and select has a programmatically associated `label`

#### Scenario: Start times are a labelled radio group
- **WHEN** available start times are rendered
- **THEN** they are radio inputs inside a `fieldset` whose `legend` names the group

#### Scenario: Usable without an author stylesheet
- **WHEN** the flow is rendered with no author CSS applied
- **THEN** the content order is logical and every control remains operable and labelled

### Requirement: Accessible failure handling with input preservation
When a submission fails validation or placement, the form SHALL be redrawn with an error summary that lists each problem in text and is associated with the offending fields, and the visitor's entered contact details SHALL be preserved. Stable domain failure codes SHALL be mapped to user-facing messages. A time that became unavailable between rendering and submission (a `conflict`) SHALL produce a clear "no longer available" message with refreshed availability, not a raw error.

#### Scenario: Validation failure redraws accessibly and preserves input
- **WHEN** a visitor submits with a missing email
- **THEN** the form is redrawn with an error summary identifying the email field and the previously entered name is preserved

#### Scenario: A now-unavailable time is reported clearly
- **WHEN** the selected time was taken by another booking between page load and submission
- **THEN** the visitor sees a clear "no longer available" message and refreshed availability, and no booking is created
