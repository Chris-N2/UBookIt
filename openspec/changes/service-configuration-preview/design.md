## Context

`ServiceBookingService.ResolveCandidatesAsync` is three filters in a loop: list
by type, keep those whose capabilities satisfy the role, keep those whose
constraints admit a length the service permits. It returns only the survivors.

Change ⑧ added a backoffice readout for the first two filters and stopped there,
because the third was not evaluated — its design D8 made the narrow wording a
load-bearing decision rather than a stylistic one, on the grounds that a
diagnostic which over-claims is worse than one that under-claims. That reasoning
was right, and this change removes its premise by evaluating the third filter
too.

Two things about the current state shape the design:

1. **⑧'s readout can be actively misleading**, not merely incomplete. With
   capabilities required and a mistyped type key, it reports "No resources have
   these capabilities" — sending an editor to the wrong control. The cause is
   structural: one number cannot say which of two filters emptied the pool.
2. **⑧ left two implementations of the capability rule** — Core's
   `CapabilitySet.IsSatisfiedBy` in the booking path, and a second application of
   it inside `SqlResourceManagementStore.ListMatchingAsync` for the preview.
   ⑧'s QA circled this twice: once accepting it because both call the same
   predicate, once noting the in-memory double's ordering could still diverge
   from the SQL store's. The preview asking Core the real question removes the
   second implementation rather than continuing to police it.

## Goals / Non-Goals

**Goals:**

- Make every reason a resource is excluded from a service's pool visible at
  configuration time, attributed to the filter that excluded it.
- Reduce the number of places that compute eligibility to exactly one.
- Let the summary say what an editor actually wants to know, now that it can do
  so truthfully.
- Discharge the type-key length obligation inherited from ⑧.

**Non-Goals:**

- Availability of any kind — open hours, lead time, horizon, conflicts.
- Multi-role composition or role count above one.
- Any delivery-API or front-end change.
- A general-purpose "dry run this service" API for external consumers; this is a
  backoffice diagnostic and stays on the management surface.

## Decisions

### D1 — The funnel is the computation; the pool is a projection of it

`ResolveCandidatesAsync` does not filter separately from the diagnostic. One
method produces the full funnel — resources of the type, those satisfying the
capabilities, those whose duration range admits the service, and the excluded
remainder with its reason — and the booking path takes the candidate list from
it.

This is ⑦-1's precedent applied again. That change shipped `GetSlotsAsync`
alongside a start-first query and made one a projection of the other explicitly
*"so they cannot drift"*; the same argument applies with more force here,
because the two consumers are a diagnostic and the thing it claims to describe.
A diagnostic that computes eligibility separately from the booker is worse than
no diagnostic: it is confidently wrong exactly when the two disagree, which is
the case it exists to detect.

*Alternative considered — two entry points sharing private predicates.* Cheaper
for the booking path, and drift is unlikely because the predicates are shared.
Rejected because what would drift is not the predicates but their **composition
and order**, and that is precisely what the funnel reports on.

**Cost accepted knowingly:** the booking path now allocates the intermediate
stage lists and discards them. A booking site has tens of resources per type,
not thousands, and the pool is already required to be unpaged. If that ever stops
being true the fix is a resolution overload that skips the intermediates, and it
should arrive with a test proving the two agree.

### D2 — Resolution takes a role and a duration, not a service

The entry point is `(ServiceRole, ServiceDuration)`.
`ResolveCandidatesAsync(serviceId)` loads the service and delegates with
`service.Roles[0], service.Duration`.

The preview describes a service being *edited*, which may not yet be valid —
most obviously it may have no name, which `Service.Create` requires. Constructing
a transient `Service` for a preview would mean either inventing a placeholder
name or refusing to preview a form until an unrelated field is filled in. Both
are the validator leaking into a question it has no stake in.

This also makes the Core surface honest about what resolution actually depends
on: a role and a duration, never a name or an id.

### D3 — The preview endpoint is a POST on the management API

A duration specification is a structured value — a kind plus whichever bounds
apply — so flattening it into query parameters would mirror the DTO badly and
invite the same three-nullable-fields ambiguity `ServiceDuration` exists to
prevent. The body is the natural carrier.

It is a read with no side effects, which is the usual argument for `GET`; the
argument loses to representing the input correctly. This mirrors how the
management API already accepts a duration on service create and update.

**Port note.** The management controller calls Core resolution, which uses the
**read** port. That does not breach ⑦-2's separation rule: that rule constrains
what resolution may *depend on* — read ports only, never a management store — not
who may call it. Nothing on the read port is added, and the management store
loses a method rather than gaining one.

### D4 — `GET resources/matching` is removed, not deprecated

Along with `IResourceManagementStore.ListMatchingAsync`, its SQL implementation,
and the in-memory double's copy of the subset test.

Leaving it would mean two preview endpoints answering overlapping questions,
one of which applies the capability rule itself. That is the exact duplication
this change exists to remove, and a deprecated endpoint with no consumer is
carrying cost for a compatibility promise that has not attached — nothing is
published and the repository is private.

