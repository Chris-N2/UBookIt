## Context

See `proposal.md` — *Why*. What matters for the approach: the mechanism that should have caught
this already exists and is good
(`NotificationDocumentationTests.The_claims_this_change_falsified_are_not_made_anywhere_in_the_docs`,
a discovery-based sweep over every shipped markdown file). It failed for three reasons that are
each worth fixing separately, because each fails differently:

1. **Nothing obliged anyone to feed it.** It is a test, and no requirement said a landing
   capability must retire the sentences it falsifies.
2. **A sibling guard pinned one of the false sentences in place**
   (`BackofficeDocumentationTests.cs:150`). Correcting the document would have turned the suite
   red, which is the worst possible signal to give the person doing the right thing.
3. **One needle never matched anything in its life.** The sweep holds out
   `"there is no search by name, email or reference"`; `README.md:121` says *"There is no search
   by name or reference"*. **Corrected during apply — it did not drift.** The README carried the
   long wording at `ca18ccd` and it was narrowed at `ae55773` when the email search shipped (a
   correct edit); the needle was written *afterwards*, quoting wording that had already gone. It
   has passed every run since, matching nothing, while the real sentence went on to be falsified
   by `find-booking`.

The population of false claims, measured rather than sampled — every shipped markdown file was
swept for negative claims about the five `17.1.0` features:

| File | Line | Claim | Falsified by |
|---|---|---|---|
| `README.md` | 114 | "Taking a booking on someone's behalf. Bookings arrive through the front-end flow." | ㊳ |
| `README.md` | 121 | "There is no search by name or reference." | ㊴ (reference only; name search is genuinely still absent) |
| `docs/backoffice.md` | 327 | "It does not take a booking on someone's behalf." | ㊳ |
| `docs/backoffice.md` | 519–520 | "**It does not place bookings.** …is not built. Bookings arrive through the front-end flow." | ㊳ |
| `docs/backoffice.md` | 524 | "It does not find a person across bookings." | ㉔ find-by-booker, surfaced in the UI by ㊴ |
| `docs/mvp.md` | 93–94 | "Taking a booking on someone's behalf… Bookings arrive through the front-end flow." | ㊳ |
| `docs/mvp.md` | 95–97 | "Finding a booking without knowing its date… searching by booker name, email or reference is a different query" | ㊴ |

`docs/backoffice.md` contradicts itself: §368 *Finding a booking* and §395 *Recording a booking
somebody made by telephone* describe in detail the two features §327 and §519 deny.

`docs/mvp.md:524`'s neighbour is the precedent for how that file is maintained: when
`move-booking` shipped, its *Amending a booking's time* bullet kept the v1-era statement and
gained a parenthetical saying what changed in `17.1.0`. Two bullets did not get the same
treatment when ㊳ and ㊴ shipped.

Also settled here because it decides the screenshot work: **nuget.org renders readme images only
from an allow-list of hosts**, and renders nothing at all from a relative path. `raw.githubusercontent.com`
is on that list; so are `camo.`, `media.` and `user-images.githubusercontent.com` and `raw.github.com`.
Plain `github.com` is **not**, except for a workflow badge path. A rejected image produces a
warning "only visible to the package owners" — so the failure is silent to everyone whose opinion
we would want.

## Goals / Non-Goals

