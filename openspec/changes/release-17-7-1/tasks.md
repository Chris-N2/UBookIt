## 1. The version and the entry

- [x] 1.1 Branch `release-17-7-1` from `origin/main` at `da9a311`, with no upstream. Bump `<Version>`
      to `17.7.1` and move every row of `docs/publishing.md`'s *Before any push* table. Re-read the
      *Tag the release* literals by eye, and do not move any history anchor. Verify: none of the
      three files contains `17.7.0` afterwards, and the diff is exactly the pins counted beforehand.

      **Done (2026-11-16).** Branched from `origin/main` `da9a311`, with no upstream. Before the
      bump, `17.7.0` appeared 1 / 25 / 5 times in `Directory.Build.props` / README / the runbook,
      and each was read in context:
      - the `<Version>`;
      - 4 screenshot URLs, 20 tag-pinned doc links, and the *uBookIt is at* sentence;
      - the runbook's *is at* sentence, and its four *Tag the release* literals (re-read by eye).

      None is a history anchor. The CHANGELOG's `## 17.7.0` heading is history and is left. After:
      0 of `17.7.0` and 1 / 25 / 5 of `17.7.1`, and the diff is exactly 31 lines.

- [x] 1.2 Write the `17.7.1` entry, undated. Check every claim against the code and the archived
      `backoffice-client-upgrade`, not against the proposal: that nothing a site resolves changed,
      the generator and package versions, the peer requirement that forced the upgrade, the live
      checks, and the Node requirement. Verify: `ChangelogTests`, `VersionTruthTests` and the
      documentation guards pass.

      **Done.** Claims checked against their sources:
      - **Nothing resolved changed:** `git diff 17.7.0 origin/main` over `Directory.Packages.props`,
        `Directory.Build.props`, `global.json` and every `src` csproj is empty. The only `src/`
        changes are `Client/src/api/*`, `package.json` / `package-lock.json`, the generator script,
        and the Backoffice `README.txt`.
      - **Versions:** `package.json` has `@hey-api/openapi-ts ^0.99.0` and `@umbraco-cms/backoffice
        ^17.8.1`. The lockfile has backoffice 17.8.1 and hey-api 0.99.0. At `17.7.0` they were
        `^0.85.2` and `^17.5.3`.
      - **Why:** the locked backoffice 17.8.1 declares the peer `@hey-api/openapi-ts >=0.99.0
        <1.0.0`.
      - **Node:** backoffice 17.8.1 `engines` `node >=24.13, npm >=11`; `ci.yml` and `publish.yml`
        use `node-version: 24`.
      - **Live checks:** the D4 set recorded in `2026-11-13-backoffice-client-upgrade` 4.2 / 5.4:
        bookings list and the too-long window, closure create and delete, the invalid closure, and
        holiday 404 withdrawing and 500 keeping the panel.
      - **CI:** the three lines and prerelease tags, as `ci-three-lines` shipped them.
      - **Wording:** no "nuget.org" mention (the feed-accounting guard) and no retired literal.

      Guards (`Changelog|VersionTruth|Documentation|RetiredClaim|UmbracoFloor`): **171/171**.


## 2. Clean build and suites

- [x] 2.1 Make sure no TestSite is running. Clean the outputs, including the stale hashed client
      assets and the backoffice `obj`. Build the client, then
      `CI=true dotnet build UBookIt.slnx -c Release --no-incremental`. Verify: 0 warnings, 0 errors.

      **Done (2026-11-16) at `89e2a3b`.** No TestSite was listening on 44348. Each project's `bin` and
      `obj` were deleted, plus the hashed client assets. `npm run build` gave exit 0, then
      `CI=true dotnet build UBookIt.slnx -c Release --no-incremental` gave **0 warnings, 0 errors**.
      *A first run was void, because of my script:* its clean step recursed into `node_modules` and
      deleted packages' own `bin` folders (`typescript/bin/tsc`), so the client build failed and Unit
      never built. It was repaired with `npm ci` (from the lockfile; the git tree stayed clean), the
      clean step was limited to each project's own `bin`/`obj` with a toolchain check, and every
      line was re-run.

- [x] 2.2 Run the suites per project, one after another, in Release with
      `UBOOKIT_TEST_DB_REQUIRED=true`. Verify, each with 0 skipped: unit **2563**, integration
      **222**, rendering **1211**, client **391**, and `openspec validate --all --strict` passing
      after the last commit.

      **Done**, one project after another, in Release with `UBOOKIT_TEST_DB_REQUIRED=true` (net10.0),
      0 skipped and 0 failed in each: unit **2563**, integration **222**, rendering **1211**, client
      **391** (19 files). `openspec validate --all --strict` gives 28 passed, to be re-run after the
      last commit.

- [x] 2.3 Run the audit as `audit.yml` does, from inside the line with its own SDK. Verify: exit 0, and
      the accepted count recorded.

      **Done**, as `audit.yml` now runs it, from inside the line with its own pinned SDK
      (`Assert-NoVulnerablePackages.ps1 -Target UBookIt.slnx`): "audited 10 project(s) in UBookIt.slnx
      (Debug, Release); 0 finding(s) accepted by a suppression", "No known advisories in the graph.",
      exit **0**.


## 3. Review and merge

- [ ] 3.1 QA (`qa-review`) in one subagent, reused across rounds, covering this change,
      `release-18-6-1` and `release-19-0-0-rc1`. Brief it that temporary source mutations are
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

- [ ] 3.2 Push this branch and open the PR. Verify: the PR's CI is green (parity is skipped on PRs by
      design).
- [ ] 3.3 Merge with a merge commit, without `--delete-branch`. Verify: the merge's push run is green,
      parity included.

## 4. Publish

- [ ] 4.1 Chris pushes tag `17.7.1` on the merge commit, **by explicit SHA**. Verify: `check` and
      `pack` pass.
- [ ] 4.2 Before approving, check the run's `packages` artifact (Chris downloads it to
      `artifacts\packages-v17`): five packages and their symbols at `17.7.1`, each nuspec naming the
      tagged commit. Dependencies identical to the published `17.7.0` nuspecs **except** that the
      uBookIt packages' references to each other read `17.7.1`. `UBookIt.Backoffice`'s
      `staticwebassets/App_Plugins/UBookItBackoffice` holds exactly one hashed bundle set (19 `.js`
      files at `17.7.0`), with no stale assets. *(Sharpened at QA round 1.)*
- [ ] 4.3 Chris approves, with both Trusted Publishing policies left enabled. Verify: the publish job
      succeeds.
- [ ] 4.4 Confirm `17.7.1` per package on the flat container, and that the package page renders.

## 5. Close

- [ ] 5.1 Stamp the entry's date.
- [ ] 5.2 Update the deferred-obligations memory: the `backoffice-client-upgrade` changelog
      obligation is paid on this line.
- [ ] 5.3 Archive last. Run the unit suite and `openspec validate --all --strict` before the archive
      commit, and validate again after it.
