## Why

`service-booker-instructions` is merged (PR #43, `d744154`) and archived (PR #46, `6ced37b`) on
`main`. It reaches no site until it is published: a service can carry instructions for the booker,
which uBookIt sends to a confirmed booker in its own wording and passes to a site's own email views.

## What Changes

1. **Version `17.4.0` → `17.5.0`**, in every place `docs/publishing.md`'s *Before any push* table
   lists. It is a **minor**: it adds public members (`Service.BookerInstructions` and its helpers,
   `FailureCodes.ServiceBookerInstructionsTooLong`, `BookerMessageModel.BookerInstructions`, a
   second `BookingMessageComposer` constructor) and an additive migration. Nothing published is
   removed or changed at the binary or source level; `PublishedSignatureTests` and
   `PublishedCallShapeTests` guard `Service.Create` and the composer's published constructor.
2. **A `17.5.0` changelog entry**, undated until it is live:
   - *What you have to do:* nothing to make it work (the column is added at startup), but **reload
     a backoffice tab left open across the upgrade**, and send `bookerInstructions` from any tool
     that updates services, because a save without it clears them (QA's release-notes obligation
     from `service-booker-instructions`). A site with its own email views must render
     `Model.BookerInstructions` to pass them on.
   - *What changed:* the field as an editor meets it; exactly which messages carry the
     instructions, by the booking's status; where they sit in uBookIt's wording; read at send time;
     not shown on the public site; not a place for secrets.
   - *For developers:* the new members by exact name, the Management API field, the unchanged
     delivery API, the migration, and no dependency changes.
   - What a downgrade does, **checked against `17.4.0`'s code**.
3. **One README bullet** under *For your staff*, naming the field: the feature change did not touch
   the README's feature list.
4. **Pack from the merged, pushed commit, tag it, verify the artifact, and publish through
   `publish.yml`.** Then date the entry and archive.

## Non-goals

- **`18.4.0`** is its own change, `release-18-4-0`, on `dev/v18`, run back to back with this one.
  uBookIt's `18.4.0` is not Umbraco's `18.4.0`; the entry does not mention Umbraco versions.
- **`marketplace-author-details`** (merged since `17.4.0`) changed only the repository's
  `umbraco-marketplace.json` and a logo, which are in no package. It is already live on the
  Marketplace and gets no changelog entry, which records what a release asks of an upgrading site.
- **Removing the `Pkcs` and MessagePack declarations** still waits for stable Umbraco `17.8.0` /
  `18.3.0`, due 2026-10-29.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

(none; `skip_specs: true`). The release binds no new requirement.

## Impact

- `Directory.Build.props` (`<Version>`), `README.md`, `docs/publishing.md`, `CHANGELOG.md`.
- Five packages at `17.5.0` on nuget.org, frozen at push.
