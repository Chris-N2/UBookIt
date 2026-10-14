## 1. Reproduce before fixing

- [x] 1.1 On the main TestSite (Umbraco 17, current `main` build), set a backoffice user's language
  to Spanish and open the Resources, Services, Bookings, Settings and Closures views. Verify:
  term keys are visible (screenshot recorded). This proves the live check in 4.x can tell broken
  from fixed.
  *Done 2026-10-14. Chris's `dotnet build` had already picked up the fix, so the client was
  rebuilt from `HEAD`'s manifest (built bundle checked: `culture: "en-us"`) and served by the
  running site. Spanish user: tab title "#ubookitSection_label | #ubookitResources_label"; the
  section label, all five tabs, the Resources heading and its create button rendered as keys,
  while Umbraco's own menu was Spanish. The fixed manifest was then restored and the client
  rebuilt (bundle: `culture: "en"`).*
- [x] 1.2 Repeat 1.1 with the user's language set to English (United Kingdom). Verify: term keys
  are visible. This confirms the British English claim in proposal.md; if it does **not** reproduce, stop
  and correct the proposal and spec before going on.
  *Done by Chris, 2026-10-14: an English (United Kingdom) user sees the raw keys. (Umbraco's UK option
  is the culture `en`, not `en-GB` — corrected in the artifacts during 4.2.) Umbraco's user-settable fallback
  language applies to content only, so it does not help.*

## 2. Guard (red first)

- [x] 2.1 Add a test in `tests/UBookIt.Tests` (beside `ClientLocalizationKeyTests`) per design D4:
  it finds exactly one `type: "localization"` entry in `Client/src/section/manifest.ts`, reads its
  `culture`, reads `UMB_DEFAULT_LOCALIZATION_CULTURE` from
  `Client/node_modules/@umbraco-cms/backoffice/dist-cms/libs/localization-api/localization.manager.js`,
  and asserts they are equal ignoring case, naming both on failure. A missing file, constant or
  entry fails; nothing skips. Verify: against the unchanged manifest the test **fails** and its
  message names `en-us` and `en`.
  *Done: `LocalizationCultureTests`. Against the unchanged manifest: FAIL, "registers its terms
  under culture 'en-us', but Umbraco's fallback culture (UMB_DEFAULT_LOCALIZATION_CULTURE) is
  'en'".*
- [x] 2.2 Prove the guard can fire on its other branches: temporarily remove the localization
  entry from the manifest and verify the test fails as "no localization entry"; temporarily point
  it at a non-existent `node_modules` path and verify it fails rather than skips. Revert both and
  confirm `git diff` shows only 2.1's test file.
  *Done, run with `--no-build` against mutated files: entry renamed → FAIL "Expected exactly one
  localization extension … found 0"; `localization.manager.js` renamed away → FAIL "Expected
  source file not found". Neither skipped. Both restored; `git status` then showed only the new
  test file.*

## 3. Fix

- [x] 3.1 In `Client/src/section/manifest.ts`, change the localization extension's `culture` to
  `"en"` and its `name` to `"uBookIt English"`. Keep the alias `UBookIt.Localization.EnUS` and the
  `js` import of `../localization/en-us.js`. Add a comment saying the culture is Umbraco's fallback
  so every backoffice language resolves, and that the alias is kept for compatibility (design D2).
  Verify: 2.1's test passes.
  *Done: `LocalizationCultureTests` passes within the full unit run (2130).*
- [ ] 3.2 Build the client first (`npm run build` in `src/UBookIt.Backoffice/Client`), then the
  solution. Verify: 0 warnings; the client suite, unit suite, rendering suite and openspec
  validation all pass. Report each count against the baselines (unit 2129 + the new test, client
  355, rendering 1178, integration 210, openspec 26 specs).
  *Suites done 2026-10-14 (Debug, per project, the TestSite running): unit 2130 (2129 + 1), rendering
  1178, integration 210, client 355; `openspec validate --all --strict` 27 passed (26 specs + this
  change). NOT yet done: the 0-warning Release build, which a running TestSite locks; it runs once
  the main TestSite is stopped, before 6.x. Restore printed NU1902/NU1903 advisories
  (`System.Security.Cryptography.Xml` 10.0.10, `…Pkcs` 10.0.0), which CLAUDE.md excludes from
  warnings-as-errors and leaves to the `audit` workflow; whether they are new since 2026-10-13 is
  checked separately, not in this change.*

## 4. Live check on main (Umbraco 17)

Each step uses a normal reload, not a hard refresh (design, Risks).

- [x] 4.1 Spanish user: open every uBookIt view (Resources list and editor, Services list and
  editor, Bookings with the move, cancel and place-on-behalf modals, Settings, Closures and the
  holiday import, Responsibility) and the uBookIt permission group in the user-group editor.
  Verify: English wording everywhere, no term keys (screenshots).
  *Done 2026-10-14, after a NORMAL reload (F5), which already served the new bundle. Each screen
  was checked by screenshot and by a script walking every shadow root for text or label
  attributes matching a uBookIt key (`ubookit…_…`, `user_permissionsEntityGroup_ubookit`). The
  script was first proved to find a key planted inside a shadow root. A scan counted only when the
  screen's heading or text proved the right view had rendered (two scans that ran on an
  unrendered view were discarded and repeated). Zero keys in each: Resources list (1441 text
  nodes), Services list (1318), Bookings (895), Move modal (958), Cancel confirmation (934),
  Record a booking (1046), Closures with holiday import (877), Settings (673), resource editor
  with responsibility and capability inputs (1405), service editor with the start-alignment
  readout (886), the Perm Test user group's permissions, showing "uBookIt", "See bookings",
  "Act on bookings" (1806). Umbraco's own screens stayed Spanish.*
