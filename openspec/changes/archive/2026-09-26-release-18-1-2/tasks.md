## 0. Facts checked at propose time

- [x] 0.1 The branch, the base and the baseline.
  *Done:* branch `release-18-1-2` from `origin/dev/v18` = `14cf33f`, version `18.1.1`, with
  `publish.yml` present. The first build after the line switch had 0 warnings and 0 errors, first
  attempt. Unit **1999/1999**, 0 skipped. `main`'s `17.2.2` changes added 18 unit tests, so the
  expected total is **2017**.
- [x] 0.2 The ported files are identical between the lines at their bases.
  *Done:* `git diff d6f0199 14cf33f` is empty for:
  - `SettingValidation.cs`, `SettingText.cs`, `Rows.cs`, `UBookItDbContext.cs`;
  - `SettingsStoreTests.cs`, `SettingsControllerTests.cs`, `PackageCompositionTests.cs`;
  - `docs/configuration.md`;
  - `openspec/specs/privacy-notice/spec.md`, `openspec/specs/site-settings/spec.md`.

  README, runbook and changelog differ, by line facts only (design D2–D4). The two delta specs here
  are `main`'s, and `diff --strip-trailing-cr` shows them identical. The v18 Umbraco range is
  `[18.2.0,19.0.0)` (`Directory.Packages.props`).

## 1. Code, tests and specs (design D1)

- [x] 1.1 Apply `git diff d6f0199 56f1837 -- src tests docs/configuration.md` with `--3way`.
  Verify:
  - no conflicts;
  - `git diff 56f1837 -- src tests docs/configuration.md` on this branch is **empty**.

  *Done:* all 7 files applied cleanly, with no conflicts. **The check as written was mis-scoped,
  and is corrected here.** Over the whole of `src tests`, the diff against `56f1837` is not empty
  and must not be: the v18 port has its own differences in 23 unrelated files, per `--stat`, such as the OpenAPI
  generator and `VersionTruthTests`. The right check is over **the 7 files `main` changed in that
  range**, and there it is **0 lines**: each is byte-identical to `main`'s.
- [x] 1.2 Verify: the unit suite passes at **2017**, 0 skipped. The integration suite passes at
  **194**, which is 191 + 3.
  *Done (in 5.2):* unit **2017**, integration **194**, exactly as predicted.
- [x] 1.3 The privacy-notice MODIFIED requirement, *A usable policy link is defined once, and means
  the same on every host*: its guarantees are diffed against `openspec/specs/privacy-notice/spec.md`
  **on this line**. That file is byte-identical to `main`'s at the base (0.2), so `release-17-2-2`
  task 1.4's table holds:
  - every SHALL carried word for word, except "the refusal SHALL name both accepted forms", which
    is **deliberately narrowed** to a value within the store's capacity, because an over-long link
    is refused for length;
  - six scenarios carried word for word, and *The screen's refusal names both forms* scoped the
    same way;
  - the italic "known gap" note superseded.

  Verify: diff the delta against this line's main spec. Exactly those three hunks differ, and
  `ChangeDeltaIntegrityTests` passes.
  *Done:* the requirement block, diffed against this line's `openspec/specs/privacy-notice/spec.md`,
  shows **exactly three hunks**: line 20 (the narrowed SHALL), lines 22–26 (the note) and line 57
  (the scoped scenario WHEN). `ChangeDeltaIntegrityTests` is in 5.2.

## 2. The README (design D2)

- [x] 2.1 Build the README from `main`'s at `56f1837`, with the four substitutions. Verify:
  - `diff` against `main`'s README shows only lines of the four kinds, each listed here;
  - the Requirements row is word for word `dev/v18`'s current row;
  - the documentation guards pass.

  *Done:* `main`'s README at `56f1837`, with the substitutions done by a script that asserted each
  one's count: the opening (1), *Get started* (1), the Requirements row (1), and **21 pins**, which
  are 16 links, 4 images and the *is at* sentence. `diff` against `main`'s shows **only** those
  lines: the opening, *Get started*, the row, and 21 pin lines, the four image lines included. The
  row is `dev/v18`'s, read from its README before the replacement. Guards: see 5.2.

## 3. The runbook and the changelog (design D3, D4)

