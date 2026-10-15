## 1. The version and the entry

- [x] 1.1 Branch `release-18-2-2` from local `dev/v18` at `c783455` (the
  `crypto-advisories-2026-10` archive).
  - Bump `<Version>` to `18.2.2`, and move every row of the table in `docs/publishing.md`
    *Before any push*, step 2.
  - Re-read the *Tag the release* literals by eye, because no guard sees them.
  - **Do not move the history anchors.**

  Verify: `git grep 18.2.1` over `Directory.Build.props`, README and the runbook is empty, apart
  from history anchors (record the counts before and after).
  *Done:* before, 1 in `Directory.Build.props`, 23 in README and 5 in the runbook. **Two of the
  README's are not uBookIt's version but the Umbraco floor** (line 16, "Umbraco 18.2.1 or later",
  and the requirements table at line 175), so the README was changed by pattern: the 16
  tag-pinned documentation links, the 4 tag-pinned screenshot URLs and the "uBookIt is at"
  sentence. The props file and the runbook (the "uBookIt is at" sentence and the four *Tag the
  release* literals) were changed throughout. After: `18.2.1` remains only at those two Umbraco
  lines; `18.2.2` appears 1, 21 and 5 times; the diff is 27 lines, as on main. The *Tag the
  release* literals were re-read by eye. No history anchor named `18.2.1`.
- [x] 1.2 Write the `18.2.2` entry, undated. **Check every claim against the code and the two
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
  *Done:* the entry is `17.3.2`'s, byte-identical below the heading (compared with `diff`); it
  names no line-specific version. Its claims were checked as recorded in `release-17-3-2` task
  1.2, and the one that differs by line was checked here: every v18 tag from `18.0.0` to
  `18.2.1` registered `culture: "en-us"`, so "every earlier release" holds on this line.
  `ChangelogTests` and `VersionTruthTests` ran in the unit suite (2.2): passed.

## 2. Clean build and suites

- [x] 2.1 Stop the TestSite. Clean the outputs (including the stale hashed client assets), build
  the client, then `CI=true dotnet build UBookIt.slnx -c Release --no-incremental`. Verify:
  0 warnings, 0 errors.
  *Done:* no TestSite was running. `App_Plugins/UBookItBackoffice` deleted, client built (the
  bundle registers `culture: "en"`), then the clean CI-mode Release build: 0 warnings, 0 errors.
- [x] 2.2 Run the suites per project, one after another. Verify, each with 0 skipped:
  - unit **2143**, integration **210**, rendering **1178**, client **355**;
  - `openspec validate --all --strict` passes (26 specs, plus this change), run as CI runs it,
    after the last commit.
  *Done, 0 skipped:* client **355**, unit **2143**, rendering **1178**, integration **210**. The
  openspec result is recorded with the commit.
- [x] 2.3 Run the audit as `audit.yml` does. Verify: exit 0, the ImageSharp TestSite exception
  reported as accepted, not stale.
  *Done:* "audited 10 project(s) (Debug, Release); 5 finding(s) accepted by a suppression"; "No
  unaccepted advisories in the graph".

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
