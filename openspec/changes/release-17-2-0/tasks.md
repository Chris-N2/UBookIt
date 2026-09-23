# Tasks

## 1. The version, and the documents that state it

- [ ] 1.1 Bump `<Version>` in `Directory.Build.props` to `17.2.0`, and verify it is the **only**
      `<Version>` element — `DeclaredVersion()` asserts exactly one, because a second would
      silently become the number every document is checked against
- [ ] 1.2 Update the *uBookIt is at* sentence in `README.md` and in `docs/publishing.md`
- [ ] 1.3 Re-pin every screenshot URL in `README.md`'s *What it looks like* to the `17.2.0` tag
- [ ] 1.4 **Verify the two anchors did NOT move** — the `## Status` sentence naming the first
      publish, and the sentence naming the version the public API was declared stable from.
      Confirm by reading them, and by `The_documented_anchors_do_not_move` passing
- [ ] 1.5 Update every version literal in `docs/publishing.md`'s *Tag the release* section — the
      example URL and the `git tag`, `git push` and `curl` commands. **Nothing guards these**;
      verify by eye and record what was changed, so the next release has a list rather than a
      warning
- [ ] 1.6 Verify no version literal was changed anywhere else: diff the working tree and account
      for every changed line naming a version

## 2. The changelog entry

- [ ] 2.1 Write the `17.2.0` entry, heading left **undated**
- [ ] 2.2 Lead with what upgrading asks of the reader, which for this release is nothing — state it
      rather than omitting it
- [ ] 2.3 Describe both capabilities as a consumer meets them, not as the changes were structured
- [ ] 2.4 Name `IPublicHolidaySource` as a new published interface a host may implement, say what
      implementing it enables, state that a site implementing nothing is unaffected, and state that
      the package ships no holiday data for any country — satisfying the scenario this change adds
      to `packaging` rather than only the prose of the proposal
- [ ] 2.5 Verify `ChangelogTests` passes on all six of its guards

## 2a. The requirement this release binds first

- [ ] 2a.1 **MODIFIED** `packaging` / *A release names the contract changes a consumer must act on*
      — the requirement reasons from obligation and all its scenarios describe a member added to an
      existing interface; a whole new interface obliges a consumer to nothing, so it fell outside.
      Diff the guarantees scenario by scenario before and after, and record the counts
- [ ] 2a.2 Verify the new scenario is satisfied by the entry actually written, not merely by the
      entry's intent

## 3. Build, and prove the suite is green from a clean one

- [ ] 3.1 Build the client, then the solution in Release with the TestSite stopped: zero warnings
- [ ] 3.2 Run every suite from a **clean** build — never accept `--no-build` as evidence, which can
      execute stale assemblies and report green
- [ ] 3.3 Run `openspec validate --all --strict`

## 4. Pack, and verify the packed metadata rather than assuming it

- [ ] 4.1 Confirm the working tree is clean and `main` is pushed — publishing from a commit that is
      not on the public repository is what makes SourceLink point nowhere
- [ ] 4.2 **Tag `17.2.0` and push the tag BEFORE packing.** Both readmes' images resolve through
      the tag; packing first produces a readme whose images 404 forever
- [ ] 4.3 Delete prior `.nupkg` output before packing. More than one match for a package id is the
      stale-artifact defect, not a null reference to work around
- [ ] 4.4 Read the packed metadata out of a `.nupkg` (it is a zip) and verify the version, the
      publisher, the icon and the Umbraco dependency bounds
- [ ] 4.5 Verify SourceLink points at the tagged commit on the public repository
- [ ] 4.6 **Fetch every link and image in the packed readme and verify each returns 200**, resolved
      as nuget.org will resolve it — from the package page, not from the repository. This is the
      check `17.0.0` did not have and `17.0.1` paid for

## 5. Publish

- [ ] 5.1 Push all five packages. The key must be passed on the command; there is no
      `dotnet nuget setapikey` in the .NET SDK
- [ ] 5.2 Confirm each package on `api.nuget.org/v3-flatcontainer/<id>/index.json` — **per package,
      not once**. `UBookIt.Core` indexed about eighty seconds before the other four last time
- [ ] 5.3 Verify the live package page renders its readme, its images and its links

## 6. Close the release out, in this order

- [ ] 6.1 Stamp the `17.2.0` entry's date **only once the packages are live**
- [ ] 6.2 Commit, and have the date-stamped commit pushed
- [ ] 6.3 Sync any spec deltas, then archive — **in that order**, and only after the date is
      stamped, because `ChangelogTests` reads the archive
- [ ] 6.4 Run the sibling-falsification sweep over every spec for sentences this release makes
      untrue, and record what was checked and what was found
- [ ] 6.5 Re-check the proposal's claim that `packaging` is the ONLY capability modified, against
      the specs as they stand, and record the result either way

## 7. Hand over to `18.1.0`

- [ ] 7.1 Record what this release learned that the 18 line's release must not rediscover —
      especially anything found in the unguarded *Tag the release* literals
- [ ] 7.2 Confirm `18.1.0` is proposed as its own change on `dev/v18`, and that nothing in this
      change edited that line
