## 1. Capture the evidence before deleting anything

The retired sentences are the fixture D2's control needs. Once they are deleted from the
documents, the wording they had is only in git history — and a needle written from memory is how
the drifted one got there.

- [x] 1.1 Copy the seven false sentences verbatim out of the working tree (`README.md:114,121`;
      `docs/backoffice.md:327,519-520,524`; `docs/mvp.md:93-94,95-97`) into a fixture the test
      project reads, and verify the fixture's text matches `git show HEAD:<path>` for each file
      rather than a retyped copy.
- [x] 1.2 Re-run the measurement over every shipped markdown file for negative claims about all
      five `17.1.0` features — not only the three in the proposal — and verify the fixture holds
      every hit, so the population is measured rather than inherited from `design.md`'s table.

**§1 findings.** The measurement confirms `design.md`'s table exactly — seven false sentences in
three files, and **no** additional denial of any of the five `17.1.0` features anywhere in
`README.md` or `docs/`. Two corrections to `design.md` came out of it, both recorded in
`RetiredClaims`:

- The drifted needle did not drift. `README.md` carried
  `"there is no search by name, email or reference"` at `ca18ccd`; it was narrowed to
  `"There is no search by name or reference"` at `ae55773` when the email search shipped — a
  correct edit — and **the needle was written afterwards, against the pre-edit wording**. It has
  matched nothing since the day it was written, which is a sharper fault than drift and the exact
  one D2's control catches.
- `"does not tell the person who booked"` was never in any shipped document. It was born as a
  needle in `cancel-and-notify`'s task list. Kept, with its evidence naming what it actually is.

All eleven pre-existing needles were recovered from git rather than retyped, so every one can be
put under D2's control rather than only the new ones.

## 2. Make the sweep's needles falsifiable (spec: *A needle that matches nothing is not mistaken for a passing check*)

- [x] 2.1 Add a test asserting every needle passed to the falsified-claims sweep matches its
      fixture sentence, and verify it **fails** when a needle is altered by one word — the
      mutation must be shown to apply, per [[verify-the-instrument-mutated-the-file]].
- [x] 2.2 Correct the drifted 0.3.0 needle: keep `"there is no search by name, email or reference"`
      and add the literal `README.md` actually carries. Verify the new needle is covered by 2.1's
      control and that removing it turns 2.1 red.

**§2 result.** `RetiredClaims` holds all 16 needles with recovered evidence;
`Every_needle_matches_the_text_it_was_written_against` passes for every one, and
`Every_needle_says_where_its_evidence_came_from` refuses an entry that cannot be re-derived.
Mutation verified to apply (`grep`, not `git diff` — the file is untracked, so a diff proves
nothing about it) and the control killed it: altering one needle by one word failed with
*"The documentation no longer says: \"There is definitely no reschedule\""*. Reverted and
re-verified.

2.2 landed differently from how it was written, because §1's evidence corrected the premise: the
old needle is **kept as-is** — it legitimately guards the pre-`ae55773` wording, which is a real
sentence a document carried — and the README's actual literal is added alongside it. The task
said "correct the drifted needle"; the needle was not drifted, it was written against a sentence
already gone, so correcting it would have destroyed a valid guard.

## 3. Retire the false claims (spec: *The documentation a consumer reads does not deny what the package does*)

Each sentence is retired in **both** halves — deleted from the document and added to the sweep —
per design D1. Verify each by deleting the correction and watching the suite fail.

- [x] 3.1 `README.md` — remove the *Taking a booking on someone's behalf* bullet and narrow the
      *finding a booking* bullet to what is still true (no search **by name**; reference lookup
      exists). Verify against `docs/backoffice.md` §368, which states what does exist.
- [x] 3.2 `docs/backoffice.md:327` — replace the denial with what the screen does, and verify it
      agrees with §395 rather than merely not contradicting it.
- [x] 3.3 `docs/backoffice.md` *What the section does not do* — re-read all four bullets, not the
      one reported. Remove *It does not place bookings* and *It does not find a person across
      bookings*; verify the two that remain are still true at HEAD.
