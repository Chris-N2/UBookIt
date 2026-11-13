## Context

See proposal.md, *Why*. The CI definitions (`.github/`, `global.json`, `scripts/ci/`, and
`Directory.Build.props` apart from `<Version>`) are byte-identical on `main` and `dev/v18`, and on
the local `dev/v19` apart from `global.json`'s SDK `version` (`10.0.300` against `11.0.100`). The
explore found that SDK version is the only legitimate non-version difference between v18 and v19
in the parity paths. Line names are hard-coded in four places:
- `ci.yml`: the pull-request filter, the concurrency group and cancel rule, and the parity `if`;
- `Assert-LineParity.ps1`: a `ValidateSet` of two lines and a two-way "other line";
- `Assert-ReleaseTag.ps1`: `$lines`;
- `audit.yml`: its matrix.

Neither script has a test today. The pattern for testing a CI script is `AuditScriptTests`: the
real script run through `pwsh` against fixtures in a temporary directory.

Facts outside the repository, read with `gh api` during the explore:
- The `release` environment admits tags matching `*.*.*`.
- The branch ruleset protects `~DEFAULT_BRANCH` and `refs/heads/dev/v**` from deletion and
  non-fast-forward pushes, and requires no status checks.
- `dev/v19` is not on origin yet.

## Goals / Non-Goals

**Goals:**
- Three published lines, each verified, parity-checked, audited, and able to publish, with
  `19.0.0-rc1` publishable from `dev/v19`.
- Each line's set of names in one place, so that dropping `dev/v18` at its EOL (June 2027) is one
  edit.
- Tests for the parity and release-tag scripts, which have had none.

**Non-Goals:** see proposal.md, *Non-goals*. Also out of scope at design level:
- changing how any check decides anything other than which lines exist, the SDK exemption, when
  parity runs, and the tag shape;
- the release-tag check's GitHub API wait, its bounds, and its grace period.

## Decisions

### D1. The list is `scripts/ci/published-lines.json`, and the scripts read it beside themselves

The file is a JSON array of branch names: `["main", "dev/v18", "dev/v19"]`. It sits under
`scripts/ci/`, so the parity check holds it identical on every line. A line therefore cannot
disagree with another about which lines exist.

Each script reads the file from `$PSScriptRoot` by default, through a `-LinesFile` parameter that
the tests point at fixtures. In `publish.yml` the scripts already come from the run's own checkout.
In `audit.yml` they come from the default branch's checkout. So the default branch's list decides
which lines are audited, which is the same rule that decides which schedule fires.

The scripts refuse a malformed list (empty, not an array of strings, or a name listed twice) rather
than reading what they can. `ci.yml` cannot read a file in a static filter, so it matches published
lines by pattern: `main`, and `dev/v*` via `startsWith(github.ref, 'refs/heads/dev/v')`. A test
holds every listed name to that pattern, so a line can never be listed that the workflow does not
treat as published.

*Alternatives considered:*
- Derive the lines from the remote (`main` plus every `origin/dev/v*`). Rejected: an end-of-life
  line's branch stays on origin, and would keep receiving parity failures and audits forever.
- A dynamic `ci.yml` job computing the filter. Rejected: GitHub filters are static.

### D2. Parity compares each line with every other listed line

For the pushed line `L`, the check compares HEAD with `origin/<M>` for every other listed `M`, and
reports each difference as a file and a line. `-Line` is validated against the list, not a
`ValidateSet`.

A pushed branch that matches `dev/v*` but is not listed is "not a published line". The check says
so and exits 0, because an unlisted branch is not parity-checked (spec). A listed line missing from
the remote stays a failure, as it is today ("not a pass").

`global.json` leaves the `git diff` path list and is compared on its own. Both sides are parsed as
JSON, `sdk.version` is removed, and the remainder is compared after a canonical re-serialisation.
A missing or unparseable `global.json` on either side fails, naming it. So whitespace differences
are ignored (harmless), and a roll-forward or any other key that differs fails.

