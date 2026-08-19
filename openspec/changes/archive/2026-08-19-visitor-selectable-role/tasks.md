## 1. Domain — the flag

- [x] 1.1 Add visitor-selectability to `ServiceRole` (`src/UBookIt.Core/Services/Service.cs`), defaulting to not selectable, with a doc comment stating design D1 and D8: the flag says which pool is *offered*, never what placement accepts, and it is not concealment because pools are already computable from public reads.
- [x] 1.2 Add the parameter to `ServiceRole.Create`. Pass by name at every call site rather than relying on position — `direct-booking-opt-in` recorded that a positional shift was caught by luck, not by design.
- [x] 1.3 Validate at most one visitor-selectable role in `Service.Create`, with its own stable failure code identifying **both** roles in conflict. Do not clear the flag on all but one: which the editor meant is not derivable.
- [x] 1.4 Confirm the duplicate-role rule ignores the flag, and that the canonical total order (type → capabilities → count) is unchanged and still total.
- [x] 1.5 Unit tests: default is off; one selectable role among several is accepted; two are rejected with the new code naming both; two otherwise-identical roles differing only in the flag are still rejected as duplicates; a selectable role with an empty pool still saves.

## 2. Persistence

- [x] 2.1 Add the column to `ServiceRoleRow` and the EF configuration; generate an additive migration adding a `bit` with `defaultValue: false` to `uBookItServiceRole`, following `20260817080001_AddDirectBookability`.
- [x] 2.2 Map the flag both ways in the service store, so a role round-trips.
- [x] 2.3 Integration test: a service with a visitor-selectable role saves, reloads value-equal, and a pre-existing role loads as not selectable.

## 3. Core — pinned availability

- [x] 3.1 Add an optional pinned resource id to `IServiceBookingService.GetBookableStartsAsync` and its implementation. Reject a pin in no role's pool with `resource-not-eligible`, **after** the range and service-existence checks so those are reported in preference.
- [x] 3.2 Thread the pin into `FeasibleRuns` so the general path calls `SlotAssignment.TrySaturateIncluding` instead of `TrySaturate` — one call site, same computation (design D2).
- [x] 3.3 **The single-slot fast path (design D4).** `FeasibleRuns` short-circuits when `slots.Count == 1` and returns the union without consulting the assignment. Under a pin it MUST filter that role's runs to the pinned candidate. This is the commonest configuration in the product, and threading only the general path ships a pin that is silently ignored on most sites.
- [x] 3.4 Test 3.3 directly: a **single-role, count-1** service with two candidates, pinning the one that is fully claimed, must offer no starts while the other is free all day. Mutation-check it — revert the fast-path filter and confirm this test, and only this test, goes red.
- [x] 3.5 Tests: a pinned answer is a subset of the unpinned one over the same range; an unpinned query is byte-for-byte what it was; a resource eligible for two overlapping-pool roles is pinned into either; a start offered by a pinned query places successfully with the same pin.
- [x] 3.6 Tests for the pin rejections: unknown resource, wrong type, missing capability, and an unknown service reported as `service-not-found` in preference to the pin failure.

## 4. Core — structural unfulfillability lifted out of the Web project

- [x] 4.1 **Decided: the lift proceeds.** `ServiceBookingFormBuilder.DurationOptions`
      needs exactly one thing from `BookingForm` — `LengthGrid`, a pure
      twelve-line function over three ints with no host, no clock and no store.
      Nothing else of `BookingForm` (zone resolution, `TodayIn`, `NotBefore`,
      `ToOption`) is reachable from it. So the triad lifted as planned: `LengthGrid`
      became `UBookIt.Core.Availability.LengthGrid.Minutes`, the intersection became
      `ServiceFulfillability.CommonLengthMinutes`, and the three questions became
      `ServiceFulfillability.IsPermanentlyUnfulfillable`. `BookingForm.LengthGrid`
      remains as a one-line delegation so the resource flow's call sites are
      untouched. Sections 4 and 7 stay in this change; the reason code ships.
      **⑩'s tests passed unmodified through the whole move** — the escape hatch
      was never reached.
