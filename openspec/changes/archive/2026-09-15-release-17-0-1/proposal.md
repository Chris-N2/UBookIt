## Why

uBookIt `17.0.0` was pushed to nuget.org on 2026-09-15 and all five packages are indexed and
restorable. Two things follow from that, and neither is optional.

**The repository still says the package has not been published.** `docs/publishing.md` carries a
Status line written to be *wrong* at this moment — it is the trigger for the feed-arrival guard
(`No_document_claims_the_package_has_reached_a_feed`, renamed during apply to
`Every_mention_of_the_feed_is_accounted_for` once publication expired its premise), whose failure
is the checklist of every sentence that publication has made stale. That checklist is now due.

**Publishing revealed two defects in the package page itself**, neither visible before there was
a page to look at. The packaged README carries nine relative
links: eight documentation pages and the licence. nuget.org resolves each of them against the
package page's own URL rather than against the repository, so every one 404s — the links a
prospective user follows to learn what uBookIt does are dead. And the package has no icon, so it renders as a generic
placeholder in nuget.org search and in the Visual Studio Package Manager. Both are being fixed
now rather than left to ride 17.1.0, because the package page is about to be linked from
Umbraco's Discord and submitted to the Umbraco Marketplace, and it is the first thing anyone
will see.

A packaged README is frozen per version exactly like the rest of the metadata — NuGet's own
documentation says a correction requires pushing a new version — so this is a release, not an
edit.

## What Changes

- **The version becomes `17.0.1`.** A patch: no API change, no schema change, no behavioural
  change. Under the project's own policy a patch carries fixes only, which is what this is.
- **Every link in `README.md` becomes an absolute URL** into the public GitHub repository, so it
  resolves identically from a nuget.org package page, the Package Manager UI, and GitHub itself.
  A new guard fails the build if a relative link or a relative image is reintroduced.
- **The package gains an icon** — the Norwood Design & Development app-icon mark, packed into all
  five packages via `PackageIcon`. A new guard asserts it is present, square, a supported format,
  within NuGet's size limit, and packed by every packable project.
- **The repository records that publication happened.** The Status line is rewritten, the
  sentences the feed-arrival guard flags are worked through one at a time, and each that has
  become true is admitted to `AcceptedPublicationMentions` with its reason and count.
- **`docs/publishing.md` gains what the first publish actually taught**, replacing guesses made
  before anyone had done it:
  - The push command becomes PowerShell. It used a trailing `\` line
    continuation, which is bash syntax and breaks in the shell this project is driven from — as
    did the artifact-deletion and verification snippets, found by sweeping the class.
  - **An API key scoped and spelled correctly still fails with 403 if its *owner* cannot publish.**
    A key owned by a nuget.org organization whose email address is unconfirmed — which is what
    happens if the organization was given an address already belonging to a member account —
    produces a 403 whose text names only the key. This cost the first publish attempt.
  - What the minutes after a push look like: accepted, validating, indexed; the package presents
    as unlisted throughout; the flat-container endpoint is the signal that restore will work.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `packaging`: two **ADDED** requirements — that documentation a consumer follows from a package
  page resolves, and that the package presents an icon.
- `packaging`: one **MODIFIED** requirement — *The version a reader is told is the version the
  package carries*. The bump to `17.0.1` exposed a defect in it that could not exist before there
  was a second version: it requires **every** stated version to equal the declared one, but four
  of the six statements it finds are anchors to history — *the first release is `17.0.0`*, *declared
  stable from `17.0.0`* — which are true precisely because they do **not** move. Satisfying the
  requirement as written would mean editing four true sentences into false ones. The modification
  splits current-version claims from anchors and gives anchors their own check: they must agree with
  the version the repository's own immutable archive records as the first release, and must
  additionally agree with one another and name no version later than the declared one. All four existing scenarios are restated under **their upstream titles unchanged** — two
  verbatim, two with reworded WHEN/THEN clauses that confine them to current-version claims — and
  five are added. **Nothing is deleted, and the one narrowing is disclosed rather than
  silent**: the leading SHALL no longer reaches anchors, which is stated in the requirement body
  and diffed guarantee-by-guarantee in `tasks.md`.

## Non-goals

- **Unlisting `17.0.0`.** Deliberate, not an oversight. nuget.org serves `17.0.1` as the default
  landing page once it indexes, so the flawed README is reached only by deliberately selecting an
  older version; unlisting would hide a version that is otherwise perfectly functional and leave a
  gap in the history for no benefit. Unlisting is also not deletion — 17.0.0 stays installable by
  exact version either way.
- **A uBookIt-specific product mark.** The icon is the publisher's brand. A package-specific mark
  is a design job, not a release task.
- **CI, GitHub Actions, or Trusted Publishing.** Still absent, still wanted, still a separate change.
- **Submitting to the Umbraco Marketplace**, and any change to the `umbraco-marketplace` tag that
  already drives it.
- **Re-verifying what 17.0.0 shipped.** Its artifacts were checked before the push and are frozen.

## Impact

- `Directory.Build.props` — `Version`, `PackageIcon`, and the icon's `None` pack item.
- `README.md` — nine links rewritten; no prose change beyond those links.
- A new image asset committed to the repository, and packed into all five packages.
- `docs/publishing.md` — Status, the push command, the key-ownership failure mode, post-push states.
- `openspec/config.yaml` and any document the feed-arrival guard flags.
- `tests/UBookIt.Tests/VersionTruthTests.cs` — `AcceptedPublicationMentions`, plus the two new guards.
- `roadmap/version_roadmap.md` — a sentence claiming its version line is build-checked, which the
  anchor/current split falsified.
- `src/UBookIt.Backoffice/Models/BookingModels.cs` — **XML documentation on a public type**, which
  ships in the package and reached a consumer's IntelliSense saying the package was unpublished.
  Documentation only: no behaviour, signature or attribute changes.
- `openspec/specs/booking-management/spec.md`, `openspec/specs/delivery-api/spec.md`,
  `openspec/specs/resource-management/spec.md` — the same `BREAKING (unpublished)` wording, edited
  **in place as prose rather than through a delta**. Deliberate: no requirement's meaning changes,
  only a parenthetical that publication made false. Recorded here because neither
  `ChangeDeltaIntegrityTests` nor `openspec validate` can see an in-place spec edit, so Impact is
  the only place a human approves it.
- **No change to any shipped assembly's behaviour, public API, schema, or migration.**
