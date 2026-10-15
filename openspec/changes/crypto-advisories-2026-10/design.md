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
- **Found at apply (2026-10-14):** the .NET 10 SDK prunes `System.Security.Cryptography.Xml`
  `(,10.0.32767]` from any project that references `Microsoft.AspNetCore.App` (read from that
  project's `obj/project.assets.json`). It does not prune `…Pkcs`. A framework reference is not
  inherited through a project reference for this purpose: each project is pruned only by its
  own framework references.
- The .NET **10.0.13** runtime and ASP.NET Core runtime (released 2026-10-13) fix all five
  advisories. Their CVEs (CVE-2026-96960, -96959, -96958 for `Xml`; CVE-2026-84008, -70162 for
  `Pkcs`, read from each GHSA) are in that release's fixed list in Microsoft's
  `release-metadata/10.0/releases.json`.

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

From the table in proposal.md, the expected set on `main` was:

- `UBookIt.Persistence` → `Pkcs`. This also fixes `UBookIt.Backoffice`'s Pkcs, through its project
  reference.
- `UBookIt.Web` → `Pkcs`. Web does not reference Persistence.
- `UBookIt.Backoffice` → `Xml`, which its Umbraco dependencies bring at 10.0.10.

That was an expectation, not the decision. Measured, the `Pkcs` pair held. The `Xml` reference
was replaced by D6. The apply measures it: add the references, re-run
`dotnet list package --vulnerable --include-transitive` for **Debug and Release**, then remove any
reference whose removal leaves the audit clean. `dev/v18` is measured on its own, because Umbraco
18.2.1's graph may differ (for example, `UBookIt.Web` may reach `Xml`).

Whether `System.Security.Cryptography.Xml` 10.0.13 itself depends on `Pkcs` 10.0.13 is read from
its nuspec at apply. If it does, `UBookIt.Backoffice`'s `Xml` reference also raises its Pkcs, and
the set may shrink.

### D3 — The nuspec diff is the proof, read from the packed packages

Pack every package before and after, extract each nuspec, and diff the dependency groups **and
the framework references**. The expected difference is exactly the added
`System.Security.Cryptography.Pkcs` `10.0.13` dependencies and D6's `Microsoft.AspNetCore.App`
framework references. Any other difference is a defect. This is the "adds nothing that was not already there"
scenario.

Existing packaging guards read packed nuspecs (`PackageCompositionTests`, the Umbraco upper-bound
check). They may enumerate dependencies. If one fails, the apply reads why and updates the
expectation it encodes with the reason; it never loosens the guard.

A new test is **not** added to assert the two pins exist. The guarantee that matters, "no shipped
package resolves an advisory", is already enforced by the scheduled audit. A test pinning
`>= 10.0.13` would become wrong the day the pins are removed (D5), which is the intended future.

### D4 — Which copy a site loads, verified rather than assumed (corrected at apply)

As proposed, this decision expected the host to reconcile the app's copy and the framework's copy
of each assembly, with the higher version winning, so that uBookIt's update would fix both
packages even on an unpatched runtime. **For `Xml` that was wrong**, and the apply found it by
doing what this decision asked. A published TestSite has **no** `System.Security.Cryptography.Xml`
asset in its `deps.json` and no copy that the host can load, because the SDK pruned the package
(Context). So the framework's copy (file 10.0.926 on the installed 10.0.9 runtime) is the one that
runs. That led to D6. (The publish folder does hold a `refs/System.Security.Cryptography.Xml.dll`, file 10.0.926: `refs/` is the set of framework reference assemblies kept for Umbraco's run-time Razor compilation, 307 files on both lines. The host does not load from it; the 3.4 traces show 0 of the 473-474 trusted platform assemblies come from `refs/`.)

For `Pkcs` the expectation stands, and is checked at two levels:

- **Publish output:** `System.Security.Cryptography.Pkcs.dll` is present at file version
  10.0.1326 (the 10.0.13 package), newer than the 10.0.9 framework's 10.0.926.
- **Run time:** start the TestSite on the unpatched 10.0.9 runtime with host tracing and record
  which path the host resolves `System.Security.Cryptography.Pkcs.dll` to. The release changelog
  states only what was observed.

Whatever is observed, the changelog tells a site to update its .NET 10 runtime to 10.0.13 or
later. For `Xml` that is the only fix, and uBookIt cannot speak for the rest of a site's graph.

### D5 — The pin has an exit

When Umbraco publishes 17.x / 18.x releases whose graphs resolve `Pkcs` 10.0.13 or later, the
change that raises the Umbraco floor to them also removes these references (spec scenario "A
declaration whose reason has gone is removed"). The deferred-obligations memory carries this, and
the `PackageVersion` entry carries a comment naming the advisories and the exit condition. The
framework references of D6 have no exit: they are not a security measure.

### D6 — `Xml` belongs to the shared framework: declare the framework, do not pin

Options, once the pruning was found (Chris chose B, 2026-10-14):

- **(A) Keep the `Xml` pin.** The audit goes green, and the nuspec declares `Xml >= 10.0.13`. But
  every site prunes it, so the declaration changes nothing that runs, while the green audit
  suggests otherwise. Rejected: there is little point in applying a pin that is inert.
- **(B) Declare `<FrameworkReference Include="Microsoft.AspNetCore.App" />`** in every packable
  project whose graph reached `Xml` (`UBookIt.Backoffice`, and `UBookIt`, which does not inherit
  the pruning from its project references). Each is installed only into an ASP.NET Core
  application, as `UBookIt.Web` already declares. The repository's restore then prunes `Xml` as a
  site's does, so the audit reports what a site runs. Chosen. `UBookIt.Tests` takes the same
  reference, because it runs ASP.NET Core code and should resolve as a site does.

The consequence is stated plainly in the release changelog: uBookIt cannot fix the three `Xml`
advisories, and a site fixes them by running .NET 10.0.13 or later. The audit cannot see a site's
runtime, so that sentence carries the guarantee. The framework references are not tied to these
advisories and stay.

## Risks / Trade-offs

- [A site pinned below 10.0.13 elsewhere] → NuGet takes the highest requested version, so a lower
  request elsewhere cannot win. A site that pins an *upper* bound below 10.0.13 gets a restore
  error naming the conflict. That is the right failure direction for a security fix.
- [The pins outlive their reason] → D5.
- [A guard enumerating packed dependencies fails] → D3: update the expectation, with the reason.
- [`dev/v18` differs] → measured separately (D2), with its own nuspec diff (D3).
- [A green audit read as "the site is safe"] → the audit sees uBookIt's graph, not a site's
  runtime. The changelog's runtime sentence (D6) is the only thing that reaches the `Xml`
  advisories, so it names the version.

## Migration Plan

None for data. A site updates the package. Rollback is the previous patch, which brings the
advisories back.
