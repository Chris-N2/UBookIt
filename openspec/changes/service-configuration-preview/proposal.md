## Why

Since change ⑦-1, a service whose duration cannot fit any resource — fixed at
four hours against rooms capped at two — resolves to an empty candidate pool and
reports it as ordinary unavailability. Nothing anywhere surfaces the cause. An
editor sees "unavailable", which is also what a fully-booked system says.

Change ⑧ built a readout precisely to expose that class of silent
misconfiguration, but could only close half of it. Candidate resolution excludes
resources on three grounds and ⑧ checked two, so its design D8 forced the
narrow wording *"3 rooms have these capabilities"* — deliberately refusing to
claim bookability, because the duration exclusion was not evaluated. This change
evaluates it, which retires that constraint.

⑧ also shipped a defect this change fixes structurally rather than by adding
another special case: when the resource type matches nothing **and** capabilities
are required, the readout says *"No resources have these capabilities."* True,
and actively misleading — it sends an editor hunting for untagged resources when
they have actually mistyped the type key.

## What Changes

- **The readout becomes a three-stage funnel.** Resolution is three successive
  filters, and the diagnostic reports the whole chain rather than a single
  number, naming the stage that reached zero:

  ```
    resources of type "room"          10   ← wrong type key, or none exist yet
         ├─ 7 lack a required capability
    with these capabilities            3   ← over-narrow capabilities
         ├─ 2 cannot provide 4 hours
    can provide this service           1   ← duration vs resource maximums
       (Red Room, Blue Room: 2h maximum)
  ```

  The *drop* between stages carries as much information as the totals: `10 → 3 →
  1` tells a different story from `10 → 10 → 1`. Reporting the whole chain is
  what makes the ⑧ conflation impossible rather than merely corrected.

- **Core owns the computation, and there is only one of it.** The funnel is the
  computation; `ResolveCandidatesAsync` projects its candidate list from the
  funnel rather than computing separately. This follows ⑦-1's precedent, which
  shipped `GetSlotsAsync` and the start-first query as *one computation with two
  projections so they cannot drift*.

- **Resolution takes a role and a duration, not a service.** `Service.Create`
  requires a name; an editor previewing a half-filled form may not have one, and
  inventing an aggregate to satisfy a validator irrelevant to the question is the
  wrong shape. `ResolveCandidatesAsync(serviceId)` loads and delegates.

- **A new management preview endpoint** taking a role and a duration and
  returning the funnel. `POST`, because a duration specification is a structured
  value (kind plus bounds), not a flat scalar.

- **BREAKING (management contract, unpublished):** ⑧'s `GET resources/matching`
  and `IResourceManagementStore.ListMatchingAsync` are **removed**, along with
  the in-memory double's copy of the subset test. They have one consumer, and
  the point of moving the rule into Core is that there is one implementation —
  keeping a second preview endpoint with its own capability filter would
  reinstate exactly the drift risk this change spends effort removing. It also
  retires the ordering divergence between the double and the SQL store that ⑧'s
  QA flagged. `GET resources/capabilities` stays; vocabulary is a different job.

- **The readout moves to a form-level summary** above the editor's groups. Its
  inputs now span both Service Requirements and Duration, and an editor who sets
  a four-hour duration must not have to scroll back to Requirements to discover
  that it just emptied the pool.

- **Type keys gain the length bound capability keys already have.** Discharges
  the obligation logged at ⑧'s QA: `Resource.Type` and `ServiceRole.ResourceType`
  still call bare `NormalizedKey.IsValid` while their columns are `nvarchar(64)`,
  so a well-formed 65-character key passes domain and API validation and fails at
  `INSERT` as a 500 where the spec promises `type-key-invalid`.

- **BREAKING (source, unpublished):** `ResolveCandidatesAsync`'s shape changes.
  Nothing is published and the repository is private, so this is taken cleanly
  rather than shimmed — the same call made for `ServiceDuration` in ⑦-1 and
  `Resource.Create` in ⑧. No schema change and no migration.

## Capabilities

### New Capabilities

None. Every change modifies behaviour an existing spec already owns.

### Modified Capabilities

- `services`: the requirement-row readout becomes a form-level resolution
  summary reporting the funnel, with wording that may now describe what can
  provide the service.
- `service-booking`: eligible-resource resolution is expressed as one
  computation with two projections, and resolves over a role and a duration
  rather than a service id.
- `resource-management`: the role match preview endpoint is removed and replaced
  by a service configuration preview endpoint.
- `resources`: type keys are bounded in length as well as in shape.
- `persistence`: the management store keeps its capability usage projection and
  loses the match projection.

## Non-goals

- **Availability is not part of the funnel.** No open hours, lead time, horizon,
  or conflict checking. Those answer "is there a free slot", which is a different
  question and would turn a configuration summary into a slot search.
- **Eligibility is still not availability.** The summary may now say a resource
  *can provide* the service, because the count is no longer a superset of the
  candidate pool. That is a statement about configuration only. This replaces
  D8's constraint rather than removing all constraint on the wording.
- **No delivery-API change.** This is a backoffice diagnostic; capabilities and
  duration are already published and nothing new is exposed publicly.
- **No multi-role and no role count above one.** Still rejected by
  `Service.Create`.
- **No front-end or Razor change.**

## Impact

**Core** — candidate resolution restructured to produce the funnel, with the
existing pool projected from it; a resolution entry point over
`(ServiceRole, ServiceDuration)`; type-key length validation.

**Persistence** — `IResourceManagementStore.ListMatchingAsync` and its SQL
implementation removed. No schema change, no migration.

**Backoffice** — new preview endpoint and DTOs, the old one removed, a
regenerated client, and the editor's summary moved to form level and rebuilt
around the funnel.

**Tests** — the removed endpoint's tests retire with it. New coverage for the
funnel's stages, for the projection equivalence (the pool the funnel reports must
equal the pool the booking path resolves), for the not-known state, and for the
type-key length boundary. ⑧'s overlapping-pool fixtures carry forward, extended
with resources whose duration ranges differ so the third stage has something to
exclude.
