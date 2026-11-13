## ADDED Requirements

### Requirement: The published lines are listed once
The repository SHALL name its published lines in exactly one list, and every check that depends on
which lines are published SHALL take them from that list: verification's treatment of a published
line, the parity check, the release-tag check and the vulnerability audit. A branch that is not in
the list SHALL be treated as any other branch: it is verified, but it is not parity-checked, it is
not audited, and a release tag cannot be published from it. Adding a line to the list, or removing
one, SHALL require no other change to bring that line under, or take it out of, those checks. The
exceptions are workflow filters that the CI platform requires to be static: those SHALL match
published lines by pattern (`main`, and `dev/v` followed by the major), not by a separate copy of
the list.

#### Scenario: A new line is brought under every check by the list alone
- **WHEN** `dev/v19` is added to the list
- **THEN** pushes to it are verified as a published line, its CI definitions are parity-checked
  against every other published line, it is audited, and a `19.x` tag on it can be published,
  with no other file naming `dev/v19`

#### Scenario: A line dropped from the list stops being checked as published
- **WHEN** a line is removed from the list
- **THEN** no parity check compares against it, the audit no longer checks it, and a tag with its
  major is refused as belonging to no published line

## RENAMED Requirements

- FROM: `### Requirement: Verification runs unattended on both published lines`
- TO: `### Requirement: Verification runs unattended on every published line`

- FROM: `### Requirement: Both published lines carry the same CI definitions`
- TO: `### Requirement: Every published line carries the same CI definitions`

## MODIFIED Requirements

### Requirement: Verification runs unattended on every published line
Verification SHALL run without a maintainer's involvement on every push to a published line (see
*The published lines are listed once*: today `main`, `dev/v18` and `dev/v19`), and every such run
SHALL complete: no run for a commit on a published line SHALL be cancelled or replaced by a run for
a later commit. Verification SHALL also run on pushes to any other branch and on every pull request
whose base is a published line. There, a run superseded by a newer push to the same branch or pull
request MAY be cancelled, so that the latest push is the one verified. Each run SHALL start from a
clean clone with full history and SHALL, in one job:

- build the solution in Release configuration, with the repository's CI warnings-as-errors rule in
  force;
- run every .NET test project in the solution;
- run the backoffice client's test suite;
- validate every OpenSpec change and spec in strict mode.

A failure of any of these SHALL fail the run. A run SHALL NOT be reported as passing when any of
these four activities was skipped, cancelled or not reached. (Steps that exist only for particular
events, such as the parity check under *Every published line carries the same CI definitions*, which
runs on pushes to a published line only, are skipped where they do not apply, and that does not
fail a run.)

#### Scenario: A push to either line is verified
- **WHEN** a commit is pushed to `main` or to `dev/v18`
- **THEN** a verification run starts for that commit without anyone triggering it, and its result is
  shown against the commit

#### Scenario: A push to any published line is verified
- **WHEN** a commit is pushed to `main`, to `dev/v18` or to `dev/v19`
- **THEN** a verification run starts for that commit without anyone triggering it, and its result is
  shown against the commit

#### Scenario: Quick successive pushes to a published line are each verified
- **WHEN** three commits are pushed to `main` one after another, each before the previous
  commit's run has finished
- **THEN** all three runs complete and each commit shows its own result; none is cancelled or
  replaced

#### Scenario: A superseded feature-branch run may be cancelled
- **WHEN** a feature branch receives a new push while its previous run is still in progress
- **THEN** the previous run may be cancelled, and the newest push is verified

#### Scenario: A pull request is verified before merge
- **WHEN** a pull request is opened or updated with a published line as its base, including
  `dev/v19`
- **THEN** a verification run reports its result on the pull request

#### Scenario: A compiler warning fails the run
- **WHEN** a commit introduces a compiler or analyzer warning that is not a NuGet vulnerability
  advisory
- **THEN** the Release build fails and the run fails

#### Scenario: An invalid spec fails the run
- **WHEN** a commit leaves an OpenSpec change or spec that fails strict validation
- **THEN** the run fails

#### Scenario: History-dependent tests have the history they read
- **WHEN** a test reads a file as it stood at a historical commit
- **THEN** that commit is present in the run's clone, so the test's result reflects the file rather
  than a missing object

### Requirement: Vulnerability advisories are audited separately from the build
A NuGet vulnerability advisory against a package in the dependency graph SHALL NOT fail the
verification build; it SHALL remain a visible warning there, unless it falls under the exception
below, in which case the audit reports it instead. A separate audit SHALL run on a
schedule at least weekly, and on demand, and SHALL fail when any package in any published line's
graph — direct or transitive — has a known advisory, except as the exception below provides. Because scheduled runs execute only from the
default branch, the audit SHALL check every published line explicitly, taking them from the list
(*The published lines are listed once*), rather than relying on any other branch's own copy of the
schedule. Each line SHALL be restored and audited with the .NET SDK that line itself pins, so a
line on a different .NET major from the default branch is audited, rather than failed by a toolchain
it was never built with.

There is one exception, and it is deliberately narrow. When a project that is **not packable**
declares a specific advisory as suppressed, the audit SHALL accept that advisory **in that project
only** and SHALL report it as accepted rather than pass over it silently. This is for an advisory
that no reachable version can fix yet, because the dependency arrives through another product.
By convention, the suppression and its stated reason are written in that project's own file.
Whether a project is packable, and which advisories it suppresses, SHALL be taken from the build
system's own evaluation of that project, not from a separate reading of the file. It SHALL be
evaluated under every configuration the audit restores in or a release is built or packed in. A
project SHALL count as not packable only if it is not packable under all of them, and an advisory
SHALL count as suppressed only if it is suppressed under all of them. A suppression declared under
some of those configurations but not all SHALL fail the audit, naming it, whether or not anything
matches it. The package manager reads suppressions
per target framework, so a project that targets more than one framework SHALL NOT have any
suppression accepted, and a suppression it declares SHALL fail the audit. A project that cannot be
evaluated SHALL fail the audit and accept nothing. The audit SHALL evaluate each suppression rather
than obey it:

