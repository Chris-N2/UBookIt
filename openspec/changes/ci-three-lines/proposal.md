## Why

uBookIt is about to have a third published line. `dev/v19` carries the Umbraco 19 port
(`19.0.0-rc1`), and Chris wants the release candidate on nuget.org for real-world testing. The CI
definitions know exactly two lines, `main` and `dev/v18`, and they name them in four places. Pushed
as it is, `dev/v19` would:
- have its pull requests left unverified;
- be compared against nothing by the parity check;
- be audited with the wrong SDK;
- be unable to publish at all: the publishing trigger and the tag check both refuse
  `19.0.0-rc1`, and the spec says a prerelease tag must not publish.

This change has to land before `dev/v19` is first pushed.

## What Changes

- **One list of published lines.** A single file under `scripts/ci/` names the published lines
  (`main`, `dev/v18`, `dev/v19`). The parity check, the release-tag check and the audit all read
  it. The workflows match the lines by pattern (`main` and `dev/v*`) where GitHub needs a static
  filter. Adding or dropping a line is a one-line edit, not four.
- **Parity across every published line.** Each line's CI definitions are compared with every other
  published line's tip. `global.json`'s SDK `version` joins `Directory.Build.props`'s `<Version>`
  as the one value allowed to differ: a line pins its own SDK, because Umbraco 19 needs .NET 11.
  The rest of `global.json` (roll-forward policy, and anything added later) is still compared.
- **Parity runs on pushes only.** It no longer runs on pull requests. A CI change touching every
  line made each line's pull request fail parity against the others' bases, a deadlock broken
  last time only by merging past a red check. The push run is where parity always counted. It
  still fails, transiently and truly, until the change reaches every line, and a release tag still
  needs a green push run.
- **Prerelease tags publish.** A release tag is `<major>.<minor>.<patch>` with an optional SemVer
  prerelease suffix (`19.0.0-rc1`), and it must still equal the tagged commit's declared
  `<Version>`. **This deliberately reverses** the `release-publishing` scenario that says
  `17.2.2-rc1` does not publish. A `v`-prefixed tag, or any other shape, still publishes nothing.
- **Each line is audited with its own SDK.** The audit sets up .NET from the audited line's
  `global.json` and runs there. The default branch's pin can no longer fail a line on another major.
- The descriptions and comments that say "both lines" in the workflows, scripts and
  `docs/publishing.md` are updated to match.

No public API, package, schema or shipped behaviour changes. This is repository tooling and its
specs.

## Non-goals

- **`scripts/verify-install.ps1`.** It uses `-r LTS` and `bin/Debug/net10.0`, so it is wrong on
  `dev/v18` and `dev/v19`. It is local tooling that CI never runs, and proving it means building a
  real Umbraco site, so it is its own change (recorded in the deferred obligations).
- **Gating merges on CI.** "CI verifies, but does not yet gate" is unchanged, and no required
  status checks are added.
- **The `release` environment's tag rule (`*.*.*`) and the nuget.org Trusted Publishing policy.**
  Neither needs changing: the rule should admit `19.0.0-rc1`, and the policy does not name tags.
  The first prerelease publish is what proves the rule, and if it refuses, nothing is published
  (design, Risks).
- **Releasing `19.0.0-rc1`.** That is `release-19-0-0-rc1`, after this change.
- **Restricting prereleases to particular lines.** Chris's decision: any line may publish a
  prerelease whose tag equals its declared version. The tag push is a maintainer's act.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `continuous-integration`:
  - *Verification runs unattended on both published lines*: generalised to every published line,
    with pull requests into any published line verified.
  - *Vulnerability advisories are audited separately from the build*: every published line is
    audited, each with its own pinned SDK.
  - *Both published lines carry the same CI definitions*: compared across every published line,
    with the SDK version exempt and the check run on pushes only.
- `release-publishing`:
  - *A release is published by pushing its tag*: a prerelease tag publishes, and the scenario
    saying it does not is replaced.
  - *A release tag is checked before anything is built*: the published lines are read from the
    list, not named in the requirement.

**Checked and deliberately not modified.**
- `packaging`, *The package declares which Umbraco majors it accepts*, and the Marketplace
  requirement ("either line", "both lines"). Both describe per-line properties that every line
  meets, and the guards behind them are line-agnostic. A third line does not falsify them.
- `continuous-integration`, *The toolchain is pinned*. It still holds per line: each line's local
  build and CI use that line's pinned band.

## Impact

- **Workflows:** `.github/workflows/ci.yml`, `publish.yml`, `audit.yml`.
- **Scripts:** `scripts/ci/Assert-LineParity.ps1`, `scripts/ci/Assert-ReleaseTag.ps1`, and a new
  published-lines file under `scripts/ci/`.
- **Tests:** the unit tests that read the CI files (`PublishingWorkflowTests`, `AuditScriptTests`,
  `NodeEngineGuardTests`) are checked. New tests cover the list, the parity exemptions and the tag
  shape.
- **Docs:** `docs/publishing.md` (the *Continuous integration* section, the release-tag check, the
  publishing steps for a prerelease, and the parity note). No setup change, so
  `docs/getting-started.md` is untouched, and no new page.
- **Lines:** lands identically on `main`, `dev/v18` and `dev/v19`, except each line's own
  `global.json` SDK version. `dev/v19` is pushed for the first time as part of the rollout (design,
  Migration Plan).
- **No CHANGELOG entry:** nothing that ships changes.
