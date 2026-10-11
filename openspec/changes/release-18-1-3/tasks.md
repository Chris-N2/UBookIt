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

- [x] 3.1 QA review (`qa-review`) in one subagent, reused across rounds, covering this change and
  `release-17-2-3`. Hand each round's fixes over as new code, and give every claim as something to
  verify.
  *Round 1: APPROVE WITH NITS for this change* (`release-17-2-3` was REJECT, MAJOR and MINOR,
  both on that line only). QA independently verified all six claims, reproducing the builds and
  suites. Two NITs here, both taken:
  - the proposal's ImageSharp path now carries its correction note;
  - 4.0's run on the branch rather than the merge commit now carries a caveat, re-checked at 3.3.

  QA also noted that 18.2.0, `18.1.2`'s floor, already required EF Core 10.0.11, which is a
  stronger reason than our declaration for the entry having no EF item. That is added to the
  proposal.
  *Round 2: APPROVE WITH NITS.* QA diffed every `Umbraco.Cms*` package from 18.2.0 to 18.2.1. No
  non-Umbraco dependency moves, so `17.2.3`'s round-2 finding doesn't carry over. One NIT, taken:
  4.0's caveat now says "not documentation, changelog or openspec" instead of "outside docs".
  *Round 3: APPROVE.* No further rounds. Three rounds, one reviewer.
- [x] 3.2 Chris pushes the branch and opens the PR into `dev/v18`. Verify, through the API, that the
  PR's CI run is green at every step, **parity included** (only `<Version>` differs in a parity
  file, and parity exempts it).
  *Done:* PR **#16**. Its run `38156759165` (pull_request, head `7940925`) was success at every
  step, parity included, read through the REST API.
- [x] 3.3 Chris merges with "Create a merge commit". Fetch. Verify: `origin/dev/v18` = local `dev/v18` =
  the merge commit, and the merge commit's own `ci` push run is green at every step.
  *Done:* merged as **`a834f1d`** (parents `836c653` and the branch tip). After a fetch, local
  `dev/v18` = `origin/dev/v18` = `a834f1d`. Its push run `38157077931` was success.
  **4.0's caveat:** the merge's first parent is `836c653`, so `dev/v18` had not moved. The merge
  changes only `CHANGELOG.md`, `Directory.Build.props` (`<Version>`), `README.md`,
  `docs/publishing.md` and this change folder, so 4.0's check holds for the tagged commit
  without a re-run.

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
  - **Caveat (QA round 1 NIT):** this ran on the branch, not on the merge commit. It stands only if
    the merge brings in no code commit from `dev/v18`. Re-check at 3.3: if `origin/dev/v18` has
    moved past `836c653` with anything that is not documentation, changelog or openspec, repeat
    this check before tagging.

- [x] 4.1 Chris pushes tag `18.1.3` on the merge commit. Verify: `git ls-remote` shows it, and a
  `publish` run started with `check` and `pack` green.
  *Done:* `git ls-remote` shows `a834f1d… refs/tags/18.1.3`. Publish run **`38158635211`**: `check` and
  `pack` were success, and `publish` was waiting.
- [x] 4.2 **Before approving**, from the run's `packages` artifact:
  - the tagged `booking-flow.png` `curl` returns 200;
  - exactly 5 `.nupkg` and 4 `.snupkg`, all `18.1.3`;
  - all five nuspecs read: authors, copyright, licence, readme, icon, project URL, and the
    repository commit = the merge commit;
  - **D2:** every `Umbraco.Cms.*` dependency is `[18.2.1, 19.0.0)`; EF Core SqlServer is `10.0.11`;
    nothing declares ImageSharp;
  - the packed README is byte-identical to `README.md` at the tag; every address in it returns
    200; every repository address is pinned to `18.1.3`.

  If anything is wrong, reject the run and stop.
  *Done, nothing wrong.* The artifact was downloaded by Chris to `artifacts/packages-v18` (gitignored).
  - Exactly 5 `.nupkg` and 4 `.snupkg`, all `18.1.3`; each `.snupkg` holds its own
    `lib/net10.0/<id>.pdb`.
  - All five nuspecs: authors `Norwood Design & Development Ltd.`, copyright `Copyright ©
    Norwood Design & Development Ltd.`, licence `MIT` (expression), `README.md` and `icon.png`
    declared and present, project URL `https://github.com/Chris-N2/UBookIt`, repository commit
    `a834f1d…` (the tagged merge commit). `umbraco-marketplace` is on `UBookIt` only, and its
    description says "for Umbraco 18".
  - **D2:** every `Umbraco.Cms.*` dependency (Backoffice 4, Web 2, Persistence 1) is
    `[18.2.1, 19.0.0)`. `Microsoft.EntityFrameworkCore.SqlServer` is `10.0.11`. No nuspec mentions
    ImageSharp.
  - The packed README is byte-identical in all five packages to `README.md` at tag `18.1.3`. It
    says "uBookIt is at `18.1.3`", and all 19 distinct addresses return 200. Every repository
    address is pinned to `18.1.3`. The tagged `booking-flow.png` returns 200.
