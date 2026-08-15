## MODIFIED Requirements

### Requirement: All candidates failing reports one of two distinct outcomes
When every attempt fails, service placement SHALL report which kind of failure occurred rather than echoing the last attempt's failures, which would be arbitrary.

The unit of an attempt is a **combination** — one candidate per role — because placement accumulates every claimed resource's rules before the conflict check. For a single-role service a combination is one candidate, and everything below reads as it always did.

When any attempt failed on `conflict`, placement SHALL fail with `conflict`: the request described a genuinely bookable slot that was taken concurrently or is already occupied, so retrying may succeed. An attempt that was **not made** because the candidates were already claimed SHALL be classified as the attempt would have been — which requires every role to have a candidate whose own rules admit the request, since otherwise no combination could have reached the conflict check at all.

When every attempt rejected the request *deterministically* — the refusal is a property of the request against a resource's configuration, such as a start off its grid, outside its open hours, inside its lead time, or beyond its horizon — placement SHALL fail with the stable code `service-unavailable`. Retrying is pointless. A role that has no rule-admitting candidate makes the whole request deterministic in this sense, however free the other roles are.

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

#### Scenario: A role that can never be filled makes the outcome deterministic
- **WHEN** one role's candidates are merely busy and another role has no candidate whose own rules admit the request
- **THEN** placement fails with code `service-unavailable`, because no combination could have reached the conflict check

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

### Requirement: Union availability over a candidate pool
`UBookIt.Core` SHALL expose a service availability query returning, for an inclusive `[from, to]` date range, every start at which the service can be booked, together with the lengths bookable at that start. The query SHALL NOT take a requested duration.

**Within a role**, a start SHALL be offered when at least one of that role's candidates can fulfil it: availability over a role's pool is a union. Across roles it is an intersection, specified separately in "Composite availability across roles"; for a single-role service the two coincide, and this requirement describes that case unchanged.

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

### Requirement: Overlapping eligibility pools are a known boundary
Capability-constrained eligibility SHALL be understood to produce eligibility pools that overlap without being identical — a role requiring a capability resolves to a strict subset of the pool of a role requiring none of the same type. Single-role resolution SHALL be unaffected by this, since each role resolves independently.

The specification SHALL record that assigning several roles across overlapping pools by taking each role's first available candidate can produce a wrong answer rather than a slow one, and that composing such roles therefore requires real assignment rather than greedy selection. The same is true of **availability** and not only of placement: two roles drawing from one pool in which a single resource is free would each report that start available, while the pair is not bookable there.

Multi-role composition is supported **only where every role names a distinct resource type**, which makes the pools disjoint and independent per-role assignment correct. `Service.Create` SHALL continue to reject two roles naming the same resource type, and any count other than 1. Relaxing that restriction requires implementing assignment in both availability and placement.

#### Scenario: Same-type roles remain rejected
- **WHEN** a service is created with two roles both naming resource type `therapist`
- **THEN** creation is rejected, whatever their required capabilities differ by

#### Scenario: Distinct-type roles are accepted
- **WHEN** a service is created with a role for type `room` and a role for type `therapist`
- **THEN** creation succeeds

#### Scenario: Fixtures exercise overlapping pools
- **WHEN** the capability test fixtures are inspected
- **THEN** they contain at least one pair of roles whose eligible pools overlap without being equal, so that a later greedy assignment defect is detectable rather than invisible

### Requirement: Booking a service resolves a resource by candidate loop
`UBookIt.Core` SHALL expose service placement taking a service id, a start instant, a requested length, booker details, and an optional preferred resource id. Placement SHALL resolve **one resource per role**, and SHALL place a single booking whose claims are those resources — all sharing the booking's one interval — through the atomic placement contract. A service SHALL NOT produce more than one booking.

For a single-role service, placement SHALL attempt candidates one at a time, each attempt running the atomic placement contract for that single resource, and SHALL return the first success. Candidates SHALL be attempted in a deterministic order — ascending resource id — so repeated identical requests behave identically.

A failed attempt SHALL leave no persisted state, whether it claimed one resource or several, so attempting candidates or combinations in sequence is safe.

For a service of several roles, each role's candidate SHALL be chosen independently, which is correct because distinct types make the pools disjoint: no choice made for one role can remove a candidate from another. Combinations SHALL be attempted in a deterministic order.

The number of attempts SHALL NOT grow as the product of the roles' pool sizes. Candidates already claimed at the requested interval SHALL be excluded before any attempt is made, from a single read over every role's shortlist, so that a fully booked service costs no placement attempts rather than one per combination. That read is advisory and SHALL NOT replace the atomic placement contract: a candidate free when it was read may be taken before the attempt lands, which the loop still handles.

