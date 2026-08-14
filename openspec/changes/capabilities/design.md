## Context

Change ⑦-2 shipped booking-via-service with eligibility defined as *type key
match*. `ServiceBookingService.ResolveCandidatesAsync` calls
`IResourceStore.ListByTypeAsync(role.ResourceType)`, filters by duration
overlap, and hands the pool to a candidate loop. Everything after that step —
union availability over heterogeneous grids, the ordered placement attempts, the
`service-unavailable`/`conflict` split, the optional preferred-resource id — is
already built and does not care *how* the pool was chosen.

That is the whole opportunity here: eligibility is a single pure predicate at
the head of a pipeline, so constraining it further is a small, local change. The
work in ⑧ is not the algorithm; it is the surrounding decisions about what a
capability *is*, who may see one, and how an editor avoids silently
misconfiguring one.

Two obligations arrive with this change, both recorded in ⑦-2's design D9:

1. `resource-not-eligible` is an oracle for pool membership. It was accepted as
   harmless because eligibility was fully derivable from public reads — but D9
   says explicitly that "⑧ must revisit this", because capabilities are what
   break that derivability.
2. ⑧ is what creates *overlapping* eligibility pools, which is the precondition
   for the assignment problem ⑨ must solve.

The current `service-booking` spec also contains a sentence this change
falsifies — *"Eligibility SHALL be determined by resource type key alone; no
capability, tag, or explicit service-to-resource association participates in
v1."* That is not incidental: it is exactly the class of stale forward-looking
statement that the last two changes caught only at spec-sync time. It is
addressed head-on in this change's delta.

## Goals / Non-Goals

**Goals:**

- Express "only a resource holding capability X may fulfil this role", with the
  simple case unchanged and zero-config.
- Keep one implementation of the eligibility predicate, reachable from every
  caller that needs it, so no second copy can drift.
- Discharge ⑦-2's D9 obligation with a stated argument rather than silently
  dropping it.
- Give an editor immediate, honest feedback about how many resources a role
  matches — the failure mode of a mistyped or over-narrow capability set is
  otherwise indistinguishable from ordinary unavailability.
- Leave ⑨ a test suite that can actually fail if its assignment logic is wrong.

**Non-Goals:**

- Multi-role composition, role count > 1, or any assignment algorithm.
- Client-specific exclusions (see D4).
- A managed capability vocabulary: no capability entity, no CRUD, no display
  names, no rename/delete semantics.
- Any front-end or Razor change.
- Reporting duration-based candidate exclusion in the diagnostic — that is ⑧a
  (see D9).

## Decisions

### D1 — Capabilities are a `CapabilitySet` value object, not a bare set

Both `Resource` and `ServiceRole` carry a `CapabilitySet`: a private
constructor plus validating factories, following `ServiceDuration`'s precedent
from ⑦-1. It normalizes (deduplicates, orders), validates every key, and owns
the subset test as `Satisfies`.

*Alternative considered — a bare `IReadOnlySet<string>` member.* Rejected on a
concrete defect, not on taste: `ServiceRole` is a `record`, and a set member
gives the compiler-generated `Equals` **reference** semantics. Two roles with
identical capabilities would compare unequal. `ImmutableHashSet<string>` has the
same problem. Since tests and mappers compare roles by value, this would produce
failures that look like persistence bugs and are actually equality bugs.
`CapabilitySet` implements structural equality over its ordered keys, so record
equality behaves as every other member does.

The second payoff is single ownership: validation, normalization, and the subset
rule live in one type instead of being re-derived by Core, the EF mapper, the
management DTO, the Lit editor, and the delivery mapper — the same reasoning
that drove `ServiceDuration` rather than three nullable sibling fields.

### D2 — Capability keys reuse the type-key rule, and the predicate is extracted

A capability key is lower-case kebab-case, non-empty, and is **rejected** rather
than auto-normalized when it does not match — consistent with how
`Resource.Create` already treats `Type`. Auto-lowercasing would mean the value
an editor typed is not the value stored, which is a surprise the type key does
not spring.

