## 1. The version and the entry

- [ ] 1.1 Branch `release-17-4-0` from local `main` at `47ea410` (the `single-choice-shortcut`
  archive).
  - Bump `<Version>` to `17.4.0`, and move every row of the table in `docs/publishing.md`
    *Before any push*, step 2.
  - Re-read the *Tag the release* literals by eye, because no guard sees them.
  - **Do not move the history anchors.**

  Verify: `git grep 17.3.2` over `Directory.Build.props`, README and the runbook is empty, apart
  from history anchors. Record the counts before and after; at `17.3.2` they were 1, 21 and 5.
- [ ] 1.2 Write the `17.4.0` entry, undated. **Check every claim against the code and the archived
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

## 2. Clean build and suites

- [ ] 2.1 Stop the TestSite. Clean the outputs (including the stale hashed client assets), build
  the client, then `CI=true dotnet build UBookIt.slnx -c Release --no-incremental`. Verify:
  0 warnings, 0 errors.
- [ ] 2.2 Run the suites per project, one after another. Verify, each with 0 skipped:
  - unit **2185**, integration **210**, rendering **1178**, client **356**;
  - `openspec validate --all --strict` passes (26 specs, plus this change), run as CI runs it,
    after the last commit.
- [ ] 2.3 Run the audit as `audit.yml` does. Verify: exit 0, the ImageSharp TestSite exception
  reported as accepted, not stale.

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
  paid. The `Pkcs` exit obligation stays open.
- [ ] 5.4 Commit and run the unit suite. Chris pushes. Verify: `main`'s CI is green.
- [ ] 5.5 Archive last, running the unit suite before the push.
