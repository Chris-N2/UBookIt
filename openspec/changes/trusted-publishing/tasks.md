## 1. Release checks script

- [x] 1.1 Write `scripts/ci/Assert-ReleaseTag.ps1` (design D3): shape, declared version (first whole-line `<Version>` at the tagged commit), line derived from each published line's declared major plus `git merge-base --is-ancestor`, and a successful `ci` push run on that line for the SHA, with bounded polling (30 s, 60 min) while one is queued or in progress. Verify locally against `17.2.1` and `18.1.1` (both pass). `gh` is not installed on this machine, although it is preinstalled on the runner. So the script's run query must also work through plain REST (`Invoke-RestMethod`, unauthenticated for this public repo). Alternatively, the script can use REST in both places, which removes the difference.
- [x] 1.2 Prove each check in 1.1 can fire, one mutation at a time, and record the output in the handover. The cases: a tag `17.2.9` on `6f0da77` (version mismatch); `18.1.1` asked of the `main` line (wrong line); a feature-branch commit (not contained); a SHA with no `ci` push run (not verified); `v17.2.1` (shape).

## 2. Verified pack script

- [x] 2.1 Write `scripts/ci/Assert-PackedRelease.ps1` (design D4). It derives the expected package set from `IsPackable`, `PackageId` and `IncludeSymbols` via `dotnet msbuild -getProperty`. It checks one `.nupkg` per packable project at the tag's version, `.snupkg` presence exactly where `IncludeSymbols` is true, no stray files, each `.nuspec` `<version>` and `<repository url commit>`, and every `sourcelink.json` value prefixed with the public raw URL at the SHA, with at least one map per symbol-bearing project. Verify: run locally on a fresh clone of `17.2.1` packed with `-p:ContinuousIntegrationBuild=true`, and confirm it passes and lists five packages and four symbol packages.
- [x] 2.2 Prove 2.1 can fire, and record the output. The cases: a deleted `.nupkg`; an extra stray file; a `.nuspec` whose version was edited; a `sourcelink.json` edited to name another commit; an empty `obj` (no maps found). Each must fail, naming the file.

## 3. The workflow

- [x] 3.1 Resolve full commit SHAs for `NuGet/login` (v1.2.0), `actions/upload-artifact` (v7.0.1) and `actions/download-artifact` (v8.0.1) from their tags via the GitHub API, and record each SHA with its tag comment. Reuse the SHAs `ci.yml` already pins for `checkout`, `setup-dotnet` and `setup-node`. Verify: each SHA resolves to that tag's commit.
- [x] 3.2 Write `.github/workflows/publish.yml` per D1, D2 and D5:
  - triggers: tag push filter `[0-9]+.[0-9]+.[0-9]+`, and `workflow_dispatch` with a required `tag` input;
  - workflow permissions `contents: read`; `check` adds `actions: read`; `publish` has `id-token: write` only;
  - two checkouts in `check` and `pack`, with `persist-credentials: false`;
  - `publish` gated by `if: github.event_name == 'push'` and `environment: release`, with no checkout;
  - ~~pre-flight flat-container query before `NuGet/login`~~ *(superseded by 7.1: no pre-flight; each file pushed and classified)*, `NuGet/login` (`user: CNorwood69`), libraries pushed first and `UBookIt` last with `--skip-duplicate`, and a step summary;
  - concurrency `publish-<tag>`.

  Verify: `actionlint`, if available, else a careful read against D1–D6. `grep` shows `id-token` appears only in the `publish` job, and no `checkout` appears in it.
- [x] 3.3 Update the `ci.yml` comment on the branches-only trigger so it names `publish.yml`'s check as what enforces "a release tag names a verified commit". Verify: the comment names the enforcing script, and the diff is comment-only.

## 4. Runbook

- [x] 4.1 Rewrite `docs/publishing.md` per D8:
  - *Pushing* becomes the tag-push-and-approve route, with a list of what the run checks and what it does not;
  - a *Setup* section with every value from D6;
  - the API-key procedure moves unchanged under *Fallback: pushing by hand*, with the 403 section kept and noted as applying to a policy owner too;
  - the *Tag the release* section says the tag is now also the trigger;
  - the "No Trusted Publishing" outstanding item is removed.

  Verify: every value in D6 appears in the runbook, and matches the workflow file (the file name, the environment name, `CNorwood69`).
