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
- [x] 6.6 Hand to `qa-review` in a **fresh context or subagent** — never the context that wrote the code. **Six rounds** (§10, §12, §14, §15, §16, §17): REJECT ×5, then APPROVE.

## 7. Follow-up, to be written up rather than remembered

- [x] 7.1 Record the deferred three as an obligation with its spike named: does `BeginUmbracoForm` need a booted Umbraco context, or only `AddDataProtection()` and `AddAntiforgery()` over a `DefaultHttpContext`? The answer decides whether that suite stays fast, and it should be settled **before** the follow-up's design is written, not during it.

      Written to `ubookit-deferred-obligations`. QA agrees the spike belongs before
      the design rather than inside it.

- [x] 7.2 Record that flow-view/partial **composition** remains untested until that follow-up, and that the existing source scan asserting each flow view names each partial path is load-bearing until then.

      Recorded, and QA's judgement recorded with it: **this is the one deferred item
      that still carries real risk.** The composed-document fixture is hand-built —
      `_ErrorSummary`, `_DateAndLength`, `_Times`, `_YourDetails` in flow order with
      `_YourDetails` guarded by `HasTimes` — and nothing checks that it matches what
      `Service.cshtml` and `Default.cshtml` actually render. It has already drifted
      once: composing `_YourDetails` unconditionally masked a real defect (§10.4).
      A second drift would be silent in the same way.

      One consequence worth naming: an `_ErrorSummary` included **twice** by a flow
      view would be invisible to every rule here, because the summary carries no
      ids. QA probed it this round and judged it belongs to the composition gap
      rather than to this change.

- [x] 7.3 Record the `role="alert"` blind spot. In `design.md` under Risks, with the
      reasoning rather than as a bare note: deleting it passes the whole suite, and
      it is now the largest unguarded property of this surface — but whether the
      announcement *works* is not something a DOM assertion can judge, and asserting
      that the attribute is merely present would be a markup rule wearing a
      behaviour rule's clothes. Written down so a later reader does not assume
      nobody looked.

- [x] 7.4 Record that the human keyboard-only and screen-reader pass owed since ⑤
      **stays its own obligation** and is not discharged by this change. §13's
      keyboard pass covered the two product fixes in one scenario; it did not cover
      the flows. QA asked explicitly that this not be absorbed into this change's
      record, and it is right to: an obligation that quietly acquires a tick because
      something adjacent was verified is worse than one still open.

- [x] 7.5 Record the two branches of `HasAccessibleName` that **no fixture
      exercises**: the wrapping-`label` case and the `aria-labelledby` case. No view
      uses either — all seven labels are `for=`-based — so both are correct,
      consistent with their siblings, and have never fired. Not worth manufacturing
      a fixture for; worth knowing, so that whoever first writes an
      `aria-labelledby` into a view knows that path is untried.

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

*Written before QA. Superseded on both counts — kept as written, with the
corrections beneath it, because the second line is exactly the claim six rounds of
review overturned.*

- 212 rendering tests, ~2s. `UBookIt.Tests` unchanged at 768 in under a second,
  which was the point of the separate project.
- Rendering suite runtime is not annoying and does not need splitting further.
- **No product code changed.** Both rules found only fixture gaps and one rig
  error; the shipped markup was correct throughout.

**As merged: 415 rendering tests, ~5s.** `UBookIt.Tests` still 768 in under a
second, so the separation held its purpose. The runtime claim survives.

**The "no product code changed" line was wrong**, and it was wrong at the moment it
was written rather than overtaken by scope creep: two real defects were sitting in
the shipped markup, and the rules as first designed could not see either (§11). Two
product files changed in the end, both `.cshtml`, both narrow.

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

## 12. QA round 2 — REJECT, remediated

Narrower than round 1, and notably **both MAJORs were about the product code the
round-1 remediation introduced**, not about the rig. QA re-ran every round-1
mutation and confirmed each now dies; it also confirmed the two suppressions are
load-bearing (deleting either makes the main rule fail) and that enumeration was the
right shape for them rather than a rule.

