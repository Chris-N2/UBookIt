## 1. The list and the parity check (design D1, D2, D3)

- [x] 1.1 Add `scripts/ci/published-lines.json`, `["main", "dev/v18", "dev/v19"]`. Verify: it parses
      as a JSON array of three distinct strings.

      **Done.** `scripts/ci/published-lines.json` is `["main", "dev/v18", "dev/v19"]`. It is read
      through a new dot-sourced helper, `scripts/ci/PublishedLines.ps1` (`Read-PublishedLines`). The
      helper refuses a missing, non-JSON, non-array or empty list, a non-string or blank entry, or a
      name listed twice, and keeps a one-element list an array (`-NoEnumerate`). One helper, so the
      parity check, the tag check and the audit cannot validate the list differently.

- [x] 1.2 Rework `scripts/ci/Assert-LineParity.ps1`:
      - a `-LinesFile` parameter (default beside the script), which refuses an empty, non-string or
        duplicated list;
      - `-Line` checked against the list. A listed line is compared with every OTHER listed line's
        `origin/<line>`. An unlisted line exits 0, saying it is not a published line. A listed line
        missing from the remote fails, naming it;
      - `global.json` compared as parsed JSON with `sdk.version` removed. A missing or unparseable
        file fails;
      - `Directory.Build.props` keeps its existing `<Version>` rule;
      - every difference annotated with its file and the line it differs from;
      - the description rewritten for N lines and push-only (no "two lines", no "the second push").

      Verify: task 1.3's tests.

      **Done** (`818ed63`).
      - `-Line` is checked against the list, and `-LinesFile` defaults beside the script.
      - An unlisted branch exits 0 with "… is not a published line (…)".
      - Every OTHER listed line is compared, and each failure is collected, so one run names every
        line that differs.
      - A listed line missing from the remote fails, naming it, "must exist on origin".
      - `global.json` is out of the whole-file `git diff` paths. It is compared as `ConvertFrom-Json
        -AsHashtable`, with `sdk.version` removed, then key-sorted and serialised. An unreadable or
        non-object file fails.
      - `Directory.Build.props` keeps the first-`<Version>` rule unchanged.
      - The description is rewritten for N lines, the two exempt values, and push-only with its
        reason.

- [x] 1.3 Add `LineParityScriptTests` (design D6): the real script through `pwsh`, against a throwaway
      git repository with `refs/remotes/origin/<line>` refs. Cases:
      - identical lines pass;
      - a workflow differing on one line fails, naming the file and that line;
      - differing on two lines names both;
      - only `sdk.version` differs: passes;
      - `rollForward` differs: fails;
      - an unparseable `global.json` fails;
      - only `<Version>` differs: passes;
      - another `Directory.Build.props` line differs: fails;
      - an unlisted `-Line` exits 0 with its message;
      - a listed line missing from the remote fails, naming it;
      - an empty list, and a duplicated name, fail.

      Verify: all pass. Then mutate the script and confirm a named test fails for each mutation:
      (a) skip one other line; (b) drop the `sdk.version` removal; (c) remove the whole of
      `global.json` from the comparison; (d) treat a missing line as a pass. Revert each one, and
      confirm `git status` is clean.

      **Done.** New support: `Support/GitScratchRepository.cs` (a throwaway repo; whole-tree commits;
      `refs/remotes/origin/<line>` set by `update-ref`; its own git identity, with autocrlf and signing
      off) and `Support/PwshScript.cs`. `LineParityScriptTests`: **13/13**, the 11 planned cases plus
      "a reformatted global.json with the same settings passes" and "differing on one line does not
      name the other".

      Mutations (scratchpad `mut_parity.py`; each reverted with `git checkout`, `git status` clean):

      | Mutation | Fails |
      |---|---|
      | (a) compare only the first other line | `Identical_lines_pass`, `A_workflow_differing_on_one_line…`, `A_file_differing_on_two_lines…`, `A_listed_line_missing…` |
      | (b) no `sdk.version` removal | `Only_the_sdk_version_differing_passes` |
      | (c) `global.json` never compared | `A_different_roll_forward_fails` |
      | (d) a missing line passes | `A_listed_line_missing_from_the_remote_fails_naming_it` |


## 2. The release tag (design D4)

