## 1. Core resolution

- [x] 1.1 Add a resolution result type carrying the three stages — resources of the type, those satisfying the capabilities, those whose range admits the duration — plus the duration-stage exclusions with the bound that excluded each.
- [x] 1.2 Restructure `ResolveCandidatesAsync` so the funnel is the single computation and the candidate pool is projected from it (design D1). There must be no second filtering path.
- [x] 1.3 Change the resolution entry point to take `(ServiceRole, ServiceDuration)`; make `ResolveCandidatesAsync(serviceId)` load the service and delegate (design D2). Resolution must not require a name or an id.
- [x] 1.4 Add the `NormalizedKey.MaxLength` bound to `Resource.Type` and to `ServiceRole.ResourceType` validation, failing with the existing `type-key-invalid` code (design D8).
- [x] 1.5 Unit tests for the funnel: each stage's count, an empty pool attributed to the correct stage, duration exclusions naming resources and bounds, and a healthy configuration excluding nothing.
- [x] 1.6 Unit test that resolution works for a role and duration with no service and no name — the case a transient `Service` would have blocked.
- [x] 1.7 Equivalence test: the funnel's final stage equals the candidate pool the booking path resolves, for the same role and duration. This is the property D1 exists to guarantee.
- [x] 1.8 Unit tests for the type-key length boundary on both `Resource.Create` and `Service.Create` — at the limit accepted, one over rejected with `type-key-invalid`.
- [x] 1.9 Mutation-check the funnel tests: reorder the stages, drop a stage, and swap which stage an exclusion is attributed to. A test suite that stays green under any of those is not covering the attribution, which is the whole point of the change.

## 2. Removing the superseded surface

- [x] 2.1 Remove `IResourceManagementStore.ListMatchingAsync`, its SQL implementation, and the `ResourceMatch` type if nothing else uses it (design D4).
- [x] 2.2 Remove the in-memory double's `ListMatchingAsync`, which carried the second copy of the subset test and an ordering that could diverge from the SQL store's.
- [x] 2.3 Remove `GET resources/matching`, its DTOs, and its tests.
- [x] 2.4 Confirm `GET resources/capabilities` and its tests are untouched — vocabulary is a different job and stays.

## 3. Management API

- [x] 3.1 Add the preview request DTO (role: resource type + required capabilities; duration: the existing `ServiceDurationModel` shape) and the response DTO carrying the three stages and the duration exclusions.
- [x] 3.2 Add the `POST` preview endpoint, obtaining its answer from Core resolution rather than computing eligibility (design D3), with the same authorization policy as every other management endpoint.
- [x] 3.3 Reject malformed type and capability keys with their existing stable codes rather than silently narrowing the configuration.
- [x] 3.4 Accept a configuration with no service name and one no saved service holds.
- [x] 3.5 Tests: the chain for a configuration, unused type key giving an empty first stage rather than an error, no-capabilities matching the whole type, malformed key rejected, and the authorization guarantee asserted the way this repo asserts it.
- [x] 3.6 Integration test that the endpoint's final stage equals what Core resolves for a saved service with the same role and duration.

  **Amended at apply time, and QA agreed the reasoning while flagging that the
  task should have been amended rather than simply ticked.** The equivalence is
  asserted at *endpoint* level in the unit suite
  (`ServicePreviewEndpointTests.Spec_scenario_preview_agrees_with_candidate_resolution`),
  which is where every other management endpoint in this repository is tested,
  and at *Core-over-real-SQL* level in the integration suite
  (`ServicePreviewTests`). It is not asserted at endpoint level over SQL because
  `UBookIt.Tests.Integration` deliberately references only Core and Persistence;
  adding a Backoffice reference would pull Umbraco into the store-level suite,
  which is a repo-shape decision this change should not make unilaterally.

## 4. Backoffice client

