## 1. The version and the entry

- [x] 1.1 Branch `release-19-0-0-rc1` from `origin/dev/v19` at `99e9634`, with no upstream. Confirm
      `<Version>` is `19.0.0-rc1` and that every row of `docs/publishing.md`'s *Before any push* table
      already names it. Verify: `VersionTruthTests` passes, and no `18.6.x` appears in
      `Directory.Build.props`, the README or the runbook.

      **Done (2026-11-16).** Branched from `origin/dev/v19` `99e9634`, with no upstream.
      - `<Version>` is `19.0.0-rc1`.
      - `19.0.0-rc1` appears 1 / 29 / 9 times in `Directory.Build.props` / README / the runbook, and
        `18.6.x` appears 0 times in all three. The README's 29 is the 25 pins plus the prerelease
        install explanation the port added. The runbook's 9 is its *is at* sentence, the four *Tag
        the release* literals, and the *Publishing a prerelease* note.
      - No bump was needed. `VersionTruthTests` passes (1.2's guard run).

- [x] 1.2 Complete the `19.0.0-rc1` entry, undated: the backoffice client line, "as in `18.6.1`", and
      *For contributors*. Check every claim against the code. Verify: `ChangelogTests`,
      `VersionTruthTests` and the documentation guards pass.

      **Done.** *Also in this release* gains the backoffice client line (`@hey-api/openapi-ts` 0.99
      against `@umbraco-cms/backoffice` 19.0.0-rc1, the same regeneration `18.6.1` ships, and that
      release's other contents included). "behave as in `18.6.0`" becomes `18.6.1`. A new *For
      contributors* section covers the .NET 11 SDK, Node 24.13+ / npm 11+, and the three-line CI with
      prerelease tags, linking `docs/publishing.md#publishing-a-prerelease`. Checked against:
      - the lockfile: backoffice 19.0.0-rc1, hey-api 0.99.0, engines `node >=24.13, npm >=11`;
      - `global.json` `11.0.100`;
      - the runbook heading at line 239.

      Guards on dev/v19 (`Changelog|VersionTruth|Documentation|RetiredClaim|UmbracoFloor`):
      **173/173**. That is two more than main and dev/v18, from the port's prerelease-floor facts.


## 2. Clean build and suites

- [x] 2.1 Make sure no TestSite is running. Clean the outputs, including the stale hashed client
      assets and the backoffice `obj`. Build the client, then
      `CI=true dotnet build UBookIt.slnx -c Release --no-incremental`. Verify: 0 warnings, 0 errors.

      **Done (2026-11-16) at `d2e933a`.** No TestSite was listening on 44348. Each project's `bin` and
      `obj` were deleted, plus the hashed client assets. `npm run build` gave exit 0, then
      `CI=true dotnet build UBookIt.slnx -c Release --no-incremental` gave **0 warnings, 0 errors**.
      *A first run was void, because of my script:* its clean step recursed into `node_modules` and
      deleted packages' own `bin` folders (`typescript/bin/tsc`), so the client build failed and Unit
      never built. It was repaired with `npm ci` (from the lockfile; the git tree stayed clean), the
      clean step was limited to each project's own `bin`/`obj` with a toolchain check, and every
      line was re-run.

- [x] 2.2 Run the suites per project, one after another, in Release with
      `UBOOKIT_TEST_DB_REQUIRED=true`. Verify, each with 0 skipped: unit **2569**, integration
      **222**, rendering **1211**, client **391**, and `openspec validate --all --strict` passing
      after the last commit.

      **Done**, one project after another, in Release with `UBOOKIT_TEST_DB_REQUIRED=true` (net11.0),
      0 skipped and 0 failed in each: unit **2569**, integration **222**, rendering **1211**, client
      **391** (19 files). `openspec validate --all --strict` gives 28 passed, to be re-run after the
      last commit.

- [x] 2.3 Run the audit as `audit.yml` does, from inside the line with its own .NET 11 SDK. Verify:
      exit 0, and the accepted count recorded.

      **Done**, as `audit.yml` now runs it, from inside the line with its own pinned SDK
      (`Assert-NoVulnerablePackages.ps1 -Target UBookIt.slnx`): "audited 10 project(s) in UBookIt.slnx
      (Debug, Release); 0 finding(s) accepted by a suppression", "No known advisories in the graph.",
      exit **0**.


## 3. Review and merge

- [ ] 3.1 QA (`qa-review`) in one subagent, reused across rounds, covering `release-17-7-1`,
      `release-18-6-1` and this change. Verify: APPROVE, or APPROVE WITH NITS with the nits resolved or
      recorded.
- [ ] 3.2 Push this branch and open the PR into `dev/v19`. Verify: the PR's CI is green (parity is
      skipped on PRs by design).
- [ ] 3.3 Merge with a merge commit, without `--delete-branch`. Verify: the merge's push run is green,
      parity included. A tag requires it.

## 4. Publish

- [ ] 4.1 Chris pushes tag `19.0.0-rc1` on the merge commit, **by explicit SHA**. Verify: `publish.yml`
      starts (its prerelease filter), and `check` and `pack` pass.
- [ ] 4.2 Before approving, check the run's `packages` artifact (Chris downloads it to
      `artifacts\packages-v19`): five packages and their symbols at `19.0.0-rc1`, each nuspec naming the
      tagged commit, with `lib/net11.0`, and with Umbraco dependencies `[19.0.0-rc1, 20.0.0)`.
- [ ] 4.3 Chris approves, with both Trusted Publishing policies left enabled. Verify: the publish job
      **starts** (proving the `release` environment admits `19.0.0-rc1`) and succeeds.
- [ ] 4.4 Confirm `19.0.0-rc1` per package on the flat container, and that the package page renders
      and shows it as a prerelease.

## 5. Close

- [ ] 5.1 Stamp the entry's date.
- [ ] 5.2 Update the deferred-obligations memory: the changelog obligation is paid on this line, and
      the `release` environment's rule is proved (or what was changed).
- [ ] 5.3 Archive last. Run the unit suite and `openspec validate --all --strict` before the archive
      commit, and validate again after it.