**Goals (design-level, beyond the proposal's scope statement)**

- Leave the sweep stronger than a one-off correction would: the next capability to land should
  find an obligation and a working instrument, not a habit to remember.
- Make the screenshots survive their own release. The readme is frozen per version; an image is
  not, and the design has to decide what a reader of an old package page sees.

**Non-Goals**

- Not a rewrite of the sweep's architecture. Discovery-based enumeration, the scoping rationale
  and `DocumentationAssert` are sound and stay as they are.
- Not a general "documentation is accurate" guard. The claim is specifically about **denying a
  shipped capability** — a decaying negative — not about prose being up to date, which no test can
  decide.

## Decisions

### D1 — Retire a sentence in both halves, never just delete it

Deleting a false sentence fixes today. Adding it to the sweep fixes every future draft, including
the one an author writes by copying an older version of the file. Both halves, every time; the
spec's *A retired sentence cannot be reintroduced* scenario is the check that the second half
happened.

**Alternative rejected:** delete-only, and rely on review. That is exactly the process that has now
failed three consecutive times.

### D2 — The needles are validated against evidence, not against intention

The needle that never matched is the more interesting failure, because it passed. A `DoesNotSay` needle is
unfalsifiable by construction: it asserts absence, so a needle that could never match anything and
a needle doing its job are indistinguishable from a green run.

**The control is a fixture of the retired sentences as the documents actually carried them**,
copied out of the file at the moment of deletion (or out of `git show HEAD:<path>` for the ones
being retired here), with a test asserting that **every needle matches its fixture**. The fixture
is the evidence; the needle is the instrument; the test is the crossing. A needle that does not
match the wording it was written against then fails immediately, in the run that introduces it —
whether it drifted from a sentence, or (as the 0.3.0 one did) never matched at all.

This is the shape [[a-guard-must-be-able-to-fire]] and `rule-checks-mechanism-not-guarantee` both
point at: ask what would differ if the statement were false, and arrange for something to differ.

**Alternatives rejected:** (a) normalise harder — whitespace normalisation is already there and
did not help, because the difference was a missing word; (b) assert the needle appears in git
history — correct in principle, but makes the suite depend on the git object store and on history
never being rewritten.

**(b) was REVERSED during apply, on evidence.** QA found that one of the sixteen fixtures had been
retyped rather than recovered — and that neither control could see it, because both compare the
fixture to a needle written in the same sitting. Self-consistency is not provenance, and the class
doc was asserting provenance as a fact. `RetiredClaimEvidenceTests` now re-runs
`git show <commit>:<path>` for every entry. The dependency this alternative was rejected for is
real and is now accepted: the repository is always a checkout, the commits are old and on `main`,
and a rewrite orphaning them would be a much larger event than a red test. **The argument against
(b) was written before there was a counter-example, and the counter-example turned out to be
inside the very fixture the argument justified.**

**That guard's first run accused five innocent fixtures**, because a redirected process stream is
decoded with the console code page and this repository's prose is full of em dashes. Setting
`StandardOutputEncoding` cut it to the one real failure. A text-comparing instrument is not
evidence until it has been normalised — [[ubookit-guard-correctness]].

### D3 — `BackofficeDocumentationTests.cs:150`'s pin is retired, and the whole list around it is
re-read

The pin goes because the sentence goes. But the finding is a sample: the same test block pins
three sentences from one list in `docs/backoffice.md` (*What the section does not do*), and of the
four bullets in that list, **two are now false** — "does not place bookings" and "does not find a
person across bookings". So the task is to re-read the list, not to delete one line.

The remaining true entries keep their pins: *does not change which resources a booking claims*
and *does not keep a history of where a booking has been* are both still accurate, and both are
assumptions "you can move a booking" actively invites.

### D4 — Images are served from `raw.githubusercontent.com`

Verified against nuget.org's published allow-list rather than assumed. The alternatives:

| Option | Why not |
|---|---|
| `github.com/<owner>/<repo>/blob/<ref>/<path>` — the form the existing link guard enforces for documents | Not on the allow-list, and serves an HTML page rather than image bytes. It would fail twice over. |
| Pack the images into the `.nupkg` | A packed readme's images are fetched by URL by the rendering host; a file inside the package has no URL the readme can name. Only the icon, licence and readme have that treatment. |
| `data:` URIs | Not a supported image source, and would bloat a readme that is rendered in the Visual Studio package pane. |

### D5 — The image URL is pinned to a release tag, and the tag becomes a publish step

**The problem:** the packed readme is frozen per published version; the image it names is fetched
live. An address on `main` means the `17.1.1` package page shows whatever `docs/images/` holds in
two years — a screenshot of a screen that has since changed, or a gap where a renamed file used to
be — on a page nobody can correct.

**The decision:** address images as
`https://raw.githubusercontent.com/Chris-N2/UBookIt/<version>/docs/images/<file>`, where
`<version>` is a git tag created at release. `VersionTruthTests` derives the expected ref from
`Directory.Build.props` in exactly the way it already derives the documented version, so a bump
that forgets the images fails the suite rather than shipping a page pointing at the previous
release.

**This adds a hard step to publishing: the tag must be pushed before the package is.** Until the
tag exists the URLs 404. That is acceptable because the ordering is already strict here (publish →
confirm on the feed → stamp the changelog date → sync → archive) and because **release tags are a
recorded want for this repository** — `17.0.0`, `17.0.1` and `17.1.0` were never tagged. This
change spends that obligation rather than carrying it.

**Alternatives rejected:**

- **`main`** — zero process, and it would in fact keep the repository readme working at every
  moment, which is the one thing the tag does not do. Rejected anyway, because its failure is
  permanent and invisible where the tag's is temporary and obvious: renaming a screenshot breaks
  every previously published package page at once, for good, with the warning shown only to the
  package owner. A breakage you can see and fix beats one you cannot see and cannot fix.
  **This comparison is the one the first version of this design failed to make** — it weighed
  `main` only against the package page, and so never noticed the repository readme had a
  different answer.
- **A commit SHA** — immutable and needs no tagging, but circular: the SHA of the commit that
  contains the readme cannot be written into that readme. It could be patched in a follow-up
  commit, which means the tree that was packed is not the tree that was reviewed.

### D6 — A small set of screenshots, each earning its place, each with real alt text

The readme is also rendered in the Visual Studio package pane, so this is a small set, not a
gallery:

1. **The booking flow as a visitor sees it** — the thing the package is for, and the one image
   that answers "what will my site look like".
2. **The bookings list in the backoffice** — what an operator works in; carries the reference
   column, the status filter and the date window in one frame.
3. **A resource's availability configuration** — the part that is hardest to picture from prose.

**Revised to four during QA round 2.** The flow shot was originally one image, and the round-1
fix for MINOR-1 (no site chrome in frame) made it a page-top capture — which put the header in
but left the image showing nothing but a column of date radios. One frame cannot hold both the
site chrome and the booking form at this viewport, so the flow takes **two**: the page top, which
shows uBookIt sitting inside somebody else's layout, and the lower half, which shows the start
times wrapping, the labelled fields and the privacy notice where the details are asked for.
Neither could carry the other's caption honestly.

**Alt text describes the screen, not the file.** A README whose accessibility section is its
headline differentiator cannot ship `![screenshot](…)`. Alt text is a task-list item with its own
verification, not a detail.

**What is in frame is composed deliberately.** Captures come from the dev TestSite, whose bookers
are fabricated — but the frame is checked before it is committed, and no capture includes a real
address, a key, or a host name that is not `localhost`.

### D7 — `docs/mvp.md` is annotated, not exempted

It is a shipped document a consumer can reach, and the sweep already reads it. It is also
explicitly history, so its v1-era statements are not corrected to the present tense — they gain
the parenthetical the `move-booking` bullet already has. This also keeps the file compatible with
the new needles: the needles match the present-tense claim, and the paraphrase-plus-parenthetical
form does not contain it. That is the same arrangement `move-booking` made deliberately, and it is
why that bullet needs no exemption today.

## Risks / Trade-offs

- **The tag is never pushed, and every image on the package page is a gap** → the runbook step,
  plus the human post-publish check the spec requires. No guard can see this; the spec says so
  rather than implying otherwise.
- **The repository's own README shows three broken images between merge and tag** → **accepted,
  time-bounded, and closed by the release change.** Found by QA as MAJOR-4; D5 argued the ref
  question entirely from the *package page* and never asked what GitHub does with the same file.
  It renders `README.md` too, so from the moment this merges to `main` until `17.1.1` is tagged,
  `github.com/Chris-N2/UBookIt` shows three broken-image icons — and unlike nuget.org's
  owner-only warning, that is visible to everyone.

  **The two cases are genuinely different, which is why one guard covers both and only one is
  defensible.** The packed readme is frozen at publication, so a ref that resolves only after the
  tag exists is merely early; nobody can reach that version's page before the package is pushed.
  The repository readme is live from the merge, so the same ref is simply broken for as long as
  the window lasts.

  **Chris's decision, 2026-09-21: accept the window and close it on the release change.** The
  mitigation is that `17.1.1` is the next change, not a distant one, and its tag step is now a
  runbook item. The honest statement of the cost: for as long as `17.1.1` is unreleased, the
  landing page is worse than it was. If that window stops being short, the answer is to bring the
  release forward, not to repoint the images at a moving branch — that would trade a visible
  temporary breakage for an invisible permanent one on every published page.
- **The new sweep needles are too aggressive and fail on a legitimate sentence** — e.g. a doc
  legitimately saying an operator cannot change a booking's *resources* → needles are the exact
  retired sentences, not paraphrases or keyword patterns, and D2's fixture test proves each one
  matches the sentence it was written for and nothing else.
- **Screenshots go stale as the UI changes** → the tag pin makes a stale screenshot a property of
  an old release rather than a lie on the current page, and the capture list is short enough to
  retake. Accepted: this is a maintenance cost the change takes on knowingly.
- **A fourth false claim exists that this sweep did not find** → the measurement above is over
  negative claims about the five `17.1.0` features. Claims falsified by *earlier* releases are not
  re-measured here, and two such (0.3.0, 0.5.0) have already been found in the past. Stated as a
  limit rather than covered by silence.
- **`docs/images/` is picked up by packing** → verify it is not swept into any `<Content>` glob;
  the images are fetched by URL and have no business in the `.nupkg`.

## Migration Plan

None — no schema, no API, no configuration. The change is documents, one test-suite correction and
one guard extension.

Rollback is `git revert`; nothing is published by this change. The tag introduced by D5 is created
by the *release* change that follows, not by this one — this change only writes the addresses that
require it and the runbook step that produces it.

## Open Questions

- **Exactly which three screens, and at what width.** The set in D6 is a decision, but the precise
  framing is worth a look at the captures before committing them. Deferrable: it changes no
  requirement, no task and no guard.
