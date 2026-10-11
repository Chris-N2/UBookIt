## 0. Start from origin

- [x] 0.1 Branch `release-17-2-3` from `origin/main`, which must include `umbraco-security-2026-10`'s
  merge (`29f020d`) and archive (`b007be8`). Verify: `Directory.Packages.props` declares
  `[17.7.1,18.0.0)` for every `Umbraco.Cms*` entry.
  *Done:* branched at `b007be8` with the upstream unset. All seven `Umbraco.Cms*` entries are
  `[17.7.1,18.0.0)`, and both EF Core pins are `10.0.11`.

## 1. The version and the entry

- [x] 1.1 Bump `<Version>` to `17.2.3` and move every row of `docs/publishing.md` step 2's table.
  Re-read the *Tag the release* literals by eye. Verify: `git grep 17.2.2` over README and the
  runbook leaves only history anchors, each named here.
  *Done:* every `17.2.2` in `Directory.Build.props` (1), `README.md` (21) and `docs/publishing.md`
  (5) was a current-version mention, and all now read `17.2.3`. That covers the *is at*
  sentences, the screenshot and documentation pins, the example URL, and the `git tag` /
  `git push` / `curl` lines, which were re-read by eye. `git grep 17.2.2` over README and the
  runbook is now empty.
- [x] 1.2 Write the `17.2.3` entry, undated, per the proposal's item 2. **Check every claim
  against the repository, not against the proposal.** Verify: `ChangelogTests` passes.
  *Done.* It is the `18.1.3` entry with this line's values, plus the EF Core item. The checks
  are the same as `release-18-1-3` 1.2, and two corrections to this proposal came out of them:
  - **The advisories are Umbraco's repository advisories.** The global `github.com/advisories/…`
    address is 404 for all four. The entry links
    `github.com/umbraco/Umbraco-CMS/security/advisories/…`, and all four return 200. Read
    through the API, each is published 2026-10-06, affects `Umbraco.Cms` `>=17.0.0, <=17.7.0`,
    and is patched in `17.7.1`. Severities are medium, medium, low, low.
  - **ImageSharp comes through `Umbraco.Cms` itself.** On this line's restored graph,
    `Umbraco.Cms/17.7.1` depends on `SixLabors.ImageSharp 3.1.12` directly, as well as through
    `Umbraco.Cms.Imaging.ImageSharp`.
  - No uBookIt package reaches ImageSharp. `dotnet list package --include-transitive` after a
    fresh restore on this branch shows 0 for `UBookIt`, `.Core`, `.Persistence`, `.Backoffice`
    and `.Web`. The control `UBookIt.TestSite` shows 3, so the instrument sees it.
  - **EF Core:** `Umbraco.Cms.Persistence.EFCore/17.7.1` requires
    `Microsoft.EntityFrameworkCore.SqlServer` `10.0.11`, read from the restored assets.
    `17.2.2` declared `10.0.10`, read from the tag's `Directory.Packages.props`. A site's own
    direct reference below that is the one thing beyond `Umbraco.Cms` it might have to move, so
    it sits under *What you have to do*.

## 2. Clean build and suites

- [x] 2.1 Stop the TestSite. Delete `src/*/bin/Release`, `src/*/obj/Release` and the backoffice
  `App_Plugins` output. Build the client first, then `CI=true dotnet build UBookIt.slnx -c Release
  --no-incremental`. Verify: 0 warnings.
  *Done 2026-10-11 on `c26527e`:* the TestSite was stopped, and every `src/*` and `tests/*`
  `bin/Release` and `obj/Release`, plus the `App_Plugins` output, was deleted. The client was
  built, then `CI=true … --no-incremental`: **0 warnings, 0 errors**.
- [x] 2.2 Run the suites per project, sequentially. Verify: unit **2029**, integration **194**,
  rendering **1168**, client **335**, each with 0 skipped. `openspec validate --all --strict`
  passes.
  *Done, all 0 skipped:* unit **2029**, integration **194**, rendering **1168**, client **335**
  (15 files). `openspec validate --all --strict`: **27/27**.

