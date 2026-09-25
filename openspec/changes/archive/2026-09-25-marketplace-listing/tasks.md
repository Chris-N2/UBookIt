## 1. Baseline on `main`

- [x] 1.1 Create branch `marketplace-listing` from `main`. Rebuild the client, then the solution in
  Release, because the build output on disk is dev/v18's. Run the unit suite and record the total.
  Verify: it is **1980**, and a total of 1991 means stale binaries.
  *Done:* `CI=true dotnet build UBookIt.slnx -c Release` (the build runs the client itself) gave
  0 warnings. Unit: **1980 passed, 0 skipped**.

## 2. The tag (D1)

- [x] 2.1 Write the tag guard in `PackageCompositionTests` (D3), including the more-than-one-package
  precondition. Verify: it **fails** against the current tree, naming `UBookIt.Backoffice`,
  `UBookIt.Core`, `UBookIt.Persistence` and `UBookIt.Web`.
  *Done:* `Only_the_package_a_site_installs_asks_to_be_listed_on_the_marketplace` failed before
  the fix with *"UBookIt.Backoffice, UBookIt.Core, UBookIt.Persistence, UBookIt.Web carry
  'umbraco-marketplace'…"*.
- [x] 2.2 Remove `umbraco-marketplace` from the shared `PackageTags` in `Directory.Build.props`,
  and append it in `src/UBookIt/UBookIt.csproj`. Verify: the guard passes, and the packed nuspecs
  show the tag on `UBookIt` alone (read them, don't infer it).
  *Done:* I packed the five projects to a temporary folder and read each nuspec's `<tags>`.
  `UBookIt`'s ends in `umbraco-marketplace`. The other four read
  `umbraco booking bookings scheduling reservations`. The guard passes.
- [x] 2.3 Prove the guard can fire from the source, one mutation at a time, reverting each:
  (a) append the tag in `UBookIt.Web.csproj`, and it fails naming `UBookIt.Web`;
  (b) remove it from `UBookIt.csproj`, and it fails naming `UBookIt`. Record both failure
  messages in this task.
  *Done:*
  - (a) *"UBookIt.Web carries 'umbraco-marketplace', so the Umbraco Marketplace lists it as
    something to install…"*
  - (b) *"UBookIt does not carry 'umbraco-marketplace', so the package a site installs is not
    listed on the Umbraco Marketplace…"*

  Both mutations were reverted. `git status` showed `UBookIt.Web.csproj` clean, and the
  aggregate's tag line was present again.

## 3. The description (D2)

- [x] 3.1 Write the description guard (D3). Verify: it **passes** on `main` today, because the
  17 line's description is correct. Then set the `<Version>` locally to `18.0.0` and rebuild, and
  it fails naming `UBookIt` and `17`. This is the 18 line's defect reproduced on `main`. Revert
  the version.
  *Done:* `Every_description_names_the_umbraco_major_its_own_version_targets` passed at `17.2.0`.
  At `18.0.0` it failed: *"UBookIt 18.0.0 describes itself as for Umbraco 17."* The version was
  restored.
- [x] 3.2 Derive the major from `$(Version)` in `UBookIt.csproj`, and drop "(LTS)". Verify: the
  packed `UBookIt` description reads "…Umbraco 17…" on `main`. With `<Version>` set locally to
  `18.0.0` it reads "…Umbraco 18…" and the guard passes. With `17.3.0-beta1` the build evaluates
  and still says 17. Revert the version after each.
  *Done:* The `UBookItUmbracoMajor` property is
  `$(Version.Substring(0, $(Version.IndexOf('.'))))`. Packed descriptions, read from the nuspec:
  - `17.2.0` gives "…for Umbraco 17: … Requires Umbraco 17 and SQL Server."
  - `18.0.0` gives "…Umbraco 18… Umbraco 18…"
  - `17.3.0-beta1` gives "…Umbraco 17…"

  The guard passed all three. The version was restored to `17.2.0`.
- [x] 3.3 Prove the no-major branch can fire: remove the interpolated major from the description
  text locally, and the guard fails for `UBookIt` as naming no major. Revert. Record the message.
  *Done:* *"UBookIt 17.2.0 names no Umbraco major, so a site author cannot tell from its
  description which Umbraco it runs on."* Restored, with both interpolations present.

## 4. Documentation

- [x] 4.1 Correct `docs/publishing.md`:
  - line 81: remove "Not submitted" as an outstanding item, and state that listing follows from
    the tag;
  - line 387: only the installable package is tagged, published library versions keep their tag,
    and delisting is unverified until seen.

  Verify: re-read both passages against the new requirement's documentation scenario.
  *Done:* The *Status* bullet now reads *"The Umbraco Marketplace still lists three
  libraries"*: listing follows from the tag with no submission, and delisting has not been seen.
  *After the push* now states:
  - there is nothing to submit;
  - only `UBookIt` is tagged, and the guard enforces it;
  - the meta-package is listed through its children;
  - pushed versions keep their tag, and delisting has not been observed (with the URL to check);
  - the listing's description is the derived `<Description>`, with the `18.0.0`/`18.1.0` history.

  That covers every clause of *What the tag cannot undo is documented*. `VersionTruth`,
  `Changelog`, `Publishing` and `Documentation` tests: 122 passed.
- [x] 4.2 Grep the live tree (excluding the archive and `ref/`) for `marketplace` and
  `Umbraco 17`/`Umbraco 18` in package-facing text, and check each hit is still true. This is the
  sibling-falsification sweep. Record the hits and the verdicts here.
  *Done.* The pattern was `marketplace|Umbraco 1[78].*(LTS)|Requires Umbraco`. Verdicts:
  - `CHANGELOG.md:270` "Requires Umbraco 17.x (LTS)": the `17.0.0` entry, historical and true.
  - `Directory.Packages.props:9` "Umbraco 17 (LTS)": that file is per line, and it's true on
    `main`.
  - `packaging/spec.md:1012` and `PackageCompositionTests.cs:361` "why the Marketplace lists
    uBookIt as running on v17 and v18": they concern the dependency range of the listing that
    stays. This change neither falsifies nor fixes them. Left alone.
  - `packaging/spec.md:520`, `VersionTruthTests.cs:861` and `publishing.md:428` mention a
    Marketplace listing in passing, which is still true.
  - `CLAUDE.md:17` and `openspec/config.yaml:5` are project prose, not package-facing, and
    unaffected.
  - No readme or package description mentions the Marketplace.

## 5. Full verification on `main`

- [x] 5.1 Run the full unit, integration and rendering suites, the client tests, and
  `openspec validate --all --strict`. Verify: all pass, with unit = 1980 + the new tests and no
  skips, and a Release build with 0 warnings.
  *Done:* `CI=true` Release build, 0 warnings. Unit **1982** (1980 + 2), integration **191**,
  rendering **1168**, all with 0 skipped. Client **335**. `openspec validate --all --strict`:
  26/26.
- [x] 5.2 QA review in a subagent (the `qa-review` skill), reused across rounds. Fixes go back
  through apply.
  *Done: round 1 APPROVE* on `fbe36e7`. QA confirmed all four handover claims by running them.
  It also added mutations of its own, all of which fired:
  - the tag put back in the shared props (names all four libraries);
  - a mixed-case tag;
  - "Tested on Umbraco 16." on `UBookIt.Core`.

  Its findings:
  - **Two MINOR, both about this section's plan, not the code.** 6.1's order conflicts and would
    not compile, and 6.4 omitted the dev/v18 spec sync. Both are folded into 6.1–6.4 below.
  - **NIT:** `publishing.md` names `17.2.1`/`18.1.1` in the past tense before they exist. It is
    re-checked at release (6.4).
  - **NIT:** the major derivation throws on a dot-less `<Version>`. That never happens in
    practice, so no action.

## 6. The 18 line

- [x] 6.1 After QA approval, merge to `main` by PR with a merge commit (Chris). On a branch from
  `dev/v18`, **first** port `The_bound_admits_this_major_and_excludes_the_next` verbatim with its
  `using System.Globalization;` and `using System.Text.RegularExpressions;` lines (D4). QA
  round 1 found that the cherry-pick otherwise conflicts, because its hunk's context is that test,
  and fails to compile, because the new guard needs `Regex`. Verify: it passes on `dev/v18`. Then
  set one library's Umbraco ceiling to `20.0.0` locally, and it fails naming that dependency.
  Revert.
  *Done.* The PR was **#2**, merged as `3eeec9d`. Its CI run on `470eed0` passed every step up to
  and including strict validation, and failed only *Parity with the other published line*, as
  expected until dev/v18 carries the props change.

  On branch `marketplace-listing-v18` from `origin/dev/v18` (`6273968`), the port is commit
  `847f102`. It was spliced from `6d48f59`'s file, and `git diff 6d48f59` of the test file came
  out **empty** (byte-identical). After a clean dev/v18 build it passed. Widening
  `Umbraco.Cms.Persistence.EFCore` to `[18.2.0,20.0.0)` failed it: *"UBookIt.Persistence ->
  Umbraco.Cms.Persistence.EFCore '[18.2.0, 20.0.0)' stops at '20.0.0', not '19.0.0'…"*. Reverted.
  - **Trap hit:** the first dev/v18 build failed on stale backoffice static-web-asset hashes from
    main's build, and a "pass" run alongside it came from main's leftover DLL. That result was
    discarded. I cleared `wwwroot/App_Plugins/UBookItBackoffice` and `obj/Release` and rebuilt
    with 0 warnings. Every result here is from that clean build.
  - **Noted, not changed (pre-existing on both lines):** the guard's text for a too-high ceiling
    says *"A ceiling inside Umbraco N refuses releases this line supports"*, which only describes
    a too-low one. The detection is right and the explanation is wrong. It is a candidate for the
    tidy patch.
- [x] 6.2 Cherry-pick the change's code commit(s) onto that branch. The test file should now apply
  cleanly; resolve by hand if not. Rebuild and verify: the packed `UBookIt` description reads
  "…Umbraco 18…", only `UBookIt` carries the tag, and both new guards pass. Mutate the description
  back to a written "Umbraco 17" on `dev/v18` and watch it fail, then revert.
  *Done.* `fbe36e7` cherry-picked cleanly as `97cb7ee`. Packed on dev/v18, `UBookIt 18.1.0`
  reads *"A booking system for Umbraco 18: … Requires Umbraco 18 and SQL Server."* Only it carries
  `umbraco-marketplace`, and the four libraries carry
  `umbraco booking bookings scheduling reservations`. All three guards pass. With "Umbraco 17"
  hand-written it failed: *"UBookIt 18.1.0 describes itself as for Umbraco 17."* That is the
  shipped defect, reproduced and caught. Reverted, and the tree is clean.
- [x] 6.3 Run the full suites on `dev/v18`. Verify: unit = 1991 + 3 (the two new guards and the
  ported bound guard), and all green. Check that `Directory.Build.props` differs between the lines
  only in `<Version>` (`git diff main dev/v18 -- Directory.Build.props`).
  *Done:*
  - `CI=true` Release build, 0 warnings.
  - Unit **1994** (1991 + 3), integration **191**, rendering **1168**, all with 0 skipped.
    Client **335**. `openspec validate --all --strict`: 26/26.
  - `git diff marketplace-listing marketplace-listing-v18` over `Directory.Build.props`,
    `global.json`, `scripts/ci` and `.github/workflows`: only the `<Version>` line.
  - `UBookIt.csproj` is byte-identical across the lines. `docs/publishing.md` differs by the same
    32 lines it did before the change (`6d48f59` vs `origin/dev/v18`), which is the known 18-line
    runbook drift. This change added none.
- [x] 6.4 Chris pushes `main` (this record) and fast-forwards `dev/v18` to
  `marketplace-listing-v18`, both in one sitting, then re-runs main's parity-red run. Verify: the
  CI runs on both lines are green, parity included. Checked by step through the public API, not
  just by the run's overall conclusion.

  This comes **before** the archive, because the archive cannot be edited afterwards. Archiving
  first would leave the record either claiming a CI result nobody had seen or carrying an
  unticked task forever.
  *Done.* Chris pushed both lines. I read every step of both runs through the public API:
  - **`main` `2f74356`, run `36128143883`: success.** The client tests, the Release build, the
    tests, *Every suite ran, nothing skipped*, strict validation and **parity** all succeeded.
  - **`dev/v18` `97cb7ee`, run `36128144025`: success**, with the same steps, parity included.

  The two pushes started in the same second. Main's parity step still saw the new dev/v18 tip, so
  no re-run was needed. The earlier red run on `main` (`3eeec9d`, before dev/v18 was pushed) was
  the expected parity failure.
- [x] 6.5 Archive on `main`. On `dev/v18`:
  - **merge the two ADDED requirements into dev/v18's own `openspec/specs/packaging/spec.md`**,
    which differs from main's, so it is never copied over;
  - copy the archive folder from `main`, so the change records are byte-identical;
  - **delete `openspec/changes/marketplace-listing/`**, which the cherry-pick carried across.

  Verify with `openspec validate --all --strict` on both lines. At release, re-check that
  `publishing.md`'s `17.2.1`/`18.1.1` wording names the versions actually shipped (QA NIT).
  *Done.* The archive is written with this box ticked, so it records only what could be verified
  before the push: strict validation on both lines, after the spec sync. The CI runs for the
  archive push itself happen after the record is frozen. They are checked and reported in the
  session, not claimed here.
