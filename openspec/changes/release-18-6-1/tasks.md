## 1. The version and the entry

- [x] 1.1 Branch `release-18-6-1` from `origin/dev/v18` at `1ff22dd`, with no upstream. Bump `<Version>`
      to `18.6.1` and move every row of `docs/publishing.md`'s *Before any push* table. Re-read the
      *Tag the release* literals by eye, and do not move any history anchor. Verify: none of the
      three files contains `18.6.0` afterwards, and the diff is exactly the pins counted beforehand.

      **Done (2026-11-16).** Branched from `origin/dev/v18` `1ff22dd`, with no upstream. Before the
      bump, `18.6.0` appeared 1 / 25 / 5 times in `Directory.Build.props` / README / the runbook,
      and each was read in context:
      - the `<Version>`;
      - 4 screenshot URLs, 20 tag-pinned doc links, and the *uBookIt is at* sentence;
      - the runbook's *is at* sentence, and its four *Tag the release* literals (re-read by eye).

      None is a history anchor. The CHANGELOG's `## 18.6.0` heading is history and is left. After:
      0 of `18.6.0` and 1 / 25 / 5 of `18.6.1`, and the diff is exactly 31 lines.


- [x] 1.2 Write the `18.6.1` entry, undated. Check every claim against the code and the archived
      `backoffice-client-upgrade`, not against the proposal: that nothing a site resolves changed,
      the generator and package versions, the peer requirement that forced the upgrade, the live
      checks, and the Node requirement. Verify: `ChangelogTests`, `VersionTruthTests` and the
      documentation guards pass.

      **Done.** Claims checked against their sources:
      - **Nothing resolved changed:** `git diff 18.6.0 origin/dev/v18` over `Directory.Packages.props`,
        `Directory.Build.props`, `global.json` and every `src` csproj is empty. The only `src/`
        changes are the regenerated client, its package files and generator script, and the
        Backoffice `README.txt`.
      - **Versions:** `@hey-api/openapi-ts ^0.99.0` and `@umbraco-cms/backoffice ^18.3.1`, locked at
        0.99.0 and 18.3.1. **At `18.6.0` they were `^0.85.2` and `^17.5.3`:** this line's section
        was built against Umbraco 17's backoffice types, which the entry states.
      - **Why:** the locked 18.3.1 declares the peer `@hey-api/openapi-ts >=0.99.0 <1.0.0`, and its
        engines are `node >=24.13, npm >=11`.
      - **Live checks on an Umbraco 18 site:** `2026-11-13-backoffice-client-upgrade` 5.2 (before)
        and 5.4 (after), the same D4 set.
      - **Wording:** no "nuget.org" mention and no retired literal.

      Guards on dev/v18: **171/171**.



## 2. Clean build and suites

- [x] 2.1 Make sure no TestSite is running. Clean the outputs, including the stale hashed client
      assets and the backoffice `obj`. Build the client, then
      `CI=true dotnet build UBookIt.slnx -c Release --no-incremental`. Verify: 0 warnings, 0 errors.

      **Done (2026-11-16) at `61f24fe`.** No TestSite was listening on 44348. Each project's `bin` and
      `obj` were deleted, plus the hashed client assets. `npm run build` gave exit 0, then
      `CI=true dotnet build UBookIt.slnx -c Release --no-incremental` gave **0 warnings, 0 errors**.
      *A first run was void, because of my script:* its clean step recursed into `node_modules` and
      deleted packages' own `bin` folders (`typescript/bin/tsc`), so the client build failed and Unit
      never built. It was repaired with `npm ci` (from the lockfile; the git tree stayed clean), the
      clean step was limited to each project's own `bin`/`obj` with a toolchain check, and every
      line was re-run.

- [x] 2.2 Run the suites per project, one after another, in Release with
      `UBOOKIT_TEST_DB_REQUIRED=true`. Verify, each with 0 skipped: unit **2557**, integration
      **222**, rendering **1211**, client **391**, and `openspec validate --all --strict` passing
      after the last commit.

      **Done**, one project after another, in Release with `UBOOKIT_TEST_DB_REQUIRED=true` (net10.0),
      0 skipped and 0 failed in each: unit **2557**, integration **222**, rendering **1211**, client
      **391** (19 files). `openspec validate --all --strict` gives 28 passed, to be re-run after the
      last commit.

- [x] 2.3 Run the audit as `audit.yml` does, from inside the line with its own SDK. Verify: exit 0, and
      the accepted count recorded.

      **Done**, as `audit.yml` now runs it, from inside the line with its own pinned SDK
      (`Assert-NoVulnerablePackages.ps1 -Target UBookIt.slnx`): "audited 10 project(s) in UBookIt.slnx
      (Debug, Release); 0 finding(s) accepted by a suppression", "No known advisories in the graph.",
      exit **0**.


## 3. Review and merge

