## Context

Change ⑥ made services definable, ⑦-1 made their duration expressive and left
`ServiceDuration.TryResolveAgainst` deliberately uncalled. This change gives it
its caller: a service becomes bookable end to end over the delivery API.

The relevant existing shape, all of which this change consumes rather than
alters:

- `ResourceClaim` is `(Guid ResourceId)`, and `Booking.Create` /
  `IBookingStore.PlaceAsync` already take a claim *set*. Single-claim is v1
  behaviour, not structure. A service therefore never owns placement — it
  *derives* a claim set over the universal primitive.
- `SlotProjector.Walk` yields `(start, maxRun)` per resource, with `maxRun`
  already capped at the resource's maximum and floored to its granularity.
  `ProjectBookableStarts` turns that into `BookableStart(start, min, maxRun)`.
- `IBookingStore.PlaceAsync` is atomic per resource and — proven by change ③'s
  QA — leaves no rows behind on failure.
- `ApiResults` maps `conflict` → 409, two not-found codes → 404, everything else
  → 400.

Two constraints frame every decision below. First, **everything optional**: ⑤'s
direct-resource path must behave identically whether or not services exist.
Second, **⑦-1's design D13** — a write path must never substitute a length for
the one the booker asked for; that defect shipped once and QA caught it only
because a spec scenario contradicted a green test.

## Goals / Non-Goals

**Goals:**

- Resolve a service's single role to eligible resources by type key.
- Answer "when can I book this service, and for how long" over a candidate pool
  whose members have different granularities, minimums, maximums, open hours,
  lead times, and horizons.
- Place a service booking by looping candidates over the existing atomic
  placement, with a deterministic outcome and no partial state.
- Report all-candidates-failed in a way that distinguishes "try again" from
  "this was never bookable", and make the latter a drift detector.
- Reuse the existing availability computation rather than growing a parallel
  one.

**Non-Goals:**

- Capability-based eligibility (⑧), multi-role composition (⑨), any front-end
  (⑩). No `services/{id}/slots?durationMinutes=` projection. No candidate-pool
  cap. No fairness or round-robin ordering. See the proposal's Non-goals.

## Decisions

### D1 — Eligibility is a new read-port method, not a reuse of `ListTypesAsync`

`IResourceManagementStore.ListTypesAsync` returns `(type, count)` pairs for the
backoffice picker. Eligibility needs whole `Resource` aggregates of one type —
constraints for `TryResolveAgainst`, open hours and exceptions for availability.
That is different data, so nothing moves or is duplicated across ports;
`ListTypesAsync` stays exactly where it is, management-only.

`IResourceStore` gains:

```csharp
Task<IReadOnlyList<Resource>> ListByTypeAsync(string type, CancellationToken ct = default);
```

*Alternative rejected:* filtering `ListAsync(skip, take)` in memory. It clamps
`take` to 500, so the pool would be silently truncated — see D2.

### D2 — The candidate pool is unpaged, and deliberately uncapped

A truncated candidate pool does not error; it reports unavailability that does
not exist, or books a resource that is not the right one. That is the
invisible-to-green-tests defect class, so `ListByTypeAsync` takes no paging
parameters at all rather than defaulting them generously — the absence of the
parameter is the guarantee.

Cost, accepted knowingly (Chris, 2026-08-13): a pool of N resources costs one
type query, one batched claims query, and N in-memory availability
computations. At realistic single-role pool sizes (tens) this is unremarkable;
at thousands it would be slow. A cap that silently truncates would be wrong, and
a cap that fails loudly has no motivating case yet, so neither ships. If one
appears, the fix is a configured maximum with its own stable failure code —
additive.

### D3 — Union availability is set-valued, encoded as arithmetic runs

This is the load-bearing decision. `BookableStart(start, min, max)` carries an
explicit promise: *every whole multiple of the resource's granularity between
the bounds may be booked*. That holds for one resource because one resource has
one grid. Over a pool it breaks two independent ways:

```
off-grid:  A g=30 offers {30,60,90}   B g=20 offers {20,40,60,80,100,120}
           union = {20,30,40,60,80,90,100,120}
           as (20,120) ⟹ claims 25, 50, 70 bookable. None are.

gap:       A offers {30}              B min 90 offers {90,120}
           union = {30} ∪ {90,120}
           as (30,120) ⟹ claims 60 bookable. It is not.
```

Neither case is exotic; two rooms with different minimums is the motivating
scenario for the whole feature. So a start carries a **list of runs**, each
`{Min, Max, Step}` meaning `Min, Min+Step, …, Max`. One contributing candidate
produces exactly one run.

*Alternatives rejected:*

