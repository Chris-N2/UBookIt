## MODIFIED Requirements

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
  packable in only one of the configurations a release is restored or packed in
- **THEN** the audit fails, naming the project, whether or not the advisory is found

#### Scenario: A suppression that depends on the configuration fails the audit
- **WHEN** a project declares an advisory as suppressed under some of the evaluated
  configurations but not all, whether or not that advisory is found
- **THEN** the audit fails, naming the project, the advisory and the configurations it is
  declared in

#### Scenario: A multi-targeted project cannot suppress an advisory
- **WHEN** a project that targets more than one framework declares any advisory as suppressed
- **THEN** the audit fails, naming the project, and accepts none of that project's findings
