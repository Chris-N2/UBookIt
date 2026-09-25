## Context

See `proposal.md` for why. The state that shapes the approach:

- `Directory.Build.props:73` declares `<PackageTags>umbraco;umbraco-marketplace;booking;…</PackageTags>`
  for every packable project. The file is a **CI parity file**, byte-identical on `main` and
  `dev/v18` except for the `<Version>` line (`scripts/ci/Assert-LineParity.ps1`).
- `src/UBookIt/UBookIt.csproj` is identical on both lines today. Its `<Description>` hard-codes
  "Umbraco 17" twice, once with "(LTS)".
- `PackageCompositionTests` already packs the whole solution once per run, through the shared
  `PackedSolution` fixture. `PackedPackage.Value("tags")` and `Value("description")` read the
  packed nuspec, and the `Aggregate` constant names the meta-package.
- `The_bound_admits_this_major_and_excludes_the_next` exists on `main` only. It derives the
  expected major from `VersionTruthTests.DeclaredVersion()`, and its body names no line.
- The published `18.1.0` meta-package has **no** direct Umbraco dependency, yet it is listed, so
  the Marketplace's dependency rule counts transitive dependencies. Untagging the libraries
  therefore does not strand the meta-package unlisted (measured 2026-09-25; see the proposal).

## Goals / Non-Goals

**Goals:**

- One source for the tag rule and one source for the description, each correct on both lines
  without a line-specific edit.
- Guards that read what ships (the packed nuspec) and are each **proved able to fire** by a real
  mutation of the source, not by forcing a branch.

**Non-Goals:**

- A general guard over every tag, or over description wording. Only the two properties the spec
  names are checked.

## Decisions

### D1. The tag is appended in the meta-package, not restated

The shared property loses `umbraco-marketplace`, and `UBookIt.csproj` sets
`<PackageTags>$(PackageTags);umbraco-marketplace</PackageTags>`. Appending keeps one list of the
other tags. Restating the full list in the csproj would give two lists to drift.

*Alternative rejected:* a condition in `Directory.Build.props` keyed on the project name. It puts
knowledge of one project into the shared file, and it would make the parity file encode package
identity.

### D2. The Umbraco major in the description is derived from `$(Version)`

`UBookIt.csproj` computes the major from `$(Version)` with an MSBuild property function and
interpolates it into `<Description>`. `Directory.Build.props` is imported before the project body,
so `$(Version)` is set by then. Take the text before the first `.` rather than
`System.Version.Parse`, so a prerelease suffix such as `17.3.0-beta1` cannot break evaluation.

The description drops "(LTS)". Whether a major is LTS is not derivable from the version, and
hand-writing it is exactly what put "Umbraco 17" on the 18 line. It now says
*"Requires Umbraco N and SQL Server"*.

*Alternative rejected:* different literal descriptions per line. That makes the csproj a
permanent cherry-pick conflict, and it relies on the guard alone to catch the next mismatch
rather than making the mismatch unwritable.

### D3. The guards live in `PackageCompositionTests` and read the packed nuspec

- **Tag guard:** split each package's `tags` value on whitespace, which is what nuget.org packs
  semicolons into. Compare the set of packages carrying `umbraco-marketplace` to exactly
  `{ Aggregate }`. The failure message names every package in the difference, in both
  directions, which covers the spec's second and third scenarios.
- **Description guard:** match `Umbraco\s+(\d+)` in every package's description. Any captured
  major that is not the declared major fails, naming the package and the major. The `Aggregate`
  package must yield at least one match. A case-sensitive match is deliberate: the descriptions
  write the product name capitalised, and matching `umbraco` in lower case would also hit
  prose about packages rather than versions.
- **Precondition, so neither can pass vacuously:** assert that more than one package was
  produced. With a single package, "only the meta-package is tagged" is true by default.

### D4. The bound guard ports verbatim

The main-line test and its `using` lines are copied to `dev/v18` with no edits. Its derivation
already yields 18/19 there. Its proof of firing on `dev/v18` is to change one library's ceiling to
`20.0.0` locally and watch it fail. It is not considered done because it passes.

### D5. Landing order across the lines

The change is implemented and QA'd on `main`, as a feature branch merged by PR as `ee501b2` was.
It is then cherry-picked to `dev/v18` with the bound-guard port as a second commit, verified by
the rebuilt suite on that line. Both are pushed in the same sitting: `Directory.Build.props`
parity makes whichever line is pushed first red until the other arrives, and the runbook already
states that.

## Risks / Trade-offs

- **[Unverified delisting]** The three library listings may stay on the Marketplace indefinitely,
  showing their last tagged version. → Stated as unknown in the runbook and checked on the live
  pages after release. If they persist, the remedy is outside this repository (a request to
  Umbraco), and is recorded as a deferred obligation, not claimed fixed.
- **[Listing lag]** The Marketplace rescans on its own schedule, so the meta-package listing may
  show the old description for a while after `18.1.1`. → Not claimed until seen.
- **[A description that names Umbraco without a major]** "a booking system for Umbraco" names no
  major and passes for libraries. That is intended. Only the meta-package must name one.
- **[Regex blind spot]** A description writing "Umbraco v18" or "Umbraco CMS 18" names a major the
  guard does not see. → Stated in the test's remarks as the vocabulary it checks, in the same way
  `VersionTruthTests` states its own. The derived description (D2) removes the only hand-written
  instance, so the guard backs up the derivation rather than being the only defence.

## Migration Plan

None for consumers. Metadata takes effect at the next packed version. Rollback is a revert on
both lines before packing. After a push, the published metadata cannot be rolled back.
