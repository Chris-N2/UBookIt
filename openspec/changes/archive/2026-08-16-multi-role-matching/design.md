## Context

⑨-1 composes a multi-role service's availability as an intersection across roles
and places a booking by walking the cartesian product of the roles' candidate
shortlists. Both are correct **only because the pools are disjoint**: a resource
has exactly one type, every role names a distinct type, so no resource can be
counted for two roles. ⑨-1's own design D1 states this as the reason independent
per-role assignment is optimal rather than merely adequate.

This change removes the premise, so both algorithms have to be rebuilt on
something that survives without it. Three existing decisions constrain how:

- **⑦-2's determinism.** Candidate order is by resource id, and repeated identical
  requests attempt the same combinations in the same sequence. Whatever replaces
  the combination loop must stay reproducible, or the failure classification and
  the placement tests lose their footing.
- **⑧a design D5 / ⑨-1a design D1 — never over-claim.** Availability that offers a
  start no assignment can fill is the same category of lie as a configuration
  summary claiming a service is bookable. Correctness here is not negotiable
  against cost.
- **⑨-1a's diagnostic.** It reasons pairwise over candidates and is silenced by a
  resource paired with itself once pools overlap.

Two things are already in place and are worth stating so they are not rediscovered:
`Count` is a stored column from ⑥ with no unique index on `(ServiceId, ResourceType)`,
so **no migration**; and the editor never enforced the duplicate-type rule (⑨-1 D1),
so it already submits and previews duplicate types.

## Goals / Non-Goals

**Goals:**

- Same-type roles and `Count > 1`, on one assignment mechanism.
- Availability that offers exactly the starts and lengths an assignment can fill.
- Placement that never claims a resource twice and never gives up on a service that
  was fulfillable.
- Byte-identical behaviour for every service whose roles name distinct types.
- Determinism preserved end to end.

**Non-Goals:**

- The pool-sufficiency diagnostic and the richer unavailability message (⑨-2a).
- Per-role preference; fair or round-robin assignment.
- Any change to the placement *contract* — `place(interval, booker, {claims})`
  stays the universal primitive, and a service still derives a claim set onto it.

## Decisions

### D1 — Roles expand to slots, and assignment is a bipartite matching

A role of count *N* contributes *N* interchangeable **slots**. A slot may be filled
by any candidate of its role; a resource fills at most one slot. A service is
fulfillable at an instant exactly when a matching saturating every slot exists.

Stated this way, same-type roles and `Count > 1` are the *same* problem rather than
two, which is why they ship together: two identical roles are two slots drawn from
one pool, and so is a count of two.

