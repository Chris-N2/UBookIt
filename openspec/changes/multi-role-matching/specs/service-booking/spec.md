## RENAMED Requirements

- FROM: `### Requirement: Booking a service resolves a resource by candidate loop`
- TO: `### Requirement: Booking a service resolves an assignment of distinct resources`

- FROM: `### Requirement: Overlapping eligibility pools are a known boundary`
- TO: `### Requirement: Overlapping eligibility pools are resolved by assignment`

## MODIFIED Requirements

### Requirement: Composite availability across roles
`UBookIt.Core` SHALL compose a multi-role service's availability from an
**assignment**: a start and a length are offered exactly when every one of the
service's role slots can be filled simultaneously by **distinct** resources able to
provide that length at that start. A role of count *N* SHALL contribute *N* slots,
and a resource SHALL fill at most one slot, because a booking cannot claim the same
resource twice.

A start SHALL NOT be offered merely because every role has some candidate able to
fulfil it. That test is necessary and not sufficient once pools overlap: two roles
drawing on one pool in which a single resource is free each have a candidate, while
the pair cannot be booked. A booking has one interval and one length, so both terms
remain common to all roles.

Where every role names a **distinct** resource type and has count 1, the pools are
disjoint, a saturating assignment exists exactly when each role independently has a
candidate, and the composed result SHALL therefore offer exactly the starts the
intersection of the roles' union availabilities offered, and at each start denote
exactly the lengths it denoted — the answer given before assignment existed.

Equality is over the **lengths denoted**, not over the run objects. Runs are now
rebuilt from the feasible length set rather than accumulated as pairwise
intersections, and a set of lengths has more than one valid expression as anchored
runs: `{60, 120}` may arrive as one run of step 60 where the pairwise fold produced
two single-length runs. Both denote the same lengths, and a consumer reads lengths.
Requiring the runs themselves to match would pin the contract to an algorithm this
requirement no longer specifies.

Lengths SHALL be composed over the **sets** of lengths the roles denote, never over
their outermost bounds, so that no length is offered which no admissible assignment
can book. The lengths offered at a start SHALL be exactly those for which a
saturating assignment exists, expressed as arithmetic runs; because that set need
not be a single anchored run, a start MAY carry several runs, as it already may.

The resulting runs SHALL be subject to the same subset elimination and deterministic
ordering as a single role's, and SHALL NOT identify which resources back them, nor
which assignment was found.

A single-role service **of count 1** SHALL have composite availability exactly equal
to that role's union availability, unchanged. A single role of count greater than 1
SHALL NOT, since it requires that many distinct resources at once.

#### Scenario: A start is offered only when every role can fulfil it
- **WHEN** a `room` is free at 10:00 and 11:00 but the only `therapist` is free at 11:00 alone
- **THEN** the service offers 11:00 and does not offer 10:00

#### Scenario: A start two roles cannot both fill is not offered
- **WHEN** two roles of one resource type draw on a pool in which exactly one resource is free at a start
- **THEN** that start is not offered, even though each role considered alone has a candidate there

#### Scenario: A count is not satisfied by one resource counted twice
- **WHEN** a single role of count 2 has exactly one free candidate at a start
- **THEN** that start is not offered

#### Scenario: One free resource each is enough when the roles can be told apart
- **WHEN** two roles of one type require different capabilities, and two resources are free of which each satisfies exactly one of the roles
- **THEN** the start is offered, because an assignment exists

#### Scenario: A greedy choice that strands a role does not lose a bookable start
- **WHEN** a start admits an assignment only if the resource eligible for both roles is given to the role that has no other candidate
- **THEN** the start is offered

#### Scenario: Distinct-type services are unaffected
- **WHEN** availability is composed for a service whose roles all name distinct resource types with count 1
- **THEN** it offers exactly the starts the intersection of the roles' union availabilities offered over the same range, and at each start denotes exactly the same lengths

#### Scenario: Lengths intersect to the common multiple
- **WHEN** at a shared start one role offers `{30, 120, 30}` and another offers `{20, 120, 20}`, over disjoint pools
- **THEN** the composite offers `{60, 120, 60}` — the multiples of 60 in the overlap — and does not advertise 30, 40, 80 or 90 minutes

#### Scenario: A role offering several runs is composed candidate by candidate
- **WHEN** at a start one role's pool offers two distinct runs and another role's offers one, over disjoint pools
- **THEN** the lengths offered are exactly those some candidate of every role can provide, and no length is offered that only the outer envelope of a role's runs contains

