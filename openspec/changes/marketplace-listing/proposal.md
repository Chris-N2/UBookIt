## Why

The Umbraco Marketplace listing is built from the packages' nuspec metadata. Two things in that
metadata are wrong, and a third defect sits next to them.

1. **Every package asks to be listed.** `umbraco-marketplace` is set once in the shared
   `PackageTags` in `Directory.Build.props`, so all five packable projects inherit it. Umbraco's
   listing rules say to tag **only the installable component**: *"if your package `MyPackage`
   references `MyPackage.Core`, only tag the former"*. The Marketplace has done what the tags ask.
   Measured on 2026-09-25, four pages are live: `ubookit`, `ubookit.persistence`, `ubookit.web`
   and `ubookit.backoffice`. `ubookit.core` is not listed only because it has no Umbraco
   dependency. A site author therefore sees four "uBookIt" entries, three of which install an
   incomplete product. Forgetting `UBookIt.Web` is exactly the silent failure the meta-package
   exists to prevent (`src/UBookIt/UBookIt.csproj`), and those listings invite it.
2. **The 18 line's installable package says it is for Umbraco 17.** `src/UBookIt/UBookIt.csproj`
   is byte-identical on both lines. The published `UBookIt 18.1.0` nuspec, and `18.0.0` before
   it, describes itself as *"A booking system for Umbraco 17 … Requires Umbraco 17 (LTS)"*. That
   description is what nuget.org shows, and it is the text on the one Marketplace listing this
   change keeps. The four library descriptions name no Umbraco major.
3. **`dev/v18` lacks a guard that `main` has.** `release-17-1-2` added
   `The_bound_admits_this_major_and_excludes_the_next` to `main`. The 18 line's `packaging` spec
   carries the scenario it enforces, *The bound excludes the next major and admits its own*, but
   no test on that line enforces it. Nothing has shipped wrong because of it: the published
   `18.1.0` bounds are `[18.2.0, 19.0.0)`. It is another fix that reached one line only.

It has to happen now. Nuspec metadata is frozen at push, so whatever `17.2.1` and `18.1.1` are
packed with is what they carry forever.

## What Changes

- **The tag.** `umbraco-marketplace` is removed from the shared `PackageTags` and added in
  `src/UBookIt/UBookIt.csproj` only. The other tags stay on every package. A guard over the
  **packed** nuspecs asserts that exactly one package carries the tag, and that it is the
  meta-package.
- **The description.** The meta-package's description names the Umbraco major **derived from
  `<Version>`**, so one `UBookIt.csproj` is correct on both lines and a cherry-pick cannot carry
  one line's major onto the other. A guard over the packed nuspecs asserts that every Umbraco
  major any package description names is the declared major.
- **The bound guard** is ported to `dev/v18` unchanged. It already derives its expected major
  from the declared version, so the same code is correct on both lines.
- **`docs/publishing.md`**:
  - Line 81 lists "Not submitted to the Umbraco Marketplace" as outstanding. Listing is automatic
    from the tag, and the listings exist.
  - Line 387 says the Marketplace picks up "the package". It will pick up the meta-package only,
    and the runbook should say so, including that already-published library versions keep their
    tag.
- **Both lines, in one sitting.** The change lands on `main` and is cherry-picked to `dev/v18`
  (plus the bound-guard port, on `dev/v18` only). Both lines must carry it before either patch is
  packed. `Directory.Build.props` is a CI parity file, so the lines are red until both are
  pushed.

Not a breaking change. It changes no API, no schema and no runtime behaviour, only package
metadata and tests, so it is patch-eligible.

## Non-goals

- **Delisting the library pages already published.** Published versions keep their tags, and
  nuget.org metadata cannot be edited. Whether the Marketplace drops a listing once its latest
  version is untagged is **unverified**. Only a web-search summary says so, not Umbraco's
  documentation. It is checked on the live pages after `17.2.1`/`18.1.1` publish and not claimed
  before.
- **Correcting the published `18.0.0`/`18.1.0` descriptions.** They are frozen. `18.1.1`
  supersedes them as the latest version.
- **`umbraco-marketplace.json`**, the richer listing metadata served from the project URL, is a
  separate and later piece of work.
- **Descriptions beyond the Umbraco major.** Wording, the icon and dependency ranges are
  unchanged. The ranges have their own requirement.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `packaging`: two ADDED requirements:
  - *Only the package a site installs asks to be listed on the Umbraco Marketplace*
  - *A package description names the Umbraco major its line targets*

  No existing requirement is modified, so nothing is replaced wholesale. The bound-guard port
  enforces a scenario both lines already have, so it needs no delta.

## Impact

- **Build metadata:** `Directory.Build.props` (a parity file, identical on both lines apart from
  `<Version>`) and `src/UBookIt/UBookIt.csproj` (identical on both lines, before and after).
- **Tests:** `tests/UBookIt.Tests/PackageCompositionTests.cs` gains two guards, reusing the
  existing `PackedSolution` fixture. On `dev/v18` it also gains the ported bound guard.
- **Docs:** `docs/publishing.md` (`VersionTruthTests` counts mentions of the feed there, so the
  edit is checked against that guard).
- **Both release lines.**
- No runtime code, API, client, schema or migration.
