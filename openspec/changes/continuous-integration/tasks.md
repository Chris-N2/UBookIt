## 1. Branch and baseline

- [ ] 1.1 After the maintainer has pushed the proposal commit, create branch `continuous-integration` from `main`; record `main`'s SHA and the local baseline counts (unit / integration / rendering / client) from a clean client build + `dotnet test` + `npm test`, so first-run counts in CI can be compared against them

## 2. Integration fixture required mode (persistence delta)

- [ ] 2.1 Add `UBOOKIT_TEST_DB_REQUIRED` to `SqlServerFixture`: exactly `true` turns the connection failure into a thrown exception carrying the existing diagnostic; any other value or absence keeps the skip. Update the class remarks. Verify by running the integration suite locally with `UBOOKIT_TEST_DB` pointed at an unreachable server (e.g. `Server=127.0.0.1,1;…;Connect Timeout=2`): without the flag every test skips; with `true` every test fails naming the variable; with `TRUE`/`1`, behaviour is recorded and matches the spec's "exactly `true`" wording (decide case-sensitivity explicitly and state it in the remarks)
- [ ] 2.2 Add a test in the integration project covering both halves of the flag (required → failure; not required → skip reason recorded) that does not depend on a reachable server; mutation-check it by inverting the flag test and confirming it goes red. Verify with `dotnet test tests/UBookIt.Tests.Integration`
- [ ] 2.3 Confirm the reachable-server path is unaffected: full integration suite green on LocalDB with `UBOOKIT_TEST_DB_REQUIRED=true` and the count equal to 1.1's baseline plus 2.2's additions

## 3. Build policy and toolchain pins

- [ ] 3.1 Add `global.json` (`10.0.300`, `rollForward: latestPatch`) and verify `dotnet --version` in the repo root reports a 10.0.3xx SDK and a clean `dotnet build -c Release` succeeds
- [ ] 3.2 Add `NU1901;NU1902;NU1903;NU1904` to `WarningsNotAsErrors` beside `TreatWarningsAsErrors` in `Directory.Build.props`, with a comment naming the audit workflow. Verify with `$env:CI='true'; dotnet build -c Release --no-incremental` → 0 warnings, 0 errors; and prove the exception is scoped to advisories by temporarily introducing an unrelated warning (e.g. an unused variable) and confirming it still fails the CI-mode build, then reverting
- [ ] 3.3 Verify `dotnet pack` output is byte-identical in content to a pack from before 3.1–3.2 (compare the file lists and `.nuspec` of each package) so the "no package bytes change" claim is measured, not asserted

## 4. Results check

- [ ] 4.1 Write `scripts/ci/Assert-TestResults.ps1` per design D2: expected projects = `tests/**/*.csproj` referencing `Microsoft.NET.Test.Sdk` (rule stated in the header); fail on a missing TRX, a TRX with zero executed tests, or any `NotExecuted` outcome, printing project, test name and message
- [ ] 4.2 Prove it can fire, locally, against doctored inputs in the scratchpad (never committed): (a) a TRX with one skipped test, (b) a TRX with zero tests, (c) one expected project's TRX deleted, (d) a clean set. Record each exit code and message here; (a)–(c) must fail naming the cause, (d) must pass
- [ ] 4.3 Confirm `UBookIt.Tests.ThemeFixture` is excluded by the rule and not by name, by temporarily adding a `Microsoft.NET.Test.Sdk` reference to it in a scratch copy and observing the script now expects it

## 5. Workflows

- [ ] 5.1 Resolve full commit SHAs for `actions/checkout`, `actions/setup-dotnet`, `actions/setup-node` at their current major release tags; record tag → SHA here
- [ ] 5.2 Write `.github/workflows/ci.yml` per design D1, D2, D5, D6, D7: triggers, `permissions: contents: read`, concurrency, `fetch-depth: 0`, setup-dotnet from `global.json`, setup-node 22 with npm cache, generated + masked SA password, SQL container with a bounded readiness wait, `UBOOKIT_TEST_DB` + `UBOOKIT_TEST_DB_REQUIRED=true` exported, `npm ci` + `npm test` in the client, `dotnet build UBookIt.slnx -c Release`, `dotnet test UBookIt.slnx -c Release --no-build --logger trx --results-directory <dir>`, the results check, the parity step (published lines and PRs into them only), and `npx --yes @fission-ai/openspec@1.13.0 validate --all --strict`. Verify locally that the YAML parses (e.g. `actionlint` via `npx`/docker if available, else a YAML parse) and that no literal password, key or `secrets.` reference appears (`Grep`)
- [ ] 5.3 Write `.github/workflows/audit.yml` per design D4: weekly schedule + `workflow_dispatch`, matrix over `main` and `dev/v18`, `dotnet list … package --vulnerable --include-transitive` with an output parse that fails on findings; header explains why the matrix must not be replaced by the current ref
- [ ] 5.4 Prove the audit parse can fire: in a scratchpad project (not the repo) reference a package version with a known advisory, run the same command and parse, and record the failure; then run it against the repo and record the clean result
- [ ] 5.5 Verify the fixture-exclusion and history needs hold in CI-shaped conditions locally: a fresh `git clone --depth 1` of the branch fails `RetiredClaimEvidenceTests` (demonstrating why `fetch-depth: 0` is needed), and a full clone passes it. Record both

