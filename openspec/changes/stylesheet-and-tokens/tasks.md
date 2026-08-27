## 1. Baseline before anything changes

- [x] 1.1 Run the full solution build and record the warning count — the baseline is **ZERO**, so read the build against zero, not against "no new warnings"
      → **0 warnings, 0 errors.**
- [x] 1.2 Run the full test suite and record the pass count and the per-project breakdown, so §5's re-run can be diffed rather than eyeballed
      → **1433 passed, 0 failed, 0 skipped**: `UBookIt.Tests` 777, `UBookIt.Tests.Rendering` 598, `UBookIt.Tests.Integration` 58. Integration ran rather than skipping, so SQL Server was reachable.
- [x] 1.3 Capture the current rendered HTML of every view under test (the rendering suite's fixtures already produce it) as the before-image for the class edits
      → **Captured the source-level `id=` and `class=` sets instead** (29 id occurrences, 31 class occurrences, interpolated forms included), because the rendering suite does not dump HTML to disk and adding a dumper would be more change than the check is worth. This is faithful for the purpose 2.5 needs it for — the edits add class attributes only, so an `id=` source diff cannot miss an id change. Recorded as a deliberate substitution rather than as the task written.

## 2. Class vocabulary (views only, no CSS yet)

Do this first and alone: it is the only part that changes rendered markup, so keeping
it in its own step means §5's suite delta has exactly one cause.

- [x] 2.1 Rename `ubookit-service-booking` → `ubookit-booking--service` in `BookingFlow/Service.cshtml`, keeping `ubookit-booking` alongside it
- [x] 2.2 Add `ubookit-field` to the wrapper `div`s in `_DateAndLength.cshtml` — **five, not three**: the three bare ones plus the two `tabindex="-1"` wrappers, which are field positions even when they hold settled text rather than a control
- [x] 2.3 Add `ubookit-field` to the three bare wrapper `div`s in `_YourDetails.cshtml`
- [x] 2.4 Add `ubookit-submit` to all three buttons: `Catalogue.cshtml` ("Continue"), `_DateAndLength.cshtml` ("Show times"), `_YourDetails.cshtml` ("Book")
- [x] 2.4a **NEW, found while applying** — rename `ubookit-time` → `ubookit-times-option`. It is the item inside `ubookit-times` but was not prefixed by it, so it read as a second block differing from its own container by one letter: the same ambiguity as 2.1. Free — the only reference anywhere was to the *id* `ubookit-time-0`, not the class. `ubookit-catalogue-choice` was checked and is **not** a third case (see design D7). Proposal and design D7 updated from "one rename" to "two"
- [x] 2.5 Confirm no id was renamed, added or removed anywhere in §2 — diff the id set against 1.3
      → **id diff empty.** Class diff exactly as intended: 2 renames, 8 `ubookit-field` (5 + 3), 3 `ubookit-submit`.
- [x] 2.6 Re-run the rendering suite and account for every difference from 1.2; an unexplained delta stops this task rather than being waved through as additive
      → **One failure, and it was the suite working as designed.**
      `BranchReachabilityTests.The_literals_that_cannot_distinguish_a_branch_are_enumerated`
      pins the set of literals a view emits from more than one place — rule 3's known
      blind spot, made visible rather than silent — and its own comment says a new
      shared literal "has to be justified, which is the moment to ask whether the
      branches need distinguishing". `ubookit-field` is emitted from mutually exclusive
      branches in `_DateAndLength`, so it is genuinely new to that set.
      **Justified rather than suppressed**, and the question it demands has a definite
      answer: the branches must NOT be distinguished, because the point of the class is
      that a field is a field whether it holds a control or the settled text that
      replaced one. Two entries added with that reasoning recorded at the site.
      Back to **1433 passed, 0 failed, 0 warnings** — level with §1.

## 3. The stylesheet

- [x] 3.1 Create `src/UBookIt.Web/wwwroot/ubookit.css`. Confirm no csproj change is needed (measured — the `Sdk.Razor` project already emits static web assets)
- [x] 3.2 Write the structural rules that need no token: `box-sizing`, `color-scheme: inherit`, field stacking, text-block measure, a `line-height` floor of 1.5
- [x] 3.3 Lay out the time grid — the radios in `_Times` currently stack vertically, which is poor for a day with many starts. Wrapping flex or grid, visible radios and labels kept exactly as rendered
      → **Inline-block, deliberately not flex/grid on the `fieldset`.** A `legend` inside a flex or grid `fieldset` is handled inconsistently across browsers, and that legend names the group — losing it is an accessibility regression, not a cosmetic one. Inline blocks wrap without a container, so this needed no markup change. The catalogue was left as a stacked column on purpose: its labels are names of unpredictable length, where wrapping gives ragged rows that scan worse than a column.
- [x] 3.4 Apply the minimum target size floor (2.5.8) — and only that; no other property touching a date input, select or button
      → Applied to the **option row** rather than the radio, since the label is associated by `for` and the row is the target. A hard `24px` constant, **not** a token: a floor a site can lower is not a floor.
- [x] 3.5 Express error and notice emphasis without a colour decision: `currentColor` border and font weight
- [x] 3.6 Verify by inspection that the file declares no literal colour value anywhere
      → Only `currentColor`, `inherit`, `transparent`, `auto` and `color-mix()` over those. The permitted-set boundary is stated at the top of the file and is what 6.2 tests.
- [x] 3.7 **NEW, found while writing the stylesheet** — the email hint had **no class hook**. My earlier audit scanned `class=` attributes only, so an element carrying just an id (`<span id="ubookit-email-hint">`) was invisible to it. Added `ubookit-hint`, which is what it is. Re-checked every other id-bearing element: all ten already carry a class or are native controls needing none

## 4. Tokens

- [x] 4.1 Define the layout tokens as use-site fallbacks: `--ubookit-space`, `-field-gap`, `-section-gap`, `-measure`, `-radius`, `-border-width`
- [x] 4.2 Define the type tokens: `--ubookit-font-family` and `-font-size` falling back to `inherit`, `-line-height` to `1.5`
- [x] 4.3 Define the colour tokens, all falling back to `currentColor`/`transparent`/`auto`: `--ubookit-accent` (used *only* as `accent-color`), `-color-error`, `-color-muted`, `-color-border`, `-color-surface`
- [x] 4.4 Audit every token declaration: **no token default may be declared on any element the package renders** (design D2). This is the change's silent-failure point
      → Audited by inspection; **the test in 6.1 is what actually holds it**, since an audit does not survive the next edit.
- [x] 4.5 Cross-check the token list against the stylesheet in both directions — nothing documented that is unread, nothing read that is undocumented
      → All 14 read by at least one declaration. Becomes 6.6 as a test.

## 5. Emission

- [x] 5.1 Create `Views/Shared/UBookIt/_Styles.cshtml` emitting the single `link` to `_content/UBookIt.Web/ubookit.css`
      → **Razor comments do not nest.** The first version wrapped a usage example in an inner `@* … *@`, which closed the outer comment early and turned the rest of the file into live markup — four build errors, including `<head>` parsed as a malformed tag helper. Noted in the file itself so the next editor does not repeat it.
- [x] 5.2 Confirm no other view emits a `link` or `style` element for package styling — one emission route, because it is the future theme's off-switch (design D4)
      → Now a test (6.7), not a one-off check.
- [ ] 5.3 Verify the partial resolves when called from a **site** layout, not only from within the package (the mechanism is measured; this confirms this particular partial) — deferred to 7.3, which needs the running TestSite
- [x] 5.4 **NEW, forced by adding the first non-rendering view.** `_Styles.cshtml` joined the rendering suite's scanned inventory and broke ten rules that assume a model, literals and a document body. Excluded **by name, with a reason and with what would lift it**, exactly as `ViewInventory`'s own guidance demands — and *not* left untested: it is covered by the emission rule instead. Two vacuity guards added so the exclusion cannot decay: the scan now counts **both** sets (so a view cannot be added and excluded in one change silently), and the exclusion set is asserted to be exactly that one file. The same treatment was needed in `ServiceFrontendTests.The_shared_partials_are_all_reached_from_a_flow`, whose premise — a partial nothing references is dead code — does not reach a partial whose consumer is the site's layout; that exemption carries its own vacuity guard too

## 6. Tests

- [x] 6.1 Guard the D2 mechanic: assert no documented token has a default declared on a package-rendered element. **Mutation-check it** by moving one default onto `.ubookit-booking` and confirming the test fails — a guard for a silent failure that cannot itself fail is worse than none
      → **Mutation-checked twice over.** Synthetically, including the nested-parenthesis `color-mix` fallback that a naive "strip `var(...)`" approach gets wrong; and by a **real edit to the shipped file** (`--ubookit-field-gap` moved onto `.ubookit-field`), which failed naming the token. Reverted.
      The detector is one pattern — `--ubookit-…\s*:` — because a custom property name is followed by `:` only where it is *declared*; inside `var(--x, default)` it is followed by `,` or `)`. No paren balancing needed.
- [x] 6.2 Assert the stylesheet declares no literal colour value, and mutation-check by adding one
      → Hex (3/4/6/8 digits) and every functional notation, each mutation-checked. Comments are **stripped first**: they explain the rules in prose and so mention the very things the rules forbid — scanning the raw file would fail on its own documentation, a false positive that teaches contributors to weaken the rule.
- [x] 6.3 Assert no appearance property targets a date input, select or button beyond the target-size floor
- [x] 6.4 Assert the class vocabulary follows the block / part / `--`variant rule, and that every field wrapper and every submit control carries its hook
      → Five rules. **Mutation-checked by reverting 2.4a**: restoring `ubookit-time` failed both the enumeration and the prefix rule, the latter naming the exact fault. Dropping one `ubookit-field` failed the pinned count. Both reverted; tree verified identical to HEAD.
      → **A loophole was found and closed while writing it.** The prefix rule needs a declared block list, and a part with no block passes the moment someone declares its prefix a "block" whether or not anything renders it — I did exactly that with `ubookit-date` before noticing. A declared block must now itself be a rendered class, so the list records what the vocabulary *is* rather than being a place to make failures disappear.
- [x] 6.5 Assert the id set is unchanged by this change, and that no stylesheet declaration selects on an id
      → Stylesheet asserted to contain no `#` at all, which given 6.2 forbids hex means no id selector. The "unchanged" half was done as the 2.5 diff; pinning ids in a test belongs to the accessibility rules that already own them rather than here.
- [x] 6.6 Assert the token list and the stylesheet agree in both directions (4.5 as a test, not a one-off audit)
      → The published list lives in the test rather than being derived from the file, so the two are checked against each other instead of against themselves.
- [x] 6.7 Assert exactly one emission route exists
- [x] 6.8 Vary the fixtures rather than testing one shape — per the standing lesson that a covering test has twice been written so it could not fail
      → Every enumeration rule carries a non-vacuity assertion, and the value-vocabulary scan is additionally proved to detect `color: red` — the named-colour case the syntax rule deliberately does not cover.
      → **Why enumeration rather than a colour-name blacklist:** listing the 148 CSS named colours would be a knowingly partial guard. Enumerating the *permitted* value vocabulary instead makes it complete — any new identifier fails and has to be justified, whether it names a colour or not. It caught my own omission of `in` (from `color-mix(in srgb, …)`) on first run.

## 7. Verify live

- [ ] 7.1 Boot the TestSite. If it will not start, clear the stale `umbracoKeyValue` upgrader rows first (SQL Server is a local instance on localhost)
- [ ] 7.2 Request `_content/UBookIt.Web/ubookit.css` and confirm `200` and a CSS content type
- [ ] 7.3 Add the partial to the TestSite layout and confirm the flow renders styled, at `/book` and through to a confirmation
- [ ] 7.4 Override two or three tokens from the site's `:root` and confirm they take effect — this is the headline claim being checked by hand, not only by a test
- [ ] 7.5 Confirm the flow still renders correctly with the partial removed
- [ ] 7.6 **Answer open question 1**: do the `tabindex="-1"` wrappers get a visible focus indicator for free when reached from an error-summary link, keyboard-only? Record what was observed; only add an override if the answer is no
- [ ] 7.7 Check the flow in a forced-colors / high-contrast mode and in dark mode, since D1 claims both work for free
- [ ] 7.8 Try `brand/tokens.css` over the defaults as the independent-tokens fixture it was kept for — the test is whether *someone else's* tokens drop cleanly, and do not commit `brand/` as part of this change

## 8. Documentation

- [ ] 8.1 Resolve open question 3 (new page, or a section of `docs/booking-page.md`) and write the styling contract: the one layout line, the token list, the class vocabulary and its naming rule
- [ ] 8.2 State that the package owns the asset and the site owns its appearance, and that the route is tokens and classes rather than a copy of the file
- [ ] 8.3 Write the ACR-style accessibility statement: per criterion, met by the shipped default / passes to the site on a token override / determined by the host page
- [ ] 8.4 Reconcile `docs/booking-page.md` — "the flow's internals are not customisable" stays true of markup and must now distinguish appearance from markup
- [ ] 8.5 Sweep `docs/` for any sentence this change falsifies **before** sweeping the specs, per the standing rule that docs go first

## 9. Close out

- [ ] 9.1 Run the outward sibling-falsification grep across `openspec/specs/` for sentences this change makes untrue, and record each as carried-forward-unchanged or corrected — silence is not a decision
- [ ] 9.2 Re-check the `## MODIFIED` requirement against the carried-forward list: all nine SHALLs and all five scenarios accounted for, with the no-author-stylesheet clause present and intact
- [ ] 9.3 Full clean build (zero warnings) and full suite green, diffed against §1
- [ ] 9.4 `openspec validate stylesheet-and-tokens --strict`
- [ ] 9.5 Record for the repo owner: CLAUDE.md invariant 5 needs rewording (open question 2) — flag it, do not edit it
- [ ] 9.6 Note in the change what remains unmeasured: a NuGet-installed consumer serving the asset by request, and whether an RCL theme view wins a location expander's first candidate (the next change's spike)
- [ ] 9.7 Hand to `qa-review` in a **fresh context or subagent** — the context that implemented this must not review it

## 10. QA rounds

- [ ] 10.1 Record each QA round's findings and their disposition here, as previous changes have
