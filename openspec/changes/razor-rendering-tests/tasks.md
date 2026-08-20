## 1. The project and the rig

- [ ] 1.1 Add `tests/UBookIt.Tests.Rendering/` (xunit, `Microsoft.NET.Test.Sdk`, AngleSharp), referencing `UBookIt.Web` and `UBookIt.Core`. Add it to the solution. Keep it **out of** `UBookIt.Tests` — that suite is 768 tests in under a second and the speed is worth protecting.
- [ ] 1.2 Share `Support/RepoFiles.cs` from `UBookIt.Tests` by `<Compile Include=… Link=…>` rather than copying. It resolves the repo root from its own compile-time path, so a linked copy resolves identically — confirm that by asserting `RepoFiles.Root` from the new project.
- [ ] 1.3 Build the rig: a `ServiceProvider` with MVC + the Razor view engine, `UBookIt.Web` added as an application part, rendering by **absolute view path** into a `StringWriter` over a `DefaultHttpContext` (design D1). One `RenderAsync(path, model)` entry point.
- [ ] 1.4 Prove the rig renders the **compiled** view rather than a re-parse: assert that no `.cshtml` file is read from disk during a render, or equivalently that rendering succeeds with the source tree's `Views` directory unreadable/renamed. If that cannot be arranged cheaply, assert instead that the view type resolved comes from the `UBookIt.Web` assembly. **Do not skip this** — "we render the shipped artefact" is the claim the whole rig's value rests on.

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
- [ ] 1.5 Smoke test: `ServiceConfirmation.cshtml` renders non-empty HTML containing the booker's name. One view, one assertion, so a broken rig fails obviously rather than as eleven confusing failures.

## 2. The view inventory

- [ ] 2.1 Enumerate the eleven in-scope views **by scanning the Views directory and subtracting the deferred three**, not by hardcoding a list — a view added later must join the suite automatically or the suite decays. The deferred three are named explicitly with their reason.
- [ ] 2.2 Assert the inventory is what it should be: eleven in scope, three deferred, fourteen total. A scan that silently found four views would otherwise pass every rule below.
- [ ] 2.3 Assert the deferred three are deferred *for the stated reason* — that each transitively reaches `BeginUmbracoForm` — rather than by name alone. `BookingFlow/Default.cshtml` reads as Umbraco-free and is not; it is a one-line delegate into `Booking/Default.cshtml`, and a by-name list would not say why.

## 3. Model fixtures

- [ ] 3.1 A fixture per in-scope view: a base model, plus the small set of base states that view's own shape needs (design D4) — errors/none, times/none, length fixed/chosen, choice offered/not, reset/not.
- [ ] 3.2 Keep fixtures free of Core: these are view models, and building them from resolved pools would drag the whole booking graph into a rendering suite. Construct them directly.
- [ ] 3.3 Where a state is awkward to construct, prefer changing the fixture over exempting the property. An exemption is a hole in the rule; an awkward fixture is only awkward.

## 4. Rule 1 — rendered markup resolves its own references

- [ ] 4.1 One test over every view × every base state (design D5), asserting per document: every `label[for]` resolves; every id in every `aria-describedby` and `aria-labelledby` resolves; no id appears twice; every input/select has an accessible name.
- [ ] 4.2 Every assertion message carries the **view path** and the offending id or element, since one test over eleven views is only debuggable if it says where it failed.
- [ ] 4.3 Tag-helper residue asserted against the **raw string**, not the parsed DOM (design D6 risk): a `<partial>` element parses into a tidy node and would pass a DOM check. Assert no literal `<partial` and no `asp-` attribute survives.
- [ ] 4.4 Mutation-check each clause by breaking one view deliberately — a dangling `aria-describedby`, a duplicated id, a `for` pointing at nothing, a `<partial>` tag — and confirm the rule goes red **and names that view**. Revert each. A rule nobody has seen fail is a rule nobody knows works.
- [ ] 4.5 Fix whatever the rule finds in the shipped views. If it finds nothing, say so explicitly in this file — "clean on first run" is a result worth recording, and is also the result most likely to mean the rule is vacuous, so pair it with 4.4.

## 5. Rule 2 — a view renders every state its model can express

- [ ] 5.1 Extract each view's referenced model properties from its source, handling **every reference form Razor admits** — `Model.X`, `Model?.X`, and `Model` passed whole to a partial. Design D3.
- [ ] 5.2 **Non-vacuity guard A**: assert every in-scope view yields at least one referenced property. A pure delegate that genuinely references none — `BookingFlow/Unavailable.cshtml` and its two siblings — is an explicit named exemption, not a silent pass.
- [ ] 5.3 **Non-vacuity guard B**: assert the extracted set for `_DateAndLength.cshtml` matches its expected fourteen properties by name. If the extraction silently stops finding things, this fails; without it, a broken regex makes the whole rule pass trivially.
- [ ] 5.4 For each referenced property, vary it (flip a bool, change a string, empty or fill a collection, move a number) and assert the rendered output differs in at least one base state. Failure message names the view **and the property**.
- [ ] 5.5 Exemptions live in one place with a reason each, and the test asserts the exemption list is non-empty only where expected — so an exemption is a decision on the record rather than an absence.
- [ ] 5.6 Mutation-check the rule where it matters most: reintroduce ⑩-1's defect by moving `_DateAndLength.cshtml`'s reset notice back inside the `OffersResourceChoice` branch, and confirm the rule goes red naming `ResourceChoiceWasReset`. **This is the test of the test** — the whole design rests on catching that class generically. Revert.
- [ ] 5.7 Fix whatever it finds, or record a clean first run as in 4.5.

## 6. Verification

- [ ] 6.1 Full clean `--no-incremental` build. Warning baseline is **zero**; read against zero.
- [ ] 6.2 All four suites green: unit, integration, client, rendering. Record the rendering suite's runtime — if it is slow enough to be annoying, say so here rather than letting the next person discover it.
- [ ] 6.3 Confirm `UBookIt.Tests` is unchanged in `git diff` apart from any linked-file plumbing, and that its runtime has not moved.
- [ ] 6.4 Attack each clause of designs D2, D3 and D4 for a clause with no covering test — the technique that found the no-common-length gap in ⑩ and two uncovered clauses in ⑩-1. Record what was found.
- [ ] 6.5 Sync-time outward grep for sibling specs this change falsifies. It has found something on six consecutive changes. In particular check whether any spec claims the accessibility bar is unverified or verified only by review.
- [ ] 6.6 Hand to `qa-review` in a **fresh context or subagent** — never the context that wrote the code.

## 7. Follow-up, to be written up rather than remembered

- [ ] 7.1 Record the deferred three as an obligation with its spike named: does `BeginUmbracoForm` need a booted Umbraco context, or only `AddDataProtection()` and `AddAntiforgery()` over a `DefaultHttpContext`? The answer decides whether that suite stays fast, and it should be settled **before** the follow-up's design is written, not during it.
- [ ] 7.2 Record that flow-view/partial **composition** remains untested until that follow-up, and that the existing source scan asserting each flow view names each partial path is load-bearing until then.
