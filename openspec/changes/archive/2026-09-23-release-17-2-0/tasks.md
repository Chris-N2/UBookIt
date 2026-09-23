# Tasks

## 1. The version, and the documents that state it

- [x] 1.1 Bump `<Version>` in `Directory.Build.props` to `17.2.0`, and verify it is the **only**
      `<Version>` element — `DeclaredVersion()` asserts exactly one, because a second would
      silently become the number every document is checked against
- [x] 1.2 Update the *uBookIt is at* sentence in `README.md` and in `docs/publishing.md`
- [x] 1.3 Re-pin every screenshot URL in `README.md`'s *What it looks like* to the `17.2.0` tag
- [x] 1.4 **Verify the two anchors did NOT move** — the `## Status` sentence naming the first
      publish, and the sentence naming the version the public API was declared stable from.
      Confirm by reading them, and by `The_documented_anchors_do_not_move` passing
- [x] 1.5 Update every version literal in `docs/publishing.md`'s *Tag the release* section — the
      example URL and the `git tag`, `git push` and `curl` commands. **Nothing guards these**;
      verify by eye and record what was changed, so the next release has a list rather than a
      warning
      *Four, exactly as the standing note predicted: the example `raw.githubusercontent.com` URL,
      `git tag <v>`, `git push origin <v>`, and the `curl -sI` URL. All four inside the section
      between `## Tag the release before you push the package` and `## Pushing`. No other version
      literal in that file needed to move.*
- [x] 1.6 Verify no version literal was changed anywhere else: diff the working tree and account
      for every changed line naming a version
      *Eleven changed lines across three files, every one accounted for: the `<Version>` element,
      the two "uBookIt is at" sentences, four screenshot URLs and the four Tag-the-release
      literals. **README.md line 58 was deliberately NOT changed** — "from `17.1.2` the packages
      say so" is a statement about when the host bound was introduced, and moving it forward would
      make it false.*

## 2. The changelog entry

- [x] 2.1 Write the `17.2.0` entry, heading left **undated**
- [x] 2.2 Lead with what upgrading asks of the reader, which for this release is nothing — state it
      rather than omitting it
- [x] 2.3 Describe both capabilities as a consumer meets them, not as the changes were structured
- [x] 2.4 Name `IPublicHolidaySource` as a new published interface a host may implement, say what
      implementing it enables, state that a site implementing nothing is unaffected, and state that
      the package ships no holiday data for any country — satisfying the scenario this change adds
      to `packaging` rather than only the prose of the proposal
- [x] 2.5 Verify `ChangelogTests` passes on all six of its guards

## 2a. The requirement this release binds first

- [x] 2a.1 **MODIFIED** `packaging` / *A release names the contract changes a consumer must act on*
      — the requirement reasons from obligation and all its scenarios describe a member added to an
      existing interface; a whole new interface obliges a consumer to nothing, so it fell outside.
      Diff the guarantees scenario by scenario before and after, and record the counts
      *Diffed: 5 scenarios in, 6 out, none dropped; every SHALL carried forward verbatim. The
      added scenario is "A release that publishes a new extension point names it".*
- [x] 2a.2 Verify the new scenario is satisfied by the entry actually written, not merely by the
      entry's intent
      *Made verifiable rather than asserted: `A_release_that_published_a_new_extension_point_names_it`
      pins the `17.2.0` entry to naming `IPublicHolidaySource` AND to saying a site implementing
      nothing is unaffected. Both halves mutation-tested — renaming the interface in the entry
      fails the first, removing the reassurance fails the second. Pinned per release because the
      previous version's assembly is not present to diff against, so "which interfaces are new"
      cannot be derived inside the suite.*

## 2b. What the pre-publish verification found — and it was owed to this release

- [x] 2b.1 **The packed readme's twelve documentation links named `blob/main`, not the release.**
      The readme is frozen per published version while the documents it links to are fetched live,
      so `17.2.0`'s page would have described whatever `main` holds years from now. This is not a
      new discovery: `release-18-0-0` found it, fixed it **on the 18 line only**, and it was
      recorded as owed at the next 17-line release. This is that release, and it is the last
      moment it can be fixed for `17.2.0` — after the push the readme cannot be corrected
