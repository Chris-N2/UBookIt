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

## 1. The version and the entry

- [x] 1.1 Branch `release-18-3-0` from local `dev/v18` at `3357f94` (the `single-choice-shortcut`
  archive).
  - Bump `<Version>` to `18.3.0`, and move every row of the table in `docs/publishing.md`
    *Before any push*, step 2.
  - Re-read the *Tag the release* literals by eye, because no guard sees them.
  - **Do not move the history anchors.**

  Verify: `git grep 18.2.2` over `Directory.Build.props`, README and the runbook is empty, apart
  from history anchors. Record the counts before and after; at `18.2.2` they were 1, 21 and 5.
  *Done:* before, 1 in `Directory.Build.props`, 21 in README (16 tag-pinned documentation links, 4 tag-pinned screenshot URLs and the "uBookIt is at" sentence) and 5 in the runbook (its "uBookIt is at" sentence and the four *Tag the release* literals). Each was read in context first; none is a history anchor. After: 0 of `18.2.2`, and 1, 21 and 5 of `18.3.0`; the diff is those 27 lines. The *Tag the release* literals were re-read by eye.
- [x] 1.2 Write the `18.3.0` entry, undated. **Check every claim against the code and the archived
  change (`2026-10-15-single-choice-shortcut`), not against the proposal.** In particular:
  - the setting's key, label and default, from `UBookItPersistenceComposer`, `SiteBookingSettings`
    and `en-us.ts`;
  - what counts as "one": the catalogue as composed (services, plus resources bookable on their
    own), not availability;
  - that a named subject (an author's id, or `ubBook`) still wins;
  - the unreadable-value behaviour (on, with a startup error naming the key);
  - the downgrade claim, **checked against `18.2.2`'s code**: what an older version does with a
    stored `UBookIt:SkipSingleChoiceCatalogue` row (the settings screen, the effective
    configuration and the startup checks);
  - the Booking Page description reaching new installs only, checked against
    `BookingPagePackageMigrationPlan`;
  - the public additions, by exact name;
  - **not confusing uBookIt `18.3.0` with Umbraco `18.3.0`**: the Umbraco floor is unchanged at `[18.2.1, 19.0.0)`, and nothing in the entry may suggest otherwise;
  - no claim about CI.

  Verify: `ChangelogTests` and `VersionTruthTests` pass.
  *Done.* Each claim was checked against a source:
  - **The key, label and default:** `UBookItPersistenceComposer.SkipSingleChoiceCatalogueSettingKey`, `en-us.ts` ("Skip the catalogue when there is only one choice") and `SiteBookingSettings` (`= true`).
  - **What counts as one, and that a named subject wins:** `BookingCatalogue.BuildAsync` and `BookingFlowViewComponent.InvokeAsync`.
  - **No restart, and Reset to configured value:** seen live in the feature's checks 2.3 and 6.1.
  - **The unreadable value:** `ResolveSkipSingleChoiceCatalogue` and `ErrorIfSkipSingleChoiceCatalogueUnreadable`, and seen live in 6.1.
  - **The downgrade, against `18.2.2`'s code:**
    - `SettingsController.GetSettings` lists `SettingCatalogue.All` only, so a stored row for an unknown key is not shown.
    - `PutSetting` and `ResetSetting` answer 404 `setting-unknown` for it, so the screen cannot remove it.
    - `RunUBookItMigrations.Report` has no check that reads it, and nothing else does.
    - The row persists, so it applies again after an upgrade.
  - **The description reaching new installs only:** `BookingPagePackageMigrationPlan` is a custom plan with one fixed state.
  - **The public additions, by name**, from the archived feature (QA-verified there).
  - The entry does not mention Umbraco `18.3.0`, so the two cannot be confused. There is no CI claim.
  - **Below the heading, identical to `main`'s `17.4.0` entry except `17.3.x` → `18.2.x`** (twice: "code built against", "Going back to"). That is the one difference this line requires; checked by comparing the two entries with that mapping applied.
  `ChangelogTests` and `VersionTruthTests`: 20 of 20 passed.

## 2. Clean build and suites

- [x] 2.1 Stop the TestSite. Clean the outputs (including the stale hashed client assets), build
  the client, then `CI=true dotnet build UBookIt.slnx -c Release --no-incremental`. Verify:
  0 warnings, 0 errors.
  *Done:* no TestSite was running. The hashed client assets in `App_Plugins/UBookItBackoffice` were deleted, the client was built, then the clean CI-mode Release build: 0 warnings, 0 errors.
- [x] 2.2 Run the suites per project, one after another. Verify, each with 0 skipped:
  - unit **2197**, integration **210**, rendering **1178**, client **356**;
  - `openspec validate --all --strict` passes (26 specs, plus this change), run as CI runs it,
    after the last commit.
  *Done, 0 skipped:* client **356**, unit **2197**, integration **210**, rendering **1178**. The openspec result is recorded with the commit.
- [x] 2.3 Run the audit as `audit.yml` does. Verify: exit 0, the ImageSharp TestSite exception
  reported as accepted, not stale.
  *NOT met, by Chris's decision (2026-10-15):* exit 1. The only finding is a NEW advisory, [GHSA-qhrr-8q5h-9q3h](https://github.com/advisories/GHSA-qhrr-8q5h-9q3h) (CVE-2026-92707, medium, published 2026-10-15 18:35 UTC). It is in MessagePack 3.1.8 and fixed in 3.1.9.
  - It reaches seven projects **only through Umbraco**: the shipped `UBookIt`, `UBookIt.Backoffice` and `UBookIt.Web`, plus the TestSite and the three test projects (corrected in QA round 1, which found `UBookIt` missing from the first list): `Umbraco.Cms.Web.Common` and `Umbraco.Cms.PublishedCache.HybridCache` 18.2.1 depend on 3.1.8. uBookIt does not reference MessagePack.
  - Umbraco's `17.8.0-rc` / `18.3.0-rc` already move to 3.1.11.
  - Nothing else was found. The ImageSharp TestSite exception is still accepted.
  - **Chris chose to ship this release as it is and fix MessagePack in its own change straight after**: a minimum-version pin like `Pkcs`, or stable Umbraco. That is recorded in the deferred-obligations memory.
  - PR CI and `publish.yml` are unaffected, because advisories are warnings there. The scheduled `audit` workflow will be red until that change lands.

## 3. Review and merge

- [ ] 3.1 QA (`qa-review`) in one subagent, reused across rounds, covering this change and
  `release-17-4-0`. Verify: APPROVE.
- [ ] 3.2 Chris pushes `dev/v18` (the `single-choice-shortcut` archive, `3357f94`), then this branch,
  and opens the PR. Verify: the PR's CI is green at every step, parity included.
- [ ] 3.3 Chris merges with "Create a merge commit". Verify: `origin/dev/v18` is the merge commit, and
  its push run is green.

## 4. Release through the workflow

- [ ] 4.1 Chris pushes tag `18.3.0` on the merge commit. Verify: `check` and `pack` are green.
- [ ] 4.2 **Before approving**, check the run's `packages` artifact, parsing each nuspec as XML:
  - the tagged `booking-flow.png` returns 200;
  - there are exactly 5 `.nupkg` and 4 `.snupkg`, all `18.3.0`, each `.snupkg` holding its `.pdb`;
  - all five nuspecs carry the authors, copyright, licence, readme, icon, project URL and the
    merge commit, with `umbraco-marketplace` on `UBookIt` only;
  - every `Umbraco.Cms.*` dependency is `[18.2.1, 19.0.0)`, and EF Core SqlServer is `10.0.11`,
    confirmed against `Directory.Packages.props` at the tag; the dependencies between the uBookIt
    packages are at `18.3.0`;
  - carried from `18.2.2`: `UBookIt.Persistence` and `UBookIt.Web` declare
    `System.Security.Cryptography.Pkcs` `10.0.13`; `UBookIt`, `UBookIt.Backoffice` and `UBookIt.Web`
    declare the `Microsoft.AspNetCore.App` framework reference; no package declares
    `System.Security.Cryptography.Xml`; the backoffice bundle registers its localization under
    `culture: "en"`;
  - **new this release:** the packed backoffice bundle carries `skipSingleChoiceCatalogueLabel`;
    `UBookIt.Web.dll`'s embedded `package.xml` carries the new Booking Page description;
  - the packed README is identical to the tag's, all its addresses return 200, and every
    repository address is pinned to `18.3.0`.

  Show the check script can fail (for example, against the `18.2.2` artifact with this release's
  expectations).
- [ ] 4.3 Chris approves. Record the summary table and the conclusion.
- [ ] 4.4 Confirm `18.3.0` per package on the flat container, and that the package page renders.

## 5. Close out

- [ ] 5.1 Stamp the entry's date.
- [ ] 5.2 Sibling sweep for `18.2.2` and for sentences the release falsifies. Fix or record each.
- [ ] 5.3 Update the deferred-obligations memory: the `17.4.0` / `18.3.0` changelog obligation is
  paid. The `Pkcs` exit obligation stays open.
- [ ] 5.4 Commit and run the unit suite. Chris pushes. Verify: `dev/v18`'s CI is green.
- [ ] 5.5 Archive last, running the unit suite before the push.
