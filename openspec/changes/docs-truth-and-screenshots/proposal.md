## Why

**uBookIt's package page currently tells a prospective consumer that the package cannot do three
things it has done since `17.1.0`.** The README's *What it does not do yet* still says bookings
cannot be taken on someone's behalf and that there is no search by reference;
`docs/backoffice.md` says both of those and adds that the section "does not find a person across
bookings" — while the same document, 190 lines earlier, documents the **New booking** button and
the **Find** box in detail. That readme is packed into all five packages and is the first thing
anybody reads.

Three separate mechanisms were in place to prevent exactly this and none of them fired:

- `NotificationDocumentationTests.The_claims_this_change_falsified_are_not_made_anywhere_in_the_docs`
  is a sweep over every shipped markdown file that holds retired sentences out. It exists
  *because* this happened twice before (0.3.0's find-by-booker and 0.5.0's emails, both found in
  the README, which at the time no guard read). **`booking-on-behalf`, `find-booking` and
  `self-service-cancellation` each shipped without feeding it.**
- `BackofficeDocumentationTests.cs:150` does the opposite of preventing it: it **pins the false
  sentence in place** with `DocumentationAssert.Says(docs, "It does not place bookings")`. The
  comment above that line records approve/decline and amend leaving the list as they shipped, so
  the maintenance habit was known — on-behalf shipped and neither the sentence nor its pin moved.
- The sweep's 0.3.0-era needle is `"there is no search by name, email or reference"`. The README
  actually says *"There is no search by name or reference"* — three words shorter, so the needle
  misses it. **Corrected during apply, and the correction is sharper than this:** the README
  carried the long wording at `ca18ccd` and it was narrowed at `ae55773` when the email search
  shipped — a correct edit — and the needle was written *afterwards*, against the wording that had
  already gone. It never matched anything, from the day it was written. A `DoesNotSay` assertion
  cannot report that about itself, which is why this change gives every needle a control.

Nothing here reaches a consumer's site or changes a byte of behaviour, which makes it patch work —
but the packed readme is **frozen per published version**, so the correction cannot be made in
place and costs a version number to deliver. That is the entire reason `17.0.1` exists.

The same delivery carries a request from the Umbraco Discord: **screenshots in the README**. It
belongs in the same change because it is the same file, the same freeze, and it walks into the
same trap from the other side — nuget.org renders **no image from a relative path and none from a
domain outside its allow-list**, and it reports that only to the package owner.

## What Changes

**The false claims, repaired at the source**

- `README.md` — *What it does not do yet* loses the two bullets `17.1.0` falsified and keeps the
  parts still true (name search is genuinely still absent; swapping a booking's resources is
  genuinely not built).
- `README.md` — *What it does* gains the four `17.1.0` features it never mentioned: booking on
  someone's behalf, lookup by reference, self-service cancellation, and the settings screen.
- `docs/backoffice.md` — lines 327, 519–520 and 524 stop contradicting the sections at 368 and 395
  that describe the very features they deny.
- The sweep is **fed the sentences those three changes retired**. The 0.3.0 needle is **kept** —
  it legitimately guards the pre-`ae55773` wording, a sentence a document really did carry — and
  the literal the README actually contains is added beside it. Replacing it, as this proposal
  first said, would have destroyed a working guard.
- Every needle gains the document text it was written against, and a test asserts the two match,
  so a needle written from memory fails in the run that introduces it.
- `BackofficeDocumentationTests.cs:150`'s pin on `"It does not place bookings"` is retired, and the
  sentences that legitimately remain in that list keep theirs.

**The sweep is given a reason to be fed**

The sweep is a test with no requirement behind it, which is why three changes walked past it. A
requirement in `packaging` makes "the shipped documentation does not deny a shipped capability" a
guarantee the package makes, so the next feature has an obligation rather than a habit to
remember.

**Screenshots**

