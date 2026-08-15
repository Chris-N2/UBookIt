## Why

A booking usually needs more than one thing at once. A massage needs a room
**and** a therapist; hiring a machine needs the machine **and** an operator.
uBookIt cannot express that: `Service.Create` rejects any service with more than
one role, so every service resolves to exactly one resource.

The domain has been shaped for this since change ①. `ResourceClaim` is
`(Guid ResourceId)`, every claim of a booking shares its single interval, and
`IBookingStore.PlaceAsync` already takes a claim *set* — single-claim is v1
**behaviour**, not structure. `SqlBookingStore.PlaceAsync` already sorts every
claim's resource id ascending, takes a lock per resource inside one transaction,
and conflict-checks across all of them, under a comment written in ③ that says
*"Ascending lock order is deadlock-proof for future multi-claim bookings."*
Change ⑧a then made resolution take `(ServiceRole, ServiceDuration)` rather than
a service, which is exactly the shape several roles need.

What blocks multi-role is not the machinery. It is that two roles can draw from
**overlapping** pools, where assigning each role its first available candidate
returns a *wrong* answer rather than a slow one:

```
Role A (needs cert-x):  eligible {Mary}
Role B (any therapist): eligible {Mary, Frank}

greedy by id → Mary→B, A unfillable → "unavailable"
but            Mary→A, Frank→B was available all along
```

This change takes the half of multi-role composition that is free of that
problem, by requiring every role to name a **distinct resource type**. A
resource has exactly one type, so distinct types make the pools **provably
disjoint**, and over disjoint pools first-available assignment is not merely
adequate — it is optimal. Real assignment (`⑨-2`) is then needed only for the
same-type case, which this change rejects.

## What Changes

- **A service may require several roles, each naming a different resource
  type.** `Service.Create` stops rejecting more than one role. Every role still
  has `Count` of exactly 1.

- **Two roles of the same resource type are rejected**, with a new stable
  failure code, whatever their capabilities. This is a **deliberate v1
  restriction, not a property of services**: it exists solely because assignment
  across overlapping pools is not implemented yet, and `⑨-2` relaxes it. Roles
  of type `therapist` requiring `cert-x` and of type `therapist` requiring
  nothing are *different roles*, and are rejected precisely because they are the
  case that needs matching.

- **Availability composes as an intersection across roles.** Within a role,
  availability is already a union over its candidates; a service is bookable at
  a start only where **every** role has a free candidate, for a length **every**
  role can provide. Both terms intersect: the starts, and the lengths at each
  common start.

- **Placement claims one resource per role, atomically.** All claims share the
  booking's single interval. The store contract and its SQL implementation
  already do this; this change makes the behaviour reachable and proves it with
  racing tests rather than assuming it.

- **The length-run invariant becomes explicit.** Every `LengthRun` produced
  anywhere is anchored at zero — its lengths are exactly the multiples of `Step`
  in `[Min, Max]` — which is what makes intersecting two runs an `lcm` rather
  than a congruence problem. ⑦-2's subset elimination already depends on this
  silently; intersection would be the third dependant, so the invariant is
  stated and enforced rather than rediscovered.

- **The backoffice editor manages several roles.** Add and remove role rows,
  each with its own type picker and capability set. This absorbs the multi-role
  editor that was previously scheduled separately: shipping multi-role services
  with an editor that can only express one role would repeat ⑥'s mistake of
  deferring its editor and needing ⑦a to repair it.

- **The configuration preview reports one chain per role.** ⑧a's form-level
  summary becomes a chain per role. Because the pools are disjoint, each role's
  chain stays independently true — no role can steal another's resource.

- **BREAKING (management contract, unpublished):** the configuration preview
  request carries a list of roles rather than one role, and the response a chain
  per role.

- **BREAKING (source, unpublished):** composite availability and placement take
  a service's whole role list. Nothing is published and the repository is
  private, so this is taken cleanly rather than shimmed — the same call made for
  `ServiceDuration` in ⑦-1, `Resource.Create` in ⑧, and resolution in ⑧a.

No schema change and no migration: the claims table has always been
one-row-per-claim.

## Guarantees deliberately removed

Three guarantees are **dropped rather than carried**, stated here because a
wholesale requirement replacement makes a removal look like nothing at all.

- **"Multi-role composition remains rejected"** (`service-booking`) — the
  scenario asserting that a two-role service is rejected with
  `service-role-invalid`. It is the behaviour this change exists to replace.
  What survives of it is narrower and is restated: two roles of the *same*
  type are still rejected, now with their own code.

