> **ON HOLD, then RE-SCOPED (2026-10-16).** The hold below was lifted the next morning: stable
> Umbraco is not due until 2026-10-29, which Chris judged too long. `messagepack-advisory` declared
> MessagePack 3.1.9 instead. It is merged (PR #34, `cb23fd6`) and archived (`6aa4d8c`). This branch
> was rebased onto `main` (it had never been pushed). Section 0 below redoes what the base change
> affects. Sections 1 and 2 keep their 2026-10-15 records, as history.
>
> *The original hold:*
>
> **ON HOLD (2026-10-15, Chris's decision after QA round 1).** QA found that shipping with
> MessagePack 3.1.8 breaks the packaging requirement *A shipped package declares the fix for an
> advisory its host cannot yet supply*. Rather than ship, pin and ship again, this release WAITS
> for stable Umbraco `17.8.0` / `18.3.0`. Their release candidates carry MessagePack 3.1.11 and
> `Pkcs` 10.0.13; confirm that in the stable nuspecs.
> - A separate change then raises the floor and removes the `Pkcs` pin, as that requirement
>   demands.
> - This change is then re-scoped on top of it: the floor raise goes first in *What you have to
>   do*, and the bump, entry, builds and QA are redone.
> - Round 1's other findings (the re-import overclaim and the project list) are fixed below.
> - The branch is local and was never pushed.

## 0. Re-scope onto `messagepack-advisory` (2026-10-16)

- [x] 0.1 Rebase this branch onto `main` at `6aa4d8c`. Verify: the branch contains it; the
  MessagePack declaration is present in `Directory.Packages.props`.
  *Done:* all three commits rebased without conflicts; `git merge-base --is-ancestor main HEAD`
  holds; `<PackageVersion Include="MessagePack" Version="3.1.9" />` is present.
- [x] 0.2 Extend the changelog entry with the advisory, and check every new claim against the
  archived `messagepack-advisory` (its proposal, and tasks 1.1, 1.4 and 2.4):
  - the GHSA id, severity and date, and its effect (from GitHub's advisory API);
  - the route (Umbraco 17.7.1's packages bring 3.1.8), and that no stable Umbraco carries the fix;
  - that `UBookIt.Backoffice` and `UBookIt.Web` declare 3.1.9, and `UBookIt` reaches it through
    them;
  - the NU1605 advice, which is an error, measured with SDK 10.0.301;
  - "no other dependency changes" against `17.3.2`, checked from the packed artifact at 4.2;
  - no claim about how Umbraco uses MessagePack.

  Verify: `ChangelogTests` and `VersionTruthTests` pass.
  *Done:* three additions to the entry: a *What you have to do* paragraph (raise a direct
  MessagePack reference below 3.1.9, or restore fails with NU1605), a *What changed* paragraph
  (the advisory), and a *For developers* bullet (the new dependency).
  - **Id, severity, date and effect:** GitHub's advisory API, checked 2026-10-15/16 — medium,
    published 2026-10-15, "nested arrays can blow past buffer length limits", allocating memory
    when deserializing into `System.Object`.
  - **The route:** Umbraco 17.7.1's packages bring 3.1.8 (`messagepack-advisory` 1.1, and its
    reviewer, from the nuspecs). No stable Umbraco carries the fix: the newest on nuget.org is
    `18.3.0-rc`.
  - **Which packages declare it:** `messagepack-advisory` 1.3 and 1.4.
  - **NU1605:** an error, measured in `messagepack-advisory` 2.4 with SDK 10.0.301.
  - **"No other dependency changes":** left for 4.2 to confirm from the artifact, as 0.2 requires.
  - The entry says nothing about how Umbraco uses MessagePack.
  - `ChangelogTests` and `VersionTruthTests`: 20 of 20.
- [x] 0.3 Redo 2.1 to 2.3 on the rebased branch: a clean CI-mode build, the suites (unit
  **2185**, integration **210**, rendering **1178**, client **356**), and the audit, which is now
  expected to **exit 0**. Verify: all green; the audit has no unaccepted advisory.
  *Done (2026-10-16):* the client assets were cleaned and rebuilt, then the clean CI-mode Release
  build: 0 warnings, 0 errors. 0 skipped: client **356**, unit **2185**, integration **210**,
  rendering **1178**. **The audit exits 0:** "5 finding(s) accepted by a suppression … No
  unaccepted advisories in the graph". The MessagePack advisory is gone, and the ImageSharp
  TestSite exception is unchanged. This supersedes 2.3's "NOT met" record.
- [x] 0.4 Task 4.2's artifact check gains: `UBookIt.Backoffice` and `UBookIt.Web` declare
  `MessagePack` `3.1.9`; no other package does.
  *Done:* added to 4.2's list, with the "no other dependency differs" check that 0.2 defers to it.
- [x] 0.5 QA round 2 for both release changes: round 1's reviewer if it can still be reached,
  otherwise a fresh one told round 1's findings and how each was resolved.
  *Round 2 (the same reviewer as round 1): APPROVE WITH NITS on both changes.* Both round-1
  MAJORs resolved: the re-import sentence, and MessagePack, fixed at its cause by
  `messagepack-advisory`. The MINOR was resolved too.
  - The reviewer reproduced the clean builds, every suite, the audit (exit 0) and openspec 27/27 on
    both lines.
  - It confirmed the rebase left the 27-mention bump byte-identical. It checked every new entry claim
    against GitHub's API and the Umbraco nuspecs.
  - It verified "no other dependency changes" now, by packing both lines and comparing them with the
    published `17.3.2` nuspecs from nuget.org, parsed as XML: the only difference is MessagePack 3.1.9 in
    Backoffice and Web.

  Nits taken:
  - the downgrade paragraph now says going back gives up the MessagePack fix;
  - 5.3 names both exits;
  - main's proposal places the Booking Page description under *What changed*, where the entry has it.

  Not taken: "code built against 17.3.x / 18.2.x" keeps the precedent's phrasing, as round 1 allowed.

## 1. The version and the entry

- [x] 1.1 Branch `release-17-4-0` from local `main` at `47ea410` (the `single-choice-shortcut`
  archive).
  - Bump `<Version>` to `17.4.0`, and move every row of the table in `docs/publishing.md`
    *Before any push*, step 2.
  - Re-read the *Tag the release* literals by eye, because no guard sees them.
  - **Do not move the history anchors.**

  Verify: `git grep 17.3.2` over `Directory.Build.props`, README and the runbook is empty, apart
  from history anchors. Record the counts before and after; at `17.3.2` they were 1, 21 and 5.
  *Done:* before, 1 in `Directory.Build.props`, 21 in README (16 tag-pinned documentation links, 4 tag-pinned screenshot URLs and the "uBookIt is at" sentence) and 5 in the runbook (its "uBookIt is at" sentence and the four *Tag the release* literals). Each was read in context first; none is a history anchor. After: 0 of `17.3.2`, and 1, 21 and 5 of `17.4.0`; the diff is those 27 lines. The *Tag the release* literals were re-read by eye.
- [x] 1.2 Write the `17.4.0` entry, undated. **Check every claim against the code and the archived
  change (`2026-10-15-single-choice-shortcut`), not against the proposal.** In particular:
  - the setting's key, label and default, from `UBookItPersistenceComposer`, `SiteBookingSettings`
    and `en-us.ts`;
  - what counts as "one": the catalogue as composed (services, plus resources bookable on their
    own), not availability;
  - that a named subject (an author's id, or `ubBook`) still wins;
  - the unreadable-value behaviour (on, with a startup error naming the key);
  - the downgrade claim, **checked against `17.3.2`'s code**: what an older version does with a
    stored `UBookIt:SkipSingleChoiceCatalogue` row (the settings screen, the effective
    configuration and the startup checks);
  - the Booking Page description reaching new installs only, checked against
    `BookingPagePackageMigrationPlan`;
  - the public additions, by exact name;
  - not confusing uBookIt `18.3.0` with Umbraco `18.3.0` if the entry mentions either;
  - no claim about CI.

  Verify: `ChangelogTests` and `VersionTruthTests` pass.
  *Done.* Each claim was checked against a source:
  - **The key, label and default:** `UBookItPersistenceComposer.SkipSingleChoiceCatalogueSettingKey`, `en-us.ts` ("Skip the catalogue when there is only one choice") and `SiteBookingSettings` (`= true`).
  - **What counts as one, and that a named subject wins:** `BookingCatalogue.BuildAsync` and `BookingFlowViewComponent.InvokeAsync`.
  - **No restart, and Reset to configured value:** seen live in the feature's checks 2.3 and 6.1.
  - **The unreadable value:** `ResolveSkipSingleChoiceCatalogue` and `ErrorIfSkipSingleChoiceCatalogueUnreadable`, and seen live in 6.1.
  - **The downgrade, against `17.3.2`'s code:**
    - `SettingsController.GetSettings` lists `SettingCatalogue.All` only, so a stored row for an unknown key is not shown.
    - `PutSetting` and `ResetSetting` answer 404 `setting-unknown` for it, so the screen cannot remove it.
    - `RunUBookItMigrations.Report` has no check that reads it, and nothing else does.
    - The row persists, so it applies again after an upgrade.
  - **The description reaching new installs only:** `BookingPagePackageMigrationPlan` is a custom plan with one fixed state.
  - **The public additions, by name**, from the archived feature (QA-verified there).
  - The entry does not mention Umbraco `18.3.0`, so the two cannot be confused. There is no CI claim.
  - This line's entry is the reference text; `release-18-3-0`'s matches it below the heading apart from the version names.
  `ChangelogTests` and `VersionTruthTests`: 20 of 20 passed.

## 2. Clean build and suites

- [x] 2.1 Stop the TestSite. Clean the outputs (including the stale hashed client assets), build
  the client, then `CI=true dotnet build UBookIt.slnx -c Release --no-incremental`. Verify:
  0 warnings, 0 errors.
  *Done:* no TestSite was running. The hashed client assets in `App_Plugins/UBookItBackoffice` were deleted, the client was built, then the clean CI-mode Release build: 0 warnings, 0 errors.
- [x] 2.2 Run the suites per project, one after another. Verify, each with 0 skipped:
  - unit **2185**, integration **210**, rendering **1178**, client **356**;
  - `openspec validate --all --strict` passes (26 specs, plus this change), run as CI runs it,
    after the last commit.
  *Done, 0 skipped:* client **356**, unit **2185**, integration **210**, rendering **1178**. The openspec result is recorded with the commit.
- [x] 2.3 Run the audit as `audit.yml` does. Verify: exit 0, the ImageSharp TestSite exception
  reported as accepted, not stale.
  *NOT met, by Chris's decision (2026-10-15):* exit 1. The only finding is a NEW advisory, [GHSA-qhrr-8q5h-9q3h](https://github.com/advisories/GHSA-qhrr-8q5h-9q3h) (CVE-2026-92707, medium, published 2026-10-15 18:35 UTC). It is in MessagePack 3.1.8 and fixed in 3.1.9.
  - It reaches seven projects **only through Umbraco**: the shipped `UBookIt`, `UBookIt.Backoffice` and `UBookIt.Web`, plus the TestSite and the three test projects (corrected in QA round 1, which found `UBookIt` missing from the first list): `Umbraco.Cms.Web.Common` and `Umbraco.Cms.PublishedCache.HybridCache` 17.7.1 depend on 3.1.8. uBookIt does not reference MessagePack.
  - Umbraco's `17.8.0-rc` / `18.3.0-rc` already move to 3.1.11.
  - Nothing else was found. The ImageSharp TestSite exception is still accepted.
  - **Chris chose to ship this release as it is and fix MessagePack in its own change straight after**: a minimum-version pin like `Pkcs`, or stable Umbraco. That is recorded in the deferred-obligations memory.
  - PR CI and `publish.yml` are unaffected, because advisories are warnings there. The scheduled `audit` workflow will be red until that change lands.

## 3. Review and merge

- [ ] 3.1 QA (`qa-review`) in one subagent, reused across rounds, covering this change and
  `release-18-3-0`. Verify: APPROVE.
- [ ] 3.2 Chris pushes `main` (the `single-choice-shortcut` archive, `47ea410`), then this branch,
  and opens the PR. Verify: the PR's CI is green at every step, parity included.
- [ ] 3.3 Chris merges with "Create a merge commit". Verify: `origin/main` is the merge commit, and
  its push run is green.

## 4. Release through the workflow

- [ ] 4.1 Chris pushes tag `17.4.0` on the merge commit. Verify: `check` and `pack` are green.
- [ ] 4.2 **Before approving**, check the run's `packages` artifact, parsing each nuspec as XML:
  - the tagged `booking-flow.png` returns 200;
  - there are exactly 5 `.nupkg` and 4 `.snupkg`, all `17.4.0`, each `.snupkg` holding its `.pdb`;
  - all five nuspecs carry the authors, copyright, licence, readme, icon, project URL and the
    merge commit, with `umbraco-marketplace` on `UBookIt` only;
  - every `Umbraco.Cms.*` dependency is `[17.7.1, 18.0.0)`, and EF Core SqlServer is `10.0.11`,
    confirmed against `Directory.Packages.props` at the tag; the dependencies between the uBookIt
    packages are at `17.4.0`;
  - carried from `17.3.2`: `UBookIt.Persistence` and `UBookIt.Web` declare
    `System.Security.Cryptography.Pkcs` `10.0.13`; `UBookIt`, `UBookIt.Backoffice` and `UBookIt.Web`
    declare the `Microsoft.AspNetCore.App` framework reference; no package declares
    `System.Security.Cryptography.Xml`; the backoffice bundle registers its localization under
    `culture: "en"`;
  - **new this release (from `messagepack-advisory`):** `UBookIt.Backoffice` and `UBookIt.Web` declare `MessagePack` `3.1.9`; no other package declares MessagePack; and no other dependency differs from the previous release's artifact;
  - **new this release:** the packed backoffice bundle carries `skipSingleChoiceCatalogueLabel`;
    `UBookIt.Web.dll`'s embedded `package.xml` carries the new Booking Page description;
  - the packed README is identical to the tag's, all its addresses return 200, and every
    repository address is pinned to `17.4.0`.

  Show the check script can fail (for example, against the `17.3.2` artifact with this release's
  expectations).
- [ ] 4.3 Chris approves. Record the summary table and the conclusion.
- [ ] 4.4 Confirm `17.4.0` per package on the flat container, and that the package page renders.

## 5. Close out

- [ ] 5.1 Stamp the entry's date.
- [ ] 5.2 Sibling sweep for `17.3.2` and for sentences the release falsifies. Fix or record each.
- [ ] 5.3 Update the deferred-obligations memory: the `17.4.0` / `18.3.0` changelog obligation is
  paid. The exit obligation stays open for both declarations, `Pkcs` and MessagePack: the floor raise to stable Umbraco removes them together (QA round 2 NIT).
- [ ] 5.4 Commit and run the unit suite. Chris pushes. Verify: `main`'s CI is green.
- [ ] 5.5 Archive last, running the unit suite before the push.
