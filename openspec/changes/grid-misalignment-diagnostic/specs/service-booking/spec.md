## ADDED Requirements

### Requirement: A permanent start-grid misalignment is detectable
`UBookIt.Core` SHALL expose a check over a service's roles that reports whether
those roles can share a bookable start **at all**.

The check SHALL be **one-directional**: it MAY report that no start can ever
exist, and SHALL NOT report that one does. Sharing a grid instant is necessary
for a bookable start and not sufficient — the instant must also fall in free time
on every resource involved, satisfy each one's lead time and horizon, and admit a
length they all permit, none of which this check evaluates. Reporting alignment
positively would assert availability, which the configuration surfaces are
forbidden to do.

Two grids SHALL be judged to meet when `gcd(step₁, step₂)` divides the offset
between their window starts, and to be permanently disjoint otherwise. The check
SHALL compute over the resources' configured **open windows** rather than their
free intervals: every candidate start lies on its resource's open-window grid
**under the configuration in force**, because placement aligns a start to that
window and a booking's length is a multiple of the resource's granularity, so the
window grid contains every start the resource can then offer. A conclusion drawn
from it therefore describes the configuration rather than the calendar, and
cannot appear and disappear as bookings come and go.

A booking placed **before** an opening-hours change may end off the grid now in
force, leaving a free interval — and so a shared start — that the current window
grid does not contain. The check SHALL still report such a pair. It describes the
configuration, which is permanently unbookable from the moment that booking
clears; falling silent would make the report consult the booking calendar, and
would withdraw it exactly while an editor was performing the repair it asked for.

Windows SHALL be compared on the same **local date** in the site zone, over the
days on which both roles are open. Comparing across dates or over a swept UTC
horizon would make the answer depend on when it was asked; a structural claim
SHALL NOT.

A daylight-saving transition falling **between** two windows on that date moves
one of them, so the wall-clock offset is not then the real one. The check SHALL
report a pair only when `gcd(step₁, step₂)` divides an hour, which is exactly when
the wall-clock offset yields the same verdict as the real one — divisibility
cannot see a shift the divisor divides. Otherwise it SHALL report nothing for that
pairing, and one such pairing SHALL clear the whole role pair, as an aligning
pairing does. Every granularity in ordinary use satisfies the condition; where it
does not, silence is required, because the alternative is accusing a
configuration that works on the transition date.

#### Scenario: A daylight-saving transition is never reported as a permanent clash
- **WHEN** two roles' resources are open across a date whose daylight-saving transition falls between their window starts, on granularities whose greatest common divisor does not divide an hour
- **THEN** the check reports nothing, and the service does have shared starts on that date

#### Scenario: A booking predating an opening-hours change does not withdraw the report
- **WHEN** a resource's opening time is changed after a booking was placed under the previous hours, so that the booking's end leaves a free interval off the new grid
- **THEN** the check still reports the pair, because the configuration is permanently misaligned once that booking clears

Two roles SHALL be reported as misaligned only when **no** candidate of one
shares a grid with **any** candidate of the other, on any day both are open. A
single awkward resource in a large pool SHALL NOT provoke the report.

For three or more roles, the check SHALL be applied pairwise. A system of
congruences is solvable exactly when it is solvable pairwise, so a clashing pair
is a complete explanation rather than one symptom among several.

The check SHALL identify the pair responsible: the two roles, the two resources,
and the window starts and granularities that cannot meet.

The check SHALL NOT reject, block, or alter any operation. Resolution,
availability, placement and service validation SHALL behave exactly as they did
without it.

#### Scenario: Grids that can never coincide are reported
- **WHEN** one role's only resource opens at 09:00 on a 30-minute granularity and another's opens at 09:15 on a 20-minute granularity
- **THEN** the check reports the two roles as permanently misaligned, naming both resources with their opening times and granularities

#### Scenario: Grids that can coincide are not reported
- **WHEN** one role's resource opens at 09:00 on a 30-minute granularity and another's opens at 09:30 on a 20-minute granularity
- **THEN** the check reports nothing, because `gcd(30, 20) = 10` divides the 30-minute offset

#### Scenario: Alignment is never reported as a positive finding
- **WHEN** the check finds that two roles' grids can coincide
- **THEN** it reports nothing at all, rather than reporting that the service is bookable, available, or has a start

#### Scenario: One aligning candidate is enough to stay silent
- **WHEN** a role has several resources, only one of which shares a grid with the other role's resource
- **THEN** the check reports nothing, because the service can be fulfilled by that candidate

#### Scenario: Bookings do not change the answer
- **WHEN** the same configuration is checked before and after a booking fills one resource's calendar
- **THEN** the check reports identically, because it is computed from open hours rather than free time

#### Scenario: A misalignment on one day only is not permanent
- **WHEN** two roles' resources share a grid on Tuesdays but not on Mondays
- **THEN** the check reports nothing, because a bookable start can exist

#### Scenario: Three roles are checked pairwise
- **WHEN** a service has three roles of which two can never share a start
- **THEN** the check reports that pair, and does not require the third role to be involved

#### Scenario: Detection changes nothing else
- **WHEN** a service whose roles are permanently misaligned is resolved, saved, and queried for availability
- **THEN** resolution returns its candidate pools, saving succeeds, and availability returns an empty result exactly as it did before this check existed
