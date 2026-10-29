## Why

Two changes are merged on `main` and reach no site until they are published:

- **`umbraco-floor-17-8`** (PR #63, `e55ba79`; archived in PR #65, `7382f61`):
  - raises the Umbraco floor to `17.8.0`, past the seven advisories Umbraco published on
    2026-10-27, one of them high;
  - removes the `Pkcs` and MessagePack declarations, since Umbraco now brings the fixes;
  - fixes the backoffice's grant wording and the duration hint.
- **`docs-single-source`** (PR #59, `fcbcec8`; archived in PR #61, `4a8dba0`): restructures the
  documentation, with new *Getting started* and *Email templates* pages and a `docs/docs.json`
  manifest. It deliberately wrote no changelog entry, so this release owes it.

A site installing uBookIt today gets `17.6.0`, which still admits a vulnerable Umbraco `17.7.1`.

## What Changes

1. **Version `17.6.0` → `17.7.0`**, in every place `docs/publishing.md`'s *Before any push* table
   lists. It is a **minor**, because the Umbraco floor rises. A site on Umbraco `17.7.x` cannot
   take this version without upgrading Umbraco first. That is not an API break: no published member
   is added, removed or changed. But a patch should never ask a site to change its host, and this
   one does.
2. **A `17.7.0` changelog entry**, undated until it is live:
   - *What you have to do:*
     - uBookIt now requires **Umbraco 17.8.0 or later**: why (the advisories), and to upgrade
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
     - EF Core 10.0.12;
     - the `Pkcs` and MessagePack declarations gone, and why that lowers nothing a site resolves;
     - no public API change.
   - What a downgrade to `17.6.x` does, **checked against `17.6.0`'s nuspecs**.

   **Three constraints carried from the changes:**
   - The entry MUST NOT say that Umbraco's advisories never reach the advisory database NuGet reads.
     That claim was false, and QA caught it in `umbraco-floor-17-8` round 1.
   - It cannot contain the retired literals "Default permissions" or "uBookIt Section". The
     retired-claims sweep covers `CHANGELOG.md`, so it says "the user group editor's permission
     switches" instead.
   - It must say the install command now gives `--version "17.*"`. A bare `dotnet add package UBookIt`
     on an Umbraco 17 site resolves to the 18 line and fails restore with NU1107. That obligation is
     recorded from `docs-single-source`.
3. **Pack from the merged, pushed commit, tag it, verify the artifact, and publish through
   `publish.yml`.** Then date the entry and archive.
4. **The Trusted Publishing observation is deferred (Chris, 2026-10-29, after QA round 1).** The plan
   was to disable the personal policy so the publish had to match the organisation-owned one. QA
   found that cannot work yet. nuget.org lists `Norwood-Design-and-Development` as an owner of
   `UBookIt` only; `UBookIt.Core`, `.Persistence`, `.Backoffice` and `.Web` are owned by `CNorwood69`
   alone. A policy applies only to the packages its owner owns, so with the personal policy disabled
   the first library push would be refused (403), not the login. This release therefore publishes
   with **both policies enabled**, as before. Chris adds the organisation as co-owner of the four
   libraries afterwards, and the next release runs the observation. `docs/publishing.md` is not
   changed by this release.

## Non-goals

- **`18.6.0`** is its own change, `release-18-6-0`, on `dev/v18`, run back to back with this one.
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
- Five packages at `17.7.0` on nuget.org, frozen at push.
