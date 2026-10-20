## Why

`service-booker-instructions` is merged (PR #44, `8dfcf69`) and archived (PR #45, `2478474`) on
`dev/v18`. It reaches no Umbraco 18 site until it is published: a service can carry instructions
for the booker, which uBookIt sends to a confirmed booker in its own wording and passes to a site's
own email views.

## What Changes

1. **Version `18.3.0` → `18.4.0`**, in every place `docs/publishing.md`'s *Before any push* table
   lists. It is a **minor**: it adds public members (`Service.BookerInstructions` and its helpers,
   `FailureCodes.ServiceBookerInstructionsTooLong`, `BookerMessageModel.BookerInstructions`, a
   second `BookingMessageComposer` constructor) and an additive migration. Nothing published is
   removed or changed at the binary or source level; `PublishedSignatureTests` and
   `PublishedCallShapeTests` guard `Service.Create` and the composer's published constructor.
2. **An `18.4.0` changelog entry**, undated until it is live. **Below its heading it is `17.5.0`'s
   entry with the version names changed** (`17.5.0` → `18.4.0`, `17.4.x` → `18.3.x`): the feature
   is byte-identical on both lines apart from the generated client's union order, as
   `service-booker-instructions` 8.1 and QA recorded.
3. **One README bullet** under *For your staff*, as on `main`.
4. **Pack from the merged, pushed commit, tag it, verify the artifact, and publish through
   `publish.yml`.** Then date the entry and archive.

## Non-goals

- **`17.5.0`** is its own change, `release-17-5-0`, on `main`, run back to back with this one.
- uBookIt's `18.4.0` is not Umbraco's `18.4.0`: the Umbraco floor stays `[18.2.1, 19.0.0)`, and the
  entry names no Umbraco version.
- **`marketplace-author-details`** changed only `dev/v18`'s docs (the listing file lives on `main`);
  nothing it changed is in a package, so it gets no entry.
- **Removing the `Pkcs` and MessagePack declarations** still waits for stable Umbraco `17.8.0` /
  `18.3.0`, due 2026-10-29.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

(none; `skip_specs: true`). The release binds no new requirement.

## Impact

- `Directory.Build.props` (`<Version>`), `README.md`, `docs/publishing.md`, `CHANGELOG.md`.
- Five packages at `18.4.0` on nuget.org, frozen at push.