- [x] 3.4 `docs/mvp.md` — annotate the two v1-era bullets with a `17.1.0` parenthetical in the
      form the *Amending a booking's time* bullet already uses, and verify the file no longer
      contains any needle literal.
- [x] 3.5 Add all retired sentences to the sweep, and verify by reinstating one sentence in one
      document and watching the suite name that file.

## 4. Remove the guard that pins a falsehood (spec: *A guard does not require a claim that has become false*)

- [x] 4.1 Retire `BackofficeDocumentationTests.cs:150`'s `Says(docs, "It does not place bookings")`
      and its sibling for the person-across-bookings claim, leaving the two accurate pins intact.
      Verify the file's remaining `Says` assertions each still match text at HEAD.
- [x] 4.2 Sweep the test project for any other positive pin on a sentence denying a `17.1.0`
      capability, and record the result — including "none found" — so the absence is measured
      rather than assumed. [[a-finding-enumerates-a-sample]].
- [x] 4.3 Update the remarks above the pins to say what changed and why, and verify the comment
      describes the assertions actually present — not the set they replaced.

**§3–4 results.** All seven sentences retired in both halves; 84 documentation tests green.

- **3.3 found more than the two false bullets.** The paragraph *below* the list enumerates what
  the section does, and it was stale in the same way — no placement, no lookup. Fixing the list
  and leaving its own summary wrong would have been the same defect one line further down.
- **3.5 exposed a weakness in the instrument.** The sweep fired correctly but its failure named
  only the sentence, leaving a maintainer to search twenty-odd files for it. `DoesNotSay` now
  takes an optional `source`, the sweep passes the document name, and the failure reads
  *"docs/backoffice.md still says, or says again: …"*. Verified by reinstating a sentence, twice.
- **4.2 result: one other, no third.** The person-across-bookings pin was the second, handled in
  the same list. Filtering all 181 `Says`/`SaysOnce` call sites for absence-shaped wording
  (`does not|no |not built|never|cannot|only`) surfaced nothing else `17.1.0` falsified — the
  self-service-cancellation and settings pins all describe shipped behaviour. **The limit of that
  measurement:** a pin phrased as an absence without any of those words would not have been in
  the filtered set.

## 5. Tell the README what the package now does

- [x] 5.1 Add the four unmentioned `17.1.0` features to *What it does* — on-behalf booking,
      reference lookup, self-service cancellation, the settings screen — each stating its gate
      (permission or setting). Verify each sentence against `CHANGELOG.md`'s `17.1.0` entry.
- [x] 5.2 Re-read the `17.1.0` version banner at `README.md:23`: "the public API is now a promise"
      sits beside a declared breaking addition. Verify whether it still reads honestly and correct
      it if not — nothing in the suite can see this.

## 6. Extend the packed-readme guard to images (spec: *An image in the packed readme is rendered, and shows what its release shipped*)

Written before the images exist, so the guard is proved by the first image rather than fitted to
it.

- [x] 6.1 Extend `VersionTruthTests`' packed-readme check: an image must name a host the feed
      renders, must resolve to a file present in the working tree, and its ref must equal the
      version `Directory.Build.props` declares. Verify each arm fails independently with a
      deliberately broken image.
- [x] 6.2 Verify the guard is reachable at all — that the pattern matches an image and the
      assertion executes — by adding a valid image and confirming the arm count rises.
      [[a-guard-must-be-able-to-fire]].
- [x] 6.3 Confirm the allow-listed host set used by the guard against nuget.org's published list
      at implementation time rather than trusting `design.md`'s copy, and cite where it was read.

**§5–6, §8 results.** README gains the four features and an honest version banner: "the public
API is a promise" now says what the promise IS — not that nothing changes, but that a change to a
published contract is deliberate, named before you meet it, and never in a patch. `17.1.0` added
members to five interfaces, so the old "treat the contracts as settled from here" sat badly
beside it.