- **"One requirement is editable"** (`services`) — the scenario asserting that
  the editor shows exactly one requirement row with no add or remove control.
  Replaced by add/remove over one or more rows. The half of it that still
  holds — that no count field is displayed — is restated in the scenario about
  the count always being sent as 1.

- **"No more than one resource lock is held at any moment"**
  (`service-booking`) — superseded rather than dropped, and stated as such in
  the requirement itself. An attempt for a service of several roles
  necessarily holds a lock per claimed resource; that is what makes the
  placement atomic across them. What replaces it is the guarantee that
  actually holds: one attempt in flight at a time, deterministic lock order,
  and all locks released together.

Everything else in the requirements this change replaces is carried forward,
including scenarios whose titles are reworded for the plural shape
(`…the pool` → `…the pools`, `…outside the pool` → `…outside every pool`).

## Capabilities

### New Capabilities

None. Every change modifies behaviour an existing spec already owns.

### Modified Capabilities

- `services`: a service may hold several roles of distinct types; a duplicate
  resource type is a validation failure; the editor edits a list of roles rather
  than a list of one; the resolution summary reports a chain per role.
- `service-booking`: resolution runs per role; availability composes as an
  intersection across roles; placement claims one resource per role and bounds
  its attempts by excluding candidates already claimed; the union-availability
  requirement is scoped to *within a role*, since it read "every start at which
  at least one candidate can fulfil the service" and that is false across
  roles; the overlapping-pools boundary narrows from "multi-role is out of
  scope" to "same-type roles are out of scope".
- `bookings`: the atomic placement contract gains the multi-claim case it
  already describes but has never had reachable behaviour for.
- `delivery-api`: the service read model publishes several roles; service
  availability is composite; a placed booking carries one claim per role.
- `resource-management`: the configuration preview accepts several roles and
  returns a chain for each.

## Non-goals

- **Same-type roles, and the assignment they need.** Two roles drawing from
  overlapping pools require real matching (augmenting paths), in **both**
  availability and placement — a union per role would call a start available
  when only one shared resource is free. That is `⑨-2`, and this change's
  distinct-type rule is what makes its absence safe rather than latent.
- **`Count` greater than 1.** A role needing two therapists is matching-free
  under distinct types, but adds multiplicity to availability and *k* locks per
  role to placement. Deferred to `⑨-2` to keep placement's hard half in one
  change.
- **Diagnosing grids that never align.** Intersecting starts introduces a new
  way for a correctly configured service to be permanently unbookable: two roles
  whose resources have different opening times *and* different granularities can
  produce start grids that never coincide (they align only when
  `gcd(step₁, step₂)` divides the offset between their interval starts). This
  change creates that possibility and `⑨-1a` reports it. Deferring is safe
  rather than merely acceptable: ⑧a's D5 boundary means the configuration
  summary already says nothing about opening hours or bookable times, so it is
  correctly silent rather than falsified — what is missing is an explanation,
  not a correction.
- **An upper bound on granularity.** `lcm` makes the existing unbounded-
  granularity hazard arithmetically sharper, so this change computes `lcm`
  safely; bounding granularity at configuration time remains its own change.
- **Choosing which resource fills a role.** The delivery request keeps its
  single optional preferred-resource id, which stays unambiguous precisely
  because the pools are disjoint — a resource belongs to exactly one type and
  therefore to at most one role. A pick-who UI is `⑩`.
- **No front-end change beyond what the contract requires.** ⑤'s
  direct-resource path must remain untouched, as in every slice.

## Impact

**Core** — `Service.Create` accepts several roles and rejects duplicate types;
composite availability across roles; placement building a claim per role; the
length-run invariant enforced where runs are constructed.

**Persistence** — no schema change, no migration. `SqlBookingStore.PlaceAsync`
is expected to need no change; the work is proving it under concurrent
multi-claim placement.

**Backoffice** — multi-role editor rows, a preview request and response carrying
several roles, a regenerated client, and the summary rendering a chain per role.

**Delivery API** — the service read model publishes the role list; composite
availability; a placed booking carries several claims.

**Tests** — composite availability across heterogeneous grids, including the
sparse case where two granularities intersect to their `lcm`; racing
multi-claim placement designed to fail against a non-atomic store; the
distinct-type rejection, built on the **overlapping-pool fixtures ⑧ was
required to carry**, so that `⑨-2`'s assignment defect is detectable rather than
invisible when the rule relaxes.
