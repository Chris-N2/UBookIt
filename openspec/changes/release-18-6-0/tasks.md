## 1. The version and the entry

- [x] 1.1 Branch `release-18-6-0` from `origin/dev/v18` at `777cdf8` (the `umbraco-floor-17-8`
  close-out merge), with no upstream.
  - Bump `<Version>` to `18.6.0`, and move every row of the table in `docs/publishing.md`
    *Before any push*, step 2.
  - Re-read the *Tag the release* literals by eye, because no guard sees them.
  - **Do not move the history anchors.**

  Verify: `git grep 18.5.0` over `Directory.Build.props`, README and the runbook is empty, apart
  from history anchors. Record the counts before and after.
  *Done (2026-10-29):* before, `18.5.0` appeared 1 / 25 / 5 times in `Directory.Build.props` /
  README / the runbook. Each was read in context: the `<Version>`, 24 tag-pinned README links and
  screenshots (three more than at the last release; `docs-single-source` added links), the two
  *uBookIt is at* sentences, and the four *Tag the release* literals. None is a history anchor.
  After: 0 of `18.5.0`, 1 / 25 / 5 of `18.6.0`, and the diff is exactly those 31 lines. The *Tag the
  release* literals were re-read by eye. The branch has no upstream.
- [x] 1.2 Write the `18.6.0` entry, undated. **Check every claim against the code, the archived
  changes (`2026-10-29-umbraco-floor-17-8`, `2026-10-24-docs-single-source`) and the published
  `18.5.0` nuspecs, not against the proposals.** In particular:
  - the advisories: their count, the high one, the versions that fix them, and where they are
    published;
  - the new floor and the "upgrade `Umbraco.Cms` first" advice, worded as the README words it;
  - the dependency changes per package, against a pack of this commit and nuget.org's `18.5.0`;
  - that removing the declarations lowers nothing a site resolves;
  - the three corrected refusal messages and two switch descriptions, quoted from `en-us.ts`;
  - the hint;
  - the docs restructure: the two new pages, `docs.json`, the install command and the SQL Server
    floor, against the README and docs at this commit;
  - the downgrade claim, **checked against `18.5.0`'s nuspecs**;
  - the three constraints in the proposal (no "never reach the database"; no retired literals;
    `--version "18.*"` named).

  Verify: `ChangelogTests`, `VersionTruthTests` and the documentation tests pass.
  *Done.* Claims against their sources:
  - **Advisories:** `gh api repos/umbraco/Umbraco-CMS/security-advisories`: seven published
    2026-10-27, GHSA-rpv9-qp4c-g469 high, vulnerable `<=17.7.1, <=18.2.1`, patched 17.7.2 / 18.2.2.
  - **Downgrade:** nuget.org's `18.5.0` nuspecs (diffed in `umbraco-floor-17-8`) declare the old
    range and the minimums named, all below what the new Umbraco brings.
  - **Wording:** quoted from `en-us.ts` at this commit.
  - **Docs:** README and `getting-started.md` at this commit (`--version`, SQL Server 2019);
    the 18.5.0 README said only SQL Server.
  - **Every earlier README showed the bare command:** checked across all 23 tags.
  - **Declarations' origin:** this line's own CHANGELOG entries.
  - **The three constraints:** no "never/do not reach"; no retired literals (the sweep passes);
    `--version` named.

  The feed-accounting guard caught a first draft that named nuget.org ("The README on nuget.org has
  shown…"); it was reworded rather than allow-listed. Guards: 171 of 171 on `dev/v18`.
  *(v18's install-command paragraph is written for this line: on an Umbraco 18 site a bare
  command works today, so the risk there is a later line, plus Umbraco 17 sites.)*

## 2. Clean build and suites

- [x] 2.1 Stop the TestSite. Clean the outputs, including the stale hashed client assets. Build the
  client, then `CI=true dotnet build UBookIt.slnx -c Release --no-incremental`. Verify: 0 warnings,
  0 errors.
  *Done (2026-10-29) at `42b63da`:* no TestSite running. The hashed assets and the backoffice `obj` were deleted, the client built, then `CI=true dotnet build … --no-incremental`: **0 warnings, 0 errors**.
- [x] 2.2 Run the suites per project, one after another. Verify, each with 0 skipped:
  - unit **2490**, integration **222**, rendering **1211**, client **391**;
  - `openspec validate --all --strict` passes, run after the last commit.
  *Done,* Release with `UBOOKIT_TEST_DB_REQUIRED=true`, 0 skipped: unit **2490**, integration **222**,
  rendering **1211**, client **391**. Validate 28/28, to be re-run after the last commit.
- [x] 2.3 Run the audit as `audit.yml` does. Verify: exit 0, **0 accepted** (the first release
  with no accepted advisory since `18.1.3`).
  *Done:* "audited 10 project(s)… (Debug, Release); 0 finding(s) accepted" / "No known advisories in
  the graph.", exit **0**.

## 3. Review and merge

- [ ] 3.1 QA (`qa-review`) in one subagent, reused across rounds, covering this change and
  `release-17-7-0`. Brief it that temporary source mutations are allowed if reverted. Verify:
  APPROVE.
- [ ] 3.2 Push this branch and open the PR. Verify: the PR's CI is green, parity included.
- [ ] 3.3 Merge with a merge commit, **without `--delete-branch`**. Verify: `origin/dev/v18` is the
  merge commit, and its push run is green.

## 4. Release through the workflow

- [ ] 4.1 Chris pushes tag `18.6.0` on the merge commit, **by explicit SHA**. Verify: `check` and
  `pack` are green.
- [ ] 4.2 **Before approving**, check the run's `packages` artifact (Chris downloads it to
  `artifacts\packages-v18`), parsing each nuspec as XML:
  - the tagged `booking-flow.png` returns 200;
  - there are exactly 5 `.nupkg` and 4 `.snupkg`, all `18.6.0`, each `.snupkg` holding its `.pdb`;
  - all five nuspecs carry the authors, copyright, licence, readme, icon, project URL and the merge
    commit, with `umbraco-marketplace` on `UBookIt` only;
  - every `Umbraco.Cms.*` dependency is `[18.3.0, 19.0.0)`, confirmed against
    `Directory.Packages.props` at the tag; EF Core SqlServer is `10.0.12`; `Microsoft.AspNetCore.OpenApi` is `10.0.12` on Backoffice
    and Web; the dependencies between
    the uBookIt packages are at `18.6.0`;
  - **no package declares `System.Security.Cryptography.Pkcs` or `MessagePack`**; the
    `Microsoft.AspNetCore.App` framework reference is on `UBookIt`, Backoffice and Web; there is no
    `System.Security.Cryptography.Xml`; the backoffice bundle's only localization culture is `en`;
  - **the dependencies differ from the published `18.5.0` packages exactly as recorded in
    `umbraco-floor-17-8` 5.3 (the v18 port)**, and in no other way;
  - **new this release:** the packed backoffice bundle carries the new refusal wording ("in the box
    headed") and no "Default permissions"; the bundle gates the variable-length hint;
  - the packed README is identical to the tag's, all its addresses return 200, and every repository
    address is pinned to `18.6.0`.

  Show the check script can fail (against the `18.5.0` artifact with this release's expectations).
- [ ] 4.3 **Chris disables his personal Trusted Publishing policy on nuget.org** (disable, not
  delete), then approves. Record which policy the run authenticated through, the summary table and
  the conclusion. If `NuGet/login` fails, re-enable the personal policy and re-run the job. No
  version is spent.
- [ ] 4.4 Confirm `18.6.0` per package on the flat container, and that the package page renders.

## 5. Close out

- [ ] 5.1 Stamp the entry's date.
- [ ] 5.2 Record the Trusted Publishing outcome in `docs/publishing.md` (*Trusted Publishing setup*,
  and the *Status* outstanding bullet on ownership), on both lines, in the same words.
- [ ] 5.3 Sibling sweep for `18.5.0` and for sentences the release falsifies. Fix or record each.
- [ ] 5.4 Update the deferred-obligations memory: the `docs-single-source` changelog obligation is
  paid, and so is the Trusted Publishing observation, if seen.
- [ ] 5.5 Archive last, running the unit suite and `openspec validate --all --strict` before the
  push.