- a finding SHALL be accepted only when the project it occurs in declares that exact advisory;
  the same advisory in any other project SHALL still fail the audit;
- a suppression that matches no finding in its project SHALL fail the audit, naming it, so that an
  exception does not outlive its reason;
- a suppression declared by a packable project SHALL fail the audit, naming the project, because a
  package uBookIt ships SHALL NEVER accept an advisory.

#### Scenario: A newly published advisory does not redden unrelated work
- **WHEN** an advisory is published against a transitive dependency and a commit that does not touch
  dependencies is pushed
- **THEN** the verification run passes and the advisory appears as a warning

#### Scenario: The audit reports an advisory on either line
- **WHEN** the scheduled audit runs and a package in `dev/v18`'s graph has an advisory
- **THEN** the audit fails, naming the line and the package

#### Scenario: The audit reports an advisory on any line
- **WHEN** the scheduled audit runs and a package in `dev/v18`'s or `dev/v19`'s graph has an
  advisory
- **THEN** the audit fails, naming the line and the package

#### Scenario: The audit can be run on demand
- **WHEN** a maintainer triggers the audit manually
- **THEN** it runs against every published line immediately, without waiting for the schedule

#### Scenario: A line on another .NET major is audited with its own SDK
- **WHEN** the default branch pins a .NET 10 SDK and `dev/v19` pins a .NET 11 SDK
- **THEN** `dev/v19` is restored and audited with its .NET 11 SDK, and its result is about its
  packages, not about the default branch's toolchain

#### Scenario: An advisory suppressed by a non-packable project is accepted there and reported
- **WHEN** the development site declares an advisory as suppressed and that advisory is found in
  the development site's graph
- **THEN** the audit does not fail on it, and its output names the advisory, the package and the
  project as accepted

#### Scenario: A suppression does not reach another project
- **WHEN** an advisory suppressed by the development site is also found in another project's graph
- **THEN** the audit fails, naming that other project and the package

#### Scenario: A stale suppression fails the audit
- **WHEN** a project declares an advisory as suppressed and the audit finds no such advisory in
  that project's graph (for example, because the dependency was upgraded)
- **THEN** the audit fails, naming the project and the advisory, and asking for the suppression to
  be removed

#### Scenario: A shipped package cannot suppress an advisory
- **WHEN** a packable project declares any advisory as suppressed, including a project that is
  packable in only one of the configurations the audit restores in or a release is built or
  packed in
- **THEN** the audit fails, naming the project, whether or not the advisory is found

#### Scenario: A suppression that depends on the configuration fails the audit
- **WHEN** a project declares an advisory as suppressed under some of the evaluated
  configurations but not all, whether or not that advisory is found
- **THEN** the audit fails, naming the project, the advisory and the configurations it is
  declared in

#### Scenario: A multi-targeted project cannot suppress an advisory
- **WHEN** a project that targets more than one framework declares any advisory as suppressed
- **THEN** the audit fails, naming the project, and accepts none of that project's findings

### Requirement: Every published line carries the same CI definitions
The CI definitions — the workflow files, the pinned SDK file, the scripts the workflows run, and
the shared build properties that carry the warnings rules — SHALL be identical on every published
line, with exactly two values exempt because they legitimately differ between lines: the package
version, and the SDK version the pinned SDK file names (a line pins the SDK its Umbraco major
needs). Everything else in those two files, including the SDK roll-forward policy, SHALL still be
identical. A verification run for a push to any published line SHALL fail when those definitions
differ from any other published line's current tip, naming the differing files and the line they
differ from, so that a CI change landed on one line only cannot pass unnoticed. A published line
that is listed but cannot be found SHALL fail the check rather than pass it, because a comparison
that could not be made has not shown the lines agree.

The check SHALL run on pushes to published lines only, not on pull requests. A CI change reaches
every line through one pull request per line, and a pull request's run compared against the other
lines' tips would fail on every one of them until all had merged. The push run is where the check
counts: it stays red, truly, until the change has reached every line, and a release tag requires a
successful push run.

#### Scenario: A CI change landed on one line is flagged
- **WHEN** a workflow file is changed on `main` and the same change has not reached `dev/v18` and
  `dev/v19`
- **THEN** the verification run for the push to `main` fails its parity check, naming the file and
  each line it differs from

#### Scenario: Parity is restored by landing the change on the other line
- **WHEN** the same change is then pushed to each other line, `dev/v18` and `dev/v19`
- **THEN** the parity check passes on runs for every line's tip, once the earlier red runs are
  re-run

#### Scenario: A line's own SDK version is not a difference
- **WHEN** `dev/v19`'s pinned SDK file names a .NET 11 SDK and `main`'s names a .NET 10 SDK, and
  nothing else in the CI definitions differs
- **THEN** the parity check passes

#### Scenario: The rest of the SDK file is still compared
- **WHEN** one line's pinned SDK file changes its roll-forward policy and the others do not
- **THEN** the parity check fails, naming the file

#### Scenario: A pull request is not parity-checked
- **WHEN** a pull request into `dev/v18` changes a workflow file that the other lines do not yet
  have
- **THEN** its verification run does not run the parity check, and the parity check runs on the
  push that merging it makes

#### Scenario: A listed line that cannot be found fails the check
- **WHEN** a published line is in the list but its branch is not on the remote
- **THEN** the parity check on every other line fails, naming the missing line
