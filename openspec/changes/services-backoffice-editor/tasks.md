Ordered so that an interruption leaves a coherent state: groups 1–2 stand alone (a new endpoint plus a refreshed client), group 3 gives a usable services list, group 4 completes the editor, and group 5 is independent and droppable.

## 1. Resource type usage endpoint

- [x] 1.1 Add `ResourceTypeUsage(string Type, int Count)` and `Task<IReadOnlyList<ResourceTypeUsage>> ListTypesAsync(CancellationToken)` to `IResourceManagementStore` in `UBookIt.Core` — read port untouched (design D1).
- [x] 1.2 Implement it in `UBookIt.Persistence` as a grouped projection over the resources table, ordered by type key for deterministic results. No migration, no eager loading of opening hours or exceptions.
- [x] 1.3 Add `GET resources/types` to `ResourcesController` (`[ApiVersion("1.0")]`, `ubookitbackoffice` group) returning a `ResourceTypeUsageModel` list via the existing controller base, so authorization comes for free.
- [x] 1.4 Unit-test the projection: empty store returns empty; mixed types return correct counts; ordering is stable across calls.
  - Split by what each layer can actually prove: the EF-translated projection is tested against LocalDB in `ResourceManagementStoreTests` (counts, key ordering, stability across calls), since a fake would not have caught the translation failure it did catch. The empty case is asserted at the controller level, where it can be isolated — the integration collection shares one un-reset database, so those tests use per-test unique type keys instead.
- [x] 1.5 Integration-test the endpoint: authorized call returns counts; unauthenticated call returns 401 without reaching handler logic.
  - Authorization is asserted structurally (the controller inherits the `[Authorize]` base), matching the convention change ③ established for every other management endpoint; there is no host-test harness to issue a real unauthenticated request. Also asserts the `resources/types` literal route cannot collide with the guid-constrained id route.

## 2. Regenerate the TypeScript client

- [x] 2.1 Start the TestSite, run `npm run generate-client` in `src/UBookIt.Backoffice/Client`, and confirm `sdk.gen.ts` now exposes the service operations plus the new types operation.
  - All 11 operations present: the 5 existing resource ones, `listServices`/`getService`/`createService`/`updateService`/`deleteService`, and `listResourceTypes`. TestSite stopped afterwards and port 44348 confirmed clear.
- [x] 2.2 Review the generated diff for unrelated drift from endpoint changes since the last run; investigate anything unexpected rather than accepting it silently (design D8).
  - No drift. Two files changed (`sdk.gen.ts`, `types.gen.ts`), 288 insertions and exactly one deletion — the import line, rewritten to include the new symbols. No existing resource operation or type was altered. `ServiceRequestModel.durationMinutes` generated as `number | null`, which is what the duration radio pair (design D4) needs.
- [x] 2.3 Confirm `npm run build` succeeds with no TypeScript errors or warnings.

## 3. Services collection view

- [x] 3.1 Add a `ubookitServices_*` block to `localization/en-us.ts` covering every user-facing string in the new UI — list headings, actions, editor labels, duration modes, hints, and error fallbacks.
  - Also added three `ubookitResources_confirmDelete*` keys: converting the resource list's delete to the shared modal (5.3) moved its previously hard-coded English into localization.
- [x] 3.2 Register a second `sectionView` in `section/manifest.ts` for Services, alongside Resources, with its own `pathname` and icon (design D3).
- [x] 3.3 Add `services-view.element.ts` mirroring `resources-view.element.ts`: component-state routing between list and editor, no URL routing.
- [x] 3.4 Add `services-list.element.ts`: `uui-table` of name, requirement summary, and duration (rendering the resource-minimum fallback in words when duration is null), with paging over `listServices` and create/edit/delete affordances.
- [x] 3.5 Render delete and load failures from the server-supplied message with a generic fallback only when absent — no per-code hard-coded strings (design D7).

## 4. Services workspace editor

- [x] 4.1 Add `services-editor.element.ts` with Details, Service Requirements, and Duration groups, reusing the resource editor's error-summary pattern: `role="alert"`, focus moved to the summary on failed save, group errors associated via `aria-describedby`, and no form state lost on failure.
- [x] 4.2 Implement the duration radio pair — defer to each resource's minimum (sends `null`) or a fixed value in minutes — with the number input disabled rather than hidden in the defer state (design D4).
- [x] 4.3 Implement Service Requirements as a one-item list with no add/remove control; `count` is not rendered and is always sent as `1` (design D5).
- [x] 4.4 Implement the resource-type combobox: populated from `GET resources/types`, accepting a new key, with a non-blocking hint when the entered type matches no existing resource (design D2).
  - Built as a native `<input list>` + `<datalist>` rather than `uui-combobox`, which structurally cannot hold a value that is not one of its options — see the control-choice note added to design D2. A failure to load the type list degrades to a plain free-text field rather than blocking the editor.
- [x] 4.5 Wire create and update through the generated client; map `service-name-required`, `service-role-invalid`, `service-duration-invalid`, and `type-key-invalid` to their groups; return to the collection view on success.
- [ ] 4.6 Verify keyboard operability of the two new control types — the radio group and the combobox — including visible focus and that the hint is exposed to assistive technology.
  - **Blocked on a logged-in backoffice session** (verification, not implementation).

## 5. Shared confirm modal

- [x] 5.1 Add a small shared helper wrapping `umbOpenModal(host, UMB_CONFIRM_MODAL, …)` from `@umbraco-cms/backoffice/modal` that returns a boolean, catching the rejection that cancellation produces (design D6). Confirmation only — no other behaviour.
- [x] 5.2 Use it for delete in `services-list.element.ts`.
- [x] 5.3 Replace `window.confirm` at `resource-list.element.ts:63` with the same helper, changing nothing else in that file. Discharges change ③'s QA obligation.
- [ ] 5.4 Verify both lists: confirming deletes, cancelling and dismissing both leave the item untouched with no request issued and no unhandled rejection in the console.
  - **Blocked on a logged-in backoffice session** (verification, not implementation).

## 6. Verification and sign-off

- [x] 6.1 Full solution build with `--no-incremental`; no new warnings beyond the documented NU1903 baseline.
  - 0 errors, 38 warnings, all NU1903. `npm run build` (tsc + vite) clean, both section views bundled.
- [x] 6.2 Full test suite green, including the new unit and integration tests.
  - 207 passing (180 unit, 27 integration), 0 skipped. There is no TypeScript test harness in this project, so the new client behaviour is covered by live verification below rather than by unit tests — consistent with change ⑤.
- [ ] 6.3 Live verification in the running TestSite backoffice: create a service with a fixed duration, create one deferring to the resource minimum, edit both, exercise the type combobox against existing and unknown types, and delete with both confirm and cancel.
  - **Blocked on a logged-in backoffice session.**
- [ ] 6.4 Confirm the resources view is unchanged in behaviour apart from the delete confirmation, and that a site with no services shows an empty state rather than an error.
  - **Blocked on a logged-in backoffice session.**
- [ ] 6.5 Run `qa-review` in a fresh context or subagent.
