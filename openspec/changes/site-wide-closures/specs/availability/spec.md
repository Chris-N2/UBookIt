## MODIFIED Requirements

### Requirement: Date exceptions
A resource SHALL support date-specific exceptions. An exception targets a single calendar date and either closes the resource for that date or replaces that date's open windows entirely. An exception SHALL take precedence over the weekly pattern for its date. At most one exception SHALL exist per resource per date.

**A date may additionally carry a site closure, which is a separate layer and not one of the resource's own exceptions.** The uniqueness rule above governs the resource's own exceptions alone: a resource may hold an exception for a date that also carries a closure, and that is not a duplicate.

**A site closure SHALL take precedence over the resource's own exception as well as over the weekly pattern**, unless the resource has opted out of that closure. A resource that has opted out SHALL resolve the date exactly as it would if the closure did not exist — its own exception where it has one, otherwise the weekly pattern. The precedence order for a date is therefore: an applicable site closure, then the resource's own exception, then the weekly pattern.

**A superseded exception SHALL be retained, not discarded.** Removing the closure, or opting out of it, SHALL restore the exception's full effect without it being re-entered.

#### Scenario: Closure exception removes availability
- **WHEN** a resource open Monday 08:00–18:00 has a closure exception for a given Monday
- **THEN** the availability projection for that date contains no free time

#### Scenario: Override exception replaces the day's windows
- **WHEN** a resource open Monday 08:00–18:00 has an override exception of 08:00–22:00 for a given Monday
- **THEN** the availability projection for that date offers free time up to 22:00

#### Scenario: A site closure supersedes the resource's own exception
- **WHEN** a resource has an override exception of 10:00–14:00 on a date carrying a site closure it has not opted out of
- **THEN** the availability projection for that date contains no free time

#### Scenario: Opting out restores the resource's own exception
- **WHEN** that resource opts out of the closure
- **THEN** the availability projection for that date offers 10:00–14:00, and not the weekly pattern

#### Scenario: A resource exception on a closure date is not a duplicate
- **WHEN** an exception is defined for a date that already carries a site closure
- **THEN** the definition is accepted, because the uniqueness rule governs the resource's own exceptions alone

### Requirement: Free-time computation
The system SHALL compute free time for a resource over a queried date range as: open hours (with the resource's own exceptions and any applicable site closures applied), minus intervals covered by blocking booking claims (see `bookings` for which statuses block). The result SHALL be an ordered list of disjoint `[start, end)` intervals. Claims in non-blocking statuses (`Cancelled`, `Declined`) SHALL NOT reduce free time. Touching or overlapping open windows SHALL coalesce: back-to-back windows (e.g. 08:00–12:00 and 12:00–14:00) form continuous bookable time, and an interval spanning their join is inside open hours.

**A site closure applicable to a date SHALL yield no open windows for that date**, whatever the weekly pattern and whatever exception the resource carries. A closure the resource has opted out of SHALL contribute nothing to the computation.

#### Scenario: Booking splits a free window
- **WHEN** a resource is open 08:00–18:00 on a date and has one confirmed claim 10:00–11:00
- **THEN** free time for that date is exactly 08:00–10:00 and 11:00–18:00

#### Scenario: Cancelled booking does not reduce free time
- **WHEN** the only claim on an open day belongs to a cancelled booking
- **THEN** free time for that date equals the full open hours

#### Scenario: Touching windows form continuous bookable time
- **WHEN** a resource is open 08:00–12:00 and 12:00–14:00 on a date with no bookings
- **THEN** free time for that date is the single interval 08:00–14:00

#### Scenario: A site closure yields no free time
- **WHEN** free time is computed for a date carrying a site closure the resource has not opted out of
- **THEN** the result contains no interval on that date, and the dates either side are unaffected

#### Scenario: An opted-out closure contributes nothing
- **WHEN** free time is computed for a date carrying a site closure the resource has opted out of
- **THEN** the result is identical to the result computed on a site holding no closure for that date
