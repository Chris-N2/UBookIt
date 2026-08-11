Ordered so that an interruption leaves a coherent state: groups 1–2 stand alone (a new endpoint plus a refreshed client), group 3 gives a usable services list, group 4 completes the editor, and group 5 is independent and droppable.

## 1. Resource type usage endpoint

- [x] 1.1 Add `ResourceTypeUsage(string Type, int Count)` and `Task<IReadOnlyList<ResourceTypeUsage>> ListTypesAsync(CancellationToken)` to `IResourceManagementStore` in `UBookIt.Core` — read port untouched (design D1).
- [x] 1.2 Implement it in `UBookIt.Persistence` as a grouped projection over the resources table, ordered by type key for deterministic results. No migration, no eager loading of opening hours or exceptions.
- [x] 1.3 Add `GET resources/types` to `ResourcesController` (`[ApiVersion("1.0")]`, `ubookitbackoffice` group) returning a `ResourceTypeUsageModel` list via the existing controller base, so authorization comes for free.
- [x] 1.4 Unit-test the projection: empty store returns empty; mixed types return correct counts; ordering is stable across calls.
  - Split by what each layer can actually prove: the EF-translated projection is tested against LocalDB in `ResourceManagementStoreTests` (counts, key ordering, stability across calls), since a fake would not have caught the translation failure it did catch. The empty case is asserted at the controller level, where it can be isolated — the integration collection shares one un-reset database, so those tests use per-test unique type keys instead.
- [x] 1.5 Integration-test the endpoint: authorized call returns counts; unauthenticated call returns 401 without reaching handler logic.
  - Authorization is asserted structurally (the controller inherits the `[Authorize]` base), matching the convention change ③ established for every other management endpoint; there is no host-test harness to issue a real unauthenticated request. Also asserts the `resources/types` literal route cannot collide with the guid-constrained id route.
  - **Additionally confirmed live against the running TestSite** (2026-08-11), which the structural assertion alone cannot prove: authorized `GET resources/types` returned 200 with `[{"type":"room","count":2},{"type":"therapist","count":1}]`, matching a cross-check against the full resource list; the same request **anonymous returned 401**; and the literal route resolved rather than being captured by the guid route.

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
  - **Correction (QA).** As first written this was only true of the Requirements group; `err-details` and `err-duration` were rendered as ids that nothing referenced, so two of the three groups had no association at all. Fixed in §8.1 — do not read the original claim as evidence.
- [x] 4.2 Implement the duration radio pair — defer to each resource's minimum (sends `null`) or a fixed value in minutes — with the number input disabled rather than hidden in the defer state (design D4).
- [x] 4.3 Implement Service Requirements as a one-item list with no add/remove control; `count` is not rendered and is always sent as `1` (design D5).
- [x] 4.4 Implement the resource-type combobox: populated from `GET resources/types`, accepting a new key, with a non-blocking hint when the entered type matches no existing resource (design D2).
  - Built as a native `<input list>` + `<datalist>` rather than `uui-combobox`, which structurally cannot hold a value that is not one of its options — see the control-choice note added to design D2. A failure to load the type list degrades to a plain free-text field rather than blocking the editor.
- [x] 4.5 Wire create and update through the generated client; map `service-name-required`, `service-role-invalid`, `service-duration-invalid`, and `type-key-invalid` to their groups; return to the collection view on success.
- [x] 4.6 Verify keyboard operability of the two new control types — the radio group and the combobox — including visible focus and that the hint is exposed to assistive technology.
  - Verified live 2026-08-11. Tab order from Name goes to the type input (datalist attached), then into the radio group, which takes a single tab stop on the checked option (roving tabindex); Down moved the selection to "inherit" and the Minutes input disabled itself reactively. The unknown-type hint is a `role="status"` region referenced from the input's `aria-describedby`.

## 5. Shared confirm modal

- [x] 5.1 Add a small shared helper wrapping `umbOpenModal(host, UMB_CONFIRM_MODAL, …)` from `@umbraco-cms/backoffice/modal` that returns a boolean, catching the rejection that cancellation produces (design D6). Confirmation only — no other behaviour.
- [x] 5.2 Use it for delete in `services-list.element.ts`.
- [x] 5.3 Replace `window.confirm` at `resource-list.element.ts:63` with the same helper, changing nothing else in that file. Discharges change ③'s QA obligation.
- [x] 5.4 Verify both lists: confirming deletes, cancelling and dismissing both leave the item untouched with no request issued and no unhandled rejection in the console.
  - Verified live 2026-08-11. Both lists show the in-page uui modal naming the item, with no native dialog. Cancelling on the services list issued **no DELETE request** (checked against the network log) and produced **no console error or unhandled rejection** — the specific failure the helper's catch exists to prevent. Confirming deleted the service and refreshed the list. On the resources list the modal and cancel path were exercised; delete was deliberately not confirmed, to avoid destroying the dev site's resources.

## 6. Verification and sign-off

- [x] 6.1 Full solution build with `--no-incremental`; no new warnings beyond the documented NU1903 baseline.
  - 0 errors, 38 warnings, all NU1903. `npm run build` (tsc + vite) clean, both section views bundled.
- [x] 6.2 Full test suite green, including the new unit and integration tests.
  - 210 passing (183 unit, 27 integration), 0 skipped. There is no TypeScript test harness in this project, so the new client behaviour is covered by live verification below rather than by unit tests — consistent with change ⑤.