## 3. Review and merge

- [ ] 3.1 QA review (`qa-review`) in one subagent, reused across rounds, covering this change and
  `release-18-1-3`. Hand each round's fixes over as new code, and give every claim as something to
  verify.
  *Round 1: REJECT for this change; `release-18-1-3` was APPROVE WITH NITS.* QA independently
  verified all six claims in the handover, reproducing the builds and suites.
  - **MAJOR:** the proposal's first ground for the patch ruling said "17.6 → 17.7.1" was a patch
    of Umbraco. It is not: 17.6 → 17.7 is an Umbraco minor. *Fix:* the ground now says the step
    stays inside Umbraco's major, as a minor plus its patch. The ruling itself is unchanged.
  - **MINOR:** the entry named only `Microsoft.EntityFrameworkCore.SqlServer` as a reference a
    site might have to raise. Umbraco 17.7.1 moves its whole set of Microsoft.Extensions and EF
    Core dependencies, and `Microsoft.Data.Sqlite`, from 10.0.10 to 10.0.11. I re-read the
    `Umbraco.Cms` and `Umbraco.Cms.Persistence.EFCore` 17.6.2 and 17.7.1 nuspecs from the feed's
    flat container to confirm it. *Fix:* *What you have to do* now says that, and names SqlServer
    as the example uBookIt also declares.
  - **NIT:** the proposal's item 2 still carried the pre-apply ImageSharp path and "Otherwise
    nothing". *Fix:* both now carry correction notes pointing here.
  - **NIT, no change:** a mixed-version site would likely lose the preview hub, because
    GHSA-4c5q's client half ships in `Umbraco.Cms.StaticAssets`. That backs the existing "upgrade
    first" advice, so the frozen entry was left alone.
- [ ] 3.2 Chris pushes the branch and opens the PR into `main`. Verify, through the API, that the
  PR's CI run is green at every step, **parity included** (only `<Version>` differs in a parity
  file, and parity exempts it).
- [ ] 3.3 Chris merges with "Create a merge commit". Fetch. Verify: `origin/main` = local `main` =
  the merge commit, and the merge commit's own `ci` push run is green at every step.

## 4. Release through the workflow (design D1, D2)

- [ ] 4.1 Chris pushes tag `17.2.3` on the merge commit. Verify: `git ls-remote` shows it, and a
  `publish` run started with `check` and `pack` green.
- [ ] 4.2 **Before approving**, from the run's `packages` artifact:
  - the tagged `booking-flow.png` `curl` returns 200;
  - exactly 5 `.nupkg` and 4 `.snupkg`, all `17.2.3`;
  - all five nuspecs read: authors, copyright, licence, readme, icon, project URL, and the
    repository commit = the merge commit;
  - **D2:** every `Umbraco.Cms.*` dependency is `[17.7.1, 18.0.0)`; EF Core SqlServer is `10.0.11`;
    nothing declares ImageSharp;
  - the packed README is byte-identical to `README.md` at the tag; every address in it returns
    200; every repository address is pinned to `17.2.3`.

  If anything is wrong, reject the run and stop.
- [ ] 4.3 Chris approves in `release`. Record the run summary table (Chris pastes it), whether any
  `.snupkg` answered 409 and the exact CLI text, and the conclusion.
- [ ] 4.4 Confirm `17.2.3` per package on `api.nuget.org/v3-flatcontainer/<id>/index.json`, and that
  the live package page renders its readme.

## 5. Close out, in this order

- [ ] 5.1 Stamp the entry's date.
- [ ] 5.2 Sibling sweep for sentences this release falsifies: `docs/`, `README.md`,
  `openspec/specs/**`, `CLAUDE.md`. Search for `17.2.2`, `17.6.2`, `17.7.1` and `ImageSharp`.
  Fix or record each.
- [ ] 5.3 Commit on `main` and run the unit suite locally; Chris pushes. Verify: `main`'s CI run is
  green at every step.
- [ ] 5.4 Archive **last**, running the unit suite locally before the push.
