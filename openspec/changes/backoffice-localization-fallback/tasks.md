## 1. Reproduce before fixing

- [ ] 1.1 On the main TestSite (Umbraco 17, current `main` build), set a backoffice user's language
  to Spanish and open the Resources, Services, Bookings, Settings and Closures views. Verify:
  term keys are visible (screenshot recorded). This proves the live check in 4.x can tell broken
  from fixed.
- [ ] 1.2 Repeat 1.1 with the user's language set to English (United Kingdom). Verify: term keys
  are visible. This confirms the `en-GB` claim in proposal.md; if it does **not** reproduce, stop
  and correct the proposal and spec before going on.

## 2. Guard (red first)

- [ ] 2.1 Add a test in `tests/UBookIt.Tests` (beside `ClientLocalizationKeyTests`) per design D4:
  it finds exactly one `type: "localization"` entry in `Client/src/section/manifest.ts`, reads its
  `culture`, reads `UMB_DEFAULT_LOCALIZATION_CULTURE` from
  `Client/node_modules/@umbraco-cms/backoffice/dist-cms/libs/localization-api/localization.manager.js`,
  and asserts they are equal ignoring case, naming both on failure. A missing file, constant or
  entry fails; nothing skips. Verify: against the unchanged manifest the test **fails** and its
  message names `en-us` and `en`.
- [ ] 2.2 Prove the guard can fire on its other branches: temporarily remove the localization
  entry from the manifest and verify the test fails as "no localization entry"; temporarily point
  it at a non-existent `node_modules` path and verify it fails rather than skips. Revert both and
  confirm `git diff` shows only 2.1's test file.

## 3. Fix

- [ ] 3.1 In `Client/src/section/manifest.ts`, change the localization extension's `culture` to
  `"en"` and its `name` to `"uBookIt English"`. Keep the alias `UBookIt.Localization.EnUS` and the
  `js` import of `../localization/en-us.js`. Add a comment saying the culture is Umbraco's fallback
  so every backoffice language resolves, and that the alias is kept for compatibility (design D2).
  Verify: 2.1's test passes.
- [ ] 3.2 Build the client first (`npm run build` in `src/UBookIt.Backoffice/Client`), then the
  solution. Verify: 0 warnings; the client suite, unit suite, rendering suite and openspec
  validation all pass. Report each count against the baselines (unit 2129 + the new test, client
  355, rendering 1178, integration 210, openspec 26 specs).

## 4. Live check on main (Umbraco 17)

Each step uses a normal reload, not a hard refresh (design, Risks).

- [ ] 4.1 Spanish user: open every uBookIt view (Resources list and editor, Services list and
  editor, Bookings with the move, cancel and place-on-behalf modals, Settings, Closures and the
  holiday import, Responsibility) and the uBookIt permission group in the user-group editor.
  Verify: English wording everywhere, no term keys (screenshots).
- [ ] 4.2 English (United Kingdom) user: the same views. Verify: English wording, no keys.
- [ ] 4.3 English (United States) user: the same views, plus Umbraco's own Users section and a
  content node. Verify: uBookIt's wording is unchanged from 1.x's baseline, and Umbraco's own
  screens show wording, not keys (design D3: nothing of Umbraco's was overwritten).
- [ ] 4.4 Site translation takes precedence: add a temporary `umbraco-package.json` to the
  TestSite's `App_Plugins` registering a `localization` extension with `culture: "es"` and an
  inline dictionary that supplies **one** uBookIt term (e.g. the Resources heading) in Spanish.
  As the Spanish user, verify: that one term is in Spanish and every other term is in English.
  Then delete the temporary folder and verify `git status` shows it gone.

## 5. Sweep for sentences this change falsifies

- [ ] 5.1 Search `docs/`, `README.md`, `CHANGELOG.md`, `CLAUDE.md`, `openspec/specs/**` and code
  comments for `en-US`, `en-us`, `English (United States)`, and `localization` near "culture".
  For each hit, record in this task: still true, or changed. The three "with `en-US` provided"
  spec sentences are expected to stay (proposal Non-goals); `docs/mvp.md:109` ("any language
  beyond `en-US`") is expected to stay. Verify: every hit is listed with its decision.

## 6. Port to dev/v18 (same sitting)

- [ ] 6.1 Cherry-pick the change's commits onto a branch from `dev/v18`. Verify: the manifest and
  the new test are identical on both lines (`git diff main dev/v18-branch -- <both files>` is
  empty), and the full v18 build and suites pass against the v18 baselines (unit 2141 + the new
  test).
- [ ] 6.2 On the v18 TestSite (Umbraco 18), repeat 1.1 against the **old** v18 build first, then
  4.1, 4.2 and 4.3 against the new one. Verify with screenshots. This is the only proof for v18:
  its guard reads the 17.5.3 npm package (design D4), not the Umbraco 18 code that runs.

## 7. Hand over

- [ ] 7.1 Record in the deferred obligations: `dev/v18`'s client builds against
  `@umbraco-cms/backoffice` `^17.5.3`, unchanged since the scaffold, so its type checks and this
  change's guard see Umbraco 17's backoffice, not 18's. An observation, not a decision. Verify:
  the entry exists.
- [ ] 7.2 Write the handover for the release change (`release-17-3-2` / `release-18-2-2`). Its
  changelog entry names the defect as an operator sees it: a backoffice user whose language is
  not US English, British English included, saw term keys instead of wording on every uBookIt
  screen; it now shows English. It tells an operator who still sees keys after upgrading to
  reload the backoffice once with a hard refresh. It names no API change. Verify: the handover is
  in this file under 7.2.
- [ ] 7.3 QA: one `qa-review` subagent for this change, reused across rounds. The handover states
  each claim (counts, guard red-then-green, screenshots per line) for it to verify, not trust.
