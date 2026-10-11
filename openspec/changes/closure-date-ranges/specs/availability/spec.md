## MODIFIED Requirements

### Requirement: Date exceptions
A resource SHALL support date-specific exceptions. An exception targets a single calendar date and either closes the resource for that date or replaces that date's open windows entirely. An exception SHALL take precedence over the weekly pattern for its date. At most one exception SHALL exist per resource per date.

**A resource may also carry closed periods** (see *Resource closed periods*). A closed period is the resource's own rule, at the same level as its exceptions, and SHALL NOT cover a date the resource also carries an exception for.

**A date may additionally carry a site closure, which is a separate layer and not one of the resource's own exceptions or closed periods.** The uniqueness rule above governs the resource's own exceptions alone: a resource may hold an exception for a date that also carries a closure, and that is not a duplicate.

**A site closure SHALL take precedence over the resource's own exception and closed periods as well as over the weekly pattern**, unless the resource has opted out of that closure. A resource that has opted out SHALL resolve the date exactly as it would if the closure did not exist — its own exception or closed period where it has one, otherwise the weekly pattern. The precedence order for a date is therefore: an applicable site closure, then the resource's own exception or closed period, then the weekly pattern.

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

## ADDED Requirements

### Requirement: Resource closed periods

A resource SHALL support **closed periods**: a start date and an end date, both inclusive, during which the resource is closed for every date covered. A closed period SHALL only close; different hours on a date remain the single-day exception's job. A closed period MAY carry a note, which is the site's own and SHALL NOT be disclosed outside the backoffice.

- **The end SHALL NOT be before the start**, refused with the stable code `closed-period-invalid`. A note longer than the package's name-column length SHALL be refused with the same code, as validation rather than as a storage error.
- **No date SHALL be covered by two of the resource's closed periods**, refused with the stable code `closed-period-overlap`.
- **No closed period SHALL cover a date for which the resource carries an exception**, refused with the stable code `closed-period-conflicts-with-exception`. A date that is both "closed for the period" and "open 10:00–14:00" is a contradiction the operator resolves; the package SHALL NOT choose silently.
- These rules are about the resource's own configuration, and SHALL be judged over the complete set a write submits. A site closure covering the same dates is not a conflict, and is governed by the precedence in *Date exceptions*.

A closed period SHALL close the resource by every route that computes availability or validates placement, on the same terms as a closure exception on each date it covers. A booking already placed on a date that a closed period later covers SHALL be unchanged, and SHALL continue to block its time.

#### Scenario: A closed period closes every date it covers
- **WHEN** a resource open every weekday carries a closed period from a Monday to the Friday of the following week
- **THEN** no date from that Monday to that Friday offers free time, and the days either side are unaffected

#### Scenario: A one-day closed period
- **WHEN** a closed period's start and end are the same date
- **THEN** that one date is closed

#### Scenario: An inverted period is refused
- **WHEN** a resource is saved with a closed period whose end is before its start
- **THEN** the write fails with the code `closed-period-invalid` and the resource is unchanged

#### Scenario: Overlapping periods are refused
- **WHEN** a resource is saved with closed periods 1–14 March and 10–20 March
- **THEN** the write fails with the code `closed-period-overlap` and the resource is unchanged

#### Scenario: Adjacent periods are not an overlap
- **WHEN** a resource is saved with closed periods 1–14 March and 15–20 March
- **THEN** the write is accepted

#### Scenario: A period covering an exception is refused
- **WHEN** a resource is saved with an override exception on 5 March and a closed period 1–14 March
- **THEN** the write fails with the code `closed-period-conflicts-with-exception` and the resource is unchanged

#### Scenario: A site closure over the same dates is not a conflict
- **WHEN** a resource with a closed period 1–14 March is saved while a site closure covers 10–20 March
- **THEN** the write is accepted, and every date from 1 to 20 March is closed for that resource

#### Scenario: An existing booking survives a closed period
- **WHEN** a closed period is added over a date that carries a confirmed booking on that resource
- **THEN** the booking is unchanged, its time remains claimed, and no new placement is accepted on that date

#### Scenario: Placement inside a closed period is refused
- **WHEN** placement is requested on a date inside the resource's closed period
- **THEN** the placement is refused as outside open hours

#### Scenario: A service accounts for a candidate's closed period
- **WHEN** a service's only candidate resource carries a closed period across a date
- **THEN** the service offers no start on that date
