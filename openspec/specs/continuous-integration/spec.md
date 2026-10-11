# continuous-integration Specification

## Purpose

Unattended verification of this repository: what runs, when, and how a run proves that every suite
it claims to have run actually ran — on each published line, so that a green result means the code
was built and tested rather than that a maintainer remembered to.

## Requirements

### Requirement: Verification runs unattended on both published lines
Verification SHALL run without a maintainer's involvement on every push to `main` or `dev/v18`,
and every such run SHALL complete: no run for a commit on a published line SHALL be cancelled or
replaced by a run for a later commit. Verification SHALL also run on pushes to any other branch and
on every pull request whose base is `main` or `dev/v18`. There, a run superseded by a newer push to
the same branch or pull request MAY be cancelled, so that the latest push is the one verified.
Each run SHALL start from a clean clone with full history and SHALL, in one job:

- build the solution in Release configuration, with the repository's CI warnings-as-errors rule in
  force;
- run every .NET test project in the solution;
- run the backoffice client's test suite;
- validate every OpenSpec change and spec in strict mode.

A failure of any of these SHALL fail the run. A run SHALL NOT be reported as passing when any of
these four activities was skipped, cancelled or not reached. (Steps that exist only for particular
events, such as the parity check under *Both published lines carry the same CI definitions*, are
skipped where they do not apply, and that does not fail a run.)

#### Scenario: A push to either line is verified
- **WHEN** a commit is pushed to `main` or to `dev/v18`
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
- **WHEN** a pull request is opened or updated with base `main` or `dev/v18`
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

### Requirement: A run proves every suite ran
A verification run SHALL fail unless every test project in the repository reported results in that
run, and SHALL fail if any test in any project reported skipped. The set of test projects expected
to report SHALL be derived from the repository's contents at that commit, not from a list maintained
by hand, so that a newly added test project is expected without further edits and a project that
stops running is noticed. A project that exists only as a fixture compiled into other tests, and
contains no tests of its own, SHALL be excluded by a rule that is stated where the check lives.

#### Scenario: A suite that stops running fails the run
- **WHEN** a test project in the solution produces no results in a run — because it was excluded
  from the build, filtered out, or failed to start
- **THEN** the run fails, naming the project

#### Scenario: A skipped test fails the run
- **WHEN** any test reports skipped
- **THEN** the run fails, naming the test and its skip reason

#### Scenario: A new test project is expected without editing CI
- **WHEN** a test project is added to the repository and the solution
- **THEN** the next run expects results from it without any change to the CI definitions

#### Scenario: Integration tests run against a real database
- **WHEN** a verification run executes the integration suite
- **THEN** a real SQL Server started for that run is reachable, the integration tests execute
  against it, and none of them skip

### Requirement: Verification holds least privilege
Verification runs SHALL hold read-only repository permissions and SHALL use no repository or
organisation secrets. Any database credential a run needs SHALL be generated within that run and
SHALL NOT appear in the repository. Every third-party action a workflow uses SHALL be referenced by
a full commit identifier, not by a movable tag or branch.

#### Scenario: A run cannot write to the repository
- **WHEN** a verification run's token is used to push, tag, comment or publish
- **THEN** the operation is refused for lack of permission

#### Scenario: No credential is committed
- **WHEN** the repository's CI definitions are searched for a database password or access key
- **THEN** none is found

#### Scenario: A moved tag cannot change what runs
- **WHEN** an upstream action's version tag is repointed to a different commit
- **THEN** this repository's runs continue to execute the commit they were pinned to

### Requirement: The toolchain is pinned
The .NET SDK feature band SHALL be fixed by a file in the repository that applies to local builds as
well as CI, so that a new SDK feature band cannot introduce warnings into CI that a local build does
not see. The Node major version used for the client, and the exact OpenSpec CLI version used for
validation, SHALL be fixed in the CI definitions.

#### Scenario: CI and a local build use the same SDK band
- **WHEN** the solution is built locally and in CI
- **THEN** both builds use an SDK from the same pinned feature band, or the build refuses to start

#### Scenario: A new OpenSpec release does not change validation
- **WHEN** a new version of the OpenSpec CLI is published
- **THEN** CI continues to validate with the pinned version until the pin is changed deliberately

### Requirement: Vulnerability advisories are audited separately from the build
A NuGet vulnerability advisory against a package in the dependency graph SHALL NOT fail the
verification build; it SHALL remain a visible warning there, unless it falls under the exception
below, in which case the audit reports it instead. A separate audit SHALL run on a
schedule at least weekly, and on demand, and SHALL fail when any package in either published line's
graph — direct or transitive — has a known advisory, except as the exception below provides. Because scheduled runs execute only from the
default branch, the audit SHALL check `dev/v18` explicitly rather than relying on that branch's own
copy of the schedule.

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

#### Scenario: The audit can be run on demand
- **WHEN** a maintainer triggers the audit manually
- **THEN** it runs against both lines immediately, without waiting for the schedule

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

### Requirement: Both published lines carry the same CI definitions
The CI definitions — the workflow files, the pinned SDK file, the scripts the workflows run, and
the shared build properties that carry the warnings rules (every part except the package version,
which legitimately differs between the lines) — SHALL be identical on `main` and `dev/v18`. A verification run for a push to either line SHALL fail
when those definitions differ from the other line's current tip, naming the differing files, so
that a CI change landed on one line only cannot pass unnoticed.

#### Scenario: A CI change landed on one line is flagged
- **WHEN** a workflow file is changed on `main` and the same change has not reached `dev/v18`
- **THEN** the verification run for the push to `main` fails its parity check, naming the file

#### Scenario: Parity is restored by landing the change on the other line
- **WHEN** the same change is then pushed to `dev/v18`
- **THEN** the parity check passes on runs for both lines' tips
