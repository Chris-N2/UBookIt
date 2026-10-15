## Why

Two fixes are merged and archived on `main`, and neither reaches a site until it is published:

- **`backoffice-localization-fallback`** (PR #26, `98f8298`; archived `87c1f49`). A backoffice
  user whose language is not US English saw uBookIt's raw term keys instead of wording on every
  uBookIt screen. British English users were affected too, because Umbraco's "English (United
  Kingdom)" is the culture `en`. The terms are now registered under Umbraco's fallback culture.
- **`crypto-advisories-2026-10`** (PR #29, `be62ed6`; archived `4170e0b`). Five Microsoft
  advisories were published on 2026-10-13 against `System.Security.Cryptography.Xml` and
  `System.Security.Cryptography.Pkcs`, and no Umbraco release fixes them yet. uBookIt now raises
  the `Pkcs` minimum to 10.0.13. The three `Xml` advisories can be fixed only by the site's .NET
  runtime, and the changelog has to say so plainly. The scheduled `audit` workflow audits the
  repository, so the merge already made it pass; sites still resolve the old minimum until this
  release is published.

`17.3.2` is the 17 line's half of the release. It goes first because features land on `main`
first. `18.2.2` follows as its own change, `release-18-2-2`.

## What Changes

1. **Version `17.3.1` → `17.3.2`**, in every place listed in the table in `docs/publishing.md`'s
   *Before any push*. It is a **patch**. Since the `17.3.1` tag, the shipped code differs in six
   files only: the backoffice manifest (`manifest.ts`), `Directory.Packages.props`, and four
   `.csproj` files. No `.cs` file under `src/` changed, and there is no migration, setting or
   endpoint change. `PublishedSignatureTests` and `PublishedCallShapeTests` guard the API. The
   two dependency changes (a raised `Pkcs` minimum in two packages, and a
   `Microsoft.AspNetCore.App` framework reference in two packages) are patch-eligible on the
   `17.2.3` precedent: they raise the minimum of something already in every site's graph, or
   declare a framework every Umbraco site already runs on.
2. **A `17.3.2` changelog entry**, undated until it is live, written from the two archived
   changes' handovers (localization task 7.2, advisories task 5.3), and every claim checked
   against the code and the archives, not against this proposal.
   - *What you have to do:* run the **ASP.NET Core Runtime 10.0.13** or later on the server
     (including a container's base image, and a rebuild for a self-contained deployment). That is
     the only fix for the three `Xml` advisories, and a site's own NuGet audit will not report
     them. Nothing else, unless the site registered its own uBookIt wording under `en-us` and has
     British English users, in which case it registers the same wording under `en` too.
   - *What changed:* the five advisories by GHSA id and what this release does and cannot do
     about each; the backoffice wording now showing in every backoffice language, with the
     hard-refresh advice; and, for sites that override uBookIt's wording, how `en-us` and `en`
     overrides behave now.
3. **Pack from the merged, pushed commit, tag it, verify the artifact, and publish through
   `publish.yml`.** Then date the entry and archive. The artifact check adds one item for this
   release: `UBookIt.Persistence` and `UBookIt.Web` declare `System.Security.Cryptography.Pkcs`
   10.0.13, and `UBookIt` and `UBookIt.Backoffice` declare the `Microsoft.AspNetCore.App`
   framework reference.
4. **After publication, the scheduled `audit` workflow is run by hand on both lines** and should
   pass. It audits the repository, not the feed, so it passes as soon as both fixes are merged;
   running it confirms that, and is recorded.

## Non-goals

- **`18.2.2`** is its own change, `release-18-2-2`, on `dev/v18`, run back to back with this one.
- **Bumping `global.json`'s SDK** to one that carries the 10.0.13 runtime. Recorded as an
  observation in the deferred obligations; a change of its own if wanted.
- **The single-choice shortcut** is the next feature, not part of a patch.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

(none; `skip_specs: true`). The release binds no new requirement.

## Impact

- `Directory.Build.props` (`<Version>`, which the parity check exempts), `README.md`,
  `docs/publishing.md`, `CHANGELOG.md`.
- Five packages at `17.3.2` on nuget.org, frozen at push.