Removing it also retires the residual risk ⑧'s QA identified: the in-memory
double ordered by `StringComparer.Ordinal` while the SQL store ordered by
database collation, a latent way for a test double to answer differently from
production.

### D5 — D8 is discharged; "eligibility is not availability" replaces it

⑧'s D8 forbade any wording implying bookability, because the count was a
superset of the candidate pool — it omitted duration exclusion, so "these can
provide the service" could be false for every resource counted. With the third
filter evaluated, the summary's final stage **is** the candidate pool, and that
objection no longer holds. The wording may say what can provide the service.

A weaker constraint takes its place and is recorded here so it is not lost:

```
can provide this service   =  configuration: type ∧ capabilities ∧ duration range
                              ↑ what the summary reports

can be booked at 10:00     =  the above ∧ open hours ∧ lead time ∧ horizon
                              ∧ granularity ∧ nothing already claims it
                              ↑ what the summary does NOT report
```

Accepted as safe because the audience is an editor configuring a service, not a
visitor looking for a slot — the two questions are asked in different places by
different people. The summary must not drift toward availability language
("available", "free", "bookable now") on that basis.

### D6 — The summary is form-level, and reports stages, not a single number

It moves above the editor's groups. Its inputs now span Service Requirements
(type, capabilities) and Duration (kind, bounds); a readout inside one group that
changes when you edit another is disorienting, and worse, an editor who sets a
four-hour duration and never scrolls up would not see that it emptied the pool.

Presenting it as a resolution summary rather than a match count also matches
what it now is — a statement about the whole service configuration.

*Alternative considered — a second readout mirrored in the Duration group.*
Rejected: two renderings of one computation is the duplication pattern this
change is removing, at the UI layer, and ⑧'s QA already found an
adjacent-wording inference problem when two related strings sat together.

### D7 — Unknown is a per-stage state, and never renders as zero

A failed or superseded preview renders silence, not a funnel of zeros. This is
⑧'s rule and the reason for it is unchanged: zero is the answer that tells an
editor their configuration is broken, so reporting it because a request failed
sends someone to fix something that is correct.

⑧'s two supporting disciplines carry forward:

- The phrasing is derived from state captured **with** the response, never from
  live state. ⑧'s QA found that deriving phrasing live while the count lagged
  let the readout briefly assert a false sentence — into a `role="status"`
  region, so a screen reader announced it before the correction.
- A stale-request token guards against an earlier in-flight preview overwriting
  a later one.

### D8 — Type keys gain the length bound capability keys already have

`Resource.Type` and `ServiceRole.ResourceType` are validated against
`NormalizedKey.MaxLength` as well as the kebab-case shape, failing with the
existing `type-key-invalid` code.

⑧ introduced `MaxLength` for capability keys and deliberately left type keys
alone as out of scope. QA agreed with the scope call but noted the result reads
as unfinished rather than deliberate, since the constant now exists and the type
call sites still ignore it. This change touches that validation neighbourhood, so
it is where the asymmetry gets resolved.

No existing data can be invalidated: the columns are `nvarchar(64)`, so nothing
longer than the new bound can already be stored.

## Risks / Trade-offs

- **[The booking path carries diagnostic allocation cost]** → D1; bounded by
  realistic pool sizes, and revisitable behind an equivalence test.
- **["Can provide this service" is read as "is available now"]** → D5 names the
  distinction and forbids availability language. The audience and the location
  (a configuration editor, not a booking flow) do most of the work.
- **[A form-level summary is easy to ignore]** → It is above the groups rather
  than below them, and it is the only element that reports on the configuration
  as a whole. Being visible from the Duration group is the specific problem it
  solves.
- **[Removing an endpoint that a future consumer might want]** → D4; nothing is
  published, the replacement answers a strictly larger question, and re-adding a
  narrower read later is cheap.
- **[The funnel exposes resource display names to the backoffice]** → Already
  the case in ⑧'s preview, and this is an authorized management endpoint. No
  change in exposure.

## Migration Plan

No schema change, no migration, no data backfill. The removed endpoint and store
method have exactly one consumer, which this change replaces in the same commit
range; the regenerated backoffice client drops the old operation.

Rollback is reverting the change set. Because the funnel's final stage is the
same candidate pool `ResolveCandidatesAsync` produced before, booking behaviour
is unchanged by construction — which is also the regression test: every ⑤/⑥/⑦/⑧
scenario must pass untouched.

## Open Questions

- **How much of the funnel to render when every stage is healthy.** A service
  whose configuration excludes nothing produces `10 → 10 → 10`, where three lines
  say what one would. Whether to collapse that to a single line, or always show
  the chain for consistency, is a presentation call best made against the real
  editor rather than in advance.
- **Whether the duration stage should name the limiting bound per resource**
  ("Red Room: 2h maximum") or only the resource. Naming the bound is more
  actionable and the data is already loaded; the risk is a long list on a wide
  pool. Settle at implementation, possibly by capping the named resources and
  saying how many more there are.
