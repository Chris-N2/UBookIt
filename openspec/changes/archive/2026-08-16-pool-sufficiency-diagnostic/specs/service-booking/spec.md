## ADDED Requirements

### Requirement: A structurally insufficient pool is detectable
`UBookIt.Core` SHALL expose a check over a service's roles that reports whether the
resources that exist can fill every role **at once**.

A role of count *N* requires *N* distinct resources, and a resource may be eligible
for more than one role, so the question is not answerable role by role: each role
having candidates is necessary and not sufficient. The check SHALL judge it as an
assignment — the roles can be filled together exactly when a matching saturating
every role slot exists over the eligibility graph — and SHALL be computed by the
same function placement and availability use, never by a second implementation of
the rule.

The check SHALL be **one-directional**: it MAY report that the roles can never be
filled together, and SHALL NOT report that they can. A sufficient pool means only
that the configuration is not structurally impossible. It says nothing about opening
hours, lead time, booking horizon, granularity, whether the resources are ever free
at the same instant, or whether their start grids ever coincide — none of which this
check evaluates. Reporting sufficiency positively would assert availability, which
the configuration surfaces are forbidden to do.

The check SHALL compute over **eligibility alone** — resource type, the capabilities
a role requires, and a duration the service permits on that resource — and SHALL NOT
consult the booking calendar. A conclusion drawn from it therefore describes the
configuration rather than the calendar, and cannot appear and disappear as bookings
come and go, or withdraw itself while an editor is performing the repair it asked
for.

When the roles cannot be filled together the check SHALL identify a **deficient
set**: the roles involved, how many distinct resources they require between them,
and how many resources are eligible for any of them. The set SHALL be reported in
terms of **roles**, not of the internal slot expansion, so that every part of the
report names something an editor can act on.

The reported set SHALL be one that is short by exactly one resource where such a set
exists, so the report names the smallest group that cannot be satisfied rather than
restating the whole service. Where several deficient sets exist the check SHALL
report one of them deterministically, as the start-grid check reports one pair.

The check SHALL NOT reject, block, or alter any operation. Resolution, availability,
placement and service validation SHALL behave exactly as they did without it — in
particular, a count exceeding the eligible pool SHALL remain a valid service, since
resources may be added later.

#### Scenario: A count exceeding the eligible pool is reported
- **WHEN** a service has one role of count 2 and only one resource is eligible for it
- **THEN** the check reports that role as deficient, requiring 2 distinct resources against 1 eligible

#### Scenario: Two roles sharing a single eligible resource are reported
- **WHEN** two roles of one resource type both resolve to the same single resource
- **THEN** the check reports the two roles together, requiring 2 distinct resources against 1 eligible

#### Scenario: A sufficient pool is not reported as a positive finding
- **WHEN** the check finds that every role can be filled at once
- **THEN** it reports nothing at all, rather than reporting that the service is bookable, available, or has a start

#### Scenario: Overlapping pools are judged together, not role by role
- **WHEN** two roles of one type each have two eligible resources, but only two resources are eligible for either role in total, and each role requires a count of 2
- **THEN** the check reports the pair as deficient, even though each role considered alone has enough candidates

#### Scenario: Distinct resources satisfying one role each is sufficient
- **WHEN** two roles of one type require different capabilities and two resources are eligible, each satisfying exactly one of the roles
- **THEN** the check reports nothing, because an assignment exists

#### Scenario: Bookings do not change the answer
- **WHEN** the same configuration is checked before and after a booking fills every eligible resource's calendar
- **THEN** the check reports identically, because it is computed from eligibility rather than free time

#### Scenario: A sufficient pool says nothing about bookability
- **WHEN** a service's roles can be filled together but their resources are never open at the same time
- **THEN** the check reports nothing, and the service still has no bookable start — sufficiency is necessary and not sufficient

#### Scenario: The deficient set names roles rather than slots
- **WHEN** a role of count 3 is deficient
- **THEN** the report identifies that role and the number of distinct resources it requires, and does not enumerate its individual slots

#### Scenario: Detection changes nothing else
- **WHEN** a service whose pools are insufficient is resolved, saved, and queried for availability
- **THEN** resolution returns its candidate pools, saving succeeds, and availability returns an empty result exactly as it did before this check existed

## MODIFIED Requirements

### Requirement: All candidates failing reports one of two distinct outcomes
When every attempt fails, service placement SHALL report which kind of failure occurred rather than echoing the last attempt's failures, which would be arbitrary.

The unit of an attempt is a **saturating assignment** — one distinct resource per role slot — because placement accumulates every claimed resource's rules before the conflict check. For a single-role service of count 1 an assignment is one candidate, and everything below reads as it always did.

When any attempt failed on `conflict`, placement SHALL fail with `conflict`: the request described a genuinely bookable slot that was taken concurrently or is already occupied, so retrying may succeed. An attempt that was **not made** because the candidates were already claimed SHALL be classified as the attempt would have been — which requires that a saturating assignment exist among the candidates whose own rules admit the request, since otherwise no attempt could have reached the conflict check at all. "Every role has such a candidate" is not that test: a role of count 2 with a single admitting candidate satisfies it and still cannot be filled.

When every attempt rejected the request *deterministically* — the refusal is a property of the request against a resource's configuration, such as a start off its grid, outside its open hours, inside its lead time, or beyond its horizon — placement SHALL fail with the stable code `service-unavailable`. Retrying is pointless. A slot that cannot be filled by any rule-admitting candidate makes the whole request deterministic in this sense, however free the other slots are.

The deterministic refusals SHALL be recognised explicitly rather than inferred from "not `conflict`". A refusal that is neither a conflict nor a known deterministic rule — a candidate deleted between resolution and its attempt, for instance — is transient, and reporting it as `service-unavailable` would both tell the caller not to retry when retrying would succeed and pollute the drift signal.

`service-unavailable` SHALL be treated as a drift signal: a client that placed only starts and lengths taken from the service availability query cannot legitimately provoke it, so its occurrence from a conforming client means availability and placement disagree. Composite availability makes this more load-bearing rather than less, since a multi-role service offers strictly fewer starts than any of its roles alone.

Where the reason no assignment could be made is that too few resources could fill the
roles **at that instant**, the `service-unavailable` message SHALL be permitted to
say so, naming the roles that were short and how many distinct resources they needed
against how many were available. The deficient set SHALL come from the same
assignment the placement ran, not from a second computation, so the message cannot
describe a different failure from the one that occurred.

The **code** SHALL be unchanged by this, and SHALL remain `service-unavailable` with
its existing status mapping: codes are contract and messages are not, so a consumer
matching on the code SHALL be unaffected by the message becoming more specific.

The message SHALL describe that instant, not the configuration. A service whose
pools are structurally insufficient and one whose resources merely happen to be busy
produce the same code, and the configuration-time check is what distinguishes them;
a placement-time message SHALL NOT claim a configuration is permanently unfulfillable
on the evidence of one instant.

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

#### Scenario: The message names what was short
- **WHEN** a service requiring two `therapist` resources is placed at an instant where only one is free and able to take the request
- **THEN** placement fails with code `service-unavailable` and a message naming the role and stating that 2 distinct resources were needed against 1 available

#### Scenario: The code is unchanged by the richer message
- **WHEN** a consumer matches on the failure code for a placement that failed for insufficiency
- **THEN** the code is `service-unavailable` exactly as before, and its status mapping is unchanged

#### Scenario: A placement-time shortfall is not reported as a configuration fault
- **WHEN** a service whose pools are structurally sufficient is placed at an instant where too few resources are free
- **THEN** the message describes that instant, and does not state that the service can never be fulfilled
