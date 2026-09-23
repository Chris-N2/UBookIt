## Context

See `proposal.md` — *Why*. What shapes the approach is where availability is already decided, and
what the 17.0.0 compatibility promise will not let us move.

Every availability answer in the package funnels through one function.
`AvailabilityConfiguration.EffectiveWindows(date)` returns the exception's windows when one exists
and the weekly pattern's otherwise; `FreeTimeCalculator.OpenIntervals` walks the range calling it;
slot projection, bookable-start projection, service availability and placement validation all
consume the result. A third layer put there is seen by all of them at once.

Two published surfaces constrain how it gets there:

- `IAvailabilityQueryService.ProjectBookableStarts(Resource, IReadOnlyList<ClaimInfo>, DateOnly,
  DateOnly)` is **public and frozen from 17.0.0**, and is deliberately pure — it exists so a service
  availability query over N candidates costs one claims round trip instead of N. It takes a
  `Resource` and no closure argument, and widening it would be a break.
- `IResourceStore` is a **published port**: a host may implement its own.

And one existing mechanism is a trap. `ResourceRowMapper.ToRow` persists
`resource.Availability.Exceptions` back to `uBookItResourceException` rows, while the management
contract round-trips the same collection — GET returns `Exceptions`, PUT replaces them wholesale.
Anything merged into that collection on the way in is written back out as the resource's own on the
next save.

## Goals / Non-Goals

**Goals:**

- One implementation of precedence, in `UBookIt.Core`, reachable by every availability route.
- No widened public signature, no member added to a published interface.
- A resource's own exceptions remain exactly what the operator typed, before and after any save.
- Closure reads cost one query per store call, not one per resource.

**Non-Goals:**

- Caching closures. They are a few dozen rows a year and every availability query is already bounded
  to `MaxQueryRangeDays`. A cache is a correctness risk (a stale closure closes a site that is open)
  bought against an unmeasured cost.
- Making the closure layer reachable by hosts as an extension point. It is data, not a seam.
- Anything the public-holiday change owns: the source interface, import, provenance, recurrence.

## Decisions

### D1 — Closures are a distinct layer on `AvailabilityConfiguration`, not merged into `Exceptions`

`AvailabilityConfiguration` gains an applicable-closure set alongside `OpenHours` and `Exceptions`,
consulted first by `EffectiveWindows`. `Create` gains an optional parameter; the existing signature
keeps working and every existing caller keeps compiling.

*Alternative considered: merge closures into the exception set at hydration.* Cheaper, and wrong.
`ToRow` would persist every inherited closure as the resource's own exception on the next save —
permanently, surviving deletion of the closure — and the editor could no longer tell an inherited
date from a typed one, which is the very thing the UI must show. It would also collide with
`duplicate-exception-date` wherever a resource already had an exception on a closure date.

*Alternative considered: pass closures alongside the resource into each computation.* Honest, but it
widens the frozen pure projection, and a second overload that silently omits closures is a footgun
aimed at exactly the caller who least expects it.

### D2 — Closures reach the domain at hydration, in the persistence layer

The resource stores read the applicable closures and hand them to `ResourceRowMapper.ToDomain`, so
a `Resource` obtained through any read port already carries them. That is what keeps D1's pure
projection correct without an argument: whoever loaded the resource loaded its closures.

*Consequence, accepted and documented:* `IResourceStore` is a published port, so **a host
implementing its own resource store owns applying closures**, exactly as it already owns applying
open hours and exceptions. This goes in the docs rather than being left to be discovered.

*Alternative considered: compose closures in `AvailabilityService`.* It would put the guarantee in
Core, above any store — but only for the id-taking queries. The pure overload would still need them
passed, which is D1's rejected alternative.

### D3 — Opt-outs are resource-owned state; applicable closures are not

Two different lifetimes, so two different homes:

- `Resource` carries its **opt-out closure ids**. They are written by the resource's own save, and
  `ToRow` writes them exactly as it writes capabilities.
- `AvailabilityConfiguration` carries the **closures that apply** — the list minus this resource's
  opt-outs — injected at hydration and never written back.

This is what makes D1's trap structurally shut rather than shut by care: the collection the write
path reads is not the collection the closure layer lives in.

### D4 — The server decides "superseded", the client renders it

The resource response marks an override exception that is currently superseded by a closure. The
marking fires **only where the outcome differs** — an exception that is itself a closure on a
closure date closes the date either way, so marking it would report a difference that does not
exist.

This matters more than it looks: as the editor ships, "Add exception" defaults to *Closed all day*,
so most exceptions are closures and the marker will rarely fire at all. Its only job is the minority
override case, which is exactly where an operator would otherwise be misled. Computing it in the
client would mean a second implementation of precedence living in TypeScript, free to disagree with
the one in Core — the same reasoning that keeps eligibility out of SQL.

### D5 — Closures are read once per store call

`SqlResourceStore.ListByTypeAsync` serves service candidate pools; a per-resource closure read there
turns one availability question into an N+1. The closure set is read once and applied to every
resource in the batch. Opt-outs come back with the resource rows, so the filtering is in memory over
data already loaded.

### D6 — Two additive tables, cascade from closure to opt-out

`uBookItSiteClosure` (id, date uniquely indexed, label) and `uBookItResourceClosureOptOut`
(resource id, closure id, unique per pair) with cascade delete from both parents. The date's
uniqueness is enforced by the index rather than by a check before writing, which is a race.

*Why the opt-out keys on closure id rather than date:* editing a closure's date carries the opt-out
with it, and a deleted-then-recreated date starts with nobody opted out rather than silently
reinstating exemptions an operator has forgotten. The cost — a dangling row if the cascade were
missed — is exactly what the cascade exists to prevent, and is testable.