`Directory.Build.props` keeps its existing rule: the first whole-line `<Version>` is dropped.

*Alternatives considered:*
- Drop `global.json`'s version line textually, as `<Version>` is dropped. Rejected: the JSON
  comma makes "the line" fragile, and a reformat would fail parity for no reason.
- Exempt `global.json` entirely. Rejected: the roll-forward policy is part of the toolchain pin,
  and must not drift.

### D3. Parity runs on pushes to published lines only

The `ci.yml` parity step's condition becomes: `github.event_name == 'push'` and (`main` or
`dev/v*`), with `-Line ${{ github.ref_name }}`. The `base_ref` arm is removed. The spec states this
as a decision with its reason, and the script's own description is updated to drop "re-run once
the second push has landed", which assumed two lines.

The release-tag check's demand for a successful push run is what makes this safe. A commit whose
push run is red on parity cannot be released until that run is re-run green.

### D4. The tag shape is one anchored pattern in the script; the trigger admits the prerelease form

The pattern in `Assert-ReleaseTag.ps1` is:

`^(\d+)\.(\d+)\.(\d+)(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$`

It matches `<major>.<minor>.<patch>` and an optional label of non-empty, dot-separated identifiers
of letters, digits and hyphens. It rejects:
- an empty label (`17.2.2-`);
- an empty identifier (`17.2.2-rc..1`);
- build metadata (`17.2.2+build.5`);
- a prefix (`v17.2.2`).

The major is still capture group 1. The tag is then compared **whole** with the declared
`<Version>`, which is unchanged, so `19.0.0` against `19.0.0-rc1` is a version failure in both
directions.

The spec says "letters, digits and hyphens", deliberately not "as SemVer defines them". SemVer
also forbids leading zeros in numeric identifiers, and this check does not enforce that: the
declared version must match anyway, and `dotnet pack` refuses an invalid version.

`publish.yml`'s trigger gains a second filter, `'[0-9]+.[0-9]+.[0-9]+-*'`. Filters are not anchored
matches, which is why the script re-tests the shape: a tag such as `1.2.3-` starts the workflow,
and the check refuses it.

`Assert-PackedRelease.ps1` compares versions as strings, and its package file names
(`<Id>.<Tag>.nupkg`) hold for `19.0.0-rc1`, so it needs no change. That rests on a recorded reading
(task 2.4), not a test. *(Corrected at QA round 1: this said "A test pins that", and none does.)* The
`release` environment's `*.*.*` rule is left alone (see Risks).

### D5. The audit builds its matrix from the list, and runs each line with the line's own SDK

A first job, `lines`, checks out the default branch, reads `scripts/ci/published-lines.json`, and
outputs it. The audit job's matrix is `line: ${{ fromJSON(needs.lines.outputs.lines) }}`.

`setup-dotnet` takes `line/global.json`, the audited line's own file. The audit step runs with
`working-directory: line`, so `dotnet` resolves the line's `global.json` rather than the default
branch's. It calls `../scripts/ci/Assert-NoVulnerablePackages.ps1 -Target UBookIt.slnx`. The script
is checked for any dependence on the working directory before this is relied on (task 3.2).

The old comment ("the parity check holds the lines' copies identical anyway") is replaced by one
that says why the line's own pin is used.

### D6. Tests run the real scripts against throwaway git repositories

The new `LineParityScriptTests` and `ReleaseTagScriptTests` follow `AuditScriptTests`. They `git
init` a temporary repository, commit fixture CI files, and create `refs/remotes/origin/<line>` with
`git update-ref`. Then they run the real script through `pwsh` with `-LinesFile` and `-Remote
origin`.

For `Assert-ReleaseTag.ps1`, steps 1–3 (shape, version, line) are local. Step 4 calls the GitHub
API. A new `-ApiBaseUri` parameter (default `https://api.github.com`) lets the tests point it at a
closed local port. A test that must pass steps 1–3 then asserts that the script reached step 4
(its "is contained in origin/dev/v19" line) and failed there on the API, never on the network
outside the machine. `-WaitMinutes 0` makes that failure immediate.

