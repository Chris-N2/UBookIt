# service-booking Specification

## Purpose
Defines how a service resolves to the bookable resources that fill its roles: eligibility (resource type plus the capabilities a role requires), availability as the union across each role's heterogeneous candidate pool composed across the roles by a saturating assignment of distinct resources, and placement that resolves such an assignment and runs the existing per-resource pipeline one attempt at a time, each attempt claiming one distinct resource per role slot. Also fixes what a caller is told when no candidate can take a booking — a deterministic refusal and a lost race are distinct answers, and conflating them would either send a caller away from a slot that exists or invite them to retry one that never will.

Eligibility's inputs are deliberately public: publishing them is what keeps a service's candidate pool derivable by any consumer, and therefore what keeps the `resource-not-eligible` refusal free of disclosure.
## Requirements
### Requirement: Eligible resource resolution
`UBookIt.Core` SHALL resolve a role and a duration specification to a candidate pool: every resource whose type key equals the role's `ResourceType` **and whose capability set contains every capability the role requires**, further restricted to those whose booking constraints admit at least one length permitted by the duration specification (`ServiceDuration.TryResolveAgainst` succeeding). Eligibility SHALL be determined by resource type key and capability subset alone; no explicit service-to-resource association participates.

Resolution SHALL take the role and duration directly, not a service identifier or a constructed `Service`. A service being edited may not yet be valid — most obviously it may have no name — and requiring an aggregate to be constructible in order to ask which resources a role resolves to would let validation of unrelated fields decide whether the question can be asked at all. Resolving a saved service by id SHALL be a thin wrapper that loads it and delegates.

Resolution SHALL be **one computation**. The candidate pool and any diagnostic view of the same resolution SHALL be projections of a single evaluation rather than separate implementations, so that a report about which resources a service resolves to cannot disagree with the resolution the booking path performs. A diagnostic that computes eligibility independently would be confidently wrong in exactly the case it exists to detect.

A role requiring no capabilities SHALL match every resource of its type, so that a service defined before capabilities were introduced, or defined without them, resolves exactly as it did previously.

The subset test SHALL be evaluated in `UBookIt.Core` through the shared capability value object, over capabilities hydrated by the read port. It SHALL NOT be reimplemented as a storage-layer query predicate: a second implementation could disagree with the first, and eligibility disagreeing with availability is a defect invisible to both.

The candidate pool SHALL be complete and SHALL NOT be paged or truncated: a partial pool would report unavailability that does not exist and place bookings on the wrong resource, without any error being raised. Resolution SHALL depend only on the read ports, never on a management store. Callers on the management surface MAY invoke resolution; that constrains what resolution depends on, not who may ask it.

A resource excluded because its constraints admit no permitted length SHALL be excluded silently — that is an answer about that resource, not a validation failure of the service. A resource excluded because it lacks a required capability SHALL likewise be excluded silently.

#### Scenario: Pool is every resource of the role's type
- **WHEN** a role names resource type `room`, requires no capabilities, and four resources of type `room` and three of type `therapist` exist
- **THEN** the candidate pool is exactly the four `room` resources

#### Scenario: Resolution does not require a valid service
- **WHEN** a role and a duration are resolved for a service that has no name and has never been saved
- **THEN** the candidate pool is returned, and no name-related validation failure is raised

#### Scenario: The pool is the same however resolution is reached
- **WHEN** the same role and duration are resolved directly and by loading a saved service carrying them
- **THEN** both produce the same candidate pool

#### Scenario: The diagnostic view and the candidate pool agree
- **WHEN** a resolution's diagnostic view and its candidate pool are compared for the same role and duration
- **THEN** the diagnostic's final stage contains exactly the resources in the candidate pool

#### Scenario: A required capability narrows the pool
- **WHEN** a role names type `therapist` and requires `cert-x`, and of three therapists only Mary carries `cert-x`
- **THEN** the candidate pool is exactly Mary, and no validation failure is raised for the others

#### Scenario: All required capabilities must be present
- **WHEN** a role requires both `cert-x` and `welsh`, and a therapist carries only `cert-x`
- **THEN** that therapist is not in the candidate pool

