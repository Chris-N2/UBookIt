## 1. The version and the entry

- [ ] 1.1 Branch `release-18-2-2` from local `dev/v18` at `c783455` (the
  `crypto-advisories-2026-10` archive).
  - Bump `<Version>` to `18.2.2`, and move every row of the table in `docs/publishing.md`
    *Before any push*, step 2.
  - Re-read the *Tag the release* literals by eye, because no guard sees them.
  - **Do not move the history anchors.**

  Verify: `git grep 18.2.1` over `Directory.Build.props`, README and the runbook is empty, apart
  from history anchors (record the counts before and after).
- [ ] 1.2 Write the `18.2.2` entry, undated. **Check every claim against the code and the two
  archived changes, not against the proposal.** In particular:
  - the five advisories by GHSA id, each linked, with severity as GitHub records it;
  - the runtime version and SDKs from Microsoft's `releases.json`, not from memory;
  - that a site's own NuGet audit will not report the `Xml` advisories (pruning), stated as a
    fact about the site's build, checked against the archived design;
  - the observed `Pkcs` result is a Windows observation (the `runtimes/win` asset);
  - the localization wording: which users saw keys (US English excepted), the hard refresh, the
    `en-us`/`en` override behaviour, and `weight: 200`, each checked against `manifest.ts`;
  - no claim about CI.

  Verify: `ChangelogTests` and `VersionTruthTests` pass.

## 2. Clean build and suites

- [ ] 2.1 Stop the TestSite. Clean the outputs (including the stale hashed client assets), build
  the client, then `CI=true dotnet build UBookIt.slnx -c Release --no-incremental`. Verify:
  0 warnings, 0 errors.
- [ ] 2.2 Run the suites per project, one after another. Verify, each with 0 skipped:
  - unit **2143**, integration **210**, rendering **1178**, client **355**;
  - `openspec validate --all --strict` passes (26 specs, plus this change), run as CI runs it,
    after the last commit.
- [ ] 2.3 Run the audit as `audit.yml` does. Verify: exit 0, the ImageSharp TestSite exception
  reported as accepted, not stale.

## 3. Review and merge

- [ ] 3.1 QA (`qa-review`) in one subagent, reused across rounds, covering this change and
  `release-17-3-2`. Verify: APPROVE.
- [ ] 3.2 Chris pushes this branch and opens the PR. Verify: the PR's CI is green at every step,
  parity included.
- [ ] 3.3 Chris merges with "Create a merge commit". Verify: `origin/main` is the merge commit, and
  its push run is green.

## 4. Release through the workflow

- [ ] 4.1 Chris pushes tag `18.2.2` on the merge commit. Verify: `check` and `pack` are green.
- [ ] 4.2 **Before approving**, check the run's `packages` artifact:
  - the tagged `booking-flow.png` returns 200;
  - there are exactly 5 `.nupkg` and 4 `.snupkg`, all `18.2.2`, each `.snupkg` holding its `.pdb`;
  - all five nuspecs carry the authors, copyright, licence, readme, icon, project URL and the
    merge commit, with `umbraco-marketplace` on `UBookIt` only;
  - every `Umbraco.Cms.*` dependency is `[18.2.1, 19.0.0)`, and EF Core SqlServer is `10.0.11`,
    confirmed against `Directory.Packages.props` at the tag;
  - **new this release:** `UBookIt.Persistence` and `UBookIt.Web` declare
    `System.Security.Cryptography.Pkcs` `10.0.13`; `UBookIt` and `UBookIt.Backoffice` declare the
    `Microsoft.AspNetCore.App` framework reference (as `UBookIt.Web` already does); no package
    declares `System.Security.Cryptography.Xml`;
  - the packed backoffice bundle registers its localization under `culture: "en"`;
  - the packed README is identical to the tag's, all its addresses return 200, and every
    repository address is pinned to `18.2.2`.
- [ ] 4.3 Chris approves. Record the summary table and the conclusion.
- [ ] 4.4 Confirm `18.2.2` per package on the flat container, and that the package page renders.

## 5. Close out

- [ ] 5.1 Stamp the entry's date.
- [ ] 5.2 Sibling sweep for `18.2.1` and for sentences the release falsifies. Fix or record each.
- [ ] 5.3 The `audit` run is shared with `release-17-3-2` (one run audits both lines). Verify:
  recorded there; nothing to run here.
- [ ] 5.4 The deferred-obligations update is shared with `release-17-3-2`. Verify: done there,
  after both lines are live.
- [ ] 5.5 Commit and run the unit suite. Chris pushes. Verify: `dev/v18`'s CI is green.
- [ ] 5.6 Archive last, running the unit suite before the push.
