# service-booking Specification

## Purpose
Defines how a service resolves to a bookable resource: eligibility (resource type plus the capabilities a role requires), the union of availability across a heterogeneous candidate pool, and candidate-loop placement that runs the existing per-resource pipeline one attempt at a time. Also fixes what a caller is told when no candidate can take a booking — a deterministic refusal and a lost race are distinct answers, and conflating them would either send a caller away from a slot that exists or invite them to retry one that never will.

Eligibility's inputs are deliberately public: publishing them is what keeps a service's candidate pool derivable by any consumer, and therefore what keeps the `resource-not-eligible` refusal free of disclosure.
## Requirements
### Requirement: Eligible resource resolution
`UBookIt.Core` SHALL resolve a service's single role to its candidate pool: every resource whose type key equals the role's `ResourceType` **and whose capability set contains every capability the role requires**, further restricted to those whose booking constraints admit at least one length permitted by the service's duration specification (`ServiceDuration.TryResolveAgainst` succeeding). Eligibility SHALL be determined by resource type key and capability subset alone; no explicit service-to-resource association participates.

A role requiring no capabilities SHALL match every resource of its type, so that a service defined before capabilities were introduced, or defined without them, resolves exactly as it did previously.

The subset test SHALL be evaluated in `UBookIt.Core` through the shared capability value object, over capabilities hydrated by the read port. It SHALL NOT be reimplemented as a storage-layer query predicate: a second implementation could disagree with the first, and eligibility disagreeing with availability is a defect invisible to both.

The candidate pool SHALL be complete and SHALL NOT be paged or truncated: a partial pool would report unavailability that does not exist and place bookings on the wrong resource, without any error being raised. Resolution SHALL depend only on the read ports, never on a management store.

A resource excluded because its constraints admit no permitted length SHALL be excluded silently — that is an answer about that resource, not a validation failure of the service. A resource excluded because it lacks a required capability SHALL likewise be excluded silently.

#### Scenario: Pool is every resource of the role's type
- **WHEN** a service's role names resource type `room`, requires no capabilities, and four resources of type `room` and three of type `therapist` exist
- **THEN** the candidate pool is exactly the four `room` resources

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
- **WHEN** a service is fixed at 120 minutes and a candidate resource's maximum duration is 90 minutes
- **THEN** that resource is not in the candidate pool, and no validation failure is raised for it

#### Scenario: A resource whose granularity admits no permitted length is excluded
- **WHEN** a service permits lengths between 40 and 50 minutes and a candidate resource has 30-minute granularity, so no multiple of 30 lies in the intersection
- **THEN** that resource is not in the candidate pool

#### Scenario: Pool is not truncated by paging
- **WHEN** the candidate pool for a role's type contains more resources than the read port's default page size
- **THEN** every resource of that type is still considered

#### Scenario: Unknown service
- **WHEN** eligibility is resolved for a service id that does not exist
- **THEN** the operation fails with code `service-not-found`

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
`UBookIt.Core` SHALL expose a service availability query returning, for an inclusive `[from, to]` date range, every start at which at least one candidate can fulfil the service, together with the lengths bookable at that start. The query SHALL NOT take a requested duration.

The lengths at a start SHALL be expressed as a list of **arithmetic runs**, each `{ Min, Max, Step }`, denoting the lengths `Min, Min + Step, …, Max` inclusive. Each contributing candidate produces exactly one run at a start: its resolved range narrowed by how much free time remains from that start, stepping by that candidate's granularity. `Min` and `Max` SHALL both be multiples of `Step`, so `Max` is always reachable.

Runs SHALL NOT be merged into a single `(minimum, maximum)` pair. Candidates differ in granularity and in minimum duration, so the union of their lengths at a start is in general neither contiguous nor confined to one grid; a merged pair would advertise lengths no candidate can book.