- [x] 4.2 Run the full .NET suite and update the feed-mention allowances and `SaysOnce` pins sentence by sentence, with a reason for each, without relaxing any guard. Verify: unit, integration and rendering totals are green and at least the baselines (1982 / 191 / 1168 on `main`). Any count change is explained in the handover.

## 5. Land on both lines

- [x] 5.1 Client tests, then a Release build (0 warnings), the full suite, and `openspec validate --all --strict` on the feature branch. Verify: all green, with counts reported.
- [ ] 5.2 Chris pushes the feature branch and opens a PR into `main`. CI must be green apart from the expected parity red, if it runs on the PR. After QA approval, merge.
- [ ] 5.3 Cherry-pick the same commits onto a branch from `dev/v18` in the same sitting, and PR into `dev/v18`. Verify: `git diff origin/main origin/dev/v18 -- .github global.json scripts/ci` is empty after both merges, and both lines' tip runs pass parity (re-run `main`'s if it went red in between).

## 6. Setup and proof (Chris, then verified together)

- [x] 6.1 (Done 2026-09-25, before apply. Verified through the public API: required reviewer `Chris-N2`, `prevent_self_review: false`, a single tag policy `*.*.*`, `can_admins_bypass: true` accepted.) Chris creates the GitHub environment `release`: required reviewer `Chris-N2`, self-review allowed, deployment tags `*.*.*` only. Verify: a screenshot of the environment settings, or a read of `gh api repos/Chris-N2/UBookIt/environments/release`.
- [x] 6.2 (Done 2026-09-25, before apply. Chris reports it saved and **active**. The policy page is not machine-readable from here.) Chris creates the nuget.org Trusted Publishing policy: owner `CNorwood69`, repository owner `Chris-N2`, repository `UBookIt`, workflow `publish.yml`, environment `release`, scope *new versions of existing packages only*, packages glob `UBookIt*`. Verify: the policy page shows it as active (not "temporarily active").
- [ ] 6.3 Dry run `17.2.1` (workflow from `main`) and `18.1.1` (workflow from `dev/v18`) from the Actions tab. Verify: `check` and `pack` are green on both, the `publish` job is skipped and no approval is requested, and the pack step's log reports five packages and four symbol packages at the right versions. **Also, from QA round 1:**
  - (a) a dispatch naming a tag that does not exist fails at the release checkout, and publishes nothing;
  - (b) *Re-run all jobs* on one green dry run is green again, which proves `overwrite: true` on the `packages` artifact under a second attempt.
- [ ] 6.4 Record a deferred obligation: the first real release through the workflow on each line (expected: the next tidy patch), and after it, removing the manual fallback in a separate change. Record with it the one unobserved answer: the CLI's wording for nuget.org's 409 on a symbol package that is still pending (design, *Risks*).

## 7. QA round 1 remediation (REJECT, 3 MAJOR / 2 MINOR / 4 NIT)

