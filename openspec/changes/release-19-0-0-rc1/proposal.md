## Why

`run-on-umbraco-19` ported uBookIt to Umbraco 19.0.0-rc1 (merged locally into `dev/v19` and
archived there), and `ci-three-lines` made `dev/v19` a published line with CI (PR #75 / #76; `dev/v19`
first pushed at `9ba0b12`; its push run is green, parity included). Chris wants the release candidate
on nuget.org for real-world testing, alongside the other packages released for Umbraco 19's release
candidate, and will announce it in Umbraco's Discord. It is released with `17.7.1` and `18.6.1`
(Chris, 2026-11-16: "All three").

## What Changes

1. **No version bump.** `<Version>` is already `19.0.0-rc1`, and every row of `docs/publishing.md`'s
   *Before any push* table already names it, because the port moved them (its task 4.3).
   Verified, not assumed (task 1.1).
2. **The undated `19.0.0-rc1` changelog entry, written by the port, is completed:**
   - *Also in this release*: the backoffice client's regeneration for Umbraco 19 (`@hey-api/openapi-ts`
     0.99 against `@umbraco-cms/backoffice` 19.0.0-rc1). This pays the `backoffice-client-upgrade`
     changelog obligation on this line. The note that `18.6.1`'s contents are included, and "behaves
     as in `18.6.1`" (was `18.6.0`), are now true since 18.6.1 is released alongside.
   - *For contributors*: the .NET 11 SDK and Node 24.13+, and CI verifying three lines with prerelease
     tags.
3. **The first prerelease tag.** Chris pushes `19.0.0-rc1` on the merge commit, by explicit SHA.
   `publish.yml`'s second trigger filter, and `Assert-ReleaseTag.ps1`'s label-aware shape and
   case-exact version check, run on a real tag for the first time. **The `release` environment's
   deployment rule (`*.*.*`) is also proved only by this tag.** If the publish job does not start,
   nothing has been pushed, and the fix is that rule (a repository setting, Chris's).
4. **Both Trusted Publishing policies stay enabled.** The organisation still co-owns `UBookIt` only
   (checked 2026-11-16).
5. **Prerelease on nuget.org.** The label makes nuget.org list it as a prerelease. A site gets it only
   by asking (`--version "19.*-*"`, as the README and *Getting started* say), so no Umbraco 17 or 18
   site picks it up by accident. *(Corrected at QA round 3: this said its Umbraco range "refuses any
   site below 19 anyway". On a .NET 10 site the range is never consulted; see item 7.)*

6. **The install text is corrected (QA round 1).** The README and *Getting started* said a bare
   install fails restore. On an Umbraco 19 release candidate it succeeds, and gets the newest stable
   uBookIt, an 18.x, which does not work there (18.x's range admits 19's prereleases). Both pages,
   and the entry's *What you have to do*, now say so. **The range `[19.0.0-rc1, 20.0.0)` is
   published as it stands (Chris).** It admits Umbraco 20 prereleases in the same way, but none is
   expected before Umbraco 19.0.0 (the README and *Getting started* rows now say the packages do not
   yet refuse one), and the guard that checks ceilings wrongly requires `X.0.0`. So one
   change will fix the ceiling and the guard on all three lines.

7. **The `UBookIt` meta-package packs a `lib/net11.0/_._` placeholder (QA round 3; Chris chose to fix
   it now).** The meta-package ships no assemblies, and with no `lib/` folder NuGet treats it as
   compatible with every framework. QA proved with a real restore that `UBookIt 19.0.0-rc1` on a
   .NET 10 (Umbraco 18) site restored with no warning or error and brought none of uBookIt, because
   no dependency group matched. Once 19 is the newest stable, every bare install on an Umbraco 18
   site would have done that. The placeholder is NuGet's convention for "this framework, no
   assemblies". A local-feed restore probe now gives **NU1202 "not compatible with net10.0"**.
   - `PackageCompositionTests.The_installable_package_is_compatible_with_its_own_framework_and_no_other`
     holds `lib/` to exactly `lib/<tfm>/_._` for the nuspec's one framework. Removing the
     placeholder fails it.
   - The `NU5128` suppression is removed: the placeholder satisfies that warning, and a pack
     without the suppression is clean.
   - 17 and 18 are left as they are: no Umbraco 17 or 18 site runs a framework below net10.0.
     Recorded with the ceiling obligation.
   - Being the first 19 release, this breaks nobody. It is a code change inside the release,
     made at Chris's direction.

## Non-goals

- `17.7.1` and `18.6.1` are their own changes, `release-17-7-1` on `main` and `release-18-6-1` on
  `dev/v18`, run back to back with this one.
- The 19.0.0-final bump (deferred obligation 1). It follows Umbraco's 19.0.0.
- The Discord announcement: Chris posts it once the packages are live.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

(none; `skip_specs: true`). The release binds no new requirement.

## Impact

- `CHANGELOG.md` (the entry), and the README and `docs/getting-started.md` (what a bare install does,
  item 6). The README and runbook version literals are already `19.0.0-rc1`.
- **One packaging change (item 7):** `src/UBookIt/UBookIt.csproj` packs the new empty `src/UBookIt/_._`
  to `lib/$(TargetFramework)/`, and drops the `NU5128` suppression. One new guard in
  `PackageCompositionTests`.