- **Widen to `(min, max)`.** Advertises unbookable lengths — and worse, it
  manufactures deterministic all-fail (D6) by construction, destroying the very
  drift signal that failure code exists to provide.
- **Narrow to the intersection across candidates.** Honest, but destroys the
  union: one 30-minute-grid resource would flatten the whole service.
- **Flat list of every bookable length per start.** Also honest and simpler to
  consume, but a week of 15-minute-grid starts each listing ~30 lengths is
  thousands of integers, and its common case (a homogeneous site) is a fully
  redundant enumeration. Runs compress it losslessly and degenerate to exactly
  today's `BookableStart` shape when the site is homogeneous — the common case
  pays nothing.

Identical runs are collapsed and the list is ordered deterministically so
responses are stable and testable. Overlapping-but-distinct runs are **not**
merged: `{30,90,30}` and `{20,120,20}` share the length 60, but merging them
would require expanding to a length set and re-deriving runs, which is the flat
encoding wearing a disguise.

### D4 — Post-filtering per-resource output is lossless, so `Availability/` is untouched

The service range at `(start, resource)` is:

```
[ max(min_r, a_r), min(maxRun_r, b_r) ]   step = granularity_r
    where [a_r, b_r] = svc.Duration.TryResolveAgainst(r.Constraints)
```

nonempty iff that resource can back that start. This is exact, not an
approximation: `TryResolveAgainst` already ceils/floors both bounds to the
resource's granularity, and `maxRun_r` is already floored to it, so every value
in the result is a reachable length.

That means the service layer composes the **existing public**
`GetBookableStartsAsync` per candidate and filters — `SlotProjector`, `Walk`,
`FreeTimeCalculator`, and the whole of `Availability/` change not at all. The
"one computation, two projections" discipline from ⑦-1's D8 extends to a third
consumer without a third computation.

*Alternative rejected:* re-parameterising `Walk` with a `DurationRange`. It
would work, but it modifies the shared traversal that both existing projections
depend on, to buy nothing — the filter outside is already exact.

### D5 — The perf seam is built now: batched claims plus a pre-loaded-resource overload

Naively composing `GetBookableStartsAsync(resourceId, …)` per candidate costs
`1 + 2N` round trips: eligibility loads N resources, then each availability call
re-loads its resource (`ResolveAsync` → `resourceStore.GetAsync`) and issues its
own claims query. Two additions collapse that to 3:

- `IAvailabilityQueryService` gains an overload taking an already-loaded
  `Resource`. The id-based method becomes a thin load-then-delegate, so the two
  cannot diverge.
- `IBookingStore` gains a claims read over several resource ids in one call.

Built now on Chris's call (2026-08-13): both are cheap while the call sites are
being written and awkward to retrofit through a shipped port. The pre-loaded
overload is also the honest shape — the service layer *has* the aggregate and
should not pretend otherwise.

### D6 — Two distinct all-fail codes, and the deterministic one is a canary

When every candidate fails, echoing the last candidate's failures is arbitrary —
it depends on iteration order and tells the caller about a resource they never
asked for. Instead:

- any candidate failed on `conflict` → **`conflict`**. The request described a
  genuinely bookable slot; it raced or is occupied. Retrying may succeed.
- otherwise → **`service-unavailable`** (new code). Every candidate rejected it
  deterministically — off-grid, outside open hours, inside lead time, beyond
  horizon. Retrying is pointless.

Mixed outcomes favour `conflict`, because the actionable advice is "try again".

The real payoff is the second code as a **drift detector**. A client that takes
its start and length verbatim from the service availability query cannot
legitimately provoke it: availability and placement would have to disagree about
the same resource's rules. So `service-unavailable` arriving from a conforming
client is a bug signal, not a user error — precisely the defect class ③'s QA
caught, which green tests cannot see. The D4 pre-filter means that in practice
all-fail collapses to `conflict`.

### D7 — Service placement is its own endpoint with its own model

`POST /services/{id}/bookings`, body `{start, durationMinutes,
preferredResourceId?, booker}`.

*Alternative rejected:* nullable `ServiceId` / `PreferredResourceId` added to
`PlacementRequestModel` alongside its required `ResourceId`, XOR-validated. This
is the same shape ⑦-1's D1 rejected for duration — several nullable fields whose
valid combinations are a minority, forcing every consumer to re-derive the rule.
A separate model makes the invalid states unrepresentable and leaves
`POST /bookings` untouched.

`PlacementResponseModel` is reused unchanged: its `ResourceId` is exactly what a
service booker needs echoed back ("you're in Room 2").

### D8 — The requested length is always required and never substituted

Even for a fixed-duration service. This is ⑦-1's D13 applied to a new write
path: that defect was a helper that defaulted a length on the render path and
was reused on the write path, letting a visitor asking for 120 minutes be
confirmed for 30.