- [x] 7.1 MAJOR — the push decides *already present* from a lagging pre-flight, and `--skip-duplicate` hid the 409. The flat-container pre-flight is removed. Each `.nupkg` (`--no-symbols`) and `.snupkg` is pushed separately and classified from the CLI's own message; anything else fails. Evidence: the CLI's output was observed against a real feed (BaGet, in Docker) for a new push, a duplicate with and without `--skip-duplicate`, a wrong key, and a combined push. That showed a duplicate `.nupkg` suppresses its sibling `.snupkg`. The extracted step then ran with the real CLI in the .NET 10 SDK image. A wrong key is refused. A re-run after a partial publish pushes 7 files and **repairs Core's symbols** (Core's package already present). A complete re-run fails with "Nothing published". The order step's two failures work, and the unrecognised-output branch fails (stub, labelled).
- [x] 7.2 MAJOR — symbol expectations came from `IncludeSymbols`. They now come from `lib/**/*.dll` in each `.nupkg`: a `.pdb` per assembly in the `.snupkg`, a `<assembly>.sourcelink.json` per assembly, and `IncludeSymbols` disagreement fails. Evidence: 13 mutations each fail naming the cause, including QA's own reproduction (IncludeSymbols=false on Core with its `.snupkg` removed; the first version passed it), a `.pdb` removed from a `.snupkg`, an assembly's map renamed away, and the nuspec repository URL. The restored pack passes at 5 + 4.
- [x] 7.3 MAJOR — confinement enforced by review only. `PublishingWorkflowTests` (design D9) guards id-token confinement across every workflow, the publish job's shape, SHA pinning, and the runbook's policy values. Evidence: 9 mutations, each failing the intended test (including a new `.yaml` workflow with `write-all`), then all 4 pass once restored.
- [x] 7.4 MINOR — runbook attribution. *What that replaces* now says which check comes from the run, from `PackageCompositionTests` (presence only, not correctness) and from `VersionTruthTests`, and that copyright is checked by nothing. It adds a human step: read one `.nuspec` before approving. *After the push* is promoted to `##` as "either route", and its wildcard paragraph is marked fallback-only. The `403` section gains the workflow-route reading.
- [x] 7.5 MINOR — `can_admins_bypass: true` is recorded in the setup section and design D6, as an accepted trade-off.
- [x] 7.6 NITs:
  - a missing package now names its project;
  - the `curl`-under-`set -e` NIT went away with the pre-flight;
  - `Assert-ReleaseTag.ps1` retries up to 3 consecutive API errors and waits out an unregistered run for up to 5 minutes. Evidence: 6 mocked cases (transient error, two errors, three errors, late registration, no run after the grace, in-progress then failure), each as specified;
  - the concurrency comment is corrected;
  - also: `overwrite: true` on the `packages` artifact, for QA's re-run question, to be proved live in 6.3(b).

## 8. QA round 2 remediation (REJECT, 2 MAJOR / 1 MINOR)

- [x] 8.1 MAJOR: every accepted `.snupkg` was counted as newly published, so a re-run of a finished release went green. nuget.org documents that the same symbol package "can be submitted multiple times" (201 each time; I confirmed this against the Microsoft Learn page itself, not from the report). Now only a `.nupkg` can be *published*; a `.snupkg` is *symbols submitted* or *symbols pending*. "Nothing published" counts packages only and states how many symbol packages were submitted. The spec requirement is reworded, the symbol-repair scenario now states its outcome (red when no package was new), and a new scenario covers symbols being accepted again. The design's false Risk is corrected in place, with the correction noted, and the runbook's publish-step paragraph is rewritten. Evidence: the extracted step ran with the real CLI against BaGet, which, like nuget.org, accepts a duplicate `.snupkg`. **QA's case (every `.nupkg` present, every `.snupkg` accepted) fails with "Nothing published … 4 symbol package(s) were (re)submitted".** The partial re-run reports 3 packages published, with Web's resubmitted symbols shown as *submitted*. The packages-only re-run, a wrong key, the two order-step failures and the stubbed unrecognised output behave as before.
- [x] 8.2 MAJOR: `PublishingWorkflowTests` was fooled by a workflow-level `id-token` placed after `jobs:` and by flow-style `uses:`. The rules now run over every non-comment line of every workflow. Exactly one `id-token` line is allowed, inside the publish job's span, and `uses:` is matched anywhere on a line, in block or flow style, quoted or not. Evidence: QA's `late.yml` verbatim now fails two tests, and the same file without its id-token still fails pinning. A flow-style `permissions: { contents: read, id-token: write }` fails. All nine round-1 mutations still fail as before; 14 in all, then 4/4 once restored.
- [x] 8.3 MINOR: "no checkout" saw only `actions/checkout@` and `./`. The publish job now has an allow-list of actions (download-artifact, setup-dotnet, NuGet/login) and forbids `git`/`gh` on its lines. The spec scenario is reworded to match. Evidence: a `git clone` in a publish-job run step fails, and so does a SHA-pinned third-party action in the job.
