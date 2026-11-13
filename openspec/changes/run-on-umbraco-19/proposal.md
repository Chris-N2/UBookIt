## Why

Umbraco 19 RC1 shipped on 2026-11-12, and Umbraco has asked package authors to try their packages
on it. uBookIt does not build on it. The explore of 2026-11-13 found that **once it does build, it
locks every editor out of its own section**. Umbraco 19 removed the bridge that turned the
backoffice cookie into an OpenIddict token. Its Management API now authenticates editors with the
cookie alone. uBookIt's seven authorization policies name only the OpenIddict scheme, so an editor's
request reaches them with no identity.

This was proven on a running RC1 TestSite, in one page load:
- Umbraco's own `user/current` returned 200;
- uBookIt's `GET bookings` returned **401**;
- the backoffice treated that 401 as an expired session, so the editor sees "Your session has timed
  out. Please sign in again."

API users (client credentials) are unaffected.

A local `dev/v19` line, built now against RC1, gives Chris something to report back to Umbraco. It
also leaves only a floor bump when 19.0.0 ships.

## What Changes

All of it is on `dev/v19` only, a local branch, and nothing is pushed (see Non-goals).

- **Retarget:** `net11.0` everywhere; `global.json` SDK `11.0.100`; Umbraco `[19.0.0-rc1,20.0.0)`;
  `Microsoft.EntityFrameworkCore.SqlServer`/`.Design` and `Microsoft.AspNetCore.OpenApi` `11.0.0`.
  Those are the versions RC1's own packages require.
- **Backoffice authentication:** every uBookIt policy accepts the backoffice cookie scheme
  (`Constants.Security.BackOfficeAuthenticationType`) as well as OpenIddict. This is exactly the
  pair Umbraco's own policies name. The cross-site request forgery stance is unchanged: Umbraco 19
  states that its Management API has no anti-forgery tokens, and that the cookie's `SameSite` is the
  defence. The same cookie, with `Path=/`, covers uBookIt's routes in the same way (design D2).
- **Email availability:** `IEmailSender.CanSendRequiredEmail()` is gone in RC1. Its replacement,
  `IsEmailConfigured()`, has identical logic: SMTP, or a pickup directory, or a notification
  handler. Three call sites change, plus the test doubles.
- **The delivery OpenAPI document stays at OpenAPI 3.1.** Left alone, .NET 11 moves it to 3.2.0.
  It is pinned to 3.1, which is the version Umbraco 19 pins for its own documents. Chris's rule:
  "stick at 3.1 unless Umbraco itself moves".
- **The declared version becomes `19.0.0-rc1`** (Chris's choice). Guards derive the expected Umbraco
  ceiling and each package description's Umbraco major from it, so the version has to move with the
  host. Only the version literals those guards name are updated.
- **The TestSite gets its own `UserSecretsId` and database (`uBookIt_v19`).** The explore found the
  worktree had inherited v18's id, so starting it would have upgraded `uBookIt_v18` to Umbraco 19
  irreversibly.
- **Backoffice client:** `@umbraco-cms/backoffice` `19.0.0-rc1`, and the client regenerated from the
  RC1 TestSite. In the explore: no conflicts, `tsc` with 0 errors, and vitest 391/391.
- **`umbraco-feedback.md`** in this change: notes on how smooth the upgrade was, for Chris to pass
  to Umbraco. It is seeded from the explore and added to during apply.
- **The RC's documentation (folded in at apply, Chris, 2026-11-13).** Moving the version to
  `19.0.0-rc1` made the documentation guards ask for release paperwork. Chris also now wants to
  publish the RC for real-world testing, as other packages are doing. So this change writes it:
  - a `CHANGELOG.md` entry for `19.0.0-rc1`, left undated until publication;
  - README and Getting started: the Umbraco floor, the install command for a prerelease
    (`--version "19.*"` does not select an RC), the version sentence, and the pinned links and
    images;
  - `docs/theming.md`'s example `net11.0`;
  - `docs/publishing.md`'s version literals;
  - the SameSite sentence;
  - the floor guard, taught to read a prerelease lower bound.

  `release-19-0-0-rc1` then keeps only publication: dating the entry, checking the artifacts, and
  the tag. It follows the CI-for-three-lines change.
- **Two storage guards ignore EF Core 11's `SET NOCOUNT OFF;` preamble** (Chris, 2026-11-13). The
  guarantee they check, one statement with no read first, is unchanged. Only the SQL's leading text
  moved.

## Non-goals

- **Not pushed, and no CI for the line yet.** `dev/v19` stays local until Umbraco 19.0.0 is
  released (Chris, 2026-11-13). So this change's verification is local: every suite, a clean
  Release build and live checks. CI for three lines (`ci.yml` branch filters, the parity script) is
  its own change, before the first push.
- **Not the publication.** No date on the CHANGELOG entry, no artifact checks, no tag, no push.
  Those are `release-19-0-0-rc1`'s. *(Amended at apply: this originally deferred all release
  wording to the release change. The version guards made the documentation part of this change;
  see What Changes.)*