#### Scenario: Extra capabilities do not disqualify
- **WHEN** a role requires `cert-x` and a therapist carries `cert-x`, `welsh`, and `massage`
- **THEN** that therapist is in the candidate pool

#### Scenario: Capabilities do not cross type boundaries
- **WHEN** a role names type `room` and requires `cert-x`, and a `therapist` resource carries `cert-x` while no `room` does
- **THEN** the candidate pool is empty — the therapist is not eligible for a room role

#### Scenario: A role requiring no capabilities is unaffected
- **WHEN** a role requires no capabilities and resources of its type carry assorted capabilities, including none at all
- **THEN** every resource of that type is in the candidate pool

#### Scenario: Overlapping pools resolve independently per role
- **WHEN** one role requires `cert-x` (matching only Mary) and another role of the same type requires nothing (matching Mary and Frank)
- **THEN** each role resolves to its own complete pool, and the pools overlap rather than partitioning the resources

#### Scenario: A resource whose range cannot admit the service is excluded
- **WHEN** a duration is fixed at 120 minutes and a candidate resource's maximum duration is 90 minutes
- **THEN** that resource is not in the candidate pool, and no validation failure is raised for it

#### Scenario: A resource whose granularity admits no permitted length is excluded
- **WHEN** a duration permits lengths between 40 and 50 minutes and a candidate resource has 30-minute granularity, so no multiple of 30 lies in the intersection
- **THEN** that resource is not in the candidate pool

#### Scenario: Pool is not truncated by paging
- **WHEN** the candidate pool for a role's type contains more resources than the read port's default page size
- **THEN** every resource of that type is still considered

#### Scenario: Unknown service
- **WHEN** eligibility is resolved for a service id that does not exist
- **THEN** the operation fails with code `service-not-found`

### Requirement: Resolution reports why each resource was excluded
A resolution SHALL be observable as an ordered chain of three stages — resources of the role's type, those of them satisfying the role's required capabilities, and those of them whose constraints admit a length the duration specification permits — so that a consumer can attribute an empty or narrowed pool to the filter responsible for it.

Reporting only the surviving count SHALL NOT be sufficient. A single number cannot distinguish a mistyped resource type from an over-narrow capability set from a duration no resource can provide, and those three faults are corrected in three different places. The count entering each stage and the count leaving it SHALL both be derivable.

For resources excluded at the duration stage, the resolution SHALL identify which resources were excluded and the bound that excluded them, since that is the fault the editor must act on.

#### Scenario: The chain attributes a narrowed pool
- **WHEN** ten `room` resources exist, three carry `projector`, and only one of those three can provide four hours
- **THEN** the resolution reports ten at the type stage, three at the capability stage, and one at the duration stage

#### Scenario: An empty pool names the stage that emptied it
- **WHEN** no resource carries a required capability
- **THEN** the capability stage reports zero while the type stage reports the resources of that type, so the fault is attributable to the capabilities rather than the type

#### Scenario: A mistyped resource type is distinguishable from a capability problem
- **WHEN** the role names a resource type no resource uses and also requires capabilities
- **THEN** the type stage reports zero, and the capability stage is not reported as the cause

#### Scenario: Duration exclusions name the resources and their limiting bound
- **WHEN** two resources carry the required capabilities but their maximum duration is shorter than a fixed service duration
- **THEN** the resolution identifies those two resources and the bound that excluded them

#### Scenario: A healthy configuration excludes nothing
- **WHEN** every resource of the role's type carries the required capabilities and can provide the duration
- **THEN** all three stages report the same count and no exclusions are listed

### Requirement: Service duration narrows each candidate independently
The lengths bookable through a service on a given resource SHALL be the intersection of the service's duration specification with that resource's own range, with each bound moved inward to a multiple of that resource's granularity. A service SHALL NOT widen any resource's range: a resource's maximum duration is a hard ceiling regardless of what the service permits, and likewise its minimum is a hard floor.

Because each candidate resolves independently against its own constraints, two candidates of the same type MAY offer different sets of lengths for the same service.

