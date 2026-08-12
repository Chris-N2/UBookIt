## ADDED Requirements

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

## MODIFIED Requirements

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
