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

**The date is chosen from the dates that actually have availability.** The step SHALL list the
dates within a bounded window that can be booked at the chosen length, so a visitor selects a
date knowing it is bookable rather than discovering afterwards that it is not. What the window
is, and how a date outside it is reached, are stated in the requirements that follow — they are
the same for every flow the package ships and are stated once rather than per flow.

#### Scenario: The step lists dates that have availability
- **WHEN** a visitor reaches the step that chooses a date, and the resource has availability within the window at the chosen length
- **THEN** those dates are offered as a choice, and a date with no availability at that length is not among them

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

**The date is chosen from the dates that actually have availability**, on the same terms as the
single-resource flow: the step lists the bookable dates within the window at the chosen length,
narrowed by the visitor's choice of who where one has been made.

#### Scenario: The service step lists dates that have availability
- **WHEN** a visitor reaches the step that chooses a date for a service, and the service can be fulfilled within the window at the chosen length
- **THEN** those dates are offered as a choice, and a date the service cannot be fulfilled on at that length is not among them

## ADDED Requirements

### Requirement: The listed window is derived from the site's own bounds, never fixed

The window of dates offered SHALL be derived from the subject's **lead time**, its **horizon**,
and the site's **maximum query range**, and SHALL NOT be a fixed number of days the code assumes
it may read.

- No date before the lead time allows SHALL be listed. Offering a date the domain will refuse is
  the guessing game this requirement exists to end, reintroduced one step earlier.
- No date beyond the horizon SHALL be listed.
- **The window SHALL NOT exceed the configured maximum query range.** A site may set that
  guardrail below the package's preferred window, and a read wider than it is **refused** — so a
  fixed window would not merely list too much, it would fail the availability read on every
  render and leave that site with no flow at all.

**A site whose bounds are narrower than the preferred window SHALL get a shorter list, never an
error and never an empty step.** This is the failure that a default configuration cannot show:
every bound above is generous by default, so a window that ignores them looks correct until
somebody tightens one.

#### Scenario: A tight query-range guardrail shortens the list
- **WHEN** the site's maximum query range is configured below the package's preferred window and a visitor reaches the date step
- **THEN** the dates listed span no more than that maximum, the availability read succeeds, and the step renders normally

#### Scenario: A short horizon shortens the list
- **WHEN** a resource's horizon is shorter than the preferred window
- **THEN** no date beyond the horizon is listed

#### Scenario: Lead time is respected in the list
- **WHEN** a resource has a lead time
- **THEN** no date earlier than the lead time allows is listed, whatever availability those days hold

### Requirement: A listed date and the times for that date agree

The dates listed and the start times shown for the selected date SHALL be derived from **one
reading of availability**, so that the two cannot disagree about the same day.

**The times shown SHALL be exactly those a read of that single date would produce.** Widening the
read must change how much is asked for and nothing about what is answered; a start that would
have been offered before SHALL be offered still, and none SHALL be added.

Two separate reads would make a page that lists a date as bookable while showing no times for it
an ordinary outcome of a booking landing between them, rather than a defect. One reading cannot
contradict itself.

#### Scenario: The times are unchanged by the wider read
- **WHEN** the start times for a date are produced from the window and from a read of that date alone, for the same subject and length
- **THEN** they are the same times

#### Scenario: A listed date has times
- **WHEN** a visitor selects a date the step listed as available, without the stored state changing
- **THEN** start times are shown for it

### Requirement: A date beyond the listed window is still reachable

The step SHALL provide a way to choose a date outside the listed window, for any date the
subject's own bounds allow.

**Because the window is smaller than what a site offers.** A horizon is commonly months and a
window is at most weeks, so a step offering only the list would put most of a site's own
availability out of reach — a change to booking policy wearing the clothes of a change to layout.

**The two controls SHALL NOT submit the same parameter, and which one wins SHALL be defined.** A
form submitting one parameter from two controls sends both values, and which is bound is an
accident rather than a decision.

**Where the chosen date lies outside the listed window, the step SHALL state which date it is
showing.** Otherwise a page presents a list with nothing selected beside times for a date the
list does not contain, and contradicts itself.

#### Scenario: A date beyond the window can be chosen
- **WHEN** a visitor chooses a date later than the listed window but within the subject's horizon
- **THEN** the flow shows that date's start times

#### Scenario: The controls do not collide
- **WHEN** the date step's controls are inspected
- **THEN** the list and the means of choosing another date submit different parameters, and the precedence between them is defined rather than left to binding order

#### Scenario: A date outside the window is named
- **WHEN** the selected date is not among those listed
- **THEN** the step states which date it is showing

### Requirement: A window with no availability explains itself

Where no date in the listed window has availability at the chosen length, the step SHALL say so,
and SHALL say what would change the answer.

**It is a different statement from an empty day.** *"No times are available on Tuesday"* tells a
visitor to try another date; *"no date in the next few weeks can take two hours"* tells them
something about every date, and leaving them to discover that one day at a time is the failure
this whole change removes.

**Where the length is the reason, that SHALL be said.** A window with nothing at two hours may be
full of half-hour gaps, and a visitor told only that there is nothing has no next move — while a
visitor told the length is the obstacle has two.

#### Scenario: An empty window is stated once, not discovered daily
- **WHEN** no date in the listed window has availability at the chosen length
- **THEN** the step states that no date in the window is available at that length, rather than rendering an empty list

#### Scenario: The empty window names a way forward
- **WHEN** the window is empty at the chosen length but has availability at a shorter one
- **THEN** the statement says that a shorter length would find availability