*Algorithm:* augmenting paths (Kuhn's). Pools are small — tens, not thousands — the
work is in-memory over already-loaded candidates, and the complexity is `O(V·E)`.
Hopcroft–Karp is asymptotically better and not worth its complexity at this size;
if pools ever grow, the seam is one function.

*Deliberately not built:* a strategy seam for selecting between algorithms. A site
with thousands of eligible resources for one role would justify Hopcroft–Karp, and a
pluggable selection is the natural shape for that — but it is a shape to adopt when a
second algorithm exists, not before. One implementation behind one function is
already the seam; introducing the abstraction now would add a decision point with
nothing to decide between, and this repository's standing rule is that an
abstraction with a single implementation is a liability rather than a design.

*Determinism:* slots are processed in role order then slot index, and each slot
tries candidates in resource-id order — the order ⑦-2 already established. Identical
inputs therefore yield an identical assignment, which is what keeps placement
reproducible and lets a test assert *which* resources were claimed rather than only
that some were.

*Alternative considered — keep the cartesian product and filter out combinations
that reuse a resource.* Rejected on cost and on honesty: the product is exponential
in the number of roles, and the filter would silently do a matching's job badly,
since a greedy walk that strands a role would report a service unavailable when it
was bookable. That is the exact failure `service-role-duplicate-type` exists to
prevent, and it would return wearing different clothes.

### D2 — Availability asks the assignment question per start **and** per length

Feasibility is not a property of a start alone. Whether a resource can fill a slot
at a start depends on the **length**: a candidate's resolved range and granularity
decide which lengths it admits there. So the bipartite graph is a function of
`(start, length)`, and availability is computed by asking, for each start, which
lengths admit a saturating matching.

The result cannot always be one `LengthRun`. Pools of `{30,90}`, `{30,90}` and
`{60}` across two roles yield feasible lengths `{30, 90}` — a run of step 60 whose
minimum is not a multiple of 60, which the anchored-at-step invariant forbids.
`ServiceBookableStart` already carries a **set** of runs, so the shape survives; the
composition does not. Runs are rebuilt from the feasible length set rather than
intersected pairwise.

*Cost:* a matching per candidate length per start. Lengths come from the union of
the candidates' runs and are bounded by the coarsest granularity across the pool;
starts are bounded by the query range cap. Everything is arithmetic over data
already in memory — no additional I/O, and the existing single batched claims read
is unchanged.

*Alternative considered — decide feasibility per start only, ignoring length.*
Rejected: it re-creates the over-claim in a smaller form, offering a start whose
advertised lengths no assignment can deliver.

*Alternative considered — keep union-then-intersection and let placement fail.*
Rejected outright; see Context.

### D3 — Distinct-type services must be provably unchanged

Where every role names a distinct type the pools are disjoint, and a saturating
matching exists exactly when each role independently has a candidate — so the new
algorithm agrees with the old one by construction. This is not an optimisation to
be asserted; it is the property that makes the change safe for every service that
exists today, and it is tested **differentially**: the same configurations, the same
range, old expectations unchanged.

### D4 — A preferred resource constrains the booking, not a role

`PreferredResourceId` currently orders one pool, because a resource belongs to
exactly one role. Once it can be eligible for several, "prefer this resource" no
longer names a slot. It is therefore read as **"this resource must appear somewhere
in the booking"**: the assignment is asked for a saturating matching that includes
it, and only if none exists does the preference fall through as it does today.

The request contract is unchanged, which matters — a per-role preference would need
a shape no consumer has yet, and ⑩'s "pick who" UI is the first plausible one.

The existing rejection of a preference naming a resource outside every pool
(`resource-not-eligible`, ⑦-2 design D9) is unchanged and still derivable from
public reads.

### D5 — Two roles identical in type and capabilities are rejected, pointing at the count

They are two spellings of one requirement. Allowing both would leave two
representations that compare unequal and publish differently while meaning the same
thing; merging them silently would rewrite an editor's two rows into one row of two.
So `service-role-duplicate-type` is **narrowed** rather than retired: it now fires
only when type *and* required capabilities match, and its message names the count as
the correction.

Two roles of one type requiring *different* capabilities stay valid — that is the
case the whole change exists for ("a senior therapist and any therapist").

**How this is expected to be used, which matters for testing.** Chris's observation
from practice: a capability is usually needed by *one* of several resources, not all
of them — two therapists of whom one holds `cert-x`. That is two roles of count 1
with differing capabilities, **not** one role of count 2 requiring `cert-x`, which
would demand the certificate of both. Capabilities on a counted role remain
meaningful and are not restricted ("three rooms, each with a projector"), so the
model keeps both.

The consequence is a trap for the test suite rather than for the model. Pools for one
resource type are nested exactly when the capability sets are comparable, and the
common shape — `{cert-x}` against `{}` — *is* nested. **Greedy assignment is optimal
over nested pools.** A suite built only from realistic configurations would therefore
pass a greedy implementation and demonstrate nothing about the assignment. Fixtures
must include **incomparable** capability sets (`{cert-x}` against `{welsh}`), where
neither pool contains the other and greedy genuinely fails.

### D6 — Canonical role ordering needs a tiebreak, or round-tripping breaks

`Service.Create` sorts roles by type key and documents that as a total order because
type keys are unique across a service — the rule being lifted. `List.Sort` is
unstable, so two same-type roles could exchange places between saves, which would
break round-trip equality and the delivery contract's promise of a deterministic
role order.

Ordering becomes: type key, then the ordinal comparison of the role's sorted
capability keys, then count. Two roles cannot tie on all three, because D5 rejects
exactly that case — so the tiebreak is total precisely *because* of D5, and the two
decisions have to move together.

### D7 — The alignment diagnostic skips a resource paired with itself, and nothing else

⑨-1a compares candidate pairs across two roles. Once pools overlap, `(r, r)` has a
zero offset, `gcd` divides it, and the check exits reporting nothing — silenced by
an assignment that can never happen, since a booking cannot claim one resource
twice.

The fix is to skip pairings of equal resource ids. It must **not** skip same-type
role pairs wholesale: two roles of one type requiring different capabilities can
draw disjoint sets of resources, so a genuine permanent misalignment between them
remains possible and must still be reported. With the fix, two roles over a
single-resource pool compare nothing and stay silent — correct, because that is an
insufficient-pool fault for ⑨-2a rather than a misalignment.

### D8 — Count is bounded at validation

Each unit is a slot the assignment must fill, so an unbounded count is unbounded
work for a configuration that cannot succeed. `Count` is validated as at least 1 and
at most a fixed ceiling, with its own stable failure code, on the reasoning that
made `NormalizedKey.MaxLength` turn an over-long key into a validation failure
rather than a 500 at INSERT.

The ceiling is a sanity bound, not a domain claim: a service needing more than a few
dozen of one resource type is a different kind of product. **A count exceeding the
number of eligible resources is NOT rejected** — that is a property of the pool
rather than of the service, resources can be added later, and refusing the save
would repeat the mistake ⑨-1a explicitly avoided. ⑨-2a reports it.

## Risks / Trade-offs

- **[Availability cost grows from set intersection to a matching per (start, length)]**
  → Bounded and in-memory: pools are small, lengths are bounded by the coarsest
  granularity, starts by the existing query-range cap, and there is no extra I/O.
  The early exits are the common cases — a slot with one candidate, and a
  distinct-type service, both resolve without search.

- **[A subtle matching bug silently under-reports availability]** → This is the
  dangerous failure, because it looks like ordinary unavailability. Mitigated by the
  differential test in D3 (distinct-type services must answer exactly as before) and
  by tests over pools where a greedy choice strands a slot. Note the trap recorded in
  D5: the *realistic* configuration shape has nested pools, over which greedy is
  optimal, so fixtures drawn from realistic examples alone would not detect a greedy
  implementation at all.

- **[`MODIFIED` requirements silently drop guarantees]** → The real procedural risk:
  unlike ⑨-1a, this change replaces requirements wholesale in three specs. Every
  modified requirement gets a guarantee-by-guarantee diff against its current text,
  as `CLAUDE.md` requires, and carried-forward guarantees need a scenario rather than
  an assumption.

- **[The self-pairing fix over-corrects into skipping same-type role pairs]** → It
  would silence a real misalignment between two differently-capable roles of one
  type. Covered by a test with disjoint capability sets on one type.

- **[Determinism lost in the matching]** → Slot and candidate iteration orders are
  fixed and asserted, not incidental.

## Migration Plan

No schema change, no migration, no backfill — verified against the model snapshot
rather than assumed. Every service that exists today has roles of distinct types and
count 1, and D3 guarantees those answer exactly as they do now. Reverting the change
restores the previous restrictions; a service saved with same-type roles or a count
above 1 would then fail validation on next save, which is the ordinary consequence
of reverting a widened rule and needs no data repair.

## Open Questions

- **The exact count ceiling.** A sanity bound rather than a domain claim; worth a
  moment's thought at apply, and trivially adjustable.
- **Whether the editor should surface a count control on every row or only when a
  count above 1 is set.** A presentation call best made once it is on screen —
  the same call ⑨-1a deferred about wording a many-resource clash.
