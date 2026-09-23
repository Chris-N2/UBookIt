# The sibling-falsification sweep

What publishing `18.1.0` makes untrue on this line. Run over all 24 specs and five shipped
documents, after the packages were live.

## Checked

All 24 specs on `dev/v18`: `availability`, `booker-erasure`, `booking-emails`,
`booking-management`, `booking-retention`, `bookings`, `default-frontend`, `delivery-api`,
`email-templates`, `packaging`, `permissions`, `persistence`, `privacy-notice`,
`public-holidays`, `resource-management`, `resources`, `responsibility`,
`self-service-cancellation`, `sensitive-data`, `service-booking`, `services`, `site-closures`,
`site-settings`, `theming`. Plus `README.md`, `docs/publishing.md`, `docs/configuration.md`,
`docs/backoffice.md`, `CHANGELOG.md`.

Swept for version literals, `v17`/`v18`, line contrasts, `Marketplace`, feed and package-page
references, and the decay words — `currently`, `today`, `so far`, `has never`, `only release`.

## Found, and fixed here

### 1. `packaging` — two present-tense sentences this release made false

> "…why nothing a resolver reads **has ever** contradicted that."
>
> "…the constraint **exists only in prose** — the readme, this spec and `CLAUDE.md`, none of which
> a package manager reads."

Both describe a feed that no longer exists. Every version a resolver would actually pick on either
line carries the Umbraco bound in its packed nuspec — verified for `18.1.0` at task 6.3
(`[18.2.0, 19.0.0)`). **`18.1.0` closed the last gap**: from `17.1.2` and `18.0.0` onward the
metadata says which line a package is for, so a resolver *can* tell them apart and the constraint
is no longer prose-only.

**This is the "one line has something the other does not" class the sweep was pointed at**, and it
is the most satisfying kind of finding: a sentence describing a deficiency that the work has since
removed. It began decaying at `17.1.2`/`18.0.0` and became wrong for the newest version of both
lines simultaneously today.

Fixed by putting the history into the past tense and adding a paragraph stating what the feed now
looks like — including that **`17.0.0`–`17.1.1` remain unbounded and always will**, since a
published version keeps the metadata it shipped with. A claim that uBookIt runs on both majors is
still supportable from those four versions and from nothing newer. One scenario added; 4 in, 5
out, nothing dropped.

### 2. `CHANGELOG.md` on this line had no entry for `17.1.2` or `17.2.0`

Both are live on nuget.org. The 17 flat container returns six versions; this file listed four of
them. Meanwhile **the `18.1.0` entry written today cites `17.2.0` twice**, and `README.md` sends a
consumer to this very file, pinned at `blob/18.1.0/`. So a reader following the link met two
references to a release the document does not contain.

Fixed by copying both entries from `main` **unedited**. Copying history is not editing it — the
rule that a released entry is never rewritten is untouched, and both entries keep their original
dates.

## Not a finding: the unsynced delta

The sweep noted that `packaging` does not yet carry the sixth contract scenario while
`ChangelogTests` already enforces it on this branch. That is task 8.3 and is the next step, not a
defect — recorded so the ordering is not mistaken for an omission.

## Checked and judged still true — with the reason

- **`packaging`: "The bound cannot be retrofitted … says nothing about `17.0.0`–`17.1.1`."**
  Exactly right and needs no widening: `17.1.2`, `17.2.0`, `18.0.0` and `18.1.0` are all bounded,
  so the unbounded set is still precisely those four.
- **`packaging`: "A second published line makes the branch component load-bearing and wrong."**
  History plus a standing hazard; a third version on this line does not touch it. This is also the
  requirement task 1.4 confirmed **originated here** and was correctly not carried a second time.
- **`site-closures` and `public-holidays`.** Neither implies a line, a version, or a future tense.
  Both are written in the flat present, carry no version literal, and so survive their own
  publication — the intended property rather than luck.
- **`CHANGELOG.md`'s `18.0.0` entry: "the two lines ship the same features."** A reader might
  expect a finding. It is a past entry that must not be edited, it was true when written, and
  `18.1.0` has made it true again in any case.
- **The version anchors**, and the history statements in `bookings` ("the first in a release after
  17.0.0") and `booking-management` ("added after `0.1.0` and never published"). All correctly
  unmoved.
- **`README.md`, `docs/configuration.md`, `docs/backoffice.md`.** Present-tense descriptions of
  shipped behaviour, no line qualifier, no denial of either capability.
- **The other 21 specs.** Their `publish`/`published` hits all concern *published contracts and
  ports*, not the feed, and are unaffected by a push.

## Noted, pre-existing, not created here

`packaging` says the Umbraco Marketplace **lists** uBookIt, while `docs/publishing.md` says the
submission **has not been made**. One of those is wrong and `18.1.0` did not cause it. Left alone
deliberately: a release is the wrong place to resolve a question that needs somebody to look at
the Marketplace, and guessing which sentence to edit would replace an inconsistency with a
falsehood. **Carried to the deferred obligations** so it is resolved deliberately rather than
discovered a third time.
