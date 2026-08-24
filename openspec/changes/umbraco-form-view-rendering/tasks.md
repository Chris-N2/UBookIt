## 1. Baseline

- [x] 1.1 Confirm a clean starting point: `dotnet build --no-incremental` with
      **zero** warnings, then `dotnet test --no-build` across the solution. Record
      the rendering suite's test count and duration.
      **Found a defect on `main` doing this** — see 1.3. Baseline after the fix:
      zero warnings; 768 + 415 + 58 = 1241 tests; rendering suite 415 tests, ~5s.
- [x] 1.2 Re-run the rig's own claim tests: `NullFileProvider` content root, and
      `RuntimeCompilation` absent from the closure. 5/5 green.
- [x] 1.3 **NEW, unplanned — the rendering suite was not running at all.**
      `0e79a03` (the VS2026 `.slnx` conversion) added
      `<Build Solution="Debug|*" Project="false" />` to `UBookIt.Tests.Rendering`,
      so since 2026-08-21 `dotnet build` skipped it and `dotnet test` never saw its
      415 tests. Fixed in its own commit; solution run 826 → 1241 tests.

## 2. Host the three views

- [x] 2.1 `SurfaceControllerTypeCollection` registered over the package's real
      surface controllers — **discovered by scanning the assembly** rather than
      listed, so a third surface controller cannot silently sit outside the rig.
- [x] 2.2 Stub `IUmbracoContextAccessor` / `IUmbracoContext`. Only
      `OriginalRequestUrl` answers; every other member **throws a
      `NotSupportedException` naming itself** and saying what to do instead. D1's
      reasoning carried as a comment at the stub.
- [x] 2.3 `AddDataProtection()` / `AddAntiforgery()` deliberately not added.
- [x] 2.4 All fourteen views render. `BookingFormModel` coverage went from 5 states
      to 11, mirroring the service states wherever the model can express them (it
      cannot express a choice control or a fixed length — recorded at the fixture).
- [x] 2.5 The 3460-vs-3458 difference explained: one trailing `\r\n`, identical
      trimmed. Pinned by a new structural test rather than left as a note.

## 3. Prove the host is minimal, rather than asserting it

- [x] 3.1 Deletion-mutated both registrations: each takes 386 of 581 tests red.
- [x] 3.2 Adding the two absent registrations changes no output — D2 holds.
- [x] 3.3 Re-ran 1.2 after the change: `RuntimeCompilation` still absent, content
      root still `NullFileProvider`. 5/5 green.

## 4. Close the deferred set

- [x] 4.1 `ViewInventory.Deferred` deleted, along with the `InScope` subtraction.
      `InScope` **renamed to `All`** across four test files rather than aliased —
      the spec's whole point is that "in scope" named a set nobody defined.
- [x] 4.2 The "why these three are deferred" test replaced by
      `Every_shipped_view_is_exercised`.
- [x] 4.3 Vacuity guard implemented and mutation-proved: emptying the exercised set
      fails `The_completeness_check_cannot_pass_by_checking_nothing`.
- [x] 4.4 Added a throwaway `.cshtml`: 7 tests fail naming it, with the message
      *"is shipped and no fixture renders it, so every rule passes over it while
      reporting green"*. Throwaway deleted; tree verified clean.

## 5. Retire the hand-built composition

- [x] 5.1 Document cases are now the rendered output of the flow views.
- [x] 5.2 Hand-built concatenation deleted. Went further than planned:
      **`DocumentCase` now holds exactly one `ViewCase`**, so an assembled document
      is unrepresentable rather than merely discouraged.
- [x] 5.3 Shared partials keep their standalone per-view cases.
- [x] 5.4 Made a flow view render `_YourDetails` twice: the duplicate-id rule fails
      across many states **with no fixture edited**. Reverted after `git status`
      confirmed one inserted line and nothing else.
- [x] 5.5 Made a flow view render `_ErrorSummary` twice: **581 tests still pass.**
      The blind spot is confirmed to be exactly what was recorded and no larger —
      verified rather than assumed.

## 6. Handle what the honest document finds