- [x] 4.2 Add the Core function answering permanent unfulfillability from all three questions — `PoolSufficiency.FindShortfall`, `StartAlignment.FindMisalignment`, and no common length — as a pure function of resolved role candidates. No store, no clock, no HTTP context.
- [x] 4.3 Repoint `ServiceUnavailableModel.IsUnavailable` at it. ⑩'s existing tests are the regression net: they must pass **unmodified**. A test that needs editing is the signal to take 4.1's escape hatch, not to edit the test.
- [x] 4.4 Tests: each of the three causes reports permanent; a fully booked but correctly configured service does not; the function is exercised without a host.

## 5. Management API + backoffice editor

- [x] 5.1 Carry the flag on the service management DTOs, always present rather than omitted when false.
- [x] 5.2 Surface the new failure code verbatim in the 400 problem-details response, like every other domain code.
- [x] 5.3 Add the visitor-selectable control to the role row in the services editor, defaulting off, labelled so an editor can see that turning it on publishes that role's resources as choices on the site.
- [x] 5.4 Where a row's count exceeds 1, state in the row that a visitor chooses one of that many and the rest are assigned.
- [x] 5.5 Do **not** prevent a second row being switched on client-side; render the server's failure against the rows in conflict with no data lost from the form, as the duplicate-role rule already does.
- [x] 5.6 Client tests: default off; round-trips; the server failure renders against the conflicting rows and preserves form state; the count-greater-than-one wording appears only when it applies.
- [x] 5.7 Keyboard and label check on the new control, consistent with the editor's accessibility baseline.

## 6. Delivery API

- [x] 6.1 Publish the flag on `ServiceRoleReadModel`, always present.
- [x] 6.2 Add the optional pinned resource id to `GET /services/{id}/bookable-starts`, mapping `resource-not-eligible` to 400 problem details.
- [x] 6.3 Assert by test that a pinned response body contains **no** resource id — the pin is in the request, and design D3's guarantee that the payload names no resource survives pinning.
- [x] 6.4 Test that an omitted pin leaves the response exactly as it was.
- [x] 6.5 Confirm `GET /resources/{id}/bookable-starts` is untouched in route, shape and semantics.

## 7. Delivery API — the reason code (⑨-1a's deferral, gated on 4.1)

- [x] 7.1 Carry the structural-unfulfillability code on the service bookable-starts response, derived from section 4's Core function and never re-evaluated in the mapper.
- [x] 7.2 Enforce exclusivity: a response with starts carries no code, and the code never accompanies starts.
- [x] 7.3 Test that the code discloses no role, type, capability, count, resource or pool size, and that a busy-but-configured service carries no code — the one-directional constraint from ⑩ design D9.

## 8. Front end — the who control

- [x] 8.1 Add the resource-choice query parameter beside `ubDate` and `ubMins` in `BookingKeys`, and read it in the service flow's input.
- [x] 8.2 Build the choice list in `ServiceBookingFlow` from the selectable role's resolved pool, ordered by display name with resource id as tiebreak. **Presentation order only** — do not touch pool order, which `resource-pin`, ⑦-2 and ⑨-2 all draw determinism from.
- [x] 8.3 Render nothing at all when no role is selectable: no control, no hidden field, no code path. Extend ⑩'s source scan over `src/UBookIt.Web/Rendering` and `Views` to assert a pin can only be submitted for a selectable role, keeping its non-vacuity assertion (the delivery mapper must still be found).
- [x] 8.4 Pass the choice to `GetBookableStartsAsync` so the times shown are the times that choice can be honoured.
- [x] 8.5 State in the control, when the selectable role's count exceeds 1, that the visitor chooses one and the rest are assigned.
- [x] 8.6 Reset a stale choice to "any" at GET **and say so** (design D11); thread the choice into placement as the pinned resource at POST.
- [x] 8.7 Render `pinned-resource-unavailable` as its own message naming the chosen resource and offering the way forward, never as "no times available".
- [x] 8.8 Tests over the **page a visitor sees**, not the message string — ⑩'s QA lesson. Include: no control for a non-selectable service; times narrow to the choice; "any" behaves as today; a busy choice reaches the refusal page rather than the no-availability page; a stale link resets and says so; the confirmation reports the chosen resource.
- [x] 8.9 Walk every literal in `FailureCodes` and assert none of them can produce the wrong page, as ⑩'s `No_placement_failure_can_produce_the_permanent_claim` does — enumerate the space rather than sampling it.