The kebab-case regex currently exists twice, in `Resource` and in `Service`. ⑧
would make three copies, so it is extracted into one shared predicate.

*On the risk of sharing:* ⑦-1's CRITICAL defect came from a shared helper
(`ResolveDuration`) used on both a read and a write path where the correct
behaviours were *opposite*. That risk does not apply here. This is a pure
predicate with one behaviour and no path-dependence: a key either matches the
normalized form or it does not, identically for resources, roles, and
capabilities. The distinction worth keeping is behavioural, not structural —
share predicates freely, suspect helpers that make decisions.

Invalid keys fail with a new stable code `capability-key-invalid` rather than
reusing `type-key-invalid`, so a client can tell which field is wrong when a
role carries both.

### D3 — Capabilities are published, which closes D9 rather than revisiting it

Resource capabilities and role required-capabilities both appear in the delivery
read models.

D9's oracle was accepted because eligibility was *derivable*: `GET /resources`
exposes `type`, `GET /services/{id}` exposes `resourceType`, eligibility is
defined as those matching, so probing `preferredResourceId` told an anonymous
caller nothing they could not compute. Constraining eligibility by capabilities
breaks that equivalence **only if the new inputs stay private**. Publishing them
restores it exactly, so the probe again discloses nothing new, and D9's
reject-don't-ignore behaviour stands unchanged. The obligation is discharged by
construction rather than by weakening an error message.

*Alternative considered — keep capabilities private and make
`resource-not-eligible` non-committal.* Rejected for two reasons. First, it
costs a genuinely useful error: a caller that named a resource learns only "not
available", which is the same thing they are told when the resource is simply
busy. Second, and decisively, **it does not actually achieve secrecy**. Union
availability over a single-member pool *is* that member's calendar: an anonymous
caller can correlate a service's bookable starts against
`/resources/{id}/bookable-starts` and read pool membership off the match, with
no probing and no error codes involved. Paying a real cost for secrecy that a
correlation attack defeats anyway is the worst of both.

*Alternative considered — a per-capability public/internal flag.* Rejected as
premature: it adds a field to every surface, makes the eligibility inputs
partially unpublishable, and leaves the oracle question live for the internal
subset — so the D9 answer would still be needed, merely conditionally. The
sensitive case that motivates it is client exclusions, and D4 removes those from
this model entirely.

The consequence to accept knowingly: capabilities are now public contract, so a
site must not use capability keys as a place to write private notes about staff.
The spec says so; the editor should not need to.

### D4 — Client-specific exclusions are not capabilities

"This client must not be offered Joan" is a real requirement, and it is
deliberately excluded from this model. It composes on top of eligibility rather
than inside it:

```
Eligible(role)  = type match ∧ role.Caps ⊆ resource.Caps    ← public, derivable
Visible(client) = Eligible(role) ∖ exclusions(client)        ← private, later
```

Three independent reasons, any one sufficient: the data is private where
capabilities are public contract (D3); its lifecycle is per-relationship rather
than per-resource; and it requires an authenticated identity that the anonymous
booking flow does not have, making it inherently staff-side or member-gated.
Modelling it as a capability would drag private data into a published contract
and make D3 unsafe.

Stating this as a boundary now is the point — it is what keeps capabilities
publishable.

### D5 — The eligibility filter runs in Core, over hydrated capabilities

`ListByTypeAsync` hydrates each `Resource`'s capabilities; the subset test runs
in Core via `CapabilitySet.Satisfies`. No capability-filtered query is pushed
into SQL and **no new read-port method is added**.

*Alternative considered — push the subset check into the SQL query.* It is
expressible (`NOT EXISTS` over the required keys) and it would load fewer rows.
Rejected because it puts a domain rule in the persistence layer, where a second
implementation of eligibility can drift from Core's — and eligibility drifting
silently from availability is precisely the defect class QA caught in ③ and that
⑦-2's two distinct all-fail codes exist to detect. It also conflicts with the
existing requirement that resolution depend only on the read ports.

The cost is loading resources of the role's type that are then discarded. A
booking site has tens of resources per type, not thousands, and the existing
spec already forbids paging this pool. If that ever stops being true, the fix is
a projection, and it should arrive with a test that proves the SQL predicate and
`Satisfies` agree.

