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

## 3. Pre-push verification

- [x] 3.1 Build the client first, then each test project sequentially in Release with the
      TestSite stopped. Record the counts.
- [x] 3.2 Clean Release build, **0 warnings**. Never accept a `--no-build` run as evidence.
- [x] 3.3 `openspec validate --all --strict`.
- [ ] 3.4 Confirm HEAD is pushed and `git branch -r --contains HEAD` lists `origin/main` —
      SourceLink embeds this commit, and packing from an unpushed commit produces source links
      that 404 for every consumer, permanently.

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
- [ ] 4.3 Confirm the repository README's images now render on `github.com/Chris-N2/UBookIt` —
      this is the moment the accepted broken-image window closes.

## 5. Pack and verify what was produced

- [ ] 5.1 `dotnet clean UBookIt.slnx -c Release`, then a clean rebuild, then pack. The clean is
      not housekeeping: `dotnet pack` is incremental and `--no-incremental` does not govern it,
      so a stale `.nupkg` survives and the wildcard pushes it.
- [ ] 5.2 Verify the artifacts: **5 `.nupkg` + 4 `.snupkg`, all `17.1.1`**; repository commit
      equals HEAD; icon and readme **declared and present**; SourceLink SHA equals HEAD.
- [ ] 5.3 Verify the **packed** readme: zero relative links, and **four image URLs pinned to
      `17.1.1`**. Inspect the packed file, not the source — that distinction is why `17.0.1`
      exists.

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
- [ ] 7.5 Merge to `main` and push, so `openspec/specs/` on `main` is the baseline the next
      change diffs against.

## 8. Record

- [ ] 8.1 Update the deferred-obligations and state notes: what `17.1.1` contains, that the tag
      step was exercised for the first time, and whether the runbook needed anything it did not
      already say.
- [ ] 8.2 Record whether the unguarded *Tag the release* literals were correct when checked by
      eye — a blind spot that is measured each release is worth more than one that is merely
      declared.
