## Why

A MessagePack advisory was published on **2026-10-15**:
[GHSA-qhrr-8q5h-9q3h](https://github.com/advisories/GHSA-qhrr-8q5h-9q3h) (CVE-2026-92707, medium).
Deserializing attacker-controlled data into `System.Object` can allocate excessive memory, because
nested array headers are trusted before the depth limit applies. It affects MessagePack < 2.5.303
and 3.0.0–3.1.8. **3.1.9** fixes it, and has been on nuget.org since 2026-09-17. No GitHub advisory
affects 3.1.9 or later (checked 2026-10-16).

It reaches the packages uBookIt ships, **only through Umbraco**: `Umbraco.Cms.Web.Common` and
`Umbraco.Cms.PublishedCache.HybridCache` 17.7.1 / 18.2.1 depend on MessagePack 3.1.8. The release
audit for `17.4.0` / `18.3.0` found it on 2026-10-15, in seven projects on both lines: the shipped
`UBookIt`, `UBookIt.Backoffice` and `UBookIt.Web`, plus the TestSite and the three test projects.

The packaging requirement *A shipped package declares the fix for an advisory its host cannot yet
supply* applies in full:
- a fixed version exists;
- the vulnerable one arrives transitively;
- no stable Umbraco release in either line's range depends on a fixed version. Umbraco `17.8.0-rc`
  and `18.3.0-rc` carry 3.1.11, but the stable releases are scheduled for 2026-10-29.

So the fixed version must be declared directly. Chris chose this (2026-10-16) over waiting, so
that the `single-choice-shortcut` release can ship now and conform.

## What Changes

- **`UBookIt.Backoffice` and `UBookIt.Web` reference MessagePack `3.1.9` directly**, through one
  `PackageVersion` entry in `Directory.Packages.props`. Those are the two packable projects whose
  graphs resolve 3.1.8, and they are siblings, so neither brings the fix to the other. `UBookIt`
  reaches 3.1.9 through them and must not declare it again. `UBookIt.Persistence` and
  `UBookIt.Core` do not have MessagePack in their graphs and must not gain it. That is the expected
  set; the apply measures it on each line (design D2).
- **What a site receives changes, and this is the point.** The packed nuspecs of
  `UBookIt.Backoffice` and `UBookIt.Web` gain a dependency on `MessagePack` `>= 3.1.9`. A site that
  installs or updates uBookIt resolves the fixed MessagePack. The package is already in every such
  site's graph, through Umbraco, so this raises its minimum version and adds nothing new.
- **Not transitive pinning**, for the reason the `Pkcs` declaration gives (design D1).
- **An exit:** the declaration is removed by the change that raises the Umbraco floor to a stable
  release whose graph resolves MessagePack 3.1.9 or later. That is the same change that removes the
  `Pkcs` declaration (design D4).
- **Both lines.** `dev/v18` resolves through Umbraco 18.2.1, so its set is measured separately.

**No release is cut by this change.** It ships in the parked `17.4.0` / `18.3.0` releases, whose
changes are re-scoped after this merges: their *What you have to do* and *What changed* gain the
advisory, and their audit task is re-run.

**Not breaking.** No public API change and no schema change. Raising the minimum of a dependency
already in the graph is patch-eligible on the `Pkcs` precedent (`17.3.2`). It rides in a minor here
only because that is the next release.

## Non-goals

- **Raising the Umbraco floor.** No stable Umbraco carries the fix yet.
- **Removing the `Pkcs` declaration.** Its exit condition has not been met either.
- **Suppressing the advisory anywhere.** The audit forbids that in a packable project, and the
  TestSite and test projects take the fix through their project references.
- **A framework reference or a runtime note.** MessagePack is not part of the ASP.NET Core shared
  framework, and the SDK does not prune it, so a package declaration reaches every site.
- **Claiming anything about how Umbraco uses MessagePack**, for example that it never deserializes
  untrusted data. That has not been established, so the changelog will not say it.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

(none; `skip_specs: true`). The requirement this change satisfies already exists in `packaging`,
and the change binds nothing new.

## Impact

- `Directory.Packages.props` (one `PackageVersion` entry, with the advisory and exit in a comment),
  and the `.csproj` of `UBookIt.Backoffice` and `UBookIt.Web`. Both lines; the sets may differ.
- The packed nuspecs of `UBookIt.Backoffice` and `UBookIt.Web`: one added dependency each.
- Packaging guards that read packed nuspecs, if any enumerate dependencies. They are updated at
  apply, never loosened.
- The parked release changes, re-scoped after merge (outside this change).