- [x] 3.1 QA (`qa-review`) in one subagent, reused across rounds, covering `release-17-7-1`,
      this change and `release-19-0-0-rc1`. Brief it that temporary source mutations are
      allowed if reverted. Verify: APPROVE, or APPROVE WITH NITS with the nits resolved or recorded.

      **Round 1 (2026-11-16): REJECT** across the three release changes. No hard gate tripped, and the
      version numbers were right on all three lines. The reviewer independently verified:
      - the bumps (31 / 31 / none needed);
      - the patch class: local `dotnet pack` against the PUBLISHED 17.7.0 / 18.6.0 nuspecs, all five
        packages identical apart from the sibling version, and the Backoffice file set equal (19 `.js`);
      - every entry fact from lockfiles and `npm view`;
      - unit 2563 / 2557 / 2569, guards 171 / 171 / 173, client 391 ×3, the audit ×3, openspec 28 ×3;
      - that the `npm ci` repair was sound.

      **Findings, and what was done:**
      - **MAJOR, fixed:** the v19 README and *Getting started* said a bare install fails restore.
        **A real restore shows it succeeds and installs 18.6.0**, because `19.0.0-rc1` comes before
        `19.0.0` in NuGet's ordering, so 18.x's `[18.3.0,19.0.0)` admits it. Both pages, and the rc1
        entry's *What you have to do*, now say a bare install gets a stable 18.x that installs
        without complaint and does not work there.
      - **MAJOR, decided by Chris:** 18.x "and not 19" is false for 19 prereleases, and 19's
        `[19.0.0-rc1,20.0.0)` admits 20 prereleases the same way. The range fix (`X.0.0-0`) changes
        what a site resolves, so it is not a patch, and the guard
        `PackageCompositionTests.The_bound_admits_this_major_and_excludes_the_next` wrongly REQUIRES
        `X.0.0` and calls `X.0.0-0` a fault. Chris chose: wording now; ONE later change for the
        ceiling and the guard on all three lines (17 and 18 as minors, 19 with its 19.0.0 floor
        change). This is recorded in the deferred obligations. The wording is done: the v18 README
        and *Getting started* say a 19 RC is not refused and needs 19.x, and the main README says a
        prerelease of 18 is not refused.
      - **MINOR, fixed:** the 17.7.1 entry said "before and after" for every check, but on 17 the
        unreachable-source (500) case ran only after. It is now worded that way. The 18.6.1 entry
        was already accurate (v18 ran it before and after).
      - **MINOR, fixed:** "every package declares exactly the dependencies 17.7.0 declared" is
        literally false, because sibling references move. Both entries now say the same third-party
        dependencies at the same versions, and only the uBookIt packages' references to each other
        move.
      - **MINOR, fixed:** task 4.2 is sharpened on all three changes: the sibling-version exemption,
        exactly one hashed bundle set, and for 19 the `net11.0` groups and stable non-Umbraco
        versions matching `Directory.Packages.props`.
      - **MINOR, fixed:** `release-19-0-0-rc1` task 4.1 is now gated on 18.6.1 being live, because
        the 19 entry names 18.6.1.
      - **NIT, fixed:** the CHANGELOG headers (main, dev/v18) and main's README versioning sentence
        now name `19.x`.
      - **NIT, accepted:** v18 and v19 `src/api` differ only in a `baseUrl` literal, which is
        overwritten at runtime.

      **A trap met while fixing:** `UmbracoFloorDocumentationTests` failed on all three lines,
      because my explanations said prereleases sort "below 18.0.0 / 19.0.0" and the guard reads any
      "below X.Y.Z" as a stated floor. They were reworded ("comes before the 19.0.0 release in version
      order"), and the guard is unchanged. Then unit **2563 / 2557 / 2569**, all green.

      **Round 2: REJECT, on one MINOR.** Every round-1 fix was verified as new code:
      - a new restore probe (Umbraco 18.3.1 with UBookIt 17.7.0) fails with NU1107, so main's
        "refuses any released Umbraco 18 or later" is true;
      - no new phrase matches the floor guard's regex;
      - my reasoning about the ceiling guard was confirmed: it demands `X.0.0`, its prerelease
        explanation is wrong for `-0`, and fixing all three lines in one change is the right shape;
      - unit 2563 / 2557 / 2569, and openspec 28 ×3.

      **Fixed:**
      - **MINOR:** v18's README and *Getting started* said a bare install gets "a later Umbraco" and
        fails. That is false today: the newest stable is 18.x, and rc1 is a prerelease a bare add
        does not pick. They now say it gets an `18.x` today and fails once `19.0.0` is released.
      - **NIT:** v19's "not 20" / "below 20" rows now say the packages do not yet refuse a prerelease
        of Umbraco 20. Proposal item 6's "none can exist" becomes "none is expected". The deferred
        obligation names every caveat row the ceiling change must reword.
      - **NIT, accepted:** some added lines are longer than their neighbours. Markdown renders them.

      Unit after the fixes: dev/v18 **2557**, dev/v19 **2569**. main is unchanged this round.

      **Round 3: REJECT, on one MAJOR, which was a real packaging defect.** A real restore against a
      local feed showed that **`UBookIt 19.0.0-rc1` on a net10.0 (Umbraco 18) site restores with no
      warning or error and brings none of uBookIt**. The meta-package had no `lib/` folder, so NuGet
      treated it as compatible with every framework, no dependency group matched, and the Umbraco
      range was never consulted. That made v19's "cannot install it at all" and v18's new "the
      restore fails" false. Once 19 is the newest stable, every bare install on an Umbraco 18 site
      would get an empty package.

      **Chris chose to fix the packaging now:**
      - `src/UBookIt/UBookIt.csproj` packs an empty `src/UBookIt/_._` to `lib/$(TargetFramework)/`.
      - The `NU5128` suppression is removed. A CI pack without it is clean, so it suppressed nothing.
      - New guard `PackageCompositionTests.The_installable_package_is_compatible_with_its_own_framework_and_no_other`:
        `lib/` is exactly `lib/<tfm>/_._` for the nuspec's single framework (the distinct
        dependency and frameworkReference group frameworks). A first version expected one `<group>`
        and failed, because the frameworkReferences group repeats `net11.0`. **Removing the
        placeholder fails it.**
      - **Restore probe** (scratchpad `nu1202_probe.ps1`: its own feed, packages folder and source
        mapping; the user cache untouched): net10.0 gives **NU1202 "Package UBookIt 19.0.0-rc1 is
        not compatible with net10.0"**. A first probe run was void, because the user-level source
        mapping applied (NU1100); the probe now clears it.
      - **Wording:** v18's README and *Getting started* now say that once 19.0.0 is released, the
        restore fails "because it is not compatible with .NET 10" (no longer "a version conflict").
        The v19 entry gains "On an earlier .NET, `UBookIt` refuses to install". Proposal item 5's
        range claim is corrected and item 7 added. Task 4.2 checks the placeholder.

      After: CI-rules builds 0 warnings / 0 errors; unit dev/v18 **2557**, dev/v19 **2570** (2569 + the
      new guard). main is unchanged.

      **Round 4: APPROVE.** Nothing is outstanding, and no further round was asked for. The reviewer
      checked the placeholder against a real pack and real restores (local feed, its own packages
      folder, a cleared source mapping):
      - the nupkg holds exactly `lib/net11.0/_._`, and its groups are unchanged;
      - **net10.0 with Umbraco 18.3.1: NU1202**;
      - **net11.0 with Umbraco 19.0.0-rc1: exit 0**, every library resolving its
        `lib/net11.0/*.dll`, so the placeholder does not change resolution on the package's own
        framework.

      **Its own mutations:**
      - the placeholder with `Pack="false"` and a CI pack gives `error NU5128`. So dropping the
        suppression was right: the old one would have hidden the placeholder's loss;
      - `PackagePath="lib/net10.0/"` fails the new guard.

      Unit dev/v18 2557, dev/v19 2570, and openspec 28 ×3, with clean trees. For 4.2 it asks that the
      published `UBookIt.19.0.0-rc1.nupkg` be confirmed to list `lib/net11.0/_._`, which v19's 4.2
      already includes.

