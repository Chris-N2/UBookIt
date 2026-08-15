## Context

Every piece of machinery multi-role composition needs already exists, built
deliberately ahead of time:

- `ResourceClaim` is `(Guid ResourceId)`, and all of a booking's claims share its
  one interval.
- `SqlBookingStore.PlaceAsync` sorts every claim's resource id ascending, takes
  an app lock per resource inside a single transaction, and conflict-checks
  across all of them — under a ③ comment reading *"Ascending lock order is
  deadlock-proof for future multi-claim bookings."*
- ⑧a's D2 made resolution take `(ServiceRole, ServiceDuration)` rather than a
  service, so resolving several roles is calling it several times.

What has never been reachable is the behaviour. `Service.Create` rejects a
second role, so no code path has ever produced a two-claim booking.

The one thing genuinely absent is **composite availability**: today a service's
availability is a union over one role's candidates, and a multi-role service
needs the intersection across roles of those unions.

The reason multi-role has waited is not machinery but correctness. Two roles can
draw from overlapping pools, and first-available assignment then returns a wrong
answer. This change removes that possibility by construction rather than solving
it.

## Goals / Non-Goals

**Goals:**

- Let a service require several roles of distinct resource types, one resource
  each.
- Compose availability across roles correctly over heterogeneous per-resource
  grids.
- Make multi-claim placement reachable, and prove its atomicity rather than
  inherit the claim.
- Leave `⑨-2` a coherent change: matching, same-type roles, `Count > 1`.

**Non-Goals:**

- Assignment across overlapping pools, in availability or in placement.
- `Count > 1`.
- Reporting *why* two roles' grids never align (`⑨-1a`).
- An upper bound on granularity.
- Any change to ⑤'s direct-resource path.

## Decisions

### D1 — Distinct resource types, because disjoint pools are what make this safe

Every role in a service must name a different `ResourceType`. Rejected with a
new stable code; a service with two `therapist` roles fails whatever their
capabilities differ by.

The rule is not cosmetic and not arbitrary scope-cutting. Eligibility requires
`resource.Type == role.ResourceType`, and a resource has exactly one type.
Therefore:

```
distinct types  ⇒  no resource is eligible for two roles  ⇒  pools are disjoint
```

Disjointness is what makes every other decision in this change sound. It buys
three separate things, and it is worth naming all three because losing any one
of them is what `⑨-2` has to solve:

1. **Assignment is trivially optimal.** With disjoint pools, choosing each role
   a candidate independently can never strand another role.
2. **Availability composes as a plain set intersection.** With overlapping
   pools it would not: two roles drawing from `{Mary, Frank}` with only Mary
   free would each report "available" while the pair is not bookable.
3. **A preferred resource id stays unambiguous.** It identifies its role
   implicitly, because it belongs to exactly one type.

**Recorded as a v1 restriction, not a truth about services.** The spec states
the reason, so relaxing it in `⑨-2` is a relaxation — additive and safe. A rule
whose justification is not written down becomes a rule people design around.

*Alternative considered — allow same-type roles and assign greedily, fixing it
later.* Rejected outright: it does not fail, it answers wrongly, and it answers
wrongly in the direction of reporting unavailability. That is the defect class
this project has twice found only through adversarial review, and it would be
invisible to any test built from disjoint fixtures.

### D2 — Composite availability is an intersection of starts, then of lengths

For a service with roles `R₁…Rₙ`:

```
starts(service) = ⋂ᵢ starts(Rᵢ)          ── one interval, so one common start
lengths(S)      = ⋂ᵢ lengths(Rᵢ, S)      ── one interval, so one common length
```

`starts(Rᵢ)` is ⑦-2's existing union over role `i`'s candidates. Starts are
absolute instants, so intersecting them is set intersection with no alignment
logic — two roles on different granularities simply share fewer starts.

Lengths need slightly more care. Each role offers a *set* of `LengthRun`s at a
start, and set intersection distributes over union:

```
(A ∪ B) ∩ C  =  (A ∩ C) ∪ (B ∩ C)
```

so the composite is every pairwise run intersection, passed through ⑦-2's
existing `Collapse`. Roles are folded pairwise, left to right. Run counts per
start are already collapsed and small, so the cost is negligible.

### D3 — Two runs intersect by `lcm`, because every run is anchored at zero

A `LengthRun(Min, Max, Step)` is not a general arithmetic progression. Every run
the system produces satisfies `Min ≡ 0 (mod Step)`, so it denotes exactly *the
multiples of `Step` in `[Min, Max]`*. The chain that guarantees it:

```
BookingConstraints.Create   min, max must be granularity multiples   (enforced)
SlotProjector.Walk          maxRun = FloorTo(…, granularity)
ProjectBookableStarts       BookableStart(start, MinDuration, maxRun)
ServiceDuration.TryResolveAgainst   CeilTo / FloorTo by granularity
GetBookableStartsAsync      min = MaxOf(mult, mult), max = MinOf(mult, mult)
```

Given that, intersection is arithmetic rather than congruence-solving:

```
A ∩ B  =  multiples of lcm(sA,sB) in [max(minA,minB), min(maxA,maxB)]

L    = lcm(sA, sB)
Min  = CeilTo (max(minA,minB), L)
Max  = FloorTo(min(maxA,maxB), L)
empty iff Min > Max
```

and the result satisfies the same invariant, so the representation is closed
under intersection.