- [x] 3.1 Apply `main`'s `d6f0199..56f1837 -- docs/publishing.md` hunks with `--3way`, then set the
  version literals to `18.1.2`: the *is at* sentence and the *Tag the release* URL and commands.
  Verify:
  - each hunk landed, or is recorded if it did not;
  - `git grep 18.1.1 docs/publishing.md` leaves only history.

  *Done:* all 7 of `main`'s hunks landed. The Marketplace paragraph, the removed bullet, both
  table rows and "every push" (76/97) merged cleanly. **3 conflicted, all at version literals**
  (line 14, the example URL, and the tag commands). They were resolved to this line's side with
  `18.1.2`. No conflict markers remain. `18.1.1` is left only at the history sentences 177, 422,
  516 and 519. `dev/v18`'s own tag-section wording about two lines is kept.
- [x] 3.3 (Added in QA round 1, MAJOR.) Port `main`'s workflow Status bullet (`39610f0`) **before
  the tag**. The runbook is frozen at the tag, and the Marketplace README links to it, so "No
  release has gone through the publishing workflow yet", which has been false since `17.2.2`
  published, would stay false at that address for good.
  *Done:* the bullet was replaced by `main`'s text, extracted from `39610f0`. Lines 74–95 of the
  *outstanding* list are now identical to `main`'s `39610f0` (`diff --strip-trailing-cr`). No test
  or spec pins that bullet (QA grepped).
- [x] 3.2 CHANGELOG:
  - the header anchor becomes `#versions-and-the-api-promise`;
  - `main`'s `## 17.2.2 — 2026-09-26` entry is copied **unedited** above `## 17.2.1`;
  - a new `## 18.1.2` entry, undated.

  Verify: `ChangelogTests` pass, and the copied entry is byte-identical to `main`'s.
  *Done:* the anchor is fixed. `## 17.2.2 — 2026-09-26` sits between `18.0.0` and `17.2.1`, and
  `diff --strip-trailing-cr` against `main`'s entry is empty. The `## 18.1.2` entry is at the top,
  undated. It states the limit (per address for the list) and the README rewrite, which "on this
  line … is also the package's Umbraco Marketplace listing".

## 4. The version

- [x] 4.1 `<Version>` → `18.1.2`. Verify: it is the only declared version, and `git grep 18.1.1`
  over README and runbook leaves only history, each named here.
  *Done:* `Directory.Build.props:53` is the only `<Version>`: `18.1.2`. The README has none. The
  runbook has the four history sentences listed in 3.1.

## 5. Clean build and suites

- [x] 5.1 Clean build (client first, `CI=true --no-incremental`). Verify: 0 warnings.
  *Done:* the outputs were cleared, the client built, then `CI=true --no-incremental`: **0
  warnings, 0 errors**, first attempt.
- [x] 5.2 Per project: unit **2017**, integration **194**, rendering 1168, client 335, all 0
  skipped. `openspec validate --all --strict` passes.
  *Done*, all with 0 skipped: unit **2017**, integration **194**, rendering **1168**, client
  **335** (15 files). openspec **27/27**. That includes `ChangeDeltaIntegrityTests`,
  `VersionTruthTests`, `ChangelogTests` and the documentation guards.

## 6. Review and merge

- [x] 6.1 QA review in a subagent, reused across rounds. Fixes are handed back as new code, and
  claims are given to verify.

  *Round 1: REJECT* on `8c0a5cc`. QA verified all 8 claims independently, including the build and
  every suite.
  - **MAJOR:** the v18 runbook's "No release has gone through the publishing workflow yet" was
    false once `17.2.2` published. The tag freezes it, and the Marketplace README links there.
    *Fix:* task 3.3, with `main`'s `39610f0` wording ported before the tag.
  - **NIT, pre-existing:** v18's SourceLink section names `main`/`origin/main`. It is pinned by
    `VersionTruthTests.cs:983` and recorded in 8.4. QA ruled deferral acceptable.
  - **NIT:** the 18 README's only major-tracks example is `17.x`. Left as is, per the non-goal
    that the READMEs read the same.

  *Round 2: APPROVE WITH NITS* on `e1895c1`. The MAJOR was verified fixed: lines 74–95 are
  identical to `39610f0`, true at tag time, and consistent with `release-publishing`. There is
  nothing to fix before merge. The cosmetic NITs are 3.3 sitting before 3.2 and uneven wrapping in
  D3 and 8.2. The first is left, because renumbering would break references. My first citation
  of the pin as `:988` was the end of the call; it is corrected to `:983`.
- [x] 6.2 Chris pushes and opens a PR into **`dev/v18`**. Verify through the REST API: the PR run is
  green at every step, parity included.
  *Done:* PR **#10**, base `dev/v18`, head `76eeff5` = local `HEAD`. Run `36254397633`
  (pull_request) was success at all 16 steps, **parity included**, read through the REST API. The
  branch's push run `36254343543` was also success.
