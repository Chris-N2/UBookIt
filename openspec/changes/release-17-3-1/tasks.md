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

- [x] 3.1 QA (`qa-review`) in one subagent, reused across rounds, covering this change and
  `release-18-2-1`. Verify: APPROVE.
  *Round 1: REJECT on both changes.* QA reproduced both clean builds (0 warnings) and every
  suite, with the counts in 2.2. It confirmed the version moves, the anchors, patch eligibility
  (no `.cs` under `src/`, no migration, no package change since `17.3.0`), the 4.2 checklist, and
  that the two entries differ only in the version lines. Every fix below is new text.
  - **1, 2 (MAJOR): two sentences were true for a preview only.** At `17.3.0`, `#import` showed
    `importFailed` ("Those closures could not be created.") on both its error branch and its
    `catch`, never "could not be reached". The refusal paragraph and the 404 paragraph both
    claimed the preview's message for the import too.
    - *Fix:* each paragraph now names each call's old message: a preview said the source could
      not be reached; an import said the closures could not be created. Checked against `17.3.0`'s
      `#preview` and `#import`.
  - **3 (NIT): "session has ended" is not observed.** Umbraco's auth interceptor re-prompts and
    retries a 401 on a GET, and the tidy live checks measured 403 only.
    - *Fix:* the paragraph now leads with the observed cause, a permission removed while the
      Closures view is open. "Session may have ended" stays only inside the quoted message, which
      is what the screen says.
    - `docs/backoffice.md`'s refusal sentence is left as tidy shipped it. That sentence does make a
      behaviour claim: an ended session shows the refusal. For a preview (a GET), Umbraco's auth
      interceptor most likely re-prompts instead, so the claim goes further than was observed.
      It is tidy's approved, merged text, and QA judged leaving it acceptable for a patch. It is
      recorded in `ubookit-deferred-obligations` for the next tidy (QA round 2 NIT).
  - **4 (NIT): the changelog preamble named `17.x` only.** It now reads "uBookIt `17.x` is for
    Umbraco 17 and `18.x` for Umbraco 18", matching the README, on both lines. The preamble is not
    a release entry, so the never-edit rule does not apply, and no test pins it.
  - No published entry was edited. `ChangelogTests` and `VersionTruthTests` re-ran: 20 passed.
  *Round 2: APPROVE WITH NITS on both changes.* QA confirmed both MAJORs fixed on both lines. It
  checked that "used to" holds for every release since the holiday import shipped: the
  `closures-view.element.ts` blob is identical from `17.2.0`/`18.1.0` through `17.2.3`/`18.1.3`, and
  `#preview`/`#import` hash the same at `17.3.0`/`18.2.0`. It re-ran `ChangelogTests` and
  `VersionTruthTests` with a build: 20 passed on each line.
  - **NIT: the re-wrapped preamble line was 126 characters.** *Taken:* re-wrapped to the file's
    width, on both lines.
  - **NIT: the round 1 record understated `docs/backoffice.md`'s session sentence.** *Taken:* the
    record above now says what that sentence claims, and the sentence is recorded as debt.
  - No must-fix. Two rounds, one reviewer.
- [x] 3.2 Chris pushes this branch and opens the PR. Verify: the PR's CI is green at every step,
  parity included. The release changes no CI file, so parity has no reason to fail.
  *Done:* PR **#24**. Its CI run `38452381372` (pull_request) was green at every step, parity included.
- [x] 3.3 Chris merges with "Create a merge commit". Verify: `origin/main` is the merge commit, and
  its push run is green.
  *Done:* merged as **`e111018`**, with the release branch head as its second parent. Its push run `38452993553` was green.

## 4. Release through the workflow

- [x] 4.1 Chris pushes tag `17.3.1` on the merge commit. Verify: `check` and `pack` are green.
  *Done:* tag `17.3.1` on `e111018` (checked on origin with `ls-remote`). Publish run **`38454063673`**:
  `check` and `pack` were green, and `publish` was waiting.
- [x] 4.2 **Before approving**, check the run's `packages` artifact:
  - the tagged `booking-flow.png` returns 200;
  - there are exactly 5 `.nupkg` and 4 `.snupkg`, all `17.3.1`, each `.snupkg` holding its `.pdb`;
  - all five nuspecs carry the authors, copyright, licence, readme, icon, project URL and the
    merge commit, with `umbraco-marketplace` on `UBookIt` only;
  - every `Umbraco.Cms.*` dependency is `[17.7.1, 18.0.0)`, and EF Core SqlServer is `10.0.11`.
    These floors are unchanged since `17.3.0`. Confirm them against `Directory.Packages.props`
    at the tag, not this list;
  - the packed README is identical to the tag's, all its addresses return 200, and every
    repository address is pinned to `17.3.1`.
  *Done, nothing wrong.* Chris downloaded the artifact to `artifacts/packages-v17`. It was
  extracted to a scratch folder and checked by script:
  - exactly 5 `.nupkg` and 4 `.snupkg`, all `17.3.1`; each `.snupkg` holds its own `.pdb`;
  - all five nuspecs: authors, copyright (`©` checked as bytes `C2 A9`), MIT, readme and icon
    (present in each package), project URL, and commit `e111018`. `umbraco-marketplace` is on
    `UBookIt` only;
  - every `Umbraco.Cms.*` dependency is `[17.7.1, 18.0.0)`; EF Core SqlServer is `10.0.11`; the inter-package
    dependencies are at `17.3.1`.
  - the packed README is identical in all five packages to `README.md` at the tag. All 19
    addresses return 200, all 17 repository addresses are pinned to `17.3.1`, and the tagged
    `booking-flow.png` returns 200.
- [x] 4.3 Chris approves. Record the summary table and the conclusion.
  *Done.* Chris approved, and the run concluded **success** at 19:27:11Z. Every `.nupkg` was
  "published", every `.snupkg` "symbols submitted", and `UBookIt` came last (summary pasted by
  Chris). There was no 409, so the pending-symbols wording is still unobserved.
- [x] 4.4 Confirm `17.3.1` per package on the flat container, and that the package page renders.
  *Done:* all five `17.3.1` packages were in the flat container by 19:56:24Z (polled every 30 s;
  `UBookIt.Backoffice` had appeared by 19:44:53Z), about 29 minutes after the run concluded, which is
  slower than last release. `nuget.org/packages/UBookIt/17.3.1` returns 200 and says "uBookIt is at
  17.3.1".

## 5. Close out

- [x] 5.1 Stamp the entry's date.
  *Done:* `## 17.3.1 — 2026-10-13`.
- [x] 5.2 Sibling sweep for `17.3.0` and for sentences the release falsifies. Fix or record each.
  *Done:* `git grep 17.3.0` over `docs/`, README, `openspec/specs` and CLAUDE.md finds nothing
  that names this package's version. The runbook's *Status* section names only the
  `17.0.0` history anchor. Nothing is falsified.
- [ ] 5.3 Commit and run the unit suite. Chris pushes. Verify: `main`'s CI is green.
- [ ] 5.4 Archive last, running the unit suite before the push.