- A small set of screenshots added to `README.md`, served over absolute URLs from a host
  nuget.org renders (`raw.githubusercontent.com` is on its allow-list; `github.com/…/blob/…`, the
  form the existing link guard enforces for documents, is **not**, and serves HTML rather than an
  image in any case).
- The packed-readme guard is extended so an image is held to what a link is already held to — the
  file it names exists in this repository — which today it is not: the guard accepts any absolute
  `https` URL outside the repository's own blob prefix **unchecked**, and a `raw.githubusercontent.com`
  URL is exactly that.

## Non-goals

- **No behavioural change of any kind.** Nothing in the package's code, schema, API surface or
  rendering is touched. This is a patch, and the README publishes that a patch carries "fixes and
  internal changes only".
- **Not the version bump.** `17.1.0` → `17.1.1` and the changelog entry belong to a separate
  release change, on this project's rule that a change is a unit of work and a version is a unit
  of publication.
- **Not the Settings view manifest.** The Settings tab rendering for users who cannot use it is a
  recorded obligation, but hiding it is a change a user can see, and closing it properly means
  sweeping every view manifest rather than the one instance found. Excluded deliberately, not
  overlooked.
- **Not the DOM test environment.** Large enough to distort this change, and it needs no release
  at all since it ships no bytes.
- **Not the other tidy-up items** — the duplicated pin in `docs/notifications.md`, the `dotnet pack`
  test collision, the ordering-test landmine, `CancellationSecrets` housekeeping. Each is its own
  change; they accumulate on `main` and `17.1.1` publishes them together.
- **`docs/mvp.md` is annotated, not rewritten.** This proposal first excluded it; the measurement
  in §1 found two of its bullets falsified, and it is a shipped document the sweep already reads.
  Its v1-era statements stay in the past tense and gain a `17.1.0` parenthetical — the form its
  own *Amending a booking's time* bullet already uses. Not a correction to the present tense: the
  file is history and stays history.
- **No new screenshot of anything holding real data.** Captures come from the dev TestSite, whose
  bookings are fabricated.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `packaging`: two **added** requirements — (1) the documentation a consumer can read SHALL NOT
  deny a capability the package ships, verified by a sweep that is fed as capabilities land; (2) an
  image in the packed readme SHALL name a file present in this repository and be served from a host
  the package feed renders. Both are additive; the existing packed-readme link requirement is
  **not** rewritten, because a requirement this change has no need to replace is a requirement
  whose guarantees cannot be silently dropped.

## Impact

| | |
|---|---|
| `README.md` | Two bullets removed from *What it does not do yet*, four features added to *What it does*, screenshots added. Packed into all five packages. |
| `docs/backoffice.md` | Three false statements corrected (lines 327, 519–520, 524). |
| `docs/publishing.md` | Gains whatever step the image-ref decision requires of a publish. |
| `tests/…/NotificationDocumentationTests.cs` | Sweep fed with the retired sentences, moved into `RetiredClaims` with the document text each was written against, and given two controls. The 0.3.0 needle is **kept, not corrected** — it guards a real pre-`ae55773` sentence; the literal the README actually carries is added beside it. |
| `tests/…/BackofficeDocumentationTests.cs` | One pin retired. |
| `tests/…/VersionTruthTests.cs` | Packed-readme guard extended to check an image names a file that exists. |
| `docs/images/` | New. Screenshot files, under version control because the guard resolves them against the working tree. |
| `src/UBookIt.TestSite/Views/` | **New `_ViewStart.cshtml` and `Shared/_Layout.cshtml`, added during apply and not foreseen in this table.** The harness had no layout, so every page rendered with no `<html>`/`<head>`/`<body>` and the stylesheet partial had nowhere to go — the documented "no layout, no stylesheet" condition. The front end could not be photographed as a consumer sees it until the harness had what an ordinary site has. Dev harness only; nothing here ships. |
| Code, schema, API surface | **Untouched.** Nothing under `UBookIt.Core`, `.Persistence`, `.Backoffice` or `.Web` is modified. |

No breaking change. No schema change. No new configuration.