- [x] 6.1 Ran the full suite against the real composition.
- [x] 6.2 Two failures, both on `BookingFlow/Default.cshtml`, both **non-vacuity
      guards rather than markup rules**. Determination written before any fix: not
      a defect — a pure delegate names no model member and emits no literal, which
      is the shape `ModelReferences.DelegatingViews` already exempts for two
      identical siblings. Registered with its reason.
- [x] 6.3 No markup defect was found, so nothing to report as one. Recorded
      explicitly in design.md, because the proposal predicted otherwise.
- [x] 6.4 Closed a hole found while there: nothing checked that an **exempted** view
      really is a delegate, so the registry could have silenced a real view.
      `An_exempted_view_really_is_a_delegate` now asserts it hands its whole model
      to exactly one shipped view. The dispatcher-structure test is
      mutation-checked (adding a `<div>` fails it).

## 7. Obligations and honesty

- [x] 7.1 (a) and (b) are discharged by this change — to be struck in
      `ubookit-deferred-obligations` **at archive**, not now.
- [x] 7.2 Carried forward explicitly **not** discharged: (c) `role="alert"`
      unguarded; (d) the ⑤ human keyboard-only + screen-reader pass; (e) the two
      never-fired `HasAccessibleName` branches.
- [x] 7.3 D4's consequence recorded: the form `action` and anti-forgery token are
      rendered and asserted by nothing.
- [x] 7.4 Recorded that the two registrations are expected to be temporary.

## 8. Verify

- [x] 8.1 `dotnet build --no-incremental` — **zero warnings**.
- [x] 8.2 Full solution green: 768 + **582** + 58 = **1408** tests. Rendering suite
      415 → 582, duration ~5s → ~16s. The rise is renders, not a boot: the three
      flow views are the largest in the package and the resource states more than
      doubled. Nothing in the closure changed (3.3).
- [x] 8.3 `openspec validate umbraco-form-view-rendering --strict` — valid.
- [x] 8.4 Outward grep run. **Nothing falsified.** The only live hits are
      `default-frontend:304,367` ("every view **in scope**"), which are precisely
      what this change's first new requirement pins by defining that set; they are
      left textually as they are rather than replaced wholesale for two words. A
      hit in `bookings:181` is the word "deferred" in an unrelated discharged
      obligation. Re-run at sync.
- [x] 8.5 `ref/` ignored (`.gitignore:494`) and untracked; no file under `ref/`
      reaches the build. `src/` is untouched by this change.
- [x] 8.6 QA review in a fresh subagent. **Round 1: REJECT**, one MAJOR.

## 9. QA round 1 — findings and dispositions

- [x] 9.1 **MAJOR — rule 2 was vacuous on the three newly-rendered views.**
      `IsLiveAsync` compares two renders; `BeginUmbracoForm` emits a per-render GUID
      form id and two fresh tokens, so two renders of one model were never equal and
      every member was reported live. Fixed by stripping the three varying values
      **at the comparison** (`RenderNondeterminism`), never in the renderer, so every
      other rule still reads real output. Both routes go through it —
      `CoVariesAsync` had the same defect and QA's report named only the first.
      Proven by a matched pair: dead `ServiceName` branch **fails** with the strip,
      **passes** without it.
- [x] 9.2 Two guards added against silent reversion: `Two_renders_of_one_model_compare_equal`
      over every shipped view, and `The_stripping_fires_where_there_is_something_to_strip`
      (exactly 3 varying values on a flow view, 0 on a deterministic partial).
- [x] 9.3 **design.md D5 rewritten**, with the original wrong claim left visible and
      the reasoning error named: the design examined what the new collaborator
      *needed* and never what it *changed* about the output existing rules read.
- [x] 9.4 **MINOR (a)** — mutation table re-derived on the final 597-test shape.
      Corrected: 391/597, not 386/581.
- [x] 9.5 **MINOR (b)** — `An_exempted_delegate_adds_no_markup_to_the_view_it_delegates_to`
      now runs over **every** entry in `DelegatingViews`, not just the dispatcher.
      Delegation target extraction moved to `ModelReferences.DelegationTargetOf` so
      both halves of the exemption's reason use one rule.
