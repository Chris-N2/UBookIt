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
- [x] 3.2 Build the client first (`npm run build` in `src/UBookIt.Backoffice/Client`), then the
  solution. Verify: 0 warnings; the client suite, unit suite, rendering suite and openspec
  validation all pass. Report each count against the baselines (unit 2129 + the new test, client
  355, rendering 1178, integration 210, openspec 26 specs).
  *Suites done 2026-10-14 (Debug, per project, the TestSite running): unit 2130 (2129 + 1), rendering
  1178, integration 210, client 355; `openspec validate --all --strict` 27 passed (26 specs + this
  change). NOT yet done: the 0-warning Release build, which a running TestSite locks; it runs once
  the main TestSite is stopped, before 6.x. *Then done: `dotnet build UBookIt.slnx -c Release --no-incremental`,
  0 errors; every warning is NU1902 or NU1903, i.e. NuGet advisories, no compiler or
  analyser warning. *(Corrected, QA round 1: the per-code tally first recorded here, 20 and 32,
  was miscounted; QA counted 36 and 72 on main, the same as v18. Only the claim that every warning
  is an NU advisory is relied on.)* Restore printed NU1902/NU1903 advisories
  (`System.Security.Cryptography.Xml` 10.0.10, `…Pkcs` 10.0.0), which CLAUDE.md excludes from
  warnings-as-errors and leaves to the `audit` workflow; whether they are new since 2026-10-13 is
  checked separately, not in this change.* *Checked: all five (GHSA-7hqp-7c5m-4vq6,
  GHSA-c399-9853-6jmj, GHSA-r955-8q5j-3h83, GHSA-846f-f764-76mx, GHSA-2fmv-rv2c-59x7) were
  PUBLISHED 2026-10-13, fixed in 10.0.13, reached through Umbraco 17.7.1's DataProtection
  dependency; no patched Umbraco 17.x or 18.x yet. Out of scope here; raised with Chris and
  recorded in the deferred obligations.*

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

- [x] 6.1 Cherry-pick the change's commits onto a branch from `dev/v18`. Verify: the manifest and
  the new test are identical on both lines (`git diff main dev/v18-branch -- <both files>` is
  empty), and the full v18 build and suites pass against the v18 baselines (unit 2141 + the new
  test).
  *Done 2026-10-14 on `backoffice-localization-fallback-v18` (from `dev/v18` 1474856): the three
  commits cherry-picked cleanly; `git diff` between the two branches on `manifest.ts`,
  `LocalizationCultureTests.cs` and `en-us.ts` is empty. Stale hashed client assets deleted,
  client rebuilt (bundle `culture: "en"`). Unit 2142 (2141 + 1; v18's own total, so not stale
  binaries), rendering 1178, integration 210, client 355, openspec 27; Release build 0 errors,
  warnings only NU1902 (36) and NU1903 (72), the same advisories as main.*
