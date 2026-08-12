# availability Specification

## Purpose

Defines how a resource's bookable time is described and computed: weekly open hours, date-specific exceptions, per-resource booking constraints, free-time computation, slot projection, and time-zone/DST semantics.

## Requirements

### Requirement: Weekly open hours
Each resource SHALL have a weekly open-hours pattern: for each day of the week, zero or more non-overlapping wall-clock windows expressed as `[start, end)` with `TimeOnly` bounds, where `start < end`. A day with no windows is closed. Overlapping or zero-length windows on the same day SHALL be rejected when the pattern is defined.

#### Scenario: Time inside an open window
- **WHEN** a resource is open Monday–Friday 08:00–18:00 and a Tuesday 09:00–10:00 interval is evaluated
- **THEN** the interval is within open hours

#### Scenario: Closed day
- **WHEN** the same resource is evaluated for any Sunday interval
- **THEN** the interval is outside open hours

#### Scenario: Invalid pattern is rejected
- **WHEN** a pattern is defined with Monday windows 08:00–12:00 and 11:00–14:00
- **THEN** the definition is rejected because the windows overlap

### Requirement: Date exceptions
A resource SHALL support date-specific exceptions. An exception targets a single calendar date and either closes the resource for that date or replaces that date's open windows entirely. An exception SHALL take precedence over the weekly pattern for its date. At most one exception SHALL exist per resource per date.

#### Scenario: Closure exception removes availability
- **WHEN** a resource open Monday 08:00–18:00 has a closure exception for a given Monday
- **THEN** the availability projection for that date contains no free time

#### Scenario: Override exception replaces the day's windows
- **WHEN** a resource open Monday 08:00–18:00 has an override exception of 08:00–22:00 for a given Monday
- **THEN** the availability projection for that date offers free time up to 22:00

### Requirement: Booking constraints
Each resource SHALL carry booking constraints: slot granularity, minimum duration, maximum duration, minimum lead time, and booking horizon. Defaults SHALL be: granularity 15 minutes, minimum duration 30 minutes, maximum duration 8 hours, lead time zero, horizon 90 days. Constraints SHALL be configurable per resource and validated for coherence (minimum ≤ maximum; durations positive multiples of granularity).

#### Scenario: Incoherent constraints are rejected
- **WHEN** constraints are defined with minimum duration 60 minutes and maximum duration 30 minutes
- **THEN** the definition is rejected

#### Scenario: Defaults apply when unspecified
- **WHEN** a resource is created without explicit constraints
- **THEN** its constraints report granularity 15 minutes, minimum duration 30 minutes, maximum duration 8 hours, lead time zero, and horizon 90 days

### Requirement: Free-time computation
The system SHALL compute free time for a resource over a queried date range as: open hours (with exceptions applied), minus intervals covered by blocking booking claims (see `bookings` for which statuses block). The result SHALL be an ordered list of disjoint `[start, end)` intervals. Claims in non-blocking statuses (`Cancelled`, `Declined`) SHALL NOT reduce free time. Touching or overlapping open windows SHALL coalesce: back-to-back windows (e.g. 08:00–12:00 and 12:00–14:00) form continuous bookable time, and an interval spanning their join is inside open hours.

#### Scenario: Booking splits a free window
- **WHEN** a resource is open 08:00–18:00 on a date and has one confirmed claim 10:00–11:00
- **THEN** free time for that date is exactly 08:00–10:00 and 11:00–18:00

#### Scenario: Cancelled booking does not reduce free time
- **WHEN** the only claim on an open day belongs to a cancelled booking
- **THEN** free time for that date equals the full open hours

#### Scenario: Touching windows form continuous bookable time
- **WHEN** a resource is open 08:00–12:00 and 12:00–14:00 on a date with no bookings
- **THEN** free time for that date is the single interval 08:00–14:00

### Requirement: Slot projection
The system SHALL project bookable start times for a requested duration: candidate starts advance in granularity steps from each free-interval start, and a candidate is offered iff the whole `[start, start + duration)` interval fits inside a single free interval and satisfies lead time and horizon. Slots SHALL never be persisted; projection is a pure computation.

Fixed-duration slot projection and bookable-start projection SHALL be derived from a single traversal of free time rather than implemented as two independent walks, so the two can never disagree about which starts are offered. The two projections SHALL be mutually consistent: for any duration, the starts offered by slot projection SHALL be exactly those bookable starts whose shortest-to-longest range includes that duration.

#### Scenario: Slots for a duration within a free window
- **WHEN** free time on a date is 09:00–11:00, granularity is 30 minutes, and slots are projected for a 60-minute duration
- **THEN** the offered start times are exactly 09:00, 09:30, and 10:00

#### Scenario: Duration that cannot fit produces no slots
- **WHEN** free time on a date is a single 09:00–10:00 interval and slots are projected for a 90-minute duration
- **THEN** no start times are offered for that interval

