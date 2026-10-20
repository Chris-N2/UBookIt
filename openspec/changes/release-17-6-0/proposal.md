## Why

`service-email-templates` is merged (PR #51, `8a3c612`) and archived (PR #54, `9b4bb45`) on
`main`. It reaches no site until it is published: a service can name an email template set, whose
views uBookIt uses before the views for every service.

## What Changes

1. **Version `17.5.0` → `17.6.0`**, in every place `docs/publishing.md`'s *Before any push* table
   lists. It is a **minor**: it adds public members (`Service.TemplateSet` and its helpers,
   `FailureCodes.ServiceTemplateSetInvalid`, `BookingMessageModel.ServiceId` and `TemplateSet`,
   `RazorBookingTemplateRenderer.SetPathFor` and `IsSuppliedInSet`) and an additive migration.
   Nothing published is removed or changed at the binary or source level;
   `PublishedSignatureTests` and `PublishedCallShapeTests` guard the shapes the change touched. The
   storage-ready hook the change added is internal and is not mentioned.
2. **A `17.6.0` changelog entry**, undated until it is live:
   - *What you have to do:* nothing to make it work (the column is added at startup). **Hard-reload
     a backoffice tab left open across the upgrade**, and send `templateSet` from any tool that
     updates services, because a save without it clears the set (the release-notes obligation
     recorded in `service-email-templates` design 10). A site with its own
     `IBookingTemplateRenderer` must read `Model.TemplateSet` to support sets.
   - *What changes for a site that supplies its own email views:* when the service cannot be read
     while a message is composed, the message is now sent in uBookIt's own wording rather than the
     site's (the deliberate narrowing in `booking-emails`).
   - *What changed:* the field as an editor meets it; the lookup order and its two failure rules;
     the lower-case rule; the startup report; branching on `TemplateSet` rather than `ServiceId`.
   - *For developers:* the new members by exact name, the Management API field, the unchanged
     delivery API and renderer interface, the migration, one more service read per message where
     a renderer is registered, and no dependency changes.
   - What a downgrade does, **checked against `17.5.0`'s code**.
3. **One README bullet** under *For developers*, naming template sets.
4. **Pack from the merged, pushed commit, tag it, verify the artifact, and publish through
   `publish.yml`.** Then date the entry and archive.

## Non-goals

- **`18.5.0`** is its own change, `release-18-5-0`, on `dev/v18`, run back to back with this one.
  A site on `17.6.0` moving to Umbraco 18 should move to `18.5.0`, which carries the same
  migration; `18.4.x` does not map the column.
- **Removing the `Pkcs` and MessagePack declarations** still waits for stable Umbraco `17.8.0` /
  `18.3.0`, due 2026-10-29.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

(none; `skip_specs: true`). The release binds no new requirement.

## Impact

- `Directory.Build.props` (`<Version>`), `README.md`, `docs/publishing.md`, `CHANGELOG.md`.
- Five packages at `17.6.0` on nuget.org, frozen at push.
