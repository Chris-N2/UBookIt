## Context

⑨-1 composes a multi-role service's availability as an intersection of its
roles' start grids. Those grids are not free-floating: a candidate's starts
advance in granularity steps from a free-interval start, and the availability
spec fixes that in "Slot projection" and "Bookable-start projection". Two roles
can therefore share no instant at all, on any day, for reasons visible nowhere
in the product.

Three existing decisions constrain what this change may do, and all three are
load-bearing rather than stylistic:

- **⑧a design D5 — eligibility is not availability.** The configuration
  resolution chains describe type, capabilities and duration range, and say
  nothing about opening hours, lead time, horizon, or existing bookings. That
  boundary is what made deferring this diagnostic *safe* rather than merely
  tolerable: the summary is correctly silent, not wrong. Anything this change
  adds must sit beside the chains, not inside them.
- **⑨-1's own reasoning for deferring** — misalignment is a property of two
  **resources'** opening hours, not of the service, so refusing to save would
  block a configuration that is not wrong.
- **⑨-1 design D1** — every role names a distinct resource type, so the pools
  are disjoint. That is what lets this check reason pairwise over roles without
  worrying that one resource fills two of them.

## Goals / Non-Goals

**Goals:**

- Detect, exactly, when a service's roles can never share a start.
- Attribute it: which roles, which resources, which opening times and steps.
- Say nothing at all in every other case.
- Leave saving, resolution, availability and placement untouched.

**Non-Goals:**

- Reporting that a service *is* bookable, or that a start exists.
- Explaining an empty availability response for ordinary reasons (fully booked,
  lead time, horizon, exception dates).
- Any delivery-API surface; that arrives with `⑩`.
- Suggesting a repair.

## Decisions

### D1 — The claim is one-directional: the check may say "never", never "yes"

The check reports impossibility or stays silent. It is never evidence that a
service can be booked.

This is not caution for its own sake. Sharing a grid instant is *necessary* for
a bookable start and nowhere near *sufficient*: the instant must also fall in
free time on both resources, satisfy both lead times and horizons, and leave
room for a length both permit. A diagnostic that said "these roles align" would
be read as "this service works", which is precisely the over-claim ⑧a's D5
exists to prevent — and it would be read that way most confidently by the person
least able to check it.

One-directional also makes the implementation honest about its own
approximation. It is allowed to be conservative — to stay silent when a service
is in fact unbookable — but never to accuse a configuration that can work.

*Alternative considered — report an alignment score or "next aligned start".*
Rejected: both are positive claims, and the second is a promise about a specific
time that free time can invalidate a moment later.

### D2 — Compute over **open windows**, not free intervals, and that is sound

A candidate's starts advance from each *free-interval* start, and free intervals
depend on bookings. The check nevertheless computes over the resource's
configured **open windows**, which depend only on configuration.

That is sound, and the argument is the reason this diagnostic can be structural
at all:

```
placement aligns a start to its OPEN WINDOW's start (bookings spec, rule 7)
a booking's length is a multiple of its resource's granularity
  ⇒ a booking's end is on the open-window grid
  ⇒ every free-interval start is on the open-window grid
  ⇒ every candidate start is on the open-window grid
```

So the window grid is a **superset** of every start the resource can ever offer.
If two roles' window grids share no instant, no pair of their candidates can
share a start, whatever the bookings. The converse does not hold — sharing a
window-grid instant does not mean either resource is free then — which is
exactly the asymmetry D1 requires.

Computing over free intervals would also make the diagnostic depend on the
booking calendar, so it would appear and disappear as bookings came and went.
A structural fault must not flicker.

### D3 — Two grids meet iff `gcd(s₁, s₂)` divides the offset between their anchors

For windows anchored at `a₁`, `a₂` with granularities `s₁`, `s₂`, a shared
instant is a solution of `a₁ + k·s₁ = a₂ + m·s₂`, i.e. `k·s₁ − m·s₂ = a₂ − a₁`.
By Bézout this has an integer solution iff `gcd(s₁, s₂) | (a₂ − a₁)`.

