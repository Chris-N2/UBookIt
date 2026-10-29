## Why

Umbraco released **17.8.0** and **18.3.0** on 2026-10-29. Three things make raising uBookIt's floor to
them necessary rather than optional:

- **Umbraco's own advisories.** On 2026-10-27 Umbraco published seven advisories, all vulnerable at
  `<= 17.7.1` and `<= 18.2.1`, which are uBookIt's current floors:
  - GHSA-rpv9-qp4c-g469 (**high**): back-office account takeover via sign-in redirect manipulation.
  - GHSA-24m3-86rh-6888 (medium): unpublished content exposed through preview and the Delivery API.
  - GHSA-4qmm-fhgv-vq9g (medium): backoffice logout does not invalidate the auth cookie.
  - GHSA-88p9-4pxf-pq3c (medium): stored XSS chained with path traversal in media management.
  - GHSA-xqwv-jvcm-5vjq (medium): start-node restrictions not enforced on some lookups.
  - GHSA-r4qv-c8pg-qq59 (medium): start-node restriction bypass when granting user groups.
  - GHSA-4vcm-wj6q-3pmg (low): website preview session did not expire.

  The fixes shipped in 17.7.2 / 18.2.2 and are carried by 17.8.0 / 18.3.0. Raising the floor is how a
  site that installs or updates uBookIt gets pulled onto a fixed Umbraco. **Our audit cannot see these
  advisories**, so this was found by reading Umbraco's advisories by hand. They are GitHub *repository*
  advisories, not in the global advisory database NuGet audit reads (checked 2026-10-29: 404). They
  are also filed against the `Umbraco.Cms` meta-package, which no shipped uBookIt package references.
- **The packaging requirement now demands that two declarations be removed.** *A shipped package
  declares the fix for an advisory its host cannot yet supply* says a declaration "SHALL be removed
  once a supported Umbraco release depends on the fixed version". The stable nuspecs (read
  2026-10-29) show that every uBookIt package carrying a declaration now reaches the fix through
  Umbraco:
  - `Umbraco.Cms.Persistence.EFCore` declares `System.Security.Cryptography.Pkcs` 10.0.13.
  - `Umbraco.Cms.Api.Common` and `Umbraco.Cms.Web.Common` declare `MessagePack` 3.1.11 and `Pkcs`
    10.0.13.

  This holds on both lines. So the `Pkcs` declaration (`crypto-advisories-2026-10`) and the
  `MessagePack` declaration (`messagepack-advisory`) have reached their recorded exits.
- **The TestSite's accepted advisories are stale.** `Umbraco.Cms.Imaging.ImageSharp` 17.8.0 / 18.3.0
  takes ImageSharp **3.2.0**. SixLabors backported all five October fixes to 3.2.0 (GHSA-gwg2, -wmxv,
  -j3p4, -j9gm, -jjfr: each now lists `< 3.2.0 → 3.2.0`). The audit script fails on a suppression that
  matches nothing, so all five `NuGetAuditSuppress` entries must go.

Chris also folded in three small items found earlier, so they ship in the same release:

- **The refusal messages say the wrong place.** Three backoffice messages tell an administrator to
  grant a permission under "Users → User Groups → Default permissions, by ticking …". On Umbraco 17
  the permissions are switches in a box headed **uBookIt**. The docs were corrected in
  `docs-single-source`, so today the screen contradicts the docs. It also falls short of the
  `site-settings` and `site-closures` scenarios, which require the message to say *where* the grant
  is given.
- **The duration hint shows in both modes.** The services editor shows "The person booking chooses how
  long they need…" with **Fixed length** selected as well, where it is false.
- **`dev/v18`'s client still builds against `@umbraco-cms/backoffice` 17.5.3.** So v18's TypeScript
  checks and client tests see Umbraco 17's API, and a break in 18 shows only at run time.

## What Changes

**Both lines** (`main` first, then ported to `dev/v18`):

- **Raise the Umbraco floor.** Every `Umbraco.Cms*` entry in `Directory.Packages.props` moves to
  `[17.8.0,18.0.0)` on `main` and `[18.3.0,19.0.0)` on `dev/v18`. The ceilings are unchanged.
- **Remove the security declarations.** Delete the `System.Security.Cryptography.Pkcs` and `MessagePack`
  `PackageVersion` entries and their comments. Delete their `PackageReference`s:
  - `Pkcs` from `UBookIt.Persistence` and `UBookIt.Web`;
  - `MessagePack` from `UBookIt.Backoffice` and `UBookIt.Web`.

  The `Microsoft.AspNetCore.App` framework references stay. They were never temporary
  (`crypto-advisories-2026-10` design D6).
- **Raise EF Core to 10.0.12** (`Microsoft.EntityFrameworkCore.SqlServer` and `.Design`). Umbraco
  17.8.0 / 18.3.0 require it, and leaving 10.0.11 is a NU1605 downgrade, which is a build error. The
  packed `UBookIt.Persistence` then declares `>= 10.0.12`, the same thing the new Umbraco floor
  already requires.
- **Remove the five ImageSharp `NuGetAuditSuppress` entries** and their comment from
  `UBookIt.TestSite.csproj`.
