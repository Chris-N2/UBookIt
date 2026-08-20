## 1. The project and the rig

- [x] 1.1 Add `tests/UBookIt.Tests.Rendering/` (xunit, `Microsoft.NET.Test.Sdk`, AngleSharp), referencing `UBookIt.Web` and `UBookIt.Core`. Add it to the solution. Keep it **out of** `UBookIt.Tests` — that suite is 768 tests in under a second and the speed is worth protecting.
- [x] 1.2 Share `Support/RepoFiles.cs` from `UBookIt.Tests` by `<Compile Include=… Link=…>` rather than copying. It resolves the repo root from its own compile-time path, so a linked copy resolves identically — confirm that by asserting `RepoFiles.Root` from the new project.
- [x] 1.3 Build the rig: a `ServiceProvider` with MVC + the Razor view engine, `UBookIt.Web` added as an application part, rendering by **absolute view path** into a `StringWriter` over a `DefaultHttpContext` (design D1). One `RenderAsync(path, model)` entry point.
- [x] 1.4 Prove the rig renders the **compiled** view rather than a re-parse: assert that no `.cshtml` file is read from disk during a render, or equivalently that rendering succeeds with the source tree's `Views` directory unreadable/renamed. If that cannot be arranged cheaply, assert instead that the view type resolved comes from the `UBookIt.Web` assembly. **Do not skip this** — "we render the shipped artefact" is the claim the whole rig's value rests on.

      *Checked at propose time, and the answer is better than assumed:*
      `Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation` is **not in
      `UBookIt.Web`'s dependency closure at all** (verified against its
      `deps.json`). The TestSite does carry it, but only via
      `Umbraco.Cms.DevelopmentMode.Backoffice` — a development-mode package,
      consistent with Umbraco requiring precompiled views in production. So
      runtime compilation is absent rather than merely disabled.
      **Consequence for 1.1: the rendering project must reference `UBookIt.Web`
      and must NOT reference `UBookIt.TestSite`**, which would drag runtime
      compilation back into the closure and quietly make this assertion a lie.
- [x] 1.5 Smoke test: `ServiceConfirmation.cshtml` renders non-empty HTML containing the booker's name. One view, one assertion, so a broken rig fails obviously rather than as eleven confusing failures.

## 2. The view inventory

- [x] 2.1 Enumerate the eleven in-scope views **by scanning the Views directory and subtracting the deferred three**, not by hardcoding a list — a view added later must join the suite automatically or the suite decays. The deferred three are named explicitly with their reason.
- [x] 2.2 Assert the inventory is what it should be: eleven in scope, three deferred, fourteen total. A scan that silently found four views would otherwise pass every rule below.
- [x] 2.3 Assert the deferred three are deferred *for the stated reason* — that each transitively reaches `BeginUmbracoForm` — rather than by name alone. `BookingFlow/Default.cshtml` reads as Umbraco-free and is not; it is a one-line delegate into `Booking/Default.cshtml`, and a by-name list would not say why.

## 3. Model fixtures

- [x] 3.1 A fixture per in-scope view: a base model, plus the small set of base states that view's own shape needs (design D4) — errors/none, times/none, length fixed/chosen, choice offered/not, reset/not.
- [x] 3.2 Keep fixtures free of Core: these are view models, and building them from resolved pools would drag the whole booking graph into a rendering suite. Construct them directly.
- [x] 3.3 Where a state is awkward to construct, prefer changing the fixture over exempting the property. An exemption is a hole in the rule; an awkward fixture is only awkward.

## 4. Rule 1 — rendered markup resolves its own references

