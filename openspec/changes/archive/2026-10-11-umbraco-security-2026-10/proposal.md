## Why

Two sets of advisories were published in the week of 2026-10-05, and both lines are affected.

- **Umbraco**, published 2026-10-06. Fixed in **17.7.1** and **18.2.1**:
  - GHSA-w5jr-cmfx-544x (medium): document version endpoints read outside the user's start node.
  - GHSA-7pj9-jmp9-p86q (medium): media deleted outside the user's start node.
  - GHSA-h5wj-vpvj-mprp (low): upload extension restrictions are bypassed by a trailing period.
  - GHSA-4c5q-6c64-96j7 (low): unauthenticated SignalR hubs disclose editorial activity.

  uBookIt's floors (`[17.6.2,18.0.0)`, `[18.2.0,19.0.0)`) admit every vulnerable version. Raising
  them is how a site installing or updating uBookIt is pulled onto a fixed Umbraco. This is the
  same move as the August 17.6.2 bump, made for the same reason.
- **SixLabors.ImageSharp 3.1.12**, published 2026-10-07: GHSA-gwg2, -wmxv (medium) and -j3p4,
  -j9gm, -jjfr (high). The only fixed versions are **4.1.2 and later**, a major version. **No
  Umbraco release depends on one**: 17.7.1 and 18.2.1 still take 3.1.12, and ImageSharp.Web 3.2.0
  is built against 3.x. In this repository the package is reached **only by `UBookIt.TestSite`**,
  through the full Umbraco install. No package uBookIt ships has a vulnerable dependency on either
  line (`dotnet list package --vulnerable --include-transitive`, 2026-10-11).

The scheduled `audit` run passed on 2026-10-05, and its next run fails on the ImageSharp
advisories. That is correct behaviour with no correct remedy available, because the fix is not
reachable through Umbraco. The audit needs a narrow, honest way to accept an advisory that
cannot be fixed yet, and nothing broader.

## What Changes

- **Raise the Umbraco floor on both lines.** All seven `Umbraco.Cms.*` entries in
  `Directory.Packages.props` move: `main` to `[17.7.1,18.0.0)` and `dev/v18` to `[18.2.1,19.0.0)`.
  The ceilings are unchanged.
- **The README states the new floor**, on each line: the opening's "Umbraco X or later" and the
  Requirements table row. On v18 that includes the "upgrade `Umbraco.Cms` itself to … first"
  advice.
- **Accept the ImageSharp advisories in the TestSite only, recorded where they apply.**
  `UBookIt.TestSite.csproj` declares each advisory as `NuGetAuditSuppress`, with its reason.
  That silences it in the build, and returns the Release build to **0 warnings**.
- **The audit judges each suppression rather than obeying it.** `dotnet list package --vulnerable`
  still reports suppressed advisories (measured 2026-10-11), so `Assert-NoVulnerablePackages.ps1`
  still sees every finding. It is extended to:
  - accept a finding **only** when the project it occurs in declares that exact advisory as
    suppressed;
  - **fail** on a suppression that no longer matches any finding in its project, so the
    exception cannot outlive its reason (Umbraco moving to ImageSharp 4 makes it stale);
  - **fail** if any packable project declares a suppression, because a shipped package must never
    accept an advisory;
  - report each accepted finding visibly (a notice, not silence);
  - take suppressions and packability from MSBuild's evaluation as Debug AND as Release: non-packable
    only if both say so, suppressed only if both say so, and a suppression declared in only one
    of them **fails**;
  - **fail** on any suppression in a project that targets more than one framework, because NuGet
    reads those per framework. *(The last two are from QA rounds 1-2; see design D3.)*
- **Upgrade checklist, as in August, on both lines**: regenerate the backoffice client and read
  the diff, confirm the migration list still holds, and do a live TestSite pass (the section
  loads and a booking can be placed and seen).

**No release is cut by this change.** `17.2.3` and `18.1.3` follow as their own release changes,
which carry the version bump and changelog.

**Not breaking.** No public API changes and no schema changes. A higher floor makes NuGet raise
a site's Umbraco packages to 17.7.1 / 18.2.1 when it installs or updates uBookIt. The Umbraco
advisories call for that upgrade anyway, and it is the documented behaviour (README,
Requirements). Precedent for shipping a dependency-range change in a patch: `17.1.2` added the
upper bound.

## Non-goals

- **Forcing ImageSharp 4 anywhere.** It would place a major version under code compiled against
  3.x, in a site that is never deployed, and protect nobody.
- Excluding the TestSite from the audit. That would hide every future advisory there, not just
  these five.
- Anything from the tidy list. Rewording historical "measured against 17.6.2" remarks: they record
  what was measured and stay true.
- The releases themselves.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `continuous-integration`: *Vulnerability advisories are audited separately from the build*
  gains the narrow exception: an advisory a non-packable project declares as suppressed is
  accepted there, a stale or packable-project suppression fails the audit, and accepted findings
  are reported. All three existing scenarios are carried forward word for word. **Two existing
  guarantees are deliberately NARROWED, not carried unchanged**, and both qualifications are
  written into the SHALLs themselves:
  - the audit "SHALL fail when any package … has a known advisory" now holds *except as the
    exception provides*;
  - an advisory "SHALL remain a visible warning" in the build *unless it falls under the
    exception*, in which case the audit reports it instead.

  Neither narrowing reaches a packable project. A shipped package still cannot accept an advisory
  in any form. *(Correction, QA round 1: this paragraph first said "every existing guarantee is
  carried forward", which was false. CLAUDE.md requires a narrowing to be stated as one.)*

## Impact

- `Directory.Packages.props` and `README.md`, both lines (literal floors differ per line).
- `src/UBookIt.TestSite/UBookIt.TestSite.csproj`, both lines.
- `scripts/ci/Assert-NoVulnerablePackages.ps1`, identical on both lines (parity-checked), so it
  must land on both before either line's CI is green.
- Possibly `src/UBookIt.Backoffice/Client` generated client, if the management API surface moved.
  It is expected not to move within a patch, but this is checked, not assumed.
- `openspec/specs/continuous-integration/spec.md` at sync, both lines.
