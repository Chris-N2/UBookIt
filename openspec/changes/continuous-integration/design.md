## Context

See proposal.md — *Why*. The facts that shape the design, all measured on 2026-09-24:

- **The build already sequences the client.** `UBookIt.Backoffice.csproj` runs `npm ci` (when
  `node_modules` is absent) and `npm run build` in its `UBookItBuildClient` target, then adds the
  output to `@(Content)` before static-web-asset discovery. A clean clone therefore needs no manual
  client step for the *build*; the stale-hash trap that recurs on the dev machine cannot occur on a
  fresh runner.
- **Warnings-as-errors keys on `CI`.** `Directory.Build.props` sets `TreatWarningsAsErrors` when
  `CI` or `TF_BUILD` is `true`, and GitHub Actions sets `CI=true` on every runner.
- **Four suites, three runners.** `UBookIt.Tests` and `UBookIt.Tests.Rendering` (xunit v2),
  `UBookIt.Tests.Integration` (xunit v3, `Exe`), and the client's vitest. `UBookIt.Tests.ThemeFixture`
  is a Razor class library compiled into the rendering tests and contains no tests.
  `SolutionIntegrityTests` already guards that no project is excluded from the solution build and
  that every test project on disk is in the solution.
- **The integration fixture skips when it cannot connect.** `SqlServerFixture` defaults to
  `(localdb)\MSSQLLocalDB` and calls `Assert.Skip` on failure. GitHub's `windows-2025` image has
  **no LocalDB and no SQL Server** (runner-images readme, checked 2026-09-24); `ubuntu-24.04` has
  Docker.
- **Some tests read history.** `RetiredClaimEvidenceTests` runs `git show <sha>:<path>`; a default
  depth-1 checkout makes every such call fail.
- **`PackedSolution` runs a real `dotnet pack` inside the unit suite**, deleting the client output
  first so it packs a checkout rather than a working copy. CI is the first genuinely clean clone it
  will run in.
- **The suites have only ever run on Windows.** The dev working tree is 993 CRLF files
  (`core.autocrlf=true`); a Linux checkout under `* text=auto` is LF, on a case-sensitive
  filesystem.
- **No `global.json`.** The dev machine builds with SDK 10.0.301; runner images carry 10.0.1xx,
  10.0.2xx, 10.0.3xx and 10.0.4xx bands.
- **Both lines target `net10.0`** with the same project and suite structure; they differ in Umbraco
  package ranges, `<Version>`, generated client sources and some prose.

## Goals / Non-Goals

**Goals:**
- A green run that *proves* each suite ran, rather than one that reports nothing failed.
- One set of CI files, identical on both lines, with drift made visible by the pipeline itself.
- A local reproduction of CI's build and test commands that is one command away.

**Non-Goals:** as proposal.md, plus: parallelising the suites across jobs, caching NuGet packages
(a cold restore is acceptable until run time says otherwise), and any matrix over OS or SDK.

## Decisions

### D1. One Linux job, SQL Server in a container started by a step

`ubuntu-24.04`, one job, steps in order. SQL Server runs as `mcr.microsoft.com/mssql/server:2022-latest`
started with `docker run` in a step, **not** as a `services:` container — a service container's
environment is fixed in YAML, so its SA password would be a literal in the repository. A step can
generate a random password (`openssl rand`), mask it (`::add-mask::`), start the container, wait for
it to accept a connection with a bounded retry, and export `UBOOKIT_TEST_DB` to later steps.

*Alternatives.* A `services:` container with a literal throwaway password — conventional, but the
repository convention forbids credentials in fixtures, and "it is only a CI password" is exactly the
exception a reviewer should not have to adjudicate. Windows with SQL Server installed per run (a
chocolatey/Express install) — minutes per run and no portability signal. Both OSes — doubles the
first-run fixes for coverage the dev machine already provides.

The container's image is Microsoft SQL Server 2022 **Developer edition** (`MSSQL_PID=Developer`,
`ACCEPT_EULA=Y`): licensed for development and test, never redistributed, not a dependency of any
package. Named here per the propose rule on third-party dependencies.

### D2. Fail-on-skip at two layers: the fixture's required mode and a results check