A run whose lengths another run at the same start already offers SHALL be dropped, and the remaining list SHALL be ordered deterministically. This is subset elimination and loses nothing: identically-configured candidates diverge as soon as one of them is booked — same grid and minimum, a shorter remaining run — and emitting both says nothing the wider one does not. Runs that merely *overlap* SHALL remain distinct, because each then carries lengths the other lacks. The set of lengths on offer at a start SHALL be identical before and after this elimination.

A start SHALL be omitted entirely when no candidate can fulfil the service from it. The response SHALL NOT identify which resource backs a start or a run: v1 resolves the resource at placement time, and naming one here would imply a guarantee placement does not make.

The query SHALL apply the same bounded-range, inverted-range, and time-zone rules as the per-resource availability queries.

#### Scenario: Homogeneous pool yields one run per start
- **WHEN** every candidate shares the same granularity, minimum, and maximum, and two of them are bookable from a given start — including when one has a booking later in the day and so offers a shorter run than the other
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
- **WHEN** at a given start every candidate's remaining free time is shorter than its resolved minimum for the service
- **THEN** that start does not appear in the response

#### Scenario: Availability does not name resources
- **WHEN** a service availability response is inspected
- **THEN** no entry carries a resource id, and nothing identifies which candidate produced a run

#### Scenario: Per-resource semantics are reused, not reimplemented
- **WHEN** a service has exactly one candidate whose constraints the service does not narrow
- **THEN** the starts returned are exactly those the per-resource bookable-start query returns for that resource over the same range

### Requirement: Booking a service resolves a resource by candidate loop
`UBookIt.Core` SHALL expose service placement taking a service id, a start instant, a requested length, booker details, and an optional preferred resource id. Placement SHALL attempt candidates one at a time, each attempt running the existing atomic placement contract for that single resource, and SHALL return the first success. At most one placement attempt SHALL be in flight at a time, so no more than one resource lock is held at any moment.

Candidates SHALL be attempted in a deterministic order — ascending resource id — so repeated identical requests behave identically. When a preferred resource id is supplied and is in the candidate pool, that resource SHALL be attempted first; the remaining candidates follow in the same deterministic order. Preference is an ordering hint only: when the preferred resource cannot take the booking, the remaining candidates SHALL still be attempted.

A failed attempt SHALL leave no persisted state, so attempting candidates in sequence is safe.

On success the result SHALL identify the resource actually booked.

#### Scenario: First available candidate is booked
- **WHEN** a service is booked at a start where the lowest-id candidate is busy and the next is free
- **THEN** the booking is placed on the next candidate and the result names that resource

#### Scenario: Deterministic ordering
- **WHEN** the same service booking is requested twice against the same state
- **THEN** the same candidate is chosen both times

#### Scenario: Preferred resource is tried first
- **WHEN** a placement supplies a preferred resource id that is in the candidate pool and is free
- **THEN** the booking is placed on that resource

#### Scenario: Preferred resource falls through when unavailable
- **WHEN** a placement supplies a preferred resource id that is in the candidate pool but is already booked at that start, and another candidate is free
- **THEN** the booking is placed on the other candidate

#### Scenario: Preferred resource outside the pool is rejected
- **WHEN** a placement supplies a preferred resource id that is not in the service's candidate pool
- **THEN** placement fails with code `resource-not-eligible` and no booking is placed, rather than silently booking a different resource

#### Scenario: An ineligible preference is reported even when the pool is empty
- **WHEN** a placement supplies a preferred resource id against a service whose candidate pool is empty
- **THEN** placement fails with code `resource-not-eligible`, not `service-unavailable` — the caller's own mistake is the more useful thing to report

#### Scenario: One lock at a time
- **WHEN** a candidate loop runs over several candidates
- **THEN** each attempt completes before the next begins, and no attempt holds a lock while another is attempted

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
When every candidate attempt fails, service placement SHALL report which kind of failure occurred rather than echoing the last candidate's failures, which would be arbitrary.

When any candidate failed on `conflict`, placement SHALL fail with `conflict`: the request described a genuinely bookable slot that was taken concurrently or is already occupied, so retrying may succeed.