- [x] 6.3 Live verification in the running TestSite backoffice: create a service with a fixed duration, create one deferring to the resource minimum, edit both, exercise the type combobox against existing and unknown types, and delete with both confirm and cancel.
  - Verified live 2026-08-11. Created "Deep tissue massage" (`therapist`, fixed 90) and "Sports physio assessment" (`physiotherapist`, inherit). The list rendered `1 × therapist / 90 minutes` and `1 × physiotherapist / Resource minimum`. Reopening showed every value exactly as entered, with the correct duration mode preselected. The type input suggested the types in use, showed the unknown-type hint for a partial and for the never-used `physiotherapist`, cleared it on an exact match, and never blocked saving. A failed save surfaced the real code and kept all form state.
- [x] 6.4 Confirm the resources view is unchanged in behaviour apart from the delete confirmation, and that a site with no services shows an empty state rather than an error.
  - Verified live 2026-08-11. The services view showed the empty-state copy, not an error, before any service existed. The resources list, editor, paging, and load behaviour are unchanged apart from the confirmation modal; all three resources survived the session intact.
- [ ] 6.5 Run `qa-review` in a fresh context or subagent.
  - First pass 2026-08-11: **REJECT**, four must-fixes, nothing structural. Hard-fail gates all passed (DevExpress scan, no widget abstraction, authorized endpoints, no migration). Remediation in §8; awaiting re-review by the same reviewer.

## 7. Problem-details `type` member (discovered during 6.3, not planned)

Live verification of a failed save surfaced *"A fatal server error occurred"* instead of the domain code. The server was correct — 400 with `{title, status, errors:[{code:"service-name-required"}]}`. The backoffice's `addErrorInterceptor` keeps a response body only when `isProblemDetailsLike()` passes, and that predicate requires a **`type`** member; our `ProblemDetails` never set one, so the interceptor discarded the payload, `errors` included, and substituted its own generic problem. The identical failure was then reproduced in the **resource** editor, making this a pre-existing defect from change ③ that silently violated `resource-management`'s "Validation failure is surfaced per field" — not a regression introduced here.

- [x] 7.1 Set `Type` (`ValidationFailed` / `NotFound` / `Conflict`) in `UBookIt.Backoffice/Mapping/ApiResults.cs`, with a comment recording why its absence is fatal rather than cosmetic.
- [x] 7.2 Apply the same to both methods in `UBookIt.Web/Mapping/ApiResults.cs`, keeping the two envelopes from drifting. Additive: the member was previously absent. Delivery consumers are unaffected by the backoffice interceptor, so this is consistency rather than a fix.
- [x] 7.3 Add regression coverage: a theory over all three statuses asserting `Type` is populated, plus an assertion in the delivery suite's shared `Problem()` helper so every existing delivery failure test now guards it too.
- [x] 7.4 Re-verify live that both editors surface real domain messages.
  - Services: "A service name is required." Resources: "A display name is required." Both appear in the announced error summary **and** the Details group, with the summary receiving focus (confirmed by reading `document.activeElement` through the shadow roots: `div#error-summary[role=alert][tabindex="-1"]`) and no form state lost.

## 8. QA remediation (first review pass)

- [x] 8.1 **[MAJOR]** Associate `err-details` and `err-duration` with their controls. Details and Duration now wrap their fields in a `fieldset` with a visually-hidden `legend` and a conditional `aria-describedby`, matching the Requirements group — previously both ids were rendered with no referrer, so the association silently did not exist.
- [x] 8.2 Trim the resource type in `#buildRequest`, so the hint and the payload evaluate the same value. `" room "` previously looked known (hint hidden) yet was rejected server-side as `type-key-invalid`.
- [x] 8.3 Track whether the type list actually loaded (`_knownTypesLoaded`). An empty list can mean "no resources" or "the request failed"; the hint is now suppressed in the failure case instead of asserting that every type entered is unused.
- [x] 8.4 Make a non-cancel modal rejection visible. `confirmDestructive` returns `"confirmed" | "cancelled" | "failed"` rather than a boolean: a rejection carrying an `Error` (for example `umbOpenModal`'s `Error('Modal manager not found.')`) is logged and surfaced to the user, while a dismissal — which rejects with `{type:'close'}` or nothing — stays silent. Both lists handle the three outcomes.
- [x] 8.5 Also taken from the same review, all non-blocking: the unknown-type live region is now always present and only its text changes (a `role="status"` inserted together with its content is often not announced); `uui-radio-group` gained an accessible name; an emptied Minutes field no longer collapses to `0` mid-edit; the services list no longer renders the "no services yet" empty state when the load actually failed; and `—` / `1 × masseur` moved into localization.
- [x] 8.6 Rebuild and retest: `dotnet build --no-incremental` 0 errors / 38 NU1903, `npm run build` clean, 210 tests passing.

Left as recorded decisions rather than changes, with reasoning:
- `type` as a bare token (`ValidationFailed`) rather than a URI reference. RFC 7807 prescribes a URI, but Umbraco's own problem bodies use bare tokens and the interceptor only tests for the member's presence. Consistency with the host wins; noted so it reads as a choice.
- No index on `uBookItResource.Type` for the grouped projection. The result is bounded by distinct type count and the table is small by nature; adding an index is a migration this change deliberately does not have.
- `ListTypesAsync` on the public `IResourceManagementStore` is source-breaking for any external implementor. The package is unreleased and the port is realistically Persistence-only, but the proposal's "no breaking change" wording is imprecise — flagged for the release notes rather than reworded now.
- The management API has no `InvalidModelStateResponseFactory`, so a malformed management request body still returns ASP.NET's default envelope without `errors`. Pre-existing and outside this change; a candidate for the CI/hardening pass alongside the other envelope work.
