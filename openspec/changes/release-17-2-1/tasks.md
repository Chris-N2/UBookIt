## 1. Turn `main` green

- [x] 1.1 Raise `AcceptedPublicationMentions`' `packaging/spec.md` / `nuget.org` count `6 → 8` and
  extend its reason in the entry's form: what synced, which sentence is a host fact, and which is a
  publication claim allowed on evidence, citing the feed check in the proposal. Verify:
  `Every_mention_of_the_feed_is_accounted_for` passes from a clean build. Then prove it can still
  fire: add one more "nuget.org" sentence to the spec locally, watch it fail naming the file, and
  revert.

  *Done:* count `6 → 8`, with the reason extended to name both sentences: the host fact, and the
  publication claim with its feed evidence. From a clean build the guard passes. A ninth
  "nuget.org" sentence appended to the spec failed it, naming `openspec/specs/packaging/spec.md`.
  The spec was restored.
## 2. The version, and the documents that state it

- [x] 2.1 Bump `<Version>` to `17.2.1` in `Directory.Build.props`. Verify it is the only declared
  version (`git grep -n "<Version>"`).
  *Done:* `git grep "<Version>"` finds only `Directory.Build.props:53` `17.2.1`.
- [x] 2.2 Move every row of `docs/publishing.md` step 3's table: the README *uBookIt is at*
  sentence, every README screenshot URL and documentation link, the runbook's *uBookIt is at*
  sentence, and the *Tag the release* literals. The last are unguarded, so re-read them by eye.
  *Done:* README: all 17 `17.2.0` literals were pins or the *is at* sentence (4 images, 12 doc
  links and LICENSE, plus the sentence), and all moved. Runbook: line 14 (*is at*), line 266
  (example URL) and lines 294–296 (`git tag`, `git push`, `curl`), re-read by eye.
- [x] 2.3 Verify the anchors did NOT move: the `## Status` first-publish sentence, the stable-API
  sentence, and runbook line 282 ("`17.2.0` is the release that pinned the links"). Diff the
  working tree and account for every changed version literal.

  *Done:* `git grep 17.2.0` over the three files leaves only runbook line 282, the history
  anchor. The `## Status` and stable-API anchors name other versions and were not touched.
  `git diff --stat`: props 1 line, README 17, runbook 5, plus the 1.1 test edit.
## 3. The changelog entry

- [x] 3.1 Write the `17.2.1` entry, undated. It opens with *What you have to do*, which says
  **nothing**, then covers (a)–(d) from the proposal. (c) is stated as a narrowing, with what
  happens to a value already stored. (d) says the libraries stop asking to be listed, and that
  delisting their existing pages is up to the Marketplace. Every claim in the entry must be
  checked against the code or the feed, not against this list.
  *Done:* every claim was checked against the code or docs.
  - The Linux failure is `17.2.0`'s `ResolvePrivacyPolicyUrl` (`Uri.TryCreate` absolute before the
    relative branch).
  - The startup error is `RunUBookItMigrations.ErrorIfPrivacyPolicyUrlUnusable` (`LogError`).
  - `/privacy` is the example at `docs/backoffice.md:288`.
  - The new refusal text is `SettingValidation.cs:90`.
  - Per-setting save is `SettingsController.cs:130`.
- [x] 3.2 Verify `ChangelogTests` passes.

  *Done:* `ChangelogTests` and `VersionTruthTests`: 20 passed.
## 4. Build, and prove the suite green from a clean build

- [x] 4.1 Stop the TestSite if it is running. Delete `src/*/bin/Release`,
  `src/UBookIt.Backoffice/wwwroot/App_Plugins/UBookItBackoffice` and
  `src/UBookIt.Backoffice/obj/Release`. Run `CI=true dotnet build UBookIt.slnx -c Release
  --no-incremental`. Verify: 0 warnings.
  *Done:* no TestSite process was running. I deleted `src/*/bin/Release`, the backoffice
  `App_Plugins` output and `obj/Release`. `CI=true` build with `--no-incremental`: 0 warnings,
  0 errors.
