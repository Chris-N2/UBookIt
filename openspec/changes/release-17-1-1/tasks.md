## 1. Bump the version

Work from the table in `docs/publishing.md` step 3, not from this list — that table is the
authority, and this project has recorded three wrong counts about it already.

- [x] 1.1 `Directory.Build.props` — `<Version>` 17.1.0 → 17.1.1. Verify by running
      `VersionTruthTests` and reading which documents it names: the failure message is the rest
      of this section's worklist.
- [x] 1.2 `README.md` — the sentence naming the version uBookIt is at.
- [x] 1.3 `README.md` — **all four** screenshot refs. Verify
      `The_readme_images_render_and_show_what_their_release_shipped` passes, and that it fails if
      one is left at `17.1.0` — that arm is the reason the refs cannot be silently forgotten.
- [x] 1.4 `docs/publishing.md` — the *"uBookIt is at"* sentence under *What nuget.org will not
      let you undo*. **Not** the `## Status` line, which is an anchor.
- [x] 1.5 `docs/publishing.md` — the four version literals in *Tag the release*. **No guard
      reads these**; check them by eye and say so in the record rather than implying a test
      covered them.
- [x] 1.6 Confirm no *history* anchor moved: the first-release sentence and the
      declared-stable-from sentence must still say `17.0.0`. `The_documented_anchors_do_not_move`
      fails if they were dragged along by a find-and-replace.

## 2. The changelog entry

- [x] 2.1 Add a `17.1.1` entry to `CHANGELOG.md`, **heading undated**, leading with what a site
      must do — nothing — before what changed. Verify `ChangelogTests` passes with the entry
      undated, which is the state it must be in at publication.
- [x] 2.2 State the consumer-visible content plainly: the package page no longer denies three
      shipped features, and the readme now carries screenshots. No API, schema or behavioural
      change. Verify each sentence against the archived change rather than from memory.

## 3. Verify, then merge and push

- [x] 3.1 Build the client first, then each test project sequentially in Release with the
      TestSite stopped. Record the counts.
- [x] 3.2 Clean Release build, **0 warnings**. Never accept a `--no-build` run as evidence.
- [x] 3.3 `openspec validate --all --strict`.
- [ ] 3.4 **Merge this change to `main` and push it**, then confirm
      `git branch -r --contains HEAD` lists `origin/main`. SourceLink embeds the packed commit,
      so packing from anything not on the public repository produces source links that 404 for
      every consumer, permanently — and `docs/publishing.md` says to pack from the merge commit
      on `main` after it is pushed.
      **This step used to be §7.5, at the end.** That made the procedure self-contradictory: the
      tag and the pack were gated on `origin/main` containing HEAD while the merge had not
      happened, so the releaser either blocked here or stepped around the one check that
      prevents a permanent SourceLink 404. QA rated it CRITICAL and it is the correct rating.

**§1–3 results.** The bump is done and the guards named the worklist exactly as 1.1 relies on:
setting `<Version>` alone failed `The_readme_images_render_and_show_what_their_release_shipped`
with all four refs listed, and `ChangelogTests` with three separate failures (no entry, entry is
only a heading, no obligation section). Every one of them was a document to edit.

**1.5 — checked by eye, and there was nothing to do.** All four version literals in *Tag the
release* already read `17.1.1`, because they were written forward-looking during
`docs-truth-and-screenshots` (QA flagged the mismatch then and judged it correct for a runbook).
**That is luck this release and a trap the next one**: at `17.1.2` they will be stale, no guard
will say so, and the person bumping will have seen them pass untouched once. Recorded rather
than enjoyed.

**1.6 — anchors held.** `docs/publishing.md:26` still says the first release is `17.0.0`, and the
declared-stable-from sentence is unmoved. Nothing was dragged along by the edits.

