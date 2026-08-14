## 1. Core resolution

- [ ] 1.1 Add a resolution result type carrying the three stages — resources of the type, those satisfying the capabilities, those whose range admits the duration — plus the duration-stage exclusions with the bound that excluded each.
- [ ] 1.2 Restructure `ResolveCandidatesAsync` so the funnel is the single computation and the candidate pool is projected from it (design D1). There must be no second filtering path.
- [ ] 1.3 Change the resolution entry point to take `(ServiceRole, ServiceDuration)`; make `ResolveCandidatesAsync(serviceId)` load the service and delegate (design D2). Resolution must not require a name or an id.
- [ ] 1.4 Add the `NormalizedKey.MaxLength` bound to `Resource.Type` and to `ServiceRole.ResourceType` validation, failing with the existing `type-key-invalid` code (design D8).
- [ ] 1.5 Unit tests for the funnel: each stage's count, an empty pool attributed to the correct stage, duration exclusions naming resources and bounds, and a healthy configuration excluding nothing.
- [ ] 1.6 Unit test that resolution works for a role and duration with no service and no name — the case a transient `Service` would have blocked.
- [ ] 1.7 Equivalence test: the funnel's final stage equals the candidate pool the booking path resolves, for the same role and duration. This is the property D1 exists to guarantee.
- [ ] 1.8 Unit tests for the type-key length boundary on both `Resource.Create` and `Service.Create` — at the limit accepted, one over rejected with `type-key-invalid`.
- [ ] 1.9 Mutation-check the funnel tests: reorder the stages, drop a stage, and swap which stage an exclusion is attributed to. A test suite that stays green under any of those is not covering the attribution, which is the whole point of the change.

## 2. Removing the superseded surface

- [ ] 2.1 Remove `IResourceManagementStore.ListMatchingAsync`, its SQL implementation, and the `ResourceMatch` type if nothing else uses it (design D4).
- [ ] 2.2 Remove the in-memory double's `ListMatchingAsync`, which carried the second copy of the subset test and an ordering that could diverge from the SQL store's.
- [ ] 2.3 Remove `GET resources/matching`, its DTOs, and its tests.
- [ ] 2.4 Confirm `GET resources/capabilities` and its tests are untouched — vocabulary is a different job and stays.

## 3. Management API

- [ ] 3.1 Add the preview request DTO (role: resource type + required capabilities; duration: the existing `ServiceDurationModel` shape) and the response DTO carrying the three stages and the duration exclusions.
- [ ] 3.2 Add the `POST` preview endpoint, obtaining its answer from Core resolution rather than computing eligibility (design D3), with the same authorization policy as every other management endpoint.
- [ ] 3.3 Reject malformed type and capability keys with their existing stable codes rather than silently narrowing the configuration.
- [ ] 3.4 Accept a configuration with no service name and one no saved service holds.
- [ ] 3.5 Tests: the chain for a configuration, unused type key giving an empty first stage rather than an error, no-capabilities matching the whole type, malformed key rejected, and the authorization guarantee asserted the way this repo asserts it.
- [ ] 3.6 Integration test that the endpoint's final stage equals what Core resolves for a saved service with the same role and duration.

## 4. Backoffice client

- [ ] 4.1 Start the TestSite against the new build and regenerate the client — it reads the live swagger, so the site must be running with the new endpoint present and the old one gone.
- [ ] 4.2 Move the readout out of Service Requirements to a form-level summary above the groups (design D6).
- [ ] 4.3 Render the three stages, attributing an empty pool to the stage responsible, and list the duration-excluded resources with their limiting bound.
- [ ] 4.4 Refresh on type, capability **and** duration change; debounce the free-text inputs, fire immediately for discrete choices.
- [ ] 4.5 Keep the not-known discipline per stage: silence, never zero, when the configuration cannot be resolved or the request failed (design D7).
- [ ] 4.6 Keep the snapshot discipline: phrasing derived from state captured with the response, never from live state, and a stale-request token so an earlier reply cannot overwrite a later one.
- [ ] 4.7 Wording may say what can provide the service; it must not say available, free, or bookable (design D5).
- [ ] 4.8 Accessibility: the summary is a live region that announces changes, every id it references resolves in its own shadow root, and the layout change keeps keyboard order sensible.

## 5. Verification

- [ ] 5.1 Full solution build with `--no-incremental`, TestSite stopped first. Only the known NU1903 advisories are acceptable.
- [ ] 5.2 Full unit and integration run, all green, including every ⑤/⑥/⑦/⑧ scenario unchanged — booking behaviour must be untouched by construction.
- [ ] 5.3 Live backoffice verification: configure a service whose duration no resource can provide and confirm the summary attributes it to the duration stage and names the resources; then mistype the type key and confirm it attributes that to the type stage rather than to capabilities.
- [ ] 5.4 Assert the summary's live region and control associations by reading the shadow DOM, not from screenshots.
- [ ] 5.5 Confirm the removed endpoint is gone from the generated client and the swagger document.
- [ ] 5.6 Stop the TestSite and check port 44348 for orphaned processes.

## 6. Handover

- [ ] 6.1 Record that the `Resource.Type` length obligation from ⑧'s QA is discharged.
- [ ] 6.2 Record that ⑧'s design D8 is discharged by evaluation, and that "eligibility is not availability" replaces it as the standing constraint on this wording.
- [ ] 6.3 At spec-sync time, grep the sibling main specs for forward-looking sentences this change falsifies — including any that describe the readout as capability-only or reference the removed endpoint. This has found something on each of the last three changes.
