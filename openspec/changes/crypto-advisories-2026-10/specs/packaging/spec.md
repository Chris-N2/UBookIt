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

A package declaration cannot fix a package that the .NET SDK **prunes** from a project that
references the ASP.NET Core shared framework (`Microsoft.AspNetCore.App`). The SDK's prune list is
recorded per project, as `packagesToPrune` in its `obj/project.assets.json`. Every Umbraco site
references that framework, so a pruned package is never restored into a site, and the site runs
the framework's copy of the assembly. For a pruned package, a shipped package SHALL NOT declare a
dependency on it, because no site would load it. It SHALL declare the shared framework itself
(`<FrameworkReference Include="Microsoft.AspNetCore.App" />`), so that the repository's restore
prunes the package as a site's does. The release notes of the version that would otherwise have
carried the fix SHALL name the .NET runtime version that fixes it, because only the site's
runtime can.

The condition is the prune list, **not** whether the shared framework contains the assembly. An
assembly the framework contains but the SDK does not prune (in .NET 10,
`System.Security.Cryptography.Pkcs`) still reaches the site as a package. The host then loads the
newer of the package's copy and the framework's, so a declared fixed version does reach the site,
and the declaration this requirement asks for applies to it as to any other package.

#### Scenario: An installing site resolves the fixed version

- **WHEN** a site installs or updates to a uBookIt version that carries the declaration
- **THEN** its restore resolves the fixed version of the package, or a later one
- **AND** the audit of every packable project in this repository reports no advisory for it

#### Scenario: A package the SDK prunes for the shared framework is left to the site's runtime

- **WHEN** the vulnerable package is one the .NET SDK prunes from a project that references
  `Microsoft.AspNetCore.App`
- **THEN** no shipped package declares a dependency on it
- **AND** every packable project whose graph reached it declares that framework reference, so
  the audit of that project resolves it as a site does
- **AND** the release notes name the .NET runtime version that fixes it

#### Scenario: A framework assembly that is not pruned is declared like any other

- **WHEN** the vulnerable package's assembly is also in the ASP.NET Core shared framework, but the
  package is not in the SDK's prune list for a project that references the framework
- **THEN** the shipped package declares the fixed version directly, as for any other package
- **AND** no framework reference is offered in its place

#### Scenario: The declaration adds nothing that was not already there

- **WHEN** the packed nuspecs before and after the declaration are compared
- **THEN** the only difference is the added dependency on each fixed package, and any framework
  reference declared under this requirement
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