### D6 — One eligibility predicate serves both the booking path and the preview

The services editor's diagnostic previews an **unsaved** role, so it cannot call
`ResolveCandidatesAsync` — that takes a saved `serviceId`. It needs its own
endpoint over `(resourceType, requiredCapabilities)`.

What it must *not* have is its own eligibility logic. Both paths call the same
`CapabilitySet.Satisfies` against resources loaded by type; only the projection
differs (the booking path needs full resources, the preview needs id and display
name). A diagnostic that computes eligibility differently from the booker is
worse than no diagnostic, because it reassures an editor about a configuration
that does not behave that way.

Port placement follows ⑦-2's line: `ListTypesAsync` was deliberately put on the
management port only, and the capability usage projection and the preview lookup
go there too. The read port gains no new method (D5), so the two ports stay
separate rather than one reaching across the other.

The preview is a `GET` with a repeated `capability` query parameter — it is a
read with no side effects, the parameter count is small, and a repeated
parameter generates cleanly into the TypeScript client.

### D7 — The capability vocabulary is derived from use, not managed

A grouped projection over the capability table, ordered by key, mirroring ⑦a's
`ListTypesAsync` exactly — including its EF gotcha: the `GROUP BY` must project
to an anonymous type and construct the record after materialization, because EF
cannot translate a positional record constructor inside a grouping projection.

Free entry stays allowed. The vocabulary describes what is in use; it does not
constrain what may be typed.

*Alternative considered — a managed capability entity with CRUD.* Rejected: it
would fix spelling, which is the lesser half of the problem. A capability set is
optional and empty is legitimate, so "no resource has `cert-x`" is
indistinguishable from "nobody has been tagged yet" — and a vocabulary table
cannot tell those apart either. The diagnostic in D8 can. It is also a whole
managed entity, editor, migration, and rename/delete semantics for a
cross-cutting tag.

### D8 — The diagnostic reports capability matching, and says only that

The services editor shows a live count of resources matching the role's type and
capabilities, refreshed as they are edited.

Its wording is a load-bearing part of this decision, not presentation polish.
`ResolveCandidatesAsync` drops candidates on **two** grounds — capability
eligibility, and `ServiceDuration.TryResolveAgainst` finding no overlap with the
resource's own range — and ⑧ checks only the first. So the diagnostic says:

> 3 rooms have these capabilities

and never *"3 rooms can provide this service"*. The stronger sentence would be
false whenever duration excludes every match, which is a real ⑦-1 configuration
(a service fixed at 4h against rooms capped at 2h resolves to an empty pool and
reports it as ordinary unavailability). A diagnostic that reassures an editor
about an unbookable service reproduces the exact failure it exists to prevent.

Reporting the duration exclusion — *"3 rooms match — 1 excluded by duration"* —
is change **⑧a**. The seam is cheap and was checked before splitting:
`uBookItResource` holds granularity, min and max duration as scalar columns on
the same row as type and display name, so ⑧a widens a projection by three
columns with no new join, adds query parameters and a response field that are
both additive, and reuses ⑦-1's already-tested `TryResolveAgainst`. ⑧a modifies
the requirement ⑧ writes rather than adding a new one; that is the one
non-additive part of the split and it is routine.

### D9 — ⑧ creates overlapping pools, so its fixtures must contain them

Today two roles either name the same type (identical pools) or different types
(disjoint pools). Greedy assignment is correct for both, which is why ⑦-2 could
defer matching without risk. Capabilities produce properly overlapping pools:

```
Role A (needs cert-x):  eligible {Mary}
Role B (any therapist): eligible {Mary, Frank}

greedy by id → Mary to B, A unfillable → "unavailable"
correct        Mary to A, Frank to B    → available
```

That is a **wrong answer**, not a slow one. ⑧ stays single-role so it cannot
fire, but the point is what ⑧ leaves behind: fixtures built from single-capability
or disjoint pools would make ⑨'s defect invisible to every inherited test. So
this change's fixtures carry overlapping pools (`{Mary} ⊂ {Mary, Frank}`) from
the outset, even though ⑧ itself only ever reads one role from them.

