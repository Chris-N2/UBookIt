## Why

Two merged changes have reached no consumer, because nothing has been packed since `17.2.0`:

- **`usable-privacy-link`** (`327783e`). A site-relative privacy policy link such as `/privacy`,
  the form `docs/backoffice.md` gives as its example, is **refused on Linux hosts**. The booking
  form shows no policy link, and every startup logs an error. The settings screen also refused the
  site-relative form on every platform. The maintainer ruled it patch-eligible on 2026-09-24
  (`archive/2026-09-24-usable-privacy-link/tasks.md` 6.3).
- **`marketplace-listing`** (`fbe36e7`). Only `UBookIt` asks to be listed on the Umbraco
  Marketplace, and its description takes its Umbraco major from its own version. Nuspec metadata
  is frozen at push, so the libraries' Marketplace listings can only stop being asked for by a
  new version.

It is the **first release verified by CI**, and the first whose release commits go through a pull
request.

**`main` is red, and this change has to turn it green first.** Archiving `marketplace-listing`
synced its two requirements into `openspec/specs/packaging/spec.md`. They mention nuget.org twice,
and `VersionTruthTests.Every_mention_of_the_feed_is_accounted_for` fails on unregistered
mentions. The mentions sat unscanned in the change's delta until the sync. CI run `36128749454`
on `d3fe838` failed on exactly that test and nothing else, and a clean local build reproduces it.
The same thing happened at `docs-truth-and-screenshots` and `release-17-1-2`, and the
registration's own reason records both.

## What Changes

1. **Register the two mentions** by raising `AcceptedPublicationMentions`' count for
   `packaging/spec.md` from `6` to `8`, with the reason extended in the entry's existing form:
   - *"A package's description is the first thing nuget.org and the Marketplace show"* describes
     what nuget.org does as a host.
   - *"A library version already published keeps its tag, because nuget.org does not allow a
     pushed version's metadata to be edited"* **is a publication claim, allowed on evidence.** On
     2026-09-25 the feed listed `17.0.0`…`17.2.0`, `18.0.0` and `18.1.0` for
     `ubookit.persistence`, `ubookit.web` and `ubookit.backoffice`. The `17.2.0` and `18.1.0`
     nuspecs of all three carry `umbraco-marketplace`.
2. **The version: `17.2.0` → `17.2.1`**, in every place `docs/publishing.md` step 3 lists. That
   list is the authority, not this proposal:
   - `<Version>`;
   - the README's *uBookIt is at* sentence and every pinned screenshot and documentation link;
   - the runbook's *uBookIt is at* sentence and its *Tag the release* literals.

   The history anchors don't move. That includes runbook line 282, "`17.2.0` is the release that
   pinned the links".
3. **A `17.2.1` changelog entry**, undated until it is live. *What you have to do:* **nothing**.
   It then describes, as a consumer meets them:
   - (a) **Fixed:** a site-relative privacy policy link is no longer refused on Linux hosts.
   - (b) **Changed:** the settings screen accepts a site-relative link.
   - (c) **Changed, stated as a narrowing:** the settings screen now refuses an absolute http(s)
     link containing a control character or a backslash. It used to store such a link, but the
     site never rendered it. This is `SettingValidation.IsValid`'s public behaviour, with no
     signature change. **A value already stored stays stored**: the screen saves one setting at a
     time (`SettingsController.cs:130` validates only the value being saved), so it blocks
     nothing else.
   - (d) **Package metadata:** only `UBookIt` carries `umbraco-marketplace`. Its description names
     the Umbraco major derived from its version. That doesn't change anything on the 17 line,
     because `17.x` already said 17, apart from dropping "(LTS)".
4. **Pack from the merged, pushed commit, tag it, verify, and publish**, then date the entry and
   archive, per `docs/publishing.md`.

A **patch**: no API signature, schema or migration change. The one behavioural narrowing is (c),
ruled patch-eligible on the record.

## Non-goals

- **`18.1.1`.** It gets its own change, `release-18-1-1`, on `dev/v18`, with its own entry. That
  line's suite is red for the same reason, and **its fix (the same `6 → 8` registration) lands in
  that change**, in the same sitting as this one.
- **The Marketplace delisting check.** The live library pages are checked after publication and
  recorded as an obligation. That check does not gate the archive, because the Marketplace
  rescans on its own schedule.
- **Trusted Publishing.** This release pushes with the existing API key.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

(none; `skip_specs: true`). This release binds no new requirement. `packaging`'s release
requirements are satisfied by what it carries, and the test-data edit changes no spec.

## Impact

- `tests/UBookIt.Tests/VersionTruthTests.cs` (one registration), `Directory.Build.props`
  (`<Version>`, a line CI's parity check exempts), `README.md`, `docs/publishing.md`,
  `CHANGELOG.md`.
- Five packages at `17.2.1` on nuget.org, frozen at push.
