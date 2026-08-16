## Context

⑨-2 built the assignment and deliberately threw away its failure reason.
`SlotAssignment.TrySaturate` returns `null`, on the stated ground that inventing a
witness shape with no consumer is the liability design D1 argues against. That was
right at the time and this change is the consumer.

Three existing decisions constrain how:

- **⑧a design D5 — eligibility is not availability.** The configuration surfaces
  report what a configuration *is*, never what is bookable. This report must be
  one-directional in the same way ⑨-1a's alignment check is: it may say a service
  can never be fulfilled; it may not say one can.
- **⑨-2 design D8 — an insufficient pool is accepted, not rejected.** A count above
  the eligible pool is a property of the pool, resources may be added later, and a
  test asserts the save succeeds. This change reports; it must not block.
- **⑨-1a design D2 — a structural report must not consult the booking calendar.**
  The alignment check computes over configured open windows rather than free
  intervals precisely so a structural fault cannot appear and disappear as bookings
  come and go.

⑨-2 also left the neighbouring signal deliberately silent: two roles over a
single-resource pool now report no misalignment, correctly, because they are short
of resources rather than misaligned. There is a test naming that case as this
change's.

## Goals / Non-Goals

**Goals:**

- A witness from the assignment, in terms an editor can act on.
- A configuration-time report that a service can never be fulfilled as configured.
- A placement-time message that says what was short.
- Both from one implementation of the rule.
- The two backoffice surfaces that currently mislead about overlapping pools.

**Non-Goals:** as the proposal states — no save-time rejection, no availability
claim, no delivery-API reason code, no per-role preference.

## Decisions

### D1 — The witness is a deficient set of *roles*, not of slots

Hall's condition fails on a set of **slots**, but slots are an internal expansion —
a role of count 3 is three of them, indistinguishable from each other. An editor
does not have slots; it has rows. The witness is therefore collapsed to roles before
it leaves Core: *these roles, needing this many distinct resources between them, can
draw on only this many*.

The arithmetic survives the collapse because slots of one role are interchangeable:
if a deficient set contains any slot of a role it may as well contain all of them,
and including the rest only adds slots without adding neighbours, so the set stays
deficient. Collapsing therefore cannot turn a true witness into a false one.

*Alternative considered — report the raw slot set.* Rejected: it would expose the
expansion as contract, and force every consumer to re-derive which row a slot came
from.

### D2 — "One witness" means one *function*, not one value

⑨-2's handover says both outputs should derive from the same witness rather than
recomputing. Taken literally that is impossible, and the distinction matters enough
to state: the two questions run over **different graphs**.

- **Configuration time** asks: can these roles be filled *at all*, by the resources
  that exist? The graph is eligibility — type, then required capabilities, then a
  duration the service permits — and nothing else.
- **Placement time** asks: can they be filled *at this instant*? The graph is the
  candidates whose own rules admit the request and which were not already claimed.

Same predicate, same code, two inputs. The seam is `SlotAssignment` itself: one
function that takes a bipartite graph and returns either an assignment or a
deficient set. Neither caller reimplements Hall's condition, which is the actual
guarantee ⑧a D1 and ⑨-1's classifier seam exist to enforce — two implementations
free to disagree is the fault, not two evaluations.

### D3 — The configuration check computes over eligibility only, never the calendar

Following ⑨-1a D2 exactly. If the report consulted free time it would appear and
disappear as bookings came and went, and would withdraw itself precisely while an
editor was performing the repair it asked for.

This makes the report **strictly weaker than "bookable"**, deliberately: a service
whose pools are sufficient may still never be bookable, because the resources are
never free together, their grids never coincide, or their open hours never overlap.
That is the one-directional stance, and it is why the wording may not imply
availability.

### D4 — The witness comes from the failed augmenting walk, so it costs nothing extra

When Kuhn's fails to fill a slot, the walk has already visited exactly the resources
reachable from it through alternating paths. Those resources, and the slots matched
to them plus the unfilled slot, are a deficient set: `|N(S)| = |S| - 1`. So the
witness is a by-product of the failure, not a second search — no subset enumeration,
which would be exponential.

It is a *tight* witness (short by exactly one), which is the useful kind: it names
the smallest group that cannot be satisfied rather than the whole configuration.
Deterministic by the slot order already fixed in ⑨-2.

### D5 — Two roles of one type are summarised by what distinguishes them

The collection view renders a service's requirements in one table cell, so the
summary must stay short while making two same-type roles tellable apart. The
distinguishing fact is the required capabilities, so they belong in the summary when
— and only when — two roles share a resource type. A service of a `room` and a
`therapist` needs no capability text to be unambiguous, and adding it everywhere
would make the common case noisier to fix the uncommon one.

*Alternative considered — always show capabilities.* Rejected on that noise
argument. *Alternative considered — merge same-type roles into one entry.* Rejected:
it would render two rows as one, which is exactly the conflation the duplicate-role
rule exists to prevent.

### D6 — The chain readout keeps per-role chains and gains a joint statement

Each role's chain is still true of the role it describes: it states what that role
resolves to, not what remains once another role has taken someone. That is worth
keeping — it is what makes a chain attributable to one row's controls.

What is missing is the joint claim, and that is exactly what the sufficiency report
is. So the readout does not change its chains; it stops *implying* independence
where the pools overlap, and the sufficiency report carries the joint fact beside it.

*Alternative considered — make the chains themselves account for competition* (role
2's count net of what role 1 took). Rejected: there is no canonical "what role 1
took" — that is the assignment's choice and it varies by instant — so the number
would be arbitrary, unattributable to a control, and would change under the editor
without the configuration changing.

## Risks / Trade-offs

- **[The report says "sufficient" and the service is still never bookable]** → By
  construction, and stated in the requirement rather than hidden: sufficiency is
  necessary and not sufficient. The alignment check already occupies one of the other
  reasons; open hours and free time are not structural and belong to availability.

- **[The wording drifts into asserting availability]** → The standing ⑧a D5
  constraint, now with two prior surfaces already worded under it. The requirement
  states the prohibited vocabulary explicitly (*available*, *free*, *bookable*).

- **[The collapse from slots to roles produces a misleading witness]** → Argued in
  D1: adding a role's remaining slots adds no neighbours, so deficiency is preserved.
  Worth a test that a count-based deficiency and a same-type-roles deficiency both
  report the roles an editor would expect.

- **[Two evaluations disagree]** → They are two calls to one function over different
  graphs (D2). The risk is not divergent logic but divergent *inputs* being presented
  as the same claim, so the two reports must be worded to say which question they
  answer: one about the configuration, one about an instant.

- **[A tight witness names fewer roles than the editor expects]** → "These 2 roles
  can draw on only 1 resource" is more actionable than restating the whole service,
  but it may look incomplete when several groups are deficient at once. Reporting the
  first is consistent with the alignment check's one-witness stance.

## Migration Plan

No schema change, no migration, no contract change. `SlotAssignment` is `internal`,
so its return-type change is not a public break. The `service-unavailable` code and
its 400 mapping are unchanged; only the message it carries becomes more specific,
and messages are explicitly not contract (`delivery-api`, problem-details mapping).

## Open Questions

- **Whether the sufficiency report should name the qualifying resources** or only
  count them. Naming them is more actionable for a small pool and unreadable for a
  large one; the alignment check names exactly two because a pair is its unit. Decide
  once it is on screen.
- **Whether the collection view shows capabilities for a counted role** (`2 × room
  (projector)`) or only where two roles share a type. D5 settles the same-type case;
  the counted case is a presentation call best made against a real list.