- [x] 4.1 One test over every view × every base state (design D5), asserting per document: every `label[for]` resolves; every id in every `aria-describedby` and `aria-labelledby` resolves; no id appears twice; every input/select has an accessible name.
- [x] 4.2 Every assertion message carries the **view path** and the offending id or element, since one test over eleven views is only debuggable if it says where it failed.
- [x] 4.3 Tag-helper residue asserted against the **raw string**, not the parsed DOM (design D6 risk): a `<partial>` element parses into a tidy node and would pass a DOM check. Assert no literal `<partial` and no `asp-` attribute survives.
- [x] 4.4 Mutation-check each clause by breaking one view deliberately — a dangling `aria-describedby`, a duplicated id, a `for` pointing at nothing, a `<partial>` tag — and confirm the rule goes red **and names that view**. Revert each. A rule nobody has seen fail is a rule nobody knows works.
- [x] 4.5 **It found one thing, and it was the rig rather than the views.**
      `_DateAndLength.cshtml` describes its length control with an id `_Times.cshtml`
      owns; rendered apart that reference dangles, rendered together it resolves,
      and forward references are legal ARIA. The view is correct and the rig was
      treating a partial as a document. Fixed by making rule 1 a **document** rule
      whose unit for the four partials is the four composed (design D7) — exempting
      the id would have switched off a real check to accommodate a rig error.
      No defect in the shipped markup. Original wording: fix whatever the rule finds in the shipped views. If it finds nothing, say so explicitly in this file — "clean on first run" is a result worth recording, and is also the result most likely to mean the rule is vacuous, so pair it with 4.4.

## 5. Rule 2 — a view renders every state its model can express

- [x] 5.1 Extract each view's referenced model properties from its source, handling **every reference form Razor admits** — `Model.X`, `Model?.X`, and `Model` passed whole to a partial. Design D3.
- [x] 5.2 **Non-vacuity guard A**: assert every in-scope view yields at least one referenced property. A pure delegate that genuinely references none — `BookingFlow/Unavailable.cshtml` and its two siblings — is an explicit named exemption, not a silent pass.
- [x] 5.3 **Non-vacuity guard B**: assert the extracted set for `_DateAndLength.cshtml` matches its expected fourteen properties by name. If the extraction silently stops finding things, this fails; without it, a broken regex makes the whole rule pass trivially.
- [x] 5.4 For each referenced property, vary it (flip a bool, change a string, empty or fill a collection, move a number) and assert the rendered output differs in at least one base state. Failure message names the view **and the property**.
- [x] 5.5 Exemptions live in one place with a reason each, and the test asserts the exemption list is non-empty only where expected — so an exemption is a decision on the record rather than an absence.
- [x] 5.6 **The rule as designed did NOT catch it, and was rewritten rather than
      the claim.** Reintroducing the defect left rule 2 green: the reset flag was
      live wherever a choice control existed and dead only where none did, and an
      "exists a state" rule finds the live states and passes. A settable flag is now
      held to the stronger form — live wherever the model *sets* it — restricted
      twice, each restriction earned by a false positive (design D8). With that, the
      reintroduced defect fails by name. This is the single most valuable thing the
      change did, and it only happened because the task demanded the mutation.

      Original: mutation-check the rule where it matters most: reintroduce ⑩-1's defect by moving `_DateAndLength.cshtml`'s reset notice back inside the `OffersResourceChoice` branch, and confirm the rule goes red naming `ResourceChoiceWasReset`. **This is the test of the test** — the whole design rests on catching that class generically. Revert.
- [x] 5.6a **It earned its place immediately — and it failed.** The catalogue
      mutation (`@if (true || Model.HasEntries)`) was NOT caught: `HasEntries` and
      `Entries` both stay live because the two states still render differently,
      while the "nothing available to book" branch becomes unreachable. So a
      property can be live while a branch it selects is dead, and rule 2 cannot see
      that. Answered with a **third rule** — every literal `id`/`class` a view can
      emit must appear in some rendered state (design D9) — which catches it by
      name. The control mutation did exactly what it was added to do: it found that
      5.6 passing was not proof.

      Original: second mutation, in a view with no history of this defect — the length explanation in `_Times.cshtml`, or the catalogue's empty state. Make its branch unreachable and confirm the rule catches that too, naming the right view and property.

      Added at apply time, and the reason is worth recording: this change was
      applied by the context that **wrote** the ⑩-1 defect 5.6 reintroduces. That
      context knows exactly where that bug was and how it looked, which is a pull
      toward a rule that recognises *that instance* rather than the class — and
      5.6 alone would go green and feel like proof. A defect in a view nobody has
      broken before is the control. If 5.6 passes and 5.6a fails, the design is
      wrong, not the code.