#### Scenario: Service narrows a resource's range
- **WHEN** a service permits 60–180 minutes and a candidate permits 30–120 minutes at 30-minute granularity
- **THEN** that candidate offers 60, 90, and 120 minutes through the service

#### Scenario: A resource ceiling is never widened
- **WHEN** a service permits lengths up to 240 minutes and a candidate's maximum is 90 minutes
- **THEN** no length above 90 minutes is offered or accepted on that candidate

#### Scenario: Candidates of one type differ
- **WHEN** a service with no duration bounds has two candidates, one permitting 30–60 minutes at 30-minute granularity and one permitting 20–40 minutes at 20-minute granularity
- **THEN** the first offers 30 and 60 minutes and the second offers 20 and 40 minutes

### Requirement: Union availability over a candidate pool
`UBookIt.Core` SHALL expose a service availability query returning, for an inclusive `[from, to]` date range, every start at which the service can be booked, together with the lengths bookable at that start. The query SHALL NOT take a requested duration.

**Within a role**, a start SHALL be offered when at least one of that role's candidates can fulfil it: availability over a role's pool is a union. Across roles the composition is an assignment, specified separately in "Composite availability across roles"; for a single-role service **of count 1** the two coincide, and this requirement describes that case unchanged. They do not coincide above count 1, which requires that many distinct resources at once.

The lengths at a start SHALL be expressed as a list of **arithmetic runs**, each `{ Min, Max, Step }`, denoting the lengths `Min, Min + Step, …, Max` inclusive. Within a role, each contributing candidate produces exactly one run at a start: its resolved range narrowed by how much free time remains from that start, stepping by that candidate's granularity. `Min` and `Max` SHALL both be multiples of `Step`, so `Max` is always reachable. A run arising from composition across roles denotes the same thing and satisfies the same property, but is not attributable to a single candidate.

Runs SHALL NOT be merged into a single `(minimum, maximum)` pair. Candidates differ in granularity and in minimum duration, so the union of their lengths at a start is in general neither contiguous nor confined to one grid; a merged pair would advertise lengths no candidate can book.

A run whose lengths another run at the same start already offers SHALL be dropped, and the remaining list SHALL be ordered deterministically. This is subset elimination and loses nothing: identically-configured candidates diverge as soon as one of them is booked — same grid and minimum, a shorter remaining run — and emitting both says nothing the wider one does not. Runs that merely *overlap* SHALL remain distinct, because each then carries lengths the other lacks. The set of lengths on offer at a start SHALL be identical before and after this elimination.

A start SHALL be omitted entirely when the service cannot be booked from it. The response SHALL NOT identify which resource backs a start or a run: the resources are resolved at placement time, and naming one here would imply a guarantee placement does not make.

The query SHALL apply the same bounded-range, inverted-range, and time-zone rules as the per-resource availability queries.

#### Scenario: Homogeneous pool yields one run per start
- **WHEN** every candidate of a single-role service shares the same granularity, minimum, and maximum, and two of them are bookable from a given start — including when one has a booking later in the day and so offers a shorter run than the other
- **THEN** that start carries exactly one run, the widest on offer, identical to what a single such resource would offer

#### Scenario: A subsumed run is dropped but a partially overlapping one is kept
- **WHEN** one candidate offers 30–90 at 30-minute steps, a second offers 30–480 at 30-minute steps, and a third offers 20–120 at 20-minute steps from the same start
- **THEN** that start carries two runs — the 30-minute one is dropped as a subset of the 480 one, and the 20-minute run survives because it carries lengths the others lack

#### Scenario: Eliminating runs never removes an offered length
- **WHEN** the lengths denoted by a start's runs are compared with the union of every candidate's own lengths at that start
- **THEN** the two sets are equal

#### Scenario: Differing granularities are not merged
- **WHEN** at one start a 30-minute-granularity candidate offers 30–90 minutes and a 20-minute-granularity candidate offers 20–120 minutes
- **THEN** that start carries two runs — `{30, 90, 30}` and `{20, 120, 20}` — and no run implies that 25, 50, or 70 minutes is bookable