- [x] 6.2 On the v18 TestSite (Umbraco 18), repeat 1.1 against the **old** v18 build first, then
  4.1, 4.2 and 4.3 against the new one. Verify with screenshots. This is the only proof for v18:
  its guard reads the 17.5.3 npm package (design D4), not the Umbraco 18 code that runs.
  *Done 2026-10-14 on the v18 TestSite (Umbraco 18, which adds the Library section).
  BEFORE (client rebuilt from `dev/v18`'s manifest, bundle `culture: "en-us"`), Spanish user:
  section label, tabs, heading, column headings and every row button rendered as keys. Read from
  the running Umbraco 18: `UMB_DEFAULT_LOCALIZATION_CULTURE` is `en` (so the v18 guard's answer,
  read from 17.5.3, matches the runtime), and the 381 `ubookit…` keys WERE loaded, in `en-us`
  (the server's default UI language boots the page in `en-US` first), but a Spanish lookup
  checks only `es` then `en`.
  AFTER (committed manifest restored, `git status` clean apart from this file; bundle
  `culture: "en"`), each language checked with the same scanner and render proof as 4.x, zero keys
  on every screen: Spanish (`documentLanguage` `es`; 382 uBookIt keys in `en`, 0 in `en-us`),
  UK (`en`) and US (`en-us`). Screens per language: Resources (1451-1452 text nodes), resource
  editor (1012), Services (1329), service editor (896), Bookings (845), Move (908), Cancel (884;
  dismissed with its own Cancel, booking 2SMJ-2MJC still Confirmed), Record a booking (992),
  Closures with the holiday import (562, content confirmed by its own sentences), Settings (683),
  the Perm Test group's permissions (2489-2490: "uBookIt", "See bookings", "Act on bookings").
  US also: Umbraco's own Users and Content sections showed wording, no key-shaped text.
  D3 on Umbraco 18, checked against the dictionary Umbraco actually registers
  (`Umb.Localization.EN`, 2727 keys): our 382 keys share NO key with it, and 0 of its keys
  differ in the merged `en`. (A first comparison against `assets/lang/en.js` showed one
  difference, `contentTypeEditor_elementDoesNotSupport`; that file and the registered extension
  differ by a full stop, and our dictionary has no such key. Not ours.)*

## 7. Hand over

- [x] 7.1 Record in the deferred obligations: `dev/v18`'s client builds against
  `@umbraco-cms/backoffice` `^17.5.3`, unchanged since the scaffold, so its type checks and this
  change's guard see Umbraco 17's backoffice, not 18's. An observation, not a decision. Verify:
  the entry exists.
  *Done: `ubookit-deferred-obligations` memory, with the 2026-10-13 advisories beside it.*
- [x] 7.2 Write the handover for the release change (`release-17-3-2` / `release-18-2-2`). Its
  changelog entry names the defect as an operator sees it: a backoffice user whose language is
  not US English, British English included, saw term keys instead of wording on every uBookIt
  screen; it now shows English. It tells an operator who still sees keys after upgrading to
  reload the backoffice once with a hard refresh. It names no API change. It also tells a site that
  translated or reworded uBookIt by registering its own dictionary under `en-us` that the override
  still applies to US English users, but a British English user (Umbraco's `en`), who used to see
  keys, now sees the package's wording; to reach them too, register the same dictionary under `en`
  as well (found by the 5.1 sweep). It says that an `en` override needs nothing special, in Umbraco's own
  terms: uBookIt's localization has `weight: 200`, and Umbraco registers higher weights first, so
  a site's ordinary `en` dictionary (no weight, or any weight up to the default of 100) is
  registered after it and overrides it; a site that already registered uBookIt wording under `en`
  keeps it.
  *(Corrected, QA round 1: the advice as first written could not be relied on, because at the
  default weight whether uBookIt's `en` terms or the site's won depended on registration order.
  8.1 fixes that; 8.4 proves the advice live. Reworded, QA round 2: it first said "below the
  default priority", while the manifest says weight 200, i.e. above.)*
  *Done: the paragraph above is the handover.* Verify: the handover is
  in this file under 7.2.
- [x] 7.3 QA: one `qa-review` subagent for this change, reused across rounds. The handover states
  each claim (counts, guard red-then-green, screenshots per line) for it to verify, not trust.

## 8. QA round 1 (REJECT): remediation

QA round 1 found one MAJOR, two MINORs and two NITs. Each fix below is new code and is reviewed as
such.

- [x] 8.1 MAJOR — a site's own `en` wording was overwritten by uBookIt's (design D6). The manifest
  entry now declares `weight: 200`, above Umbraco's default of 100, so uBookIt registers first in
  `en` and any default-weight `en` dictionary overrides it. Chris chose this over telling sites to
  lower their own weight (2026-10-14). Spec: the requirement now says the precedence includes
  English, and gains two scenarios (a site's own English wording takes precedence; the priority
  cannot drop unnoticed). Design: D3's false sentence corrected, D6 added, D4 extended. Proposal:
  the weight added to What Changes. 7.2's advice corrected.
  *Verify:* `LocalizationCultureTests` gains a weight test that reads Umbraco's default from
  `localization.registry.js` (`extension.weight ?? N`). Proved at its boundary after a clean client
  rebuild (the first attempt ran stale binaries, visible as 1 test instead of 2, and was
  discarded): weight removed → FAIL "declares no weight"; `100` → FAIL "weight is 100, but
  Umbraco's default weight is 100"; `101` → pass; `200` → pass. The culture test was re-proved
  after the entry-extraction rewrite: `en-us` → FAIL naming `en-us` and `en`. Manifest restored
  each time (`git diff --stat` shows only the intended edit).
- [x] 8.2 MINOR — "never loaded" was false (the v18 evidence showed `en-us` loaded for a Spanish
  user). Reworded to "never looked up" / "never consulted" in `manifest.ts`, the test's XML doc and
  proposal.md; design's table now says "consulted" and explains why loaded is not the question.
- [x] 8.3 MINOR — design.md's lookup block still said `es-es`. Now generic (base name, language,
  `en`), with a regional culture named as such for the example.
  NITs: the culture test's failure message no longer says "not exactly the registered culture"
  (it states the lookup chain instead); 3.2's miscounted tally is corrected above.
- [x] 8.4 Live, with a REAL package file, as QA asked (registration order is the point, so an
  in-page registration cannot prove it). Add a temporary `App_Plugins/LiveCheckEnOverride/
  umbraco-package.json` to the TestSite declaring a `localization` extension, `culture: "en"`, no
  weight, with an inline dictionary supplying ONE uBookIt term (the Resources heading). Restart the
  TestSite so the manifest is read. As a UK (`en`) user and as a US (`en-us`) user: that one term
  shows the site's wording and every other term the package's. Then, as a contrast that proves the
  check can fail, set uBookIt's weight BELOW the site's (the plan first said 100; see the
  correction below for why 100 proves nothing), rebuild, reload, and observe the package's wording
  win; restore. Finally delete the temporary folder and confirm `git status`.
  Both lines.
  *main (Umbraco 17), 2026-10-14. The override file at
  `src/UBookIt.TestSite/wwwroot/App_Plugins/LiveCheckEnOverride/umbraco-package.json` (culture
  `en`, no weight, supplying only `ubookitResources.label` = "Rooms and people") was read at
  TestSite start: the registry listed `LiveCheck.EnOverride` (`en`, default 100) beside
  `UBookIt.Localization.EnUS` (`en`, 200) and Umbraco's `en`. UK user (`en`): merged `en` resolved
  `ubookitResources_label` to "Rooms and people" and `ubookitResources_create` to uBookIt's
  "Create resource"; the tab, heading and page title read "Rooms and people", everything else was
  uBookIt's wording. US user (`en-us`): `en-us` holds no uBookIt label, so the lookup reached `en`
  and showed "Rooms and people" on the tab, heading and title, the rest uBookIt's.
  CONTRAST. At `weight: 100` (client rebuilt) the SITE still won, with uBookIt's extension listed
  BEFORE the site's. *(Corrected, QA round 2: that run says nothing about the state before the
  fix. Umbraco's `byType` sorts by `weight || 0`, so an EXPLICIT 100 beats the site's ABSENT
  weight (0) and sorts first; the state before the fix had no weight, 0 against 0, which falls back
  to arrival order, and was never observed. QA round 1's prediction is therefore neither confirmed
  nor refuted, and this record no longer calls it a correction. Weight 200 makes the question
  moot, since it sorts first under both orderings.)* To prove the weight decides, uBookIt's
  weight was set BELOW the site's (`50`, client rebuilt): the registry then registered uBookIt last
  and its "Resources" overwrote the site's wording on tab, heading and title. So the check can
  fail, and the weight is what decides it. Manifest restored with `git checkout`, client rebuilt
  (bundle `weight: 200`), `git status` showing only the untracked override folder.*
  *dev/v18 (Umbraco 18), same file and method after the round's commits were cherry-picked and the
  client rebuilt from cleared assets. Registry: `UBookIt:200`, `Umb:100`, `LiveCheck:100` (`en`).
  UK user (`en`): "Rooms and people" on tab, heading and title, "Create resource" uBookIt's. US
  user (`en-us`, no uBookIt label in `en-us`): the same. Contrast at weight 50: "Resources"
  (uBookIt's) won, as on 17. Manifest restored with `git checkout`, client rebuilt to
  `weight: 200`. The override folder was then deleted and `git status` was clean.*
- [x] 8.5 Full suites, Release build, `openspec validate --all --strict`, on both lines, with the
  client assets cleared and rebuilt first on each line. Then port the round's commits to the v18
  branch and check the two branches are identical on the manifest, the test and the artifacts.
  *main: unit 2131 (2129 + 2), rendering 1178, integration 210, client 355, openspec 27; Release 0
  errors, warnings NU1902 x36 and NU1903 x72 only. v18 (after cherry-picking the round's commits
  and clearing/rebuilding the client): unit 2143 (2141 + 2), rendering 1178, integration 210,
  client 355, openspec 27; Release 0 errors, NU1902 x36 and NU1903 x72 only. `git diff` between the
  branches on the manifest, the test and the change folder: empty.*
- [ ] 8.6 Return to the same QA reviewer with each claim to verify, naming the fixes as new code.

## 9. QA round 2 (REJECT): remediation

- [x] 9.1 MAJOR — both guards could be satisfied by COMMENT text, a false-green this round's own
  fix introduced (8.1 widened the entry slice to start at the `{`, which took in the comment above
  `type:` that names both fields). `LocalizationEntry` now strips `/* */` and `//` comments from
  the slice before reading, and `Field` accepts a field only as the first thing on its line.
  *Verify,* after clearing and rebuilding the client (`dotnet test --no-build`, the manifest
  mutated and restored each time; expected result in brackets):
  - as written [pass] → pass;
  - culture `en-us` [FAIL] → FAIL;
  - weight `100` [FAIL] → FAIL;
  - M1, comment line `// Was culture: "en" before X.` above the entry, real culture `en-us`
    [FAIL] → FAIL (QA's first false-green);
  - M2, comment `// Keep weight: 200 here.`, real weight deleted [FAIL] → FAIL (QA's second);
  - M3, the real weight commented out as `// weight: 300,` [FAIL] → FAIL (QA's third);
  - M4, block comment `/* weight: 300, */`, real weight deleted [FAIL] → FAIL;
  - M5, `weight: 200` moved before `type:` [pass] → pass;
  - M6, `weight: 50` moved before `type:` [FAIL] → FAIL. M6 proves M5's move applied, because the
    unmutated file would also pass.
  Manifest restored; `git status` shows only the intended edits.
- [x] 9.2 MINOR — the record asserted an overwrite at the default weight that was never observed,
  and missed Umbraco's first sort (`byType`, `weight || 0`, `extension.registry.js:85`, checked).
  D6 now describes both orderings and calls the default-weight outcome order-dependent. The test's
  XML doc and weight failure message say the same. The 7.2 parenthetical says "could not be
  relied on". 8.4's plan text and conclusion are corrected (the 100 contrast proves nothing about
  the pre-fix state; the 50 contrast stands).
- [x] 9.3 NIT — 7.2 now says it in Umbraco's terms (`weight: 200`, higher registers first), not
  "below the default priority".
- [x] 9.4 Suites on both lines (assets cleared and rebuilt), branches identical, back to the same
  QA reviewer.
  *main 2131 / v18 2143 unit, 1178, 210, client 355, openspec 27 on each; branches identical.*

## 10. QA round 3: APPROVE WITH NITS

- [x] 10.1 QA verified the round-2 MAJOR fix and probed it beyond the claimed matrix (trailing
  comments, a `weight: 300` inside a string, `//` in a `js:` URL, multi-line block comments, a
  weight on the `name` line, a `/*` inside a string): every case either failed loudly or passed
  when it should. MINOR and NIT verified; counts and parity re-verified on both lines. QA: "Further
  QA rounds are not warranted. The NIT can be fixed at apply's discretion without another review."
- [x] 10.2 NIT taken: the weight failure message said that at exactly the default "the winner
  depends on the order the extensions arrived in". An explicit `100` beats a weightless site in
  `byType`, so it now says "the outcome depends on the site's own weight and on the order the
  extensions arrived in". Message text only; the guard's threshold is unchanged. Both guard tests
  pass.
- [x] 7.3 (see sections 8-10: three rounds, REJECT, REJECT, APPROVE WITH NITS).

