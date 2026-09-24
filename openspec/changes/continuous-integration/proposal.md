## Why

Nothing builds or tests uBookIt except a maintainer's machine. Every green result on either
published line — `main` (17.x, Umbraco 17 LTS) and `dev/v18` (18.x, Umbraco 18 STS) — was green
because somebody remembered to run four suites by hand, and once already a suite stopped running
for three days without anyone noticing (`0e79a03`, 415 tests). The repository is now public on
GitHub, so GitHub Actions is available; this change makes verification unattended on both lines.
It is also the prerequisite for Trusted Publishing, which needs a registered workflow and is the
follow-up change.

## What Changes

- **A verification workflow** runs on every push and on pull requests into `main` and `dev/v18`:
  a clean-clone Release build under the repository's CI warnings-as-errors rule, the three .NET
  test suites, the client's vitest suite, and `openspec validate --all --strict`.
- **It runs on Linux against a real SQL Server** started for the job. GitHub's Windows images
  carry no LocalDB or SQL Server, so a Windows job would have skipped every integration test and
  reported green. Linux is also where many Umbraco sites run, so the first run is a genuine
  portability test of the suites (line endings, case-sensitive paths); whatever it surfaces is
  fixed in this change.
- **A run in which any suite did not run fails.** Each test project on disk must report results,
  and no test may be skipped. The integration fixture gains an opt-in *required* mode
  (`UBOOKIT_TEST_DB_REQUIRED`) in which an unreachable database is a failure rather than a skip;
  CI sets it. Locally the existing skip-with-diagnostic behaviour is unchanged.
- **NuGet vulnerability advisories stay warnings in the build** (they would otherwise become
  errors the moment CI turns warnings into errors, reddening unchanged code when an advisory is
  published) and are instead checked by **a separate scheduled audit** that covers both lines
  from the default branch's schedule and can be run on demand.
- **The toolchain is pinned**: a `global.json` fixes the .NET SDK feature band (this also applies
  to local builds), the workflow fixes the Node major, and the OpenSpec CLI version is fixed in
  the workflow.
- **Both lines carry byte-identical CI definitions**, landed by cherry-pick in the same sitting,
  and the workflow checks that they have not drifted.
- **Prose made false by this change is corrected**: `docs/publishing.md`'s "There is no CI,
  anywhere", `SolutionIntegrityTests`' "There is no CI on this repository", and the `CLAUDE.md`
  warnings-as-errors convention (which gains the advisory exception) — on both lines.

No package bytes change, so no version is consumed and nothing is published.

## Capabilities

### New Capabilities

- `continuous-integration`: what unattended verification guarantees on this repository — when it
  runs, what it runs, that every suite demonstrably ran, least privilege, toolchain pinning, the
  advisory audit, and parity between the published lines.

### Modified Capabilities

- `persistence`: *Integration test coverage on real SQL Server* — gains a required mode in which
  an unreachable server fails the run instead of skipping. Every existing guarantee (real server,
  `UBOOKIT_TEST_DB` defaulting to LocalDB, per-run database created and dropped, the coverage
  list, skip-with-diagnostic when not required, never silently passing) is carried forward.

## Non-goals

- **Trusted Publishing / any publish workflow.** The next change. This one holds no secrets and
  requests no write permissions.
- **A DevExpress dependency scan.** Decided in explore: nothing in this repository would pull
  DevExpress in, and invariant 2 continues to be enforced by QA review as `CLAUDE.md` states.
- **Branch protection / required status checks.** A GitHub repository setting, owned by the
  maintainer; this change makes the checks exist, not mandatory.
- **A Windows job.** Considered and not taken; the dev machine remains the Windows coverage.
- **`npm audit` of the client's dev-dependencies.** They are build tools, not shipped runtime
  code; the audit covers the NuGet graph consumers actually receive.
- **Code coverage reporting, badges, release automation, Dependabot.**

## Impact

- New: `.github/workflows/` (verification and audit workflows), `global.json`, a results-check
  script under `scripts/`.
- Changed: `tests/UBookIt.Tests.Integration/Support/SqlServerFixture.cs` (required mode, test
  code only), `Directory.Build.props` (advisory codes not promoted to errors — build policy only,
  no package-bytes effect), `SolutionIntegrityTests` remarks, `docs/publishing.md`, `CLAUDE.md`.
- Possibly changed: tests or instruments that turn out to depend on Windows line endings or a
  case-insensitive filesystem. **If the Linux run exposes a defect in shipped code** rather than in
  a test, apply stops and brings it to the maintainer: that is a release decision, not part of a
  repo-only change.
- Both `main` and `dev/v18`. No public API, schema, or package-content change. Not breaking.