| | |
|---|---|
| `UBookIt.Tests` | **1809** passed |
| `UBookIt.Tests.Integration` | **167** passed |
| `UBookIt.Tests.Rendering` | **1168** passed |
| Client (vitest) | **290** passed |
| Clean Release build | **0 warnings, 0 errors** |
| `openspec validate --all --strict` | **23/23** |

## 4. Tag before packing (design D1 — the step that is new this release)

- [ ] 4.1 `git tag 17.1.1` on the commit being packed, and `git push origin 17.1.1`.
- [ ] 4.2 **Verify over the network**, because nothing else can:
      `curl -sI https://raw.githubusercontent.com/Chris-N2/UBookIt/17.1.1/docs/images/booking-flow.png`
      must answer `200`. Check all four images, not one.
- [ ] 4.3 Confirm the repository README's images now render on `github.com/Chris-N2/UBookIt`.
      **This works only because §3.4 merged first**: before the merge, `main`'s readme still
      points at `17.1.0`, a tag that does not exist and cannot be created, so the `17.1.1` tag
      alone would change nothing on the landing page. The window closes at merge **plus** tag,
      and an earlier draft of this task claimed the tag alone did it.

## 5. Pack and verify what was produced

- [ ] 5.1 `dotnet clean UBookIt.slnx -c Release`, then a clean rebuild, then pack. The clean is
      not housekeeping: `dotnet pack` is incremental and `--no-incremental` does not govern it,
      so a stale `.nupkg` survives and the wildcard pushes it.
- [ ] 5.2 Verify the artifacts: **5 `.nupkg` + 4 `.snupkg`, all `17.1.1`**; repository commit
      equals HEAD; icon and readme **declared and present**; SourceLink SHA equals HEAD.
- [ ] 5.2a **`git rev-parse 17.1.1^{}` equals `git rev-parse HEAD`** — the tag points at the
      commit actually being packed. Checking the repository commit and the SourceLink SHA
      against HEAD does **not** establish this: the tag is created a step earlier, and any
      commit in between decouples them silently. The frozen readme's images would then address
      a tag that is not the source they came from, and nothing — no test, not even the
      post-publish page check — could detect it.
- [ ] 5.3 Verify the **packed** readme in **all five packages**, not one: zero relative links,
      and four image URLs pinned to `17.1.1`. Each package carries its own copy, which is why
      `docs/publishing.md` says to check all five. Inspect the packed file, not the source —
      that distinction is why `17.0.1` exists.

## 6. Publish

- [ ] 6.1 Push all five packages. Read the key-ownership note first if the key is new: a `403`
      names the key but usually means its **owner** cannot publish.
- [ ] 6.2 Confirm indexing on the flat-container endpoint per package —
      `api.nuget.org/v3-flatcontainer/<id>/index.json`. The website lags and *unlisted during
      validation is a state, not a flag*.
- [ ] 6.3 **Open the package page and look at the screenshots.** Every image must render. This is
      the only check that sees what a consumer sees: no test reaches nuget.org, and a rejected
      image is reported to the owner alone.

## 7. Only after the feed confirms

- [ ] 7.1 Stamp the release date on the `CHANGELOG.md` heading, and commit.
- [ ] 7.2 Sync specs — expect **none**, since this change sets `skip_specs: true`. Verify that
      `openspec/specs/` is untouched rather than assuming it.
- [ ] 7.3 Archive the change. **In this order**: `ChangelogTests` reads the archive, so archiving
      before the date is stamped turns the suite red.
- [ ] 7.4 Re-run the full suite after archiving. The last two releases both had a guard fire at
      this point, and that firing is the deliverable rather than an obstacle.
- [ ] 7.5 Push the post-release commits (date stamp, archive) to `main`, so `openspec/specs/`
      on `main` is the baseline the next change diffs against. **The merge itself happened at
      §3.4**; what remains here is pushing what the release added afterwards.

## 8. Record

- [ ] 8.1 Update the deferred-obligations and state notes: what `17.1.1` contains, that the tag
      step was exercised for the first time, and whether the runbook needed anything it did not
      already say.