Excluding a candidate SHALL NOT change the outcome the caller is told. The excluded attempts SHALL be classified as the attempts they replaced would have been classified, and **being claimed is not sufficient to classify them**: the placement rules that are properties of a resource are evaluated before the conflict check, so a candidate that is claimed *and* would have been refused anyway — a start off its grid, outside its open hours, inside its lead time, or beyond its horizon — contributed a deterministic refusal and never reached the conflict check.

The unit of that classification SHALL be the **combination**, not the candidate. A combination reaches the conflict check only when *every* role contributes a candidate whose own rules admit the request, since placement accumulates the rules of every resource it would claim before checking for conflicts. The all-fail outcome SHALL therefore be `conflict` only when every role has such a candidate **and** at least one of the candidates excluded for being claimed is one of them. One admitting candidate in one role SHALL NOT make the outcome `conflict` while another role has none: no combination containing it could ever have raced.

A failure that is a property of the request or the site rather than of any resource — a broken site time zone, an interval that cannot be represented — SHALL be reported as itself even when every candidate was excluded and no attempt ran. Reducing the rule evaluation to a yes/no would bury it under an all-fail code that blames the pool for a fault that has nothing to do with it.

That classification SHALL come from the same rule evaluation placement runs, never from a second implementation of the rules.

Treating every excluded candidate as a lost race would report `conflict` for a request that can never succeed, inviting a retry that cannot help — and would silence the drift signal `service-unavailable` exists to be, in proportion to how busy the site is.

At most one placement attempt SHALL be in flight at a time. **This supersedes the earlier guarantee that no more than one resource lock is held at any moment**: an attempt for a service of several roles necessarily holds a lock for each resource it claims, which is what makes the placement atomic across them. Those locks SHALL be acquired in a deterministic order, so concurrent attempts sharing a resource cannot deadlock, and they SHALL be released together when the attempt commits or fails.

When a preferred resource id is supplied and is in some role's candidate pool, that resource SHALL be attempted first for **its own** role; the remaining candidates of that role follow in the same deterministic order, and other roles are unaffected. The preference identifies its role unambiguously, since a resource has exactly one type and therefore belongs to at most one role's pool. Preference is an ordering hint only: when the preferred resource cannot take the booking, the remaining candidates SHALL still be attempted. A preferred resource id in no role's pool SHALL be rejected with `resource-not-eligible`.

On success the result SHALL identify every resource actually booked.

#### Scenario: First available candidate is booked
- **WHEN** a single-role service is booked at a start where the lowest-id candidate is busy and the next is free
- **THEN** the booking is placed on the next candidate and the result names that resource

#### Scenario: Deterministic ordering
- **WHEN** the same service booking is requested twice against the same state
- **THEN** the same candidate is chosen both times

#### Scenario: One resource is claimed per role
- **WHEN** a service requiring a `room` and a `therapist` is booked
- **THEN** exactly one booking is created, carrying one claim for a `room` and one for a `therapist`, both over the booking's single interval

#### Scenario: A role with no free candidate prevents the booking
- **WHEN** every `therapist` is busy at a start but a `room` is free
- **THEN** no booking is placed and no claim is persisted for the room

#### Scenario: Preferred resource is tried first
- **WHEN** a placement supplies a preferred resource id that is in a role's candidate pool and is free
- **THEN** the booking is placed using that resource for its role

#### Scenario: Preferred resource falls through when unavailable
- **WHEN** a placement supplies a preferred resource id that is in a role's candidate pool but is already booked at that start, and another candidate of that role is free
- **THEN** the booking is placed on the other candidate

#### Scenario: Preferred resource outside every pool is rejected
- **WHEN** a placement supplies a preferred resource id that is in no role's candidate pool
- **THEN** placement fails with code `resource-not-eligible` and no booking is placed, rather than silently booking a different resource

#### Scenario: An ineligible preference is reported even when a pool is empty
- **WHEN** a placement supplies a preferred resource id against a service one of whose roles has an empty candidate pool
- **THEN** placement fails with code `resource-not-eligible`, not `service-unavailable` — the caller's own mistake is the more useful thing to report

#### Scenario: One attempt at a time
- **WHEN** a candidate loop runs over several candidates or combinations
- **THEN** each attempt completes before the next begins, and no attempt holds a lock while another is attempted

#### Scenario: A fully booked service costs no placement attempts
- **WHEN** every candidate of every role is already claimed at the requested start, and each of them would otherwise have accepted the request
- **THEN** placement fails with `conflict` without attempting any combination, rather than attempting one per pair of candidates

