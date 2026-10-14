## Context

See proposal.md — Why, for the advisories and the measured graph. Facts the decisions rest on:

- The repository uses NuGet Central Package Management (`Directory.Packages.props`,
  `ManagePackageVersionsCentrally`), with no transitive pinning today.
- Project graph: `UBookIt` → `UBookIt.Backoffice`, `UBookIt.Web`; `UBookIt.Backoffice` →
  `UBookIt.Core`, `UBookIt.Persistence`; `UBookIt.Web` → `UBookIt.Core`; `UBookIt.Persistence` →
  `UBookIt.Core`. `UBookIt.Core` has no package dependencies.
- NuGet resolves the highest version requested anywhere in a graph, so a direct reference in a
  leaf project reaches every project above it.
- ASP.NET Core 10's shared framework (`Microsoft.AspNetCore.App`) also contains
  `System.Security.Cryptography.Xml.dll` and `…Pkcs.dll` (seen in the installed 10.0.9 shared
  framework, 2026-10-14).

## Goals / Non-Goals

**Goals:** the requirement in `specs/packaging/spec.md`; the audit green on both lines; the
smallest declared-dependency change that achieves it.

**Non-Goals:** see proposal.md.

## Decisions

### D1 — Direct references, not transitive pinning

Microsoft documents both as remedies for a transitive advisory. Both put the pinned package into
the packed nuspec. They differ in what else they put there.

- **Transitive pinning** (`CentralPackageTransitivePinningEnabled=true`) is all-or-nothing per
  project. Every `PackageVersion` entry becomes a pin wherever it is reached transitively, and each
  pin is promoted to a nuspec dependency ("Transitive Pinning and Pack", NuGet docs). Here, that
  would declare EF Core and `Umbraco.Cms.*` packages in projects that do not reference them, and it
  would make the floating Umbraco ranges pins. Rejected.
- **A direct `PackageReference`** (version from `Directory.Packages.props`) declares exactly the one
  package, in exactly the project that names it. Chosen.

### D2 — Which projects get the reference: measured, smallest set

The rule is: reference a fixed package directly in a packable project **only if** that project's
resolved graph contains the package at a vulnerable version, **and** no project it references
already brings the fixed version.

From the table in proposal.md, the expected set on `main` is:

- `UBookIt.Persistence` → `Pkcs`. This also fixes `UBookIt.Backoffice`'s Pkcs, through its project
  reference.
- `UBookIt.Web` → `Pkcs`. Web does not reference Persistence.
- `UBookIt.Backoffice` → `Xml`, which its Umbraco dependencies bring at 10.0.10.

That is an expectation, not the decision. The apply measures it: add the references, re-run
`dotnet list package --vulnerable --include-transitive` for **Debug and Release**, then remove any
reference whose removal leaves the audit clean. `dev/v18` is measured on its own, because Umbraco
18.2.1's graph may differ (for example, `UBookIt.Web` may reach `Xml`).

Whether `System.Security.Cryptography.Xml` 10.0.13 itself depends on `Pkcs` 10.0.13 is read from
its nuspec at apply. If it does, `UBookIt.Backoffice`'s `Xml` reference also raises its Pkcs, and
the set may shrink.

### D3 — The nuspec diff is the proof, read from the packed packages

Pack every package before and after, extract each nuspec, and diff the dependency groups. The
expected difference is exactly the added `System.Security.Cryptography.*` dependencies, at
`10.0.13`. Any other difference is a defect. This is the "adds nothing that was not already there"
scenario.

Existing packaging guards read packed nuspecs (`PackageCompositionTests`, the Umbraco upper-bound
check). They may enumerate dependencies. If one fails, the apply reads why and updates the
expectation it encodes with the reason; it never loosens the guard.

A new test is **not** added to assert the two pins exist. The guarantee that matters, "no shipped
package resolves an advisory", is already enforced by the scheduled audit. A test pinning
`>= 10.0.13` would become wrong the day the pins are removed (D5), which is the intended future.

### D4 — Which copy a site loads, verified rather than assumed

At run time, the app's own copy of an assembly (from the NuGet package) and the shared
framework's copy are reconciled by the host. The expectation is that the higher assembly or file
version wins. So a site that takes uBookIt's update gets the fixed DLL even on an unpatched .NET
runtime, and a site on the patched runtime is fixed either way.

The apply verifies this, without overclaiming: publish the TestSite and record the version of
`System.Security.Cryptography.Xml.dll` in the publish output. The release changelog says only what
was observed. Whatever is observed, the changelog also advises taking the October .NET servicing
update, because uBookIt cannot speak for the rest of a site's graph.

### D5 — The pins have an exit

When Umbraco publishes 17.x / 18.x releases whose graphs resolve 10.0.13 or later, the change that
raises the Umbraco floor to them also removes these references (spec scenario "A declaration
whose reason has gone is removed"). The deferred-obligations memory carries this, and the
`PackageVersion` entries carry a comment naming the advisories and the exit condition.

## Risks / Trade-offs

- [A site pinned below 10.0.13 elsewhere] → NuGet takes the highest requested version, so a lower
  request elsewhere cannot win. A site that pins an *upper* bound below 10.0.13 gets a restore
  error naming the conflict. That is the right failure direction for a security fix.
- [The pins outlive their reason] → D5.
- [A guard enumerating packed dependencies fails] → D3: update the expectation, with the reason.
- [`dev/v18` differs] → measured separately (D2), with its own nuspec diff (D3).

## Migration Plan

None for data. A site updates the package. Rollback is the previous patch, which brings the
advisories back.
