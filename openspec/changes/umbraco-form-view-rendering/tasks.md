## 1. Baseline

- [ ] 1.1 Confirm a clean starting point: `dotnet build --no-incremental` with
      **zero** warnings (the baseline is zero, not NU1903-only), then
      `dotnet test --no-build` across the solution. Record the rendering suite's
      test count and duration — the change is judged against both.
- [ ] 1.2 Re-run the rig's own claim tests before touching anything: the content
      root is a `NullFileProvider`, and `RuntimeCompilation` is absent from the
      dependency closure. These are the guarantees this change is most able to
      break.

## 2. Host the three views

- [ ] 2.1 In `ViewRenderer`, register `SurfaceControllerTypeCollection` over the
      package's real surface controller types (`BookingSurfaceController`,
      `ServiceBookingSurfaceController`), not a fabricated list.
- [ ] 2.2 Add a stub `IUmbracoContextAccessor` returning a stub `IUmbracoContext`
      whose `OriginalRequestUrl` is a fixed URL. Per design D1, leave the other
      eight members `null!` or throwing — do **not** give them benign values — and
      carry D1's reasoning as a comment at the stub, since the next reader's
      instinct will be to "fix" it.
- [ ] 2.3 Do **not** add `AddDataProtection()` or `AddAntiforgery()` (design D2).
- [ ] 2.4 Verify all fourteen views render. `BookingFormModel` states must cover
      `Booking/Default.cshtml` as the `ServiceFormModel` states cover
      `Service.cshtml`.
- [ ] 2.5 Resolve the open question in design.md: why `BookingFlow/Default.cshtml`
      rendered 3460 bytes to `Booking/Default.cshtml`'s 3458 for the same model.
      Explain it or fix it; do not assume it is benign.

## 3. Prove the host is minimal, rather than asserting it

- [ ] 3.1 Mutation-check each registration by **deletion**: remove
      `SurfaceControllerTypeCollection`, confirm the suite goes red; restore.
      Repeat for the context accessor. A registration whose removal is silent is
      not required and must be deleted, not kept for safety.
- [ ] 3.2 Confirm the reverse for the two registrations deliberately absent: adding
      `AddDataProtection()`/`AddAntiforgery()` changes no rendered output. If it
      does, D2 is wrong and must be revised before proceeding.
- [ ] 3.3 Re-run 1.2. If `RuntimeCompilation` has entered the closure, **stop** —
      the rig's central claim is broken and the approach needs rethinking.

## 4. Close the deferred set

- [ ] 4.1 Delete `ViewInventory.Deferred` and the `InScope` subtraction that reads
      it. `InScope` becomes the shipped set.
- [ ] 4.2 Replace the test that asserts *why* three views are deferred (it now has
      nothing to assert) with the completeness check: the exercised set equals the
      shipped set.
- [ ] 4.3 Implement the vacuity guard the spec requires — the completeness check
      fails when the exercised set is empty or smaller than the shipped set. Prove
      it by mutation: make the exercised set empty and confirm red.
- [ ] 4.4 Mutation-check completeness properly: add a throwaway `.cshtml` to the
      package and confirm the suite goes red naming it. This is the scenario "a
      shipped view outside the suite fails", and it is the one most likely to be
      written so it cannot fail. Delete the throwaway afterwards and confirm the
      tree is clean.

## 5. Retire the hand-built composition

- [ ] 5.1 Rebuild `ViewFixtures.BuildDocuments` so document-level cases are the
      rendered output of `BookingFlow/Service.cshtml` and
      `Booking/Default.cshtml`.
- [ ] 5.2 **Delete** the hand-built concatenation. Do not keep it alongside
      (design D3) — two descriptions of one page is the liability being removed.
- [ ] 5.3 Keep the shared partials' standalone per-view cases; rules 2 and 3 are
      per-view and unaffected.
- [ ] 5.4 Prove the composition now tracks the view: change a flow view to render a
      partial twice, or to drop its `HasTimes` guard, and confirm the documents the
      rules see change **with no fixture edit**. This is the requirement's central
      scenario. Revert with `git checkout --` only after `git status` confirms the
      file carries nothing but the mutation.
- [ ] 5.5 Confirm the known blind spot is still exactly that and no larger: a
      doubled `_ErrorSummary` remains invisible (it carries no ids). Record it in
      the change's Risks as carried-forward, not as newly discovered.

## 6. Handle what the honest document finds

- [ ] 6.1 Run the full suite against the real composition. Expect failures; they
      are the point.
- [ ] 6.2 For each failure, decide explicitly: a real defect in shipped markup, or
      a fixture that was wrong. Write the determination down before fixing.
- [ ] 6.3 Report each real markup defect as a finding **before** fixing it, with the
      state that produces it. Do not fold defect fixes silently into this diff.
- [ ] 6.4 For each defect fixed, mutation-check that the rule detects its
      reintroduction — the fix is not evidence that the rule can see it.

## 7. Obligations and honesty

- [ ] 7.1 Discharge deferred obligation (a) — the three Umbraco-dependent views —
      and (b) — flow-view/partial composition — in
      `ubookit-deferred-obligations`. Strike them only once this change archives.
- [ ] 7.2 Carry forward, explicitly **not** discharged: (c) `role="alert"`
      unguarded; (d) the ⑤ human keyboard-only + screen-reader pass; (e) the two
      never-fired `HasAccessibleName` branches. None is touched here, and none may
      acquire a tick because something adjacent was verified.
- [ ] 7.3 Record design D4's consequence where a reader will meet it: the form
      `action` and anti-forgery token are now rendered and asserted by nothing.
- [ ] 7.4 Record that the two registrations are expected to be temporary, removed
      by editor-facing packaging.

## 8. Verify

- [ ] 8.1 `dotnet build --no-incremental` — zero warnings. Note: `dotnet test` does
      **not** accept `--no-incremental` (MSBuild-only switch, a parse error); build
      first, then `dotnet test --no-build`.
- [ ] 8.2 Full solution test run green. Report the rendering suite's new count and
      duration against the 1.1 baseline; a large duration jump means something was
      booted that should not have been.
- [ ] 8.3 `openspec validate --strict` against the pinned CLI (1.6.0 — 1.9.0
      reports scenario-title mismatches that 1.6.0 does not).
- [ ] 8.4 Sync-time **outward** grep: find sibling specs this change falsifies. It
      has found something on four consecutive changes. Note `delivery-api`'s Purpose
      correction is a separate standing obligation and is **not** this change's to
      fix.
- [ ] 8.5 Confirm `ref/` is ignored and untracked, and that no Umbraco source was
      copied into the build.
- [ ] 8.6 QA review in a **fresh context or subagent** — never this one.