#### Scenario: A length only one role can provide is not offered
- **WHEN** at a shared start one role can provide 30 to 60 minutes and another only 90 to 120 minutes
- **THEN** that start is not offered at all, because no length is common to both

#### Scenario: Differing grids narrow rather than merge
- **WHEN** one role's candidates offer lengths on a 30-minute grid and another's on a 45-minute grid over the same range, over disjoint pools
- **THEN** the composite offers only multiples of 90 minutes within that range

#### Scenario: A length is offered only where an assignment provides it
- **WHEN** at a start the only assignment that saturates the slots uses resources whose common lengths are narrower than the union of what each role could provide alone
- **THEN** only the narrower set is offered

#### Scenario: A feasible length set that is not one anchored run is still expressed exactly
- **WHEN** the lengths for which an assignment exists at a start are 30 and 90 minutes but not 60
- **THEN** the start carries runs denoting exactly 30 and 90, and does not advertise 60

#### Scenario: A single-role service of count 1 is unaffected
- **WHEN** composite availability is computed for a service with one role of count 1
- **THEN** the result is identical to that role's union availability over the same range

#### Scenario: Composite availability does not name resources
- **WHEN** a composite availability response is inspected
- **THEN** no entry carries a resource id, and nothing identifies which role, candidate or assignment produced a run

### Requirement: Booking a service resolves an assignment of distinct resources
`UBookIt.Core` SHALL expose service placement taking a service id, a start instant,
a requested length, booker details, and an optional preferred resource id. Placement
SHALL resolve **one resource per role slot**, all distinct, and SHALL place a single
booking whose claims are those resources — all sharing the booking's one interval —
through the atomic placement contract. A service SHALL NOT produce more than one
booking.

A role of count *N* SHALL contribute *N* slots. Resolving the slots SHALL be an
assignment problem, not an independent choice per role: a resource may be eligible
for more than one slot, and choosing greedily can strand a slot whose only candidate
was consumed by another, reporting a service unavailable when it was bookable.
Placement SHALL therefore find a saturating assignment where one exists.

For a single-role service of count 1, placement SHALL attempt candidates one at a
time, each attempt running the atomic placement contract for that single resource,
and SHALL return the first success. Candidates SHALL be attempted in a deterministic
order — ascending resource id — so repeated identical requests behave identically.

A failed attempt SHALL leave no persisted state, whether it claimed one resource or
several, so attempting candidates or assignments in sequence is safe.

Assignments SHALL be resolved in a deterministic order, so repeated identical
requests against identical state claim the same resources.

The number of attempts SHALL NOT grow as the product of the roles' pool sizes.
Candidates already claimed at the requested interval SHALL be excluded before any
attempt is made, from a single read over every role's shortlist, so that a fully
booked service costs no placement attempts rather than one per combination. That
read is advisory and SHALL NOT replace the atomic placement contract: a candidate
free when it was read may be taken before the attempt lands, which placement still
handles.

Excluding a candidate SHALL NOT change the outcome the caller is told. The excluded
attempts SHALL be classified as the attempts they replaced would have been
classified, and **being claimed is not sufficient to classify them**: the placement
rules that are properties of a resource are evaluated before the conflict check, so
a candidate that is claimed *and* would have been refused anyway — a start off its
grid, outside its open hours, inside its lead time, or beyond its horizon —
contributed a deterministic refusal and never reached the conflict check.

The unit of that classification SHALL be a **saturating assignment**, not a
candidate. An attempt reaches the conflict check only when every slot is filled by a
resource whose own rules admit the request, since placement accumulates the rules of
every resource it would claim before checking for conflicts. The all-fail outcome
SHALL therefore be `conflict` only when a saturating assignment exists among the
candidates whose rules admit the request **and** at least one resource in it was
excluded for being claimed. One admitting candidate in one role SHALL NOT make the
outcome `conflict` while some slot cannot be filled at all: no assignment containing
it could ever have raced.

A failure that is a property of the request or the site rather than of any resource —
a broken site time zone, an interval that cannot be represented — SHALL be reported
as itself even when every candidate was excluded and no attempt ran. Reducing the
rule evaluation to a yes/no would bury it under an all-fail code that blames the pool
for a fault that has nothing to do with it.

That classification SHALL come from the same rule evaluation placement runs, never
from a second implementation of the rules.