- [x] 2.1 `scripts/ci/Assert-ReleaseTag.ps1`:
      - the lines come from the list (a `-LinesFile` parameter, default beside the script);
      - the shape is the D4 pattern, with the major still in group 1, and the shape message updated;
      - a new `-ApiBaseUri` parameter (default `https://api.github.com`) for step 4;
      - the description updated (step 1's shape, step 3's "same pair" sentence).

      Verify: task 2.3's tests.

      **Done** (`3b72602`).
      - The lines come from `Read-PublishedLines` (`-LinesFile`, default beside the script). A bad
        list fails as `line`.
      - The shape is `-cnotmatch '^(\d+)\.(\d+)\.(\d+)(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$'`
        (case-sensitive), and the major is still group 1.
      - The shape message lists the allowed and refused forms.
      - Step 2's description says "compared whole", and step 3's says the lines come from the list.
      - New `-ApiBaseUri` parameter, default `https://api.github.com`, used to build `$runsUri`.

- [x] 2.2 `.github/workflows/publish.yml`: add the trigger filter `'[0-9]+.[0-9]+.[0-9]+-*'` beside the
      existing one, and update the header and `check` job comments that name two lines. Verify:
      `PublishingWorkflowTests` passes, and the YAML parses as one plain document.

      **Done.** Trigger `tags: ['[0-9]+.[0-9]+.[0-9]+', '[0-9]+.[0-9]+.[0-9]+-*']`, with a comment that
      `1.2.3-` starts the workflow and the check refuses it. The header ("IDENTICAL on every published
      line … ONE … policy serves them all") and the `check` job's "origin/<line> for each line in the
      list" are updated. `PublishingWorkflowTests` gives **5/5**, including the one-plain-YAML-document
      test.

- [x] 2.3 Add `ReleaseTagScriptTests` (design D6), with the API pointed at a closed local port and
      `-WaitMinutes 0`, so nothing leaves the machine. Cases:
      - shape refusals: `v17.2.2`, `17.2`, `17.2.2-`, `17.2.2-rc..1` and `17.2.2+build.5` each fail
        on "shape";
      - `19.0.0` on a commit declaring `19.0.0-rc1`, and the reverse, fail on "version";
      - `19.0.0-rc1` on a commit contained in `origin/dev/v19`, whose tip declares 19, passes steps
        1–3. The output names `dev/v19` as contained, then fails at step 4 on the API, not before;
      - a 19 tag on a commit only in `main` fails on "line";
      - a major no listed line has fails on "line";
      - two listed lines declaring the same major fail as ambiguous.

      Verify: all pass, and no case touches the network beyond loopback. Then mutate:
      (a) the original `^(\d+)\.(\d+)\.(\d+)$` refuses `19.0.0-rc1`, so the positive case fails;
      (b) dropping the label's identifier rule lets `17.2.2-rc..1` pass;
      (c) a hard-coded `@('main','dev/v18')` makes the `dev/v19` case fail.
      Revert each one, and confirm `git status` is clean.

      **Done.** `ReleaseTagScriptTests` gives **14/14**. Each run uses `-ApiBaseUri http://127.0.0.1:1`
      (a closed loopback port), `-WaitMinutes 0` and `-PollSeconds 0`. The fixture history is a root,
      plus each line forked from it (main 17.7.0 by way of a 19.0.0-rc2 commit, dev/v18 18.6.0,
      dev/v19 19.0.0-rc1). Cases:
      - shape refusals: `v17.2.2`, `17.2`, `17.2.2-`, `17.2.2-rc..1`, `17.2.2-rc1.`,
        `17.2.2+build.5`, `17.2.2-rc1+build.5` and `17.2.2-rc_1`;
      - version both ways;
      - **the positive case:** `19.0.0-rc1` gives "matches." and "is contained in origin/dev/v19.",
        with no shape, version or line error, then "verified … could not be read from the GitHub
        API";
      - line not containing the commit;
      - a major no line has;
      - ambiguous major (a 4-line list).

      Mutations (scratchpad `mut_tag.py`; each reverted, `git status` clean):

      | Mutation | Fails |
      |---|---|
      | (a) the old `^(\d+)\.(\d+)\.(\d+)$` | the positive case, version-reverse, line, ambiguous: 4 |
      | (b) label `(?:-[0-9A-Za-z.-]+)?` | the shape cases `17.2.2-rc..1` and `17.2.2-rc1.` |
      | (c) hard-coded `@('main','dev/v18')` | the positive case, line, no-line, ambiguous: 4 |

- [x] 2.4 Confirm that `Assert-PackedRelease.ps1` needs no change for a prerelease. Read its version
      and file-name handling against `<Id>.19.0.0-rc1.nupkg` and `<Id>.19.0.0-rc1.snupkg`, and a
      nuspec `<version>19.0.0-rc1</version>`. Verify: the record names the lines read. If anything
      would refuse a prerelease, stop and raise it.

      **Done, no change needed.** `Assert-PackedRelease.ps1` builds the expected names as
      `"$($package.PackageId).$Tag.nupkg"` / `.snupkg` (lines 164-165) and compares the nuspec
      `<version>` with `"$($metadata.version)" -ne $Tag` (line 188). Both are string operations on the
      tag as given, with no `[version]` cast, `-split` or numeric parse anywhere in the file (grep).
      `dotnet pack` names `UBookIt.19.0.0-rc1.nupkg` and writes `<version>19.0.0-rc1</version>` as
      declared, so a prerelease passes through unchanged.


## 3. ci.yml, the audit, and the guards (design D1, D3, D5, D6)

- [x] 3.1 `.github/workflows/ci.yml`:
      - pull requests `branches: [main, 'dev/v*']`;
      - the concurrency group and `cancel-in-progress` use `main` or
        `startsWith(github.ref, 'refs/heads/dev/v')`;
      - the parity step's `if` is `github.event_name == 'push'` and a published-line pattern, with
        `-Line "${{ github.ref_name }}"`;
      - the header and step comments are updated for N lines and push-only parity.

      Verify: `PublishingWorkflowTests` and `NodeEngineGuardTests` pass, and the YAML parses.

      **Done** (`61cf394`).
      - PR `branches: [main, 'dev/v*']`.
      - The concurrency group and `cancel-in-progress` use `github.ref == 'refs/heads/main' ||
        startsWith(github.ref, 'refs/heads/dev/v')`.
      - The parity step is renamed "Parity with the other published lines". Its `if:` is
        `!cancelled() && github.event_name == 'push' && (main || startsWith dev/v)`, and it runs
        with `-Line "${{ github.ref_name }}"`; the `base_ref` arms are removed.
      - The header and comments now cover the list, the pattern, the two exempt values and
        push-only.
      - `PublishingWorkflowTests` 5/5 (the YAML parses as one document); `NodeEngineGuardTests`
        green.

- [x] 3.2 `.github/workflows/audit.yml`:
      - a `lines` job outputs the list;
      - the matrix is `fromJSON` of that output;
      - `setup-dotnet` uses `line/global.json`;
      - the audit step runs with `working-directory: line`, calling
        `../scripts/ci/Assert-NoVulnerablePackages.ps1 -Target UBookIt.slnx`;
      - the comments are updated.

      Before relying on the working directory, read `Assert-NoVulnerablePackages.ps1` for any
      dependence on it, and record what you find. Verify: `AuditScriptTests` and
      `PublishingWorkflowTests` pass (every action is still pinned), and the YAML parses.

      **Done.**
      - **Working-directory read first:** `Assert-NoVulnerablePackages.ps1` uses its working
        directory only to resolve `-Target`. Its `dotnet msbuild $path` calls take `path` from
        `dotnet list`'s JSON report, which gives absolute paths. There is no `Get-Location`,
        `$PWD`, `Resolve-Path` or `Set-Location`. So running it from inside `line/` is safe.
      - **A new `lines` job:** it checks out the default branch, dot-sources `PublishedLines.ps1`,
        and writes `lines=<ConvertTo-Json -InputObject … -Compress>` to `GITHUB_OUTPUT`. Simulated
        locally, that gives `lines=["main","dev/v18","dev/v19"]`, and a one-line list stays
        `["main"]`.
      - **The `audit` job** has `needs: lines` and a matrix of
        `fromJSON(needs.lines.outputs.lines)`. `setup-dotnet` uses `line/global.json`, and the
        audit step uses `working-directory: line` and
        `../scripts/ci/Assert-NoVulnerablePackages.ps1 -Target UBookIt.slnx`. The comment claiming
        "the parity check holds the lines' copies identical anyway" is replaced.
      - **Finding during the simulation:** dot-sourcing the helper set `Set-StrictMode` in the
        CALLER's scope, which broke the calling shell. The fix moves `Set-StrictMode` inside
        `Read-PublishedLines`. Confirmed: after dot-sourcing, the caller's strict mode is off.
      - `AuditScriptTests` and `PublishingWorkflowTests` are green; the new job's checkout is
        pinned by SHA.

- [x] 3.3 Add the static guards (design D6):
      - every listed line matches `^(main|dev/v\d+)$`;
      - the list names `main` *(amended at apply: was "this line's own branch"; see design D6)*;
      - no file under `.github/` or `scripts/ci/`, other than the list, names `dev/v18` or `dev/v19`
        literally.

      Verify: they pass. Then mutate: (a) list `dev/19`; (b) put `dev/v18` back into `ci.yml`'s
      filter; (c) remove `main` from the list. Each must fail a named guard. Revert each one.

      **Done.** New `PublishedLinesTests`, 4/4:
      - every listed name matches `^(main|dev/v\d+)$`;
      - no duplicates;
      - `main` is listed;
      - no `dev/v\d+` appears in any file under `.github/` or `scripts/ci/` except the list.

      **Deviation, recorded in design D6 and in this task:** the guard "the list names this line's
      own branch" cannot be written, because a test cannot know its line (CI checks out detached).
      A dev line dropping itself from the list is caught by parity, since the list is
      parity-checked.

      All CI-related classes together: **93/93**. Mutations (each reverted, `git status` clean):

      | Mutation | Fails |
      |---|---|
      | (a) `dev/19` listed | `Every_listed_line_is_one_the_workflows_treat_as_published` |
      | (b) `dev/v18` put back into `ci.yml`'s PR filter | `No_CI_definition_names_a_dev_line_except_the_list` |
      | (c) `main` removed | `Main_is_always_a_published_line` |


## 4. Documentation

- [x] 4.1 `docs/publishing.md`:
      - the *Continuous integration* section: lines from the list, parity on pushes only, and the
        SDK exemption;
      - the release-tag check's description: prerelease tags, and the lines from the list;
      - the parity note at ~line 544;
      - a short "publishing a prerelease" note: the version is declared first, the tag equals it,
        and it is published as a prerelease on nuget.org.

      Verify: `DocumentationLinkTests` and the documentation guards pass, and a grep shows no
      remaining "both lines" claim about CI in `docs/`.

      **Done.** `docs/publishing.md`:
      - *Status*: "every push to a published line (`main`, `dev/v18` and `dev/v19`)".
      - *Continuous integration*:
        - a new opening paragraph on the list and the pattern;
        - the identical-definitions paragraph now gives both exempt values;
        - a new paragraph on push-only parity, with the re-run and the release consequence;
        - the `ci` bullet covers any published line, and the `audit` bullet covers every published
          line, from the list, each with its own SDK;
        - the local-build sentence gives each line's SDK band.
      - *Publishing a release*: the tag form with the label; the check's three conditions (shape,
        whole-version, the line from the list including `19.x` on `dev/v19`); and a new subsection,
        *Publishing a prerelease* (declare it first, it is listed as a prerelease, the `release`
        environment's `*.*.*` rule).
      - "One policy serves every line", "One listing serves every line", and the Marketplace file
        "on a `dev/v*` line".

      **Two guards fired on my own text, and both were fixed at the cause:**
      - `VersionTruthTests.Every_mention_of_the_feed_is_accounted_for` found two new unregistered
        "nuget.org" mentions in the prerelease note. They were reworded ("The feed lists it…",
        "Nothing is pushed until…") rather than registered, because neither sentence needs the
        name.
      - `ChangeDeltaIntegrityTests` found no task naming the replaced requirements, which led to
        task 4.2.

      A grep for "both lines" / "either line" in `docs/` now finds nothing about CI.


- [x] 4.2 Diff the guarantees of every requirement this change replaces wholesale, against the
      current `openspec/specs/` text (CLAUDE.md, *Rewriting a requirement destroys guarantees
      silently*). Each SHALL and each scenario is classed as carried, deliberately dropped (with
      its reason in the proposal), or superseded by a stronger claim:
      - `continuous-integration`: "Verification runs unattended on every published line" (renamed
        from "…on both published lines"), "Vulnerability advisories are audited separately from
        the build", and "Every published line carries the same CI definitions" (renamed from "Both
        published lines carry the same CI definitions");
      - `release-publishing`: "A release is published by pushing its tag", and "A release tag is
        checked before anything is built".

      Verify: design.md's *Guarantee diff* accounts for every old SHALL and scenario, checked line
      by line against the current specs, and anything found missing is restored to the delta.

      **Done.** A word-level `difflib` of each old requirement against its delta (scratch, inline),
      printing only the text the OLD version has and the new does not:

      | Requirement | Old-only text, and its disposition |
      |---|---|
      | Verification runs unattended… | `main` or `dev/v18` (×3) and the old title in the cross-reference: generalised to the list and the renamed title. Nothing dropped. |
      | Vulnerability advisories… | "either", "`dev/v18` explicitly", "both lines": generalised to every published line, from the list. Nothing dropped. |
      | Every published line carries… | "(every part except the package version…)": restated as the two exempt values, with "everything else … SHALL still be identical". The names `main`/`dev/v18`, "either", "both lines' tips": generalised. Nothing dropped. |
      | A release is published by pushing its tag | "of the form `<major>.<minor>.<patch>`" and "three dot-separated numbers": superseded by the widened release-tag definition. **`17.2.2-rc1`: deliberately dropped** (proposal *What Changes*). "`v17.2.2`)" is punctuation only; the example is kept. |
      | A release tag is checked… | "commit;" gained ", compared whole, including any prerelease label;". "`main` and `dev/v18`)" is generalised to the list. Nothing dropped. |

      Every old scenario title is present in its delta: `openspec validate --strict` refuses a
      MODIFIED block that omits one. It caught three titles I had reworded, which are now kept
      verbatim beside the new scenarios. The design's *Guarantee diff* matches this table.


## 5. Verify on `main`

- [x] 5.1 Clean Release build (`--no-incremental`), with 0 warnings and 0 errors, and the unit suite,
      one suite at a time. Integration and Rendering read no CI file. Run them only if the build
      changes anything they compile, and record the reason either way. Verify: the totals are
      recorded against `main`'s baseline (2522 unit tests).

      **Done.** `dotnet build UBookIt.slnx -c Release --no-incremental`: **0 warnings, 0 errors**.
      Unit **2553/2553**, which is main's baseline 2522 plus 31 new: `LineParityScriptTests` 13,
      `ReleaseTagScriptTests` 14, `PublishedLinesTests` 4. **Integration and Rendering not run:** this
      change touches no file under `src/`. Its compiled changes are confined to
      `tests/UBookIt.Tests` (three test classes and two `Support/` files), which neither of those
      projects references, and neither reads any CI file.

- [x] 5.2 Run the reworked parity script locally against the real three lines. Use a scratch clone
      in which `refs/remotes/origin/dev/v19` points at the local `dev/v19` carrying this change
      (task 6.2). Verify: it passes for each of the three lines, and fails as expected for a branch
      missing the change.

      **Done**, against the real branches, with no scratch clone needed. The three worktrees share
      one repository, so temporary `refs/remotes/sim/<line>` refs pointed at `ci-three-lines`,
      `ci-three-lines-v18` and `ci-three-lines-v19`. The real script was run in each worktree with
      `-Remote sim`:

      | Run | Result |
      |---|---|
      | main (`5debd70`) | identical to dev/v18 and dev/v19, **exit 0** |
      | dev/v18 (`5604ffb`) | identical to main and dev/v19, **exit 0** |
      | dev/v19 (`3d5a6a9`) | identical to main and dev/v18 (`<Version>` and `sdk.version` differ, as exempt), **exit 0** |
      | negative: `sim/dev/v18` at the OLD `origin/dev/v18`, from dev/v19 | **exit 1**: 7 files named against `sim/dev/v18` (3 workflows, 2 scripts, the list, the helper); main still identical |

      The sim refs were deleted (0 left). **Instrument note:** a first negative run printed `exit 0`,
      because piping through `Select-Object -First 6` stopped pwsh before its exit code was set. It
      was re-run without truncation, giving exit 1.


## 6. Ports (design D7)

- [x] 6.1 `ci-three-lines-v18` from `origin/dev/v18`: copy this change's CI files, tests and docs as
      on `main`, with no edits, plus the change artifacts. Verify:
      `git diff ci-three-lines ci-three-lines-v18 -- .github scripts/ci Directory.Build.props
      global.json` shows only `<Version>`. The unit suite is green, with its total recorded.

      **Done.**
      - `ci-three-lines-v18` was branched from `origin/dev/v18` (`55e1a18`) with no upstream, and
        main's six commits cherry-picked cleanly (`docs/publishing.md` auto-merged).
      - `git diff ci-three-lines ci-three-lines-v18 -- .github scripts/ci Directory.Build.props
        global.json` shows `<Version>` only.
      - The CI sections of `docs/publishing.md` (*Continuous integration* through *Publishing a
        prerelease*) are identical to main's.
      - Release `--no-incremental` build: 0 warnings, 0 errors. Unit **2547/2547** (dev/v18's 2516 +
        31).

- [x] 6.2 `ci-three-lines-v19` from local `dev/v19`, the same way, with `global.json` keeping
      `11.0.100`. Verify: the same diff shows only `<Version>` and `sdk.version`. The unit suite is
      green, with its total recorded.

      **Done.**
      - `ci-three-lines-v19` was branched from local `dev/v19` (`e260835`, the archived Umbraco 19
        port), and the same six commits cherry-picked cleanly.
      - The parity-path diff from main's branch shows exactly `<Version>` 17.7.0 → 19.0.0-rc1 and
        `global.json` `"version": "10.0.300"` → `"11.0.100"`, the two exempt values. The CI docs
        sections are identical.
      - Release `--no-incremental` build: 0 warnings, 0 errors. Unit **2559/2559** (net11.0;
        dev/v19's 2528 + 31).

- [x] 6.3 `openspec validate --all --strict` with the pinned CLI on all three branches, after the last
      commit. Verify: it passes on each.

      **Done.** `npx --yes @fission-ai/openspec@1.13.0 validate --all --strict` gives **28 passed, 0
      failed**, exit 0, with a clean tree, on `ci-three-lines` (`ac083cb`), `ci-three-lines-v18`
      (`3f69b5c`) and `ci-three-lines-v19` (`f05a178`). It was re-run on all three after this
      record's own commit and copy; see the next commit's message.


## 7. QA

- [ ] 7.1 QA (`qa-review`): a fresh subagent, reused across rounds, told what is claimed and asked to
      verify rather than trust. Ask it to probe the D2 JSON comparison, the D4 pattern, and the
      "listed once" guard's reach in particular. Verify: APPROVE or APPROVE WITH NITS, with the nits
      resolved or recorded.

## 8. Rollout (design, Migration Plan)

- [ ] 8.1 Merge `ci-three-lines-v19` into local `dev/v19`, then **push `dev/v19`** for the first time.
      Chris confirms before the push. Verify:
      - the push run builds and tests the Umbraco 19 port on Linux / .NET 11;
      - every activity except parity passes;
      - parity fails, naming `main` and `dev/v18` as lacking the change.

      Any other failure is a finding, and is fixed on `dev/v19` before going on.
- [ ] 8.2 Push `ci-three-lines` and `ci-three-lines-v18`, and open their PRs. Verify: both PR runs are
      green, with the parity step skipped.
- [ ] 8.3 Merge both (CI green, QA approved). Verify: both merge push runs pass parity against every
      line, including `origin/dev/v19`.
- [ ] 8.4 Re-run `dev/v19`'s push run. Verify: it is green, parity included.

## 9. Close

- [ ] 9.1 Record merge SHAs, archive on `main`, and bring the archive to `dev/v18` and `dev/v19` by
      close-out PRs. Update the deferred obligations: strike "CI for three lines"; the next step is
      `release-19-0-0-rc1`, which proves the `release` environment's `*.*.*` rule with the real
      tag. Verify: the lines are level, and openspec validates on each after the last commit.