The image guard is written and **currently failing for exactly the right reason** — *"README.md
carries no images"* — which is 6.2's reachability evidence in advance. 6.3: the allow-list was
read at implementation time from
`learn.microsoft.com/nuget/nuget-org/package-readme-on-nuget-org#allowed-domains-for-images-and-badges`
(consulted 2026-09-21), confirming `raw.githubusercontent.com` is allowed and plain `github.com`
is not.

§8 also forced a registered count: `Every_mention_of_the_feed_is_accounted_for` tracks
`nuget.org` mentions per document and the runbook went 17 → 21. The guard's whole purpose is to
make that a decision, so the four new ones are classified in the registry rather than waved
through — all behavioural facts about the host, none a publication claim.

## 7. Screenshots

- [x] 7.1 Start the TestSite, compose and capture the three screens from design D6, and verify no
      frame carries a real address, key or non-`localhost` host.
- [x] 7.2 Commit them under `docs/images/`, and verify they are **not** swept into any packing
      glob — inspect a produced `.nupkg` rather than reading the `.csproj`.
- [x] 7.3 Add them to `README.md` with alt text that describes the screen to somebody who cannot
      see it, and verify each alt text is read against the image rather than against the filename.
- [x] 7.4 Run the guard from §6 against the real URLs and verify it passes for the right reason —
      temporarily break one path and confirm it fails.

**§7 results.** Three images in `docs/images/`, captured from the TestSite and cropped to frame:
`booking-flow.png`, `bookings-screen.png`, `availability.png`. Four clean bookings were recorded
through the shipped *New booking* dialog for the captures — **FQ7R-M7X3, CGRR-QP4J, CKBF-XTVP,
ZZHD-MDZM** on 2026-10-05 — additive only; nothing was deleted, so ㊵'s `BJQ4-ZP5C` and the rest of
the recorded residue stand.

**The front end needed a TestSite layout before it could be photographed honestly.** The harness
had none, so every page rendered with no `<html>`/`<head>`/`<body>` and `_Styles.cshtml` had
nowhere to go — the documented "no layout, no stylesheet" state in `docs/booking-page.md`. New
`src/UBookIt.TestSite/Views/_ViewStart.cshtml` and `Views/Shared/_Layout.cshtml` give it what a
real site has. **The shipped stylesheet had never been seen rendered on this harness**; it works.

**7.4 / 6.2: all five arms of the image guard were shown to fire** — bad host, missing file, wrong
ref, relative path, and empty alt text — each with the mutation verified present on disk before
the run. The alt-text arm first reported blind; that was a bad mutation of mine (stripping a
prefix still left alt text), not a blind guard. Re-mutated properly, it fires.
[[verify-the-instrument-mutated-the-file]].

**7.2** was checked by opening the produced `.nupkg`: 13 entries, `README.md` packed, the only
image `icon.png`. No `docs/images` entry.

## 8. The publish step the pin requires (spec: *What the pin asks of a publish is written down*)

- [x] 8.1 Add the tag step to `docs/publishing.md` in the correct position (tag pushed **before**
      the package), and verify the ordering against the sequence already recorded there.
- [x] 8.2 State the post-publish human check — that the package page renders every image — and
      verify it is pinned by the runbook's own guard so it cannot be tidied away.
- [x] 8.3 Record in `docs/publishing.md` that no automated check can see a missing tag, so the
      limitation is stated rather than implied.

## 9. Verification

- [x] 9.1 Build the client first (`npm run build`), then run each test project sequentially —
      never a solution-level `dotnet test` — and record counts against the baseline of
      1804 unit / 167 integration / 1168 rendering / 290 client.
- [x] 9.2 `openspec validate --all --strict` and confirm 22/22.
- [x] 9.3 Confirm zero warnings in a clean Release build with the TestSite stopped.
- [x] 9.4 Confirm no source file, schema, API surface or rendering output changed —
      `git diff --stat` should touch only documents, images, tests and the runbook.

## 10. Verification results (2026-09-21)