Two caveats, both handled rather than assumed away:

- **Integer solutions may be negative** — the equation is over the *infinite*
  grids, while real windows are bounded. So the test as stated is necessary but
  not sufficient for a start to exist inside the two windows. Under D1 that is
  the right direction: failing it proves impossibility, passing it proves
  nothing, and the check stays silent.
- **Three or more roles.** A system of congruences is solvable iff it is
  solvable **pairwise** (`gcd(mᵢ, mⱼ) | (aᵢ − aⱼ)` for every pair). So the check
  remains pairwise, and reporting the first clashing pair is a complete
  explanation rather than one symptom of several.

### D4 — A role clashes only when **every** pairing fails

A role is a pool, not a resource. The service can be booked if *some* candidate
of each role aligns, so the diagnostic fires only when, for two roles, no
candidate of one shares a grid with any candidate of the other — across every
day both are open.

That quantification is what stops the report firing on a large pool where one
awkward resource happens not to line up. It also sets the cost: for two roles it
is `|A| × |B|` window pairings per day, each an integer `gcd`. Pools are small
in practice, the work is arithmetic with no I/O, and it runs only when the
backoffice asks — but the implementation must not turn it into a query per
candidate.

### D5 — Days are compared in local wall-clock terms, so DST cannot fake a clash

Open hours are weekly wall-clock windows; a window's UTC instant moves by an
hour across a DST boundary, and the *offset between two windows* moves with it
only if the two are in different zones — which, for a single site, they are not.
Comparing two windows on the same local date in the site zone therefore keeps
the offset stable and the arithmetic honest.

The check compares windows that fall on the **same local date**, over the days
of the week both roles are open. Exception dates that close a day remove it from
consideration; an exception that *opens* different hours is a window like any
other on that date.

*Alternative considered — compute over UTC instants across a fixed horizon.*
Rejected: it makes the answer depend on which horizon was swept, and a service
could be declared misaligned in one query range and not another. A structural
claim must not depend on when it was asked.

### D6 — The finding sits beside the chains, not inside them

The preview response gains a member of its own, and the editor renders it as a
separate statement. The resolution chains keep their exact meaning: they still
describe type, capabilities and duration, and still say nothing about opening
hours.

Folding it in would break ⑧a's D5 in the most literal way — a chain line that
mentioned opening times would be the summary claiming something about
availability. Keeping it separate also keeps the wording rules separate: the
chains may not use availability vocabulary, while this statement is *about*
whether a start can exist and must say so plainly.

*Alternative considered — a fourth stage on each role's chain.* Rejected: the
chain's stages are successive filters over one role's pool, each a subset of the
last. Misalignment is a property of a **pair** of roles and belongs to neither.

## Risks / Trade-offs

- **[The check is conservative and stays silent on a real fault]** → Accepted
  and deliberate (D1). Silence is the failure mode we choose, because the other
  one accuses a configuration that works.
- **[An editor reads silence as "this service is bookable"]** → The wording
  never asserts the positive, and the summary beside it already avoids
  availability vocabulary. Worth watching in the live pass.
- **[Cost on large pools]** → `|A| × |B|` per open day, pure arithmetic, run on
  demand. If it ever matters, the pairwise loop can stop at the first aligning
  pair — which is the common case and is already the early exit.
- **[The diagnostic drifts from the real grid rule]** → It derives its anchors
  and steps from the same resource configuration the projector uses, and the
  tests enumerate real projected starts to confirm that a reported clash really
  does yield no shared start.

## Migration Plan

No schema change, no migration, no data backfill. The check is additive and
read-only; reverting it removes a report and changes no stored state.

## Open Questions

- **Whether the editor should report a clash between two roles that currently
  have no resources.** With an empty pool there is nothing to align, so the
  check is silent — but so is everything else, and the chain already says the
  pool is empty. Probably right to leave, worth confirming against the real
  editor.
- **How to word a clash involving many resources.** Naming one offending pair is
  the plan; whether an editor with ten rooms wants "and 9 more" or a count is a
  presentation call best made once it is on screen.