- [x] 4.2 English (United Kingdom) user: the same views. Verify: English wording, no keys.
  *Done 2026-10-14. The page language was `en`: Umbraco's UK option IS the culture `en`
  (`Umb.Localization.EN`), not `en-GB` as the artifacts first said. Proposal, design and spec
  corrected (and Spanish likewise: Umbraco's option is `es`, not `es-ES`); `--strict` still valid.
  Same screens and method as 4.1, zero keys in each: Resources (1441), Services (1319), Bookings
  (896), Move (959), Cancel (935), Record a booking (1047), Closures (877), Settings (673),
  resource editor (1405), service editor (886), the Perm Test group's permissions (1806, showing
  "uBookIt", "See bookings", "Act on bookings"). Umbraco's own menu was in English.*
- [x] 4.3 English (United States) user: the same views, plus Umbraco's own Users section and a
  content node. Verify: uBookIt's wording is unchanged from 1.x's baseline, and Umbraco's own
  screens show wording, not keys (design D3: nothing of Umbraco's was overwritten).
  *Done 2026-10-14. Umbraco's own localization manager reported `documentLanguage` `en-us`, with
  dictionaries `en` and `en-us` loaded. Read in-page: 382 uBookIt keys in `en`, 0 in `en-us`, so a
  US user misses in `en-us` and finds the term in `en` (the lookup path the design predicts). D3
  checked directly: each of the 2616 string keys in Umbraco's own `assets/lang/en.js` was compared
  with the merged `en` dictionary, and 0 differed. Same screens and method as 4.1, zero keys in
  each: Resources (1442), Services (1319), Bookings (896), Move (959), Cancel (935), Record a booking
  (1047), Closures (877), Settings (673), resource editor (1405), service editor (885), Perm Test
  group permissions (1806). Umbraco's Users section and Content section showed wording
  throughout.*
- [x] 4.4 Site translation takes precedence: add a temporary `umbraco-package.json` to the
  TestSite's `App_Plugins` registering a `localization` extension with `culture: "es"` and an
  inline dictionary that supplies **one** uBookIt term (e.g. the Resources heading) in Spanish.
  As the Spanish user, verify: that one term is in Spanish and every other term is in English.
  Then delete the temporary folder and verify `git status` shows it gone.
  *Done 2026-10-14 by an equivalent in-page method, chosen to avoid a temporary file, a site restart
  and another change of Chris's user language. A package manifest's localization ends up as a
  registration in `umbExtensionsRegistry`, and a user's language ends up as a
  `umbLocalizationRegistry.loadLanguage(...)` call, so both were made directly in the page: a
  `localization` extension with `culture: "es"` and an inline dictionary supplying ONLY
  `ubookitResources.label` = "Recursos", then `loadLanguage('es')`. The manager then held `en`,
  `en-us` and `es`, with exactly one uBookIt key in `es`. Opening uBookIt by a click (no reload):
  the Resources tab, heading and page title read "Recursos"; every other uBookIt term was English
  ("Create resource", "Services", "Bookings", column headings, row buttons); Umbraco's own menu
  was Spanish; the scan found 0 keys (1442 text nodes). A reload then restored `en-us` with only
  `en` and `en-us` loaded. Nothing was written to disk, so there is nothing to delete.*

## 5. Sweep for sentences this change falsifies

- [x] 5.1 Search `docs/`, `README.md`, `CHANGELOG.md`, `CLAUDE.md`, `openspec/specs/**` and code
  comments for `en-US`, `en-us`, `English (United States)`, and `localization` near "culture".
  For each hit, record in this task: still true, or changed. The three "with `en-US` provided"
  spec sentences are expected to stay (proposal Non-goals); `docs/mvp.md:109` ("any language
  beyond `en-US`") is expected to stay. Verify: every hit is listed with its decision.
  *Done 2026-10-14. Searched `en-US|en-us|English (United States)|US English|culture` and
  `locali[sz]|translat|raw key|term key` across docs, README, CHANGELOG, CLAUDE.md,
  `openspec/specs/**`, `src/**` (.ts/.cs/.json) and `tests/**`. Decisions:
  `README.md:120` "no language other than `en-US`": still true (the wording is US English only).
  `docs/mvp.md:109`: still true. `booking-management/spec.md:655,1028`,
  `resource-management/spec.md:184` "with `en-US` provided": still true, kept (Non-goals).
  Every reference to the file `en-us.ts` (five C# tests, the vitest suites,
  `move-fields.ts:105`, `place-on-behalf-fields.ts:104`): true, the file keeps its name.
  `CHANGELOG.md:53-55` (17.3.1, "if your site overrides uBookIt's backoffice wording"): a
  published entry, not edited; it does raise a point for the release note, now in 7.2.
  `culture` in `PreservedQuery.cs`, `PreservedQueryTests.cs`, `BookingMessageComposer.cs:106` and
  the TestSite's generated schemas: different concepts (front-end query string, email date
  formats, Umbraco schema), unaffected. Nothing changed.*

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
  reload the backoffice once with a hard refresh. It names no API change. It also tells a site that
  translated or reworded uBookIt by registering its own dictionary under `en-us` that the override
  still applies to US English users, but a British English user (Umbraco's `en`), who used to see
  keys, now sees the package's wording; to reach them too, register the same dictionary under `en`
  as well (found by the 5.1 sweep). Verify: the handover is
  in this file under 7.2.
- [ ] 7.3 QA: one `qa-review` subagent for this change, reused across rounds. The handover states
  each claim (counts, guard red-then-green, screenshots per line) for it to verify, not trust.
