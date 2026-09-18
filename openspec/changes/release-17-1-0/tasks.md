## 1. The version

- [x] 1.1 Set `<Version>` to `17.1.0` in `Directory.Build.props`. All five packages inherit it;
      there is no per-project version to follow.
- [x] 1.2 Update `README.md`'s API-promise callout to state `17.1.0`.
- [x] 1.3 Update `docs/publishing.md`'s version-reuse warning to state `17.1.0`.
- [x] 1.4 Leave the two history anchors exactly as they are — `"the first release is 17.0.0"` and
      `"17.0.1 exists because of it"`. Confirm by grep that `17.0.1` still appears in
      `docs/publishing.md` and nowhere else outside `openspec/changes/archive/`.
- [x] 1.5 Run `VersionTruthTests` alone and confirm all 11 pass. The bump is designed to fail this
      class until 1.2 and 1.3 are done, so a green run here is the evidence those edits were the
      right ones — not a formality.

## 2. The changelog

- [x] 2.1 Write `CHANGELOG.md` with an entry for `17.1.0` that **opens with what upgrading asks of
      the reader** (design D2), then what they gain.
- [x] 2.2 In that entry, name all four contract changes by interface and member, transcribed from
      the archived proposals rather than recalled: `IBookingObserver` gains a moved member;
      `IBookingStore` gains a move write; `IServiceBookingService` gains a move (all three from
      `2026-09-16-move-booking`); `IBookingService` gains `CancelAsVisitorAsync`
      (`2026-09-18-self-service-cancellation`). State that **none has a default implementation**, so
      a site implementing any of them will not compile until it adds the new members.
- [x] 2.3 State the non-breaking upgrade path in the same entry: every new feature is behind a flag
      that defaults off, and self-service cancellation additionally requires booker emails to be on,
      so a site that implements no ports upgrades without action.
- [x] 2.4 Summarise the five features at a level a consumer cares about — settings screen, operator
      booking moves, booking on behalf of a caller, booking lookup, and self-service cancellation by
      emailed link. Name the settings keys a site must set to turn the new ones on.
- [x] 2.5 Add short entries for `17.0.0` (first release) and `17.0.1` (nine relative README links
      that resolved against nuget.org rather than the repository; metadata is frozen per version, so
      a new version was the only fix). Do not reconstruct detail that was never written at the time.
- [x] 2.6 Record in the file itself that entries for released versions are history and are never
      edited forward — the same rule as the version anchors, stated where an editor will meet it.

## 3. The guard

- [x] 3.1 Add a test asserting `CHANGELOG.md` has an entry for the version `Directory.Build.props`
      declares, and that the entry is **not empty**. Fail with the version it could not find, in the
      shape `VersionTruthTests` already uses.
- [x] 3.2 Do **not** assert the entry is complete or true (design D3). State that boundary in the
      test's remarks: the guard proves presence, a human proves honesty. Naming the limit is what
      stops the next reader assuming coverage that is not there.
- [x] 3.3 Mutation-check both halves: (a) bump the declared version without adding an entry and
      confirm it fails naming that version; (b) empty the `17.1.0` entry's body, leaving the
      heading, and confirm it still fails. A heading is not a callout, and a guard that accepts one
      is the shape this project has been caught by repeatedly.
- [x] 3.4 Confirm the guard reads the heading by **identity**, not `contains`: an entry for
      `17.1.10` must not satisfy a check for `17.1.1`. Loose substring matching has produced a false
      pass in this repository before.
- [x] 3.5 Verify the guard can fire at all — that the file it reads is the file the repository ships
      and that the test executes. `RepoFiles` is the existing route; do not introduce a second one.

## 4. The pointer

- [x] 4.1 Add a link to `CHANGELOG.md` from `README.md`'s API-promise callout, so the sentence
      promising that breaks are called out explicitly sits beside the place they are.
