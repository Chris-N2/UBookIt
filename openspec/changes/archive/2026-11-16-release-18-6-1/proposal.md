## Why

`backoffice-client-upgrade` (PR #72 on this line, `3670d0d`; archived in PR #74) regenerated the
backoffice section's API client with `@hey-api/openapi-ts` 0.99, against `@umbraco-cms/backoffice`
18.3.1. Until then this line's section was built against the 17.x backoffice package (17.5.3). The
shipped `UBookIt.Backoffice` bundle therefore differs from `18.6.0`'s, and reaches no site until it
is published. That change wrote no changelog entry, because no release was in it. The deferred
obligations record that the next release on each line owes one.

It is released now together with `17.7.1` and `19.0.0-rc1` (Chris, 2026-11-16: "All three"), so
that every line ends level with what is published.

## What Changes

1. **Version `18.6.0` → `18.6.1`**, in every place `docs/publishing.md`'s *Before any push* table
   lists: `<Version>`, 25 README pins (24 tag-pinned links and screenshots, plus the *uBookIt is
   at* sentence), the runbook's *uBookIt is at* sentence, and its four *Tag the release* literals.
   It is a **patch**. Nothing a site resolves changes: since `18.6.0` there is no change to
   `Directory.Packages.props`, `Directory.Build.props` (apart from `<Version>`), `global.json` or
   any project file. The only `src/` changes are the regenerated client code, its package files
   and generator script, and the Backoffice project's contributor `README.txt`.
2. **A `18.6.1` changelog entry**, undated until it is live:
   - *What you have to do:* nothing, with the reasons (no API, schema, configuration or
     dependency change; going back is safe);
   - *What changed:* the regenerated client, now against Umbraco 18's own types, and why (18.x requires the new
     generator); no
     behaviour change intended, and the live checks that showed none;
   - *For contributors:* Node 24.13+ / npm 11+ to build the client, CI on Node 24, and CI now
     verifying three lines with prerelease tags allowed (`ci-three-lines`).
3. **Pack from the merged, pushed commit, tag it, check the artifact, and publish through
   `publish.yml`**, with **both Trusted Publishing policies left enabled**. Then date the entry and
   archive. nuget.org still lists the organisation as an owner of `UBookIt` only (checked
   2026-11-16), so the observation deferred at `17.7.0` / `18.6.0` stays deferred.

4. **The 18 line's requirement rows are corrected (QA round 1; Chris chose wording now, range
   later).** uBookIt 18.x's Umbraco range `[18.3.0, 19.0.0)` admits Umbraco 19 *prereleases*,
   because `19.0.0-rc1` sorts below `19.0.0`. So "and not 19" was false for Umbraco 19's release
   candidates: a bare install there gets an 18.x without complaint, and it does not work. The README
   and *Getting started* now say so. Changing the range itself would change what a site resolves,
   so it is not a patch. It is recorded as a deferred obligation, to be done in one change for all
   three lines (the ceiling excluding the next major's prereleases, and the guard that wrongly
   requires `X.0.0`). The CHANGELOG header also names `19.x` now.

## Non-goals

- `17.7.1` and `19.0.0-rc1` are their own changes, `release-17-7-1` on `main` and
  `release-19-0-0-rc1` on `dev/v19`, run back to back with this one.
- The Trusted Publishing observation (above).
- `scripts/verify-install.ps1`, which is still owed as its own change.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

(none; `skip_specs: true`). The release binds no new requirement.

## Impact

- `Directory.Build.props`, `README.md`, `docs/publishing.md`, `docs/getting-started.md` and
  `CHANGELOG.md`.
- No code. Docs pages: the README (version literals and the Umbraco requirement row),
  `docs/getting-started.md` (the Umbraco requirement row: a 19 release candidate is not refused) and
  `docs/publishing.md` (version literals). Setup itself does not change.
