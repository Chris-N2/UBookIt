## Why

`tidy-2026-10` is merged (PR #22, `1e05ccd`) and archived (`a541c78`) on `dev/v18`, and none of its
product fixes reach a site until they are published:
- the Closed periods hints sit under their own period's fields;
- the holiday import summary pluralises, and leaves out "0 rows were skipped";
- a refused holiday request (401/403) says it was refused, not that the source failed;
- a holiday source that goes away while the Closures screen is open withdraws the import again.
  That rule had been broken since the holiday import, because the client throws on a non-2xx
  response and only the thrown path ran. The tidy change's live check found it and fixed it.

`18.2.1` is the 18 line's half of the release, and follows `release-17-3-1` on `main`. The
maintainer's own site runs the 18 line.

## What Changes

1. **Version `18.2.0` → `18.2.1`**, in every place listed in the table in `docs/publishing.md`'s
   *Before any push*. It is a **patch**:
   - no public type, member, endpoint, setting or migration is added or changed;
   - `PublishedSignatureTests` and `PublishedCallShapeTests` guard that;
   - the only differences a site sees are backoffice markup and wording.
2. **An `18.2.1` changelog entry**, undated until it is live. It is `17.3.1`'s entry with the line's
   own versions: the 404 repair restores behaviour broken since `18.1.0`, which shipped the
   holiday import on this line.
3. **Pack from the merged, pushed commit, tag it, verify the artifact, and publish through
   `publish.yml`.** Then date the entry and archive.

## Non-goals

- **`17.3.1`** is its own change, `release-17-3-1`, on `main`.
- **The single-choice shortcut** is the next feature, not part of a patch.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

(none; `skip_specs: true`). The release binds no new requirement.

## Impact

- `Directory.Build.props` (`<Version>`, which the parity check exempts), `README.md`,
  `docs/publishing.md`, `CHANGELOG.md`.
- Five packages at `18.2.1` on nuget.org, frozen at push.