#### Scenario: A claimed candidate that would have been refused anyway is not a race
- **WHEN** the only candidate is already claimed and the requested start is also off its grid, or the requested interval is outside its open hours
- **THEN** placement fails with `service-unavailable`, not `conflict` — the request could not have succeeded whatever that resource's calendar looked like, so a retry is pointless

#### Scenario: A claimed candidate is not a race when another role can never be filled
- **WHEN** one role's only candidate is merely claimed, and another role's only candidate would refuse the request whatever its calendar looked like
- **THEN** placement fails with `service-unavailable` — no combination could have reached the conflict check, so the claimed candidate in the first role never had a race to lose

#### Scenario: A site misconfiguration survives every candidate being excluded
- **WHEN** the site time zone is unusable and every candidate is already claimed at the requested interval, so no attempt runs
- **THEN** placement reports the time-zone failure itself, not an all-fail code describing the pool

## ADDED Requirements

### Requirement: Composite availability across roles
`UBookIt.Core` SHALL compose a multi-role service's availability as the **intersection** across its roles of each role's union availability. A start SHALL be offered only where every role has a candidate able to fulfil it, and a length SHALL be offered at that start only where every role has a candidate able to provide it. A booking has one interval and one length, so both terms are common to all roles.

Intersection SHALL be over the sets of lengths the roles denote, not over their bounds. Each role offers a set of arithmetic runs at a start; the composite is every pairwise intersection of a run from one role with a run from another, since set intersection distributes over the union each role already represents. Intersecting only the outermost minimum and maximum would advertise lengths no combination of resources can book.

Two runs SHALL intersect to the multiples of the **least common multiple** of their steps within the overlap of their ranges. This is well defined because every run denotes exactly the multiples of its own step within its range — its minimum is always a multiple of its step — so the two grids are in phase at zero and no congruence solving is required. The result SHALL satisfy the same property, so the representation is closed under intersection.

The resulting runs SHALL be subject to the same subset elimination and deterministic ordering as a single role's, and SHALL NOT identify which resources back them.

A single-role service's composite availability SHALL be exactly that role's union availability, unchanged.

#### Scenario: A start is offered only when every role can fulfil it
- **WHEN** a `room` is free at 10:00 and 11:00 but the only `therapist` is free at 11:00 alone
- **THEN** the service offers 11:00 and does not offer 10:00

#### Scenario: Lengths intersect to the common multiple
- **WHEN** at a shared start one role offers `{30, 120, 30}` and another offers `{20, 120, 20}`
- **THEN** the composite offers `{60, 120, 60}` — the multiples of 60 in the overlap — and does not advertise 30, 40, 80 or 90 minutes

#### Scenario: A length only one role can provide is not offered
- **WHEN** at a shared start one role can provide 30 to 60 minutes and another only 90 to 120 minutes
- **THEN** that start is not offered at all, because no length is common to both

#### Scenario: Differing grids narrow rather than merge
- **WHEN** one role's candidates offer lengths on a 30-minute grid and another's on a 45-minute grid over the same range
- **THEN** the composite offers only multiples of 90 minutes within that range

#### Scenario: Intersection distributes over each role's union
- **WHEN** a role offers two distinct runs at a start and another role offers one
- **THEN** the composite is the union of both pairwise intersections, and the set of lengths offered equals the set of lengths some resource of every role can provide

#### Scenario: A single-role service is unaffected
- **WHEN** composite availability is computed for a service with one role
- **THEN** the result is identical to that role's union availability over the same range

#### Scenario: Composite availability does not name resources
- **WHEN** a composite availability response is inspected
- **THEN** no entry carries a resource id, and nothing identifies which role or candidate produced a run

### Requirement: Every bookable length run is anchored at its step
A `LengthRun` SHALL denote exactly the multiples of its `Step` lying within `[Min, Max]`; that is, `Min` SHALL always be a multiple of `Step`. Every construction site SHALL enforce this rather than assume it.

The property is load-bearing in more than one algorithm. Subset elimination relies on two runs of equal step being in phase, and intersection across roles relies on both grids being anchored at zero so that their common lengths are the multiples of the least common multiple of their steps. An out-of-phase run would silently break both — advertising lengths no resource can book, or discarding lengths that were bookable — without any error being raised.

#### Scenario: Constructed runs are anchored
- **WHEN** any bookable length run is produced by availability projection or duration resolution
- **THEN** its minimum is a multiple of its step

#### Scenario: An out-of-phase run cannot be constructed
- **WHEN** a length run is constructed with a minimum that is not a multiple of its step
- **THEN** the construction is rejected rather than producing a run whose lengths the elimination and intersection rules would misread