Client built first, then each project sequentially in Release — never a solution-level
`dotnet test`, per the recorded build/test race.

| | |
|---|---|
| `UBookIt.Tests` | **1807** passed (baseline 1804 + 3 new: the needle control, the evidence control, the image guard) |
| `UBookIt.Tests.Integration` | **167** passed (unchanged) |
| `UBookIt.Tests.Rendering` | **1168** passed (unchanged) |
| Client (vitest) | **290** passed, 13 files (unchanged) |
| Clean Release build | **0 warnings, 0 errors** |
| `openspec validate --all --strict` | **23/23** (22 + this change) |

**9.4 — the change touches no product code.** Modified: `README.md`, `docs/backoffice.md`,
`docs/mvp.md`, `docs/publishing.md` and four files under `tests/`. Added: `docs/images/`,
`tests/UBookIt.Tests/Support/RetiredClaims.cs`, and the two TestSite view files. **Nothing under
`src/UBookIt.Core`, `.Persistence`, `.Backoffice` or `.Web` is modified** — the only `src/` change
is the dev harness, which ships nowhere.

**What no test can see, restated so it is not mistaken for covered:** that the `17.1.0` tag exists
(it does not yet — no release has ever been tagged), and that nuget.org renders the images. Both
are human steps, both now in `docs/publishing.md`, and the pin means **the release change must
move **every** image ref with the version** — the guard fails if it does not.

## 11. QA round 1 — REJECT (4 MAJOR, 5 MINOR, 3 NIT)

QA re-measured every claim in the handover. All confirmed except one, and it did the outward
sibling-spec sweep (clean) that I had not.

- [x] 11.1 **[MAJOR-1] One of sixteen fixtures was retyped, not recovered — and the class doc
      asserted otherwise.** `e545deeb` says *"every key the cancel flow can emit"*; the fixture
      said *"every key the dialog renders"*. Text corrected. **But the finding is the control, not
      the typo:** both existing controls compare the fixture to a needle written in the same
      sitting, so self-consistency passed while provenance was invented — and
      `Every_needle_says_where_its_evidence_came_from` only checked for an `@`. New
      `RetiredClaimEvidenceTests.Every_fixture_is_the_text_the_named_commit_holds` re-runs
      `git show <commit>:<path>` for all sixteen. **This reverses design D2(b)** on evidence; D2
      now records the reversal rather than still arguing against it. The class doc no longer
      asserts the recovery as a fact — it names its enforcement.
      **The new guard's first run accused five innocent fixtures**, because a redirected stream is
      decoded with the console code page and this repository's prose is full of em dashes.
      `StandardOutputEncoding` cut it to the one real failure — [[ubookit-guard-correctness]],
      and the reason its remarks now carry that warning.
- [x] 11.2 **[MAJOR-2] The availability alt text describes a day the image does not contain.**
      It says *"Monday to Sunday"*; the crop ends after Saturday. This change's own thesis,
      failing on its own deliverable, in the one sentence a blind reader gets *instead of* the
      image. **Blocked on a re-capture** — see §12.
- [x] 11.3 **[MAJOR-3] `docs/publishing.md:92` said `<Version>` is "the only place", which this
      change falsified by adding three hand-written refs to the README.** Step 3 now distinguishes
      where the version is *declared* from the four documents that state it in prose, tabulates
      them, and says the guard makes it a checklist rather than a risk. The tag section points at
      it. **This supersedes the recorded "a version bump touches three files" measurement.**
      Fixing it tripped `Every_documented_version_is_the_declared_version`: my table quoted the
      sentence with a `<version>` placeholder, which the guard read as a second, wrong version in
      the file. Described rather than quoted, with a note saying why.
- [x] 11.4 **[MAJOR-4] The GitHub README shows three broken images between merge and tag.** D5
      argued the ref question entirely from the package page and never asked what GitHub does
      with the same file. **Chris's decision: accept the window, close it on the release change.**
      Recorded in `design.md` Risks with the reason the two cases differ — the packed readme is
      frozen so an early ref is merely early, while the repository readme is live so the same ref
      is broken. The `main` alternative's entry now records that it would have kept the repository
      readme working, and why that still loses to a breakage you can see and fix.