- [x] 9.6 **MINOR (c)** — recorded as design D6, with the deferral/structural-exemption
      distinction the spec sentence blurs. Deliberately not repaired by inventing an
      exemption facility with no user.
- [x] 9.7 **MINOR (d)** — accepted, and it is the sharper finding: the *silent* gap is
      `[ValidateAntiForgeryToken]` on the two surface controllers, asserted by
      nothing. Recorded as design D4a and carried to deferred obligations.
- [x] 9.8 **NITs** — stale "deferred" / "in-scope" comments corrected in
      `ViewInventory`, `ViewFixtures`, `ModelReferences`, `BranchReachabilityTests`.
      Remaining uses are deliberate historical references.
- [x] 9.9 **CI answered by Chris (2026-08-24): there is none, and the repo moves from
      Azure DevOps to GitHub later.** So `90eba80`'s commit message overclaims when
      it says the exclusion hid the tests "and in CI" — corrected here rather than by
      rewriting a mid-branch commit for a prose error. The substantive consequence is
      worse than first framed: with no pipeline, **nothing runs any suite
      unattended**, so 415 tests stopping was not a near-miss with a net behind it.
      No `azure-pipelines.yml` written — it would be built for a home the project is
      leaving.
- [x] 9.10 Re-review by the same QA subagent: **APPROVE WITH NITS.** It verified the
      MAJOR fix in both directions and ran two mutations I had not: patterns
      matching nothing, and — the important one — patterns matching **too much**,
      which is the silent direction. Over-stripping destroys rule 2's signal while
      everything still passes; `The_stripping_fires_where_there_is_something_to_strip`
      catches it via the `partial == 0` arm.

## 10. QA round 2 — nits accepted

- [x] 10.1 **MINOR 1 — `Two_renders_of_one_model_compare_equal` was itself vacuous.**
      It used `ViewFixtures.For(view)[0]`, and `_ErrorSummary`'s first state has no
      errors, so it compared `""` against `""`. A guard against vacuity, vacuous.
      Now loops **every** state and requires at least one to render something.
      Mutation-checked: stripping to empty fails it, naming `_ErrorSummary` and
      `_YourDetails` among others.
- [x] 10.2 **MINOR 2 — rule 2's partial-masking bound is now enumerated, not just
      recorded.** QA said recording in D5 would be enough and bounding would be
      better; bounding is what rule 3's shared-literal blind spot already gets, so
      consistency argued for it. `The_members_a_partial_keeps_alive_are_enumerated`
      lists all nine, with a non-vacuity arm. Mutation-checked: adding a reference
      to `Name` in `Service.cshtml` fails it, naming the new member.
      Note the enumeration is **more pessimistic than QA's summary** — QA listed
      `HasTimes` as genuinely checked, which is true of rule 1 catching it, not of
      rule 2. The list reflects rule 2 alone, and D5 says so.
- [x] 10.3 D5 extended with QA's refinement of the lesson: a precondition never
      asserted is a rule never verified to run.

## 11. The regression class that started this

- [x] 11.1 **`SolutionIntegrityTests` added to `UBookIt.Tests`** — deliberately not
      to the rendering suite, because a project excluded from the build cannot report
      its own absence. Asserts no project carries a build exclusion, and that every
      test project on disk is in the solution. Both carry non-vacuity arms.
- [x] 11.2 Mutation-checked by reintroducing the **exact** original regression
      (`<Build Solution="Debug|*" Project="false" />` on the rendering project): the
      guard fails, naming the project — and the run itself shows the rendering suite
      absent, which is the silence being guarded against.
- [x] 11.3 Scope note: this is beyond the change as proposed. It is here because the
      `.slnx` fix is on this branch, and a fix whose regression nothing can detect is
      the pattern this whole change exists to argue against. With no CI, this test is
      the substitute for a pipeline rather than a supplement to it.

## 12. Ready

- [x] 12.1 Zero warnings; 770 + 598 + 58 = **1426** passing; `openspec validate
      --strict` clean; `src/` untouched; tree clean.
- [ ] 12.2 Sync + archive + merge — **held for Chris**, who asks to be present.