- [x] 12.1 **MAJOR — the new `IsOnThePage` guard had an untested path.** Adding
      `|| fieldId == BookingFieldIds.Times` passed all 260 tests: every Times error
      in the fixtures came with times still present. That is the *most likely real
      instance* of the fault the guard was written for, and the one
      `default-frontend` names explicitly — a conflict redraw where nothing is left
      that day. Closed with the state "service: time taken and none left"; QA's
      mutation now dies.

- [x] 12.2 **MAJOR — the product change narrowed a sibling requirement, and task
      6.5 had not been re-run since the change acquired product code.**
      `default-frontend`'s "Accessible failure handling with input preservation"
      says the summary "lists each problem in text **and is associated with the
      offending fields**", unqualified; the fix deliberately renders some problems
      with no association. That is a narrowing of a shipped guarantee and lived only
      in implementation notes.

      Now a **MODIFIED requirement** in the delta — the first in this change —
      restating every SHALL and both existing scenarios verbatim and adding two.
      Guarantees diffed mechanically: 2 scenarios → 4, no SHALL dropped.

      Task 6.5 re-run against the product changes. One further edge found and
      addressed in the same requirement: the standing clause that hints and error
      text are associated with their controls governs text belonging to a control
      that *is* rendered, and the new branch renders error text where no control
      exists — a case it does not reach rather than one it forbids. Stated rather
      than left to inference. Everything else clean.

- [x] 12.3 **MINOR — the in-page-link clause was in the code and not the spec.**
      The check that found *both* real product defects had no requirement clause and
      no scenario. Both added, with the reason it is not a lesser member of that
      list: it is the reference the error summary is built on.

- [x] 12.4 **MINOR — rule 3's enumeration stated its safety reason in prose.**
      "Covered instead by fixtures reaching both sides" is now asserted:
      `Each_shared_literal_is_rendered_by_more_than_one_state` fails if a shared
      literal is rendered from only one state, since that means only one of its
      branches is exercised. It does not close the blind spot — QA is right that a
      branch can be disabled while a sibling keeps the literal alive — but it makes
      the stated mitigation fail when it stops being true.

- [x] 12.5 **MINOR — `IsOnThePage` expressed a whitelist as a fallthrough.** Now an
      exhaustive `switch` over all five field ids, with an unknown id not linked.
      A sixth added later can no longer inherit `HasTimes` semantics by silence,
      which is the absence-is-not-a-decision shape this change rejects elsewhere.

- [x] 12.6 **NIT — the strong rule's failure now names the state** it found the
      flag dead in, which is the piece needed to act on it now that the rule checks
      per state and suppressions are keyed by one.

- [x] 12.7 Re-verified: clean `--no-incremental` build at **0 warnings**; 768 unit,
      **277** rendering, 58 integration, 69 client.

## 13. Outstanding — recorded, not claimed

- ~~**The keyboard pass over the two product fixes is NOT done.**~~ **Done
  2026-08-21**, in a real browser against the real scenario, and it passes.

  The scenario was produced end to end rather than simulated: a hand-made POST
  carrying a pin to a service with no picker returns `resource-not-eligible`, and
  the redraw renders the summary link to `#ubookit-who` — the exact case that linked
  nowhere before the fix.

  **Verified by real interaction:** clicking that link moves focus to the wrapper
  (`document.activeElement` becomes `DIV#ubookit-who`), and the focused element's
  text is the error message, so a screen reader announces the problem on arrival.
  That is precisely the thing a DOM assertion cannot judge, and it is the reason QA
  asked for the pass.

  **Verified structurally:** the wrapper carries `tabIndex === -1` and is absent
  from the sequential focus order — a target, not a control. The who control itself
  is a native `<select>` with `tabIndex 0`, a `label[for]` that resolves and an
  `aria-describedby` that resolves. Reading order is date → length → who → show
  times → times → booker fields → book.

  **Not verified, and stated as such:** sequential traversal by actually pressing
  Tab. The browser extension's synthetic key events do not drive native focus
  traversal — five Tab presses left focus inside the date input's segments — so tab
  ORDER rests on DOM order plus `tabIndex`, not on keys pressed. A native `<select>`
  with `tabIndex 0` is keyboard operable by construction, so this does not cast
  doubt on the control; it means one link in the chain is reasoned rather than
  observed. A human keyboard pass is still owed for the flows generally, as it has
  been since ⑤.