- [x] 4.2 Use an **absolute** URL, `https://github.com/Chris-N2/UBookIt/blob/main/CHANGELOG.md`. A
      relative link resolves against nuget.org from the packed readme — the defect that cost
      `17.0.1` — and the existing link guard checks the form.
- [x] 4.3 Run the readme link guards and confirm the new link is seen by them rather than being an
      unwatched exception.

## 5. Specs

- [x] 5.1 Confirm the `packaging` delta adds one requirement and modifies none, so no guarantee can
      be dropped by wholesale replacement. If a modification becomes necessary, diff the guarantees
      of the requirement being replaced before touching it.
- [x] 5.2 `openspec validate --all --strict`.

## 6. Verification

- [x] 6.1 Clean `bin`/`obj` of the packable projects and delete every stale `.nupkg`/`.snupkg`.
      `dotnet pack` is incremental and a stale artifact survives a rebuild that looks clean.
- [x] 6.2 Client build first (`npm run build`), then a full clean Release build, then the suites as
      separate `dotnet test` steps. Never accept a `--no-build` run as evidence — it can execute
      stale assemblies and report green, which has happened here.
- [x] 6.3 Run the outward sibling sweep over `openspec/specs/` for sentences this change falsifies.
      It has found something on six consecutive changes; if it finds nothing, record why per
      candidate rather than leaving the null undocumented.
- [ ] 6.4 Hand the change to a QA subagent. Report build state, test counts and what was verified,
      and ask it to verify rather than trust each claim. Tell it explicitly that each round's fixes
      are new code: on this project a fix has caused the next round's defect repeatedly.

## 7. Handover

- [ ] 7.1 Do **not** pack or push. Publishing is a non-goal; the runbook is Chris's.
- [ ] 7.2 Confirm the tree is committed and pushed before he packs — SourceLink embeds the commit
      SHA, and packing an uncommitted tree names the wrong one. `git branch -r --contains HEAD` must
      list `origin/main`.
- [ ] 7.3 Record the deferred items so they are decisions rather than omissions: release tags,
      `<PackageReleaseNotes>` linking the changelog, and CI.

## 8. The outward sibling sweep — what it found, and why nothing

Run over all 23 capability specs with emphasis stripped and lines unwrapped, probing for document
inventories, readme-totality claims, breaking-change callouts, repository-layout claims, link rules
and version claims. **Nothing falsified.** The streak of six consecutive changes breaks here, so the
null is recorded per candidate rather than assumed:

- **`packaging` — "every link that names a file in this repository SHALL name a file that exists".**
  Touched, and satisfied: `CHANGELOG.md` exists, and the link is absolute. Verified the guard
  actually sees the new link by making it relative and watching
  `The_readme_links_resolve_from_anywhere` fail — it is inside the population, not an exception.
- **`packaging` — "The version a reader is told is the version the package carries".** Not
  modified. The bump moves the two current-version claims and leaves the two history anchors, which
  is exactly what the requirement demands; all 11 of its guards pass.
- **`theming` — "a change to either is thereafter a breaking change to be called out as one".**
  Not falsified but newly *dischargeable*: until this change there was nowhere a callout could land.
  Strengthened rather than contradicted, so no delta.
- **`delivery-api` — "the reversal is a breaking change for existing consumers of the API, accepted
  and documented as such".** Shipped inside `17.0.0`, the first release, so it broke nothing that
  existed. The changelog's `17.0.0` entry correctly claims no obligation.
- **Link rules in `booking-emails`, `email-templates`, `privacy-notice`, `persistence`,
  `self-service-cancellation`, `default-frontend`.** All matched the probe on the word "link" and
  all concern cancellation links, privacy-policy links or in-page anchors. None is about
  documentation links. Noise, not candidates.
- **Version-shaped sentences in `bookings`, `booking-management`, `permissions`.** All use "version"
  to mean a prior release's behaviour or an installation upgrading, none states a version number.