**The fixture** gains `UBOOKIT_TEST_DB_REQUIRED`: when exactly `true`, the connection failure
throws (failing the tests, same diagnostic text) instead of recording a skip reason. CI sets it.
This makes the *cause* visible where it happens — "no SQL Server reachable" as a failure message —
instead of a generic "N tests skipped" from the results check.

**The results check** (a PowerShell script under `scripts/ci/`, run with the runner's `pwsh`)
reads the TRX files `dotnet test` writes and fails when:

1. any test project discovered on disk (`tests/**/*.csproj` whose project references
   `Microsoft.NET.Test.Sdk`) has no TRX file, or a TRX with zero executed tests; or
2. any TRX reports a skipped (`NotExecuted`) result — printing the test name and message.

Deriving the expected set from `Microsoft.NET.Test.Sdk` rather than from a name pattern is what
excludes `UBookIt.Tests.ThemeFixture` *by rule*: it references no test SDK because it has no tests.
The rule is written in the script's header.

The vitest suite is checked by its own exit code and a reporter that fails on zero tests
(`--passWithNoTests` is off by default in vitest; the design relies on that default and the task
list proves it).

*Why both layers.* The fixture alone does not catch a project that never runs; the results check
alone would catch the integration skip but name it less usefully, and would not protect a
maintainer running `UBOOKIT_TEST_DB_REQUIRED=true dotnet test` locally against a stopped server.
*Alternative:* xunit's `failSkips` runner option — rejected because it differs between the v2 and
v3 runners this repository uses, and because it would not detect a project that produced nothing.

**The results check must be shown able to fire** before it is trusted (see *A guard must be able to
fire*): tasks require running it against a doctored TRX with a skip, a TRX with zero tests, and a
missing project, and recording each failure message.

### D3. Advisory codes are excluded from warnings-as-errors in `Directory.Build.props`

`<WarningsNotAsErrors>$(WarningsNotAsErrors);NU1901;NU1902;NU1903;NU1904</WarningsNotAsErrors>`
alongside the existing `TreatWarningsAsErrors` line, with a comment pointing at the audit. In
the props rather than as a `-p:` in the workflow so that `CI=true dotnet build` on a dev machine
reproduces CI exactly, and so the exception is next to the rule it qualifies. It is build policy;
it changes no package byte.

`CLAUDE.md`'s convention line becomes "warnings are errors in CI, except NuGet vulnerability
advisories, which a scheduled audit checks instead" on both lines.

### D4. The audit is a second workflow that checks out each line by name

`audit.yml`: `schedule` (weekly, Monday 06:00 UTC) and `workflow_dispatch`, with a matrix over
`ref: [main, dev/v18]`. Each leg checks out its ref, restores, and runs
`dotnet list UBookIt.slnx package --vulnerable --include-transitive`, failing when the output reports
any vulnerable package (the command itself exits 0 on findings, so the script parses its output —
and the task list proves the parse can fire against a known-vulnerable fixture package reference in
a scratch project, not in the repository).

The file is identical on both lines (D6), but **only `main`'s copy's schedule fires** — GitHub runs
scheduled workflows from the default branch only. Hence the explicit matrix: the audit of
`dev/v18` must not depend on `dev/v18`'s own schedule, which would silently never run. Stated in the
file's header so nobody "simplifies" the matrix to `${{ github.ref }}`.

*Known limitation:* GitHub disables scheduled workflows in a public repository after 60 days without
repository activity. Recorded in `docs/publishing.md`'s CI section; not engineered around.

### D5. Pinning

- `global.json`: `{"sdk": {"version": "10.0.300", "rollForward": "latestPatch"}}` — any 10.0.3xx
  patch, nothing outside the band. The dev machine's 10.0.301 and the runner's 10.0.3xx both
  satisfy it; a machine without a 10.0.3xx SDK gets a clear "compatible SDK not found" rather than a
  silent band change. `actions/setup-dotnet` reads it via `global-json-file`.
- Node: `actions/setup-node` with `node-version: 22` (the dev machine's major) and npm caching keyed
  on the client's `package-lock.json`.
- OpenSpec: `npx --yes @fission-ai/openspec@1.13.0 validate --all --strict` — the version on the dev
  machine today.
- Actions by full commit SHA with the version in a trailing comment: `actions/checkout`,
  `actions/setup-dotnet`, `actions/setup-node` (all MIT, GitHub-owned). SHAs are resolved at apply
  from each action's release tag and recorded in `tasks.md`.

### D6. Parity: a step that diffs the CI definitions against the other line

The CI definitions are `.github/workflows/**`, `global.json` and `scripts/ci/**`. On a push to
`main` the parity step runs `git diff --exit-code origin/dev/v18 HEAD -- <those paths>`, and the
mirror on `dev/v18`; it prints the differing files on failure. It runs only for pushes to the two
published lines and pull requests into them (for a PR, against the *other* line's tip), not for
feature-branch pushes — a feature branch is expected to differ until it lands.

*Consequence, accepted deliberately:* between the maintainer pushing one line and the other, the
first line's run is red. That red is true — the lines do differ at that moment — and it is cleared
by re-running after the second push. This is the pressure that was missing on 2026-09-23, when a
fix reached one line only three times in a day.

*Alternative:* a test in the unit suite that reads the other line with `git show origin/…` — rejected
because locally `origin/*` can be stale, so the test would pass or fail on the state of the
maintainer's last fetch rather than on the code.

### D7. Triggers, permissions, concurrency

- `ci.yml` triggers: `push` (all branches) and `pull_request` with `branches: [main, dev/v18]`.
- `permissions: contents: read` at workflow level; no `secrets.*` referenced anywhere.
- `concurrency: ci-${{ github.ref }}`, `cancel-in-progress: true` except on the two published lines,
  where every commit's run completes.
- Checkout with `fetch-depth: 0`, which also fetches the other line's ref for D6.

### D8. The first Linux run is part of apply, and what it finds is triaged, not absorbed

Apply pushes the change branch and treats the first runs as measurement. Each failure is classified
before it is fixed:

- **Instrument defect** (a test or guard assuming CRLF, `\`, case-insensitivity, a Windows code
  page): fixed in this change, and the fix must keep the guard able to fire — normalise the input
  the instrument reads, never loosen its assertion.
- **Shipped-code defect** (anything under `src/` that behaves differently on Linux): **apply stops
  and the maintainer decides**. A repo-only change cannot fix shipped behaviour without a release.

Every classification is recorded in `tasks.md` with the failing test and the cause.

## Risks / Trade-offs

- [The first Linux run surfaces many instrument failures] → accepted in explore; triaged per D8.
  If the volume is large, apply may land the workflow with the failing tests' fixes in separate
  commits so each is reviewable.
- [A shipped-code portability defect is found] → D8: stop and escalate. It would be a real finding
  about sites hosted on Linux, which is the point of choosing Linux.
- [`PackedSolution`'s pack is slow on a cold runner] → accepted; measure the first run's duration
  and record it. Only if a run exceeds ~20 minutes does caching come back into scope.
- [SQL Server container slow to start] → bounded wait (≈60 s) with a clear failure; never an
  unbounded poll.
- [The parity check reds a line transiently] → D6, accepted and documented in `docs/publishing.md`.
- [An advisory is ignored because it is only a warning] → the audit fails loudly on schedule; the
  audit's failure notification goes to the maintainer by GitHub's default workflow-failure email.
- [`global.json` blocks a contributor without a 10.0.3xx SDK] → the error names the required band;
  documented in the README's build section if one exists, else in `docs/publishing.md`.
- [Scheduled audit silently disabled after 60 days' inactivity] → D4, documented.

## Migration Plan

1. Land on `main` via the change branch (the workflow runs on the branch push itself, which is the
   first real run).
2. Cherry-pick the CI-definition commit(s) and the prose corrections to `dev/v18` in the same
   sitting; the prose is edited per line where the documents differ.
3. The maintainer pushes both lines; both runs must be green with parity passing.
4. The maintainer may then make the `ci` check required in branch protection (non-goal here).

Rollback: delete the workflow files on both lines. Nothing else depends on them; `global.json`
and the props exception are independently harmless.

## Open Questions

- The exact first-run duration, and whether it argues for caching — answered by the first run.