- [x] 11.5 **[MINOR-1] `booking-flow.png` shows none of the site chrome its caption claims**, and
      is a third the width of the other two. The layout was added to get chrome into frame and the
      frame has none. **Blocked on a re-capture** — see §12.
- [x] 11.6 **[MINOR-2] `availability.png` carries a stray mouse cursor.** Same re-capture.
- [x] 11.7 **[MINOR-3] The added requirement was broader than its guard** — it demanded every
      image name a file in this repository, while the guard (rightly) skips allow-listed hosts
      that are not ours. The requirement now carries the same carve-out and a scenario for it,
      rather than stating an absolute the implementation does not hold.
- [x] 11.8 **[MINOR-4] `### Moving a booking` was an empty heading and the move prose sat under
      *Recording a booking somebody made by telephone*.** Pre-existing at HEAD — but my new
      cross-reference pointed a reader straight at the empty heading, and task 3.3 re-read that
      very list. Forty lines moved under their own heading; no wording changed, and the suite's
      pins on that prose stayed green throughout, which is the check that the move was pure.
- [x] 11.9 **[MINOR-5] `docs/mvp.md`'s two annotated bullets disagreed on tense** — one converted
      to past, one left present. Both past now; D7 says the file stays history.
- [x] 11.10 **[NIT-1]** The bookings alt text and caption omitted the **New booking** button,
      which is in the frame and is one of the four features this change adds to *What it does*.
- [x] 11.11 **[NIT-2] The crossing guard now exists.**
      `No_guard_pins_a_sentence_the_sweep_holds_out` scans every `Says`/`SaysOnce` in the suite
      against `RetiredClaims.All`. Until now *"A guard does not require a claim that has become
      false"* rested on task 4.2's manual sweep and its stated blind spot. **Verified by
      reinstating the exact pin this change removed**: it fails naming the file, the sentence and
      the capability that falsified it. Carries an anti-vacuity floor so a regex that stopped
      matching cannot pass over nothing.
- [x] 11.12 **[NIT-3]** The requirement claimed to "extend rather than restate" while one arm
      genuinely restates the link guard's relative-image scenario. It now says so and accepts the
      overlap, with the reason.

**Suite after round 1: 1809 unit** (1807 + the evidence guard + the crossing guard), all green.

## 12. All three images re-captured

**All three, not the two that were faulty** — the new browser session rendered the backoffice at a
different scale, so keeping the old bookings shot would have put two visibly different text sizes
on one page. Every image is now 1560px wide, from one session, with the pointer parked out of
frame.

| Image | What changed |
|---|---|
| `booking-flow.png` | Now opens at the page top, so the **site's header, navigation and typeface are in frame** — which is what the caption always claimed and the old crop never showed. 470×475 → 1560×670. |
| `bookings-screen.png` | Re-taken at the new scale; also removes a pointer artifact over the Find label that the old one carried and nobody had noticed. |
| `availability.png` | Includes the **Opening hours** panel title for context, and no cursor. |

**Every alt text was then read against its own image, one at a time** — the verification 7.3
claimed and did not perform. That caught two more over-claims of exactly the kind MAJOR-2 named,
both mine, both from this round:

1. The first re-crop showed all seven days but clipped Sunday's *Add window* link, while the alt
   text said every day had one. Fixed by cropping to a clean boundary and describing the frame:
   Monday to Friday shown, the rest continuing below.
2. A draft said "Saturday begins at the foot of the picture" when only an unlabelled panel edge
   was visible.

The lesson is not that the alt text was wrong twice more. It is that **reading the sentence
against the picture catches it in seconds and nothing else does** — no guard can see this, and it
took a human reviewer to find the first one.

## 13. Round 1 verification (2026-09-21)

