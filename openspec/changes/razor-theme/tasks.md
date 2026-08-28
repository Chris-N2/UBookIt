## 1. Prove the mechanism where it has not been proven

- [x] 1.1 Add a theme **fixture** RCL under `tests/` (`Sdk.Razor`, referencing `UBookIt.Web`), supplying views at the theme path for at least two view components across both `Booking` and `BookingFlow`. Fixtures live under `tests/` and **never** under `src/UBookIt.Web/Views` — `ViewInventory` scans that directory on disk, so a fixture placed there silently joins the shipped view set and every ⑪ rule starts asserting over a test fixture.
- [x] 1.2 Assert the fixture's views are precompiled into its own assembly at the expected identifiers and that no loose `.cshtml` reaches its build output — the spike's setup check, so a later failure cannot be misread as a precedence result.
- [x] 1.3 **Measure precedence without runtime compilation**, in `UBookIt.Tests.Rendering` (which references `UBookIt.Web` and deliberately not `UBookIt.TestSite`, so runtime compilation is absent from its closure). This is the open risk from D8 and ⑫'s unmeasured gap. If precedence does **not** hold here, stop and report: the design needs rethinking, not adjusting.
- [x] 1.4 Record the measured configurations, so the documentation task in §7 can state them without asserting more than was measured.

## 2. Theme registration that provably wins

- [x] 2.1 Add the view-location expander with the `{0}`-only theme format `/Views/Shared/UBookIt/Themes/<theme>/{0}.cshtml`. No `{1}` and no `{2}` — under this resolution path `{1}` is whatever controller is executing and `{2}` is empty.
- [x] 2.2 Add the registration entry point and theme options, registering the expander so it is appended **last** in the chain. **Corrected after QA:** the original text named an `IComposer` "since composers run at `IUmbracoBuilder.Build()`" — that is false. `AddComposers()` composes immediately, so a composer registered nothing for any site that called `AddUBookItTheme` after it, silently and with the stylesheet suppressed too. It is now an `IPostConfigureOptions<RazorViewEngineOptions>`, which runs after every `IConfigureOptions` regardless of registration order. Treat that ordering as a mechanism to verify, not a fact to rely on — see 2.4a.
- [x] 2.2a Verify the ordering claim against the framework rather than restating it: sweep every supported call order in the resolution guard, and add a boot-time check in the running site that the theme's location really is first.
- [x] 2.3 Fix the theme at startup and document it as a constraint: one theme package-wide, `PopulateValues` contributing nothing, so changing the theme later would leave a populated view-location cache keyed without it.
- [x] 2.4a **QA round 1 (CRITICAL).** The composer mechanism was factually wrong and produced a silent unthemed+unstyled site for the most natural call order. Replaced with a post-configure, swept across every call order, backed by a boot-time check of the outcome, and mutation-checked from a clean build. Also fixed from the same round: the guard now calls the package's real entry point rather than a chain the test assembled (MAJOR), the mutation is the rejected mechanism rather than a test-owned enum (MAJOR), the published model types are bound to the shipped views' own `@model` (MAJOR), the emission assertion is untrimmed so the column-0 whitespace property is guarded (MINOR), and the unreachable no-model diagnostic is gone (MINOR).
- [x] 2.4 **The resolution guard.** Run the whole expander chain as the framework runs it and assert the theme's view is what resolves, for every view the theme supplies. It must NOT assert that an expander is registered, where it was registered from, or that a composer ran — a guard of that kind passes on the exact broken configuration this exists to catch.
- [x] 2.5 Mutation-check 2.4 against the real failure and confirm the guard fails. A guard that cannot see a losing chain is not a guard. **Two mutations, both run:** (a) the rejected mechanism — a plain `Configure` ahead of the host's setups, which is what the spike and the composer version both produced; (b) the shipped registration downgraded from `PostConfigure` to `Configure`, which fails three tests including the boot check. **Run the mutation from a clean build**: restoring a file with a timestamp-preserving copy left it older than the mutation's own build output, MSBuild skipped recompiling it while still reporting success, and the result was a false RED that read exactly like a design flaw.
- [x] 2.6 Assert resolution wins for **both** view components, not just one, so a chain correct for one and wrong for another cannot pass.

## 3. The theme contract and completeness check

- [x] 3.1 Publish the required view set — both components' view names — with the view model each receives.
- [x] 3.2 Implement the completeness check over a theme's assembly using `ApplicationPartManager.PopulateFeature(new ViewsFeature())`; `ViewDescriptor.RelativePath` gives the theme identifier and `descriptor.Type.Assembly` gives the supplying assembly.
- [x] 3.3 Check **declared view models as well as presence**. A view at the right path with the wrong `@model` passes a name-only check and then throws on a visitor's request.
- [x] 3.4 Make the check public API and usable from a theme's own test project — this is where an incomplete theme is meant to be caught.
- [x] 3.5 Log an error at boot naming each missing view, and let the package's view render in its place. Assert the log content, not merely that something was logged.
- [x] 3.6 Assert a **complete** theme logs nothing, so the check cannot pass by always complaining.
- [x] 3.7 Assert per-view fallback actually renders the package's view for a view the theme omits — the behaviour is the framework's default, so the test must prove the package's handling of it rather than the framework's.