## 9. Accessibility and live verification

- [x] 9.1 Markup review of the new control: labelled, associated with its own step, semantic, no JavaScript dependency, and the relationship between the choice and the times stated in text rather than implied by layout.
- [x] 9.2 **Browser verification is mandatory and is not optional because tests are green** — no C# test renders Razor, and two defects of this class have already shipped into a fully green suite. Exercise in the TestSite: a service with a selectable role, choosing a person, a busy person, "any", and a stale link.
- [x] 9.3 Confirm no tag helper was introduced anywhere under `src/UBookIt.Web/Views`; use `@await Html.PartialAsync("~/…", Model)`. Without a `_ViewImports.cshtml` a tag helper renders as visible text rather than failing the build.
- [x] 9.4 Keyboard-only pass over the service flow with the new control present.

## 10. Verification and sync

- [x] 10.1 Full clean build. The warning baseline is **zero** — read the build against zero, not against a remembered baseline.
- [x] 10.2 Full unit, integration and client suites green. Confirm ⑩'s and `resource-pin`'s tests are unmodified in `git diff`.

      760 unit, 58 integration, 69 client — all green; clean build at zero
      warnings. `resource-pin`'s tests (`ServiceBookingTests`,
      `MultiRolePlacementTests`, `SlotAssignmentTests`) are untouched, and so is
      every ⑩ test **except three, all of them enumerations this change's spec
      deliberately widens**, recorded here rather than buried in a diff:

      - `The_only_flow_state_in_the_URL_is_…` — the URL-key enumeration gains
        `ubWho`. `default-frontend`'s "Flow state is carried in the URL" now names
        the chosen resource, so the assertion had to grow by exactly one.
      - `Contact_details_never_appear_in_a_URL` — the `BookingKeys` names
        `BookingSubject.cs` may reference, for the same reason. Its actual
        claim — that no contact field appears in any GET form or redirect — is
        unchanged and still asserted.
      - `No_code_path_in_the_front_end_can_pin_a_resource` → renamed
        `Only_the_who_control_can_put_a_pin_into_the_front_ends_markup`. ⑩ design
        D7 asserted a total absence; this change builds the consumer, so the
        absence becomes conditional. The scan is narrowed rather than dropped —
        exactly one view may emit the field, both the control and the field are
        guarded on the model, and ⑩'s non-vacuity assertion (the delivery mapper
        must still be found) is kept.
- [x] 10.3 Attack each clause of designs D1, D3, D4 and D8 separately for a clause with no covering test — the ⑩ technique that found the no-common-length gap. Record what was found.

      **Found two uncovered clauses, both now tested.**

      *D1's decisive clause* — "a service-level flag naming the role by type key
      was rejected because two roles may share a type". Every test had the
      selectable role naming a type no other role used, so a by-type resolution
      would have passed all of them. Now covered by
      `Only_the_selectable_roles_pool_is_offered_where_two_roles_share_a_type`:
      two `therapist` roles, one selectable, and the choices are the selectable
      role's pool rather than the union — with a non-vacuity assertion that the
      excluded therapist really is a candidate of the service.

      *D8's whole point* — "placement is unchanged; a pin on an unflagged role is
      still honoured". Nothing tested it. It is precisely the clause a later
      change would "fix" into an access control, and the spec has a scenario for
      it. Now covered by `A_pin_is_honoured_for_a_role_that_is_not_visitor_selectable`,
      over both placement and the availability query.

      D3 and D4 were already covered: the subset property, the unchanged unpinned
      answer, the resource-free payload and the pinned-then-placed round trip for
      D3; the single-role fast path plus its mutation check for D4.

      **Two defects found by the browser, not by the suite** — both fixed, both
      now covered:
      - the error summary printed the at-most-one failure once per conflicting
        row, stuttering the same sentence twice. Deduplicated by message in
        `summaryLines`, with the per-row messages untouched.
      - the message read "Requirements 1 and 2 are **all** marked as choosable"
        for the commonest case. Now "both" for a pair, "all" for three or more,
        with a test for each.