- **The page is now honest but still incoherent in one state** (QA's observation,
  for a follow-up rather than this change): a redraw with no times tells the visitor
  "Please enter your name" while rendering no name field. The summary fix makes the
  *link* honest and arguably makes the incoherence less visible. Not introduced
  here; worth an obligation rather than silence.

## 14. QA round 3 — REJECT, remediated

One MAJOR, of the same shape as round 2's, which is why QA held the line. I had
asked it to hunt for a fifth uncovered path rather than accept that I had closed
the one it found. There were three.

- [x] 14.1 **MAJOR — three of the five `IsOnThePage` arms were silently mutable.**
      `Duration => Model.HasTimes`, `Resource => Model.HasTimes` and `_ => true` all
      passed 277 tests. The first two are the **opposite** fault from the one this
      code was written to fix: a *working* association silently dropped rather than a
      dangling one emitted.

      Closing it needed more than fixtures, and that is the finding. Every rule so
      far asked "does this link resolve", and a link that is never emitted resolves
      vacuously — **dropping an association was invisible to the entire suite**. Two
      changes:

      - three fixture states: a duration failure and a refused choice each with no
        times, and an error naming an unrecognised control (the default arm, added
        last round so a sixth field id could not inherit `HasTimes` by silence, and
        then exercised by nothing);
      - a new rule, `Every_problem_whose_control_is_on_the_page_is_linked_to_it`,
        asserting the direction nothing asserted. The spec already required it — "a
        problem SHALL be associated with its field where that field is on the page" —
        so only the check was missing.

      All five arms now die under mutation, and so does removing the guard entirely.
      Non-vacuity guarded: `The_linked_problem_rule_is_exercised` fails if the
      fixtures stop producing errors that name rendered controls.

- [x] 14.2 **A third suppression of one shape appeared**, which QA had named in
      advance as the signal to revisit — "and even then it should be a narrow rule
      tied to the specific field, with each instance still recorded". Taken as
      given: the three entries stay enumerated and visible, the shared reason is a
      constant rather than three restatements, and
      `Every_suppression_is_of_the_shape_it_claims` proves each entry really is a
      flag silenced by an error against the control it speaks about. A blanket rule
      would have swallowed a genuine defect coexisting with such an error; a shared
      *reason* without a check would have let an unrelated exemption hide behind the
      wording.

- [x] 14.3 **NIT — the MODIFIED requirement's explanatory sentence over-fitted**,
      stating this front end's composition as a property of the capability inside a
      requirement alternative UIs implement. Reworded as an example, with the
      general claim ("that some controls may be dropped") kept.

- [x] 14.4 **NIT — the requirement's first paragraph stated the association
      guarantee unqualified** while the second narrowed it, so a reader who greps
      and stops gets a false answer. A parenthetical pointer added; no guarantee
      touched, and the guarantee diff re-run afterwards — 2 → 4 scenarios, no SHALL
      dropped.

- [x] 14.5 **Recorded, per QA:** the `:102` calculus. The interpretation that a
      clause about associating error text with its control does not reach the case
      where no control exists now lives inside a *different* requirement, so an
      editor rewriting `:102` will not see it. The alternative — a second MODIFIED
      entry adding one cross-reference sentence to a requirement carrying eight
      SHALLs and five scenarios — carries more wholesale-replacement risk than it
      removes. That trade is the reason, not an oversight.