### D7 — The closure list is not a setting, and does not go on the settings screen

The `site-settings` spec promises that every row on that screen presents the configured value beside
the effective one, with an action restoring it. A closure has no configured counterpart, so it could
not honour that promise, and putting it there would mean modifying requirements that carry a great
deal of hard-won reasoning. Closures get their own view; `site-settings` is untouched.

The access split reinforces it: that screen is all-or-nothing behind one verb, while closures are
read by `Configure` **or** `Settings` and written only by `Settings`. A partially-editable box on an
all-or-nothing screen is the "control that looks live and isn't" failure this change is otherwise
designing against.

### D8 — Two existing verbs, not a fifth

Writing is a site-level act (`Settings`); reading and exempting one resource are resource-level acts
(`Configure`). `UBookIt.Settings` is never seeded and has no super-user bypass, so **on an upgraded
site nobody can create a closure until an administrator grants the verb** — including the
administrator. This is accepted deliberately: only someone with that authority should be able to
shut the whole site. The closures view therefore explains its own read-only state the way the
settings view already explains its own absence, rather than rendering controls that would be
refused.

*Alternative considered: `Configure` writes too.* It works on every site the moment they upgrade,
and was rejected because "may add a meeting room" would then mean "may shut the organisation".

### D9 — Delta shape: why `persistence` gains requirements rather than having them rewritten

`persistence`'s *Schema shape and naming* enumerates the schema with "SHALL comprise", but four
later tables — responsibilities, flags, settings, cancellation secrets — each arrived as **their
own ADDED requirement** rather than by rewriting that enumeration, and capability hydration arrived
as its own requirement rather than by rewriting *Store implementations honour Core semantics*. This
change follows that established shape. It is recorded here so a reviewer can tell a deliberate
choice from an omission.

Where a requirement's own sentences are genuinely falsified, it is modified in full instead:
`availability` (two requirements), `bookings` (the pipeline's description of `outside-open-hours`),
`permissions` (what two verbs govern), `resource-management` (the contract and the editor).

**Guarantee diff for each MODIFIED requirement** — every scenario and SHALL in the current spec was
classified before rewriting:

- *Date exceptions*: carried forward — single-date targeting, closure-or-replacement, precedence
  over the weekly pattern, one exception per resource per date (reworded to name the resource's own
  exceptions, since the uniqueness it guarantees is unchanged), both original scenarios verbatim.
  Added — closure precedence, retention of a superseded exception, three scenarios.
- *Free-time computation*: carried forward — the subtraction of blocking claims, ordered disjoint
  output, non-blocking statuses, window coalescing, all three original scenarios verbatim. Added —
  closures among the inputs, two scenarios.
- *Placement validation pipeline*: carried forward — the full ordered code list, start-alignment
  rule, no-failures-as-exceptions rule, both representability paragraphs and their reasoning, the
  service-pool rule, all nine original scenarios verbatim. Added — one clause in the
  `outside-open-hours` gloss, the non-disclosure sentence, two scenarios.
- *Access within the section is decided by four verbs*: carried forward — all four bullets, every
  italic rationale, Manage-implies-Read, Settings-implies-nothing, the union rule, the
  no-permission-store rule, all twelve original scenarios verbatim. Added — closures in two bullets,
  a rationale paragraph, a sentence that reading under either verb is not an implication, five
  scenarios.
- *Resource CRUD endpoints* and *Workspace editor for a resource*: carried forward — every existing
  sentence and every existing scenario verbatim. Added — opt-outs on the request, projected closures
  and the superseded marking on the response, the Global closures group, and their scenarios.

## Risks / Trade-offs

- **A host implementing `IResourceStore` silently loses closures** → documented as that host's
  obligation, in the same place the other published-port obligations are stated. The shipped store
  is covered by a seam test through the production entry point.
- **A guard over the closure layer and a guard over projection can both pass while nothing connects
  them** → the seam is tested through the real query path, not on either side of the join. This
  failure mode has occurred on this project before and is named in the spec rather than left to
  reviewer diligence.
- **The superseded marker is the kind of statement that can quietly become false** → it is derived
  server-side from the same precedence implementation the projection uses, so it cannot disagree
  with availability without availability itself being wrong.
- **Opting out is a per-closure, per-resource decision, so a site with many resources and many
  closures does a lot of ticking** → accepted for this change. Bulk exemption ("this resource
  ignores all closures") is a plausible later feature and deliberately not invented now; it would
  change what an opt-out means.
- **The list grows every year** → the view defaults to upcoming and nothing is pruned. If it ever
  needs paging, the endpoint already filters server-side.
- **`MaxQueryRangeDays` bounds every availability query**, so the closure read is over a bounded
  range and no query can be made expensive by adding closures.

## Migration Plan

One additive EF Core migration creating both tables. Nothing existing is altered or dropped, so
rollback is dropping two empty tables; a site that never creates a closure is byte-for-byte
unaffected in behaviour.

Order of work follows `tasks.md`: domain first (precedence is testable with no storage at all),
then persistence and hydration, then the management surface, then the two client surfaces, then
docs. The seam test lands with the hydration work rather than at the end, so that the join is
proved before anything is built on it.

Released in `17.2.0` on `main`, then cherry-picked to `dev/v18` for the 18 line, per the branching
rule that shared truth moves by cherry-pick in both directions.

## Open Questions

- **The label's length cap** is set to match the existing name columns (512). Nothing depends on the
  number; if a shorter cap proves better for the opt-out list's layout it can change without
  touching the specs, since they say "the package's name-column length" rather than a figure.
- **Whether the closures view eventually wants a bulk "close this date for everything except…"
  affordance** is a UI question that can be answered after the holiday import exists and the list
  has realistic contents.