- [x] 10.4 Sync-time outward grep for sibling specs this change falsifies. It has found something on five consecutive changes.

      **Repeated at sync over the synced specs; still clean.** The only new hits
      are this change's own: `default-frontend`'s "no control, hidden field, or
      other means by which a pinned resource could be submitted" (correctly scoped
      to a service with NO selectable role) and the "names no resource" sentences,
      which this change strengthened to "pinned or not" rather than weakened.

      *Preliminary pass run during apply.* Grepped
      `openspec/specs/` for "no control", "hidden field", "no code path",
      "deliberately absent", "names no resource", "choose which". Three hits, none
      falsified:
      - `delivery-api` "Response names no resource" — still true, and this change
        adds a scenario saying it stays true under a pin.
      - `delivery-api:310` "the assignment chooses which slot it fills" — the
        pin's own semantics, unchanged.
      - `resource-management:66` — a containment scenario about `IBookingStore`,
        unrelated.

      Note the sentence this change *does* falsify is inside a requirement it
      modifies rather than a sibling: ⑩'s design D7 absence, handled in 8.3.
- [x] 10.5 **Manual edit at sync, which no delta will make for you**: correct `openspec/specs/delivery-api/spec.md` lines 5 and 8 so the delivery API stops naming the default Razor front end as one of its consumers — it reads Core in-process, and `default-frontend` forbids otherwise. Likely "any *alternative* front-end". Discharges the obligation parked at ⑩'s sync.

      **Done at sync, after Chris reviewed the wording.** Both sentences
      corrected; `openspec validate --specs` passes 9/9, and the identical claim
      was already removed from shipped source in §11.4, so spec and code now
      agree. The auth stance's three SHALL/SHALL NOT sentences are untouched —
      only the trailing rationale clause changed.

      The sentences as they were:
      - line 5 (Purpose): "so any front-end (default Razor, a separate-repo
        DevExpress UI, a SPA, a mobile client) is a symmetric consumer."
      - line 8 (Anonymous access and auth stance): "This keeps every UI (the
        default front-end, a separate-repo DevExpress UI, a SPA, a mobile client)
        a symmetric consumer of the same contract."

      Both need the default Razor front end removed from the list — it consumes
      Core in-process and never this API.
- [x] 10.6 Diff the guarantees of every MODIFIED requirement one final time against `openspec/specs/`: list each SHALL and scenario in the current version and confirm it is carried forward, deliberately dropped with a reason in the proposal, or superseded by a stronger claim.

      **Re-run at sync, independently, by the syncing agent** — which repeated the
      diff over all eight rather than trusting the apply pass's result, then
      audited the git diff for deleted lines as a second check. Every deletion was
      a paragraph reflow carrying its content forward, or one of two deliberate
      in-place THEN rewordings. Nothing dropped.

      *Original pass, run during apply.* Both halves checked mechanically —
      scenario titles and body SHALL sentences:

      | requirement | scenarios (main → delta) | SHALLs dropped |
      |---|---|---|
      | services / Service definition | 11 → 15 | none |
      | services / Service validation with stable failure codes | 11 → 14 | none |
      | services / Service CRUD endpoints | 6 → 8 | none |
      | services / Service requirements are edited as a list of roles | 6 → 10 | none |
      | delivery-api / Service read model | 12 → 14 | none |
      | delivery-api / Service bookable-start read | 8 → 12 | none |
      | default-frontend / A visitor-facing refusal discloses no configuration detail | 2 → 4 | none |
      | default-frontend / Flow state is carried in the URL | 3 → 4 | none |

      Every requirement grew; none shrank, and no scenario title or SHALL sentence
      from the current spec is absent from its replacement. Nothing was
      deliberately dropped, so the proposal needs no removal statement.
