## 0. Start from origin

- [x] 0.1 Branch `release-17-2-2` from `origin/main`, which must include `trusted-publishing`'s
  archive `d6f0199`. Verify `.github/workflows/publish.yml` exists, and that the unit suite passes
  at baseline, **1987**, from a clean build, before any edit.
  *Done:* branch `release-17-2-2` from `d6f0199`. `publish.yml` is present. A clean
  `CI=true --no-incremental` build had 0 warnings and 0 errors on the first attempt. Unit
  **1987/1987**, 0 skipped.

## 1. The setting length limit (spec: site-settings, ADDED)

- [x] 1.1 Replace the literal `2048` for the setting value in `UBookItDbContext` with an
  `internal const` in `UBookIt.Persistence` (design D1). Verify: `git grep -n 2048 src` finds it
  only at the constant, and a migration diff (`dotnet ef migrations add` to scratch, then discard)
  is empty.
  *Done:* `SettingRow.MaxValueLength` (`internal const`, `Entities/Rows.cs`), read by
  `HasMaxLength`.
  - **The verification as written was wrong, and is corrected here.** `git grep 2048 src` also
    finds the generated migrations and model snapshot. Those are history and are not edited, so
    the literal stays in them rightly. Outside `Migrations/`, the only hit is the constant.
  - **The migration diff is empty, and it was shown by a stronger check than the one planned.**
    The integration fixture's `MigrateAsync` refuses a model with pending changes. It passed with
    the constant (194/194). When the constant was temporarily set to `2050`, every
    `SettingsStoreTests` case failed with `PendingModelChangesWarning`: "The model for context
    'UBookItDbContext' has pending changes. Add a new migration…". So the constant can't drift
    from the migrations without the suite going red.
- [x] 1.2 Add the length check to `SettingValidation.IsValid`, after the blank check and before the
  kind switch. It measures every row `SettingText.RowsFor` would store (design D1), with the limit
  interpolated from the constant. Verify with unit tests:
  - accept at the limit and refuse at limit + 1, for `Text` and `Url` (a valid `https://` link
    padded to length);
  - `EmailList`: a list over the limit in total, with every address fitting, is **accepted**, and a
    list with one over-long address is refused;
  - each refusal's error names the limit;
  - a blank value still gets the blank message.

  *Done:* the check calls `SettingText.RowsFor` with an empty configuration and refuses if any row
  is longer than the constant. Message: "Must be no longer than 2048 characters." For a list:
  "Each address must be no longer than 2048 characters."
  - **Found during apply:** the plan measured the whole submitted string, which would have refused
    a long recipient list that works today. The proposal, spec (two new scenarios) and design D1
    were revised before the code was written.
  - **Found during apply:** no catalogue setting is of the `Text` kind. `Text` is reached through a
    constructed descriptor.
  - Tests in `SettingsControllerTests`:
    - `The_limit_is_the_boundary_whatever_the_setting_type` (Text and Url);
    - `A_blank_value_is_still_told_it_is_blank`;
    - the four controller tests in 1.3.
- [x] 1.3 The seam, per design D2 (revised during apply):
  - **Integration** (`SettingsStoreTests`, real SQL Server): a value of exactly
    `SettingRow.MaxValueLength` round-trips, including one made of non-BMP characters, and one
    unit longer fails at the store.
  - **Unit** (`SettingsControllerTests`, the real controller): at limit + 1, 400
    `SettingValueInvalid` and nothing written; at the limit, the written rows equal `RowsFor`'s;
    a recipient list over the limit in total is written in full.

  **Mutation check:** delete the check from 1.2, and confirm that the controller's limit + 1 test
  fails. Record the failure text, then restore.

  *Done:*
  - **Integration** (`SettingsStoreTests`):
    - `A_value_at_the_declared_capacity_round_trips_intact`, ASCII and surrogate pairs;
    - `A_value_past_the_declared_capacity_fails_at_the_store`, which asserts `DbUpdateException`
      with an inner message containing "truncated", so a broken fixture can't satisfy it.

    Integration total **194/194**, 0 skipped.
  - **Unit:**
    - `A_value_longer_than_the_store_holds_is_refused_before_the_store`;
    - `A_value_at_the_store_limit_is_written_as_the_rows_the_store_will_hold`;
    - `A_recipient_list_longer_than_the_store_limit_in_total_is_still_stored` (200 addresses);
    - `A_recipient_list_with_one_address_the_store_cannot_hold_is_refused_whole`, which asserts
      nothing written and nothing removed.
  - **Mutations,** each verified to have changed the file and been restored byte-identical:
    - check disabled (`.Any(row => false)`): exactly 4 fail. These are the controller limit + 1,
      the list with one long address, and both kinds of `The_limit_is_the_boundary…`, each with
      "Expected BadRequestObjectResult / Actual OkResult" or "Assert.False() Failure".
    - whole-string measurement, the rejected design: exactly 1 fails,
      `A_recipient_list_longer_than_the_store_limit_in_total_is_still_stored`, with "Expected
      OkResult / Actual BadRequestObjectResult".
    - constant raised to 2050: see 1.1.
  - **The remarks on `The_screen_accepts_a_policy_link…`** said "no layer checks length on write".
    That is now false, and it has been rewritten to point at the new tests.