- [ ] 3.2 Push this branch and open the PR. Verify: the PR's CI is green (parity is skipped on PRs by
      design).
- [ ] 3.3 Merge with a merge commit, without `--delete-branch`. Verify: the merge's push run is green,
      parity included.

## 4. Publish

- [ ] 4.1 Chris pushes tag `18.6.1` on the merge commit, **by explicit SHA**. Verify: `check` and
      `pack` pass.
- [ ] 4.2 Before approving, check the run's `packages` artifact (Chris downloads it to
      `artifacts\packages-v18`): five packages and their symbols at `18.6.1`, each nuspec naming the
      tagged commit. Dependencies identical to the published `18.6.0` nuspecs **except** that the
      uBookIt packages' references to each other read `18.6.1`. `UBookIt.Backoffice`'s
      `staticwebassets/App_Plugins/UBookItBackoffice` holds exactly one hashed bundle set (19 `.js`
      files at `18.6.0`), with no stale assets. *(Sharpened at QA round 1.)*
- [ ] 4.3 Chris approves, with both Trusted Publishing policies left enabled. Verify: the publish job
      succeeds.
- [ ] 4.4 Confirm `18.6.1` per package on the flat container, and that the package page renders.

## 5. Close

- [ ] 5.1 Stamp the entry's date.
- [ ] 5.2 Update the deferred-obligations memory: the `backoffice-client-upgrade` changelog
      obligation is paid on this line.
- [ ] 5.3 Archive last. Run the unit suite and `openspec validate --all --strict` before the archive
      commit, and validate again after it.
