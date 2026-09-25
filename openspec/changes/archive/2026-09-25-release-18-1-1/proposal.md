## Why

`17.2.1` went live on 2026-09-25 (`archive/2026-09-25-release-17-2-1`). The Umbraco 18 line has
the same two unreleased changes, and on this line one of them corrects a defect that shipped:

- **`usable-privacy-link`** (`9cd14ef` on this line, not an ancestor of the `18.1.0` tag). A
  site-relative privacy policy link such as `/privacy` is refused on Linux hosts, and the
  settings screen refused the site-relative form everywhere. Ruled patch-eligible on 2026-09-24.
- **`marketplace-listing`** (`97cb7ee` here). **`UBookIt 18.0.0` and `18.1.0` describe themselves
  on nuget.org and the Marketplace as "A booking system for Umbraco 17 … Requires Umbraco 17
  (LTS)".** Only a new version can replace that description as the latest. The libraries also
  stop asking to be listed.

**This line is red on origin and has to be turned green first.** It is red for the reason `main`
was: `marketplace-listing`'s archive synced two nuget.org mentions into
`openspec/specs/packaging/spec.md`, and `Every_mention_of_the_feed_is_accounted_for` fails on
them (run `36128749573` on `5eb344e`). `17.2.1` fixed `main` with a `6 → 8` registration. That
fix has to be made **by hand** here, because this line's registration entry differs from main's
around that line.

## What Changes

1. **Register the two mentions** by raising `packaging/spec.md`'s nuget.org count `6 → 8`, with
   reasons and feed evidence equivalent to `main`'s. The spec sentences are byte-identical on both
   lines (`marketplace-listing` synced the same text). The entry's existing prose, which differs
   from main's, is left as it is.
2. **The version: `18.1.0` → `18.1.1`**, in the places `docs/publishing.md` step 3 lists:
   `<Version>`, the README's *uBookIt is at* sentence and all 16 pins, the runbook's *uBookIt is
   at* sentence (line 14), and its *Tag the release* literals (266, 292–294). History does not
   move: runbook 401 and `packaging/spec.md:1022`.
3. **Copy the dated `17.2.1` entry into this line's changelog, unedited**, below `18.1.0`, the
   precedent `release-18-1-0` set (`a4fcb0f`: "copying history is not editing it"). The `18.1.1`
   entry refers to it, and the README sends readers to this file.
4. **An `18.1.1` entry**, undated until live. *What you have to do:* **nothing**. It covers the
   same four changes as `17.2.1`, in wording that survived four QA rounds there:
   - no category sentence for the refusals;
   - the Linux sentence scoped to a site-relative link.

   Plus the 18-line difference: **the description correction is a fix here, not a tidy-up**.
   `18.0.0`/`18.1.0` said Umbraco 17, and `18.1.1` says Umbraco 18.
5. **PR into `dev/v18`**, merged with a merge commit. Pack from the merge commit once its own push
   run is green, then tag, verify, publish, date and archive, by `17.2.1`'s procedure.

A **patch**: no API signature, schema or migration change. The one narrowing is the one ruled
patch-eligible.

## Non-goals

- **Changing the lines' shared prose.** `publishing.md:83` and `:397` ("until `17.2.1`/`18.1.1`")
  become fully true when this publishes. That is verified, not edited.
- **Anything beyond what `17.2.1` shipped.** The bound-guard port (`847f102`) is test-only and
  gets no entry.
- **Marketplace delisting.** It is checked after this publishes, and it does not gate the archive.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

(none; `skip_specs: true`).

## Impact

- `tests/UBookIt.Tests/VersionTruthTests.cs`, `Directory.Build.props` (`<Version>` only, which
  parity exempts), `README.md`, `docs/publishing.md`, `CHANGELOG.md`.
- Five packages at `18.1.1` on nuget.org, frozen at push.