- **Not 19.0.0 final.** Moving the floor to `[19.0.0,20.0.0)` and the version to `19.0.0` is a
  one-line follow-up once Umbraco releases.
- **No behaviour change, and no new feature.** Every spec guarantee holds on 19 as it does on 18.
- **Not `main` or `dev/v18`.** Neither changes. The explore's back-port candidates (none so far) are
  recorded, not applied.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

None. `skip_specs: true`.

Checked rather than assumed:
- **No spec names an authentication scheme.** `openspec/specs` mentions neither OpenIddict, nor
  bearer tokens, nor cookies as a mechanism.
- **No spec states an OpenAPI version.**
- **`packaging` derives every Umbraco major from the declared version**, which is why the version
  moves.
- **`delivery-api`'s "Anonymous access and auth stance"**, including *Ambient identity is not trusted
  for writes*, describes behaviour. It becomes an acceptance criterion here, because on 19 the
  backoffice cookie is the editor's only credential, and it reaches the delivery routes (as it
  already did on 18: `Path=/`).

## Impact

| Area | What changes |
|---|---|
| `global.json`, every `*.csproj`, `Directory.Packages.props` | SDK 11.0.100, `net11.0`, Umbraco RC1, EF Core and OpenApi 11.0.0 |
| `Directory.Build.props` | Version `19.0.0-rc1` |
| `UBookIt.Backoffice/Composers/UBookItAuthorizationComposer.cs` | Both schemes on every policy |
| `UBookItNotificationBootCheck.cs`, `BookingEmailHandler.cs`, `HostMailAvailability.cs` | `IsEmailConfigured()` |
| `UBookIt.Web/Composing/UBookItDeliveryApiComposer.cs` | OpenAPI version pinned to 3.1 |
| `UBookIt.TestSite/UBookIt.TestSite.csproj` | Its own `UserSecretsId` |
| Client | `@umbraco-cms/backoffice` 19.0.0-rc1, lockfile, regenerated `src/api` |
| Tests | Interface members on the test doubles. Guards whose expected values follow the host or framework major (target-framework literals, the Umbraco range) are updated, each one listed with its reason |
| Docs and CHANGELOG | The RC's documentation, folded in at apply (see What Changes) |
| `UmbracoFloorDocumentationTests` | Reads a prerelease lower bound |
| Two integration storage guards | Skip EF Core 11's leading `SET NOCOUNT OFF;` |

**No breaking change to uBookIt's public API.** The host requirement changes, which is the point of
the line. **No schema change:** uBookIt's migrations are untouched, and Umbraco migrates its own
schema on first boot.

**Documentation** *(amended at apply)*. This change updates:
- `README.md`;
- `docs/getting-started.md` (setup: the floor and the prerelease install command);
- `docs/theming.md`;
- `docs/publishing.md`;
- `CHANGELOG.md`.

The SameSite sentence for site operators goes in the getting-started or configuration page:
`Umbraco:CMS:Security:AuthCookieSameSite=None` exposes uBookIt's backoffice API exactly as much as
Umbraco's own. No page is added, so `docs/docs.json` is unchanged.