- [x] 4.3 Chris approves in `release`. Record the run summary table (Chris pastes it), whether any
  `.snupkg` answered 409 and the exact CLI text, and the conclusion.
  *Done.* Chris approved, and the run concluded **success** at 17:26:47Z. The summary table, as
  Chris pasted it:

  | File | Result |
  |---|---|
  | UBookIt.Backoffice.18.1.3.nupkg | published |
  | UBookIt.Backoffice.18.1.3.snupkg | symbols submitted |
  | UBookIt.Core.18.1.3.nupkg | published |
  | UBookIt.Core.18.1.3.snupkg | symbols submitted |
  | UBookIt.Persistence.18.1.3.nupkg | published |
  | UBookIt.Persistence.18.1.3.snupkg | symbols submitted |
  | UBookIt.Web.18.1.3.nupkg | published |
  | UBookIt.Web.18.1.3.snupkg | symbols submitted |
  | UBookIt.18.1.3.nupkg | published |

  **No `.snupkg` answered 409**, so the feed's wording for a still-pending symbol package is
  still unobserved, and that obligation stays open.
- [x] 4.4 Confirm `18.1.3` per package on `api.nuget.org/v3-flatcontainer/<id>/index.json`, and that
  the live package page renders its readme.
  *Done:* the flat container, polled every 30 s, listed all five at `18.1.3` between 17:31:44Z and
  17:31:46Z (times UTC). `nuget.org/packages/UBookIt/18.1.3` returns 200, and its rendered readme
  says "uBookIt is at 18.1.3" and carries "How it's built". The live `ubookit.nuspec` has version
  `18.1.3`, `umbraco-marketplace`, "for Umbraco 18" and commit `a834f1d`. The live
  Backoffice nuspec declares `[18.2.1, 19.0.0)`.

## 5. Close out, in this order

- [x] 5.1 Stamp the entry's date.
  *Done:* `## 18.1.3 — 2026-10-11`.
- [x] 5.2 Sibling sweep for sentences this release falsifies: `docs/`, `README.md`,
  `openspec/specs/**`, `CLAUDE.md`. Search for `18.1.2`, `18.2.0`, `18.2.1` and `ImageSharp`.
  Fix or record each.
  *Done:* nothing falsified. The search covered `18.1.2`, `18.2.0`, `18.2.1` and `ImageSharp`.
  What remains:
  - the new floor itself, in the README opening and requirements row;
  - `docs/publishing.md`'s audit note, which is still true: only `UBookIt.TestSite` accepts the
    ImageSharp advisories;
  - `packaging`'s `18.2.0`, a past-tense illustration of a NuGet minimum. The deferred
  obligation from `umbraco-security-2026-10` about the direct-link view is discharged as "did not
  reproduce" (`release-18-1-3` 4.0).
- [x] 5.3 Commit on `dev/v18` and run the unit suite locally; Chris pushes. Verify: `dev/v18`'s CI run is
  green at every step.
  *Done:* the record commit is local; the unit suite ran on it, and its result is in the commit
  message. Chris pushes it together with the archive (5.4), so the push's CI run is checked in
  the session rather than recorded here.
- [x] 5.4 Archive **last**, running the unit suite locally before the push.
  *Done:* archived with this box ticked, after the unit suite ran locally. The push's CI run is
  checked in the session, because this record is frozen by then.