- [x] 4.1 Start the TestSite against the new build and regenerate the client — it reads the live swagger, so the site must be running with the new endpoint present and the old one gone.
- [x] 4.2 Move the readout out of Service Requirements to a form-level summary above the groups (design D6).
- [x] 4.3 Render the three stages, attributing an empty pool to the stage responsible, and list the duration-excluded resources with their limiting bound.
- [x] 4.4 Refresh on type, capability **and** duration change; debounce the free-text inputs, fire immediately for discrete choices.
- [x] 4.5 Keep the not-known discipline per stage: silence, never zero, when the configuration cannot be resolved or the request failed (design D7).
- [x] 4.6 Keep the snapshot discipline: phrasing derived from state captured with the response, never from live state, and a stale-request token so an earlier reply cannot overwrite a later one.
- [x] 4.7 Wording may say what can provide the service; it must not say available, free, or bookable (design D5).
- [x] 4.8 Accessibility: the summary is a live region that announces changes, every id it references resolves in its own shadow root, and the layout change keeps keyboard order sensible.

## 4a. QA remediation

- [x] 4a.1 Fix the summary reporting a capability stage when the configuration requires no capabilities (QA MAJOR). Omit the stage: it filtered nothing by construction, so its count stays derivable and no sentence refers to capabilities the editor never entered.
- [x] 4a.2 Extract the phrasing into a pure `resolutionLines` over the snapshot, and add `vitest` plus a covering suite (design D9). Mutation-checked: reverting 4a.1 fails the suite.
- [x] 4a.3 Cover the persistence spec's "No capability matching in storage" over `IResourceManagementStore`, and "Projections stay off the read port" over `IResourceStore` (QA MAJOR). Mutation-checked by re-adding the projection as a default interface method.
- [x] 4a.4 Remove `"bookable"` from the granularity exclusion string — task 4.7 bans the word outright, and a rule with judgement calls in it drifts.
- [x] 4a.5 Remove the by-id chain overload and `DurationExclusion.ResourceId`: public surface with no production consumer, the defect this project has shipped twice.
- [x] 4a.6 Restore the dropped "describes the type when no capabilities are required" scenario into the services delta, with the rule that governs it.

## 5. Verification

- [x] 5.1 Full solution build with `--no-incremental`, TestSite stopped first. Only the known NU1903 advisories are acceptable.
- [x] 5.2 Full unit and integration run, all green, including every ⑤/⑥/⑦/⑧ scenario unchanged — booking behaviour must be untouched by construction.
- [x] 5.3 Live backoffice verification: configure a service whose duration no resource can provide and confirm the summary attributes it to the duration stage and names the resources; then mistype the type key and confirm it attributes that to the type stage rather than to capabilities.
- [x] 5.4 Assert the summary's live region and control associations by reading the shadow DOM, not from screenshots.
- [x] 5.5 Confirm the removed endpoint is gone from the generated client and the swagger document.
- [x] 5.6 Stop the TestSite and check port 44348 for orphaned processes.

## 6. Handover

- [x] 6.1 Record that the `Resource.Type` length obligation from ⑧'s QA is discharged.
- [x] 6.2 Record that ⑧'s design D8 is discharged by evaluation, and that "eligibility is not availability" replaces it as the standing constraint on this wording.
- [x] 6.3 At spec-sync time, grep the sibling main specs for forward-looking sentences this change falsifies — including any that describe the readout as capability-only or reference the removed endpoint. This has found something on each of the last three changes.

  **It found something again — a fourth consecutive change.** Two requirements in
  `specs/services/spec.md` that this change did not otherwise touch describe the
  diagnostic by its old name and shape: "Resource type is chosen from types
  already in use" routes the nothing-matches answer "through the
  matching-resource readout … so that one control reports one answer", and
  "Required capabilities are edited on the requirement row" describes removing
  every capability as restoring "type-only matching". Both survive a sync of the
  requirement this change *does* modify, and both would then name a readout that
  no longer exists. Carried into the services delta as MODIFIED requirements with
  their scenarios re-expressed against the resolution summary's stages.

  Everything else the grep surfaced was already inside a requirement this change
  removes or modifies: the persistence match projection, the whole
  `resource-management` role-match preview requirement (including its
  "differing only by resources Core excludes for reasons other than capability
  matching" scenario), and ⑧'s D8 wording clause at
  `specs/services/spec.md:275`, which sits inside "The requirement row reports
  how many resources match" and is replaced wholesale.