- [x] 5.7 **It found two unreached branches, both fixture gaps rather than dead
      markup, and both real pages**: the refused-pin redraw (a choice control *with*
      an error against it) and the conflict redraw (times *with* an error). Fixed by
      adding those states rather than exempting the branches, per 3.3 — a branch
      nothing renders is either dead or untested, and exempting would have recorded
      the second as the first. No defect in the shipped markup.

## 6. Verification

- [x] 6.1 Full clean `--no-incremental` build. Warning baseline is **zero**; read against zero.
- [x] 6.2 All four suites green: unit, integration, client, rendering. Record the rendering suite's runtime — if it is slow enough to be annoying, say so here rather than letting the next person discover it.
- [x] 6.3 Confirm `UBookIt.Tests` is unchanged in `git diff` apart from any linked-file plumbing, and that its runtime has not moved.
- [x] 6.4 Attack each clause of designs D2, D3 and D4 for a clause with no covering test — the technique that found the no-common-length gap in ⑩ and two uncovered clauses in ⑩-1. Record what was found.
- [x] 6.5 Sync-time outward grep for sibling specs this change falsifies. It has found something on six consecutive changes. In particular check whether any spec claims the accessibility bar is unverified or verified only by review.
- [ ] 6.6 Hand to `qa-review` in a **fresh context or subagent** — never the context that wrote the code.

## 7. Follow-up, to be written up rather than remembered

- [ ] 7.1 Record the deferred three as an obligation with its spike named: does `BeginUmbracoForm` need a booted Umbraco context, or only `AddDataProtection()` and `AddAntiforgery()` over a `DefaultHttpContext`? The answer decides whether that suite stays fast, and it should be settled **before** the follow-up's design is written, not during it.
- [ ] 7.2 Record that flow-view/partial **composition** remains untested until that follow-up, and that the existing source scan asserting each flow view names each partial path is load-bearing until then.

## 8. What apply changed about the design

Recorded here as well as in `design.md`, because the corrections came from doing
the work and a reader of the tasks should not have to infer them.

- **D7** — rule 1 is a *document* rule, and the four shared partials form one
  document together. Found because `_DateAndLength` describes its length control
  with an id `_Times` owns.
- **D8** — "changes the output in some state" does not catch ⑩-1's defect. A
  settable flag must be live wherever the model sets it. Found by task 5.6.
- **D9** — a live property does not mean a live branch, so branches are checked
  separately. Found by task 5.6a, the control mutation.

Two of the three were found by mutation rather than by reasoning, and the third by
the rule failing on a correct view. **The suite would have shipped green and
worthless without them**: as first designed it caught neither of the two defects it
was written for.

## 9. Numbers

- 212 rendering tests, ~2s. `UBookIt.Tests` unchanged at 768 in under a second,
  which was the point of the separate project.
- Rendering suite runtime is not annoying and does not need splitting further.
- **No product code changed.** Both rules found only fixture gaps and one rig
  error; the shipped markup was correct throughout.

## 10. QA round 1 — REJECT, remediated

Three MAJORs, each demonstrated by a mutation that left all 212 tests green. All
three were real. Two were refinements of rules that already caught what they were
built for; the third was a clause simply missing.

- [x] 10.1 **MAJOR — the strong rule's "only where set" restriction lost real
      coverage.** `@if (Model.LengthIsFixed && !Model.LengthIsTheProblem)` passed
      everything: a fixed-length service on a date with no times would render a
      length *dropdown*, offering a choice the service does not permit — the same
      substitution class as ⑩-1.

      The restriction was earned by a genuine false positive, but it was an
      over-broad fix: it discarded every false-state to accommodate one legitimately
      silent state, and it contradicted this change's own spec ("exempt only by an
      explicit, reasoned entry — never by being absent from a list"). Now the flag
      is checked in **every** state that can express it, with the two legitimately
      silent states as explicit keyed entries carrying their reasons, and
      `Every_suppression_is_still_earning_its_place` failing if one stops applying.
      QA's mutation now dies.

      Restriction (a), *only where settable*, QA checked and found genuinely
      necessary. It stays.

