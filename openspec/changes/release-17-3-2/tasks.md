## 1. The version and the entry

- [x] 1.1 Branch `release-17-3-2` from local `main` at `4170e0b` (the `crypto-advisories-2026-10`
  archive).
  - Bump `<Version>` to `17.3.2`, and move every row of the table in `docs/publishing.md`
    *Before any push*, step 2.
  - Re-read the *Tag the release* literals by eye, because no guard sees them.
  - **Do not move the history anchors.**

  Verify: `git grep 17.3.1` over `Directory.Build.props`, README and the runbook is empty, apart
  from history anchors (record the counts before and after).
  *Done:* before, 1 in `Directory.Build.props`, 21 in README (16 tag-pinned documentation links,
  4 tag-pinned screenshot URLs and the "uBookIt is at" sentence; corrected in QA round 1, which
  found the first record's 17/4 split wrong) and 5 in the runbook (the "uBookIt is at" sentence
  and the
  four *Tag the release* literals). Each was read in context first; none was a history anchor.
  After: 0 of `17.3.1`, and 1, 21 and 5 of `17.3.2`; the diff is those 27 lines. The *Tag the
  release* literals were re-read by eye.
- [x] 1.2 Write the `17.3.2` entry, undated. **Check every claim against the code and the two
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
  *Done.* Each claim was checked against a source, not the proposal:
  - **Advisories:** all five `github.com/advisories/<id>` addresses return 200. Package, CVE and
    severity come from GitHub's advisory API: the three `Xml` ones are high; `Pkcs`
    GHSA-846f-f764-76mx is high and GHSA-2fmv-rv2c-59x7 medium (GitHub's word, as `17.2.3`'s entry
    used). All five are denial-of-service ("Denial of Service Vulnerability" in each summary).
  - **Runtime and SDKs:** `releases.json` lists 10.0.13 (2026-10-13, security), ASP.NET Core
    Runtime 10.0.13, SDKs 10.0.113 and 10.0.402. The release-notes address returns 200.
  - **"Your own NuGet audit will not tell you":** the archived advisory design (Context, D6) and
    Microsoft Learn's compatibility-rules page (pruning is on by default for .NET 10 projects).
    Worded as "with the .NET 10 SDK's default settings".
  - **"Newer than any runtime before 10.0.13, and .NET uses the newer":** Microsoft's documented
    rule (archived design D4), plus the Windows observation, which the entry labels as Windows.
  - **Pkcs routes and downgrade:** EF Core's SQL Server provider and MailKit (the archived
    proposal's measured table). The downgrade sentence follows `17.2.3`'s.
  - **Localization:** `culture: "en"`, `weight: 200` (`manifest.ts:239-243`). "Every earlier
    release" was checked: `en-us` at the commit that declared `17.0.0` (`b9ce573`, before tags
    existed) and at every tag from `17.1.1` to `17.3.1`. The example key was checked:
    `ubookitResources_create` is "Create resource" (`en-us.ts`); a first draft named
    `ubookitResources_heading`, which does not exist, and was corrected. The permissions are the
    four `entityUserPermission` toggles in the user group editor (`manifest.ts`). The override
    paragraph follows the archived localization handover (task 7.2).
  - No claim about CI.
  `ChangelogTests` and `VersionTruthTests` ran in the unit suite (2.2): passed.

## 2. Clean build and suites

- [x] 2.1 Stop the TestSite. Clean the outputs (including the stale hashed client assets), build
  the client, then `CI=true dotnet build UBookIt.slnx -c Release --no-incremental`. Verify:
  0 warnings, 0 errors.
  *Done:* no TestSite was running. `App_Plugins/UBookItBackoffice` deleted, client built (the
  bundle registers `culture: "en"`), then the clean CI-mode Release build: 0 warnings, 0 errors.
- [x] 2.2 Run the suites per project, one after another. Verify, each with 0 skipped:
  - unit **2131**, integration **210**, rendering **1178**, client **355**;
  - `openspec validate --all --strict` passes (26 specs, plus this change), run as CI runs it,
    after the last commit.
  *Done, 0 skipped:* client **355**, unit **2131**, rendering **1178**, integration **210**. The
  openspec result is recorded with the commit below.
- [x] 2.3 Run the audit as `audit.yml` does. Verify: exit 0, the ImageSharp TestSite exception
  reported as accepted, not stale.
  *Done:* exit 0; "audited 10 project(s) (Debug, Release); 5 finding(s) accepted by a
  suppression"; "No unaccepted advisories in the graph".