## 4. Stylesheet suppression

- [x] 4.1 Make `~/Views/Shared/UBookIt/_Styles.cshtml` emit nothing when a theme is active, unless the theme declares it wants the package's stylesheet.
- [x] 4.2 Add the theme's opt-in and assert it emits exactly what an unthemed site emits.
- [x] 4.3 Extend the existing single-emission-route check to hold with a theme active — it is strengthened by this change, not qualified by it.
- [x] 4.4 Confirm `_Styles.cshtml` stays in `ViewInventory.NotRendered` on its existing stated grounds, or, if it now emits something a visitor can perceive, move it into `All` per the rule that class already documents. Razor comments do not nest — an inner `@* *@` closes the outer one and the rest of the file becomes live markup.

## 5. The building-block contract

- [x] 5.1 Verify a theme view can call the package's shared partials by absolute path with the model they declare, and that a theme calling none of them is equally valid.
- [x] 5.2 Record `~/Views/Shared/UBookIt/_{DateAndLength,ErrorSummary,Times,YourDetails}.cshtml`, `IBookingFormView` and `ServiceFormModel` as promised public surface, so a later change to them is called out as breaking.
- [x] 5.3 Confirm ⑪'s 598 rendering tests are untouched. **If they need changing, stop** — that is the signal the design has drifted into replacing partials rather than bypassing them.

## 6. Exercise it live

- [x] 6.1 Register a theme in `UBookIt.TestSite` and confirm both components render themed under real Umbraco page rendering, at `/` and `/?flow`. **Re-run after the QA round-1 fix, from a wiped `bin`/`obj`.** The first run was against a stale DLL still holding the mutated `Configure` registration, and the TestSite's order wins under that too — so it passed while proving nothing about the shipped mechanism. QA caught the risk; the timeline confirmed it.
- [x] 6.2 Confirm an unthemed site is byte-for-byte unchanged. Re-run on the clean build against the pre-change capture taken from stashed original code.
- [x] 6.3 Stop the TestSite before rebuilding — it locks its DLLs and the failure reads exactly like a code fault.

## 7. Documentation, and the promises being reversed

- [x] 7.1 Rewrite `docs/booking-page.md` §"What you still cannot change": the flow's markup **is** customisable, via a theme. Keep the warning that a **site file** at a package view's path still does nothing, since that is the trap a reader meets first.
- [x] 7.2 Correct the same section's prediction that the fix needs "a path the package deliberately does not compile into itself" — the spike disproved it. The theme path *is* precompiled, into the theme's own assembly, and wins anyway.
- [x] 7.3 State the measured configurations for both the override failure and the theme route, distinguishing measurement from expectation.
- [x] 7.4 Add the themed-rendering boundary to the published accessibility statement: markup claims describe the views the package ships; a theme's markup is the theme author's; no claim is made about a theme in either direction.
- [x] 7.5 Document the theme authoring route: the path convention, the required view set, the completeness check, the stylesheet opt-in, and the building blocks.
- [x] 7.6 Edit **CLAUDE.md invariant 5** to add the fourth category — theirs entirely when a theme replaces the markup — keeping the three existing categories and the no-author-stylesheet anchor intact.

## 8. Verify and close

- [x] 8.1 Full solution build at **zero** warnings — the baseline is zero, so read every build against zero and not against "no new ones".
- [x] 8.2 Full test suite green, with the count compared against the 1572 baseline.
- [x] 8.3 `openspec validate --strict` across all specs.
- [x] 8.4 **Re-read the three delta specs against the code before syncing.** Deltas go stale after every QA round; on ⑬ both were corrected at sync, and syncing them unread would have written a weaker requirement into the main spec than the code held while looking like a clean sync.
- [ ] 8.5 At sync, hand-correct the `default-frontend` **Purpose** paragraph, which asserts the markup AA claim unconditionally and is falsified by this change. It is prose outside any requirement, so no delta touches it and no grep for a requirement name will find it.
- [x] 8.6 Re-run the outward sweep for sibling specs this change falsifies. Use **multiline** matching when absence-checking a clause: a wrapped sentence defeats a single-line grep and reads exactly like a dropped guarantee.
- [ ] 8.7 Hand the change to `qa-review` in a **fresh context or subagent** — the context that implemented it never reviews it.
