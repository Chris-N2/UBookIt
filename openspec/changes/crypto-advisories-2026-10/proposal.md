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

**The two packages are not in the same position, which was found at apply (2026-10-14).** The
.NET 10 SDK *prunes* `System.Security.Cryptography.Xml` from every project that references the
ASP.NET Core shared framework (`Microsoft.AspNetCore.App`). Every Umbraco site does, so no site
ever loads a package copy of `Xml`: it runs the shared framework's copy, and only a .NET runtime
update changes that. `System.Security.Cryptography.Pkcs` is **not** pruned, so a package copy does
ship in the site, and a newer one replaces the framework's. A pin can therefore fix `Pkcs`, but a
pin of `Xml` would be inert at every real site. Both are fixed in the **.NET 10.0.13** runtime
(released 2026-10-13): all five advisories' CVEs are in its fixed list.

## What Changes

- **Each affected packable project references the patched `Pkcs` directly**, at 10.0.13, through
  `Directory.Packages.props`. A project gets a direct reference only to a package already in its
  resolved graph at a vulnerable version. The exact set is measured at apply, per line, not
  assumed from the table above. On `main` it is `UBookIt.Persistence` and `UBookIt.Web`.
- **`Xml` is not pinned. Instead, the packages that still saw it declare the shared framework.**
  `UBookIt.Web` already declares `<FrameworkReference Include="Microsoft.AspNetCore.App" />`.
  `UBookIt.Backoffice` and `UBookIt` now declare it too (and the non-packable `UBookIt.Tests`).
  They are only ever installed into an ASP.NET Core application, so this declares what is already
  true. It also means that the repository's audit resolves `Xml` as a site does, from the framework.
  So the audit stops reporting a package copy that no site runs. In return, the release changelog
  must say that the `Xml` advisories are fixed only by the site's .NET runtime (design D6).
- **Not transitive pinning.** `CentralPackageTransitivePinningEnabled` would pin *every*
  `PackageVersion` entry wherever it is reached indirectly, and NuGet promotes each such pin to a
  declared dependency in the packed nuspec. That would add EF Core and `Umbraco.Cms.*` dependencies
  that nobody chose (design D1).
- **What a site receives changes, and this is the point.** The packed nuspecs of
  `UBookIt.Persistence` and `UBookIt.Web` gain a dependency on `System.Security.Cryptography.Pkcs`
  `>= 10.0.13`. A site that installs or updates uBookIt then resolves the fixed `Pkcs`. That
  package is already in every such site's graph, so this changes its minimum version, not its
  presence. The packed nuspecs of `UBookIt` and `UBookIt.Backoffice` gain a framework reference to
  `Microsoft.AspNetCore.App`, which every Umbraco site already has.
- **The .NET runtime, in the release changelog.** A site must update its .NET 10 runtime to
  **10.0.13** or later. That is the only fix for the three `Xml` advisories, and it also fixes
  `Pkcs` for the rest of the site's graph. The apply observes which `Pkcs` copy a site on an older
  runtime actually loads, rather than assuming it (design D4).
- **An obligation to remove the `Pkcs` pin** when Umbraco ships releases whose own dependencies
  are 10.0.13 or later. It is recorded in the design and in the deferred obligations, so that the
  pin does not outlive its reason. The framework references are not temporary (design D6).
- **Both lines.** `dev/v18` resolves through Umbraco 18.2.1, so its set is measured separately.

**No release is cut by this change.** It is meant to ship in the same patch as
`backoffice-localization-fallback`: `17.3.2` and `18.2.2`, through their release changes.

**Not breaking.** No public API change and no schema change. Raising the minimum of a dependency
that is already present is patch-eligible on the same precedent as `17.2.3`'s Umbraco floor.
Declaring `Microsoft.AspNetCore.App` adds no requirement either: uBookIt already depends on
Umbraco's web packages, which declare that framework themselves, and `UBookIt.Web` has always
declared it.

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
  already in the graph, and is removed once Umbraco carries the fix. Where the vulnerable package
  is one the .NET SDK prunes from every project that references the ASP.NET Core shared framework
  (every Umbraco site), a package declaration cannot reach the site, so the package declares the
  framework instead, and the release notes name the runtime that fixes it. Being in the framework
  is not the condition: an assembly the framework contains but the SDK does not prune (`Pkcs`) is
  declared like any other. No existing requirement is modified or removed.

## Impact

- `Directory.Packages.props` (one `PackageVersion` entry, `Pkcs`) and the `.csproj` of each
  affected packable project: a `PackageReference` in Persistence and Web, a `FrameworkReference` in
  Backoffice and `UBookIt`, and the same `FrameworkReference` in `UBookIt.Tests`. Both lines; the
  project sets may differ.
- The packed nuspecs: one added dependency each for Persistence and Web, one added framework
  reference each for `UBookIt` and Backoffice.
- Possibly `tests/` packaging guards that enumerate packed dependencies (for example
  `PackageCompositionTests`). Found and updated at apply, never loosened to pass.
- `openspec/specs/packaging/spec.md` at sync, both lines.