- [x] 14.6 **Correcting my own record, in the other direction.** §12.4 said the
      shared-literal assertion "does not close the blind spot". QA checked and found
      it closes the specific instance demonstrated in round 1 — disabling the
      settled-length error branch while its sibling keeps the literal alive now
      fails. The general limitation survives (the guard counts states, not
      branches), but I under-claimed, and an under-claim in a record is as wrong as
      an over-claim.

- [x] 14.7 Re-verified: clean `--no-incremental` build at **0 warnings**; 768 unit,
      **336** rendering, 58 integration, 69 client.

## 15. QA round 4 — REJECT, remediated

One MAJOR, and it was **the same shape as round 3's, one level up**. Round 3's
diagnosis had been: every rule asked "does this link resolve", and a link never
emitted resolves vacuously. That shape survived the fix — every rule now asked
about *links*, so a message that never reached the page satisfied all of them.

- [x] 15.1 **MAJOR — nothing asserted that an error message reaches the document
      at all.** Two mutations, both 336 green: deleting the summary's
      `else { @error.Message }` branch, and replacing the link's text with
      "click here".

      The first violates a SHALL this change had written **that same round**. The
      MODIFIED requirement narrows the association guarantee — don't link to a
      control that isn't there — and the entire compensating argument is "every
      problem is still stated in text". Nothing checked the compensation. I wrote
      the clause and did not implement it, which is worth naming separately from
      missing a case: the spec was right and the code silently did not follow it.

      Closed by `Every_problem_is_stated_in_the_document`: every message in
      `Model.Errors` appears in the document's text, and where the problem is
      linked, the link's own text is the message. Both assertions, not one — the
      first alone kills "click here" today only because that mutation happens to
      leave the message nowhere else; a view rendering the message in a sibling
      span would slip past. A link's text is its accessible name, and that is the
      property being stated.

      Non-vacuity: `Both_halves_of_the_reporting_rule_are_exercised` counts linked
      and unlinked problems **separately** (floors 4 and 2, actuals 9 and 3). A
      single total would have sat green over precisely the hole — a fixture set
      producing only linked problems is the state in which the deleted `else`
      survives.

- [x] 15.2 **MINOR — a summary link's target was never required to be focusable.**
      Dropping `tabindex="-1"` from both `_DateAndLength` wrappers passed 336.
      Resolving is not arriving: a plain `div` moves the viewport and leaves focus
      in the summary, which for a keyboard or screen-reader user is the failure the
      link exists to prevent.

      This had been verified **by hand, once**, in §13's keyboard pass, and I had
      filed it under "things a DOM assertion cannot judge". That was a conflation:
      *announcement* is not checkable, *focusability* plainly is. Folded into rule
      1's in-page-link clause rather than made a separate test, since it is a
      property of the same link and separating them would let one drift from the
      other. A negative `tabindex` satisfies it — such a wrapper should be a target
      and absent from the tab order.

      A fourth mutation QA had not named: the `_Times` `fieldset` is a link target
      too, and a `fieldset` is not natively focusable. Same exposure; also now dead.

- [x] 15.3 **MINOR — `role="alert"` recorded rather than fixed.** See §7.3.

- [x] 15.4 Spec additive on both requirements — nothing rewritten, so no guarantee
      could be dropped by construction. 4 → 6 scenarios. Re-verified: 0 warnings;
      768 unit, **376** rendering, 58 integration, 69 client.

## 16. QA round 5 — REJECT, remediated

I had asked QA two questions of my own, having noticed the pattern, and the more
useful half of its answer was that **both of my candidates were wrong**.