Treating every excluded candidate as a lost race would report `conflict` for a
request that can never succeed, inviting a retry that cannot help — and would silence
the drift signal `service-unavailable` exists to be, in proportion to how busy the
site is.

At most one placement attempt SHALL be in flight at a time. **This supersedes the
earlier guarantee that no more than one resource lock is held at any moment**: an
attempt for a service of several slots necessarily holds a lock for each resource it
claims, which is what makes the placement atomic across them. Those locks SHALL be
acquired in a deterministic order, so concurrent attempts sharing a resource cannot
deadlock, and they SHALL be released together when the attempt commits or fails.

When a preferred resource id is supplied and is eligible for some slot, placement
SHALL seek a saturating assignment **including that resource**, in whichever slot it
fits. A resource may now be eligible for several slots, so the preference identifies
the booking rather than a role. Preference remains a hint only: when no saturating
assignment includes it, placement SHALL fall back to any saturating assignment rather
than failing. A preferred resource id eligible for no slot SHALL be rejected with
`resource-not-eligible`.

On success the result SHALL identify every resource actually booked.

#### Scenario: First available candidate is booked
- **WHEN** a single-role service of count 1 is booked at a start where the lowest-id candidate is busy and the next is free
- **THEN** the booking is placed on the next candidate and the result names that resource

#### Scenario: Deterministic ordering
- **WHEN** the same service booking is requested twice against the same state
- **THEN** the same resources are chosen both times

#### Scenario: One resource is claimed per role
- **WHEN** a service requiring a `room` and a `therapist` is booked
- **THEN** exactly one booking is created, carrying one claim for a `room` and one for a `therapist`, both over the booking's single interval

#### Scenario: A count claims that many distinct resources
- **WHEN** a service with one role of count 2 is booked and two candidates are free
- **THEN** one booking is created carrying two claims naming two different resources

#### Scenario: No resource is claimed twice
- **WHEN** a service whose roles draw on one pool is booked
- **THEN** no booking is created in which the same resource fills two slots, and no such attempt is made

#### Scenario: A greedy choice that strands a slot does not lose a bookable service
- **WHEN** one resource is eligible for both slots, a second is eligible for only one of them, and both are free
- **THEN** the booking succeeds, with the shared resource taking the slot the other cannot fill

#### Scenario: A role with no free candidate prevents the booking
- **WHEN** every `therapist` is busy at a start but a `room` is free
- **THEN** no booking is placed and no claim is persisted for the room

#### Scenario: A count exceeding the free resources prevents the booking
- **WHEN** a role of count 2 has two candidates of which only one is free
- **THEN** no booking is placed, and the free one is not claimed

#### Scenario: Preferred resource is used
- **WHEN** a placement supplies a preferred resource id eligible for some slot and free
- **THEN** the booking is placed using that resource

#### Scenario: Preferred resource falls through when unavailable
- **WHEN** a placement supplies a preferred resource id that is eligible but already booked at that start, and a saturating assignment exists without it
- **THEN** the booking is placed on that assignment

#### Scenario: Preferred resource outside every pool is rejected
- **WHEN** a placement supplies a preferred resource id eligible for no slot
- **THEN** placement fails with code `resource-not-eligible` and no booking is placed, rather than silently booking a different resource

#### Scenario: An ineligible preference is reported even when a pool is empty
- **WHEN** a placement supplies a preferred resource id against a service one of whose roles has an empty candidate pool
- **THEN** placement fails with code `resource-not-eligible`, not `service-unavailable` — the caller's own mistake is the more useful thing to report

#### Scenario: One attempt at a time
- **WHEN** placement runs over several candidates or assignments
- **THEN** each attempt completes before the next begins, and no attempt holds a lock while another is attempted

#### Scenario: A fully booked service costs no placement attempts
- **WHEN** every candidate of every role is already claimed at the requested start, and each of them would otherwise have accepted the request
- **THEN** placement fails with `conflict` without attempting any assignment, rather than attempting one per combination of candidates

#### Scenario: A claimed candidate that would have been refused anyway is not a race
- **WHEN** the only candidate is already claimed and the requested start is also off its grid, or the requested interval is outside its open hours
- **THEN** placement fails with `service-unavailable`, not `conflict` — the request could not have succeeded whatever that resource's calendar looked like, so a retry is pointless

#### Scenario: A claimed candidate is not a race when a slot can never be filled
- **WHEN** one slot's only candidate is merely claimed, and another slot's only candidate would refuse the request whatever its calendar looked like
- **THEN** placement fails with `service-unavailable` — no assignment could have reached the conflict check, so the claimed candidate never had a race to lose

