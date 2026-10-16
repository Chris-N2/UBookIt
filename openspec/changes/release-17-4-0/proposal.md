## Why

`single-choice-shortcut` is merged (PR #32, `0a0daff`) and archived (`47ea410`) on `main`. It
reaches no site until it is published: a booking page whose catalogue would offer exactly one
service or resource goes straight to it, behind a new editable setting that is on by default.

**It also publishes `messagepack-advisory`** (re-scoped 2026-10-16), which is merged (PR #34,
`cb23fd6`) and archived (`6aa4d8c`). `UBookIt.Backoffice` and `UBookIt.Web` declare MessagePack
3.1.9, fixing GHSA-qhrr-8q5h-9q3h, which reaches every site through Umbraco 17.7.1. Without it, this
release would break the packaging requirement *A shipped package declares the fix for an advisory
its host cannot yet supply*. That is why QA round 1 put the release on hold.

## What Changes

1. **Version `17.3.2` → `17.4.0`**, in every place `docs/publishing.md`'s *Before any push* table
   lists. It is a **minor**, for two reasons. It adds API members: `SiteBookingSettings.SkipSingleChoiceCatalogue`,
   `UBookItPersistenceComposer.SkipSingleChoiceCatalogueSettingKey`, and a second
   `BookingFlowViewComponent` constructor. And it **changes what an upgraded site renders**: a
   site with a single bookable thing stops showing its catalogue. Nothing published is removed or
   changed, at the binary or the source level, and `PublishedSignatureTests` and
   `PublishedCallShapeTests` guard that. There is no migration and no schema change.
2. **A `17.4.0` changelog entry**, undated until it is live:
   - *What you have to do:* nothing to make it work. **But a site whose booking page offers
     exactly one service or resource now goes straight to it.** To keep showing the catalogue,
     untick *Skip the catalogue when there is only one choice* on uBookIt's settings tab, or set
     `UBookIt:SkipSingleChoiceCatalogue` to `false`. A site with page copy written around a list
     of one should either do that or remove the copy.
   - *What changed:* the feature as a visitor and an operator meet it, including that a
     configured `false` works without the backoffice, and what an unreadable value does.
   - *For developers:* the new members; that a theme's `Catalogue` view is not rendered for a
     catalogue of one while the setting is on; that the delivery API is unchanged; and that the
     Booking Page document type's description changed for **new installs only**, because this release adds no
     migration step and an ordinary upgrade does not re-import the schema.
   - What a downgrade does. A stored value for the setting is a row an older version never reads,
     so nothing breaks. This must be checked against `17.3.2`'s code, not assumed.
   - **The MessagePack advisory:** in *What changed*, what it is, that it reaches every site through
     Umbraco, and that updating uBookIt fixes it without an Umbraco upgrade. In *What you have to
     do*, that a site referencing MessagePack directly below 3.1.9 must raise that reference,
     because restore otherwise fails with NU1605 (measured by `messagepack-advisory`, task 2.4).
     In *For developers*, the new dependency. No claim about how Umbraco uses MessagePack, which
     was never established.
3. **Pack from the merged, pushed commit, tag it, verify the artifact, and publish through
   `publish.yml`.** Then date the entry and archive.

## Non-goals

- **`18.3.0`** is its own change, `release-18-3-0`, on `dev/v18`, run back to back with this one.
  uBookIt's `18.3.0` is **not** Umbraco's `18.3.0`, which is at release candidate. The entry must
  not let the two be confused (uBookIt's major tracks Umbraco's; its minor does not).
- **Removing the `System.Security.Cryptography.Pkcs` and MessagePack declarations** waits for
  stable Umbraco `17.8.0` / `18.3.0`, scheduled for 2026-10-29. Neither is on nuget.org (checked
  2026-10-16: the newest is `18.3.0-rc`).

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

(none; `skip_specs: true`). The release binds no new requirement.

## Impact

- `Directory.Build.props` (`<Version>`, which the parity check exempts), `README.md`,
  `docs/publishing.md`, `CHANGELOG.md`.
- Five packages at `17.4.0` on nuget.org, frozen at push.