## 3. Review and merge

- [ ] 3.1 QA (`qa-review`) in one subagent, reused across rounds, covering this change and
  `release-18-2-2`. Verify: APPROVE.
  *Round 1: REJECT on both changes.* QA reproduced both clean builds, every suite, the audit and
  validation, the bump counts and every entry claim against its primary source.
  - **MAJOR: "Microsoft published five .NET advisories on 13 October 2026" was false.** GitHub
    lists 40 NuGet advisories published that day, and `releases.json` lists 44 CVEs for 10.0.13,
    three of them critical by GitHub's rating (two in Kestrel, on every platform; one in MsQuic).
    The entry made the runtime update look like a fix for three DoS advisories only, which
    understates it. *Fix (new text, verified first):* *What you have to do* now says the update
    fixes 44 vulnerabilities, links the release notes, names the critical Kestrel ones as not
    uBookIt's but in the runtime the site runs on, and tells the reader to take it regardless.
    *What changed* now says five of that day's advisories are against packages in uBookIt's graph.
    Same text on both lines (byte-identical below the heading).
  - MINOR (v18): task 3.3 checked `origin/main`; now `origin/dev/v18`.
  - MINOR: 4.2 had dropped the check that the uBookIt packages depend on each other at the new
    version; restored on both lines.
  - NITs taken: main 1.1's README split corrected; v18's proposal no longer reads as if `18.2.2`
    lands on `main`; a hosting platform that provides the runtime is covered; 5.1 rechecks the
    "no Umbraco release" sentence before stamping, because Umbraco published release candidates
    today. NIT declined: "(medium)" for GHSA-2fmv-rv2c-59x7 stays, GitHub's API value and the
    `17.2.3` entry's convention.
- [ ] 3.2 Chris pushes this branch and opens the PR. Verify: the PR's CI is green at every step,
  parity included.
- [ ] 3.3 Chris merges with "Create a merge commit". Verify: `origin/main` is the merge commit, and
  its push run is green.

## 4. Release through the workflow

- [ ] 4.1 Chris pushes tag `17.3.2` on the merge commit. Verify: `check` and `pack` are green.
- [ ] 4.2 **Before approving**, check the run's `packages` artifact:
  - the tagged `booking-flow.png` returns 200;
  - there are exactly 5 `.nupkg` and 4 `.snupkg`, all `17.3.2`, each `.snupkg` holding its `.pdb`;
  - all five nuspecs carry the authors, copyright, licence, readme, icon, project URL and the
    merge commit, with `umbraco-marketplace` on `UBookIt` only;
  - every `Umbraco.Cms.*` dependency is `[17.7.1, 18.0.0)`, and EF Core SqlServer is `10.0.11`,
    confirmed against `Directory.Packages.props` at the tag, and the dependencies between the
    uBookIt packages are at `17.3.2`;
  - **new this release:** `UBookIt.Persistence` and `UBookIt.Web` declare
    `System.Security.Cryptography.Pkcs` `10.0.13`; `UBookIt` and `UBookIt.Backoffice` declare the
    `Microsoft.AspNetCore.App` framework reference (as `UBookIt.Web` already does); no package
    declares `System.Security.Cryptography.Xml`;
  - the packed backoffice bundle registers its localization under `culture: "en"`;
  - the packed README is identical to the tag's, all its addresses return 200, and every
    repository address is pinned to `17.3.2`.
- [ ] 4.3 Chris approves. Record the summary table and the conclusion.
- [ ] 4.4 Confirm `17.3.2` per package on the flat container, and that the package page renders.

## 5. Close out

- [ ] 5.1 Stamp the entry's date. Before stamping, recheck "No Umbraco release fixes them yet":
  Umbraco published `17.8.0-rc` and `18.3.0-rc` on 2026-10-15. If a stable release that resolves
  `Pkcs` 10.0.13 is out by then, correct the sentence (the entry is not yet published).
- [ ] 5.2 Sibling sweep for `17.3.1` and for sentences the release falsifies. Fix or record each.
- [ ] 5.3 Run the `audit` workflow by hand (Actions → *audit* → *Run workflow*). Verify: green for
  both lines. Chris runs it and reports the result.
- [ ] 5.4 Update the deferred-obligations memory: the advisory section closes (the `Pkcs` exit
  obligation stays open); the localization section closes.
- [ ] 5.5 Commit and run the unit suite. Chris pushes. Verify: `main`'s CI is green.
- [ ] 5.6 Archive last, running the unit suite before the push.
