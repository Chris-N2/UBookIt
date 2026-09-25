## 1. Turn `main` green

- [ ] 1.1 Raise `AcceptedPublicationMentions`' `packaging/spec.md` / `nuget.org` count `6 → 8` and
  extend its reason in the entry's form: what synced, which sentence is a host fact, and which is a
  publication claim allowed on evidence, citing the feed check in the proposal. Verify:
  `Every_mention_of_the_feed_is_accounted_for` passes from a clean build. Then prove it can still
  fire: add one more "nuget.org" sentence to the spec locally, watch it fail naming the file, and
  revert.

## 2. The version, and the documents that state it

- [ ] 2.1 Bump `<Version>` to `17.2.1` in `Directory.Build.props`. Verify it is the only declared
  version (`git grep -n "<Version>"`).
- [ ] 2.2 Move every row of `docs/publishing.md` step 3's table: the README *uBookIt is at*
  sentence, every README screenshot URL and documentation link, the runbook's *uBookIt is at*
  sentence, and the *Tag the release* literals. The last are unguarded, so re-read them by eye.
- [ ] 2.3 Verify the anchors did NOT move: the `## Status` first-publish sentence, the stable-API
  sentence, and runbook line 282 ("`17.2.0` is the release that pinned the links"). Diff the
  working tree and account for every changed version literal.

## 3. The changelog entry

- [ ] 3.1 Write the `17.2.1` entry, undated. It opens with *What you have to do*, which says
  **nothing**, then covers (a)–(d) from the proposal. (c) is stated as a narrowing, with what
  happens to a value already stored. (d) says the libraries stop asking to be listed, and that
  delisting their existing pages is up to the Marketplace. Every claim in the entry must be
  checked against the code or the feed, not against this list.
- [ ] 3.2 Verify `ChangelogTests` passes.

## 4. Build, and prove the suite green from a clean build

- [ ] 4.1 Stop the TestSite if it is running. Delete `src/*/bin/Release`,
  `src/UBookIt.Backoffice/wwwroot/App_Plugins/UBookItBackoffice` and
  `src/UBookIt.Backoffice/obj/Release`. Run `CI=true dotnet build UBookIt.slnx -c Release
  --no-incremental`. Verify: 0 warnings.
- [ ] 4.2 Run every suite without `--no-build` evidence from an older build. Verify: unit **1982**,
  integration 191, rendering 1168, client 335, all with 0 skipped, and
  `openspec validate --all --strict` passes.

## 5. Review and merge

- [ ] 5.1 QA review in a subagent (`qa-review`), reused across rounds.
- [ ] 5.2 Chris pushes the branch and opens the PR into `main`. Verify: the PR's CI run is green
  **at every step, parity included** (D2), read through the API.
- [ ] 5.3 Chris merges with **"Create a merge commit"**. Fetch, and verify `origin/main`'s tip is
  the merge commit and the local `main` equals it.

## 6. Tag, pack and verify, from the merge commit

- [ ] 6.1 On `main` at the merge commit, with a clean tree: `git branch -r --contains HEAD` lists
  `origin/main`. Tag `17.2.1` and have it pushed. Verify
  `curl -sI https://raw.githubusercontent.com/Chris-N2/UBookIt/17.2.1/docs/images/booking-flow.png`
  returns 200.
- [ ] 6.2 Delete prior build output again (4.1's list), then `dotnet build -c Release
  --no-incremental` and `dotnet pack UBookIt.slnx -c Release`. Verify exactly one `.nupkg` per id,
  all `17.2.1`.
- [ ] 6.3 Read each of the five nuspecs, not one:
  - version, publisher, icon, readme and licence;
  - every `Umbraco.Cms.*` dependency bounded `[17.6.2, 18.0.0)`;
  - `umbraco-marketplace` on `UBookIt` alone;
  - `UBookIt`'s description names Umbraco 17.
- [ ] 6.4 SourceLink: the four `.snupkg`/`sourcelink.json` resolve to the tagged merge commit on
  the public repository.
- [ ] 6.5 Read the README **out of the `.nupkg`**: no relative links, every link and image pinned
  to `17.2.1`, and "uBookIt is at `17.2.1`". Fetch every link and image address in it. Verify each
  returns 200, retrying a 503 before recording anything.

## 7. Publish

- [ ] 7.1 Chris pushes the five packages with the API key.
- [ ] 7.2 Confirm each package on `api.nuget.org/v3-flatcontainer/<id>/index.json`, **per
  package**.
- [ ] 7.3 Verify the live package page renders its readme, images and links.

## 8. Close out, in this order

- [ ] 8.1 Stamp the entry's date once the packages are live.
- [ ] 8.2 Sibling sweep: sentences this release falsifies, including `docs/publishing.md`'s "until
  17.2.1/18.1.1" wording (true for this line once live; the 18 half becomes true with `18.1.1`).
  Fix what is falsified, and record the findings here. Record here what `release-18-1-1` must
  carry, including the same `6 → 8` registration.
- [ ] 8.3 Commit on `main` (D3), run the unit suite, and have Chris push. Verify: `main`'s CI run is
  green at every step.
- [ ] 8.4 Archive **last**, so that no box it records is still open. **Run the unit suite locally
  before the push** (D3), and verify it is green. The archive push's own CI run is checked and
  reported in the session, because the record is frozen by then.