- [x] 6.3 Merged with a merge commit. Verify: `origin/dev/v18` = local = the merge commit, and the
  merge commit's own push run is green at every step.
  *Done:* merged as **`befc4c5`**, with parents `14cf33f` and `76eeff5`. After a fetch,
  `origin/dev/v18` = local `dev/v18` = `befc4c5`, and `git branch -r --contains` lists
  `origin/dev/v18`. Run `36254684346` (push, `dev/v18`, head `befc4c5`) was success at all 16
  steps. The commit declares `18.1.2`, and tag `18.1.2` did not yet exist on origin.

## 7. Release through the workflow (design D5)

- [x] 7.1 Chris pushes tag `18.1.2` on the merge commit. Verify: `check` and `pack` are green and
  `publish` is waiting.
- [x] 7.2 **Before approving:**
  - the tagged `booking-flow.png` returns 200;
  - the artifact is in a new, empty folder: exactly 5 `.nupkg` + 4 `.snupkg` at `18.1.2`;
  - all five nuspecs: publisher, copyright, licence, readme and icon present, commit = the merge
    commit, `Umbraco.Cms.*` at `[18.2.0, 19.0.0)`, siblings at `18.1.2`, the Marketplace tag on
    `UBookIt` alone, the description naming Umbraco 18;
  - the packed README is byte-identical to the tag's, and every address returns 200, with
    repository addresses pinned to `18.1.2`.

  *7.1 done:* `git ls-remote` shows `befc4c5… refs/tags/18.1.2`. Publish run **`36256190310`**:
  `check` and `pack` were success at every step, and `publish` is waiting. The artifact
  `packages` is 872,460 bytes and expires 2026-10-03T16:40:17Z.

  *7.2 done, nothing wrong:*
  - `booking-flow.png` at the tag returns 200.
  - **The artifact**, which Chris downloaded to `D:\Downloads\uBookIt\18.1.2`, a new folder: exactly
    5 `.nupkg` and 4 `.snupkg`, all `18.1.2`, and nothing else.
  - **All five nuspecs were read.** In every one:
    - authors and copyright `Norwood Design & Development Ltd.`;
    - licence MIT;
    - readme and icon declared and present;
    - project URL public;
    - commit **`befc4c5…`**.
  - **Every `Umbraco.Cms.*` is `[18.2.0, 19.0.0)`.**
  - **Every sibling is at `18.1.2`.**
  - `umbraco-marketplace` is on `UBookIt` only. Its description says "A booking system for
    **Umbraco 18** … Requires Umbraco 18 and SQL Server."
  - **The v18-only dependencies predate this change:** `Microsoft.AspNetCore.OpenApi` 10.0.11 on
    Backoffice and Web, and EF Core SqlServer 10.0.11. Both are at the same versions in `18.1.1`'s
    `Directory.Packages.props`, and both come from the Umbraco 18 port.
  - **Symbols:** each `.snupkg` holds its own `.pdb`.
  - **The packed README in all five is byte-identical to `README.md` at tag `18.1.2`** (SHA-256
    `B6B7…96F3`).
  - Every one of its 19 distinct addresses returns 200. All 16 repository links are
    `blob/18.1.2/`, and none name `17.2.2` or `18.1.1`. It says "uBookIt is at `18.1.2`". The
    fragment matches `booking-page.md:294` at the tag.
- [x] 7.3 Chris approves. Record the summary table as he pastes it, and the 409 wording if one
  occurs.
  *Done.* The `publish` job ran 16:46:04–16:46:20Z and was **success** at every step. The summary
  table, verbatim as Chris copied it:

  | File | Result |
  |---|---|
  | UBookIt.Backoffice.18.1.2.nupkg | published |
  | UBookIt.Backoffice.18.1.2.snupkg | symbols submitted |
  | UBookIt.Core.18.1.2.nupkg | published |
  | UBookIt.Core.18.1.2.snupkg | symbols submitted |
  | UBookIt.Persistence.18.1.2.nupkg | published |
  | UBookIt.Persistence.18.1.2.snupkg | symbols submitted |
  | UBookIt.Web.18.1.2.nupkg | published |
  | UBookIt.Web.18.1.2.snupkg | symbols submitted |
  | UBookIt.18.1.2.nupkg | published |

  **No `.snupkg` got a 409**, so the pending-symbols wording is **still unobserved**, now after
  both lines' first workflow releases.
