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
- [x] 2.2 In that entry, name every contract change by interface and member, **derived by diffing
      the compiled interface surface against `f831986` (the 17.0.1 release commit)** — not
      transcribed from the archived proposals. QA round 1 rejected the transcribed version: it named
      four interfaces where there are five, missing `IBookingManagementStore.FindByReferenceAsync`
      entirely, because `find-booking`'s proposal contains no BREAKING line for a guard to find.
      The compiled surface is the authority; prose about it is not. State that **none of the members
      has a default implementation**, so a site implementing any of those ports will not compile
      until it adds them — and that `IBookingObserver.BookingPlacedOnBehalfAsync`, which *is*
      defaulted, does not need adding.
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
- [ ] 7.2 Stamp the release date on `17.1.0`'s heading at publication — `## 17.1.0 — YYYY-MM-DD`.
      The heading ships undated deliberately (QA round 1): a date written before the push is a
      claim nuget.org can contradict if publication slips, and the file's own never-edit rule then
      makes the correction awkward. The changelog says the stamp completes an entry rather than
      revising it, so this is the one edit a released entry may still receive.
- [ ] 7.3 Confirm the tree is committed and pushed before he packs — SourceLink embeds the commit
      SHA, and packing an uncommitted tree names the wrong one. `git branch -r --contains HEAD` must
      list `origin/main`.
- [ ] 7.4 Record the deferred items so they are decisions rather than omissions: release tags,
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

## 9. QA round 1 — REJECT (1 CRITICAL, 2 MAJOR, 4 MINOR, 2 NIT)

**The CRITICAL was the defect this change exists to prevent, shipped by this change.** The 17.1.0
entry named four interfaces; there are five, and `IBookingManagementStore.FindByReferenceAsync` was
absent entirely.

- [x] 9.1 **[CRITICAL]** Rebuild the breaking table from the compiled interface surface diffed
      against `f831986`, not from the archived proposals. Five interfaces, ten members:
      `IBookingObserver.BookingMovedAsync`; `IBookingStore.MoveAsync`;
      `IBookingManagementStore.FindByReferenceAsync`; `IServiceBookingService.MoveAsync` + **two**
      `PlaceOnBehalfAsync` overloads; `IBookingService.MoveAsync`, `PlaceOnBehalfAsync`,
      `PlaceForServiceOnBehalfAsync`, `CancelAsVisitorAsync`. Also state that
      `IBookingObserver.BookingPlacedOnBehalfAsync` **is** defaulted and needs no action, and that
      `ICancellationSecretStore` is new and therefore breaks nobody. Method recorded as design D7.