#### Scenario: The two projections agree
- **WHEN** slots are projected for a given duration, and bookable starts are projected over the same resource and date range
- **THEN** the slot start times are exactly the bookable starts whose minimum bookable length is at most that duration and whose maximum bookable length is at least it

### Requirement: Bookable-start projection
The system SHALL project, for a resource over an inclusive date range, every aligned start time together with the shortest and longest length bookable from that start. Candidate starts SHALL advance in granularity steps from each free-interval start, exactly as fixed-duration slot projection does, and SHALL satisfy lead time and horizon on the same terms.

For each qualifying start, the longest bookable length SHALL be the run from that start to the end of its containing free interval, clamped to the resource's maximum duration and floored to a granularity multiple. The shortest bookable length SHALL be the resource's minimum duration. A start whose longest bookable length is shorter than its shortest bookable length SHALL NOT be offered.

Bookable starts SHALL never be persisted; the projection is a pure computation. The projection SHALL be bounded by the same maximum query range as every other availability query.

#### Scenario: Maximum length shortens towards the end of a free interval
- **WHEN** free time on a date is 09:00–12:00, granularity is 60 minutes, and the resource permits 60 minutes to 8 hours
- **THEN** the offered starts are 09:00 with a maximum of 3 hours, 10:00 with a maximum of 2 hours, and 11:00 with a maximum of 1 hour

#### Scenario: The resource maximum caps the run
- **WHEN** free time on a date is 09:00–17:00, granularity is 60 minutes, and the resource's maximum duration is 2 hours
- **THEN** every offered start reports a maximum bookable length of at most 2 hours

#### Scenario: A start too close to the end of a free interval is not offered
- **WHEN** free time on a date is 09:00–09:45, granularity is 15 minutes, and the resource's minimum duration is 30 minutes
- **THEN** starts are offered at 09:00 and 09:15 only, and 09:30 is not offered because only 15 minutes remain

#### Scenario: The maximum is floored to a granularity multiple
- **WHEN** a free interval leaves 100 minutes from a start and the resource's granularity is 30 minutes
- **THEN** the reported maximum bookable length for that start is 90 minutes

#### Scenario: Bookable starts respect lead time
- **WHEN** the resource has a lead time that excludes the earliest otherwise-free start
- **THEN** that start is absent from the bookable-start projection, on the same terms as fixed-duration slot projection

### Requirement: Time zone and DST semantics
Availability rules SHALL be interpreted as wall-clock times in a single site-wide IANA time zone. Booking instants SHALL be stored as UTC together with the IANA zone id in effect at placement; an existing booking's UTC interval SHALL NOT move if zone rules later change. On a spring-forward date, wall-clock times inside the nonexistent gap SHALL be excluded from availability. On a fall-back date, an ambiguous wall-clock time SHALL resolve to its first occurrence (the earlier UTC offset).

#### Scenario: Spring-forward gap is not bookable
- **WHEN** the site zone is `Europe/London`, a resource is open 00:30–02:30, and availability is projected for the date the clocks go forward (01:00 → 02:00)
- **THEN** free time excludes the nonexistent 01:00–02:00 wall-clock hour and includes 00:30–01:00 and 02:00–02:30

#### Scenario: Ambiguous time resolves to first occurrence
- **WHEN** the site zone is `Europe/London` and a booking is placed at wall-clock 01:30 on the date the clocks go back (02:00 → 01:00)
- **THEN** the stored UTC instant corresponds to the earlier-offset (BST, UTC+1) occurrence, i.e. 00:30 UTC

### Requirement: Bounded query range
The availability free-time and slot queries SHALL reject a requested date range whose inclusive span exceeds a configurable maximum, `MaxQueryRangeDays`, with the stable failure code `date-range-too-large`. The span SHALL be counted inclusively (a `[from, to]` range where `from == to` is one day). `MaxQueryRangeDays` SHALL be a site booking setting, configurable per site, defaulting to 31. This bounds the day-by-day open-hours computation so an unbounded range can never be requested. The check SHALL be applied by the query service itself, so every caller — HTTP delivery endpoints and any in-process consumer — is protected, and it SHALL be evaluated before the resource is loaded or free time is computed.

#### Scenario: Range within the maximum is computed
- **WHEN** free time is queried for an inclusive range of 31 days with the default maximum of 31
- **THEN** the query is computed normally and returns free time

#### Scenario: Range exceeding the maximum is rejected
- **WHEN** free time is queried for an inclusive range of 32 days with the default maximum of 31
- **THEN** the query fails with the code `date-range-too-large` and no free time is computed

#### Scenario: Slot query honours the same bound
- **WHEN** slots are queried for a range wider than `MaxQueryRangeDays`
- **THEN** the query fails with the code `date-range-too-large`

#### Scenario: Bound is evaluated before resource lookup
- **WHEN** an over-wide range is queried for a resource id that does not exist
- **THEN** the failure is `date-range-too-large` (the range is rejected before the resource is loaded)
