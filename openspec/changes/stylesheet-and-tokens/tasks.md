## 1. Baseline before anything changes

- [ ] 1.1 Run the full solution build and record the warning count — the baseline is **ZERO**, so read the build against zero, not against "no new warnings"
- [ ] 1.2 Run the full test suite and record the pass count and the per-project breakdown, so §5's re-run can be diffed rather than eyeballed
- [ ] 1.3 Capture the current rendered HTML of every view under test (the rendering suite's fixtures already produce it) as the before-image for the class edits

## 2. Class vocabulary (views only, no CSS yet)

Do this first and alone: it is the only part that changes rendered markup, so keeping
it in its own step means §5's suite delta has exactly one cause.

- [ ] 2.1 Rename `ubookit-service-booking` → `ubookit-booking--service` in `BookingFlow/Service.cshtml`, keeping `ubookit-booking` alongside it
- [ ] 2.2 Add `ubookit-field` to the three bare wrapper `div`s in `_DateAndLength.cshtml` (lines ~37, ~66, ~122) — including the two `tabindex="-1"` wrappers, which are field positions even when they hold settled text rather than a control
- [ ] 2.3 Add `ubookit-field` to the three bare wrapper `div`s in `_YourDetails.cshtml`
- [ ] 2.4 Add `ubookit-submit` to all three buttons: `Catalogue.cshtml` ("Continue"), `_DateAndLength.cshtml` ("Show times"), `_YourDetails.cshtml` ("Book")
- [ ] 2.5 Confirm no id was renamed, added or removed anywhere in §2 — diff the id set against 1.3
- [ ] 2.6 Re-run the rendering suite and account for every difference from 1.2; an unexplained delta stops this task rather than being waved through as additive

## 3. The stylesheet

- [ ] 3.1 Create `src/UBookIt.Web/wwwroot/ubookit.css`. Confirm no csproj change is needed (measured — the `Sdk.Razor` project already emits static web assets)
- [ ] 3.2 Write the structural rules that need no token: `box-sizing`, `color-scheme: inherit`, field stacking, text-block measure, a `line-height` floor of 1.5
- [ ] 3.3 Lay out the time grid — the radios in `_Times` currently stack vertically, which is poor for a day with many starts. Wrapping flex or grid, visible radios and labels kept exactly as rendered
- [ ] 3.4 Apply the minimum target size floor (2.5.8) — and only that; no other property touching a date input, select or button
- [ ] 3.5 Express error and notice emphasis without a colour decision: `currentColor` border and font weight
- [ ] 3.6 Verify by inspection that the file declares no literal colour value anywhere

## 4. Tokens

- [ ] 4.1 Define the layout tokens as use-site fallbacks: `--ubookit-space`, `-field-gap`, `-section-gap`, `-measure`, `-radius`, `-border-width`
- [ ] 4.2 Define the type tokens: `--ubookit-font-family` and `-font-size` falling back to `inherit`, `-line-height` to `1.5`
- [ ] 4.3 Define the colour tokens, all falling back to `currentColor`/`transparent`/`auto`: `--ubookit-accent` (used *only* as `accent-color`), `-color-error`, `-color-muted`, `-color-border`, `-color-surface`
- [ ] 4.4 Audit every token declaration: **no token default may be declared on any element the package renders** (design D2). This is the change's silent-failure point
- [ ] 4.5 Cross-check the token list against the stylesheet in both directions — nothing documented that is unread, nothing read that is undocumented

## 5. Emission

- [ ] 5.1 Create `Views/Shared/UBookIt/_Styles.cshtml` emitting the single `link` to `_content/UBookIt.Web/ubookit.css`
- [ ] 5.2 Confirm no other view emits a `link` or `style` element for package styling — one emission route, because it is the future theme's off-switch (design D4)
- [ ] 5.3 Verify the partial resolves when called from a **site** layout, not only from within the package (the mechanism is measured; this confirms this particular partial)

## 6. Tests

- [ ] 6.1 Guard the D2 mechanic: assert no documented token has a default declared on a package-rendered element. **Mutation-check it** by moving one default onto `.ubookit-booking` and confirming the test fails — a guard for a silent failure that cannot itself fail is worse than none
- [ ] 6.2 Assert the stylesheet declares no literal colour value, and mutation-check by adding one
- [ ] 6.3 Assert no appearance property targets a date input, select or button beyond the target-size floor
- [ ] 6.4 Assert the class vocabulary follows the block / part / `--`variant rule, and that every field wrapper and every submit control carries its hook
- [ ] 6.5 Assert the id set is unchanged by this change, and that no stylesheet declaration selects on an id
- [ ] 6.6 Assert the token list and the stylesheet agree in both directions (4.5 as a test, not a one-off audit)
- [ ] 6.7 Assert exactly one emission route exists
- [ ] 6.8 Vary the fixtures rather than testing one shape — per the standing lesson that a covering test has twice been written so it could not fail

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