- [x] 2b.2 Pin all twelve to the `17.2.0` tag. Verified none remain on a branch ref
- [x] 2b.3 **Carry BOTH halves across from `dev/v18`, not one.** Moving a guard without its
      requirement, or a requirement without its guard, is the recorded failure mode that left this
      defect on one line in the first place:
      - the guard `The_readme_documentation_links_name_the_release_they_shipped_with`, mutation-
        tested here — reverting a single link to `blob/main` fails it by name
      - the requirement *A documentation link in the packed readme names the release it shipped
        with*, as an `ADDED` delta with all five of its scenarios
- [x] 2b.4 Verify the link addresses resolve on the tag once it is moved to the final commit

## 3. Build, and prove the suite is green from a clean one

- [x] 3.1 Build the client, then the solution in Release with the TestSite stopped: zero warnings
- [x] 3.2 Run every suite from a **clean** build — never accept `--no-build` as evidence, which can
      execute stale assemblies and report green
- [x] 3.3 Run `openspec validate --all --strict`
      *Release build from a cleared client output and `--no-incremental`: 0 warnings, 0 errors.
      Unit 1950, integration 183, rendering 1168, client 335. `validate --all --strict` 25/25.
      The TestSite was stopped first — it holds the Release output and the build cannot be trusted
      while it runs.*

## 4. Pack, and verify the packed metadata rather than assuming it

- [x] 4.1 Confirm the working tree is clean and `main` is pushed — publishing from a commit that is
      not on the public repository is what makes SourceLink point nowhere
- [x] 4.2 **Tag `17.2.0` and push the tag BEFORE packing.** Both readmes' images resolve through
      the tag; packing first produces a readme whose images 404 forever
- [x] 4.3 Delete prior `.nupkg` output before packing. More than one match for a package id is the
      stale-artifact defect, not a null reference to work around
      *Four stale `17.1.2` artifacts were present and were deleted. This is not hypothetical
      housekeeping: `GenerateNuspec` skips when its outputs look up to date, so they would have
      survived the rebuild and been matched by the push wildcard.*
- [x] 4.4 Read the packed metadata out of a `.nupkg` (it is a zip) and verify the version, the
      publisher, the icon and the Umbraco dependency bounds
      *Five packages, exactly one `.nupkg` each: all `17.2.0`, publisher `Norwood Design &
      Development Ltd.`, `icon.png` present, and every `Umbraco.Cms.*` dependency bounded
      `[17.6.2, 18.0.0)` — the bound `17.1.2` exists to carry.*
- [x] 4.5 Verify SourceLink points at the tagged commit on the public repository
      *All four `.snupkg` resolve to `db1ed39`, which is the tagged commit and is on origin.
      Four rather than five is expected — the `UBookIt` metapackage carries no assembly.*
- [x] 4.6 **Fetch every link and image in the packed readme and verify each returns 200**, resolved
      as nuget.org will resolve it — from the package page, not from the repository. This is the
      check `17.0.0` did not have and `17.0.1` paid for
      *Sixteen addresses verified against the pushed tag: four images and twelve documentation
      links, all 200. One returned 503 on the first attempt; retried rather than assumed either
      way, and it is 200 — the file is present at the tag and its raw form resolves too.*
      *The readme INSIDE the package was then read out of the `.nupkg` rather than inferred from
      the working tree: zero relative links, every doc link and image pinned to `17.2.0`, and its
      "uBookIt is at" sentence says `17.2.0`.*

## 5. Publish

- [x] 5.1 Push all five packages. The key must be passed on the command; there is no
      `dotnet nuget setapikey` in the .NET SDK
- [x] 5.2 Confirm each package on `api.nuget.org/v3-flatcontainer/<id>/index.json` — **per package,
      not once**. `UBookIt.Core` indexed about eighty seconds before the other four last time
      *All five confirmed present on the flat container — `ubookit`, `ubookit.core`,
      `ubookit.persistence`, `ubookit.web`, `ubookit.backoffice`. Checked per package rather than
      inferred from one, and from the flat container rather than the website, which lags.*
- [x] 5.3 Verify the live package page renders its readme, its images and its links
      *The `17.2.0` package page returns 200, and the two reference kinds the packed readme
      depends on both resolve at the tag: a pinned image and a pinned documentation link.*

## 6. Close the release out, in this order

- [x] 6.1 Stamp the `17.2.0` entry's date **only once the packages are live**
- [x] 6.2 Commit, and have the date-stamped commit pushed
- [x] 6.3 Sync any spec deltas, then archive — **in that order**, and only after the date is
      stamped, because `ChangelogTests` reads the archive
      *`packaging` synced: 21 requirements to 22, 88 scenarios to 94, verified against a snapshot
      taken BEFORE the sync rather than against a report of it. `git diff --numstat` shows 78
      insertions and 0 deletions — a pure addition, so the wholesale replacement was byte-identical
      apart from what it added and nothing could have been dropped unseen.*
