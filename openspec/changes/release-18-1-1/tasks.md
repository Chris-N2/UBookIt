## 0. Facts checked at propose time

- [x] 0.1 The privacy fix is the same change on both lines, and the pre-fix code was identical at
  both tags.
  *Done:*
  - `git diff 327783e^ 327783e` and `9cd14ef^ 9cd14ef` give byte-identical change lines for
    `SettingCatalogue.cs`, `SettingValidation.cs` and `UBookItPersistenceComposer.cs`.
  - `git diff 17.2.0 18.1.0` over those files, `RunUBookItMigrations.cs` and
    `SettingsController.cs` is empty.
  - `git diff main release-18-1-1` over the three `src` files and `UBookIt.csproj` is empty.
  - `18.1.0:src/UBookIt/UBookIt.csproj` says "for Umbraco 17 … Requires Umbraco 17 (LTS)".

## 1. Turn `dev/v18` green

- [x] 1.1 By hand (D2), raise the `packaging/spec.md` / `nuget.org` count `6 → 8` and extend the
  reason to name the two synced sentences. The feed evidence is **re-fetched** for this record.
  Verify: the guard passes from a clean build, and a ninth mention fails it (then revert).

  *Done:* count `6 → 8`, with the reason extended by hand in this entry's form.
  - Feed evidence **re-fetched** on 2026-09-25: the three libraries list `17.0.0`–`17.2.1`,
    `18.0.0` and `18.1.0`. Their `17.2.0`/`18.1.0` nuspecs carry the tag and their `17.2.1`
    nuspecs do not (new since `main`'s record).
  - From a clean dev/v18 build the guard passes. A ninth mention failed it, naming
    `packaging/spec.md`. Restored.
## 2. The version

- [x] 2.1 Change `<Version>` to `18.1.1`. It is the only declared version.
  *Done:* the only `<Version>` is `Directory.Build.props:53` `18.1.1`.
- [x] 2.2 Move the README (the *is at* sentence and the 16 pins) and runbook lines 14, 266 and
  292–294. Re-read the tag literals by eye.
  *Done:* README 17 literals (the sentence plus 16 pins), and runbook 14, 266 and 292–294, read
  before and after. `git diff --stat`: props 1, README 17, runbook 5.
- [x] 2.3 Verify that only the history anchors still say `18.1.0` (runbook 401,
  `packaging/spec.md:1022`) and that no anchor moved.

  *Done:* only the history anchors still say `18.1.0`: `publishing.md:401` and
  `packaging/spec.md:1022`.
## 3. The changelog

- [x] 3.1 Copy `main`'s dated `## 17.2.1 — 2026-09-25` entry, unedited, between `18.0.0` and
  `17.2.0`. Verify that a diff of the entry text between `main` and this branch is empty (D3).
  *Done:* copied between `18.0.0` and `17.2.0`. A diff of the entry (CR-stripped) against
  `main:CHANGELOG.md` is **empty**.
- [x] 3.2 Write the `18.1.1` entry, undated.
  - *What you have to do*: **nothing**.
  - The privacy paragraphs reuse `17.2.1`'s final wording, where 0.1 shows the facts are the same.
  - The Marketplace paragraph says that `18.0.0`/`18.1.0` described themselves as for Umbraco 17,
    and that `18.1.1` says Umbraco 18.

  Check every sentence against `18.1.0` vs HEAD on this line.
  *Done:* the privacy and settings paragraphs are `17.2.1`'s final text (0.1 shows the facts are
  the same). New in this entry:
  - A "same changes as `17.2.1`" lead.
  - A description paragraph, re-read as claims before any test:
    - I **dropped** "no package manager would have installed them on 17" (unmeasured: it
      depends on how a site references Umbraco).
    - I dropped "the two lines cannot swap it again" (true only while `<Version>` is right).
    - I dropped "this release is the one the listing shows" (Marketplace behaviour, unverified).
    - I dropped "on nuget.org" (an unregistered feed mention, which the guard caught).
    - I dropped "and on the Umbraco Marketplace listing". The listing is rendered client-side, so
      its text could not be read with `curl`.
    - What remains is verified: the `18.0.0`/`18.1.0` nuspec descriptions say "for Umbraco 17
      … Umbraco 17 (LTS)", and all their Umbraco dependencies are `[18.2.0, 19.0.0)`.
  - A Marketplace-tag paragraph saying "up to `18.1.0`", verified: all five `18.0.0` and
    `18.1.0` nuspecs carry the tag.
