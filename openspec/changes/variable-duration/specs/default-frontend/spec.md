## ADDED Requirements

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

## MODIFIED Requirements

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