- [x] 16.1 **Both proposed next levels rejected, with reasons worth keeping.**

      *Is the rendered message the right one for the failure code?* No — and joining
      them would be a **regression**. The fixtures carry arbitrary strings
      deliberately; this suite's property is *whatever message the model carries
      reaches the page*, which should be content-agnostic. `BookingMessages` is unit
      tested for code → message, the flow tests assert the model carries the right
      message, and this suite asserts message → page. The chain closes at
      `BookingError.Message`. Re-joining it here would break rendering tests on
      every copy edit.

      *Extra or stale problems?* Already covered — the rule iterates the **model's**
      errors, so filtering or truncating fails. Only an *invented* problem escapes,
      and a hardcoded list item is not a plausible regression. The one plausible
      variant, `_ErrorSummary` included twice by a flow view, belongs to the
      composition gap (§7.2).

      Recorded because asking "what else" is only useful if the answer is allowed to
      be "nothing, and here is why" — otherwise it manufactures work.

- [x] 16.2 **MAJOR — requirement 1's own clause was not implemented.** The
      requirement says every control "carries an **accessible name**".
      `HasAccessibleName` returned true on `label[for="…"] is not null` —
      **existence, not a name**. An empty `<label for="ubookit-name"></label>`
      leaves the control unnamed and passed.

      The tell was inside the method the whole time: the `aria-label` branch checked
      `{ Length: > 0 }` while both element-based branches checked only existence.
      That kind of internal inconsistency is visible by reading, without any
      mutation — a cheaper detector than mutation for this class, and worth
      remembering as one.

- [x] 16.3 **MINOR, same class — a reference can resolve to an empty element.** An
      `aria-describedby` pointing at an empty span resolves perfectly, and the
      message is still in the summary, so §15.1's rule is satisfied too. The visitor
      standing on the field hears the association announced and nothing else. Empty
      `legend`s the same: a group boundary announced with no name, which is worse
      than no grouping.

      One predicate — `HasText`, non-whitespace `TextContent` — applied at three
      sites: described and labelling targets, accessible names (all three branches,
      including the wrapping-`label` case QA had not named), and a new
      `Every_group_is_named_by_its_legend`, which checks the `legend` is present as
      well as non-empty since nothing had ever checked the legend at all.

      `IsNullOrWhiteSpace` rather than `IsNullOrEmpty` is load-bearing, not
      incidental: Razor readily produces an element containing only a newline and
      indentation, and `IsNullOrEmpty` would have left the hole open. QA confirmed
      by mutation.

- [x] 16.4 **Ten mutations, all previously green, all dead.** Labels emptied
      (name 17, date 25); legends emptied (details 17, times 17); described targets
      emptied (name 1, email 1, who 1, times 1, duration 2); email hint emptied (17).
      The duration target is a **tenth QA did not name** — I swept every described
      target rather than the four listed, on the principle that a list handed to you
      is a sample. Each mutation was verified to have actually applied (md5 before
      and after) before its result was trusted.

- [x] 16.5 **No product code changed** — the first round where the finding was
      purely a hole in the rules and the shipped views already satisfied it.
      Spec additive on requirement 1: one clause, three scenarios. Re-verified:
      0 warnings; 768 unit, **415** rendering, 58 integration, 69 client.

## 17. QA round 6 — APPROVE

No blocking findings. QA re-ran all ten mutations independently and every failure
count matched, then probed four further edges that held: a whitespace-only label, a
whitespace-only described target, a single-space described target, and `<legend>`
removed outright from each fieldset. The guarantee diff on the MODIFIED requirement
was re-run mechanically after all three rounds of editing it — every SHALL carried,
both original scenarios byte-verbatim, 2 → 6 scenarios.

**The shape of the review, worth stating once.** The rig was sound from round 1 and
never needed rework. Every round after found the same fault at a different altitude:
**a rule checking the mechanism rather than the guarantee the mechanism serves.**

    links resolve → are they emitted → is anything said → does the target speak

Each level was invisible from the one below, and every one was found by mutation
rather than by reading. That is the transferable lesson, and it generalises past
this suite: a rule that checks a mechanism passes whenever the mechanism is absent,
and absence is the failure mode nobody writes a test for.

The pattern is exhausted on this surface. The next level up is "does the text say
the *right* thing", which is §16.1 — deliberately out of scope, because the chain
already closes elsewhere and coupling the rig to wording would cost more than it
buys.
