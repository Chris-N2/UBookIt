## 1. The version and the entry

- [x] 1.1 Branch `release-17-7-0` from `origin/main` at `7382f61` (the `umbraco-floor-17-8`
  close-out merge), with no upstream.
  - Bump `<Version>` to `17.7.0`, and move every row of the table in `docs/publishing.md`
    *Before any push*, step 2.
  - Re-read the *Tag the release* literals by eye, because no guard sees them.
  - **Do not move the history anchors.**

  Verify: `git grep 17.6.0` over `Directory.Build.props`, README and the runbook is empty, apart
  from history anchors. Record the counts before and after.
  *Done (2026-10-29):* before, `17.6.0` appeared 1 / 25 / 5 times in `Directory.Build.props` /
  README / the runbook. Each was read in context: the `<Version>`, 24 tag-pinned README links and
  screenshots (three more than at the last release; `docs-single-source` added links), the two
  *uBookIt is at* sentences, and the four *Tag the release* literals. None is a history anchor.
  After: 0 of `17.6.0`, 1 / 25 / 5 of `17.7.0`, and the diff is exactly those 31 lines. The *Tag the
  release* literals were re-read by eye. The branch has no upstream.
- [x] 1.2 Write the `17.7.0` entry, undated. **Check every claim against the code, the archived
  changes (`2026-10-29-umbraco-floor-17-8`, `2026-10-24-docs-single-source`) and the published
  `17.6.0` nuspecs, not against the proposals.** In particular:
  - the advisories: their count, the high one, the versions that fix them, and where they are
    published;
  - the new floor and the "upgrade `Umbraco.Cms` first" advice, worded as the README words it;
  - the dependency changes per package, against a pack of this commit and nuget.org's `17.6.0`;
  - that removing the declarations lowers nothing a site resolves;
  - the three corrected refusal messages and two switch descriptions, quoted from `en-us.ts`;
  - the hint;
  - the docs restructure: the two new pages, `docs.json`, the install command and the SQL Server
    floor, against the README and docs at this commit;
  - the downgrade claim, **checked against `17.6.0`'s nuspecs**;
  - the three constraints in the proposal (no "never reach the database"; no retired literals;
    `--version "17.*"` named).

  Verify: `ChangelogTests`, `VersionTruthTests` and the documentation tests pass.
  *Done.* Claims against their sources:
  - **Advisories:** `gh api repos/umbraco/Umbraco-CMS/security-advisories`: seven published
    2026-10-27, GHSA-rpv9-qp4c-g469 high, vulnerable `<=17.7.1, <=18.2.1`, patched 17.7.2 / 18.2.2.
  - **Downgrade:** nuget.org's `17.6.0` nuspecs (diffed in `umbraco-floor-17-8`) declare the old
    range and the minimums named, all below what the new Umbraco brings.
  - **Wording:** quoted from `en-us.ts` at this commit.
  - **Docs:** README and `getting-started.md` at this commit (`--version`, SQL Server 2019);
    the 17.6.0 README said only SQL Server.
  - **Every earlier README showed the bare command:** checked across all 23 tags.
  - **Declarations' origin:** this line's own CHANGELOG entries.
  - **The three constraints:** no "never/do not reach"; no retired literals (the sweep passes);
    `--version` named.

  The feed-accounting guard caught a first draft that named nuget.org ("The README on nuget.org has
  shown…"); it was reworded rather than allow-listed. Guards: 171 of 171 on `main`.
  *(v18's install-command paragraph is written for this line: on an Umbraco 18 site a bare
  command works today, so the risk there is a later line, plus Umbraco 17 sites.)*

## 2. Clean build and suites

- [x] 2.1 Stop the TestSite. Clean the outputs, including the stale hashed client assets. Build the
  client, then `CI=true dotnet build UBookIt.slnx -c Release --no-incremental`. Verify: 0 warnings,
  0 errors.
  *Done (2026-10-29) at `4e4e64c`:* no TestSite running. The hashed assets and the backoffice `obj` were deleted, the client built, then `CI=true dotnet build … --no-incremental`: **0 warnings, 0 errors**.
- [x] 2.2 Run the suites per project, one after another. Verify, each with 0 skipped:
  - unit **2496**, integration **222**, rendering **1211**, client **391**;
  - `openspec validate --all --strict` passes, run after the last commit.
  *Done,* Release with `UBOOKIT_TEST_DB_REQUIRED=true`, 0 skipped: unit **2496**, integration **222**,
  rendering **1211**, client **391**. Validate 28/28, to be re-run after the last commit.
- [x] 2.3 Run the audit as `audit.yml` does. Verify: exit 0, **0 accepted** (the first release
  with no accepted advisory since `17.2.3`).
  *Done:* "audited 10 project(s)… (Debug, Release); 0 finding(s) accepted" / "No known advisories in
  the graph.", exit **0**.

## 3. Review and merge

- [x] 3.1 QA (`qa-review`) in one subagent, reused across rounds, covering this change and
  `release-18-6-0`. Brief it that temporary source mutations are allowed if reverted. Verify:
  APPROVE.
  *(Round 1: **REJECT**. The reviewer independently confirmed the advisories, the downgrade minimums
  against the published nuspecs, the dependency claims from the assets files, the wording diff, the
  docs claims (all 26 published READMEs carried the bare command), minor as the class, the 31-line
  bump, the clean builds, every suite count, the audit and validate. Findings:
  - **MAJOR:** the Trusted Publishing observation could not work. The organisation co-owns only
    `UBookIt`, so with the personal policy disabled the libraries' push is refused, and 4.3's
    recovery named the wrong step. I confirmed the owners from nuget.org's search API. **Chris
    deferred the observation** (proposal item 4; 4.3, 5.2 and 5.4 rewritten).
  - **MINOR:** the entry omitted that a site which followed the `17.4.0` advice (MessagePack 3.1.9) fails
    restore with NU1605, because Umbraco now requires 3.1.11. Added under *What you have to do*. The
    earlier entry's wording was re-read to confirm it said 3.1.9.
  - **NIT, taken:** the refusal quote is now "The settings refusals now say", since the closures read
    refusal names two switches.
  - **NIT, recorded:** nothing guards the entry's `--version` literal. It was checked by eye, and is
    left unguarded for a one-off entry.)*
  *(Round 2: **APPROVE WITH NITS**, nothing owed before merge. The reviewer re-checked each fix as new code:
  - the MessagePack paragraph, against the assets files, the 17.4.0 / 18.3.0 entries and the
    versions that exist on nuget.org;
  - the scoped quote, against `en-us.ts`;
  - the deferral, against `publish.yml`'s `push_one`;
  - the round-1 record on both lines, with no placeholder left.

  Its 211/211 guard filter passed and validate was 28/28. Its one NIT, an over-long line in the
  refusal paragraph, was re-wrapped on both lines. The word diff is empty and the guards still pass,
  171/171 each.)*
- [x] 3.2 Push this branch and open the PR. Verify: the PR's CI is green, parity included.
- [x] 3.3 Merge with a merge commit, **without `--delete-branch`**. Verify: `origin/main` is the
  merge commit, and its push run is green.

## 4. Release through the workflow

- [x] 4.1 Chris pushes tag `17.7.0` on the merge commit, **by explicit SHA**. Verify: `check` and
  `pack` are green.
- [x] 4.2 **Before approving**, check the run's `packages` artifact (Chris downloads it to
  `artifacts\packages-v17`), parsing each nuspec as XML:
  - the tagged `booking-flow.png` returns 200;
  - there are exactly 5 `.nupkg` and 4 `.snupkg`, all `17.7.0`, each `.snupkg` holding its `.pdb`;
  - all five nuspecs carry the authors, copyright, licence, readme, icon, project URL and the merge
    commit, with `umbraco-marketplace` on `UBookIt` only;
  - every `Umbraco.Cms.*` dependency is `[17.8.0, 18.0.0)`, confirmed against
    `Directory.Packages.props` at the tag; EF Core SqlServer is `10.0.12`; the dependencies between
    the uBookIt packages are at `17.7.0`;
  - **no package declares `System.Security.Cryptography.Pkcs` or `MessagePack`**; the
    `Microsoft.AspNetCore.App` framework reference is on `UBookIt`, Backoffice and Web; there is no
    `System.Security.Cryptography.Xml`; the backoffice bundle's only localization culture is `en`;
  - **the dependencies differ from the published `17.6.0` packages exactly as recorded in
    `umbraco-floor-17-8` 4.3**, and in no other way;
  - **new this release:** the packed backoffice bundle carries the new refusal wording ("in the box
    headed") and no "Default permissions"; the bundle gates the variable-length hint;
  - the packed README is identical to the tag's, all its addresses return 200, and every repository
    address is pinned to `17.7.0`.

  Show the check script can fail (against the `17.6.0` artifact with this release's expectations).
- [x] 4.3 Chris approves, with **both Trusted Publishing policies left enabled** (the observation is
  deferred, see the proposal's item 4). Record the summary table and the conclusion. If a push is
  refused, the meta-package goes last and so stays unpushed. Fix the cause, then re-run the
  publish job; already-published packages are reported as *already present*.
- [x] 4.4 Confirm `17.7.0` per package on the flat container, and that the package page renders.

## 5. Close out

- [x] 5.1 Stamp the entry's date.
- [x] 5.2 Record in the deferred-obligations memory that the Trusted Publishing observation is
  deferred, and what it waits on: the organisation co-owning all five ids, with Chris's invitations
  accepted. `docs/publishing.md` is unchanged.
- [x] 5.3 Sibling sweep for `17.6.0` and for sentences the release falsifies. Fix or record each.
- [x] 5.4 Update the deferred-obligations memory: the `docs-single-source` changelog obligation is
  paid.
- [x] 5.5 Archive last, running the unit suite and `openspec validate --all --strict` before the
  push.

## Record (2026-10-29)

- **3.2 / 3.3:** PR #67; both `verify` runs passed. Merged with a merge commit and no
  `--delete-branch`, as `51d5d54`. The push run on `main`, `40962982430`, succeeded. The worktree was
  unaffected.
- **4.1:** Chris pushed tag `17.7.0` on `51d5d54` by explicit SHA; `git ls-remote` confirms it. Publish run
  `40964958892`: `check` and `pack` succeeded on the first attempt.
- **4.2:** the tagged `booking-flow.png` answered 200 before approval. Chris downloaded the artifact to
  `artifacts\packages-v17`. The check script (every item in 4.2, nuspecs parsed as XML, dependencies compared
  with nuget.org's `17.6.0`) gave **117 PASS, 0 FAIL, exit 0**.
  - **Its first run failed one check on every package: the repository URL.** The packages carry
    `https://github.com/Chris-N2/UBookIt.git`, the clone form, exactly as `17.6.0` did. The script
    wrongly expected it without `.git`, so the expectation was fixed, not the packages.
  - While writing it, a hint-gate pattern was found to match `17.6.0` too. It was replaced by one that
    requires the conditional, and a crash now fails the run (exit 2) instead of passing.
  - **Negative control:** nuget.org's published `17.6.0` packages, with this release's expectations,
    gave **23 FAIL, exit 1**. The old artifact zip had been cleared, so the published packages were
    used instead.
- **4.3:** Chris approved, with both Trusted Publishing policies enabled (the observation is
  deferred). Run `40964958892` concluded **success**: check, pack and publish.
- **4.4:** `17.7.0` is in the flat-container index of all five ids. All ten packages across both lines
  appeared about 7 minutes after publish. `nuget.org/packages/UBookIt/17.7.0` answers 200 to GET.
- **5.1:** the entry is stamped `## 17.7.0 — 2026-10-29`.
- **5.2 / 5.4:** recorded in the deferred-obligations memory: the `docs-single-source` changelog
  obligation is paid; the Trusted Publishing observation is deferred until the organisation co-owns
  all five ids, which Chris will arrange before the next release.
- **5.3:** outside `CHANGELOG.md` and `openspec`, `17.6.0` appears only as history (the "Added in"
  doc comments on `BookingMessageModel`, and test comments). Nothing to move.
- **5.5:** archived on a close-out branch; unit suite and `openspec validate --all --strict` run before the push.