- [x] 1.4 (Added in QA round 1.) The privacy-notice MODIFIED requirement, *A usable policy link is
  defined once, and means the same on every host*: diff its guarantees against
  `openspec/specs/privacy-notice/spec.md`, not its prose.

  | Guarantee as it stands | Decision |
  |---|---|
  | SHALL: usable = trimmed, non-blank, no control character or backslash, and site-relative (one `/`) or absolute http(s) | carried word for word |
  | SHALL: every other value is unusable and treated as absent | carried word for word |
  | SHALL NOT depend on the host OS; a `/` value SHALL be judged site-relative, never as an absolute URI | carried word for word |
  | SHALL accept exactly when the site would use it, within the store's capacity; validation and resolution stay separate and SHALL apply the same definition | carried word for word |
  | SHALL: "The screen's refusal SHALL name both accepted forms" | **narrowed, deliberately**: "When the screen refuses a value within the store's capacity". An over-long link is refused for length and states the limit. Stated in the proposal |
  | Italic note: over-long is a "known gap, not yet addressed", and the write "fails at the database" | **superseded**: it now points at the `site-settings` refusal. It is informative, not a SHALL |
  | 7 scenarios: Linux `/privacy`; Windows `/privacy`; absolute http(s); refusals unchanged; screen accepts site-relative; screen and site agree within capacity | 6 carried word for word |
  | Scenario: *The screen's refusal names both forms* | carried, with its WHEN scoped "within the settings store's capacity", to match the narrowed SHALL |

  Verify: `ChangeDeltaIntegrityTests` passes, and the existing tests on this requirement pass
  unchanged: `The_policy_link_refusal_names_both_accepted_forms` uses `privacy`, which is within
  capacity.

## 2. The bound guard's message

- [x] 2.1 In `PackageCompositionTests`, choose the ceiling message by comparing versions (design
  D3), with detection unchanged: lower, higher and unparseable each get their own text. Verify with
  one test case per branch through the helper. Each asserts its own text and the absence of the
  other two. Mutation: swap two branches, and confirm that exactly the affected cases fail.
  *Done:* `CeilingFault(high, major)`. The detection line `high != expectedCeiling` is unchanged.
  - **Found during apply:** a fourth case, the right bound spelled differently (`18.0`,
    `18.0.0.0`). Detection flags it, and a lower/higher message would be false for it, so it gets
    its own text.
  - A prerelease of the ceiling (`18.0.0-rc`) sorts below it and is reported as too low.
  - `A_wrong_ceiling_is_explained_in_the_direction_it_is_wrong` has 7 cases: 3 below, 2 above,
    1 spelling and 1 unparseable. Each asserts its own text and the absence of the other three.
  - **Mutation** (`>` → `<`): exactly the 4 strictly ordered cases fail (`17.7.0`, `16.0.0`,
    `18.0.1`, `19.0.0`). `18.0.0-rc`, `18.0` and `eighteen` are correctly unaffected, because
    they don't reach that comparison as unequal.

## 3. The runbook's Marketplace sentences

