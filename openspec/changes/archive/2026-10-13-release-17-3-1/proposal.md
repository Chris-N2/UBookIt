## Why

`tidy-2026-10` is merged (PR #23, `5269a2b`) and archived (`f0a9a47`) on `main`, and none of its
product fixes reach a site until they are published:
- the Closed periods hints sit under their own period's fields;
- the holiday import summary pluralises, and leaves out "0 rows were skipped";
- a refused holiday request (401/403) says it was refused, not that the source failed;
- a holiday source that goes away while the Closures screen is open withdraws the import again.
  That rule had been broken since the holiday import, because the client throws on a non-2xx
  response and only the thrown path ran. The tidy change's live check found it and fixed it.

`17.3.1` is the 17 line's half of the release. It goes first because features land on `main`
first. `18.2.1` follows as its own change.

## What Changes

1. **Version `17.3.0` → `17.3.1`**, in every place listed in the table in `docs/publishing.md`'s
   *Before any push*. It is a **patch**:
   - no public type, member, endpoint, setting or migration is added or changed;
   - `PublishedSignatureTests` and `PublishedCallShapeTests` guard that;
   - the only differences a site sees are backoffice markup and wording.
2. **A `17.3.1` changelog entry**, undated until it is live:
   - *What you have to do:* nothing.
   - *What changed:* the four fixes above, as an operator meets them. That includes the exact
     new wording of the summary and the refusal, since a site that overrides backoffice terms
     may want to know.
   - The documentation changes are in the packed readme and `docs/`: the window sentence, both
     majors named, and the 2048 figure now guarded. Name them only where an operator would notice.
   - CI-only changes ship nothing, so the entry does not list them.
3. **Pack from the merged, pushed commit, tag it, verify the artifact, and publish through
   `publish.yml`.** Then date the entry and archive.

## Non-goals

- **`18.2.1`** is its own change, `release-18-2-1`, on `dev/v18`, run back to back with this one.
- **The single-choice shortcut** (go straight to dates when only one resource or service is
  available) is the next feature, not part of a patch.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

(none; `skip_specs: true`). The release binds no new requirement.

## Impact

- `Directory.Build.props` (`<Version>`, which the parity check exempts), `README.md`,
  `docs/publishing.md`, `CHANGELOG.md`.
- Five packages at `17.3.1` on nuget.org, frozen at push.