#### Scenario: A gap between candidates is preserved
- **WHEN** at one start a candidate offers only 30 minutes and another offers 90–120 minutes
- **THEN** that start carries the runs `{30, 30, 30}` and `{90, 120, 30}`, and 60 minutes is not advertised as bookable

#### Scenario: Free time truncates a run
- **WHEN** a candidate permits up to 120 minutes through the service but only 60 minutes of free time remains from a start
- **THEN** that candidate's run at that start ends at 60 minutes

#### Scenario: A start no candidate can fulfil is omitted
- **WHEN** at a given start every candidate of a role has remaining free time shorter than its resolved minimum for the service
- **THEN** that start does not appear in the response

#### Scenario: Availability does not name resources
- **WHEN** a service availability response is inspected
- **THEN** no entry carries a resource id, and nothing identifies which candidate produced a run

#### Scenario: Per-resource semantics are reused, not reimplemented
- **WHEN** a single-role service has exactly one candidate whose constraints the service does not narrow
- **THEN** the starts returned are exactly those the per-resource bookable-start query returns for that resource over the same range

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

### Requirement: Every bookable length run is anchored at its step
A `LengthRun` SHALL denote exactly the multiples of its `Step` lying within `[Min, Max]`; that is, `Min` SHALL always be a multiple of `Step`. Every construction site SHALL enforce this rather than assume it.

The property is load-bearing in more than one algorithm. Subset elimination relies on two runs of equal step being in phase. Composing availability across roles relies on it too: the lengths for which an assignment exists are found by scanning the multiples of each candidate step, which only enumerates bookable lengths because every run is anchored at zero. An out-of-phase run would silently break both — advertising lengths no resource can book, or discarding lengths that were bookable — without any error being raised.

#### Scenario: Constructed runs are anchored
- **WHEN** any bookable length run is produced by availability projection or duration resolution
- **THEN** its minimum is a multiple of its step

#### Scenario: An out-of-phase run cannot be constructed
- **WHEN** a length run is constructed with a minimum that is not a multiple of its step
- **THEN** the construction is rejected rather than producing a run whose lengths the elimination and composition rules would misread

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

### Requirement: A submitted length is validated, never substituted
Service placement SHALL require an explicit requested length, including for a service whose duration is fixed. The requested length SHALL be validated and SHALL NEVER be replaced by a permitted one: a booker who asks for a length the service cannot provide SHALL be told so, not silently confirmed for a different length.

A requested length shorter than every candidate's resolved minimum SHALL fail with `duration-too-short`; longer than every candidate's resolved maximum SHALL fail with `duration-too-long`. These are decided from constraints alone, before any availability or placement work.

#### Scenario: Fixed-duration service still requires the length
- **WHEN** a placement for a fixed 60-minute service omits the requested length
- **THEN** the request is rejected as invalid rather than defaulted to 60 minutes

#### Scenario: Mismatched length against a fixed service is rejected
- **WHEN** a placement for a fixed 60-minute service requests 90 minutes
- **THEN** placement fails with `duration-too-long` and no booking is placed at any length

#### Scenario: Too short for every candidate
- **WHEN** a placement requests 15 minutes and every candidate's resolved minimum is 30 minutes
- **THEN** placement fails with `duration-too-short`

#### Scenario: A length one candidate permits is accepted
- **WHEN** a placement requests 40 minutes and only one candidate's resolved range admits 40 minutes
- **THEN** that candidate is the one booked

### Requirement: All candidates failing reports one of two distinct outcomes
When every attempt fails, service placement SHALL report which kind of failure occurred rather than echoing the last attempt's failures, which would be arbitrary.

The unit of an attempt is a **saturating assignment** — one distinct resource per role slot — because placement accumulates every claimed resource's rules before the conflict check. For a single-role service of count 1 an assignment is one candidate, and everything below reads as it always did.

When any attempt failed on `conflict`, placement SHALL fail with `conflict`: the request described a genuinely bookable slot that was taken concurrently or is already occupied, so retrying may succeed. An attempt that was **not made** because the candidates were already claimed SHALL be classified as the attempt would have been — which requires that a saturating assignment exist among the candidates whose own rules admit the request, since otherwise no attempt could have reached the conflict check at all. "Every role has such a candidate" is not that test: a role of count 2 with a single admitting candidate satisfies it and still cannot be filled.

