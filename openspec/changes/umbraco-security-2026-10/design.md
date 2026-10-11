## Context

- **Floors today:** `main` `[17.6.2,18.0.0)`, `dev/v18` `[18.2.0,19.0.0)`, on seven `Umbraco.Cms.*`
  entries in `Directory.Packages.props`. The entries move together, as the comment there requires.
- **Fixed Umbraco versions:** 17.7.1 and 18.2.1, from the repository advisories' `patched_versions`
  (2026-10-11). On `main` this is a **minor** Umbraco step (17.6 → 17.7), the same shape as August's
  17.5.3 → 17.6.2. On v18 it is a patch (18.2.0 → 18.2.1).
- **ImageSharp:** `Umbraco.Cms.Imaging.ImageSharp` 17.6.2 through 17.7.1, and 18.2.0 and 18.2.1,
  all depend on `SixLabors.ImageSharp 3.1.12` and `SixLabors.ImageSharp.Web 3.2.0`. All five
  advisories cover every version up to 4.1.1, so the first fixed version is 4.1.2.
- **Measured 2026-10-11:** with `NuGetAuditSuppress` for one advisory in the TestSite project, the
  restore no longer warns for it, but `dotnet list package --vulnerable --include-transitive
  --format json` still reports it. So the audit sees suppressed findings, and the build does not
  warn about them.
- **`audit.yml`** runs `scripts/ci/Assert-NoVulnerablePackages.ps1 -Target line/UBookIt.slnx` once
  per line. `scripts/ci/` is parity-checked across lines. The TestSite project file is not.
- Only the TestSite reaches ImageSharp. Every packable project and every test project reports no
  vulnerable packages.

## Goals / Non-Goals

**Goals:**
- No site can install uBookIt alongside an Umbraco affected by the 2026-10-06 advisories.
- The audit goes green for a true reason, an accepted and reported exception, and turns red again
  by itself when the exception stops being needed.
- The Release build is back to 0 warnings on both lines.

**Non-Goals:** ImageSharp 4 anywhere; excluding a project from the audit; the releases.

## Decisions

### D1. The floors are the advisories' fixed versions, nothing higher

`[17.7.1,18.0.0)` and `[18.2.1,19.0.0)`. Raising the floor any further would force sites onto
versions for no stated reason. Each line's README states its own floor in the same places as
today. The v18 Requirements row also tells a site to upgrade `Umbraco.Cms` itself to the floor
first, and that advice moves with it.

**Not moved:** remarks recording what was *measured* against 17.6.2 (`docs/booking-page.md`, the
package migration plan, `package.xml`, the theme registration and the rendering rig). They are
statements about history. The CHANGELOG entries for earlier releases stay as they are. The v18
`packaging` spec's `18.2.0` example is a worked illustration of NuGet minimum semantics, not a
statement of the current floor, so it stays.

### D2. The suppression lives in the project that needs it, and is the single source

`UBookIt.TestSite.csproj` declares the five advisory URLs as `NuGetAuditSuppress` items, under a
comment stating the reason. No Umbraco release depends on a fixed ImageSharp, and the TestSite is
the only project reaching it. That one declaration does two jobs: it silences the build warning,
and it tells the audit what has been accepted.

Rejected: a separate allow-list file in `scripts/ci/`. It would be a second list to keep in step
with the first. Because `scripts/ci/` is parity-checked, it would also have to be identical on
both lines, while the need for it is per line: the two lines' Umbraco versions could adopt
ImageSharp 4 at different times.

### D3. The audit asks MSBuild what each project suppresses and whether it is packable

For each project in the `dotnet list` report, the script runs
`dotnet msbuild <project> -getProperty:IsPackable -getItem:NuGetAuditSuppress` and reads the
JSON it returns:

- **Suppressions:** the evaluated `NuGetAuditSuppress` items' `Identity`, compared with the
  finding's advisory URL ignoring case and a trailing slash.
- **Packability:** a project is non-packable only if its evaluated `IsPackable` is `false`.
- **A project MSBuild cannot evaluate** (a non-zero exit, or output that is not the expected
  JSON) fails the audit by name, and accepts nothing.

So conditions, `<Choose>`, document order (the last `IsPackable` wins) and imports such as
`Directory.Build.targets` mean what they mean to the build. An item inside a `<Target>` is a
suppression to neither.

**And it is asked under the conditions that decide (QA round 2).** Round 1's version asked MSBuild
once, with its defaults (Debug, outer evaluation). QA reproduced two disagreements:

1. **Configuration.** A project non-packable only in Debug was let accept an advisory, though
   `publish.yml` packs Release, where it is packable.
2. **Multi-targeting.** NuGet reads suppressions per target framework, from inner evaluations.
   An outer-only suppression was accepted although NuGet still warned, and a per-framework one
   was refused although NuGet honoured it.

So each project is evaluated as **Debug and as Release**. It counts as non-packable only if it is
non-packable under both, and an advisory counts as suppressed only if it is suppressed under
both. A **multi-targeted project may not suppress at all**: its suppressions fail and accept
nothing. Each rule refuses wherever the script and the thing that decides could disagree,
instead of modelling the disagreement. Per-framework evaluation was rejected for this change:
no project here multi-targets, so it would be machinery with no user. The refusal is stated in
the spec rather than hidden.