#### Scenario: A site misconfiguration survives every candidate being excluded
- **WHEN** the site time zone is unusable and every candidate is already claimed at the requested interval, so no attempt runs
- **THEN** placement reports the time-zone failure itself, not an all-fail code describing the pool

### Requirement: Overlapping eligibility pools are resolved by assignment
Capability-constrained eligibility SHALL be understood to produce eligibility pools
that overlap without being identical — a role requiring a capability resolves to a
strict subset of the pool of a role requiring none of the same type. Single-role
resolution SHALL be unaffected by this, since each role resolves independently.

Assigning several slots across overlapping pools by taking each slot's first
available candidate SHALL be understood to produce a wrong answer rather than a slow
one, so composing such slots requires real assignment rather than greedy selection.
This applies to **availability** as well as placement: two roles drawing from one
pool in which a single resource is free would each report that start available,
while the pair is not bookable there.

Multi-role composition SHALL therefore be supported for roles of the same resource
type and for counts greater than one, resolved by assignment in both availability
and placement. `Service.Create` SHALL accept two roles naming the same resource type
where their required capabilities differ, and SHALL accept a count greater than 1
within its permitted bound.

The test fixtures SHALL continue to contain at least one pair of roles whose eligible
pools overlap without being equal, so that a greedy assignment defect is detectable
rather than invisible.

#### Scenario: Same-type roles are accepted when their capabilities differ
- **WHEN** a service is created with two roles both naming resource type `therapist`, one requiring `cert-x` and the other requiring nothing
- **THEN** creation succeeds

#### Scenario: Distinct-type roles are accepted
- **WHEN** a service is created with a role for type `room` and a role for type `therapist`
- **THEN** creation succeeds

#### Scenario: Fixtures exercise overlapping pools
- **WHEN** the capability test fixtures are inspected
- **THEN** they contain at least one pair of roles whose eligible pools overlap without being equal, so that a greedy assignment defect is detectable rather than invisible

### Requirement: Eligibility remains derivable from public reads
Every input to the eligibility rule SHALL be readable through the anonymous delivery
API: a resource's type and capabilities, and, for **every one of a service's roles**,
its resource type and required capabilities. A caller SHALL therefore be able to
compute each role's candidate pool from public reads alone, without probing.

A role's **count** SHALL also be published. It is not an input to eligibility, but it
determines how many distinct resources a role consumes, so without it a caller can
compute the pools and still not know what the service requires of them.

This SHALL be treated as a standing constraint rather than a convenience. The
`resource-not-eligible` failure returned for an out-of-pool `preferredResourceId`
discloses pool membership; it is acceptable precisely because the same fact is
already derivable. Any future change that constrains eligibility by data not
published here SHALL either publish that data or revisit that failure, and SHALL NOT
leave the two silently out of step.

#### Scenario: A pool is computable from public reads
- **WHEN** an anonymous caller reads a service and the resource list
- **THEN** the caller can determine which resources are eligible for each of the service's roles without attempting a booking

#### Scenario: What a service requires is computable from public reads
- **WHEN** an anonymous caller reads a service whose role has a count greater than one
- **THEN** the published role states that count

#### Scenario: Probing an ineligible resource discloses nothing new
- **WHEN** an anonymous caller submits a `preferredResourceId` naming a resource outside every pool and receives `resource-not-eligible`
- **THEN** the disclosed fact was already derivable from the published resource and service reads

### Requirement: A permanent start-grid misalignment is detectable
`UBookIt.Core` SHALL expose a check over a service's roles that reports whether
those roles can share a bookable start **at all**.

The check SHALL be **one-directional**: it MAY report that no start can ever exist,
and SHALL NOT report that one does. Sharing a grid instant is necessary for a
bookable start and not sufficient — the instant must also fall in free time on every
resource involved, satisfy each one's lead time and horizon, and admit a length they
all permit, none of which this check evaluates. Reporting alignment positively would
assert availability, which the configuration surfaces are forbidden to do.

Two grids SHALL be judged to meet when `gcd(step₁, step₂)` divides the offset between
their window starts, and to be permanently disjoint otherwise — subject to the
daylight-saving condition below, which narrows when that judgement may be *reported*.
The check SHALL compute over the resources' configured **open windows** rather than
their free intervals: every candidate start lies on its resource's open-window grid
**under the configuration in force**, because placement aligns a start to that window
and a booking's length is a multiple of the resource's granularity, so the window
grid contains every start the resource can then offer. A conclusion drawn from it
therefore describes the configuration rather than the calendar, and cannot appear and
disappear as bookings come and go.

