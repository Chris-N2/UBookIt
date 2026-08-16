## Why

⑨-2 made a service able to require several resources at once — two roles of one
type, or a count above one — and deliberately **accepts** a configuration whose
count exceeds its eligible pool, because resources may be added later and refusing
the save would block something that is not wrong (⑨-2 design D8). The consequence
is a service that is permanently unfulfillable for a purely structural reason, with
nothing anywhere saying so: availability correctly returns no starts, placement
correctly returns `service-unavailable`, and every resolution chain reports
healthy, because each role really does have candidates. They just cannot be filled
*at the same time*.

⑨-2 also removed the one signal that used to fire nearby. Its design D7 made the
start-grid check skip a resource paired with itself, so two roles drawing on a
single-resource pool now correctly report **no misalignment** — they are short of
resources rather than misaligned. That silence is right and leaves a hole exactly
where this change lands; ⑨-2 shipped a test naming it.

The information needed already exists. A saturating assignment fails only for
Hall's reason: some set of role slots has fewer distinct eligible resources between
them than it has slots. The failing augmenting walk *is* that witness — ⑨-2 simply
discarded it, on the ground that inventing a shape with no consumer is a liability.
This change is the consumer.

## What Changes

- **The assignment yields a witness instead of a bare failure.**
  `SlotAssignment.TrySaturate` currently returns `null` when no saturating
  assignment exists. It will return the **deficient set**: the slots that cannot all
  be filled, and the distinct resources they were competing for. When a slot's
  augmenting walk fails, the resources it reached are that set's neighbourhood — so
  this is a return-type change on one function, not a new algorithm. Not a breaking
  change: `SlotAssignment` is `internal`.

- **A configuration-time pool-sufficiency check**, in Core, over the same resolved
  `RoleCandidates` the booking path acts on — the shape `StartAlignment` already
  established. It reports that a service can never be fulfilled *as configured*:
  "this service needs 2 `therapist` with `cert-x`; only 1 resource qualifies". Like
  the alignment check it is **one-directional** — it may report that no assignment
  can ever exist, and SHALL NOT report that one does, because sufficiency of the
  pool says nothing about opening hours, lead time, horizon, or existing bookings.

- **A richer `service-unavailable` message**, derived from the *same* witness asked
  at placement time rather than recomputed: "two therapists are needed and only one
  is free then" instead of the current generic refusal. The stable code is
  unchanged — codes are contract, messages are not — so no consumer breaks.

- **The configuration preview endpoint carries the sufficiency report**, beside the
  resolution chains and the misalignment report, obtained from Core rather than
  computed in the backoffice so the two cannot disagree.

- **The services editor renders it**, as a statement beside the existing reports.

- **The services collection view stops hiding what distinguishes two roles.** It
  currently summarises a service of two `therapist` roles as
  "1 × therapist-mrm, 1 × therapist-mrm" — the differing required capabilities that
  make the configuration *legal* are not shown, so a configuration the domain now
  accepts renders identically to one it rejects with a message telling the editor to
  use a count instead.

- **The resolution-chain readout's presentation follows.** ⑨-2's sync corrected the
  requirement's justification (pools can overlap, so a combined count would
  double-count) but not what the editor shows. The chains still read as though each
  role's pool were its own, which is the misreading the sufficiency report exists to
  correct — and they are the surface it lands on. Repairing one without the other
  leaves the same screen half-corrected.

## Capabilities

### New Capabilities

None. Every operation extends behaviour an existing spec already owns.

### Modified Capabilities

- `service-booking`: gains the pool-sufficiency check as its own requirement, and
  modifies the all-fail outcome requirement so the `service-unavailable` message may
  name what was short. The code, its 400 mapping, and its standing role as a drift
  signal are unchanged.
- `resource-management`: the service configuration preview endpoint carries the
  sufficiency report alongside the chains and the misalignment report.
- `services`: the editor reports pool insufficiency; the collection view's role
  summary distinguishes two roles of one type; the resolution-chain readout's
  presentation stops implying disjoint pools.

## Non-goals

- **Rejecting an insufficient configuration at save time.** ⑨-2 design D8 decided
  the opposite deliberately and asserts it in a test: a count exceeding the pool is a
  property of the *pool*, not of the service, and resources may be added later. This
  change reports; it must not block. Reversing that would also break the setup order
  an editor naturally works in — define the service, then add the people.

- **Asserting availability.** The standing constraint from ⑧a design D5 holds
  unchanged: eligibility is not availability. A sufficient pool means only that an
  assignment is *not structurally impossible*; it says nothing about open hours, lead
  time, horizon, granularity, or existing bookings. The wording may say a service can
  never be fulfilled; it may never say one is bookable, free, or available.

- **A delivery-API reason code.** ⑨-1a deferred one to ⑩ deliberately, and the same
  reasoning applies here: an empty `bookable-starts` response is still
  indistinguishable from a fully booked week, but ⑩ is the first consumer that could
  act on a code, and adding one now is contract surface with nothing behind it.

- **Per-role preference**, and **whether direct resource booking survives to v1** —
  both still ⑩'s, unchanged by this change.

- **Bounding resource granularity.** Still logged separately.

## Impact

**Core** — `SlotAssignment` returns a witness; a new sufficiency check over resolved
pools; the all-fail classification in `ServiceBookingService` uses the witness for
its message. No change to what any of them *decide* — only to what they can explain.

**Backoffice** — the preview response gains a member; the editor gains a report; the
collection view's summary and the chain readout's presentation change.

**Delivery API** — unchanged in contract. A richer message flows through the
existing `{ code, message, field }` echo without a spec change, because messages are
not contract.

**Persistence** — unchanged. No schema change, no migration.

**Tests** — the new ground is the witness itself (a deficient set is a claim about
*which* slots and *which* resources, so it needs fixtures where the naive answer and
the minimal one differ), and the two-surfaces-agree property: the configuration-time
report and the placement-time message must come from one computation and must not be
assertable separately.
