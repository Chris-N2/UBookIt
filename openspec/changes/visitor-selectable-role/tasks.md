## 1. Domain — the flag

- [ ] 1.1 Add visitor-selectability to `ServiceRole` (`src/UBookIt.Core/Services/Service.cs`), defaulting to not selectable, with a doc comment stating design D1 and D8: the flag says which pool is *offered*, never what placement accepts, and it is not concealment because pools are already computable from public reads.
- [ ] 1.2 Add the parameter to `ServiceRole.Create`. Pass by name at every call site rather than relying on position — `direct-booking-opt-in` recorded that a positional shift was caught by luck, not by design.
- [ ] 1.3 Validate at most one visitor-selectable role in `Service.Create`, with its own stable failure code identifying **both** roles in conflict. Do not clear the flag on all but one: which the editor meant is not derivable.
- [ ] 1.4 Confirm the duplicate-role rule ignores the flag, and that the canonical total order (type → capabilities → count) is unchanged and still total.
- [ ] 1.5 Unit tests: default is off; one selectable role among several is accepted; two are rejected with the new code naming both; two otherwise-identical roles differing only in the flag are still rejected as duplicates; a selectable role with an empty pool still saves.

## 2. Persistence

- [ ] 2.1 Add the column to `ServiceRoleRow` and the EF configuration; generate an additive migration adding a `bit` with `defaultValue: false` to `uBookItServiceRole`, following `20260817080001_AddDirectBookability`.
- [ ] 2.2 Map the flag both ways in the service store, so a role round-trips.
- [ ] 2.3 Integration test: a service with a visitor-selectable role saves, reloads value-equal, and a pre-existing role loads as not selectable.

## 3. Core — pinned availability

- [ ] 3.1 Add an optional pinned resource id to `IServiceBookingService.GetBookableStartsAsync` and its implementation. Reject a pin in no role's pool with `resource-not-eligible`, **after** the range and service-existence checks so those are reported in preference.
- [ ] 3.2 Thread the pin into `FeasibleRuns` so the general path calls `SlotAssignment.TrySaturateIncluding` instead of `TrySaturate` — one call site, same computation (design D2).
- [ ] 3.3 **The single-slot fast path (design D4).** `FeasibleRuns` short-circuits when `slots.Count == 1` and returns the union without consulting the assignment. Under a pin it MUST filter that role's runs to the pinned candidate. This is the commonest configuration in the product, and threading only the general path ships a pin that is silently ignored on most sites.
- [ ] 3.4 Test 3.3 directly: a **single-role, count-1** service with two candidates, pinning the one that is fully claimed, must offer no starts while the other is free all day. Mutation-check it — revert the fast-path filter and confirm this test, and only this test, goes red.
- [ ] 3.5 Tests: a pinned answer is a subset of the unpinned one over the same range; an unpinned query is byte-for-byte what it was; a resource eligible for two overlapping-pool roles is pinned into either; a start offered by a pinned query places successfully with the same pin.
- [ ] 3.6 Tests for the pin rejections: unknown resource, wrong type, missing capability, and an unknown service reported as `service-not-found` in preference to the pin failure.

## 4. Core — structural unfulfillability lifted out of the Web project

- [ ] 4.1 **Decide the escape hatch first (design D6).** Read what `ServiceBookingFormBuilder.DurationOptions` needs from `BookingForm`. If it is `LengthGrid` and nothing else, lift both into Core and continue. If it pulls in materially more, stop: sections 4 and 7 leave this change as ⑩-1a, the reason code defers again, and the decision is recorded in this file with what was found.
- [ ] 4.2 Add the Core function answering permanent unfulfillability from all three questions — `PoolSufficiency.FindShortfall`, `StartAlignment.FindMisalignment`, and no common length — as a pure function of resolved role candidates. No store, no clock, no HTTP context.
- [ ] 4.3 Repoint `ServiceUnavailableModel.IsUnavailable` at it. ⑩'s existing tests are the regression net: they must pass **unmodified**. A test that needs editing is the signal to take 4.1's escape hatch, not to edit the test.
- [ ] 4.4 Tests: each of the three causes reports permanent; a fully booked but correctly configured service does not; the function is exercised without a host.

## 5. Management API + backoffice editor

- [ ] 5.1 Carry the flag on the service management DTOs, always present rather than omitted when false.
- [ ] 5.2 Surface the new failure code verbatim in the 400 problem-details response, like every other domain code.
- [ ] 5.3 Add the visitor-selectable control to the role row in the services editor, defaulting off, labelled so an editor can see that turning it on publishes that role's resources as choices on the site.
- [ ] 5.4 Where a row's count exceeds 1, state in the row that a visitor chooses one of that many and the rest are assigned.
- [ ] 5.5 Do **not** prevent a second row being switched on client-side; render the server's failure against the rows in conflict with no data lost from the form, as the duplicate-role rule already does.
- [ ] 5.6 Client tests: default off; round-trips; the server failure renders against the conflicting rows and preserves form state; the count-greater-than-one wording appears only when it applies.
- [ ] 5.7 Keyboard and label check on the new control, consistent with the editor's accessibility baseline.