- [x] 4.2 Run every suite without `--no-build` evidence from an older build. Verify: unit **1982**,
  integration 191, rendering 1168, client 335, all with 0 skipped, and
  `openspec validate --all --strict` passes.

  *Done:* unit **1982**, integration **191**, rendering **1168**, all 0 skipped. Client
  **335**. `openspec validate --all --strict`: 26/26.
## 5. Review and merge

- [x] 5.1 QA review in a subagent (`qa-review`), reused across rounds.
  *Round 1: REJECT* on `fe30b10`.
  - **MAJOR:** the entry said the booking form "has always accepted" a site-relative link,
    directly under the paragraph saying Linux refused it.
  - **MINOR:** "lead off-site" misnamed the refusal category, since an absolute
    `https://other.example` is off-site and accepted.
  - **Three plan MINORs:**
    - gate the pack on the merge commit's own push run, not the PR's synthetic merge ref (6.1);
    - check `UBookIt.*` sibling dependency versions (6.3);
    - an explicit recovery if verification finds a defect after the tag is pushed (6.6).

  Round-2 commit `734ae7b` addressed all five. QA confirmed all five handover claims by running
  them, apart from the refuted parts of claim 4.

  *Round 2: REJECT* on `734ae7b`.
  - The MAJOR and the three plan MINORs were verified fixed. QA measured on Windows that
    `/privacy` is not absolute there, and read that Linux has no route to rendering a
    site-relative link at `17.2.0`.
  - **But the refusal-category rewrite made a new false generalisation**: `mailto:`, `tel:`,
    `ftp:` and `file:` are refused, yet they neither run script nor disguise an off-site link.
    The rule is an allow-list, so no single "because" is true. Any category sentence fails on
    `https://other.example` (round 1) or on `mailto:` (round 2).
  - Fix: drop the category and state the list, plus the one reason that is true
    (`javascript:`).
  - This record's earlier line "All five were fixed in the round-2 commit" was written before
    review and was not true of the category MINOR. It is corrected here rather than left
    standing (QA NIT).
  - `2493142` also fixed a defect the implementer caught before sending round 3. The first
    rewrite (`d087771`) said "Nothing that used to be refused is accepted now", which is false
    for `/privacy` on Linux. "Apart from that" scoped it.

  *Round 3: APPROVE WITH NITS* on `2493142`.
  - QA compared `17.2.0`'s resolver with HEAD's on both platforms. On Windows it measured a
    sample of `/`-rooted values: seven single-slash values, plus `//host/x` and `///x`. It then
    reasoned from the two rules that the sets `17.2.0`-on-Windows and HEAD accept are equal, and
    that `//` is refused by both. "Every" is reasoning, not measurement. So "Apart from that, nothing that used to be refused is accepted now" holds
    on Windows and Linux.
  - **NIT, taken:** the refusal list read as complete, but a value with no leading slash
    (`privacy`) is also refused. It was made non-exhaustive ("including") rather than extended,
    because a fifth item could itself be incomplete. QA asked to read that edit before the tag.

  *Round 4: APPROVE* on `52ceb2b`, for the entry as it will be frozen, re-read sentence by
  sentence against `17.2.0` and HEAD. Two NITs, both taken:
  - The entry: "If it runs on Linux and its privacy policy link was missing" also covered sites
    whose link was missing for other reasons, which stay missing. It is now scoped to "its
    site-relative privacy policy link", in QA's wording.
  - The record: this task's round-3 bullet said QA measured "every" value. That is corrected to
    a sample plus reasoning.
- [x] 5.2 Chris pushes the branch and opens the PR into `main`. Verify: the PR's CI run is green
  **at every step, parity included** (D2), read through the API.
  *Done:* PR **#3**. Run `36135686155` on `e57288f` was success at every step, **parity
  included**, read through the API. D2's prediction held.
- [x] 5.3 Chris merges with **"Create a merge commit"**. Fetch, and verify `origin/main`'s tip is
  the merge commit and the local `main` equals it.

  *Done:* merged as **`6f0da77`** with a merge commit. After a fetch, `origin/main` = local
  `main` = `6f0da77`.
## 6. Tag, pack and verify, from the merge commit