Client built first, then each project sequentially in Release with the TestSite stopped.

| | |
|---|---|
| `UBookIt.Tests` | **1809** passed (1807 + the evidence guard + the crossing guard) |
| `UBookIt.Tests.Integration` | **167** passed |
| `UBookIt.Tests.Rendering` | **1168** passed |
| Client (vitest) | **290** passed |
| Clean Release build | **0 warnings, 0 errors** |
| `openspec validate --all --strict` | **23/23** |

## 14. QA round 2 — REJECT (2 MAJOR, 3 MINOR, 8 NIT)

Both MAJORs were **inside round 1's fixes**, as predicted. QA also re-derived all 16 fixtures
independently (16/16), proved the MINOR-4 move pure by a line-multiset diff rather than by the
green pins I offered, and confirmed every other claim — except one, again.

- [x] 14.1 **[MAJOR-A] The crossing guard was blind to a wrapped pin, and its floor could not
      notice.** QA reinstated the retired pin split across two concatenated literals — a shape
      three pins here already use — and the guard **passed**. The regex required `"…")`, matching
      176 of 185 call sites; the floor was 50, so the entire class could have been lost without
      the number moving. Line-wrapping has defeated a guard on this project twice before.
      **Replaced the regex with a scanner** that walks to the matching close paren and joins
      every literal inside, and **replaced the numeric floor with a coverage assertion**: every
      call site must be read, and the only ones allowed to go unread are registered individually
      with a reason. Registered **by call text, not line number** — a line number moves on
      unrelated edits and a guard that cries wolf gets weakened rather than fixed.
      Verified with QA's own mutation: it now fails, naming file, line and capability.
      **The fix had a fault of its own**: the scanner read its own registry strings as five more
      unreadable pins, so the file that documents the guard defeated the guard — the same shape
      that broke `SaysOnce`'s pin on `docs/publishing.md`. Excluded, with the reason recorded.
- [x] 14.2 **[MAJOR-B] The release checklist told you to edit the one line that must never be
      edited.** My table row named "the Status line", which is the `first release is 17.0.0`
      **anchor** pinned by `The_documented_anchors_do_not_move` — three lines above my own
      paragraph saying history must not move. A releaser following the checklist would have
      turned the build red. The row now names the *"uBookIt is at"* sentence under *What
      nuget.org will not let you undo*, and a callout says plainly that the Status line is not
      on the list.
      **And the fix reproduced the trap it was fixing**: my first draft *quoted* the anchor
      verbatim, duplicating a pinned phrase — which `docs/publishing.md` itself warns about four
      paragraphs earlier — and added a `nuget.org` mention the accounting guard had not been
      told about. Described rather than quoted now.
- [x] 14.3 **[MINOR-a]** "Four documents" was four rows across three documents. Now three, with
      `README.md` counted once.
- [x] 14.4 **[MINOR-b]** "Drifted" survived in three places. `proposal.md`'s Impact row was the
      serious one — it recorded "one drifted needle corrected", and **both halves were false**:
      the needle was deliberately *kept*, because correcting it would have destroyed a valid
      guard. The archived proposal is the record of what this change did.
- [x] 14.5 **[MINOR-c] The flow image gained chrome and lost the form.** Round 1's fix for
      MINOR-1 made it a page-top capture showing nothing but a column of date radios. One frame
      cannot hold the site chrome and the booking form at this viewport, so **the flow now takes
      two images**: the page top, and the lower half showing the wrapping start-times run, the
      labelled fields and the privacy notice. D6 records the revision from three to four.
      Adding the fourth immediately falsified *"the **three** screenshot URLs"* in the runbook —
      a count I had written two hours earlier. Both count claims are now count-free.
- [x] 14.6 **[NIT-1]** `tasks.md` said the round-1 flow image was "910 → 1560"; it was 470×475.
      A measurement stated as fact, wrong, in the record of a round about measurements stated as
      fact. Corrected in §12.