- [x] 10.7 Hand to `qa-review` in a **fresh context or subagent** — never the context that wrote the code.

## 11. QA round 1 — REJECT, remediated

Two MAJORs, three MINORs, three NITs. Both MAJORs were on the Web edge and both
were real; the Core work was found well covered, and the D4 and D6 claims were
independently re-verified by the reviewer.

- [x] 11.1 **MAJOR — a flag-cleared stale choice reset silently.**
      `ResourceChoiceState.Resolve` returned `None` when no role was selectable,
      and `None` carries `WasReset: false`; the notice also lived inside the
      view's `@if (Model.OffersResourceChoice)` branch. So for the third of design
      D11's three causes — the editor turns the picker off while a bookmarked
      "book with Jane" link is in the wild — the message was *structurally
      unreachable*, and the visitor got a form that would book anyone with nothing
      saying their choice had been dropped. That is exactly the quiet substitution
      D11 exists to prevent.
      **Fix:** `Resolve` now reports `WasReset` for a request that named a
      resource even when there is no selectable role, and the view renders the
      notice in its own branch outside the control's guard.
      **Test:** `A_choice_whose_role_is_no_longer_selectable_is_reset_and_said_so`
      now asserts the "says so" half (it previously asserted only the reset — a
      test written to the implementation), plus
      `The_reset_notice_is_rendered_even_where_no_control_remains` over the
      markup, since no C# test renders Razor. Mutation-checked: restoring `None`
      turns it red.

- [x] 11.2 **MAJOR — the path that names a refused pin had no covering test.**
      QA proved it: forcing the controller's guard to always use the generic
      wording left all 762 tests green. The only assertion touching it was
      `Assert.Contains("Jane", BookingMessages.PinnedUnavailable("Jane"))` — a
      tautology over the helper that never exercised the controller.
      **Fix:** the decision moved out of the surface controller into
      `BookingMessages.ForFailures(failures, chosenResourceName)`, host-free for
      the same reason `BuildConfirmation` is. The controller keeps only the store
      read, and only when a pin was actually refused.
      **Test:** `A_refused_pin_is_named_on_the_page_the_visitor_sees` and
      `An_unreadable_name_still_says_what_happened`, the second checking the
      fallback *and* that no other code's message changes when a name is present.
      Mutation-checked: dropping the substitution turns both red.

      *One correction to the QA report, on a fact rather than a finding.* It
      states the refusal "is also not reachable in the TestSite as configured" and
      that §9.2's claim to have exercised a busy person is therefore unsupported.
      The first half is right and useful: via the documented `?flow=<id>` harness
      the subject is author-named, so the redirect is bare and the redraw never
      renders the flow. The apply pass reached it the other way — `?flow&ubBook=s:<id>`,
      the catalogue-token form — and did observe the rendered page: *"Frank (cap)
      is not available at the time you chose…"*, with the summary linking to
      `#ubookit-who` and the select carrying `aria-invalid="true"`. So the
      behaviour had been seen; what it lacked, and what mattered, was a test. The
      finding stands.

- [x] 11.3 **MINOR — both carriers of the choice across POST→redirect→GET were
      individually disableable with the suite green.** Now covered by
      `A_failed_submission_redraws_the_visitors_own_choice` (the TempData carrier,
      which is the only live one for an author-named single-service site) and
      `The_redirect_after_a_submission_carries_the_chosen_resource` (the URL
      carrier), the latter also asserting no empty parameter appears when nobody
      was chosen.

