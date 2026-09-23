# The sibling-falsification sweep

What publishing `17.2.0` makes untrue elsewhere. Run over all 24 specs and the five shipped
documents, after the packages were live rather than before — a release falsifies sentences by
*happening*, so the sweep has to look at a world where it has.

## Checked

All 24 specs: `availability`, `booker-erasure`, `booking-emails`, `booking-management`,
`booking-retention`, `bookings`, `default-frontend`, `delivery-api`, `email-templates`,
`packaging`, `permissions`, `persistence`, `privacy-notice`, `public-holidays`,
`resource-management`, `resources`, `responsibility`, `self-service-cancellation`,
`sensitive-data`, `service-booking`, `services`, `site-closures`, `site-settings`, `theming`.

Shipped documents: `README.md`, `docs/publishing.md`, `docs/configuration.md`,
`docs/backoffice.md`, `CHANGELOG.md`.

## Found, and fixed here

**The whole cluster is in `docs/publishing.md`, and it is the same defect this release was created
to guard against: unguarded prose stating what a version bump touches.** Pinning the readme's
documentation links (task 2b) added a sixth thing that moves with the version, and the checklist
describing what moves did not know about it. Left alone, the next release reads a table that
omits twelve addresses and ships them stale — which is how `17.0.0` happened.

| Sentence | Why publication broke it |
|---|---|
| "Three documents state a version … `README.md` in two places" | Three places now: the sentence, the screenshot URLs, and the documentation links. **Fixed** to three. |
| The bump table itself | Had no row for the documentation links. **Fixed** — a row added. |
| "the first four … `VersionTruthTests` for the README and runbook sentences and the screenshot URLs" | A fifth guarded thing exists now (`The_readme_documentation_links_name_the_release_they_shipped_with`). **Fixed** to five, and the guard named. |
| "The packed readme's screenshots are addressed to a git tag" | Its links are too, as of this release. **Fixed**, and the section now says why the link case is worse: an unpinned image shows a stale screenshot, an unpinned link sends a reader to another line's documentation. |
| "Until it does, every image on the package page is a broken image" | Every documentation link is dead too. **Fixed**. |
| "And the screenshot URLs move with the version" | True of twelve links as well. **Fixed** to cover both, naming the half-done state as the thing to avoid. |
| "Open the package page and look at the screenshots" | The only check that sees what a consumer sees, and it was images-only while the release actually verified sixteen addresses. **Fixed** — and this one is PINNED by `The_publishing_runbook_states_what_cannot_be_undone`, so the guard failed on the edit and the pin was moved deliberately rather than the sentence being left alone. |

## Found, and its remedy is not an edit

**`README.md`: "For Umbraco 18, install uBookIt `18.x`."**

Read against the feature list two sections below — which now advertises site-wide closures and the
public holiday import — this is misleading as of today. The only released `18.x` is `18.0.0`, and
it carries neither. A reader on Umbraco 18 following that sentence gets a package without the two
features the page just sold them.

**Editing `main`'s README does not fix it.** `17.2.0`'s readme is frozen inside the package with
that sentence already in it; nothing can change what that page says. The mismatch exists from the
moment `17.2.0` published until `18.1.0` does, and **shipping `18.1.0` is the only thing that ends
it.** That makes the 18 release a correction rather than merely the next item, and the window is
however long it takes.

Recorded rather than papered over, and carried into `release-18-1-0` as the reason it is urgent.

## Checked and judged still true — with the reason, since each looks like a finding

- **`packaging`: "so this requirement binds releases from here on and says nothing about
  `17.0.0`–`17.1.1`."** Exact, and does NOT need extending. `17.1.2` introduced the host bound and
  `17.2.0` packed `[17.6.2, 18.0.0)` — verified in the nuspec at task 4.4 — so the unbounded range
  is still precisely those four versions.
- **`packaging`: "With two lines published against two different Umbraco majors…"** and the
  Marketplace-claim sentence. Publishing a sixth 17-line version changes neither: still two lines,
  and the Marketplace claim concerns the frozen `17.0.0`–`17.1.1` metadata.
- **`packaging`: "five packages carrying separately-specified icons can drift apart silently."**
  Still five; this release added no package id.
- **The two anchors** — `## Status`'s "the first release is `17.0.0`" and the API-stability
  sentence. Unaffected by design, and task 1.4 forbids moving them.
- **`README.md`: "from `17.1.2` the packages say so"**, and "`17.1.0` itself added members to five
  published interfaces". Both history. Task 1.6 left line 58 deliberately and that was right.
- **`README.md`'s *What it does not do yet*.** All four items survive closures and holidays; no
  decayed negative sentence.
- **`site-closures` and `public-holidays`.** Searched for "unreleased", "forthcoming", "will
  ship", "not yet available": nothing. Both are written in present-tense SHALL form, so
  publication falsifies no sentence in either — which is the intended property, not luck.
- **`docs/configuration.md` and `docs/backoffice.md`.** Both describe shipped behaviour in the
  present tense already, including "ships no holiday data for any country".
- **`packaging`'s "The documentation a consumer reads does not deny what the package does".** Swept
  the shipped markdown by hand for a sentence denying closures or the import: none. The `18.x`
  finding above is a *misdirection about which line carries the feature*, not a denial that the
  package has it, so it falls outside this requirement rather than triggering it.

## Not a finding: the unsynced delta

The sweep flagged that `packaging` does not yet require what `17.2.0` shipped — the new
documentation-link requirement and the new-interface scenario live only in this change's delta.
That is task 6.3 and is the next step, not a defect. Noted so a reader of this record does not
mistake the ordering for an omission.
