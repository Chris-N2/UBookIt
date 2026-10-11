## 1. The version and the entry

- [x] 1.1 Branch `release-17-3-0` from local `main` at `2f5150c` (the `closure-date-ranges`
  archive). Bump `<Version>` to `17.3.0` and move every row of `docs/publishing.md` step 2's table.
  Re-read the *Tag the release* literals by eye. Verify: `git grep 17.2.3` over README and the
  runbook is empty.
  *Done:* every `17.2.3` in `Directory.Build.props` (1), README (21) and the runbook (5) moved to
  `17.3.0`. No history anchor named `17.2.3`. QA confirmed the diffs are identical once the version
  is masked.
- [x] 1.2 Write the `17.3.0` entry, undated. **Check every claim against the code and the archived
  change, not against this list.** Verify: `ChangelogTests` passes.

  *Done:* checked claim by claim by QA across three rounds (see 3.1). `ChangelogTests` passes.
## 2. Clean build and suites

- [x] 2.1 Stop the TestSite. Clean the outputs, build the client, then
  `CI=true dotnet build UBookIt.slnx -c Release --no-incremental`. Verify: 0 warnings.
  *Done:* the final clean `CI=true --no-incremental` build, at round 2's tip, had 0 warnings and
  0 errors. QA reproduced it.
- [x] 2.2 Run the suites per project. Verify: unit **2108**, integration **207**, rendering
  **1178**, client **346**, each with 0 skipped; `openspec validate --all --strict` passes.

  *Done, 0 skipped:* unit **2108**, integration **210** (207 plus this release's 3
  downgrade-read tests), rendering **1178**, client **346**; openspec 27/27.
## 3. Review and merge

- [x] 3.1 QA (`qa-review`) in one subagent, reused across rounds, covering this change and
  `release-18-2-0`. Verify: APPROVE.
  *Round 1: REJECT on both changes*, two must-fix findings. QA re-verified the version mentions,
  the history anchors, and both lines' clean builds and suites.
  - **1. Downgrade then upgrade crashes the site.** On the older version a closure created or
    moved gets the column's default end, 0001-01-01. Back on this version, reading it threw
    `closure-range-invalid`, and since every resource read loads closures, the site went down.
    The entry said "nothing breaks".
    - *Fix, in code:* `SiteClosureRowMapper.ToDomain` reads an end before its start as the
      single day the row was written as.
    - *Fix, in the entry:* it now says what a round trip does: single days, and a closure moved
      earlier keeps its old end. It advises checking closures afterwards.
    - *Guard:* `A_closure_written_without_an_end_reads_as_its_one_day_and_does_not_throw` inserts
      the older build's exact row with raw SQL. **Shown to fire:** with the clamp removed it fails
      with the `InvalidOperationException`. Restored.
  - **2.** `docs/delivery-api.md` named `Create` for closed periods; it is now
    `CreateWithClosedPeriods`.
  - **3 (minor).** The entry lists the new failure codes, the wider meaning of
    `duplicate-closure-date`, and the missing members.
  - **4 (minor).** The entry says how a service's page moves: only when every candidate is closed
    today.
  - **5 (nit).** README "What it does" names ranges and closed periods.
  - **6 (nit).** `FirstOpenDate`'s doc comment no longer sits inside `HorizonDays`'s.

  *Round 2: REJECT on both changes.* QA verified fixes 2–6 and judged the clamp acceptable in the
  release, as long as it is complete. It was not.
  - **1. The clamp covered reads only.** Two queries still used the stored end (0001-01-01), and a
    QA probe showed both failing:
    - the Closures view's default "upcoming" filter hid a future closure written while
      downgraded, though it still closed the site;
    - the overlap check missed it, so a new range over its date was accepted.

    *Fix:* both queries now use the end as read, `(c.EndDate < c.Date ? c.Date : c.EndDate)`.
    Tests: `A_closure_written_without_an_end_is_still_listed_as_upcoming` and
    `A_range_over_a_closure_written_without_an_end_is_refused`, each inserting the older build's
    row. **Shown to fire:** reverting each query to the stored end fails exactly its own test, and
    restoring passes all three.
  - **2. The downgrade paragraph was wrong about moves.** The older build's update sets only
    `Date`, so a moved closure keeps its old end. *Fix:* the entry now separates creates (single
    day) from moves (old end kept; one day if moved past it, otherwise from the new start to the
    old end). It notes these closures appear in the view and are guarded against overlap.

  *Round 3: APPROVE WITH NITS on both changes.*
  - QA re-ran clean builds of both lines; they match.
  - It checked every use of `db.SiteClosures` in `src`: each reads through the clamped mapper,
    queries with the corrected end, or works by id.
  - It confirmed the downgrade paragraph against the old build's `ToRow` and update.
  - One optional nit, **taken in QA's own words**: the older build checks overlap by start date
    only, so it can add a closure inside an existing range. The entry now says so, and that
    neither can be edited until one is deleted.

  Three rounds, one reviewer.
