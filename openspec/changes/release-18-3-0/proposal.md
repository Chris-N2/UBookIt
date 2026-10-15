## Why

`single-choice-shortcut` is merged into `dev/v18` (PR #33, `3cdc04a`) and archived there
(`3357f94`). It reaches no site on the 18 line until it is published: a booking page whose
catalogue would offer exactly one service or resource goes straight to it, behind a new editable
setting that is on by default. The maintainer's own site runs the 18 line and has exactly one
bookable resource, so it is the first site this changes.

## What Changes

1. **Version `18.2.2` → `18.3.0`**, in every place `docs/publishing.md`'s *Before any push* table
   lists. It is a **minor**, for two reasons. It adds API members: `SiteBookingSettings.SkipSingleChoiceCatalogue`,
   `UBookItPersistenceComposer.SkipSingleChoiceCatalogueSettingKey`, and a second
   `BookingFlowViewComponent` constructor. And it **changes what an upgraded site renders**: a
   site with a single bookable thing stops showing its catalogue. Nothing published is removed or
   changed, at the binary or the source level, and `PublishedSignatureTests` and
   `PublishedCallShapeTests` guard that. There is no migration and no schema change.
2. **An `18.3.0` changelog entry**, undated until it is live. It has the same substance as `main`'s
   `17.4.0` entry (`release-17-4-0`), and the text below the heading should be byte-identical
   unless a line difference requires otherwise:
   - *What you have to do:* nothing to make it work. **But a site whose booking page offers
     exactly one service or resource now goes straight to it.** To keep showing the catalogue,
     untick *Skip the catalogue when there is only one choice*, or set
     `UBookIt:SkipSingleChoiceCatalogue` to `false`.
   - *What changed*, *For developers*, and what a downgrade does, as in `17.4.0`.
3. **Pack from the merged, pushed commit, tag it, verify the artifact, and publish through
   `publish.yml`.** Then date the entry and archive.

## Non-goals

- **`17.4.0`** is `main`'s change, `release-17-4-0`, run back to back with this one.
- **This is uBookIt `18.3.0`, not Umbraco `18.3.0`.** Umbraco's is at release candidate, and this
  release does not move uBookIt's Umbraco floor (still `[18.2.1, 19.0.0)`). uBookIt's major tracks
  Umbraco's and its minor does not. The entry must not let a reader think uBookIt `18.3.0`
  requires or follows Umbraco `18.3.0`.
- **Removing the `System.Security.Cryptography.Pkcs` pin** waits for stable Umbraco `18.3.0`.
  It is not on nuget.org (checked 2026-10-15: the newest is `18.3.0-rc`).

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

(none; `skip_specs: true`). The release binds no new requirement.

## Impact

- `Directory.Build.props` (`<Version>`, which the parity check exempts), `README.md`,
  `docs/publishing.md`, `CHANGELOG.md`.
- Five packages at `18.3.0` on nuget.org, frozen at push.