- [x] 14.7 **[NIT-2]** The CHANGELOG row is guarded by `ChangelogTests`, not `VersionTruthTests`.
- [x] 14.8 **[NIT-3]** The `verified == All.Count` half of the anti-vacuity assert is entailed by
      the assert above it and cannot fire. Kept as a stated invariant, with a comment saying so,
      and the half that *can* fire given its own message.
- [x] 14.9 **[NIT-4]** An empty `AsWritten` verified against any file, because `Contains("")` is
      always true. Caught explicitly now, so the guard stands without its sibling.
- [x] 14.10 **[NIT-5]** `GitShow` read stdout to the end before stderr — the classic pipe
      deadlock — and discarded the `WaitForExit` result, so a timeout would throw on
      `ExitCode` rather than report. Both streams read concurrently; a timeout kills the process
      and reports.
- [x] 14.11 **[NIT-7]** The 119-character line in the spec delta is rewrapped.
- **[NIT-6]** Left as QA advised: a fake badge in the README to exercise the unchecked branch is
      a worse cure than the untested branch.
- **[NIT-8]** Left: the tag examples use `17.1.1` deliberately, as a forward-looking runbook.

**Suite after round 2: 1809 / 167 / 1168 / 290, clean Release 0 warnings, `--strict` 23/23.**

## 15. QA round 3 — REJECT (1 MAJOR, 4 MINOR, 4 NIT)

Both round-2 MAJORs confirmed fixed; MAJOR-A judged fixed well. The MAJOR raised was in the new
image's alt text — round 1's MAJOR-2 class again.

- [x] 15.1 **[MAJOR] The new alt text said "ten radio options"; the image shows nine.**
      12:30 to 16:30 at half-hour steps is nine, and the frame confirms seven on the first row
      and two on the second. **This is the fourth wrong count in this change** — 910→1560, "four
      documents", "the three screenshot URLs", and now this — and the first that would have
      shipped: alt text is frozen into five packages and is the sentence a blind reader gets
      *instead of* the picture, so they are the one reader who cannot catch it.
      Corrected, and then **every number in the four alt texts and captions was audited**: the
      rest are literals read off the screen (times, dates, "30 minutes") or counts QA
      independently confirmed ("four bookings", "two rows", "two paragraphs").
- [x] 15.2 **[MINOR-1] The new callout said "described, not quoted" three lines above a
      paragraph quoting the anchor verbatim.** `docs/publishing.md` carried *"the first release
      is `17.0.0`"* twice. Round 3 created the contradiction by adding the rule without re-reading
      its neighbour — the exact fault the callout exists to prevent. Both history statements are
      now described, and the file carries the phrase once.
- [x] 15.3 **[MINOR-2] The MAJOR-B rewrite silently dropped coverage.** Round 2's row read "the
      Status line, **and this section**"; round 3 removed the wrong half and took the right half
      with it. The tag section still spells `17.1.1` out four times, including a copy-pasteable
      `git tag`, and nothing on the checklist mentioned them — so at `17.2.0` a releaser would
      tag the wrong version. **Wholesale replacement applied to a runbook row**: a guarantee the
      previous version carried, not restated, invisible in the diff. Restored as its own row,
      and stated plainly that **no guard checks it** — the illustrative commands are invisible
      to every test, so that row is the one to re-read by eye.
- [x] 15.4 **[MINOR-3] The rationale described the mechanism, and described it wrongly.** It
      said the guard "reads every one it finds"; it matches one phrasing only, and a bare version
      elsewhere in the file is safe — as the tag section and a `<version>` in a URL both
      demonstrate today. Restated as the guarantee: quoting a *pinned sentence* reproduces it,
      and a pinned sentence that appears twice stops pinning the original.
- [x] 15.5 **[MINOR-4]** `tasks.md`'s forward-looking instruction to the release change still
      said "the three image refs". Now count-free, like the durable instruction in the runbook.
- [x] 15.6 **[NIT-1] A comment describing behaviour the code does not have** — it claimed a
      verbatim or raw literal "would read as unreadable", and QA measured both forms being read
      **correctly** and caught. Safer than claimed, and still the defect this change is about.
      Restated as the guarantee: such a pin cannot silently pass.
