## 0. Start from origin

- [ ] 0.1 Branch `release-18-1-3` from `origin/dev/v18`, which must include `umbraco-security-2026-10`'s
  merge (`8bc5361`) and archive (`836c653`). Verify: `Directory.Packages.props` declares
  `[18.2.1,19.0.0)` for every `Umbraco.Cms*` entry.

## 1. The version and the entry

- [ ] 1.1 Bump `<Version>` to `18.1.3` and move every row of `docs/publishing.md` step 2's table.
  Re-read the *Tag the release* literals by eye. Verify: `git grep 18.1.2` over README and the
  runbook leaves only history anchors, each named here.
- [ ] 1.2 Write the `18.1.3` entry, undated, per the proposal's item 2. **Check every claim
  against the repository, not against the proposal.** Verify: `ChangelogTests` passes.

## 2. Clean build and suites

- [ ] 2.1 Stop the TestSite. Delete `src/*/bin/Release`, `src/*/obj/Release` and the backoffice
  `App_Plugins` output. Build the client first, then `CI=true dotnet build UBookIt.slnx -c Release
  --no-incremental`. Verify: 0 warnings.
- [ ] 2.2 Run the suites per project, sequentially. Verify: unit **2041**, integration **194**,
  rendering **1168**, client **335**, each with 0 skipped. `openspec validate --all --strict`
  passes.

## 3. Review and merge

- [ ] 3.1 QA review (`qa-review`) in one subagent, reused across rounds, covering this change and
  `release-17-2-3`. Hand each round's fixes over as new code, and give every claim as something to
  verify.
- [ ] 3.2 Chris pushes the branch and opens the PR into `dev/v18`. Verify, through the API, that the
  PR's CI run is green at every step, **parity included** (only `<Version>` differs in a parity
  file, and parity exempts it).
- [ ] 3.3 Chris merges with "Create a merge commit". Fetch. Verify: `origin/dev/v18` = local `dev/v18` =
  the merge commit, and the merge commit's own `ci` push run is green at every step.

## 4. Release through the workflow (design D1, D2, D4)

- [ ] 4.0 **The direct-link check (D4)**, on the merge commit's TestSite: open
  `/umbraco/section/ubookit/view/bookings` directly. Record what renders. If it is blank, repeat on
  `080dc6e` (18.2.0) and on `main`, and either record it as older than this release or stop and
  report.

- [ ] 4.1 Chris pushes tag `18.1.3` on the merge commit. Verify: `git ls-remote` shows it, and a
  `publish` run started with `check` and `pack` green.
- [ ] 4.2 **Before approving**, from the run's `packages` artifact:
  - the tagged `booking-flow.png` `curl` returns 200;
  - exactly 5 `.nupkg` and 4 `.snupkg`, all `18.1.3`;
  - all five nuspecs read: authors, copyright, licence, readme, icon, project URL, and the
    repository commit = the merge commit;
  - **D2:** every `Umbraco.Cms.*` dependency is `[18.2.1, 19.0.0)`; EF Core SqlServer is `10.0.11`;
    nothing declares ImageSharp;
  - the packed README is byte-identical to `README.md` at the tag; every address in it returns
    200; every repository address is pinned to `18.1.3`.

  If anything is wrong, reject the run and stop.
- [ ] 4.3 Chris approves in `release`. Record the run summary table (Chris pastes it), whether any
  `.snupkg` answered 409 and the exact CLI text, and the conclusion.
- [ ] 4.4 Confirm `18.1.3` per package on `api.nuget.org/v3-flatcontainer/<id>/index.json`, and that
  the live package page renders its readme.

## 5. Close out, in this order

- [ ] 5.1 Stamp the entry's date.
- [ ] 5.2 Sibling sweep for sentences this release falsifies: `docs/`, `README.md`,
  `openspec/specs/**`, `CLAUDE.md`. Search for `18.1.2`, `18.2.0`, `18.2.1` and `ImageSharp`.
  Fix or record each.
- [ ] 5.3 Commit on `dev/v18` and run the unit suite locally; Chris pushes. Verify: `dev/v18`'s CI run is
  green at every step.
- [ ] 5.4 Archive **last**, running the unit suite locally before the push.
