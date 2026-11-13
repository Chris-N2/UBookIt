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

- [ ] 2.1 `scripts/ci/Assert-ReleaseTag.ps1`:
      - the lines come from the list (a `-LinesFile` parameter, default beside the script);
      - the shape is the D4 pattern, with the major still in group 1, and the shape message updated;
      - a new `-ApiBaseUri` parameter (default `https://api.github.com`) for step 4;
      - the description updated (step 1's shape, step 3's "same pair" sentence).

      Verify: task 2.3's tests.
- [ ] 2.2 `.github/workflows/publish.yml`: add the trigger filter `'[0-9]+.[0-9]+.[0-9]+-*'` beside the
      existing one, and update the header and `check` job comments that name two lines. Verify:
      `PublishingWorkflowTests` passes, and the YAML parses as one plain document.
- [ ] 2.3 Add `ReleaseTagScriptTests` (design D6), with the API pointed at a closed local port and
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
- [ ] 2.4 Confirm that `Assert-PackedRelease.ps1` needs no change for a prerelease. Read its version
      and file-name handling against `<Id>.19.0.0-rc1.nupkg` and `<Id>.19.0.0-rc1.snupkg`, and a
      nuspec `<version>19.0.0-rc1</version>`. Verify: the record names the lines read. If anything
      would refuse a prerelease, stop and raise it.

## 3. ci.yml, the audit, and the guards (design D1, D3, D5, D6)

- [ ] 3.1 `.github/workflows/ci.yml`:
      - pull requests `branches: [main, 'dev/v*']`;
      - the concurrency group and `cancel-in-progress` use `main` or
        `startsWith(github.ref, 'refs/heads/dev/v')`;
      - the parity step's `if` is `github.event_name == 'push'` and a published-line pattern, with
        `-Line "${{ github.ref_name }}"`;
      - the header and step comments are updated for N lines and push-only parity.

      Verify: `PublishingWorkflowTests` and `NodeEngineGuardTests` pass, and the YAML parses.
- [ ] 3.2 `.github/workflows/audit.yml`:
      - a `lines` job outputs the list;
      - the matrix is `fromJSON` of that output;
      - `setup-dotnet` uses `line/global.json`;
      - the audit step runs with `working-directory: line`, calling
        `../scripts/ci/Assert-NoVulnerablePackages.ps1 -Target UBookIt.slnx`;
      - the comments are updated.

      Before relying on the working directory, read `Assert-NoVulnerablePackages.ps1` for any
      dependence on it, and record what you find. Verify: `AuditScriptTests` and
      `PublishingWorkflowTests` pass (every action is still pinned), and the YAML parses.
- [ ] 3.3 Add the static guards (design D6):
      - every listed line matches `^(main|dev/v\d+)$`;
      - the list names this line's own branch: the line whose major is `Directory.Build.props`'s
        major, read from the list's own naming rule;
      - no file under `.github/` or `scripts/ci/`, other than the list, names `dev/v18` or `dev/v19`
        literally.

      Verify: they pass. Then mutate: (a) list `dev/19`; (b) put `dev/v18` back into `ci.yml`'s
      filter; (c) remove this line from the list. Each must fail a named guard. Revert each one.

## 4. Documentation

- [ ] 4.1 `docs/publishing.md`:
      - the *Continuous integration* section: lines from the list, parity on pushes only, and the
        SDK exemption;
      - the release-tag check's description: prerelease tags, and the lines from the list;
      - the parity note at ~line 544;
      - a short "publishing a prerelease" note: the version is declared first, the tag equals it,
        and it is published as a prerelease on nuget.org.

      Verify: `DocumentationLinkTests` and the documentation guards pass, and a grep shows no
      remaining "both lines" claim about CI in `docs/`.

## 5. Verify on `main`

- [ ] 5.1 Clean Release build (`--no-incremental`), with 0 warnings and 0 errors, and the unit suite,
      one suite at a time. Integration and Rendering read no CI file. Run them only if the build
      changes anything they compile, and record the reason either way. Verify: the totals are
      recorded against `main`'s baseline (2522 unit tests).
- [ ] 5.2 Run the reworked parity script locally against the real three lines. Use a scratch clone
      in which `refs/remotes/origin/dev/v19` points at the local `dev/v19` carrying this change
      (task 6.2). Verify: it passes for each of the three lines, and fails as expected for a branch
      missing the change.

## 6. Ports (design D7)

- [ ] 6.1 `ci-three-lines-v18` from `origin/dev/v18`: copy this change's CI files, tests and docs as
      on `main`, with no edits, plus the change artifacts. Verify:
      `git diff ci-three-lines ci-three-lines-v18 -- .github scripts/ci Directory.Build.props
      global.json` shows only `<Version>`. The unit suite is green, with its total recorded.
- [ ] 6.2 `ci-three-lines-v19` from local `dev/v19`, the same way, with `global.json` keeping
      `11.0.100`. Verify: the same diff shows only `<Version>` and `sdk.version`. The unit suite is
      green, with its total recorded.
- [ ] 6.3 `openspec validate --all --strict` with the pinned CLI on all three branches, after the last
      commit. Verify: it passes on each.

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