The same class failed twice: first a model of MSBuild, then MSBuild asked about the wrong build.
The test for "does the gate check the guarantee" is *which build ships* and *which evaluation
NuGet reads*, not "is the answer from MSBuild".

**Superseded at QA round 1. The first version read each project file as XML** and claimed that
erred safe ("it can reject an exception it cannot see, but never accept one"). QA disproved
that with four reproductions where it **accepted** an advisory MSBuild would not:
- a later `IsPackable=true` after an earlier `false`;
- a suppression under `<Choose><When Condition>`;
- a suppression inside a `<Target>`;
- by reasoning, a `Directory.Build.targets` override.

A second, weaker model of MSBuild disagrees with MSBuild in *both* directions, so "fail-closed"
was never available to it. The lesson is to ask the thing that decides. Each of the four is now
a test that fails against the XML version (verified by running the new tests against it), and
the earlier rule "a conditional suppression fails outright" is dropped in favour of what the
condition evaluates to.

Cost: two MSBuild evaluations per project. The real solution's audit takes a minute or two
locally for 10 projects, which is acceptable for a weekly job. Environment variables are
MSBuild properties, so the evaluation sees the same environment the audit's restore does.

Outcomes, per project:

| Situation | Result |
|---|---|
| Finding with no matching suppression in its own project | **fail** (unchanged behaviour) |
| Finding matched by a suppression in its own non-packable project | **accepted**: `::notice` naming advisory, package and project, and counted |
| Suppression in a non-packable project matching no finding in that project | **fail**: stale, asks for removal |
| Any suppression in a packable project | **fail**, whether or not it matches anything |

The existing non-vacuity checks are unchanged: an unconsulted vulnerability source fails, and so
does a report naming no projects. The summary line states how many findings were accepted, so a
green run with accepted findings does not read as "no advisories".

### D4. The script gets an input for its report, so it can be tested

`Assert-NoVulnerablePackages.ps1` gains an optional `-Report <path>`. When given, it reads that
JSON instead of running `dotnet list`. The workflow does not change. Project paths in a report
are resolved as given. The test fixtures write a report and small, real SDK projects into a
temporary directory, because the script evaluates them. Fixture conditions use a property nothing
sets, never one an environment variable could supply: `CI` is set on the runner.

A test class in `UBookIt.Tests`, the first to run one of the CI scripts, invokes `pwsh` against
fixtures for each row of D3. It also covers the unchanged cases (a plain finding fails; a clean
report passes) and the scenario where a suppression does not reach another project. `pwsh` is
present wherever the suite runs: the CI runner's image ships it, and it is this project's local
shell. Each test asserts the exit code **and** a substring of the output naming what it should
name, so a test cannot pass for the wrong reason. Each new branch is proved able to fire by
mutation during apply.

Rejected: verifying by hand only, as the CI scripts have been until now. This script is the
gate that turns an advisory red. An exception mechanism inside it is exactly where a silent
widening could hide.

### D5. Upgrade checklist, per line, as in August

1. Raise the floors, restore, then run a clean `--no-incremental` Release build. Expect 0 warnings
   once D2 is in place, and record the number either way.
2. Regenerate the backoffice client against the running TestSite (`npm run generate-client`).
   Read the diff. Expect none within these versions, but check rather than assume.
3. Run the unit, integration, rendering and client suites, each separately and in order.
   `MultiClaimConcurrencyTests` pins the migration list.
4. Live TestSite pass: the backoffice loads, the uBookIt section and its workspaces register, and a
   booking can be placed on the front end and seen on the Bookings screen.
5. `dotnet list package --vulnerable --include-transitive` shows only the five accepted
   ImageSharp findings, in the TestSite, and the script with no `-Report` exits 0 against the
   solution.

### D6. One branch per line; the script lands on both before either can be green

`umbraco-security-2026-10` from `main`, and `umbraco-security-2026-10-v18` from `dev/v18`. The
script and its tests are identical on both. Floors, README and TestSite suppressions are per
line. The parity check fails until both lines carry the script, so CI is expected to be red on
the first PR's push until the second lands. That is the documented behaviour (`docs/publishing.md`,
*Continuous integration*), and Chris re-runs it. QA reviews both branches in one review.

## Risks / Trade-offs

- **17.6 → 17.7 is a minor version of Umbraco.** Its surface may have moved. D5's
  client-regeneration diff and live pass are how this is caught. A change in the backoffice surface
  would be reported to Chris before going further, not absorbed.
- **The exception is visible in CI only as notices.** That is accepted. The alternative, failing
  until Umbraco moves, leaves the audit permanently red, which teaches people to ignore red.
- **Stale detection depends on the advisory URL matching exactly**, after normalising case and a
  trailing slash. If GitHub ever changes its advisory URL form, every suppression becomes stale and
  the audit fails. It fails loudly, never by widening, which is the right direction.
- **The first two PRs are red on parity until both land**, as above.