Guards that need no script run:
- every listed line matches `main` or `dev/v<digits>`;
- the list has no duplicates, and names `main`;
- no file under `.github/` or `scripts/ci/` names `dev/v18` or `dev/v19` literally, outside the
  list (the spec's "listed once").

Each new guard is proven to fire by a mutation during apply.

*(Amended at apply, task 3.3.)* This said the list must name "the current line". A test cannot
tell which line it runs on: CI checks out a detached commit, and no test has a notion of its own
line. So the guard is `main` is always listed. A `dev/vNN` line that drops itself is caught by
parity instead: the list is a parity-checked file, so that line's copy differs from every other
line's, and the next push to any of them fails.

### D7. Order of work and identity across lines

The change is written on `main` (branch `ci-three-lines`). Its non-version files are then copied
to `dev/v18` and `dev/v19` with no edits, which the parity check itself confirms (Migration Plan).
Each line's `global.json` keeps its own SDK version, which is now exactly the exempt value.

## Risks / Trade-offs

- **[The `release` environment's `*.*.*` rule may not admit `19.0.0-rc1`.]** GitHub uses fnmatch
  rules, under which `*` matches `0-rc1`, so it should. A dry run never reaches the publish job, so
  only the real tag proves it. If it refuses, the publish job does not start and nothing is pushed
  to nuget.org. The fix is a settings change by Chris. `release-19-0-0-rc1` records this.
- **[Removing parity from pull requests removes an early warning.]** A CI change that misses a line
  is found on the push after merge, not before. That push run is red until the change reaches
  every line, and release requires it green. Chris accepted this trade (explore, question 3).
- **[`dev/v19`'s first push is its first CI run on .NET 11 / Linux.]** The Umbraco 19 port has
  only been verified locally on Windows and LocalDB. Its first push run may fail on something
  local runs could not show (SDK 11.0.100 on `setup-dotnet`, or the SQL container with EF Core
  11). That is a true finding, handled as one: fixed on `dev/v19` before any rc1 tag. It does not
  affect `main` or `dev/v18`.
- **[Any line may publish a prerelease.]** A `17.x.y-rc1` tag on `main` would publish if `main`
  declared it. Chris accepted this. The tag is a maintainer's act, and the version must be declared
  in the commit first.
- **[The list is a new single point of truth.]** A typo in it (for example `dev/19`) would drop a
  line from every check at once. The pattern guard (D6) refuses any name that `ci.yml` would not
  treat as a published line, and the `main`-listed guard keeps the LTS line in it. A `dev/vNN` line
  that leaves itself out is caught by parity on the list file itself. *(Corrected at QA round 1:
  this named a "names the current line" guard that D6's amendment had already replaced.)*

## Migration Plan

`dev/v19` is not on origin, and the change must reach all three lines. The order makes every red
run a true red:

1. `ci-three-lines` on `main` is implemented, QA-approved, and **not merged yet**. The same files
   go to `ci-three-lines-v18` (from `dev/v18`) and into local `dev/v19` (via a `ci-three-lines-v19`
   branch, merged locally after QA).
2. **Push `dev/v19`** (its first push). Its run builds and tests the Umbraco 19 port. Parity fails
   against `origin/main` and `origin/dev/v18`, which do not have the change yet. That failure is
   true and expected.
3. Open PRs for `ci-three-lines` into `main` and `ci-three-lines-v18` into `dev/v18`. They are
   green, because parity no longer runs on PRs. Merge both, with QA's approval and green CI.
4. The merge pushes run parity against every line, including `dev/v19`, and pass.
5. **Re-run `dev/v19`'s push run**, which now passes. That green run is what an rc1 tag needs.

Rollback: revert the change on every line. Until then, the old two-line scripts do not know
`dev/v19`, so its tags are refused and its parity is unchecked.

## Guarantee diff of the modified requirements

Each MODIFIED requirement replaces the old one wholesale, so each old SHALL and scenario is
accounted for here.

**continuous-integration, *Verification runs unattended…* (renamed "on every published line"):**
- Unattended on every push to a published line: **carried**, from two named lines to every listed
  line.
- No run on a published line cancelled or replaced: **carried**.
- Other branches verified, and a superseded run may be cancelled: **carried**.
- PRs into a published line verified: **carried**. `dev/v19` is now included.
- Clean clone, full history, one job, the four activities, and "skipped is not passing":
  **carried** word for word.
- The parity parenthetical: **carried**, and now also says parity runs on pushes only.
- Scenarios: all seven are **carried**. *A push to either line is verified* is kept verbatim
  (still true), and *A push to any published line is verified* is **added** beside it. The PR
  scenario names `dev/v19`.

**continuous-integration, *Vulnerability advisories are audited separately…*:**
- "Either published line's graph" becomes "any published line's graph": **carried** (wider).
- "Check `dev/v18` explicitly" becomes "check every published line explicitly, from the list":
  **carried** (wider).
- Each line audited with its own SDK: **added**.
- The non-packable exception, packability taken from the build's own evaluation, every
  configuration, the multi-target rule, the cannot-evaluate rule, and the three evaluate-don't-obey
  bullets: **carried** word for word.
- Scenarios: all nine are **carried**. *The audit reports an advisory on either line* is kept
  verbatim (still true), and *…on any line* is **added** beside it. "Against both lines" becomes
  "every published line". *A line on another .NET major is audited with its own SDK* is
  **added**.

**continuous-integration, *Both published lines carry the same CI definitions* (renamed "Every
published line carries…"):**
- The four kinds of definition held identical: **carried**.
- The version exemption: **carried**. The SDK version is **added** as the second exempt value, and
  "everything else in those files" is stated as still compared, so the exemption cannot widen.
- A push run fails when the definitions differ, naming the files: **carried**, and now also names
  the line.
- A listed line that cannot be found fails rather than passes: **newly stated**. It was the
  script's behaviour but not the spec's.
- On pull requests: **deliberately dropped**. The old requirement did not mention PRs, but
  `ci.yml` ran parity on them. The new text states the drop and its reason (proposal, *What
  Changes*).
- Scenarios: *A CI change landed on one line is flagged* is **carried** for three lines. *Parity
  is restored by landing the change on the other line* keeps its title. Its body now lands the
  change on each other line, and needs the re-run that the script has always documented. Four
  scenarios are **added**: SDK version exempt, roll-forward compared, PR not checked, and missing
  line fails.

**release-publishing, *A release is published by pushing its tag*:**
- The release tag starts the workflow, which is the only route: **carried**.
- Any other tag or branch publishes nothing: **carried**, with the definition of "release tag"
  widened.
- Built from the tagged commit in the same run, never elsewhere: **carried** word for word.
- The scenario *Other refs do not publish* is **superseded**. Its example `17.2.2-rc1` is
  **deliberately removed**: this change exists to publish that shape. Its other example
  (`v17.2.2`) stays, and four refusals are added (`17.2`, `17.2.2-`, `17.2.2-rc..1`,
  `17.2.2+build.5`).
- *A prerelease tag starts publishing* is **added**.
- *A release tag starts publishing* and *What is pushed is what this run built* are **carried**.

**release-publishing, *A release tag is checked before anything is built*:**
- The three fail conditions: **carried**. The version comparison is now explicitly whole,
  including the label, and the published lines are now read from the list instead of being named.
- The bounded wait, and the run ending in anything but success failing: **carried** word for word.
- A failed check leaves nuget.org untouched: **carried**.
- Scenarios: all five are **carried**. Two are **added**: a release tag on a prerelease commit is
  refused in both directions, and a prerelease tag is placed on its line by its major.