**The invariant is currently incidental, and this change makes it explicit.**
`LengthRun(5, 15, 10)` is constructible today, and `Admits` even honours its
phase via `(duration - Min) % Step`. ⑦-2's `Subsumes` already depends on the
invariant silently — its comment says *"both minima are multiples of that shared
step, so the two grids are in phase"* — and intersection would be the third
dependant. Three algorithms relying on an unenforced property is how a guard
ends up correct only by accident, which is a defect shape this project has hit
before. The invariant is therefore enforced where runs are constructed and
stated on the type.

**`lcm` is computed as `a / gcd(a, b) * b`** — dividing first, so the
intermediate cannot overflow where `a * b` would. Granularity has no upper bound
in the domain (a known, separately-logged hazard), so the arithmetic must be
safe even though bounding the input is another change's job.

### D4 — Placement claims one resource per role, and atomicity is proved rather than assumed

The claim set is one resource per role, and `IBookingStore.PlaceAsync` receives
it whole. No new store method and no new locking code: the SQL implementation
already sorts ids ascending and locks each inside one transaction.

That the code already looks right is **not** evidence. This repository's ③ QA
found a CRITICAL concurrency hole in an implementation that also looked right,
and the standing rule is that a concurrency claim needs a racing test designed
to fail against the broken version. So this change adds:

- Concurrent placements of two multi-claim bookings that **share one** of two
  resources: exactly one succeeds, the other fails `conflict`.
- A partial-overlap race, where booking 1 claims `{A, B}` and booking 2 claims
  `{B, C}` — the case a per-booking lock would pass and a per-resource lock
  catches.
- Proof that a failed multi-claim placement leaves **no** rows, since the
  candidate loop depends on it.

### D5 — The preview reports a chain per role

⑧a's summary becomes `n` chains, one per role, each labelled by its resource
type. Because the pools are disjoint, each chain remains independently true: no
role can consume a resource another role was counting.

That property is exactly what fails under `⑨-2`, and the spec says so, so the
summary's meaning does not silently change when same-type roles arrive.

The existing wording constraint carries forward unchanged: the summary describes
what **can provide** the service and never states or implies availability
(⑧a's D5). Composite availability makes that boundary more load-bearing rather
than less, because a service whose roles all resolve healthily can still be
unbookable for reasons the summary does not evaluate — see D6.

### D6 — Grids that never align are created here and diagnosed in `⑨-1a`

Intersecting starts introduces a new way for a correct-looking configuration to
be permanently unbookable. Start grids are anchored at each resource's free
interval and stepped by its own granularity, so two roles can miss each other
forever:

```
Role 1  room       opens 09:00  granularity 30  →  09:00  09:30  10:00  …
Role 2  therapist  opens 09:15  granularity 20  →  09:15  09:35  09:55  …

30k = 15 + 20m  ⇒  30k − 20m = 15
LHS divisible by gcd(30,20) = 10;  15 is not  ⇒  no solution, ever
```

Both roles look healthy in isolation. The service is never bookable. The test is
exact and cheap — grids align iff `gcd(step₁, step₂)` divides the offset between
their interval starts — but reporting it is a diagnostic with its own UI surface,
so it is `⑨-1a`.

**Deferring is safe, not merely tolerable.** ⑧a's D5 boundary means the
configuration summary asserts nothing about opening hours or bookable times, so
it is *correctly silent* here rather than falsified. What is missing is an
explanation in the booking flow, not a correction to something that lies. Had
the summary claimed availability, this deferral would not be available.

*Alternative considered — reject the configuration at save time.* Rejected: the
misalignment is a property of two **resources'** opening hours, not of the
service. Editing a resource can make the service bookable without touching it,
so refusing to save would block a configuration that is not wrong.

## Risks / Trade-offs

- **[Composite availability is much sparser than either role alone]** →
  Inherent, not a defect: two grids share only their `lcm`. It makes D6's
  diagnostic more valuable, which is why `⑨-1a` follows immediately.
- **[The distinct-type rule is read as permanent]** → D1 records the reason in
  the spec, and `⑨-2` is named as the relaxation. The rejection tests are built
  on overlapping-pool fixtures so they flip rather than get rewritten.
- **[Multi-claim placement looks free and therefore untested]** → D4; the racing
  tests are the deliverable, not the code.
- **[The zero-phase invariant is violated by a future constructor]** → D3
  enforces it where runs are built rather than trusting three call sites.
- **[Deferring the editor would strand multi-role services]** → It is in scope,
  for the reason ⑥ learned the hard way.

## Migration Plan

No schema change, no migration, no data backfill. Every existing service has one
role and continues to resolve identically — the intersection of one role's
availability with nothing is that role's availability, and a one-role claim set
is what placement already produces.

Rollback is reverting the change set. The regression gate is that every ⑤/⑥/⑦/⑧
scenario passes untouched, and specifically that a single-role service's
availability and placement are byte-for-byte what they were.

## Open Questions

- **Whether the composite starts query should short-circuit on the first role
  with no availability.** Cheap and obviously correct, but it changes which role
  a future diagnostic would blame for an empty result. Worth settling against
  `⑨-1a`'s needs rather than in advance.
- ~~**How the editor should present roles that cannot be reordered.**~~
  **Settled at apply.** `Service.Create` sorts roles ordinally by type key, so
  the aggregate holds one canonical order and every store agrees by
  construction rather than by an accident of query planning — the delivery
  contract's promise of a deterministic order needed an owner, and no read path
  was a good one. Validation still reports against the order the caller
  supplied, so a failure names the row the editor is showing.

  **Visible consequence, accepted:** a service reopened after a role is added
  shows its rows in type order rather than entry order. The editor renders the
  list it is given and nothing depends on the order, so this is a reordering,
  not a loss.