- [x] 15.7 **[NIT-2]** `Regex.Unescape` throws on an unrecognised escape, so a verbatim literal
      containing `\p` would crash the guard rather than report. Wrapped: a guard that crashes
      says nothing about what it guards.
- [x] 15.8 **[NIT-4] The registry is now guarded in both directions.** An entry matching no call
      site fails as a stale exemption. Verified by adding a bogus entry: it fails naming it.
      [[a-rule-has-preconditions]] — guard both directions of any normalisation.
- **[NIT-3]** Left. The self-exclusion could be removed by dropping the `DocumentationAssert.`
      prefix from the registry strings and the scanner's own pattern, but that makes both less
      readable to buy back a file that holds no pins. Recorded as a deliberate choice; QA
      verified it hides nothing today.

**Re-verified after the NIT edits, because they touched the scanner:** the wrapped-pin mutation
still fails naming file, line and capability.

**Suite after round 3: 1809 / 167 / 1168 / 290, clean Release 0 warnings, `--strict` 23/23.**

## 16. QA round 4 — APPROVE WITH NITS

Every round-3 finding verified fixed by re-measurement, not by reading the claim. QA diffed the
whole README and confirmed `ten` → `nine` was the **only** change and that all four PNGs are
byte-identical to round 3 — so its line-by-line reading of the other three alt texts carries
forward rather than being taken on trust. It re-enumerated every countable assertion across the
four images independently and reached the same result. It re-proved the scanner itself rather
than accepting my re-proof, and tested NIT-4's registry in **both** directions with mutations of
its own design.

- [x] 16.1 **[NIT] The Tag row named two kinds of example; the section holds four version
      literals** — the illustrative URL, `git tag`, `git push` and `curl`. The row now covers
      every one. Cosmetic, and recorded rather than assumed, which is the habit worth keeping:
      the row's whole instruction is "re-read this by eye", and a row that under-describes what
      to re-read is the same species of defect as everything else in this change.

**Declined, with the reason on the record:** NIT-3, the self-exclusion of
`RetiredClaimEvidenceTests.cs`. Removing it means dropping the `DocumentationAssert.` prefix from
the registry strings and the scanner's own pattern, which makes both harder to read to buy back a
blind spot in a file that holds no pins. QA verified again that it hides nothing today.

**Final: 1809 / 167 / 1168 / 290, clean Release 0 warnings, `--strict` 23/23.**

## 17. The arc, since it is the useful part of the record

**Four rounds. Three of them, the fix carried the next defect; the fourth did not.**

| Round | Verdict | Where the findings were |
|---|---|---|
| 1 | REJECT — 4 MAJOR | The original implementation |
| 2 | REJECT — 2 MAJOR | **Both inside round 1's fixes** |
| 3 | REJECT — 1 MAJOR | **The image added by round 2's fix**, and three self-contradictions created while fixing round 2's self-contradiction |
| 4 | APPROVE | One cosmetic row |

**The single most reusable thing here.** Every guard this change added was *written* correctly and
*scoped* wrongly, and in each case the scope error was invisible from the guard's own green run:

- the needle control compared a fixture to a needle written in the same sitting — self-consistency,
  not provenance;
- the crossing guard's regex read 176 of 185 call sites, and its numeric floor could not notice the
  other nine going missing;
- the evidence guard's first run accused five innocent fixtures because a redirected stream is
  decoded with the console code page.

**A floor that counts cannot see a class disappear; a coverage assertion can.** That is the change
from round 2 to round 3 and it is the one worth carrying forward.

**And the one no guard reached, four times over:** a sentence asserting something its artifact does
not contain — "Monday to Sunday" over a frame ending at Saturday, "ten radio options" over nine,
"four documents" over three, "the three screenshot URLs" over four. Reading the sentence against
the thing it describes caught every one in seconds. Nothing else did, and nothing else could.
