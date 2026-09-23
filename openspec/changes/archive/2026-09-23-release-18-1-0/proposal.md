# Release `18.1.0`

## Why

**This release is a correction, not merely the next one.**

`17.2.0` published today carrying site-wide closures and the public holiday import. Its readme —
**frozen inside the package and uncorrectable** — advertises both features, and then tells a
reader on Umbraco 18 to install uBookIt `18.x`. The only released `18.x` is `18.0.0`, which has
neither. So a page live on nuget.org right now sells two capabilities and points half its readers
at a package without them.

Editing `main` cannot fix that: a packed readme is frozen per version. **Publishing `18.1.0` is
the only thing that ends it**, and the window is however long this takes.

The features themselves are already on `dev/v18` — cherry-picked, client regenerated against the
v18 OpenAPI document, suites green. What is missing is the release.

**It is a minor for the same reason `17.2.0` was.** The major tracks the *Umbraco* major and
cannot signal anything about uBookIt's own compatibility; capability lands in a minor. Nothing
here is breaking: `IPublicHolidaySource` is a new interface, not a changed one.

## What this change is for

`17.2.0` is two hours old and everything it learned applies here — but **the 18 line does not
have most of it**, because fixes were made on one line and not carried. That is the defect this
release is most likely to repeat, so the specifics are named rather than trusted to memory.

### Measured before proposing, not assumed

- **`dev/v18` does NOT have `A_release_that_published_a_new_extension_point_names_it`.** Confirmed
  absent from `tests/UBookIt.Tests/ChangelogTests.cs` on this branch. The guard is pinned **per
  release** — on `main` it reads `[InlineData("17.2.0", "IPublicHolidaySource")]` — so this line
  needs **both the guard and its own `18.1.0` row**. Without it, `18.1.0` can ship the same new
  interface unnamed with every test green.
- **`packaging`'s *A release names the contract changes a consumer must act on* has 5 scenarios
  here, 6 on `main`.** The missing one is *A release that publishes a new extension point names
  it* — added by `17.2.0` precisely because a whole new interface obliges a consumer to nothing
  and so fell outside a requirement that reasons entirely from obligation.
- **The bump checklist in `docs/publishing.md` is stale here too, and has been for longer.**
  Confirmed present on this branch: "`README.md` in two places", "the first four disagree", "The
  packed readme's screenshots are addressed", "And the screenshot URLs move with the version",
  "Open the package page and look at the screenshots". This line pinned its documentation links
  at `18.0.0`, so the checklist has been wrong here since then — `17.2.0` only discovered it
  because pinning was fresh.
- **`dev/v18` DOES already have the requirement *A documentation link in the packed readme names
  the release it shipped with*.** It originated here. Nothing to carry; recorded so it is not
  "fixed" twice.
- **The unguarded *Tag the release* literals say `18.0.0`.** Four of them, in the same positions
  as on the 17 line: the example `raw.githubusercontent.com` URL, `git tag`, `git push origin`,
  and the `curl -sI` URL. Nothing reads them.

### The hazards that are the same on any line

- A version number cannot be reused, so a mistake is permanent.
- The tag must exist and be pushed **before** the pack, because both the readme's images and its
  documentation links resolve through it.
- Stale `.nupkg` survive a rebuild — `GenerateNuspec` skips when its outputs look current — and
  are then matched by the push wildcard. Delete `src/*/bin/Release` before packing.
- The two history anchors must not move, and never find-and-replace a version.
- The changelog date is stamped only once the packages are live, and archiving precedes nothing —
  `ChangelogTests` reads the archive.

### One thing `17.2.0` got wrong in its ordering, and this release fixes

`17.2.0` tagged, then ran the readme verification, which **found a defect** — twelve documentation
links naming a branch. The fix landed a commit *after* the tag, so the tag named a commit whose
readme was wrong and had to be deleted and re-pushed. Cheap, because nothing referenced it yet;
impossible after a package push.

**So here the readme verification happens BEFORE tagging.** The tag is the last thing created
before the pack, not the first.

## What changes

Nothing in `src/`. Version metadata, a changelog entry, the stale runbook prose, the missing guard
and the missing scenario — plus whatever verification turns up.

## Impact

**Version metadata**

- `Directory.Build.props`: `<Version>` `18.0.0` → `18.1.0`.

**Documents that state a version and derive nothing**

- `README.md`: the *uBookIt is at* sentence, every screenshot URL, and every documentation link,
  all re-pinned to the `18.1.0` tag.
- `docs/publishing.md`: the *uBookIt is at* sentence; the four unguarded *Tag the release*
  literals; and the stale bump checklist listed above.
- `CHANGELOG.md`: a new `18.1.0` entry, **undated until the packages are live**.

**Carried from `main`, both halves each time**

- The guard `A_release_that_published_a_new_extension_point_names_it`, with an `18.1.0` row.
- `packaging`'s sixth scenario on the contract-change requirement.

## Affected specs

**`packaging` is modified in one requirement** — *A release names the contract changes a consumer
must act on* — to carry the scenario `17.2.0` added. This is a line-sync rather than a new
decision: the requirement's reasoning was settled and reviewed on the 17 line, and leaving the two
lines' copies different is the failure this release exists to stop repeating.

**No other capability is modified.** `site-closures` and `public-holidays` arrived with the
cherry-picks and already describe shipped behaviour in the present tense. Recorded as considered
rather than left silent; if verification finds a claim this release falsifies, it is proposed here
rather than fixed quietly.

## Out of scope

- The three deferred cosmetic nits from `public-holiday-import`, and the standing tidy items. A
  release is the wrong place to spend them.
- Any change to `main`. This line's release must not edit the other's, which is the mirror of the
  mistake being corrected.
