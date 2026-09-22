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

**`packaging` — one ADDED requirement, and `skip_specs` was WRONG.**

The first version of this proposal argued `skip_specs: true` on the grounds that
*The package declares which Umbraco majors it accepts* already governs the 17 line, so this
change adds no guarantee. **QA checked, and the requirement is not on `main` at all** — it exists
only in `dev/v18`'s copy of `openspec/specs/packaging/spec.md`. `main`'s baseline has 20
requirements and none of them is this one.

The hedge in that argument — "on a branch that had not yet been brought into it" — *was* the
defect, and CLAUDE.md names it: *"`openspec/specs/` on `main` is the baseline every later change
diffs against, so unmerged specs make the sibling-sweep and wholesale-replacement disciplines
read a false baseline."* Archiving with `skip_specs` would have left `main` enforcing a guard
whose requirement `main` does not state — behaviour present in the code and absent from the
spec, which is precisely the inverse of the risk this proposal raised about the guard.

So the requirement is carried across as an `## ADDED` delta, exactly as the guard was, and for
the same reason: **one requirement, one implementation, on both lines.** Cherry-picking the
enforcement and leaving the guarantee behind was half a job.

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