So: the client always states the length; Core validates it against the resolved
ranges; a mismatch is rejected. An omitted field is a model-binding failure, not
an inferred value — even though for a fixed service the "right" value is
knowable. Knowing the right value is exactly what makes silent substitution
tempting and wrong.

A length outside the pool-wide bounds fails fast with `duration-too-short` /
`duration-too-long`, decided from constraints alone before any free-time work.
A length inside those bounds but in a gap or off every grid falls through to the
candidate loop and surfaces as `service-unavailable` (D6) — which keeps the
canary meaningful, since availability would never have offered such a length.

### D9 — An ineligible preferred resource is rejected, not ignored

`preferredResourceId` is an ordering hint: an eligible-but-busy preference falls
through to other candidates, because that is what "preferred" means. But a
preference naming a resource *outside* the candidate pool fails with a new
`resource-not-eligible` code rather than being silently dropped.

Rationale: silently booking Frank when the caller named Mary is the same class of
harm as silently booking 30 minutes when they asked for 120. A caller naming a
specific resource has stated an expectation about *which* resource; falling
through discards it invisibly. Rejecting is recoverable — the caller can retry
without the preference.

*Flagged for review:* this is a judgement call rather than a settled decision,
and it is the one place in this change where a reasonable person could prefer
lenient behaviour. Revisit if ⑩'s pick-who UI makes rejection feel harsh.

### D10 — `service-unavailable` maps to 400, not a new status

`ApiResults` gains only `service-not-found` in its 404 arm (it currently falls
through to 400, which is a latent wrong answer). `service-unavailable` and
`resource-not-eligible` take the default 400. `service-unavailable` is the same
category as `outside-open-hours` — the request as stated cannot be satisfied —
which is already 400. Adding a 422 or 409 variant would put meaning on the
status line that the stable code already carries, and the mapper's contract is
that codes are the contract.

### D11 — Availability responses name no resource

A service bookable-start entry carries starts and runs, never a resource id.
v1 resolves the resource at placement time and makes no promise about which one;
naming a candidate in the availability response would imply a guarantee the
loop does not make, and would leak the pool's composition to anonymous callers
before ⑩ decides whether pick-who is even offered.

## Risks / Trade-offs

- **[Runs are a more complex wire shape than a min/max pair]** → Its common case
  is a single run, structurally identical to today's `BookableStart` plus a
  `step` field, so a homogeneous site's client code is barely different. The
  alternative is a contract that lies (D3), which is not a trade worth making.

- **[No in-repo consumer until ⑩]** → Four public endpoints ship with only tests
  exercising them, repeating the `TryResolveAgainst` situation but on a *public
  contract* rather than an internal method. Mitigated by shipping only the
  set-valued query and not the fixed-length projection (which would double the
  unexercised surface), and by integration tests that drive the endpoints over
  HTTP rather than only unit-testing Core. Live verification through the running
  TestSite is a task, not an optional extra.

- **[N in-memory availability computations per query]** → D5 removes the round
  trips, not the computation. It stays linear in pool size and is bounded by the
  existing `MaxQueryRangeDays`. Acceptable at realistic pool sizes; D2 records
  where the cliff is.

- **[The candidate loop widens the placement race window]** → With N candidates,
  the elapsed time between the query and the final attempt is longer than for a
  direct booking, so `conflict` is likelier under load. This is inherent to
  pessimistic candidate iteration and is why ③'s atomicity proof matters:
  correctness never depends on the window, only the success rate does. Each
  attempt holds at most one lock and leaves no state on failure.

- **[`service-unavailable` could become noise if a future front-end guesses
  starts]** → The canary's value depends on clients using availability output
  verbatim. ⑩ must not construct starts arithmetically from a grid. Recorded
  here so ⑩ inherits the constraint rather than discovering it.

- **[Rejecting an ineligible preferred resource may be over-strict]** → See D9;
  flagged for review rather than silently settled.

## Migration Plan

No schema change, no migration, no data backfill. Every addition is additive:
new Core types, two new port methods, one new availability overload, one new
controller, new DTOs, one added arm in an existing mapper. Rollback is removal
of the new surface; nothing existing is modified in a way that would need
reverting.

The two new failure codes (`service-unavailable`, `resource-not-eligible`) are
additive to `FailureCodes` and appear only on the new endpoints.

## Open Questions

- **D9's strictness** — reject or ignore an ineligible preferred resource. Built
  as reject; revisit at ⑩.
- **Run ordering key** — ordered deterministically, but by `(Min, Step)` or by
  contributing resource id? Resource id is not exposed (D11), so the ordering
  must be derived from the run values themselves. Settle at implementation;
  either satisfies the spec.