- [x] 9.2 **Verified independently of QA's count.** QA's prose said nine members; its own table rows
      sum to ten, and the diff confirms ten signatures (nine distinct names — `PlaceOnBehalfAsync`
      appears twice on `IServiceBookingService`, which that file's own remarks call out as "two
      members, not one"). Swept **every** `public interface` under `src/`, not only `UBookIt.Core`:
      no port outside Core is affected.
- [x] 9.3 **[MAJOR]** Correct "every feature is off until you turn it on" in `CHANGELOG.md`,
      `proposal.md` and `design.md`. True of one feature in five. Move and book-on-behalf are live
      on upgrade for groups holding `BookingsManage`, booking lookup for `BookingsRead` — verbs the
      seed granted before this release. On-behalf additionally requires Umbraco's *Sensitive data*
      (`BookingsController.cs:474-475`), which QA did not note. The settings screen's `Settings`
      verb is deliberately outside the seeded set, so that one really is inert.
- [x] 9.4 **[MAJOR]** Add `CHANGELOG.md` to `VersionTruthTests.LiveDocuments()` — every
      documentation guard in the repository was blind to it. Register its three `nuget.org`
      mentions, each **verified against the live feed** rather than reasoned about:
      `GET api.nuget.org/v3-flatcontainer/{ubookit,ubookit.core}/index.json` both return exactly
      `[17.0.0, 17.0.1]`.
- [x] 9.5 **[MINOR]** Derive `Released_versions_keep_their_entries`' population from the archived
      release changes instead of hardcoding `{17.0.0, 17.0.1}`. Proved derived by adding a
      `release-17-2-0` directory to the archive and watching the guard demand a 17.2.0 entry;
      directory removed, `git status` clean.
- [x] 9.6 **[MINOR]** Give `17.0.0` a "What you have to do" section — the file's own opening rule
      requires one of every entry, and the ordering guard only inspects the declared version.
- [x] 9.7 **[MINOR]** `proposal.md` said "three new settings". One. The other two keys QA and I both
      initially read as new pre-date `17.0.1`.
- [x] 9.8 **[MINOR]** Ship `17.1.0`'s heading **undated**; the date is stamped at publication
      (task 7.2). The changelog states that a stamp completes an entry rather than revising it.
- [x] 9.9 **[NIT]** The round-1 handover reported a mutant that proved nothing — `17.1.10` against
      a check for `17.1.0` is rejected by plain `Contains` too. The load-bearing case is `17.1.01`,
      which was re-run and does fire. Reported accurately here.
- [x] 9.10 **[NIT]** `CHANGELOG.md`'s two relative links are safe: only `README.md` is packed
      (`Directory.Build.props:103-104`), so nothing resolves them against nuget.org. Checked rather
      than assumed, because that is precisely the defect that cost `17.0.1`.

### Found while fixing, not by QA

- [x] 9.11 **My own registry note overstated the guard.** I wrote that the accepted-mention count is
      "exact". Measured: a fourth mention fails, and all three disappearing fails, but **three
      dropping to two passes** — the count bounds the claims, it does not pin them. Note corrected
      to say what the mechanism does. Same shape as the CRITICAL one level down: a sentence
      describing a property the code did not have.

## 10. QA round 2 — REJECT (1 MAJOR, 4 MINOR, 1 NIT)

Round 1's CRITICAL and both MAJORs were verified closed by the reviewer's own independent sweep —
including that **no interface or member was REMOVED and no signature CHANGED** between `f831986`
and HEAD, so nothing harder is hiding behind the additions. The round's own finding was that a
round-1 *fix* had introduced a new false statement, which is this project's standing pattern.

- [x] 10.1 **[MAJOR] My round-1 "correction" was itself wrong: the accepted-mention count IS exact.**
      Restored, with the measurement in each direction (four → unclassified; two → unconsumed
      allowance; zero → three unconsumed).

      **The cause was NOT what either of us first said.** QA attributed it to a stale assembly
      (`--no-build`); I reproduced the false pass on a *fresh* build, so that was not it either.
      The real cause: my mutation string spanned a line wrap in `CHANGELOG.md` — the file reads
      `"...on nuget.org and
cannot be changed"` — so `str.replace` matched nothing, returned the
      text unchanged, and I read the resulting green as evidence about the guard. **A mutation that
      silently no-ops does not report a passing guard; it reports nothing at all.** Asserting the
      file changed is now done in every mutant in this change, and line wrapping has defeated an
      instrument in this repository before (㉛).
- [x] 10.2 **[MINOR] Permission names.** The changelog and proposal used `Manage Bookings` /
      `Read Bookings` / `Settings`; the backoffice shows **Act on bookings**, **See bookings** and
      **Change site settings** (`Client/src/localization/en-us.ts:551,554,560`). The table exists so
      a reader goes and reviews those groups — with the wrong vocabulary they would search for
      permissions that do not exist. Gating logic was right; only the words were wrong.
- [x] 10.3 **[MINOR] `ISettingsStore` is also new**, not just `ICancellationSecretStore`. The
      "a new interface breaks nothing" sentence now names both. An enumeration whose job is
      completeness, short by one — the CRITICAL's shape at small scale.
- [x] 10.4 **[MINOR] The date stamp is now guarded**, using the heading capture that was taken and
      never read. `Every_released_version_is_dated` requires a date on every version in
      `ReleasedVersions()`; the declared version may ship undated because it is not yet archived.
      This also **bounds the exception** the changelog grants its own never-edit rule: a released
      entry may receive its date and nothing else. Mutation-checked both ways — removing `17.0.1`'s
      date fails, and `17.1.0` undated still passes.
- [x] 10.5 **[NIT] Counts.** Ten signatures, **six** distinct names (`MoveAsync` on three
      interfaces, `PlaceOnBehalfAsync` on two), five interfaces. Both QA's round-1 "nine members"
      and my commit message's "nine names" were wrong; the changelog's per-interface table was
      never wrong, which is why listing per interface is the right shape.

### 10.6 [MINOR] `FindByReferenceAsync` is not declared in any spec — DEFERRED, with the reason

**The gap is real.** CLAUDE.md requires a breaking change to be called out explicitly in its spec.
Nine of the ten breaks are; `IBookingManagementStore.FindByReferenceAsync` is declared only in a
source comment in `Stores.cs`, and `find-booking`'s proposal carries no `BREAKING` line at all.
`openspec/specs/booking-management/spec.md` has no callout for it.

**Not closed here, deliberately.** Closing it means a `## MODIFIED Requirements` entry for *A
booking can be found by its reference* — a ~60-line body with **8 scenarios** — replaced wholesale,
which is this project's most expensive failure mode and the one thing that must not be done quickly
or unsupervised. The gap predates this change: it was created when `find-booking` merged, not by
the release.

**What the release does instead:** the consumer-facing callout, which is the protection that
actually matters to a site, now exists in `CHANGELOG.md` and is correct. The proposal no longer
claims all ten were declared — it states that nine were and names the one that was not.

**Owed to `booking-management`**, recorded in the deferred obligations. Whoever next touches that
capability should add the declaration with a proper guarantee diff.