- [x] 10.2 **MAJOR — rule 1 never checked in-page fragment links.** Pointing every
      error-summary link at nothing passed all 212 tests. `a[href^="#"]` is the
      internal reference the views themselves worry about — `_DateAndLength` carries
      the length control's id on a plain `div` *solely* so that link has a target —
      and it was the one clause missing.

      **Adding it found a real defect**: an error against the choice control, on a
      page where the picker had been turned off since the visitor's page was drawn,
      linked to an id nothing rendered. Fixed in the view by the idiom already
      established for the settled length — the wrapper carries the control's id.

- [x] 10.3 **MAJOR — rule 3 is blind where two branches emit the same literal**,
      and that was hiding a live gap: deleting the settled-length error span left
      everything green, because the sibling branch emits an identical one.

      The blind spot cannot be removed without parsing Razor's branch structure. It
      is now **bounded**: every literal a view emits more than once is enumerated in
      `The_literals_that_cannot_distinguish_a_branch_are_enumerated` with the reason
      it is safe, so a new shared literal fails and has to be justified. The gap it
      was hiding is closed by a fixture reaching the settled-length rejection.

- [x] 10.4 **MINOR — the composed document was not what the flow views do.** Both
      guard `_YourDetails` with `@if (Model.HasTimes)`; the composition included it
      unconditionally, which *masked*. Now guarded, and the state that exposes it
      (errors with no times) is exercised — **which found the second real defect**:
      the error summary linking to booker fields the page had not rendered. Fixed by
      linking only to controls that are on the page.

- [x] 10.5 **MINOR — the spec prescribed method rather than guarantee.** Four
      clauses telling an implementer to derive from source, to check separately, and
      how to remedy a gap. `default-frontend` is implemented by any alternative
      front end, and none of them owe this repository a regex. Trimmed to the
      guarantees; the technique lives in design and code, where it belongs.

- [x] 10.6 **MINOR — rule 1 had no codified non-vacuity guard** while rules 2 and 3
      each had one. Closed rather than deferred, since it was two lines:
      `Every_in_scope_view_appears_in_some_document` asserts the document set is
      non-empty and covers every in-scope view, so rule 1 can no longer pass over
      nothing.

- [x] 10.7 **NITs** — rule 1 now names the composed document's parts, so a failure
      says which file to open; the "added to CI" claim is corrected (**this
      repository has no CI definition at all**); the standalone/composed
      contradiction between D2, D7 and the proposal is reconciled;
      `Rendering_reads_no_cshtml_from_disk` is renamed to what it asserts and now
      also asserts the `NullFileProvider` structurally; the unreachable
      `states.Count == 0` escape in rule 3 is replaced by an assertion.

- [x] 10.8 **One existing test needed updating**, and the reason is worth recording:
      `ServiceFrontendTests.The_reset_notice_is_rendered_even_where_no_control_remains`
      asserted the exact `@if` source line, which the product fix legitimately
      changed. It now matches the condition rather than the spelling, with a note
      that the behavioural guarantee has moved to the rendering suite. This is the
      brittleness of source-scanning tests, demonstrated on the very change that
      exists to replace them.

## 11. What the two product defects were

Both found by rules added in response to QA, and both narrow but real:

- **An error against the choice control, with no choice control on the page.**
  Reachable when the role's visitor-selectable flag is cleared between a visitor
  loading the form and submitting it: `resource-not-eligible` comes back against a
  control that is no longer rendered, and the error summary linked to nothing.
  Fixed with the idiom the settled length already used — a wrapper carrying the
  control's id.
- **An error against a booker field, on a page with no times.** Reachable when a
  submission fails validation and the last slot goes in the meantime: the redraw
  renders no booker fields, and the summary still linked to them. Fixed by linking
  only to controls that are on the page.

Neither is dramatic. Both are exactly the class this suite was built for — a
message pointing somewhere that does not exist — and neither was findable by any
test that existed before it.
