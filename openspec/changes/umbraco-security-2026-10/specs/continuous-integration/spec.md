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
The suppression, the build warning it silences and its stated reason all live in that one project
file. Whether a project is packable, and which advisories it suppresses, SHALL be what the build
system's own evaluation of that project says, the same evaluation the package manager's audit
uses, and not a separate reading of the file. A project that cannot be evaluated SHALL fail the
audit and accept nothing. The audit SHALL evaluate each suppression rather than obey it:

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
- **WHEN** a packable project declares any advisory as suppressed
- **THEN** the audit fails, naming the project, whether or not the advisory is found