- [x] 6.1 On `main` at the merge commit, with a clean tree: `git branch -r --contains HEAD` lists
  `origin/main`. **The merge commit's own `ci` push run on `main` must be green at every step**,
  read through the API. The PR's run tested GitHub's synthetic merge ref, not this SHA, so the
  commit we pack has to be CI-verified in its own right (QA round 1). Tag `17.2.1` and have it
  pushed. Verify
  `curl -sI https://raw.githubusercontent.com/Chris-N2/UBookIt/17.2.1/docs/images/booking-flow.png`
  returns 200.
  *Done:*
  - `git branch -r --contains 6f0da77` lists `origin/main`.
  - The merge commit's own push run, `36136147859`, was success at every step, parity included.
    `main` is green again, after being red since `d3fe838`.
  - Tag `17.2.1` was created on `6f0da77` and pushed by Chris. `git ls-remote` shows
    `6f0da775… refs/tags/17.2.1`.
  - The `curl` of `booking-flow.png` at the tag returned 200 (in the 6.5 sweep).
- [x] 6.2 Delete prior build output again (4.1's list), then `dotnet build -c Release
  --no-incremental` and `dotnet pack UBookIt.slnx -c Release`. Verify exactly one `.nupkg` per id,
  all `17.2.1`.
  *Done:* no TestSite running and no leftover `.nupkg`. The 4.1 outputs were deleted, then a
  `CI=true` build with `--no-incremental` (0 warnings) and `dotnet pack`. Result: exactly five
  `.nupkg` and four `.snupkg`, all `17.2.1`.
- [x] 6.3 Read each of the five nuspecs, not one:
  - version, publisher, icon, readme and licence;
  - every `Umbraco.Cms.*` dependency bounded `[17.6.2, 18.0.0)`;
  - **every `UBookIt.*` dependency names `17.2.1`** (QA round 1). A stale sibling version would
    freeze a meta-package that installs `17.2.0` libraries, without the fix this release exists
    for;
  - `umbraco-marketplace` on `UBookIt` alone;
  - `UBookIt`'s description names Umbraco 17.
  *Done:* all five read.
  - Every package: publisher `Norwood Design & Development Ltd.`, licence MIT, and `icon.png` and
    `README.md` declared **and present** in the zip. Repository commit `6f0da77`.
  - Sibling dependencies:
    - `UBookIt` → Backoffice 17.2.1 and Web 17.2.1
    - Backoffice → Core 17.2.1 and Persistence 17.2.1
    - Persistence → Core 17.2.1
    - Web → Core 17.2.1
  - Every `Umbraco.Cms.*` dependency is `[17.6.2, 18.0.0)`.
  - `umbraco-marketplace` is on `UBookIt` only, and its description says "for Umbraco 17 …
    Requires Umbraco 17 and SQL Server."
- [x] 6.4 SourceLink: the four `.snupkg`/`sourcelink.json` resolve to the tagged merge commit on
  the public repository.
  *Done:* the `sourcelink.json` of all four assemblies maps to
  `raw.githubusercontent.com/Chris-N2/UBookIt/6f0da775…/*`. A sample file
  (`src/UBookIt.Core/UBookIt.Core.csproj`) fetched through it returned 200.
- [x] 6.5 Read the README **out of the `.nupkg`**: no relative links, every link and image pinned
  to `17.2.1`, and "uBookIt is at `17.2.1`". Fetch every link and image address in it. Verify each
  returns 200, retrying a 503 before recording anything.

  *Done:*
  - The README was extracted from all five packages, and there is one distinct hash.
  - 0 relative links. Every repository address is pinned to `17.2.1`, and it says "uBookIt is
    at `17.2.1`".
  - 14 unique addresses: 9 doc links, 4 images and the publisher site. The file contains exactly
    14 URLs, and there are no HTML or reference-style links.
  - After the tag push, **all 14 returned 200 on the first attempt**.
  - The one fragment, `#accessibility-what-we-hold-and-what-becomes-yours`, matches GitHub's slug
    of `### Accessibility: what we hold, and what becomes yours` at `17.2.1:docs/booking-page.md:294`.
- [x] 6.6 **If 6.2–6.5 find a defect that needs a commit, nothing is pushed to nuget.org.**
  Recovery:
  1. Delete the tag locally and on origin (`git tag -d 17.2.1`,
     `git push origin :refs/tags/17.2.1`). That is safe while no package references it.
  2. Fix through a PR.
  3. Re-tag the new merge commit once its own push run is green.
  4. Redo 6.1–6.5 in full.

  Verify: either nothing was found, or the recovery happened and is recorded here. (QA round 1.)

  *Not needed:* 6.2–6.5 found nothing.
## 7. Publish

- [x] 7.1 Chris pushes the five packages with the API key.
  *Done:* Chris pushed the five `.nupkg` (with their `.snupkg`) on 2026-09-25.
- [x] 7.2 Confirm each package on `api.nuget.org/v3-flatcontainer/<id>/index.json`, **per
  package**.
  *Done, per package:* `ubookit`, `ubookit.core`, `ubookit.persistence`, `ubookit.web` and
  `ubookit.backoffice` all list `17.2.1` on the flat container. Indexing took ~10 min, polled every
  2 min: 0/5 until 13:53, 1/5 at 13:55, 4/5 at 13:57, 5/5 at 13:59.
- [x] 7.3 Verify the live package page renders its readme, images and links.

  *Done:* `nuget.org/packages/UBookIt/17.2.1` returns 200. Its rendered readme references 4
  images and 9 doc links, all pinned to `17.2.1`, with 0 `17.2.0` references and "uBookIt is
  at `17.2.1`". The live nuspec carries `umbraco-marketplace` and "for Umbraco 17".
## 8. Close out, in this order

- [x] 8.1 Stamp the entry's date once the packages are live.
  *Done:* the heading is now `## 17.2.1 — 2026-09-25`.
- [x] 8.2 Sibling sweep: sentences this release falsifies, including `docs/publishing.md`'s "until
  17.2.1/18.1.1" wording (true for this line once live; the 18 half becomes true with `18.1.1`).
  Fix what is falsified, and record the findings here. Record here what `release-18-1-1` must
  carry, including the same `6 → 8` registration.
  *Done:*
  - A sweep of `docs/`, `README.md`, `openspec/specs/**` and `CLAUDE.md` for `17.2.0` leaves only
    the history anchor `publishing.md:282`.
  - A sweep for `linux|site-relative|PrivacyPolicyUrl|umbraco-marketplace|(LTS)`: every hit is
    consistent with what shipped (`docs/backoffice.md:293`, the `privacy-notice` requirement
    and scenarios, the `packaging` Marketplace requirements, and `publishing.md`).
  - `publishing.md:83` and `:397` ("until `17.2.1`/`18.1.1` … on the meta-package alone") are
    true for this line now it is live. The 18 half becomes true when `18.1.1` publishes, and
    `release-18-1-1` verifies it. **Left as is, deliberately.**
  - Nothing falsified.

  **What `release-18-1-1` must carry:**
  - the `packaging/spec.md` nuget.org registration `6 → 8`. It is made **by hand**, because
    dev/v18's `VersionTruthTests` entry differs from main's around that line;
  - the bump `18.1.0 → 18.1.1` in the README (17 literals), runbook lines 14, 266 and 292–294
    (line 401 is history), and `<Version>`;
  - its own entry. The privacy fix is not in `18.1.0` (`9cd14ef` is not an ancestor), and on
    that line the description change **corrects a shipped defect**: `18.0.0`/`18.1.0` said
    "for Umbraco 17";
  - this release's QA lessons:
    - no category sentence for the refusals;
    - scope the Linux sentence to a site-relative link;
    - gate the pack on the merge commit's own run;
    - check sibling dependency versions;
    - use the 6.6 recovery.
- [ ] 8.3 Commit on `main` (D3), run the unit suite, and have Chris push. Verify: `main`'s CI run is
  green at every step.
- [ ] 8.4 Archive **last**, so that no box it records is still open. **Run the unit suite locally
  before the push** (D3), and verify it is green. The archive push's own CI run is checked and
  reported in the session, because the record is frozen by then.
