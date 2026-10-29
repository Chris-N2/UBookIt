## Why

Two changes are merged on `dev/v18` and reach no site until they are published:

- **`umbraco-floor-17-8`** (PR #64, `1a05632`; archived in PR #66, `777cdf8`):
  - raises the Umbraco floor to `18.3.0`, past the seven advisories Umbraco published on
    2026-10-27, one of them high;
  - removes the `Pkcs` and MessagePack declarations, since Umbraco now brings the fixes;
  - fixes the backoffice's grant wording and the duration hint.
- **`docs-single-source`** (PR #60, `1b6bb2c`; archived in PR #62, `d456023`): restructures the
  documentation, with new *Getting started* and *Email templates* pages and a `docs/docs.json`
  manifest. It deliberately wrote no changelog entry, so this release owes it.

A site installing uBookIt today gets `18.5.0`, which still admits a vulnerable Umbraco `18.2.1`.

## What Changes

1. **Version `18.5.0` → `18.6.0`**, in every place `docs/publishing.md`'s *Before any push* table
   lists. It is a **minor**, because the Umbraco floor rises. A site on Umbraco `18.2.x` cannot
   take this version without upgrading Umbraco first. That is not an API break: no published member
   is added, removed or changed. But a patch should never ask a site to change its host, and this
   one does.
2. **A `18.6.0` changelog entry**, undated until it is live:
   - *What you have to do:*
     - uBookIt now requires **Umbraco 18.3.0 or later**: why (the advisories), and to upgrade
       `Umbraco.Cms` itself first;
     - nothing else.
   - *What changed:*
     - the refusal messages and the permission switches' descriptions now say where a grant is given
       and how;
     - the duration hint shows only for a variable length;
     - the documentation is restructured;
     - the install command now names the line;
     - the README states SQL Server 2019 or later.
   - *For developers:* the dependency changes, by package:
     - the Umbraco minimum;
     - EF Core 10.0.12, and `Microsoft.AspNetCore.OpenApi` 10.0.12 (this line only);
     - the `Pkcs` and MessagePack declarations gone, and why that lowers nothing a site resolves;
     - no public API change.
   - What a downgrade to `18.5.x` does, **checked against `18.5.0`'s nuspecs**.

   **Three constraints carried from the changes:**
   - The entry MUST NOT say that Umbraco's advisories never reach the advisory database NuGet reads.
     That claim was false, and QA caught it in `umbraco-floor-17-8` round 1.
   - It cannot contain the retired literals "Default permissions" or "uBookIt Section". The
     retired-claims sweep covers `CHANGELOG.md`, so it says "the user group editor's permission
     switches" instead.
   - It must say the install command now gives `--version "18.*"`. A bare `dotnet add package UBookIt`
     picks the newest uBookIt. That is right for an Umbraco 18 site today, but it stops being right
     once a later line ships, and it is already wrong on an Umbraco 17 site, where it fails restore
     with NU1107. That obligation is recorded from `docs-single-source`. *(Not copied from
     `release-17-7-0`'s wording, which is about an Umbraco 17 site and would be false here.)*
3. **Pack from the merged, pushed commit, tag it, verify the artifact, and publish through
   `publish.yml`.** Then date the entry and archive.
4. **The Trusted Publishing observation.** Before approving, Chris **disables** (does not delete) his
   personal Trusted Publishing policy, so the publish must match the organisation-owned policy. That
   settles the open question of which policy matches. `NuGet/login` runs before any push, so a
   failure there costs no version: re-enable the policy and re-run the job. Whatever is seen goes
   into `docs/publishing.md` (*Trusted Publishing setup* names `CNorwood69` as owner) in the
   close-out, on both lines.

## Non-goals

- **`17.7.0`** is its own change, `release-17-7-0`, on `main`, run back to back with this one.
- Removing Chris's personal ownership of the packages. That waits until a release has published
  through the organisation policy, and is recorded afterwards.
- The v18 client's move to `@umbraco-cms/backoffice` 18.x, which was split off from
  `umbraco-floor-17-8`.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

(none; `skip_specs: true`). The release binds no new requirement.

## Impact

- `Directory.Build.props` (`<Version>`), `README.md`, `docs/publishing.md`, `CHANGELOG.md`.
- At close-out, `docs/publishing.md` (*Trusted Publishing setup*, *Status* outstanding bullets) with
  whatever the publish shows.
- Five packages at `18.6.0` on nuget.org, frozen at push.
