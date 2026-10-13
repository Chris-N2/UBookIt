## 1. The version and the entry

- [x] 1.1 Branch `release-17-3-1` from local `main` at `f0a9a47` (the `tidy-2026-10` archive).
  - Bump `<Version>` to `17.3.1`, and move every row of the table in `docs/publishing.md`
    *Before any push*, step 2.
  - Re-read the *Tag the release* literals by eye, because no guard sees them.
  - **Do not move the history anchors.**

  Verify: `git grep 17.3.0` over `Directory.Build.props`, README and the runbook is empty. Before
  the bump the counts were 1, 21 and 5.
  *Done:* every `17.3.0` there was a current-version sentence or a tag-pinned URL: 1 in
  `Directory.Build.props`, 21 in README, and 5 in the runbook (the "uBookIt is at" sentence and the
  four *Tag the release* literals). All moved to `17.3.1`, and none remain. The *Tag the release*
  literals were re-read by eye. No history anchor named `17.3.0`.
- [x] 1.2 Write the `17.3.1` entry, undated. **Check every claim against the code and the archived
  `tidy-2026-10`, not against the proposal.** In particular:
  - quote the new summary and refusal wording from `en-us.ts`;
  - say what the 404 repair restores and since when;
  - make no claim about CI.

  Verify: `ChangelogTests` passes.
  *Done.* Each claim was checked against the source rather than the proposal:
  - **Summary wording** is quoted from `en-us.ts:729-735`. The old wording comes from the `17.3.0`
    tag: `"Created %0% closures. %1% rows were skipped."`.
  - **Refusal text** is quoted verbatim from `en-us.ts:722-723`.
  - **404 repair, "since 17.2.0":** the holiday import shipped in `17.2.0`. At the `17.3.0` tag a
    thrown 404 reached the `catch`, which set the source-failed state.
  - **The override paragraph:** the element still calls
    `localize.term("ubookitClosures_importSummary", created, skipped)` with the same two
    arguments as at `17.3.0`, so a string override using `%0%`/`%1%` still interpolates.
    `importRefused` is a new key, so the entry tells a site with its own translation to add it.
    - A first draft said an untranslated term "appears in English". **That was checked and
      dropped.** Umbraco's fallback culture is `en` (`UMB_DEFAULT_LOCALIZATION_CULTURE`), while
      uBookIt registers its terms as `en-us` (`section/manifest.ts:224`), and
      `registerLocalization` keys by the exact code. So for a backoffice in another language the
      fallback is unproven. It affects every uBookIt term and predates this release, so it is
      recorded in `ubookit-deferred-obligations` to test live, not fixed in a patch.
  - **No claim about CI.**

## 2. Clean build and suites

- [x] 2.1 Stop the TestSite. Clean the outputs, build the client, then
  `CI=true dotnet build UBookIt.slnx -c Release --no-incremental`. Verify: 0 warnings.
  *Done:* no TestSite was running. Client built, then the clean CI-mode Release build: 0 warnings,
  0 errors.
- [x] 2.2 Run the suites per project, one after another. Verify, each with 0 skipped:
  - unit **2129**, integration **210**, rendering **1178**, client **355**;
  - `openspec validate --all --strict` passes (26 specs, plus this change).

  *Done, 0 skipped, each project on its own and in turn:* client **355**, unit **2129**,
  rendering **1178**, integration **210**; openspec 27/27.
  - The entry's override paragraph was corrected after the full unit run (see 1.2). So
    `ChangelogTests` and `VersionTruthTests` were re-run against the final text: 20 passed.

## 3. Review and merge

- [ ] 3.1 QA (`qa-review`) in one subagent, reused across rounds, covering this change and
  `release-18-2-1`. Verify: APPROVE.
- [ ] 3.2 Chris pushes this branch and opens the PR. Verify: the PR's CI is green at every step,
  parity included. The release changes no CI file, so parity has no reason to fail.
- [ ] 3.3 Chris merges with "Create a merge commit". Verify: `origin/main` is the merge commit, and
  its push run is green.

## 4. Release through the workflow

- [ ] 4.1 Chris pushes tag `17.3.1` on the merge commit. Verify: `check` and `pack` are green.
- [ ] 4.2 **Before approving**, check the run's `packages` artifact:
  - the tagged `booking-flow.png` returns 200;
  - there are exactly 5 `.nupkg` and 4 `.snupkg`, all `17.3.1`, each `.snupkg` holding its `.pdb`;
  - all five nuspecs carry the authors, copyright, licence, readme, icon, project URL and the
    merge commit, with `umbraco-marketplace` on `UBookIt` only;
  - every `Umbraco.Cms.*` dependency is `[17.7.1, 18.0.0)`, and EF Core SqlServer is `10.0.11`.
    These floors are unchanged since `17.3.0`. Confirm them against `Directory.Packages.props`
    at the tag, not this list;
  - the packed README is identical to the tag's, all its addresses return 200, and every
    repository address is pinned to `17.3.1`.
- [ ] 4.3 Chris approves. Record the summary table and the conclusion.
- [ ] 4.4 Confirm `17.3.1` per package on the flat container, and that the package page renders.

## 5. Close out

- [ ] 5.1 Stamp the entry's date.
- [ ] 5.2 Sibling sweep for `17.3.0` and for sentences the release falsifies. Fix or record each.
- [ ] 5.3 Commit and run the unit suite. Chris pushes. Verify: `main`'s CI is green.
- [ ] 5.4 Archive last, running the unit suite before the push.