- [x] 3.2 Chris pushes `main` (the archive), then this branch, and opens the PR. Verify: the PR's CI
  is green at every step, parity included.
  *Done:* PR **#20**: run `38176138388` (pull_request, head `6488f02`) green at every step, parity
  included.
- [x] 3.3 Chris merges with "Create a merge commit". Verify: `origin/main` is the merge commit, and
  its push run is green.

  *Done:* merged as **`89bdfbf`**. Its push run `38176919903` was green.
## 4. Release through the workflow

- [x] 4.1 Chris pushes tag `17.3.0` on the merge commit. Verify: `check` and `pack` are green.
  *Done:* tag `17.3.0` is on `89bdfbf`. Publish run **`38177197672`**: `check` and `pack` were green,
  and `publish` was waiting.
- [x] 4.2 **Before approving**, from the run's `packages` artifact:
  - the tagged `booking-flow.png` returns 200;
  - there are exactly 5 `.nupkg` and 4 `.snupkg`, all `17.3.0`;
  - all five nuspecs carry the authors, copyright, licence, readme, icon, project URL and the
    merge commit;
  - every `Umbraco.Cms.*` dependency is `[17.7.1, 18.0.0)`, and EF Core SqlServer is `10.0.11`;
  - the packed README is identical to the tag's, all its addresses return 200, and every
    repository address is pinned to `17.3.0`.
  *Done, nothing wrong.* The artifact was downloaded by Chris to `artifacts/packages-v17`.
  - Exactly 5 `.nupkg` and 4 `.snupkg`, all `17.3.0`; each `.snupkg` holds its own `.pdb`.
  - All five nuspecs: authors, copyright, MIT, readme and icon (present in each package), project
    URL, and commit `89bdfbf`. `umbraco-marketplace` is on `UBookIt` only ("for Umbraco 17").
  - Every `Umbraco.Cms.*` dependency is `[17.7.1, 18.0.0)`; EF Core SqlServer is `10.0.11`. Nothing
    mentions ImageSharp.
  - The packed README is identical in all five packages to `README.md` at the tag ("uBookIt is at
    `17.3.0`"). All 19 addresses return 200, and every repository address is pinned to `17.3.0`.
- [x] 4.3 Chris approves. Record the summary table and the conclusion.
  *Done.* Chris approved, and the run concluded **success** at 21:44:26Z. Every `.nupkg` was
  "published", every `.snupkg` "symbols submitted", and `UBookIt` came last. No 409, so the
  pending-symbols wording is still unobserved.
- [x] 4.4 Confirm `17.3.0` per package on the flat container, and that the package page renders.

  *Done:* all five `17.3.0` packages were in the flat container by 21:53:00Z, the 10th of the 10
  across both lines. `nuget.org/packages/UBookIt/17.3.0` returns 200 and says "uBookIt is at
  17.3.0".
  - The README images were broken on GitHub between merge and tag, as they are at every release:
    they pin to a tag that does not exist yet. Pushing the tag fixed them.
## 5. Close out

- [x] 5.1 Stamp the entry's date.
  *Done:* `## 17.3.0 — 2026-10-11`.
- [x] 5.2 Sibling sweep for `17.2.3` and for sentences the release falsifies. Fix or record each.
  *Done:* the previous version's mentions in README and the runbook were moved at 1.1, and only
  the changelog's history keeps them. The feature's own sweep ran at its archive. Nothing is
  falsified.
- [x] 5.3 Commit; run the unit suite; Chris pushes. Verify: `main`'s CI is green.
  *Done:* committed locally after the unit suite ran. Chris pushes on 2026-10-12, so the CI
  check happens in the session that does the push.
- [x] 5.4 Archive last, running the unit suite before the push.

  *Done:* archived with this box ticked.