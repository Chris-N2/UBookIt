## ADDED Requirements

### Requirement: The listed window starts after a closure that covers today

Where **today is closed for the subject by a site closure or a closed period**, the window of dates
offered SHALL start on the **first date from today onwards that no closure or closed period applying
to the subject covers**, following ranges that adjoin or overlap one another. A visitor arriving
during a three-month closure SHALL be offered the first weeks after it, rather than being told that
nothing in the next few weeks is available.

- **For a resource**, the closures that apply are the site closures it has not opted out of and its
  own closed periods.
- **For a service**, the window SHALL start on the earliest such date of any candidate resource. A
  date on which every candidate is closed cannot offer the service, and a date on which one candidate
  is open might.
- **Only closures and closed periods SHALL move the start.** A date that is merely fully booked, or
  outside the weekly pattern, SHALL NOT move it. The window's start is a cheap fact about ranges, and
  is not an availability search.
- **Every other bound still holds**: no date beyond the horizon is listed, and the window SHALL NOT
  exceed the maximum query range. Where the first open date lies beyond the horizon, the window SHALL
  start today, exactly as it does without this rule, so the step explains itself as it already does.
- **What the step says SHALL name the window truthfully.** Where the window does not start today, the
  list's legend and every statement about an empty window SHALL name the dates the window spans,
  rather than "the next N days". It SHALL NOT say why the window moved: a closure is not disclosed
  publicly, and a shifted window looks the same as any other.

Where today is not closed for the subject, the window SHALL start today, exactly as before.

#### Scenario: A closure covering today moves the window
- **WHEN** a visitor reaches the date step on a date inside a site closure that runs to the end of December, for a resource that has not opted out and whose horizon reaches February
- **THEN** the dates listed start on 1 January, and the legend names the dates the window spans rather than "the next 30 days"

#### Scenario: Adjoining ranges are followed
- **WHEN** today is inside a site closure ending on a Friday and the resource's own closed period runs from the Saturday to the following Wednesday
- **THEN** the window starts on that Thursday

#### Scenario: An opted-out closure does not move the window
- **WHEN** today is inside a site closure that the resource has opted out of
- **THEN** the window starts today

#### Scenario: A fully booked today does not move the window
- **WHEN** today is open for the resource but every start is already taken
- **THEN** the window starts today, and today is simply not listed

#### Scenario: A service starts where its first candidate reopens
- **WHEN** today is inside one candidate's closed period ending in five days and another candidate's ending in ten
- **THEN** the service's window starts on the sixth day

#### Scenario: Beyond the horizon, the window does not move
- **WHEN** a closure covering today ends after the resource's horizon
- **THEN** the window starts today, and the step states that no date in it has availability

#### Scenario: The shifted window discloses no closure
- **WHEN** the window has moved because of a closure
- **THEN** nothing the step renders names the closure, its label, or the fact that a closure moved the window
