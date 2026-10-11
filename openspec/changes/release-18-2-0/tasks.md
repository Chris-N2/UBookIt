## 1. The version and the entry

- [ ] 1.1 Branch `release-18-2-0` from local `dev/v18` at `0c62245` (the `closure-date-ranges`
  archive). Bump `<Version>` to `18.2.0` and move every row of `docs/publishing.md` step 2's table.
  Re-read the *Tag the release* literals by eye. Verify: `git grep 18.1.3` over README and the
  runbook is empty.
- [ ] 1.2 Write the `18.2.0` entry, undated. **Check every claim against the code and the archived
  change, not against this list.** Verify: `ChangelogTests` passes.

## 2. Clean build and suites

- [ ] 2.1 Stop the TestSite. Clean the outputs, build the client, then
  `CI=true dotnet build UBookIt.slnx -c Release --no-incremental`. Verify: 0 warnings.
- [ ] 2.2 Run the suites per project. Verify: unit **2120**, integration **207**, rendering
  **1178**, client **346**, each with 0 skipped; `openspec validate --all --strict` passes.

## 3. Review and merge

- [ ] 3.1 QA (`qa-review`) in one subagent, reused across rounds, covering this change and
  `release-18-2-0`. Verify: APPROVE.
- [ ] 3.2 Chris pushes `dev/v18` (the archive), then this branch, and opens the PR into `dev/v18`. Verify: the PR's CI
  is green at every step, parity included.
- [ ] 3.3 Chris merges with "Create a merge commit". Verify: `origin/dev/v18` is the merge commit, and
  its push run is green.

## 4. Release through the workflow

- [ ] 4.1 Chris pushes tag `18.2.0` on the merge commit. Verify: `check` and `pack` are green.
- [ ] 4.2 **Before approving**, from the run's `packages` artifact:
  - the tagged `booking-flow.png` returns 200;
  - there are exactly 5 `.nupkg` and 4 `.snupkg`, all `18.2.0`;
  - all five nuspecs carry the authors, copyright, licence, readme, icon, project URL and the
    merge commit;
  - every `Umbraco.Cms.*` dependency is `[18.2.1, 19.0.0)`, and EF Core SqlServer is `10.0.11`;
  - the packed README is identical to the tag's, all its addresses return 200, and every
    repository address is pinned to `18.2.0`.
- [ ] 4.3 Chris approves. Record the summary table and the conclusion.
- [ ] 4.4 Confirm `18.2.0` per package on the flat container, and that the package page renders.

## 5. Close out

- [ ] 5.1 Stamp the entry's date.
- [ ] 5.2 Sibling sweep for `18.1.3` and for sentences the release falsifies. Fix or record each.
- [ ] 5.3 Commit; run the unit suite; Chris pushes. Verify: `dev/v18`'s CI is green.
- [ ] 5.4 Archive last, running the unit suite before the push.