When every attempt rejected the request *deterministically* — the refusal is a property of the request against a resource's configuration, such as a start off its grid, outside its open hours, inside its lead time, or beyond its horizon — placement SHALL fail with the stable code `service-unavailable`. Retrying is pointless. A slot that cannot be filled by any rule-admitting candidate makes the whole request deterministic in this sense, however free the other slots are.

The deterministic refusals SHALL be recognised explicitly rather than inferred from "not `conflict`". A refusal that is neither a conflict nor a known deterministic rule — a candidate deleted between resolution and its attempt, for instance — is transient, and reporting it as `service-unavailable` would both tell the caller not to retry when retrying would succeed and pollute the drift signal.

`service-unavailable` SHALL be treated as a drift signal: a client that placed only starts and lengths taken from the service availability query cannot legitimately provoke it, so its occurrence from a conforming client means availability and placement disagree. Composite availability makes this more load-bearing rather than less, since a multi-role service offers strictly fewer starts than any of its roles alone.

#### Scenario: Concurrent taking reports conflict
- **WHEN** every candidate is free at query time but all are taken before placement completes
- **THEN** placement fails with code `conflict`

#### Scenario: All candidates busy reports conflict
- **WHEN** a placement targets a start where every candidate already has a blocking claim, and each of them would otherwise have accepted the request
- **THEN** placement fails with code `conflict`, not `service-unavailable`

#### Scenario: Deterministic rejection reports service-unavailable
- **WHEN** a placement targets a start that is outside every candidate's open hours
- **THEN** placement fails with code `service-unavailable`

#### Scenario: A slot that can never be filled makes the outcome deterministic
- **WHEN** one slot's candidates are merely busy and another slot has no candidate whose own rules admit the request
- **THEN** placement fails with code `service-unavailable`, because no saturating assignment could have reached the conflict check

#### Scenario: A site misconfiguration is reported as itself
- **WHEN** every attempt fails because the configured site time zone is invalid, including when no attempt ran because every candidate was already claimed
- **THEN** placement fails with `time-zone-invalid`, not with either all-fail code — it is site configuration rather than a race or a per-candidate refusal

#### Scenario: A transient refusal is not the deterministic code
- **WHEN** the only candidate is deleted between resolution and its placement attempt, so the attempt fails with `resource-not-found`
- **THEN** placement fails with code `conflict`, not `service-unavailable`, because a retry may succeed

#### Scenario: A mixed outcome favours conflict
- **WHEN** one candidate of a single-role service rejects a start as off-grid and another rejects it as conflicting
- **THEN** placement fails with code `conflict`, because retrying may still succeed

#### Scenario: Availability-driven requests do not provoke the deterministic code
- **WHEN** a placement uses a start and a length taken verbatim from the service availability query for the same service, and no concurrent booking intervenes
- **THEN** placement succeeds, and `service-unavailable` is not returned

### Requirement: Direct-resource booking is unaffected
Introducing booking via a service SHALL NOT change how a resource is booked directly. The existing per-resource availability queries, the existing placement pipeline, and the existing placement endpoint SHALL behave identically whether or not any service exists, and whether or not a resource happens to be in some service's candidate pool. A booking placed through a service SHALL be an ordinary booking, indistinguishable in shape from a directly placed one, and SHALL block the resolved resource for other bookings on the same terms.

#### Scenario: Direct placement is unchanged by services
- **WHEN** a resource that belongs to a service's candidate pool is booked directly at a valid interval
- **THEN** placement succeeds exactly as it would if no service existed

#### Scenario: A service booking blocks direct booking
- **WHEN** a service booking resolves to a resource and a direct booking is then attempted for an overlapping interval on that resource
- **THEN** the direct booking fails with `conflict`

#### Scenario: A direct booking removes a candidate
- **WHEN** a resource in a candidate pool is booked directly and service availability is then queried over that interval
- **THEN** that resource contributes no runs for the affected starts

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
