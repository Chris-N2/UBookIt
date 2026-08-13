## MODIFIED Requirements

### Requirement: Bounded query range
The availability free-time and slot queries SHALL reject a requested date range whose inclusive span exceeds a configurable maximum, `MaxQueryRangeDays`, with the stable failure code `date-range-too-large`. The span SHALL be counted inclusively (a `[from, to]` range where `from == to` is one day). `MaxQueryRangeDays` SHALL be a site booking setting, configurable per site, defaulting to 31. This bounds the day-by-day open-hours computation so an unbounded range can never be requested. The check SHALL be applied by the query service itself, so every caller — HTTP delivery endpoints and any in-process consumer — is protected, and it SHALL be evaluated before the resource is loaded or free time is computed.

A range SHALL additionally be rejected with `date-range-invalid` when it cannot be walked day by day within the representable calendar — that is, when `to` is the last representable date. Bounding the span is not sufficient on its own: a range ending at the last representable date can be one day wide and so passes the span check, while the day-by-day computation still steps past the end of the calendar. Every date-range query SHALL be subject to both checks, and no availability query SHALL raise an exception for any pair of dates the caller can express.

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

#### Scenario: The first representable date is queryable
- **WHEN** any availability query is requested for the first representable date
- **THEN** the query is computed normally, because iteration moves forward and never steps before the start of the calendar

#### Scenario: The day before the last representable date is queryable
- **WHEN** an availability query is requested for the day before the last representable date
- **THEN** the query is computed normally — the rejection is confined to the boundary that cannot be walked, not to far-future dates generally