- **Correct the three refusal messages** (`notPermitted`, `notPermittedWrite`, `notPermittedRead`) so
  they name the box headed **uBookIt** in the user group editor, matching the docs. Correct the stale
  comments that repeat the old path (`UBookItPermissionSeed.cs`, `section/manifest.ts`). Add a guard
  that holds the retired user-group-editor claims out of the backoffice's English terms. Today that
  guard covers shipped markdown only.
- **Show the variable-length hint only when Variable length is selected.**
- **State the new floor** in `README.md` and `docs/getting-started.md`, and correct `docs/publishing.md`:
  - it no longer says the TestSite accepts ImageSharp advisories;
  - *What the audit cannot see* gains Umbraco's own repository advisories.
- **The upgrade checklist**, as for every floor raise:
  - regenerate the backoffice client against the running TestSite and read the diff;
  - confirm the migration list still holds;
  - do a live pass: the section loads, and a booking is placed and seen.

**`dev/v18` only:**

- Raise `Microsoft.AspNetCore.OpenApi` 10.0.11 → 10.0.12. `Umbraco.Cms.Api.Common` /
  `.Api.Management` 18.3.0 require it, so this is the same NU1605 constraint as EF Core.
- ~~Move the client's `@umbraco-cms/backoffice` from `^17.5.3` to Umbraco 18's package.~~ **Split
  off at apply (Chris, 2026-10-29).** The 18.3.0 package requires `@hey-api/openapi-ts` 0.99 or
  later, so the move also means upgrading the client's generator from 0.85 and regenerating the API
  client. That hit design D6's stop condition. It becomes a change of its own on `dev/v18`, so this
  change does not move v18's client.

**Not breaking.** No public API changes and no schema changes. The higher floor makes NuGet raise a
site's Umbraco packages to 17.8.0 / 18.3.0 when it installs or updates uBookIt. The advisories call
for that anyway, and it is the documented behaviour (README, *Requirements*). Removing a declaration
lowers nothing a site resolves, because Umbraco now brings the same or a later version.

**No release is cut by this change.** A release change follows, proposed as `17.7.0` / `18.6.0`. It
carries:
- the version bump;
- the changelog: the Umbraco advisories and the new floor, the removed declarations, the fixed messages
  and hint;
- the `docs-single-source` debt (the restructured docs, the manifest, `--version "17.*"`, SQL Server
  2019);
- the Trusted Publishing org-policy observation. Chris disables his personal policy before publishing.

## Non-goals

- **Umbraco 19.** `19.0.0-beta1` targets `net11.0` against .NET 11 release candidates. A spike comes
  later, and nothing ships against a beta.
- **Making the audit see Umbraco's own advisories.** NuGet cannot, because the advisories are not in
  the database it reads. This change documents that limit. A scripted check against Umbraco's advisory
  feed would be a CI change of its own.
- **Moving `main`'s client off `@umbraco-cms/backoffice` 17.5.x.** That would be routine tidying, not
  a floor obligation, and `main`'s client already sees the right major.
- The other open tidy items (`DocumentationAssert`'s non-asserting matcher, the `backoffice.md`
  session-ended sentence, `global.json`'s SDK band). They stay in the deferred list.
- The release itself.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

_None._ No requirement changes:
- The floor raise and the removed declarations are what *A shipped package declares the fix for an
  advisory its host cannot yet supply* already prescribes. No spec names a floor version.
- The corrected messages bring the code back into line with the existing `site-settings` and
  `site-closures` scenarios ("where it is granted").
- The hint fix is presentation the `services` spec does not describe.

The change sets `skip_specs: true`.

## Impact

- **Dependencies**, both lines:
  - `Directory.Packages.props`: the Umbraco ranges, EF Core, and the two removed entries; on v18 also
    `Microsoft.AspNetCore.OpenApi`;
  - the `UBookIt.Persistence`, `UBookIt.Web` and `UBookIt.Backoffice` csproj files;
  - `UBookIt.TestSite.csproj`.
- **Packed nuspecs:**
  - `UBookIt.Persistence`, `UBookIt.Web` and `UBookIt.Backoffice` drop a dependency;
  - every package's Umbraco minimum rises;
  - `UBookIt.Persistence`'s EF Core minimum rises to 10.0.12.
- **Backoffice client:**
  - `src/localization/en-us.ts` (three terms);
  - `src/section/services-editor.element.ts`;
  - the stale comment in `src/section/manifest.ts`;
  - ~~on v18, `package.json` / `package-lock.json` and whatever the 18 typings require~~ (split off,
    see *What Changes*).
- **Server:** the comment in `src/UBookIt.Backoffice/Composers/UBookItPermissionSeed.cs`.
- **Tests:**
  - the floor patterns in `UmbracoFloorDocumentationTests`;
  - a new guard over the localization terms;
  - a client test for the hint;
  - anything that counted the removed declarations.
- **Documentation:**
  - `README.md` (the opening floor sentence and the *Requirements* row);
  - `docs/getting-started.md` (*Requirements* and the upgrade-first advice);
  - `docs/publishing.md` (*Continuous integration*: accepted advisories, and what the audit cannot see).

  No page is added, so `docs/docs.json` is unchanged.