- [x] 6.4 Run the sibling-falsification sweep over every spec for sentences this release makes
      untrue, and record what was checked and what was found
      *Full record in `sweep.md`. Seven sentences in `docs/publishing.md` were falsified by this
      release's own doc-link pinning — the bump checklist stopped enumerating everything that
      moves, which is precisely the defect class this release exists to guard against. All seven
      fixed. One of them is PINNED, so the guard failed on the edit and the pin was moved
      deliberately rather than the sentence left alone.*
- [x] 6.5 Re-check the proposal's claim that `packaging` is the ONLY capability modified, against
      the specs as they stand, and record the result either way
      *Holds. The sweep read all 24 specs and found no other requirement this release falsifies or
      leaves incomplete; `site-closures` and `public-holidays` are written in present-tense SHALL
      form and so survive their own publication. Everything else the sweep found was in shipped
      documents, not specs.*
      *One finding has no edit as its remedy: `README.md` tells an Umbraco 18 reader to install
      `18.x`, which today means `18.0.0` — a release carrying neither feature the page advertises.
      `17.2.0`'s copy of that sentence is frozen in the package, so only shipping `18.1.0` ends
      the mismatch. Carried into `release-18-1-0`.*

## 7. Hand over to `18.1.0`

- [x] 7.1 Record what this release learned that the 18 line's release must not rediscover —
      especially anything found in the unguarded *Tag the release* literals

      **The four unguarded literals, now an inventory rather than a warning.** In
      `docs/publishing.md`, between `## Tag the release before you push the package` and
      `## Pushing`: the example `raw.githubusercontent.com` URL, `git tag <v>`,
      `git push origin <v>`, and the `curl -sI` URL. Nothing reads them. On the 18 line they will
      say `18.0.0`.

      **The bump checklist is now correct on `main` and STALE ON `dev/v18`.** This release found
      seven sentences in `docs/publishing.md` falsified by its own doc-link pinning — the "three
      documents / two places" count, the bump table's missing row, "the first four" guarded
      things, and four sentences in *Tag the release* that justify the tag on image grounds alone.
      The 18 line pinned its links at `18.0.0` and so has carried the same stale prose for longer.
      **Check whether `dev/v18`'s runbook has the same seven defects before bumping anything.**

      **The stale-artifact deletion is load-bearing.** Four `17.1.2` `.nupkg` were present and
      would have survived the rebuild — `GenerateNuspec` skips when its outputs look current — and
      been matched by the push wildcard. Delete `src/*/bin/Release` before packing, every time.

      **A 503 is not a 404.** One documentation link failed on first fetch and was 200 on retry.
      Retry before recording an address as broken, and before recording it as fine.

      **The tag had to be moved once.** A defect found by the pre-publish verification landed a
      commit after the tag was already pushed, so the tag named a commit whose readme was wrong.
      Deleting and re-pushing a tag is cheap while no package references it, and impossible after.
      **Do the readme verification BEFORE tagging next time**, not between tagging and packing.

- [x] 7.2 Confirm `18.1.0` is proposed as its own change on `dev/v18`, and that nothing in this
      change edited that line
      *Nothing in this change touched `dev/v18`: every commit is on `main`, and the cherry-picks
      that put the features on the 18 line were made before this change existed.*

      **What `release-18-1-0` must carry that this release added, and would otherwise ship
      without — each is a one-line-only mistake waiting to happen, which is the exact shape of the
      defect this release found:**

      1. **`A_release_that_published_a_new_extension_point_names_it`** — added to
         `ChangelogTests` on `main` only. It is pinned PER RELEASE (`[InlineData("17.2.0",
         "IPublicHolidaySource")]`), so the 18 line needs both the guard AND its own
         `18.1.0` row, or it ships the same new interface unnamed with every test green.
      2. **The `packaging` requirement *A release names the contract changes a consumer must act
         on*** gained a sixth scenario about publishing a new interface. `dev/v18` has the
         five-scenario version.
      3. **The seven `docs/publishing.md` corrections** listed under 7.1.

      **And the reason `18.1.0` is a correction rather than the next item:** `17.2.0`'s packed
      readme — frozen, uncorrectable — tells an Umbraco 18 reader to install `18.x`, while
      advertising two features no released `18.x` has. That misdirection is live on nuget.org from
      now until `18.1.0` publishes. See `sweep.md`.