- [x] 3.1 Rewrite the *Status* bullet (currently "The Umbraco Marketplace still lists three
  libraries … has not yet been seen") and the *After the push* sentence ("has **not been
  observed**") as observed on 2026-09-25: only `UBookIt` is listed, showing the 18.x line and
  supporting 17 and 18. Keep the instruction to check the live page after a release. Verify:
  `VersionTruthTests` and every documentation test pass, and `git diff` shows no pinned phrase
  removed without its pin being judged.
  *Done:*
  - **The Status bullet was removed, not rewritten.** It sat under "What is still outstanding",
    and the observation means it's no longer outstanding. Rewriting it there would have listed
    something finished as open.
  - The observation is recorded where the procedure lives. The *After the push* paragraph now says
    the Marketplace dropped the libraries, seen on 2026-09-25, and keeps "still check … after a
    release that changes the tag", because one observation is not a promise.
  - No pinned phrase was in either sentence. Documentation guards: 122/122.
- [x] 3.2 Re-read "until/up to `17.2.1`/`18.1.1`" (`publishing.md` ~181 and ~524) against the feed:
  both versions are live. Record whether they stand. The expectation is that they do.
  *Done:* the flat container for `ubookit` lists `17.2.1` and `18.1.1` (2026-09-26). All three
  sentences stand, unedited: "the procedure for every release up to", "every release up to …
  followed", and "Until … every package carried it".

## 3R. The README rewrite (design D7)

- [x] 3R.1 List every guard that reads `README.md`, from the test code, not grep counts (D7.3):
  each `Says`/`SaysOnce`/`DoesNotSay` phrase, version anchor, link, image and publisher guard,
  plus the shipped-markdown sweeps. Record the list in `README-inventory.md`.
  *Done:* `README-inventory.md` §1 lists 14 guards.
  - **Re-judged and kept deliberately:** "uBookIt makes no DDoS-protection claim". Its test
    records it as Chris's concern, not QA voice.
  - **Found:** neither link guard checks a `tree/<ref>/…` link, so a directory link pinned to
    `main` would pass. It is recorded as a candidate obligation. The rewrite uses none.
- [x] 3R.2 Inventory every actionable claim in the current README (D7.1). Mark each one kept,
  moved (name the docs page, and quote the line that already says it), or dropped (with a reason).
  Verify: every D7.2 item is marked *kept*.
  *Done:* 31 claims, with sub-items: all kept, apart from 6 moved, each quoting the docs line that
  already carries it, and 2 dropped as duplicated illustration. Every D7.2 item is marked *kept*.
- [x] 3R.3 Write the new README. **Do 4.1 (the bump) first**, so the rewrite is pinned to `17.2.2`
  from the start and the version guards check it as written. Verify:
  - the full unit suite passes;
  - no pin was deleted: each changed guard is listed in the inventory with its reason;
  - every link and image is absolute, `https`, and pinned.

  *Done:*
  - **Sibling sweep while writing:** three things the rewrite falsified were found and fixed.
    - `publishing.md` step 3's table said the version sentence is "near the top". It is now named
      as *Versions and the API promise*.
    - The same table put every screenshot under *What it looks like*. It is now "the opening and
      *What it looks like*".
    - `CHANGELOG.md`'s header linked `README.md#what-the-version-number-means`, a heading that no
      longer exists. It now links `#versions-and-the-api-promise`. That is the header paragraph,
      not a released entry.
  - Documentation guards 122/122 on the final text. The full-suite totals are in 5.2.
  - Links (after round 1, counted with `grep -o`, not estimated): 16 into the repository, 13
    distinct addresses, all `blob/17.2.2/…` and resolving, now
    including `openspec/specs/site-settings/spec.md` and `.github/workflows/ci.yml`; 4 images on
    `raw.githubusercontent.com/…/17.2.2/`; 2 external (the publisher site, and OpenSpec, which
    returned 200).
- [x] 3R.4 Check every sentence of "How it's built" against the repository, and record the evidence
  for each in the inventory: the specs path, the CI workflow's fail-on-skip, the `release`
  approval, and the QA-in-a-subagent rule in `CLAUDE.md`. No history, counts or model names.
  *Done:* `README-inventory.md` §3 has one row per sentence. "Every link into the repository" is
  deliberately scoped, because external links are unchecked. The link to `CLAUDE.md` says it is
  "the same file the AI agents working on it read", which is how Claude Code loads it.
- [x] 3R.5 Render check. View the README as GitHub renders it on the branch, and read it top to
  bottom as a stranger. The first screen should show what it is, who it's for, a screenshot and
  the install line. Record anything that reads wrong. The Marketplace render is checked after
  `18.1.2`.
  *Partly done:* read top to bottom as text. Two fixes came out of it: a doubled bold on the API
  line, and "all four of these are off" being false for the Settings screen, which is locked
  rather than off; it now reads "off, or granted to nobody". **The GitHub render needs the
  branch pushed.** It is open until Chris pushes.
  *Done after the push:* GitHub's own renderer (`/readme?ref=release-17-2-2`, HTML) gives:
  - 13 headings in the intended order;
  - 4 images, 35 links, and no stray emphasis markers in the rendered text;
  - the opening reads as intended.

  The images and repository links point at tag `17.2.2`, which doesn't exist until 7.2, so they
  are broken on the branch view by design. 7.3's sweep covers them after tagging.

## 3S. The version and the entry

- [x] 4.1 Bump `<Version>` to `17.2.2` and move every row of `docs/publishing.md` step 3's table.
  Re-read the *Tag the release* literals by eye. Verify that `git grep 17.2.1` over README and the
  runbook leaves only history anchors, each named here.
  *Done:* `<Version>` is `17.2.2`, and `git grep "<Version>"` finds only `Directory.Build.props`.
  - The runbook's *is at* sentence (line 14), the example URL, and the `git tag` / `git push` /
    `curl` lines were re-read by eye.
  - The README carries `17.2.2` throughout (task 3R.3).
  - `git grep 17.2.1` over README and runbook leaves only the three history sentences named in 3.2.
- [x] 4.2 Write the `17.2.2` entry, undated. It opens with *What you have to do: nothing*. It states
  the setting-length refusal as a narrowing: a value that used to fail at the database is now
  refused with a message, and nothing stored is affected. It also names this as the first release
  published by the workflow, and says the README has been rewritten with no change to what it
  promises. **Check every claim against the code, not against this list.**
  Verify: `ChangelogTests` passes.
  *Done, with one deliberate deviation.* **The entry does NOT say this is the first release
  published by the workflow.** It is written before the release, and the changelog at the tag is
  frozen: the README links to it. If D5's fallback is used, the sentence would be false in a
  document that can't be corrected. It becomes a close-out record (8.2) instead.
  - Each claim was checked:
    - the 2048 column;
    - "failed at the database instead of being refused". This is from the integration test's
      truncation failure. Earlier wording claimed a "server error", which is not measured, so it
      was removed;
    - `SettingValidation.IsValid` is stricter with the same signature;
    - per-address for the recipient list (`SettingText.RowsFor`);
    - "no value you have stored is affected", because none can exceed the column.
  - README: "Nothing it promised has been withdrawn", which is the inventory's claim, for QA to
    verify.
  - `ChangelogTests` passed within the 122.

## 5. Clean build and suites

- [x] 5.1 Stop the TestSite. Delete `src/*/bin/Release`, the backoffice `App_Plugins` output and
  `obj/Release`. Build the client first, then `CI=true dotnet build UBookIt.slnx -c Release
  --no-incremental`. Verify: 0 warnings.
  *Done:* no TestSite was running. The outputs were deleted, the client built, then a `CI=true
  --no-incremental` build: **0 warnings, 0 errors**, first attempt. This was after every mutation
  had been restored.
- [x] 5.2 Run the suites per project, sequentially. Verify:
  - unit **1987 + the new tests**, integration **191 + the new tests**, rendering 1168 and client
    335, each with 0 skipped; state the exact new totals;
  - `openspec validate --all --strict` passes.
  *Done*, all with 0 skipped:
  - unit **2001** (1987 + 14: 7 settings, 7 ceiling);
  - integration **194** (191 + 3);
  - rendering **1168**;
  - client **335** (15 files).

  `openspec validate --all --strict`: **27/27**.

## 6. Review and merge

- [x] 6.1 QA review (`qa-review`) in one subagent, reused across rounds. Hand each round's fixes
  over as new code, and give it every claim as something to verify.

  *Round 1: REJECT* on `fb9ce74`. QA confirmed all build, suite and validate claims by running
  them.
  - **MAJOR:** `openspec/specs/privacy-notice/spec.md` still called the over-long write a "known
    gap, not yet addressed". My sibling sweep had not looked in `openspec/specs/**`. *Fix:* a
    MODIFIED delta with every guarantee diffed. One is deliberately narrowed: "the refusal names
    both forms" now applies within the store's capacity, because an over-long link is refused for
    its length. That narrowing was found while writing the fix; QA had not raised it. It is stated
    in the proposal.
  - **MAJOR:** README "The run fails if any test suite didn't report or any test was skipped" is
    false for the client suite, where vitest fails only on zero files. *Fix:* scoped to ".NET test
    project".
  - **MAJOR:** README "Gated releases", and the inventory's evidence for it: the documented
    fallback pushes a local pack, not the run's artifact. *Fix:* the sentence names the manual
    fallback, and the inventory's false evidence is corrected in place, with the correction stated.
  - **MINORs, all taken:**
    - "every commit" becomes "every push";
    - "design" is dropped from the universal claim, because `release-17-2-0` has none;
    - links added to a spec (`site-settings`) and to `ci.yml`, as D7.4 said;
    - the template-commit pointer restored;
    - "additions are preferred to changes" restored, now inventory 9a;
    - ceiling messages: a **prerelease** branch (`18.0.0-rc` refuses no 17 release, it admits
      earlier 18 prereleases), and four-part comparison (`18.0.0.1` is above, not "spelled
      differently"). The D3 deviation from `NuGetVersion` is recorded;
    - D5 now states the exact artifact-download and push commands. **Found while writing them:**
      my first glob `UBookIt.*.nupkg` also matched the meta-package, which would have pushed it
      among the libraries. That is corrected before this round.
  - **NITs, all taken:**
    - the test comment's unmeasured "500";
    - the second screenshot's alt text no longer says "same";
    - D6's item number;
    - 7.3 scoped "pinned" to repository links;
    - the `Text` case uses plain letters.
  - **Also added:** `docs/configuration.md` gets one sentence saying the screen refuses values
    over 2048, per address for the recipients list. It is where validation is described, and it
    said nothing about length.
  - **Not changed:** `publishing.md:76/97` ("every commit pushed"). That is inherited wording in
    the runbook, and QA raised it only as the README's source. It is left for QA to rule on.
  - **Round-2 state** (after a clean `--no-incremental` build, 0 warnings), all 0 skipped:
    - unit **2005** (the ceiling theory went from 7 to 11 cases: `17.7.0-rc`, `18.0.0.1`,
      `18.0.0.0` and `18.x` added, and `18.0.0-rc` moved to *prerelease*);
    - integration **194**, rendering **1168**, client **335**;
    - openspec **27/27**.

    The first run failed `ChangeDeltaIntegrityTests.Every_modified_requirement_is_named_in_its_change_tasks`,
    because the new MODIFIED delta was not named in these tasks. The guard did its job, and task
    1.4 now carries the name and the guarantee diff. **Mutation** (prerelease branch disabled):
    exactly the `18.0.0-rc` case fails.

  *Round 2: APPROVE WITH NITS* on `ee4c184`. There is nothing to fix before merge.
  - QA re-ran every claim.
  - It diffed the privacy-notice delta against the main spec itself and found exactly three
    hunks.
  - **It ruled the narrowing right**: naming URL forms for an over-long value would point the
    editor at the wrong fix.
  - It checked the D5 command block end to end.

  NITs:
  - **Taken:**
    - the nested italic in the delta's note, now quotation marks;
    - a 170-character comment line, re-wrapped;
    - `publishing.md:76/97` "every commit pushed" changed to "every push". QA ruled it optional,
      and it costs one word each.
  - **Noted, not changed:** `docs/configuration.md`'s literal "2048" is tied to the constant by
    no guard. It is accurate today, and it only matters if the constant changes, which would need
    a migration and a change of its own.

  After the NITs: a clean build with 0 warnings, unit **2005**, openspec **27/27**. The other
  suites are unaffected: only a C# comment, a spec note and runbook prose changed.
- [x] 6.2 Chris pushes the branch and opens the PR into `main`. Verify, through the API, that the
  PR's CI run is green at every step, parity included.
  *Done:* PR **#9**, head `56f1837` = local `HEAD`. Run `36237053839` (pull_request) was success
  at every step, **parity included**, read through the REST API. The branch's own push run
  `36236259487` was also success.
  - **Found:** `gh` is not installed here. D5's fallback depended on `gh run download`, and it now
    uses the run page's artifact download. It is a plan-only fix to a path that runs only on
    failure. It was told to QA.
- [x] 6.3 Chris merges with "Create a merge commit". Fetch. Verify that `origin/main` = local
  `main` = the merge commit, and that **the merge commit's own `ci` push run** is green at every
  step.
  *Done:* merged as **`5ced980`**, with parents `d6f0199` and `56f1837`. After a fetch,
  `origin/main` = local `main` = `5ced980`, and `git branch -r --contains` lists `origin/main`.
  Run `36237443425` (push, `main`, head `5ced980`) was success at every step, **parity
  included**, read through the REST API. The commit declares `<Version>17.2.2</Version>`, and tag
  `17.2.2` does not exist on origin yet.

## 7. Release through the workflow (design D4, D5)

- [x] 7.1 **Before tagging:** confirm the fallback API key has not expired (D5's fallback depends on
  it).
  *Done:* Chris confirmed on 2026-09-26 that the key was created last week and is well within its
  30 days.
- [x] 7.2 Chris pushes tag `17.2.2` on the merge commit. Verify that `git ls-remote` shows it, and
  that a `publish` run started for it, with `check` and `pack` green.
- [x] 7.3 **Before approving:**
  - the tagged `booking-flow.png` `curl` returns 200;
  - from the run's `packages` artifact, read **all five** nuspecs against D4's list, and all four
    `.snupkg` are present;
  - from the README extracted from a `.nupkg`: every address returns 200, every **repository**
    address is pinned to `17.2.2` (the two external links can't be), and
    it says "uBookIt is at `17.2.2`".

  If anything is wrong, reject and follow D5. Record which path was taken.
  *7.2 done:* `git ls-remote` shows `5ced980… refs/tags/17.2.2`. Publish run **`36237696936`**:
  `check` was success at every step, including "The tag names a verified commit on its line, at
  its declared version". `pack` was success at every step, including "The packed release is
  exactly what should be published". `publish` is *waiting*. The artifact `packages` is 844,205
  bytes and expires 2026-10-03T11:06:16Z.

  *7.3 done, nothing wrong, so no D5 path:*
  - `booking-flow.png` at the tag returns 200.
  - **The artifact**, downloaded by Chris to `D:\Downloads\uBookIt\17.2.2`: exactly 5 `.nupkg`
    and 4 `.snupkg`, all `17.2.2`, and nothing else.
  - **All five nuspecs were read.** In every one:
    - authors `Norwood Design & Development Ltd.`;
    - copyright `Copyright © Norwood Design & Development Ltd.`;
    - licence `MIT` (expression);
    - `README.md` and `icon.png` declared and present in the zip;
    - project URL `https://github.com/Chris-N2/UBookIt`;
    - repository commit `5ced980…`, the tagged merge commit.
  - **Dependencies:**
    - `UBookIt` → Backoffice and Web `17.2.2`;
    - Backoffice → Core and Persistence `17.2.2`, plus four `Umbraco.Cms.*` at `[17.6.2, 18.0.0)`;
    - Persistence → Core `17.2.2`, `Umbraco.Cms.Persistence.EFCore` `[17.6.2, 18.0.0)`, and
      `Microsoft.EntityFrameworkCore.SqlServer` `10.0.10`;
    - Web → Core `17.2.2`, plus two `Umbraco.Cms.*` at `[17.6.2, 18.0.0)`;
    - Core → none.
  - `umbraco-marketplace` is on `UBookIt` only. Its description says "A booking system for
    Umbraco 17 … Requires Umbraco 17 and SQL Server."
  - **Symbols:** each of the 4 `.snupkg` holds its own `lib/net10.0/<id>.pdb`.
  - **The README packed in all five is byte-identical to `README.md` at tag `17.2.2`** (SHA-256
    `7C68…C04D`). Every address in it, 19 distinct, returns 200 on the first attempt. The 13
    repository addresses and 4 images are pinned to `17.2.2`. It says "uBookIt is at `17.2.2`",
    with no `17.2.1`. The `#accessibility-…` fragment matches `docs/booking-page.md:294` at the
    tag.
- [x] 7.4 Chris approves in `release`. Record:
  - the run's summary table verbatim;
  - whether any `.snupkg` answered 409, and **the exact CLI text**, including whether the classifier
    recognised it;
  - the run's conclusion.

  If the run failed, record the log excerpt and the D5 path taken.
  *Done.* The run concluded **success**; no D5 path was needed. The summary table, verbatim as
  Chris copied it from the run page (the API does not expose it unauthenticated):

  | File | Result |
  |---|---|
  | UBookIt.Backoffice.17.2.2.nupkg | published |
  | UBookIt.Backoffice.17.2.2.snupkg | symbols submitted |
  | UBookIt.Core.17.2.2.nupkg | published |
  | UBookIt.Core.17.2.2.snupkg | symbols submitted |
  | UBookIt.Persistence.17.2.2.nupkg | published |
  | UBookIt.Persistence.17.2.2.snupkg | symbols submitted |
  | UBookIt.Web.17.2.2.nupkg | published |
  | UBookIt.Web.17.2.2.snupkg | symbols submitted |
  | UBookIt.17.2.2.nupkg | published |

  Libraries went first and the meta-package last, as designed. **No `.snupkg` got a 409**, so
  nuget.org's CLI wording for a still-pending symbol package **was not observed**. That
  obligation from `trusted-publishing` stays open for a later release, most likely a re-run or
  `18.1.2`. It is not discharged here.
- [x] 7.5 Confirm `17.2.2` per package on `api.nuget.org/v3-flatcontainer/<id>/index.json`, and that
  the live package page renders its readme, images and links.
  *Done:* **the first release published by `publish.yml`.**
  - Chris approved at about 11:16Z. The `publish` job (`108392617206`) ran 11:16:18–11:16:34Z,
    and was success at every step, including "Log in to nuget.org (OIDC -> one-hour API key)"
    and "Push each package, then its symbols, and say which were new". So the token exchange, the
    policy's owner, scope and glob, and approval in `release` all worked on a real release.
  - **The flat container, per package**, polled every 30 s. All times are UTC; the poller printed
    local BST, which is UTC+1, and those were converted: 1/5 (`web`) at 11:19:46Z, 3/5 at
    11:20:48Z (`core`, `ubookit`), and **5/5 at 11:21:50Z** (`persistence`, `backoffice`). That is
    **about 5 minutes after the push**. nuget.org's own `Last-Modified` on `ubookit/index.json` is
    11:20:02Z, which agrees. (Chris read the mixed-zone summary in chat as an hour and 5 minutes;
    the zones were unlabelled there. The record is now in one zone.)
  - `nuget.org/packages/UBookIt/17.2.2` returns 200. Its rendered readme has "How it's built",
    the 4 images and 13 repository doc links, all at `17.2.2`, none at `17.2.1`. It says "uBookIt
    is at 17.2.2", and no rendering warning is shown.
  - The live `ubookit.nuspec` has version `17.2.2`, `umbraco-marketplace`, "for Umbraco 17", and
    commit `5ced980`.

## 8. Close out, in this order

- [x] 8.1 Stamp the entry's date.
  *Done:* `## 17.2.2 — 2026-09-26`.
- [x] 8.2 Rewrite the *Status* bullet "No release has gone through the publishing workflow yet" to
  what is now true: `17.2.2` went through it, `18.x` has not yet, and the fallback stays until it
  has. Verify: the documentation guards pass.
  *Done:* the bullet is now "Only the 17 line has released through the publishing workflow". It
  says:
  - `17.2.2` was first, on 2026-09-26, with the token exchange, policy and approval all working;
  - no `18.x` release has used it yet, and the fallback stays until one has;
  - **the pending-symbols 409 wording has still not been seen**, and the step fails closed if it
    differs.

  It stays under *outstanding*, which lists the 18 line's first release, not Trusted Publishing
  setup. So `release-publishing`'s "The status section SHALL no longer list Trusted Publishing as
  outstanding" still holds. Guards: see 8.5.
- [x] 8.3 Sibling sweep for sentences this release falsifies: `docs/`, `README.md`,
  `openspec/specs/**`, `CLAUDE.md`. Search for `17.2.1`, `2048`, `Marketplace`, `fallback`,
  `publishing workflow` and `not yet`. Fix or record each.
  *Done:*
  - `17.2.1`: only history remains. That is `CHANGELOG.md`'s entry and `publishing.md` :177,
    :424, :520 and :523, all still true. The versions in `release-publishing`'s scenarios are
    worked examples.
  - `2048`: `CHANGELOG.md`, `docs/configuration.md`, the constant, and generated migrations. The
    privacy-notice main spec's stale note is superseded by this change's delta at archive.
  - `not yet`: only `publishing.md:80`, rewritten in 8.2. The other hits are about bookings.
  - `Marketplace`: `packaging` says the docs "SHALL NOT claim that an earlier library listing
    disappears until that has been observed on the Marketplace". It has been observed, by Chris on
    2026-09-25, so the runbook's new claim is permitted and the spec stays true. **Nothing
    falsified.**
  - `fallback` / `publishing workflow`: README :140 and the runbook are consistent with a
    fallback that still exists. `release-publishing`'s runbook requirement still holds.
- [x] 8.4 Record what `release-18-1-2` must carry: the D1 constant and check, with both tests; the
  D3 message; the README rewrite and its inventory, ported with the line's version, title line and
  Requirements row, and diffed against `main`'s README so those are the only differences; the runbook sentences (3.1 and 8.2, with the 18 half becoming true); its own version
  moves and entry; and what 7.4 observed. Also record the remaining obligation: remove the fallback
  after `18.1.2`.
  *Done.* **`release-18-1-2` must carry**, on `dev/v18` through its own PR, cherry-picked rather
  than merged:
  1. **The setting limit:**
     - `SettingRow.MaxValueLength`, `HasMaxLength` reading it, and `SettingValidation`'s
       per-row check through `SettingText.RowsFor`;
     - the 6 unit tests in `SettingsControllerTests` and the 3 integration cases in
       `SettingsStoreTests`;
     - the rewritten remark on `The_screen_accepts_a_policy_link…`;
     - the `site-settings` ADDED and `privacy-notice` MODIFIED deltas. **Check that v18's
       `privacy-notice` main spec is byte-identical to main's first. If not, re-diff the
       guarantees on that line.**
     - its own task naming the MODIFIED requirement, or `ChangeDeltaIntegrityTests` fails.
  2. **The ceiling message:** `CeilingFault` and its 11-case theory. On v18, major is 18, so the
     expected ceiling is `19.0.0`. The theory's literals are written for 17 (`CeilingFault(high,
     17)`), so they carry over unchanged as a test of the helper, not of the line.
  3. **The README rewrite and `README-inventory.md`**, with the title line "Bookings for Umbraco
     18", the v18 Requirements row, and every pin at `18.1.2`. Then `diff` against main's README:
     the only differences allowed are the version, "Umbraco 17/18" in the opening, and the
     Requirements row. The "Get started" line "Umbraco 17.6.2 or later" becomes v18's floor,
     18.2.0. **This README is the Marketplace listing: read it on the Marketplace after the
     rescan.**
  4. **The runbook:** the Marketplace paragraph (3.1), the step-3 table rows, "every push"
     (76/97), and the Status bullet (8.2). There it becomes true that both lines have released
     through the workflow, so rewrite it again and remove it from *outstanding* if nothing is
     left.
  5. `docs/configuration.md`'s limit sentence; the `CHANGELOG.md` header anchor.
  6. **Its own** `<Version>` 18.1.2, the pins, and an entry. The entry says the same as
     17.2.2's, for the 18 line.
  7. **What 7.4 observed:** OIDC publish works; libraries go first; the summary table must be
     pasted by Chris, because the API needs auth; `gh` is absent here, so the artifact comes from
     the run page. The 409 wording is **still unobserved**.

  **Obligations that remain after both lines release:** remove the manual API-key fallback from
  `docs/publishing.md` (its own change); the unobserved 409 wording; the `tree/` link blind spot
  (inventory §1); the client suite's skip detection (QA round 1, MAJOR 2's root).
- [x] 8.5 Commit on `main`, run the unit suite locally, and Chris pushes. Verify: `main`'s CI run is
  green at every step.
  *Done:* **`39610f0`** was committed after unit 2005/2005 and openspec 27/27 ran on it locally.
  `--no-build` was valid, because only markdown changed since the clean build of the same code.
  Chris pushed it. Run `36239391216` (push, `main`, head `39610f0`) was success at every step,
  with no step failed or skipped, read through the REST API.
- [x] 8.6 Archive **last**, running the unit suite locally before the push. Verify: the archive
  push's own CI run is checked and reported in the session.
  *Done:* archived with this box ticked. The deltas are synced into `site-settings` (ADDED) and
  `privacy-notice` (MODIFIED, guarantees diffed in 1.4). Before the push, the unit suite ran
  locally on the archive commit; the result is in that commit's message. The push's CI run is
  checked in the session, because this record is frozen by then.
