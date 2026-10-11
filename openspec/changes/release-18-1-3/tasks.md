## 0. Start from origin

- [x] 0.1 Branch `release-18-1-3` from `origin/dev/v18`, which must include `umbraco-security-2026-10`'s
  merge (`8bc5361`) and archive (`836c653`). Verify: `Directory.Packages.props` declares
  `[18.2.1,19.0.0)` for every `Umbraco.Cms*` entry.
  *Done:* branched at `836c653` with the upstream unset. All seven `Umbraco.Cms*` entries are
  `[18.2.1,19.0.0)`.

## 1. The version and the entry

- [x] 1.1 Bump `<Version>` to `18.1.3` and move every row of `docs/publishing.md` step 2's table.
  Re-read the *Tag the release* literals by eye. Verify: `git grep 18.1.2` over README and the
  runbook leaves only history anchors, each named here.
  *Done:* every `18.1.2` in `Directory.Build.props` (1), `README.md` (21) and `docs/publishing.md`
  (5) was a current-version mention, and all now read `18.1.3`. That covers the *is at*
  sentences, the screenshot and documentation pins, the example URL, and the `git tag` /
  `git push` / `curl` lines, which were re-read by eye. `git grep 18.1.2` over README and the
  runbook is now empty: neither carries a history anchor naming `18.1.2`.
- [x] 1.2 Write the `18.1.3` entry, undated, per the proposal's item 2. **Check every claim
  against the repository, not against the proposal.** Verify: `ChangelogTests` passes.
  *Done.* Each claim was checked, and two of the proposal's own were corrected on the way:
  - **The advisories are Umbraco's repository advisories, not in GitHub's global database.**
    `github.com/advisories/GHSA-…` returns 404 for all four. They link to
    `github.com/umbraco/Umbraco-CMS/security/advisories/…`, and each returns 200. Read through the
    API, each is published 2026-10-06, affects `Umbraco.Cms` `>=18.0.0, <=18.2.0` (and 17.0–17.7.0),
    and is patched in `18.2.1` (and `17.7.1`). Severities are medium, medium, low, low, as "medium",
    not "moderate".
  - **ImageSharp reaches a site through `Umbraco.Cms` itself.** `project.assets.json` shows
    `Umbraco.Cms/18.2.1` depending on `SixLabors.ImageSharp 3.1.12` directly, as well as through
    `Umbraco.Cms.Imaging.ImageSharp`. The proposal said only "Umbraco's imaging package".
  - "No uBookIt package depends on ImageSharp": `dotnet list package --include-transitive` finds
    no ImageSharp for `UBookIt`, `UBookIt.Core`, `.Persistence`, `.Backoffice` or `.Web`. In the
    same run, the control `UBookIt.TestSite` lists it, so the instrument can see it. This is
    re-checked on the packed nuspecs at 4.2.
  - "Raises only the Umbraco packages uBookIt depends on" is the README's requirements-row
    wording, unchanged since QA approved it in `umbraco-security-2026-10`.
  - There's no EF Core sentence, because `18.1.2` already declared `10.0.11`.

## 2. Clean build and suites

- [x] 2.1 Stop the TestSite. Delete `src/*/bin/Release`, `src/*/obj/Release` and the backoffice
  `App_Plugins` output. Build the client first, then `CI=true dotnet build UBookIt.slnx -c Release
  --no-incremental`. Verify: 0 warnings.
  *Done 2026-10-11 on `ae884cf`:* the TestSite was stopped, and every `src/*` and `tests/*`
  `bin/Release` and `obj/Release`, plus the `App_Plugins` output, was deleted. The client was
  built, then `CI=true … --no-incremental`: **0 warnings, 0 errors**.
- [x] 2.2 Run the suites per project, sequentially. Verify: unit **2041**, integration **194**,
  rendering **1168**, client **335**, each with 0 skipped. `openspec validate --all --strict`
  passes.
  *Done, all 0 skipped:* unit **2041**, integration **194**, rendering **1168**, client **335**
  (15 files). `openspec validate --all --strict`: **27/27**.

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

- [x] 4.0 **The direct-link check (D4)**, on the merge commit's TestSite: open
  `/umbraco/section/ubookit/view/bookings` directly. Record what renders. If it is blank, repeat on
  `080dc6e` (18.2.0) and on `main`, and either record it as older than this release or stop and
  report.
  *Done early, on 2026-10-11, before QA,* on this branch's TestSite. The code is identical to the
  release's: only docs and the version differ. Started 16:30 BST, its `deps.json` names
  `Umbraco.Cms.Core/18.2.1`, logged in by Chris.
  - Three direct navigations: Bookings, Closures, Bookings again. **Each rendered its own view, with
    its own tab active.** The `uui-tab` states read through the DOM show `Bookings:true` and every
    other tab false. The second Bookings load showed its four bookings. No console errors.
  - A screenshot taken during the first load showed the body before the table had arrived, and a
    half-scale image made the active tab hard to read. The DOM settled it.
  - **Not reproduced, so nothing is attributed and nothing is gated.** The original observation
    came straight after a TestSite restart and re-login, during which Umbraco core logged auth
    and SignalR errors. That's a plausible cause, but it is not shown. The deferred obligation
    is discharged as "did not reproduce".

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
