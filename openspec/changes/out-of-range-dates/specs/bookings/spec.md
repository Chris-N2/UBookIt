## MODIFIED Requirements

### Requirement: Placement validation pipeline
Booking placement SHALL validate a request against an ordered rule pipeline, producing a structured result: success, or failure carrying one stable machine-readable code per failed rule. The rules and codes SHALL be, in order: `interval-invalid` (end not after start, malformed, or not representable), `granularity` (start or duration not aligned), `duration-too-short`, `duration-too-long`, `lead-time` (starts sooner than minimum lead), `horizon` (starts beyond booking horizon), `outside-open-hours` (interval not fully inside open hours with exceptions applied), `conflict` (overlaps a blocking claim). Start alignment is defined relative to the start of the coalesced open window containing the interval; when the interval lies outside open hours, start alignment cannot be evaluated and only `outside-open-hours` is reported. Failures SHALL NOT be signalled by exceptions.

A request SHALL fail with `interval-invalid`, ahead of every other rule, when the requested interval cannot be represented: when the start added to the duration would exceed the last representable instant, or fall before the first. Both directions SHALL be covered — a far-future start overflows, and a large negative duration underflows.

A request SHALL likewise fail with `interval-invalid` when the interval is representable but the surrounding window the open-hours rule needs cannot be. Evaluating open hours inspects the day either side of the requested start, so a start within **one day of either end** of the calendar SHALL be rejected — the first two and the last two representable dates.

Two days are needed at each end, for different reasons. Above, the day after the start must exist and the day-by-day walk then steps once past it. Below, the day before the start must exist and must itself be mappable to UTC, which the first representable date is not for a site zone far enough east. Such a request SHALL be rejected as an unrepresentable interval rather than raising an exception.

No placement request SHALL raise an exception for any start instant or duration the caller can express, whatever its magnitude or sign.

When placement runs over a service's candidate pool, an `interval-invalid` failure SHALL be reported as itself rather than translated into an all-candidates-failed outcome. It describes the request, not any candidate's answer to it, so every candidate reports it identically; answering "no resource can fulfil this" would blame the pool for a fault it has nothing to do with, and would send a caller whose date is simply unrepresentable looking for a different time slot.

#### Scenario: Inverted interval
- **WHEN** placement is requested with an end instant not after its start
- **THEN** the result is failure with code `interval-invalid`

#### Scenario: Request outside open hours
- **WHEN** placement is requested for 07:00–08:00 on a resource open from 08:00
- **THEN** the result is failure with code `outside-open-hours`

#### Scenario: Valid request succeeds with structured result
- **WHEN** placement is requested for an aligned, correctly sized interval in free open time
- **THEN** the result is success and carries the created `Confirmed` booking

#### Scenario: An interval that would overflow is rejected
- **WHEN** placement is requested with a start close enough to the last representable instant that adding the duration would exceed it
- **THEN** the result is failure with code `interval-invalid` and no exception is raised

#### Scenario: An interval that would underflow is rejected
- **WHEN** placement is requested with a large negative duration, such that the interval end would fall before the first representable instant
- **THEN** the result is failure with code `interval-invalid` and no exception is raised

#### Scenario: A start at a calendar boundary is rejected
- **WHEN** placement is requested for a start on either of the first two, or either of the last two, representable dates, so the open-hours rule's surrounding day window cannot be formed
- **THEN** the result is failure with code `interval-invalid` and no exception is raised

#### Scenario: A service placement echoes the request-level failure
- **WHEN** a service placement is requested with a start at a calendar edge, so every candidate rejects it as unrepresentable
- **THEN** the result carries `interval-invalid`, not `service-unavailable`

#### Scenario: Ordinary far-future placement is unaffected
- **WHEN** placement is requested for a start many years ahead but far from the representable limit
- **THEN** the request is evaluated by the normal pipeline and fails on `horizon`, not on `interval-invalid`