- [x] 3.3 `ChangelogTests` and `VersionTruthTests` pass.

  *Done:* 20/20.
## 4. Clean build and suites

- [x] 4.1 Clear `src/*/bin/Release`, the backoffice `App_Plugins` output and `obj/Release`. Run a
  `CI=true` build with `--no-incremental`. Verify: 0 warnings.
  *Done:* no TestSite process; outputs cleared; `CI=true` build with `--no-incremental`:
  0 warnings.
- [x] 4.2 Unit **1994**, integration 191, rendering 1168, client 335, all 0 skipped;
  `openspec validate --all --strict` passes.

  *Done:* unit **1994**, integration **191**, rendering **1168**, 0 skipped. Client **335**.
  Validate 26/26.
## 5. Review and merge

- [ ] 5.1 QA review in a subagent, reused across rounds.
- [ ] 5.2 Chris pushes the branch and opens a PR into `dev/v18`. Verify: the PR run is green at
  every step, parity included.
- [ ] 5.3 Merged with a merge commit. `origin/dev/v18` = local `dev/v18` = the merge commit.

## 6. Tag, pack and verify, from the merge commit

- [ ] 6.1 `git branch -r --contains HEAD` lists `origin/dev/v18`. **The merge commit's own push run
  is green at every step.** Tag `18.1.1` on it, and Chris pushes the tag.
- [ ] 6.2 Clear the outputs, build clean, pack. Verify: exactly five `.nupkg` and four `.snupkg`,
  all `18.1.1`.
- [ ] 6.3 Read all five nuspecs:
  - publisher, licence, and icon and readme present in the zip;
  - repository commit = the merge commit;
  - **every `UBookIt.*` dependency is `18.1.1`**;
  - every `Umbraco.Cms.*` dependency is `[18.2.0, 19.0.0)`;
  - `umbraco-marketplace` on `UBookIt` alone;
  - `UBookIt`'s description says **Umbraco 18**.
- [ ] 6.4 SourceLink resolves to the merge commit, and a sample source file returns 200.
- [ ] 6.5 README extracted from the packages: one hash, no relative links, everything pinned to
  `18.1.1`, "is at `18.1.1`". After the tag is pushed, every URL returns 200 (retry a 503), and
  any fragment matches GitHub's slug.
- [ ] 6.6 If 6.2–6.5 find a defect needing a commit, push nothing to nuget.org:
  1. Delete the tag locally and on origin.
  2. Fix through a PR.
  3. Re-tag after the new merge commit's push run is green.
  4. Redo 6.1–6.5.

## 7. Publish

- [ ] 7.1 Chris pushes the five packages.
- [ ] 7.2 The flat container lists `18.1.1`, checked **per package**.
- [ ] 7.3 The live page renders the pinned images and links. The live `UBookIt` nuspec says
  Umbraco 18.

## 8. Close out

- [ ] 8.1 Stamp the date.
- [ ] 8.2 Sibling sweep. Include `publishing.md:83`/`:397`, which are now true on both lines, and
  record what was found.
- [ ] 8.3 Commit, run the unit suite, and Chris pushes. Verify: `dev/v18`'s run is green at every
  step.
- [ ] 8.4 Archive **last**, and run the unit suite locally before the push. The CI for that push
  is reported in the session.
