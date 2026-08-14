## 1. Core domain

- [x] 1.1 Extract the normalized-key predicate into one shared internal helper in `UBookIt.Core.Common`, replacing the `GeneratedRegex` copies in `Resource` and `Service`. Behaviour must be identical — a pure predicate, no path-dependent decisions (design D2).
- [x] 1.2 Add `CapabilitySet` value object: private constructor, validating factory returning `DomainResult`, `Empty`, normalized storage (deduplicated, deterministically ordered), structural equality and `GetHashCode`, and a `Satisfies` subset test. Reject malformed keys rather than normalizing them.
- [x] 1.3 Add `FailureCodes.CapabilityKeyInvalid` (`capability-key-invalid`).
- [x] 1.4 Add `Capabilities` to `Resource`, defaulting to empty, with the `capabilities` parameter placed beside `availability` in `Resource.Create` (design D11). Surface key failures with `capability-key-invalid`.
- [x] 1.5 Add `RequiredCapabilities` to `ServiceRole`, defaulting to empty, and validate role capability keys in `Service.Create` so that a malformed type key and a malformed capability key produce two distinct, separately-fielded failures.
- [x] 1.6 Unit tests for `CapabilitySet`: equality across construction orders and duplicates, record value-equality when carried as a member, subset satisfied/unsatisfied/empty-requirement, and key rejection. Include a test that fails if `Satisfies` is inverted or made an intersection test.
- [x] 1.7 Unit tests for `Resource.Create` and `Service.Create` capability validation, including the two-distinct-failures case.

## 2. Eligibility

- [x] 2.1 Add the capability subset term to `ServiceBookingService.ResolveCandidatesAsync`, evaluated in Core over hydrated capabilities via `CapabilitySet.Satisfies` (design D5). No new read-port method.
- [x] 2.2 Build the shared test fixture set with **overlapping** capability pools (`{Mary} ⊂ {Mary, Frank}`) plus a disjoint pool and a no-capability resource, per design D9. These back both the eligibility and the preview tests.
- [x] 2.3 Unit tests for eligibility: capability narrows the pool, all required capabilities must be present, extra capabilities do not disqualify, capabilities do not cross type boundaries, empty requirement matches the whole type, and overlapping pools resolve independently per role.
- [x] 2.4 Regression tests proving the ⑤/⑥/⑦ paths are unchanged: a service whose role requires no capabilities resolves exactly as before against resources with and without capabilities, and direct-resource booking is untouched.
- [x] 2.5 Mutation-check every test added in 1.6, 2.3 and 2.4 against a deliberately broken `Satisfies` and a removed subset term — a test that passes with the term removed is not covering it (see the test-scenario discipline note).

## 3. Persistence

- [x] 3.1 Add `ResourceCapabilityRow` and `ServiceRoleCapabilityRow` entities with composite primary keys on (owner id, key) and cascade delete from their owners.
- [x] 3.2 Configure both in `UBookItDbContext` and add a **new** migration creating `uBookItResourceCapability` and `uBookItServiceRoleCapability` — do not amend ⑥'s migration (design D10).
- [x] 3.3 Hydrate capabilities in resource reads used by candidate resolution, and in service/role reads. Map to and from `CapabilitySet` in the row mappers.
- [x] 3.4 Persist capability sets on resource and service writes, replacing rather than merging on full update.
- [x] 3.5 Add `ListCapabilitiesAsync` to `IResourceManagementStore` — grouped projection over the capability table with counts, ordered by key. Project the `GROUP BY` to an anonymous type and construct the record after materialization (EF cannot translate a positional record constructor inside a grouping projection).
- [x] 3.6 Add the match projection to `IResourceManagementStore`: resources of a type carrying every required capability, returning id and display name.
- [x] 3.7 Integration tests on real SQL Server: capability round-trip for resources and roles, empty set round-trips as empty, owner delete removes capability rows, duplicate key rejected at the schema level, usage projection counts and ordering, match projection results.
- [x] 3.8 Integration test asserting the preview's match projection agrees with Core's candidate resolution on the same fixture, differing only by non-capability exclusions (design D6).