- [ ] 8.2 Record whether the unguarded *Tag the release* literals were correct when checked by
      eye — a blind spot that is measured each release is worth more than one that is merely
      declared.

## 9. QA round 1 — REJECT (2 CRITICAL, 2 MAJOR, 3 MINOR, 2 NIT)

QA confirmed every measurement independently — counts, clean build, `--strict`, the anchors, the
absence of any stale tag, the DevExpress gate, the sibling sweep, and that no code changed between
`a875088` and `e5fe25f` outside the TestSite and the test projects. It also attacked
`skip_specs: true` and accepted it, naming the nine `packaging` requirements this release relies
on. Both CRITICALs were in artifacts, not in the bump.

- [x] 9.1 **[CRITICAL-1] The merge was scheduled AFTER the pack, and the gate could not pass.**
      §3.4 required `origin/main` to contain HEAD; §7.5 merged as the last step. Executed
      literally the releaser blocks, or steps around the one check preventing a permanent
      SourceLink 404. `docs/publishing.md` says to pack from the merge commit on `main` after it
      is pushed, and `17.1.0` did exactly that — `design.md`'s ordering bullet and D4 had both
      omitted the merge, so nothing corrected it. Merge is now §3.4, D4 is *"Merge first, archive
      last"*, and the ordering bullet states the full sequence with the reason each end is fixed.
- [x] 9.2 **[CRITICAL-2] The changelog entry — a document frozen at publication — was false.**
      It said the packed readme denied three features including finding a person by email
      address. **Verified against `git show a875088:README.md`: the packed readme denied TWO**
      (on-behalf placement, reference lookup) and **affirmed** the email search in the same
      bullet that denied reference search. The third denial lived in `docs/backoffice.md`, which
      is not packed, and had been false since `0.3.0` — not `17.1.0`.
      **Where the error came from is the reusable part.** The archived change's body was
      accurate: two denials in the README, a third in `docs/backoffice.md`. Its opening sentence
      compressed that to "cannot do three things", and this release's proposal **re-expanded the
      compression into a precise claim about the packed readme specifically** — inventing a
      detail the loose original never asserted. Task 2.2 said "verify against the archived change
      rather than from memory" and was ticked; it had been verified against this proposal, which
      carried the same error. Both corrected.
- [x] 9.3 **[MAJOR-1] 4.3's premise was false.** Before the merge, `main`'s readme points at
      `17.1.0` — a tag that does not exist and cannot be created, since the images post-date that
      release. The `17.1.1` tag alone changes nothing on the landing page. Falls out of 9.1's
      reordering; the task and the proposal now say merge **plus** tag.
- [x] 9.4 **[MAJOR-2] Nothing checked that the tag points at the packed commit.** Repository
      commit and SourceLink SHA were both compared to HEAD, which does not establish it — the tag
      is created a step earlier and any commit between decouples them, with no test and not even
      the post-publish page check able to see it. New §5.2a.
- [x] 9.5 **[MINOR-1] A fourth wrong count of the bump list** — "five places across four files",
      in both the proposal and D2, when the authority table holds six across four. Corrected, and
      D2 now records that the miscount happened *inside the decision that says read the table, not
      memory*.
- [x] 9.6 **[MINOR-2]** 5.3 checked "the packed readme" singular; each of the five packages
      carries its own copy.
- [x] 9.7 **[MINOR-3]** The changelog now says `docs/backoffice.md` carried three denials rather
      than "the same" two, and credits the `docs/mvp.md` corrections it had omitted.
- [x] 9.8 **[NIT-1]** The archived design's "three broken images" is four. Noted here rather than
      edited: `openspec/changes/archive/**` is history and is never edited to satisfy a later
      reading.
- **[NIT-2]** Left. `CHANGELOG.md` is outside the retired-claim sweep's file set and the new entry
      paraphrases retired claims in the past tense. Green today, and widening the sweep to cover a
      file whose entries must never be edited needs its own thought.
