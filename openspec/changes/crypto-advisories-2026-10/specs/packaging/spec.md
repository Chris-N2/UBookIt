## ADDED Requirements

### Requirement: A shipped package declares the fix for an advisory its host cannot yet supply

A package uBookIt ships SHALL NOT resolve, in its own dependency graph, a package version with a
known security advisory when a fixed version of that package exists. Where the vulnerable version
arrives transitively, and no Umbraco release within the line's supported range depends on a fixed
version, the package SHALL declare a direct dependency on the fixed version. The declaration SHALL
appear in the **packed** nuspec, because that is what a site's resolver reads.

The declaration SHALL be narrow:

- it SHALL name only a package that is already in that project's resolved graph, so it raises a
  minimum version and never adds a package a site did not already receive;
- it SHALL NOT be achieved by a mechanism that also declares dependencies the project never chose
  (NuGet's transitive pinning promotes every centrally versioned package it pins into the packed
  nuspec);
- it SHALL be removed once a supported Umbraco release depends on the fixed version, so the
  package does not carry a dependency whose reason has gone.

A package SHALL NOT accept such an advisory by suppressing it. The audit already refuses that for
a packable project.

#### Scenario: An installing site resolves the fixed version

- **WHEN** a site installs or updates to a uBookIt version that carries the declaration
- **THEN** its restore resolves the fixed version of the package, or a later one
- **AND** the audit of every packable project in this repository reports no advisory for it

#### Scenario: The declaration adds nothing that was not already there

- **WHEN** the packed nuspecs before and after the declaration are compared
- **THEN** the only difference is the added dependency on each fixed package
- **AND** each added package was already present in that project's resolved graph before the
  declaration

#### Scenario: The declaration is read from the packed package

- **WHEN** the declaration is verified
- **THEN** it is read from the produced `.nupkg`'s nuspec, not from the repository's
  package-management files, because the two can disagree

#### Scenario: A declaration whose reason has gone is removed

- **WHEN** a supported Umbraco release depends on the fixed version or a later one
- **THEN** the direct declaration is removed in the change that raises the Umbraco floor to that
  release