- [x] 11.4 **MINOR — the delivery API base controller repeated the falsehood task
      10.5 corrects in the spec.** Its doc comment listed the default front end as
      a symmetric consumer of the delivery API. Corrected in source, with the
      reason stated, so the shipped code and the spec will agree once 10.5 lands.

- [x] 11.5 **NIT — the reset notice carried `class="ubookit-field-error"`.** It is
      a notice rather than a validation failure and correctly sets no
      `aria-invalid`, so the class disagreed with the semantics. Now
      `ubookit-notice`.

- [x] 11.6 **NIT — the new messages said "The person you chose".** Nothing
      restricts a visitor-selectable role to a person-like type, so a site
      offering a choice of room was told about a person. The two failure messages
      are now type-neutral ("Your choice…", "let us pick for you"). The control's
      own wording — "Who would you like?", "Anyone who is available" — is left as
      the spec's own vocabulary: the requirement is titled "A visitor may choose
      **who** fulfils a service" and every scenario is about people, so changing
      that is a propose-time decision rather than an apply-time one.

- [ ] 11.7 **MINOR, accepted with reason — `StructuralReasonAsync` resolves the
      pools a second time.** Correct but wasteful: every empty availability
      response pays a second pool resolution, and a fully booked week is the
      common empty answer. Not fixed here because the cheap fixes are worse than
      the cost — threading the pools out means either widening
      `GetBookableStartsAsync`'s return type (a published contract, for a
      performance detail) or resolving them in the controller and passing them in,
      which would give the controller a second way to compute what Core already
      computes, and that is the drift design D6 exists to prevent. Flagged for
      whoever next touches the availability contract, where the change belongs.

- [ ] 11.8 **NIT, accepted — a pin with no times renders "No times are available
      on \<date\>".** Literally false about the *service*, and offers only one of
      the two ways forward. It is what the spec's own scenario asks for ("choosing
      them shows the no-times message", design D10), so changing it needs a spec
      change rather than an implementation one. Recorded for a follow-up.

- [x] 11.9 Re-verify: clean `--no-incremental` build at **0 warnings**; 767 unit
      (was 762 — five added), 58 integration, 69 client, all green.

## 12. QA round 2 — APPROVE WITH NITS, tidied

Both round-1 MAJORs re-verified fixed by the reviewer's own mutations and a live
pass, including the specific check that the reset notice now covers **all three**
of D11's causes rather than two. The round-1 factual correction was accepted and
independently reproduced. Both deferrals (11.7, 11.8) were judged legitimate and
stay open as recorded.

- [x] 12.1 **MINOR — the surface controller's own wiring had no net.** Both lines
      survived mutation with 767 green: `ChosenResourceNameAsync` forced to null
      (so a refused pin silently loses the visitor's own name from its wording),
      and `ChosenResourceId = form.PinnedResourceId` cut (so the redraw drops the
      choice on an author-named single-service site, where TempData is the only
      carrier). The *decisions* were covered after round 1; what was not covered
      was that the controller ever supplies them.
      **Fix:** `The_surface_controller_wires_both_halves_of_the_choice_into_a_failed_submission`,
      a source scan over the shipped controller — the technique this project
      already uses for the Post-Redirect-Get query string in
      `Contact_details_never_appear_in_a_URL`, and the strongest net available
      without a host harness. It carries a non-vacuity assertion so a rename
      cannot leave it passing. Mutation-checked: cutting either line turns it red.

- [x] 12.2 **NIT — `ResourceChoiceState.None` was left dead by the round-1 fix.**
      Deleted rather than kept, with a comment in its place saying why: it was the
      value the defective branch returned, and "no selectable role" is not the
      same state as "no choice was made". Leaving it would invite someone to reach
      for it and reintroduce the defect. `default` covers the genuinely empty case.

- [x] 12.3 Re-verify after tidying: clean `--no-incremental` build at **0
      warnings**; **768** unit, 58 integration, 69 client, all green.