### D10 — A new migration, and additive child tables

`uBookItResourceCapability (ResourceId, Key)` and
`uBookItServiceRoleCapability (ServiceRoleId, Key)`, each with a composite
primary key so a duplicate tag on one owner is impossible at the schema level
rather than only in `CapabilitySet`. `ServiceRoleRow` already carries a `long`
surrogate id, so both are ordinary child tables with cascade delete from their
owner.

This is a **new** migration, not an amendment to ⑥'s. ⑦-1 amended ⑥'s migration
in place because it was correcting a shape ⑥ had got wrong and no data existed;
here the earlier migrations are right, have been applied, and this is genuinely
new structure. Amending applied migrations to add unrelated tables would be
rewriting history for no benefit.

Nothing is destructive: no column is dropped, no row rewritten, and rollback is
dropping two empty tables.

### D11 — The source break is taken cleanly

`Resource.Create` gains a `capabilities` parameter positioned beside
`availability` where it reads naturally, rather than appended after `id` purely
to preserve positional callers. `ServiceRole` gains a member.

Nothing is published and the repository is private, so the compatibility promise
in CLAUDE.md ("once published") has not yet attached — the same call Chris made
explicitly for `ServiceDuration` in ⑦-1. The alternative, a defaulted trailing
parameter, buys compatibility nobody needs at the cost of a signature ordered by
history instead of meaning.

## Risks / Trade-offs

- **[Capabilities are permanently public contract]** → Accepted deliberately
  (D3), and bounded by D4 keeping private per-client data out of the model. The
  spec states that capability keys are visible to anonymous callers, so a site
  cannot be surprised by it.
- **[The diagnostic could be read as "this service is bookable"]** → D8 fixes
  the wording narrowly and ⑧a completes it. The risk of the *stronger* wording
  is worse than the risk of the weaker one: an under-claiming diagnostic is
  merely incomplete, an over-claiming one is wrong.
- **[⑧a is a small follow-up and small follow-ups slip]** → Recorded with its
  provenance: it closes a ⑦-1 hazard that exists today with or without ⑧, not
  leftover ⑧ scope. If it slips, ⑧'s diagnostic is still true.
- **[In-memory eligibility filtering loads discarded rows]** → D5; bounded by
  the realistic number of resources per type, and revisitable behind a
  differential test.
- **[An empty capability set is legitimate, so "nothing matches" is ambiguous]**
  → This is the residual hazard the diagnostic exists for; it converts a silent
  narrowing into a visible number at configuration time, which is the only point
  where the editor still remembers what they intended.
- **[The `service-booking` spec asserts eligibility is type-only]** → Falsified
  by this change and rewritten in its delta, not left to be discovered at sync
  time.

## Migration Plan

One new EF Core migration creating two child tables with composite primary keys
and cascade delete. No backfill: an existing resource or role simply has no
capability rows, which reads back as an empty `CapabilitySet` and is exactly its
current behaviour. No column is altered or dropped.

Rollback is the down migration dropping two empty tables, plus removal of the
additive API surface. Because empty required-capabilities is the default and
means "no constraint", a site that installs ⑧ and never tags anything behaves
identically to ⑦-2 — which is also the regression test: every ⑤/⑥/⑦ scenario
must pass unchanged.

Backoffice client regeneration is required for the new DTO members and
endpoints, which needs the TestSite running against the new build.

## Open Questions

- **Capability display names.** Keys are shown raw in the backoffice, which is
  consistent with type keys but will read poorly on a public front-end. ⑩ has to
  decide whether to present raw keys, take a lookup from content, or ask for a
  managed vocabulary after all — deliberately not pre-empted here.
- **Diagnostic refresh trigger.** Debounced on edit versus on blur; either
  satisfies the requirement, settle at implementation against how the existing
  editor already behaves.
- **Whether the preview endpoint should return matching resource names or only
  a count.** Names are more useful and the pool is small; the risk is an editor
  reading it as an availability list. Leaning names, settle at implementation.
