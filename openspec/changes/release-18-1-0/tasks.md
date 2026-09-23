# Tasks

## 1. Carry from `main` what the 17 line fixed — BOTH halves each time

Done first, because a guard added after the entry is written cannot tell you the entry was wrong.

- [ ] 1.1 Carry `A_release_that_published_a_new_extension_point_names_it` into `ChangelogTests`,
      with an `[InlineData("18.1.0", "IPublicHolidaySource")]` row. **Verify by mutation** that
      both halves fire: removing the interface name from the entry, and removing the sentence
      saying a site implementing nothing is unaffected
- [ ] 1.2 Carry `packaging`'s sixth scenario — *A release that publishes a new extension point
      names it* — as a `MODIFIED` delta on *A release names the contract changes a consumer must
      act on*. Diff the guarantees: this line has 5 scenarios, `main` has 6, and none of the 5 may
      be lost
- [ ] 1.3 Fix the stale bump checklist in `docs/publishing.md`, measured on THIS branch rather
      than assumed from the 17 line's diff: the "in two places" count, the table's missing row for
      documentation links, "the first four" guarded things, and the *Tag the release* sentences
      that justify the tag on image grounds alone
- [ ] 1.4 Verify nothing was carried that this line already has — it originated *A documentation
      link in the packed readme names the release it shipped with*, and fixing that twice would
      be its own defect

## 2. The version, and the documents that state it

- [ ] 2.1 Bump `<Version>` to `18.1.0`; verify it is the only `<Version>` element
- [ ] 2.2 Update the *uBookIt is at* sentence in `README.md` and `docs/publishing.md`
- [ ] 2.3 Re-pin every screenshot URL **and every documentation link** in `README.md` to the
      `18.1.0` tag
- [ ] 2.4 Update the four unguarded *Tag the release* literals, and record which four
- [ ] 2.5 **Verify the two anchors did NOT move** — the first-publish sentence and the API
      stability sentence
- [ ] 2.6 Audit the whole diff: account for every changed line naming a version, and name any
      version literal deliberately left alone

## 3. The changelog entry

- [ ] 3.1 Write the `18.1.0` entry, heading **undated**
- [ ] 3.2 Lead with what upgrading asks of the reader
- [ ] 3.3 Describe both capabilities as a consumer meets them
- [ ] 3.4 Name `IPublicHolidaySource`, what implementing it enables, and that a site implementing
      nothing is unaffected — satisfying the scenario carried in 1.2, verified by the guard
      carried in 1.1 rather than by reading it over
- [ ] 3.5 State that the package ships no holiday data for any country
- [ ] 3.6 Verify every `ChangelogTests` guard passes, including the newly carried one

## 4. Build, and prove the suite is green from a clean one

- [ ] 4.1 Build the client, then the solution in Release with the TestSite stopped: zero warnings
- [ ] 4.2 Run every suite from a clean build — never accept `--no-build` as evidence
- [ ] 4.3 Run `openspec validate --all --strict`

## 5. Verify the readme BEFORE tagging

**This ordering is the correction `17.2.0` paid for.** It tagged first, then verified, then found
a defect, and had to delete and re-push a tag.

- [ ] 5.1 Confirm the working tree is clean and `dev/v18` is pushed
- [ ] 5.2 Confirm **no** readme link or image names a branch rather than the release — the check
      whose absence cost `17.2.0` a tag
- [ ] 5.3 Confirm every readme address is absolute; a relative one is the `17.0.0` defect
- [ ] 5.4 Only then tag `18.1.0` and have the tag pushed
- [ ] 5.5 Fetch every readme address against the pushed tag and require 200 — **retry a failure
      before believing it**, because a 503 is not a 404

## 6. Pack, and verify the packed metadata rather than assuming it

- [ ] 6.1 Delete `src/*/bin/Release` before packing. `GenerateNuspec` skips when its outputs look
      current, so a stale `18.0.0` `.nupkg` would survive the rebuild and be matched by the push
      wildcard
- [ ] 6.2 Clean rebuild, then pack; verify exactly one `.nupkg` per package
- [ ] 6.3 Read the packed metadata out of the `.nupkg` and verify version, publisher, icon, and
      that every `Umbraco.Cms.*` dependency is bounded to the **18** range, not the 17 one
- [ ] 6.4 Verify SourceLink resolves to the tagged commit, and that the commit is on origin
- [ ] 6.5 Read the readme **out of the package** and confirm it is the pinned one — not the
      working tree's copy, which is not what ships

## 7. Publish

- [ ] 7.1 Push all five packages
- [ ] 7.2 Confirm each on `api.nuget.org/v3-flatcontainer/<id>/index.json` **per package**
- [ ] 7.3 Open the package page: images render, and a documentation link lands
- [ ] 7.4 **Confirm the correction actually happened** — `18.1.0` is installable and carries both
      features, so `17.2.0`'s frozen "install `18.x`" sentence is now true

## 8. Close out, in this order

- [ ] 8.1 Stamp the `18.1.0` entry's date once the packages are live
- [ ] 8.2 Commit, and have it pushed
- [ ] 8.3 Sync the spec delta, then archive — in that order
- [ ] 8.4 Run the sibling-falsification sweep over every spec, and record what was checked and
      what was found
- [ ] 8.5 Re-check the proposal's claim that `packaging` is the only modified capability
- [ ] 8.6 Record what now differs between the two lines and why, so the next release on either one
      starts from a statement of the difference rather than discovering it
