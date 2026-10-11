## Why

`closure-date-ranges` is merged (`4bdeeab`) and archived (`2f5150c`) on `main`, and reaches no site
until it is published:
- closures as date ranges;
- per-resource closed periods;
- a booking page that looks past a closure covering today.

The maintainer's own site runs the 18 line and goes live as soon as `18.2.0` publishes. `17.3.0`
is the 17 line's half of the same release, and is published first because features land on `main`
first.

## What Changes

1. **Version `17.2.3` → `17.3.0`**, in every place `docs/publishing.md`'s *Before any push* table
   lists. It is a **minor**: a feature, an additive migration, and additive API members. Nothing
   published is removed or changed, at the binary or the source level, and
   `PublishedSignatureTests` and `PublishedCallShapeTests` guard that.
2. **A `17.3.0` changelog entry**, undated until it is live:
   - *What you have to do:* nothing to make it work, because the migration applies itself. It
     states what a downgrade loses.
   - *What changed:* the feature, as an operator meets it.
   - *For developers:* the API additions, and the two omission rules. An update without
     `closedPeriods` leaves them; one without `endDate` makes a single day, which is the trade-off
     the change's proposal requires the changelog to state. Plus the new members, the defaulted
     interface members, and what a custom `IResourceStore` or a theme replacing
     `_AvailableDates` must do.
3. **Pack from the merged, pushed commit, tag it, verify the artifact, and publish through
   `publish.yml`.** Then date the entry and archive.

## Non-goals

- **`18.2.0`** is its own change, `release-18-2-0`, on `dev/v18`, run back to back with this one.
- **The single-choice shortcut** the maintainer raised on 2026-10-11 (go straight to dates when
  there is only one resource or service) is a later change.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

(none; `skip_specs: true`). The release binds no new requirement.

## Impact

- `Directory.Build.props` (`<Version>`, which the parity check exempts), `README.md`,
  `docs/publishing.md`, `CHANGELOG.md`.
- Five packages at `17.3.0` on nuget.org, frozen at push.
