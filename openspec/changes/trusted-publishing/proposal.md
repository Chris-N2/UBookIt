## Why

Every uBookIt release so far has been pushed from a laptop with a nuget.org API key, and nuget.org
now caps new keys at 30 days — the current one expires around mid-October 2026. Trusted Publishing
replaces the long-lived key with a one-hour key issued to a registered GitHub Actions workflow, but
it can only be used from CI, and until now CI has deliberately verified and never published. The
`continuous-integration` change made verification unattended; this change makes the push itself
unattended, gated by a maintainer's approval, and removes the monthly key renewal.

It also turns several manual checks in `docs/publishing.md` into ones a run performs. The tagged
commit must be on the public repository. The version must be the declared one. The artifacts must
be freshly packed, with SourceLink naming the public repository at that commit. The libraries go
before the meta-package. Each of these is currently a line in a runbook that a maintainer has to
remember.

## What Changes

- A new workflow, `.github/workflows/publish.yml`, triggered by pushing a release tag
  (`<major>.<minor>.<patch>`). It runs three jobs in order:
  - **check**: read-only. It refuses a tag that does not equal the tagged commit's `<Version>`,
    that is not on the published line whose major matches, or whose commit has no successful `ci`
    push run (it waits for one still in progress).
  - **pack**: read-only. It packs the tagged commit from a clean clone and verifies what it
    produced: the package set, each version, the repository commit in each `.nuspec`, and the
    SourceLink URLs. The result is kept as a run artifact.
  - **publish**: runs in the GitHub environment `release`, which requires a maintainer's
    approval. It is the only job anywhere in the repository that may request an OIDC token. It
    does not check out the repository. It exchanges the token for a temporary nuget.org key
    (`NuGet/login`) and pushes the verified artifacts, libraries first.
- The same workflow can be started by hand (`workflow_dispatch`) against an existing tag as a
  **dry run**. That runs check and pack in full and never publishes. So the pipeline can be proved
  against `17.2.1` and `18.1.1` before the first real release goes through it.
- The nuget.org user the key is issued for (`CNorwood69`, the personal account that owns every
  uBookIt package) is a plain value in the workflow, not a secret.
- The file is identical on `main` and `dev/v18`. The existing parity check already covers
  everything under `.github/`, so one workflow file name means one nuget.org policy covers both
  lines.
- `docs/publishing.md` is updated:
  - It describes publishing through the workflow, and the one-time setup outside the repository
    (the `release` environment and the nuget.org Trusted Publishing policy).
  - It removes "No Trusted Publishing" from *What is still outstanding*.
  - It keeps the laptop API-key procedure, labelled as the **fallback**, not the route.
- The `ci.yml` comment claiming "a release tag names a commit whose push has already been verified"
  now points at the check that enforces it.

No package content, public API or schema changes. No release is cut by this change: the first real
use is the next tidy patch on each line.

## Non-goals

- **Removing the manual fallback.** It stays documented until a release has gone through the
  workflow successfully on both lines. Removing it is a later change, recorded as a deferred
  obligation rather than scheduled here.
- **Gating merges on CI** (branch protection / required status checks). The check job requires a
  green run for the *tagged* commit only.
- **Waiting for nuget.org indexing.** The run ends when nuget.org accepts the push. Confirming the
  packages are restorable and that the package page renders stays a human step in the runbook.
- **Moving package ownership to the organisation.** The policy is owned by the personal account
  that owns the packages today. A later transfer means re-creating the policy under the new owner,
  and the runbook says so.
- **Tag protection rules** on the repository. Only maintainers can push tags today, and the
  `release` environment's approval is the gate.
- **Changing `ContinuousIntegrationBuild` for local builds.** It is set on the workflow's pack
  command only (see design).

## Capabilities

### New Capabilities
- `release-publishing`: how a release reaches nuget.org. Covers the tag trigger, the checks a
  release must pass before anything is pushed, the verified pack, the approval gate, the
  credential being confined to the one job that pushes, the dry run, and what the runbook tells a
  maintainer (including the fallback).

### Modified Capabilities
<!-- None. `continuous-integration` already requires that "the workflow files" be identical on both
     lines, which covers the new file without a delta, and its least-privilege requirement is scoped
     to verification runs, which this change does not alter. -->

## Impact

- **New:** `.github/workflows/publish.yml`, plus a script under `scripts/ci/` for the release
  checks. Both land on `main` and `dev/v18` in one sitting, or the parity check goes red.
- **Changed:** `docs/publishing.md` (the route, the setup, the fallback, the status list) and a
  comment in `.github/workflows/ci.yml`. The documentation guards
  (`Every_mention_of_the_feed_is_accounted_for`, the `SaysOnce` pins) will need their counts
  revisited for the rewritten sections.
- **Outside the repository, done by the maintainer:**
  - A GitHub environment `release`, with the maintainer as a required reviewer, self-review
    allowed, and deployments limited to release-shaped tags.
  - A nuget.org Trusted Publishing policy: owner `CNorwood69`, repository `Chris-N2/UBookIt`,
    workflow `publish.yml`, environment `release`.
- **New third-party actions**, each pinned to a full commit SHA like the existing ones:
  - `NuGet/login` (Apache-2.0; latest release v1.2.0 at proposal time)
  - `actions/upload-artifact` (MIT; v7.0.1)
  - `actions/download-artifact` (MIT; v8.0.1)

  All three are workflow-only. None of them ships in a package.
- **New test-only dependency** *(added at QA round 5)*: `YamlDotNet` 18.1.0 (MIT), referenced by
  `tests/UBookIt.Tests` only, so that the workflow guards read YAML as a parser does. Nothing
  shipped references it, and it has no advisories at the time of adding.
