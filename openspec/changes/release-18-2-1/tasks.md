## 1. The version and the entry

- [ ] 1.1 Branch `release-18-2-1` from local `dev/v18` at `a541c78` (the `tidy-2026-10` archive).
  - Bump `<Version>` to `18.2.1`, and move every row of the table in `docs/publishing.md`
    *Before any push*, step 2.
  - Re-read the *Tag the release* literals by eye, because no guard sees them.
  - **Do not move the history anchors.**

  Verify: `git grep 18.2.0` over `Directory.Build.props`, README and the runbook is empty. Before
  the bump the counts were 1, 21 and 5.
- [ ] 1.2 Write the `18.2.1` entry, undated. **Check every claim against the code and the archived
  `tidy-2026-10`, not against the proposal.** In particular:
  - quote the new summary and refusal wording from `en-us.ts`;
  - say what the 404 repair restores and since when (`18.1.0` on this line);
  - make no claim about CI.

  Verify: `ChangelogTests` passes.

## 2. Clean build and suites

- [ ] 2.1 Stop the TestSite. Clean the outputs, build the client, then
  `CI=true dotnet build UBookIt.slnx -c Release --no-incremental`. Verify: 0 warnings.
- [ ] 2.2 Run the suites per project, one after another. Verify, each with 0 skipped:
  - unit **2141**, integration **210**, rendering **1178**, client **355**;
  - `openspec validate --all --strict` passes (26 specs, plus this change).

## 3. Review and merge

- [ ] 3.1 QA (`qa-review`) in one subagent, reused across rounds, covering this change and
  `release-17-3-1`. Verify: APPROVE.
- [ ] 3.2 Chris pushes this branch and opens the PR. Verify: the PR's CI is green at every step,
  parity included. The release changes no CI file, so parity has no reason to fail.
- [ ] 3.3 Chris merges with "Create a merge commit". Verify: `origin/dev/v18` is the merge commit, and
  its push run is green.

## 4. Release through the workflow

- [ ] 4.1 Chris pushes tag `18.2.1` on the merge commit. Verify: `check` and `pack` are green.
- [ ] 4.2 **Before approving**, check the run's `packages` artifact:
  - the tagged `booking-flow.png` returns 200;
  - there are exactly 5 `.nupkg` and 4 `.snupkg`, all `18.2.1`, each `.snupkg` holding its `.pdb`;
  - all five nuspecs carry the authors, copyright, licence, readme, icon, project URL and the
    merge commit, with `umbraco-marketplace` on `UBookIt` only;
  - every `Umbraco.Cms.*` dependency is `[18.2.1, 19.0.0)`, and EF Core SqlServer is `10.0.11`.
    These floors are unchanged since `18.2.0`. Confirm them against `Directory.Packages.props`
    at the tag, not this list;
  - the packed README is identical to the tag's, all its addresses return 200, and every
    repository address is pinned to `18.2.1`.
- [ ] 4.3 Chris approves. Record the summary table and the conclusion.
- [ ] 4.4 Confirm `18.2.1` per package on the flat container, and that the package page renders.

## 5. Close out

- [ ] 5.1 Stamp the entry's date.
- [ ] 5.2 Sibling sweep for `18.2.0` and for sentences the release falsifies. Fix or record each.
- [ ] 5.3 Commit and run the unit suite. Chris pushes. Verify: `dev/v18`'s CI is green.
- [ ] 5.4 Archive last, running the unit suite before the push.
