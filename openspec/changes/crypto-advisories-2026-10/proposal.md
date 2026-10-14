## Why

Five Microsoft .NET advisories were published on **2026-10-13**, and both lines are affected. They
are denial-of-service vulnerabilities in two packages, all fixed in **10.0.13**, which is on
NuGet:

- `System.Security.Cryptography.Xml` 10.0.0–10.0.12: GHSA-7hqp-7c5m-4vq6, GHSA-c399-9853-6jmj,
  GHSA-r955-8q5j-3h83 (all high).
- `System.Security.Cryptography.Pkcs` 10.0.0–10.0.12: GHSA-846f-f764-76mx (high),
  GHSA-2fmv-rv2c-59x7 (moderate).

Unlike the ImageSharp advisories on 2026-10-11, these reach **the packages uBookIt ships**, not
only the TestSite. Measured with `dotnet list package --vulnerable --include-transitive` on `main`
(2026-10-14):

| Project (packable) | Vulnerable resolution | Reached through |
|---|---|---|
| `UBookIt` | Xml 10.0.10, Pkcs 10.0.10 | `UBookIt.Backoffice`, `UBookIt.Web` |
| `UBookIt.Backoffice` | Xml 10.0.10, Pkcs 10.0.10 | Umbraco 17.7.1 → Examine / OpenIddict → `Microsoft.AspNetCore.DataProtection` 10.0.10 |
| `UBookIt.Web` | Pkcs 10.0.0 | Umbraco 17.7.1 → MailKit → MimeKit |
| `UBookIt.Persistence` | Pkcs 10.0.0 | EF Core SqlServer 10.0.11 → `Microsoft.Data.SqlClient` 6.1.6; MailKit |
| `UBookIt.Core` | none | |

**There is no Umbraco release that fixes them**: the newest are 17.7.1 and 18.2.1, which are
already uBookIt's floors. So raising the floor, as on 2026-10-11, is not available. Suppressing
them is not available either: the audit refuses a suppression in a packable project, rightly,
because a shipped package must never accept an advisory. The scheduled `audit` workflow therefore
fails on both lines from its next run, and correctly.

Chris chose to pin the patched versions now rather than wait for Umbraco (2026-10-14). The
ImageSharp case waited because the only fix was a new major with licence changes that Umbraco
itself pins below. This fix is a patch release of a Microsoft package, with no such obstacle.

## What Changes

- **Each affected packable project references the patched package directly**, at 10.0.13, through
  `Directory.Packages.props`. A project gets a direct reference only to a package already in its
  resolved graph at a vulnerable version. The exact set is measured at apply, per line, not
  assumed from the table above.
- **Not transitive pinning.** `CentralPackageTransitivePinningEnabled` would pin *every*
  `PackageVersion` entry wherever it is reached indirectly, and NuGet promotes each such pin to a
  declared dependency in the packed nuspec. That would add EF Core and `Umbraco.Cms.*` dependencies
  that nobody chose (design D1).
- **What a site receives changes, and this is the point.** The packed nuspec of each affected
  package gains a dependency on `System.Security.Cryptography.Xml` and/or `…Pkcs` `>= 10.0.13`. A
  site that installs or updates uBookIt then resolves the fixed versions. Both packages are
  already in every such site's graph; this changes their minimum version, not their presence.
- **A note on the .NET runtime, in the release changelog.** ASP.NET Core 10's shared framework also
  ships both assemblies. A site should take the October .NET servicing update as well. The design
  records which copy is loaded, and the apply verifies it rather than assuming it.
- **An obligation to remove the pins** when Umbraco ships releases whose own dependencies are
  10.0.13 or later. Recorded in the design, and in the deferred obligations, so that the pins do
  not outlive their reason.
- **Both lines.** `dev/v18` resolves through Umbraco 18.2.1, so its set is measured separately.

**No release is cut by this change.** It is meant to ship in the same patch as
`backoffice-localization-fallback`: `17.3.2` and `18.2.2`, through their release changes.

**Not breaking.** No public API change and no schema change. Raising the minimum of a dependency
that is already present is patch-eligible on the same precedent as `17.2.3`'s Umbraco floor.

## Non-goals

- **Raising the Umbraco floor.** No fixed Umbraco exists yet.
- **Suppressing the advisories anywhere.** In a packable project, the audit forbids it. In the
  TestSite and the test projects it is unnecessary, because they take the fix through their
  project references.
- **Transitive pinning** (see What Changes, and design D1).
- **The ImageSharp TestSite exception** from 2026-10-11. Unchanged.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `packaging`: ADDS one requirement. When a package uBookIt ships would resolve a dependency with a
  known advisory, and no supported Umbraco release fixes it, the package declares the patched
  version directly. The declaration is read from the packed nuspec, adds no package that was not
  already in the graph, and is removed once Umbraco carries the fix. No existing requirement is
  modified or removed.

## Impact

- `Directory.Packages.props` (two `PackageVersion` entries) and the `.csproj` of each affected
  packable project. Both lines; the project sets may differ.
- The packed nuspecs of the affected packages: one or two added dependencies each.
- Possibly `tests/` packaging guards that enumerate packed dependencies (for example
  `PackageCompositionTests`). Found and updated at apply, never loosened to pass.
- `openspec/specs/packaging/spec.md` at sync, both lines.
