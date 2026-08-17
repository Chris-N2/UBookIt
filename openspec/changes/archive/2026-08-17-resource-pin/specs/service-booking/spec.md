## MODIFIED Requirements

### Requirement: Booking a service resolves an assignment of distinct resources
`UBookIt.Core` SHALL expose service placement taking a service id, a start instant,
a requested length, booker details, and an optional pinned resource id. Placement
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

When a **pinned resource id** is supplied and is eligible for some slot, placement
SHALL seek a saturating assignment **including that resource**, in whichever slot it
fits. A resource may be eligible for several slots, so the pin identifies the booking
rather than a role, and the assignment chooses where it goes.

A pin SHALL be honoured or reported, never substituted. When no saturating assignment
includes the pinned resource **but one exists without it**, placement SHALL fail with
the stable code `pinned-resource-unavailable` rather than booking a different
resource. A caller who names a resource has chosen it; quietly confirming a booking
on someone else answers a question that was not asked.

Where no saturating assignment exists **either way**, the pin SHALL NOT be reported
as the cause. Nothing could have been booked whoever was named, and answering "the
resource you chose was unavailable" would invite a caller to pick another when there
is no other to pick; the all-candidates-failed outcomes answer instead, exactly as
they do for a request that named nobody.

That failure SHALL be **transient**: the pinned resource may be free at another time
or may free up, so a retry can succeed. It SHALL NOT be treated as a deterministic
refusal, which exists to tell a caller not to bother.

It SHALL be distinct from `conflict`, which reports that nothing could be booked and
leaves a caller unable to tell that other resources were free; and from
`resource-not-eligible`, which reports a resource that could never fulfil the service
at any time. A pinned resource id eligible for no slot SHALL still be rejected with
`resource-not-eligible`, before any question of availability arises — the caller's
own mistake remains the more useful thing to report.

Whether a pin can be honoured SHALL be judged over the same candidates the placement
attempt acts on, never by a second computation of the question. The pin failure SHALL
NOT be routed through the all-candidates-failed classification: assignments may exist
in abundance without the pinned resource, so reporting it as a fact about the pool
would describe a different failure from the one that occurred.

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

#### Scenario: A pinned resource is used
- **WHEN** a placement supplies a pinned resource id eligible for some slot and free
- **THEN** the booking is placed using that resource

#### Scenario: A pin that cannot be honoured fails rather than substituting
- **WHEN** a placement supplies a pinned resource id that is eligible but already booked at that start, and a saturating assignment exists without it
- **THEN** placement fails with code `pinned-resource-unavailable`, and no booking is placed on that assignment

#### Scenario: A pin failure invites a retry
- **WHEN** a placement fails because its pinned resource could not be included
- **THEN** the code is not one of the deterministic refusals, because the resource may be free at another time

#### Scenario: A pin failure is not reported as a pool failure
- **WHEN** a placement supplies a pinned resource that cannot be included, for a service whose other candidates could have been assigned
- **THEN** placement fails with `pinned-resource-unavailable`, not `conflict` or `service-unavailable`

#### Scenario: A pool that could not be assigned at all is not blamed on the pin
- **WHEN** a placement supplies a pinned resource for a service at an instant where no saturating assignment exists with or without it
- **THEN** placement fails with the all-candidates-failed outcome it would have reported for an unpinned request, not with `pinned-resource-unavailable`

#### Scenario: A pinned resource refused by its own rules is reported as the pin failing
- **WHEN** a placement supplies a pinned resource that is eligible and free, but whose own configuration refuses the requested start
- **THEN** placement fails with `pinned-resource-unavailable` rather than substituting a resource that would have accepted it

#### Scenario: A pinned resource outside every pool is rejected
- **WHEN** a placement supplies a pinned resource id eligible for no slot
- **THEN** placement fails with code `resource-not-eligible` and no booking is placed, rather than silently booking a different resource

#### Scenario: An ineligible pin is reported even when a pool is empty
- **WHEN** a placement supplies a pinned resource id against a service one of whose roles has an empty candidate pool
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

### Requirement: Eligibility remains derivable from public reads
Every input to the eligibility rule SHALL be readable through the anonymous delivery
API: a resource's type and capabilities, and, for **every one of a service's roles**,
its resource type and required capabilities. A caller SHALL therefore be able to
compute each role's candidate pool from public reads alone, without probing.

A role's **count** SHALL also be published. It is not an input to eligibility, but it
determines how many distinct resources a role consumes, so without it a caller can
compute the pools and still not know what the service requires of them.

This SHALL be treated as a standing constraint rather than a convenience. The
`resource-not-eligible` failure returned for an out-of-pool `pinnedResourceId`
discloses pool membership; it is acceptable precisely because the same fact is
already derivable. Any future change that constrains eligibility by data not
published here SHALL either publish that data or revisit that failure, and SHALL NOT
leave the two silently out of step.

The `pinned-resource-unavailable` failure SHALL be held to the same standard, and
SHALL be understood to disclose a resource's occupancy at an instant rather than its
pool membership. It is acceptable on the same grounds and no others: a resource's
free time is already published, so the failure tells a caller nothing a bookable-
starts read would not. Any future change that hides per-resource availability
SHALL revisit this failure, which would otherwise become an occupancy oracle for a
resource whose calendar the API had stopped publishing.

#### Scenario: A pool is computable from public reads
- **WHEN** an anonymous caller reads a service and the resource list
- **THEN** the caller can determine which resources are eligible for each of the service's roles without attempting a booking

#### Scenario: What a service requires is computable from public reads
- **WHEN** an anonymous caller reads a service whose role has a count greater than one
- **THEN** the published role states that count

#### Scenario: Probing an ineligible resource discloses nothing new
- **WHEN** an anonymous caller submits a `pinnedResourceId` naming a resource outside every pool and receives `resource-not-eligible`
- **THEN** the disclosed fact was already derivable from the published resource and service reads

#### Scenario: A refused pin discloses nothing not already published
- **WHEN** an anonymous caller submits a `pinnedResourceId` naming an eligible resource and receives `pinned-resource-unavailable`
- **THEN** the disclosed fact — that the resource is not free at that instant — was already derivable from that resource's published bookable starts