## 6. First runs on Linux (design D8)

- [ ] 6.1 Push the branch (the maintainer if the push prompts); record the run URL, duration, and per-project test counts against 1.1's baseline. Every project must appear; integration count must equal the local count
- [ ] 6.2 For every failure, classify as instrument defect or shipped-code defect and record it here with test name and cause. **Any shipped-code defect: stop and bring it to the maintainer before continuing.**
- [ ] 6.3 Fix instrument defects by normalising what the instrument reads (line endings, separators, casing, encoding), never by loosening an assertion; for each fix, show the guard still fires by mutating what it guards on Windows locally. Re-push until green
- [ ] 6.4 Prove the pipeline itself fails when it should, on the pushed branch, each as a throwaway commit reverted afterwards: (a) a test marked skipped → run fails at the results check; (b) the integration project excluded from the solution build → run fails (SolutionIntegrityTests and/or the results check, record which); (c) an unrelated compiler warning → build fails; (d) an OpenSpec strict-validation error → run fails. Record run URLs. Squash or drop these commits before QA so history stays clean
- [ ] 6.5 Trigger `audit.yml` with `workflow_dispatch` on the branch and record that both legs ran (`main` and `dev/v18`) and their result

## 7. Prose the change falsifies

- [ ] 7.1 `docs/publishing.md`: replace "There is no CI, anywhere" with what now runs, where, and what it does not do (no publishing; not required by branch protection; audit schedule and the 60-day inactivity caveat; the transient parity red). Run the full unit suite — the documentation guards watch this file; if one goes red, judge the sentence rather than relaxing the guard
- [ ] 7.2 `SolutionIntegrityTests` remarks: correct "There is no CI on this repository" to state that CI runs it and why the test still lives in a project that runs (it guards the exclusion that a solution-level CI run would itself be blind to)
- [ ] 7.3 `CLAUDE.md` conventions line: warnings are errors in CI except NuGet vulnerability advisories, which the scheduled audit checks
- [ ] 7.4 Outward grep across `openspec/specs/`, `docs/`, `README.md`, `CHANGELOG.md`, `CLAUDE.md`, `src/`, `tests/` for `CI`, `continuous integration`, `nothing runs unattended`, `no CI`, `LocalDB`, `skip` (in the integration-test sense), `global.json`, `SDK`; record every hit and its verdict (true / made true / rewritten). Grep both the vocabulary of this change and of what it replaces
- [ ] 7.5 Guarantee diff of the persistence delta against `openspec/specs/persistence/spec.md`: list every SHALL and scenario of *Integration test coverage on real SQL Server* and record carried / narrowed / dropped for each (expected: all carried; "Unreachable server is visible" narrowed to the not-required case, deliberately)
- [ ] 7.6 `npx openspec validate --all --strict` passes locally with the pinned CLI

## 8. The other line

- [ ] 8.1 Cherry-pick the CI-definition commit(s) (`.github/`, `global.json`, `scripts/ci/`), the fixture change, the props change and the test-remark change onto `dev/v18`; edit `docs/publishing.md` and `CLAUDE.md` per that line's text. Rebuild the client after the branch switch (stale-hash trap), then build + all four suites locally on `dev/v18` and record counts
- [ ] 8.2 Verify parity mechanically: `git diff --exit-code main dev/v18 -- .github global.json scripts/ci` is empty
- [ ] 8.3 Record the Linux-fix commits from 6.3 and confirm each either applies to `dev/v18` or is shown not to be needed there (the lines' tests differ in places)

## 9. Handover to QA

- [ ] 9.1 Write the handover in this file: claims made (counts, run URLs, the fire-proofs from 4.2, 5.4, 6.4), what was classified in 6.2 and why, and what QA should verify rather than trust. Spawn the QA subagent per `CLAUDE.md`