- [x] 7.4 The flat container lists `18.1.2` per package, with times in **UTC**. The live page
  renders the new readme, and the live nuspec is right.
  *Done:*
  - Polled every 30 s, with the times printed by `date -u`:
    - 0/5 until 16:48:37Z;
    - 2/5 (`persistence`, `web`) at 16:49:09Z;
    - 3/5 (`ubookit`) at 16:50:11Z;
    - **5/5 at 16:51:12Z**, about 5 minutes after the job finished.
  - nuget.org's `Last-Modified` on `ubookit/index.json` is 16:50:04Z, which agrees.
  - `nuget.org/packages/UBookIt/18.1.2` returns 200. Its rendered readme has "Bookings for
    Umbraco 18", "How it's built", the 4 images and 13 repository links, all at `18.1.2`. It says
    "uBookIt is at 18.1.2", and no rendering warning is shown.
  - The live `ubookit.nuspec` has `18.1.2`, `umbraco-marketplace`, "for Umbraco 18", and commit
    `befc4c5`.

## 8. Close out, in this order

- [x] 8.1 Stamp the `18.1.2` entry's date.
  *Done:* `## 18.1.2 — 2026-09-26`.
- [x] 8.2 The runbook's workflow Status bullet, which is at `main`'s wording since 3.3: **both
  lines have now released through the workflow**. Rewrite it to say so, and name the fallback removal as the next change. Keep the
  unobserved 409 wording, if still unobserved.
  *Done:* the bullet now reads "The manual fallback is still documented, although both lines now
  release through the workflow". It says:
  - `17.2.2` and `18.1.2`, both 2026-09-26, worked on their first push;
  - removing *Fallback: pushing by hand* is the next change;
  - the 409 sentence stands unchanged, because the wording is still unobserved.

  It stays under *outstanding*, because what is outstanding is the fallback's removal, not
  Trusted Publishing setup, so `release-publishing` still holds.
- [x] 8.3 Sibling sweep on this line, over `docs/`, `README.md`, `openspec/specs/**` and
  `CLAUDE.md`: `18.1.1`, `2048`, `Marketplace`, `fallback`, `publishing workflow`, `not yet`.
  *Done:*
  - `18.1.1`: only the four history sentences (`publishing.md` 183, 428, 522, 525).
  - `not yet`: the runbook's "CI … does not yet gate" is true. The rest are about bookings and
    services, apart from `privacy-notice:288`, the "known gap" note, which this change's delta
    supersedes at archive.
  - `Marketplace`, `2048` and `fallback`: consistent, as on `main` (release-17-2-2 8.3). The
    packaging spec's "SHALL NOT claim … until … observed" is satisfied by the 2026-09-25
    observation.
  - **Nothing falsified on this line.**
- [x] 8.4 Record the obligations that remain on both lines:
  - fallback removal;
  - the 409 wording;
  - the `tree/` link blind spot;
  - client skip detection;
  - `main`'s runbook Status bullet, which still says only the 17 line has released;
  - **v18 runbook (QA round 1, NIT, pre-existing):** *The order that makes SourceLink correct*
    says "Pack from the merge commit on main" and "`# must list origin/main`". On this line it
    should name `dev/v18`. The sentence is pinned verbatim by `VersionTruthTests.cs:983`
    (`SaysOnce`), so fixing it means a line-specific guard change. It is left for its own change
    rather than taken into a patch unplanned. The fallback reader must substitute the line.

  *Recorded*, in order of urgency:
  1. **`main`'s runbook Status bullet**, which is false as of today: it says "No `18.x` release has
     used it yet". It is fixed on `main` in the same sitting, straight after this archive, with this
     line's 8.2 wording. **Do not leave it for the fallback-removal change.** A one-line-only fix
     is the recurring defect.
  2. **Fallback removal** on both lines, in its own change. Chris's key expires around
     mid-October.
  3. **The pending-symbols 409 wording**, unobserved after two releases.
  4. **v18 SourceLink wording** (above).
  5. **The `tree/` link blind spot** and **client-suite skip detection** (`release-17-2-2`).
- [x] 8.5 Commit on `dev/v18`, run the unit suite locally, and Chris pushes. Verify: CI is green at
  every step.
  *Done:* **`afcaa98`** was committed after unit 2017/2017 and openspec 27/27 ran on it locally.
  `--no-build` was valid, because only markdown changed since the clean build of the same code.
  Chris pushed it. Run `36257188937` (push, `dev/v18`, head `afcaa98`) was success at all 16 steps,
  read through the REST API.
- [x] 8.6 Archive **last**, running the unit suite locally before the push. The push's CI is checked
  in the session.
  *Done:* archived with this box ticked. The deltas are synced into `site-settings` (ADDED) and
  `privacy-notice` (MODIFIED, guarantees diffed in 1.3). The unit suite ran locally on the archive
  commit before the push; the result is in the commit message. The push's CI is checked in the
  session, because this record is frozen by then.