## 6. Delivery API

- [ ] 6.1 Publish the flag on `ServiceRoleReadModel`, always present.
- [ ] 6.2 Add the optional pinned resource id to `GET /services/{id}/bookable-starts`, mapping `resource-not-eligible` to 400 problem details.
- [ ] 6.3 Assert by test that a pinned response body contains **no** resource id — the pin is in the request, and design D3's guarantee that the payload names no resource survives pinning.
- [ ] 6.4 Test that an omitted pin leaves the response exactly as it was.
- [ ] 6.5 Confirm `GET /resources/{id}/bookable-starts` is untouched in route, shape and semantics.

## 7. Delivery API — the reason code (⑨-1a's deferral, gated on 4.1)

- [ ] 7.1 Carry the structural-unfulfillability code on the service bookable-starts response, derived from section 4's Core function and never re-evaluated in the mapper.
- [ ] 7.2 Enforce exclusivity: a response with starts carries no code, and the code never accompanies starts.
- [ ] 7.3 Test that the code discloses no role, type, capability, count, resource or pool size, and that a busy-but-configured service carries no code — the one-directional constraint from ⑩ design D9.

## 8. Front end — the who control

- [ ] 8.1 Add the resource-choice query parameter beside `ubDate` and `ubMins` in `BookingKeys`, and read it in the service flow's input.
- [ ] 8.2 Build the choice list in `ServiceBookingFlow` from the selectable role's resolved pool, ordered by display name with resource id as tiebreak. **Presentation order only** — do not touch pool order, which `resource-pin`, ⑦-2 and ⑨-2 all draw determinism from.
- [ ] 8.3 Render nothing at all when no role is selectable: no control, no hidden field, no code path. Extend ⑩'s source scan over `src/UBookIt.Web/Rendering` and `Views` to assert a pin can only be submitted for a selectable role, keeping its non-vacuity assertion (the delivery mapper must still be found).
- [ ] 8.4 Pass the choice to `GetBookableStartsAsync` so the times shown are the times that choice can be honoured.
- [ ] 8.5 State in the control, when the selectable role's count exceeds 1, that the visitor chooses one and the rest are assigned.
- [ ] 8.6 Reset a stale choice to "any" at GET **and say so** (design D11); thread the choice into placement as the pinned resource at POST.
- [ ] 8.7 Render `pinned-resource-unavailable` as its own message naming the chosen resource and offering the way forward, never as "no times available".
- [ ] 8.8 Tests over the **page a visitor sees**, not the message string — ⑩'s QA lesson. Include: no control for a non-selectable service; times narrow to the choice; "any" behaves as today; a busy choice reaches the refusal page rather than the no-availability page; a stale link resets and says so; the confirmation reports the chosen resource.
- [ ] 8.9 Walk every literal in `FailureCodes` and assert none of them can produce the wrong page, as ⑩'s `No_placement_failure_can_produce_the_permanent_claim` does — enumerate the space rather than sampling it.

## 9. Accessibility and live verification

- [ ] 9.1 Markup review of the new control: labelled, associated with its own step, semantic, no JavaScript dependency, and the relationship between the choice and the times stated in text rather than implied by layout.
- [ ] 9.2 **Browser verification is mandatory and is not optional because tests are green** — no C# test renders Razor, and two defects of this class have already shipped into a fully green suite. Exercise in the TestSite: a service with a selectable role, choosing a person, a busy person, "any", and a stale link.
- [ ] 9.3 Confirm no tag helper was introduced anywhere under `src/UBookIt.Web/Views`; use `@await Html.PartialAsync("~/…", Model)`. Without a `_ViewImports.cshtml` a tag helper renders as visible text rather than failing the build.
- [ ] 9.4 Keyboard-only pass over the service flow with the new control present.

## 10. Verification and sync

- [ ] 10.1 Full clean build. The warning baseline is **zero** — read the build against zero, not against a remembered baseline.
- [ ] 10.2 Full unit, integration and client suites green. Confirm ⑩'s and `resource-pin`'s tests are unmodified in `git diff`.
- [ ] 10.3 Attack each clause of designs D1, D3, D4 and D8 separately for a clause with no covering test — the ⑩ technique that found the no-common-length gap. Record what was found.
- [ ] 10.4 Sync-time outward grep for sibling specs this change falsifies. It has found something on five consecutive changes.
- [ ] 10.5 **Manual edit at sync, which no delta will make for you**: correct `openspec/specs/delivery-api/spec.md` lines 5 and 8 so the delivery API stops naming the default Razor front end as one of its consumers — it reads Core in-process, and `default-frontend` forbids otherwise. Likely "any *alternative* front-end". Discharges the obligation parked at ⑩'s sync.
- [ ] 10.6 Diff the guarantees of every MODIFIED requirement one final time against `openspec/specs/`: list each SHALL and scenario in the current version and confirm it is carried forward, deliberately dropped with a reason in the proposal, or superseded by a stronger claim.
- [ ] 10.7 Hand to `qa-review` in a **fresh context or subagent** — never the context that wrote the code.