## 4. Management API

- [x] 4.1 Add capabilities to the resource create/update/read DTOs and mappers; treat an omitted collection as empty.
- [x] 4.2 Add required capabilities to the service role DTOs and mappers.
- [x] 4.3 Add the capability usage endpoint to `ResourcesController`, mirroring the resource type usage endpoint including its authorization policy.
- [x] 4.4 Add the role match preview endpoint — `GET` with a repeated `capability` query parameter (design D6) — with the same authorization policy.
- [x] 4.5 Confirm `capability-key-invalid` maps to a 400 problem-details response carrying a `type` member, and that its field identifies the capability control rather than the type control.
- [x] 4.6 Tests for the new endpoints. The authorization guarantee is asserted the way this repo already asserts it — by checking the controller inherits the `[Authorize]` backoffice base — not by issuing an unauthenticated request; there is no host test harness that could observe a real 401.

## 5. Backoffice client

- [x] 5.1 Start the TestSite against the new build and regenerate the client (`npm run generate-client` reads the live swagger — the site must be running).
- [x] 5.2 Add a Capabilities section to the resource workspace editor: add/remove keys, offering those already in use, permitting a new one.
- [x] 5.3 Add required-capability editing to the service requirement row, offering keys already in use, permitting a new one, and permitting removal of all of them.
- [x] 5.4 Associate `capability-key-invalid` failures with the capability control in both editors, with no data loss from the form.
- [x] 5.5 Add the matching-resource readout to the requirement row, refreshing on type or capability change, informational and non-blocking, working for a service that has never been saved.
- [x] 5.6 Word the readout to claim capability matching only — "N rooms have these capabilities", never "can provide this service" (design D8). Remove the now-redundant type-specific "no resources have this type" hint, which the readout subsumes.
- [x] 5.7 Accessibility for the new controls: labelled inputs, programmatically associated errors, keyboard operability with visible focus, `uui-*` components, and a readout that is announced when it changes rather than only visible.

## 6. Delivery API

- [x] 6.1 Add capabilities to the delivery resource read model and mapper, deterministically ordered, empty collection rather than null.
- [x] 6.2 Add required capabilities to the delivery service read model and mapper, same treatment.
- [x] 6.3 Integration tests: both models expose capabilities, an empty set serializes as an empty collection, and a consumer can compute the candidate pool from the two reads alone.
- [x] 6.4 Confirm no `UBookIt.Web` rendering change was made — no Razor, ViewComponent, or default-frontend edit is in scope.

## 7. Verification

- [x] 7.1 Full solution build with `--no-incremental` (stop the TestSite first — a running site breaks the build with copy locks). Only the known NU1903 transitive advisories are acceptable; every compiler, analyzer and TypeScript warning must be fixed.
- [x] 7.2 Full unit and integration test run, all green.
- [x] 7.3 Live verification in the running backoffice: tag a resource, require that capability on a service, watch the readout change, and confirm the readout's wording. Assert control associations by reading the shadow DOM via `javascript_tool`, not from screenshots.
- [x] 7.4 Live verification of both delivery reads through the scratchpad API script, reading raw `.Content` rather than `Invoke-RestMethod` output.
- [x] 7.5 End-to-end: book a service whose role requires a capability, confirm it places on an eligible resource; then require a capability nothing carries and confirm the failure is the expected code, not an exception.
- [x] 7.6 Stop the TestSite and check port 44348 for orphaned processes.

## 8. Handover

- [x] 8.1 Record ⑧a `duration-exclusion-diagnostic` in the deferred-obligations memory **with its provenance** — it closes a ⑦-1 hazard, not leftover ⑧ scope — including the verified seam (three scalar columns on `uBookItResource`, additive parameters and response field, reuses `TryResolveAgainst`).
- [x] 8.2 Record that ⑦-2's design D9 obligation is discharged by publication rather than deferred again, so ⑨ does not reopen it.
- [ ] 8.3 At spec-sync time, grep the sibling main specs for forward-looking sentences this change falsifies — the "eligibility by type key alone" sentence is handled in the delta, but check for others (this has found something on each of the last two changes).