When every candidate rejected the request *deterministically* — the refusal is a property of the request against that resource's configuration, such as a start off its grid, outside its open hours, inside its lead time, or beyond its horizon — placement SHALL fail with the stable code `service-unavailable`. Retrying is pointless.

The deterministic refusals SHALL be recognised explicitly rather than inferred from "not `conflict`". A refusal that is neither a conflict nor a known deterministic rule — a candidate deleted between resolution and its attempt, for instance — is transient, and reporting it as `service-unavailable` would both tell the caller not to retry when retrying would succeed and pollute the drift signal.

`service-unavailable` SHALL be treated as a drift signal: a client that placed only starts and lengths taken from the service availability query cannot legitimately provoke it, so its occurrence from a conforming client means availability and placement disagree.

#### Scenario: Concurrent taking reports conflict
- **WHEN** every candidate is free at query time but all are taken before placement completes
- **THEN** placement fails with code `conflict`

#### Scenario: All candidates busy reports conflict
- **WHEN** a placement targets a start where every candidate already has a blocking claim
- **THEN** placement fails with code `conflict`, not `service-unavailable`

#### Scenario: Deterministic rejection reports service-unavailable
- **WHEN** a placement targets a start that is outside every candidate's open hours
- **THEN** placement fails with code `service-unavailable`

#### Scenario: A site misconfiguration is reported as itself
- **WHEN** every candidate attempt fails because the configured site time zone is invalid
- **THEN** placement fails with `time-zone-invalid`, not with either all-fail code — it is site configuration rather than a race or a per-candidate refusal

#### Scenario: A transient refusal is not the deterministic code
- **WHEN** the only candidate is deleted between resolution and its placement attempt, so the attempt fails with `resource-not-found`
- **THEN** placement fails with code `conflict`, not `service-unavailable`, because a retry may succeed

#### Scenario: A mixed outcome favours conflict
- **WHEN** one candidate rejects a start as off-grid and another rejects it as conflicting
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
Every input to the eligibility rule SHALL be readable through the anonymous delivery API: a resource's type and capabilities, and a role's resource type and required capabilities. A caller SHALL therefore be able to compute a service's candidate pool from public reads alone, without probing.

This SHALL be treated as a standing constraint rather than a convenience. The `resource-not-eligible` failure returned for an out-of-pool `preferredResourceId` discloses pool membership; it is acceptable precisely because the same fact is already derivable. Any future change that constrains eligibility by data not published here SHALL either publish that data or revisit that failure, and SHALL NOT leave the two silently out of step.

#### Scenario: A pool is computable from public reads
- **WHEN** an anonymous caller reads a service and the resource list
- **THEN** the caller can determine which resources are eligible for the service's role without attempting a booking

#### Scenario: Probing an ineligible resource discloses nothing new
- **WHEN** an anonymous caller submits a `preferredResourceId` naming a resource outside the pool and receives `resource-not-eligible`
- **THEN** the disclosed fact was already derivable from the published resource and service reads

### Requirement: Overlapping eligibility pools are a known boundary
Capability-constrained eligibility SHALL be understood to produce eligibility pools that overlap without being identical — a role requiring a capability resolves to a strict subset of the pool of a role requiring none of the same type. Single-role resolution SHALL be unaffected by this, since each role resolves independently.

The specification SHALL record that assigning several roles across overlapping pools by taking each role's first available candidate can produce a wrong answer rather than a slow one, and that multi-role composition therefore requires real assignment rather than greedy selection. Multi-role composition remains out of scope; `Service.Create` continues to reject more than one role and any count other than 1.

#### Scenario: Multi-role composition remains rejected
- **WHEN** a service is created with two roles
- **THEN** creation is rejected with the `service-role-invalid` code

#### Scenario: Fixtures exercise overlapping pools
- **WHEN** the capability test fixtures are inspected
- **THEN** they contain at least one pair of roles whose eligible pools overlap without being equal, so that a later greedy assignment defect is detectable rather than invisible
