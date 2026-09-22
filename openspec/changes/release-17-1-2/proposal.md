## Why

**uBookIt `17.1.1` installs into an Umbraco 18 site, builds, and then the site will not start.**
Measured on 2026-09-22, not inferred:

| step | result |
|---|---|
| `dotnet add package UBookIt --version 17.1.1` on an Umbraco 18 site | **succeeds, no warning** |
| `dotnet build` | **succeeds, 0 errors** |
| `dotnet run` | **unhandled exception, process exits** |

```
Unhandled exception. System.Reflection.ReflectionTypeLoadException:
  Could not load all types from "UBookIt.Backoffice, Version=17.1.1.0" …
  . Could not load type 'Umbraco.Cms.Api.Management.OpenApi.BackOfficeSecurityRequirementsOperationFilterBase'
  . Could not load type 'Umbraco.Cms.Api.Common.OpenApi.OperationIdHandler'
    at Umbraco.Cms.Core.Composing.TypeFinder.GetTypesWithFormattedException(Assembly a)
    at Umbraco.Extensions.UmbracoBuilderExtensions.AddUmbraco(…)
```

It dies inside `AddUmbraco()`, during type discovery, **before the web host listens** — so this is
not a degraded backoffice, it is a site that does not boot. The two missing types are **Umbraco's
own**, removed from `Umbraco.Cms.Api.Management` and `Umbraco.Cms.Api.Common` in 18 when the CMS
swapped its OpenAPI generator.

**Nothing in the package metadata prevents any of it.** A NuGet dependency version is a
*minimum*, so `17.1.1` declaring `Umbraco.Cms.Api.Management 17.6.2` reads as "17.6.2 or
higher" — and Umbraco 18 satisfies it.

**Publishing `18.0.0` made this urgent rather than relieving it.** There is now a correct package
sitting beside the broken combination, and nothing a resolver reads distinguishes them. A site on
Umbraco 18 that pins `17.1.1` — following a tutorial, a lockfile, or a habit — gets a bricked site
and a stack trace about Umbraco internals with uBookIt's name nowhere near the top.

## What Changes

**An upper bound on every `Umbraco.Cms.*` dependency**, `[17.6.2,18.0.0)`, so the combination is
refused at restore rather than discovered at boot. Same mechanism `18.0.0` shipped, aimed at the
other side of the line.

**`17.1.2`, a patch, and the reasoning is the whole argument rather than a convention.** This
project's rule is that a patch *never breaks*, which CLAUDE.md sharpens to "an existing site keeps
working". The bound does stop something that currently restores — so it is only a patch if that
something never worked. **It never worked: the site cannot start.** The measurement above is what
licenses the version number, and it was taken before the number was chosen.

**A changelog entry that says so plainly**, because "your restore now fails" is exactly the
surprise a patch is supposed never to deliver. A reader who sees a restore start failing must be
able to find out in one place why that is the package protecting them.

## Non-goals

- **Not a fix for `17.0.0`–`17.1.1`.** Published versions keep the metadata they were published
  with. Those four remain installable into Umbraco 18 forever, and nothing can change it.
- **Not a change to what `17.x` supports.** The bound states the existing requirement; it does not
  narrow it. Umbraco 17.6.2 and every later 17 patch and minor resolve exactly as before.
- **Not the readme pinning work.** `release-18-0-0` added two `packaging` requirements about
  documentation refs; applying them to the 17 line is separate, and this change stays small
  deliberately — it exists to stop a site being bricked.
- **Not 17.2's features.** Site-wide closures and bank holidays are their own change.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

**None — and `skip_specs: true`, which needs arguing rather than asserting.**

`release-18-0-0` added the requirement *The package declares which Umbraco majors it accepts*,
and that requirement already governs this. It says every `Umbraco.Cms.*` dependency a published
package declares SHALL carry an upper bound excluding the next Umbraco major, and it explicitly
records that the bound **cannot be retrofitted** to versions already on nuget.org.

So this change adds no guarantee. It brings the 17 line into compliance with one that already
exists, on a branch that had not yet been brought into it — which is the definition of a release
that adds nothing and ships something.

**The guard comes with it.** `Every_umbraco_dependency_names_an_upper_bound` was written on
`dev/v18` and reads the packed nuspec; it must be cherry-picked here, or the 17 line would hold
the requirement with nothing enforcing it.

## Impact

| | |
|---|---|
| `Directory.Build.props` | `17.1.1` → `17.1.2` |
| `Directory.Packages.props` | Upper bound `[17.6.2,18.0.0)` on the `Umbraco.Cms.*` dependencies |
| `README.md` | Version sentence, four image pins (guarded to follow the version) |
| `CHANGELOG.md` | A `17.1.2` entry — undated until the feed confirms |
| `docs/publishing.md` | Its version literals, which the existing guard will surface on the bump |
| `tests/` | `Every_umbraco_dependency_names_an_upper_bound`, cherry-picked from `dev/v18` |

**No code changes, no schema change, no API change.** A site already on Umbraco 17 sees nothing
different; a site on Umbraco 18 stops being able to install a package that would have bricked it.
