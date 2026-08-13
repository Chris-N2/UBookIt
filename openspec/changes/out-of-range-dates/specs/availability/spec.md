## MODIFIED Requirements

### Requirement: Bounded query range
The availability free-time and slot queries SHALL reject a requested date range whose inclusive span exceeds a configurable maximum, `MaxQueryRangeDays`, with the stable failure code `date-range-too-large`. The span SHALL be counted inclusively (a `[from, to]` range where `from == to` is one day). `MaxQueryRangeDays` SHALL be a site booking setting, configurable per site, defaulting to 31. This bounds the day-by-day open-hours computation so an unbounded range can never be requested. The check SHALL be applied by the query service itself, so every caller — HTTP delivery endpoints and any in-process consumer — is protected, and it SHALL be evaluated before the resource is loaded or free time is computed.

A range SHALL additionally be rejected with `date-range-invalid` when either endpoint sits at the edge of the representable calendar — that is, when `from` is the first representable date or `to` is the last. Bounding the span is not sufficient on its own: a range at either edge can be one day wide and so passes the span check.

The two edges fail for different reasons, and both SHALL be rejected. At the end, the day-by-day computation increments once past its final day and steps off the calendar. At the start, mapping a wall-clock time on the first representable date into UTC subtracts the site zone's offset; for any zone east of UTC the result falls before the first representable instant, which cannot be represented. Rejecting both keeps the rule independent of which zone the site is configured for, rather than succeeding in one zone and raising an exception in another.

No availability query SHALL raise an exception for any pair of dates the caller can express, in any site time zone.

#### Scenario: Range within the maximum is computed
- **WHEN** free time is queried for an inclusive range of 31 days with the default maximum of 31
- **THEN** the query is computed normally and returns free time

#### Scenario: Range exceeding the maximum is rejected
- **WHEN** free time is queried for an inclusive range of 32 days with the default maximum of 31
- **THEN** the query fails with the code `date-range-too-large` and no free time is computed

#### Scenario: Slot query honours the same bound
- **WHEN** slots are queried for a range wider than `MaxQueryRangeDays`
- **THEN** the query fails with the code `date-range-too-large`

#### Scenario: A range ending at the last representable date is rejected
- **WHEN** any availability query is requested with `to` set to the last representable date, even for a one-day span
- **THEN** the query fails with the code `date-range-invalid` and no exception is raised

#### Scenario: A range starting at the first representable date is rejected
- **WHEN** any availability query is requested with `from` set to the first representable date
- **THEN** the query fails with the code `date-range-invalid` and no exception is raised, whatever the site time zone

#### Scenario: Rejection is confined to the edges
- **WHEN** an availability query is requested for the day after the first representable date, or the day before the last
- **THEN** the query is computed normally — only the two boundary dates are refused, not far-past or far-future dates generally
