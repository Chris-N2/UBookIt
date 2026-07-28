## ADDED Requirements

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