A booking placed **before** an opening-hours change may end off the grid now in
force, leaving a free interval — and so a shared start — that the current window grid
does not contain. The check SHALL still report such a pair. It describes the
configuration, which is permanently unbookable from the moment that booking clears;
falling silent would make the report consult the booking calendar, and would withdraw
it exactly while an editor was performing the repair it asked for.

Windows SHALL be compared on the same **local date** in the site zone, over the days
on which both roles are open. Comparing across dates or over a swept UTC horizon
would make the answer depend on when it was asked; a structural claim SHALL NOT.

A daylight-saving transition falling **between** two windows on that date moves one
of them, so the wall-clock offset is not then the real one. The check SHALL report a
pair only when `gcd(step₁, step₂)` divides an hour, which is exactly when the
wall-clock offset yields the same verdict as the real one — divisibility cannot see a
shift the divisor divides. Otherwise it SHALL report nothing for that pairing, and
one such pairing SHALL clear the whole role pair, as an aligning pairing does. Every
granularity in ordinary use satisfies the condition; where it does not, silence is
required, because the alternative is accusing a configuration that works on the
transition date.

A candidate SHALL NOT be compared against **itself**. Once two roles may draw on one
pool, the same resource appears in both, and a resource trivially shares its own grid
— which would silence the report through an assignment that can never occur, since a
booking cannot claim one resource twice. Pairings of equal resources SHALL be skipped
rather than treated as meeting.

Same-type role pairs SHALL NOT be skipped wholesale on that account. Two roles of one
resource type requiring different capabilities can draw on disjoint sets of
resources, so a permanent misalignment between them remains possible and SHALL still
be reported.

Two roles SHALL be reported as misaligned only when **no** candidate of one shares a
grid with **any** candidate of the other, on any day both are open. A single awkward
resource in a large pool SHALL NOT provoke the report.

For three or more roles, the check SHALL be applied pairwise. A system of congruences
is solvable exactly when it is solvable pairwise, so a clashing pair is a complete
explanation rather than one symptom among several.

The check SHALL identify the pair responsible: the two roles, the two resources, and
the window starts and granularities that cannot meet.

The check SHALL NOT reject, block, or alter any operation. Resolution, availability,
placement and service validation SHALL behave exactly as they did without it.

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

#### Scenario: A resource shared between two roles does not silence the report
- **WHEN** two roles of one resource type draw on a pool, and the only pairing that appears to meet is a resource compared against itself
- **THEN** the check does not treat that pairing as meeting, because no booking could claim that resource for both roles

#### Scenario: A misalignment between two same-type roles is still reported
- **WHEN** two roles of one resource type require different capabilities, no resource satisfies both, and no resource of one role's pool shares a grid with any of the other's
- **THEN** the check reports the pair

#### Scenario: Bookings do not change the answer
- **WHEN** the same configuration is checked before and after a booking fills one resource's calendar
- **THEN** the check reports identically, because it is computed from open hours rather than free time

#### Scenario: A misalignment on one day only is not permanent
- **WHEN** two roles' resources share a grid on Tuesdays but not on Mondays
- **THEN** the check reports nothing, because a bookable start can exist

#### Scenario: A daylight-saving transition is never reported as a permanent clash
- **WHEN** two roles' resources are open across a date whose daylight-saving transition falls between their window starts, on granularities whose greatest common divisor does not divide an hour
- **THEN** the check reports nothing, and the service does have shared starts on that date

#### Scenario: A booking predating an opening-hours change does not withdraw the report
- **WHEN** a resource's opening time is changed after a booking was placed under the previous hours, so that the booking's end leaves a free interval off the new grid
- **THEN** the check still reports the pair, because the configuration is permanently misaligned once that booking clears

#### Scenario: Three roles are checked pairwise
- **WHEN** a service has three roles of which two can never share a start
- **THEN** the check reports that pair, and does not require the third role to be involved

#### Scenario: Detection changes nothing else
- **WHEN** a service whose roles are permanently misaligned is resolved, saved, and queried for availability
- **THEN** resolution returns its candidate pools, saving succeeds, and availability returns an empty result exactly as it did before this check existed
