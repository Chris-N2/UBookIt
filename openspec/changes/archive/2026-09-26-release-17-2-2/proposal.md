## Why

Three reasons. The second and third are why this is a release rather than a commit:

1. **The standing tidy patch.** Three obligations are open, and none of them changes a public
   signature:
   - `docs/publishing.md` states the Marketplace library delisting as unseen. That is false since
     2026-09-25.
   - A guard's failure message describes the wrong fault in one of its two directions.
   - A settings value longer than the store holds is accepted by validation, then fails at SQL
     Server instead of being refused.
2. **The first real release through `publish.yml`.** `trusted-publishing` merged and was archived
   with dry runs green on both lines. A dry run never reaches the token exchange, the nuget.org
   policy's scope and glob, or approval in the `release` environment. Only a pushed tag does, and
   the manual API-key fallback can't be retired until that has happened on both lines. Chris's
   last 30-day key expires around mid-October, so this has to happen before then.
3. **The README is the Marketplace's front page, and it doesn't read like one.** It was built up
   change by change, and every QA round added a caveat to its bullet. The result is accurate
   sentence by sentence, but it's written for a reviewer, not for someone deciding whether to
   install. Before a visitor learns what uBookIt does or sees a screenshot, they get a permissions
   caveat, the API-promise blockquote and a whole section on version numbers. The readme is frozen
   per version, so a rewrite reaches the Marketplace only with a release, and this release is the
   cheap moment. Folded in 2026-09-26, on Chris's approval.

## What Changes

1. **`docs/publishing.md`, the Marketplace sentences.**
   - The *Status* bullet "The Umbraco Marketplace still lists three libraries … has not yet been
     seen" is rewritten as observed. On 2026-09-25, after the rescan, only `UBookIt` was listed,
     showing the 18.x line and support for Umbraco 17 and 18.
   - The *After the push* sentence "has **not been observed**. Check … before saying they are
     gone" gets the same treatment. It keeps its instruction to check the live page, because the
     observation is one event and not a rule the Marketplace promises.
   - "Until `17.2.1`/`18.1.1`" and "up to `17.2.1`/`18.1.1`" (lines 181 and 524) are re-read.
     Both versions are live now, so they are expected to stand. That is to be verified, not
     assumed (QA NIT from `marketplace-listing`).
2. **The bound guard's message.** `The_bound_admits_this_major_and_excludes_the_next` gets one
   wrong ceiling message: "A ceiling inside Umbraco N refuses releases this line supports". It is
   true only for a ceiling that is too low. A ceiling that is too high admits the next Umbraco
   major, which this line was never built against, and the message should say that. **Detection is
   unchanged.** Only the text of the failure changes, and each direction is proved to produce its
   own text.
3. **A setting value longer than the store holds is refused with a 400.** The store's column is
   `nvarchar(2048)`, and neither `SettingValidation` nor the controller checks length. So a value
   that passes validation (a long privacy link, or any `Text` setting) reaches SQL Server and fails
   there. The existing requirement *"A value the screen stores is validated when it is written"*
   already says constraints are validated, so this is conformance, and it is pinned by an ADDED
   requirement. **This narrows `SettingValidation.IsValid`'s public behaviour, with no signature
   change**, the same shape that `17.2.1`'s (c) was ruled patch-eligible on. Nothing that could be
   stored before is refused now. **What is measured is each stored row, not the submitted text.**
   A recipient list is stored one row per address, so a list longer than 2048 characters in total
   works today and must keep working. (This was found during apply; the first plan measured the
   whole string and would have broken it.) **Ruled patch-eligible by Chris, 2026-09-26.**
4. **The version: `17.2.1` → `17.2.2`**, in every place `docs/publishing.md` step 3 lists. That
   list is the authority. A `17.2.2` changelog entry, undated until live. *What you have to do:*
   **nothing**.
5. **Release through the workflow.** Merge by PR, push the tag, read one `.nuspec` from the run's
   `packages` artifact, `curl` the tagged screenshot, then approve in `release`. Then record:
   - what the run summary said per package and per symbol package;
   - **the exact text nuget.org's CLI returned for any 409 on a `.snupkg`**, which is an open
     obligation from `trusted-publishing`.

   If the run fails, the documented fallback is used and the failure is recorded as a finding.
6. **The README rewrite** (design D7). It is an editorial change: every claim it makes stays true,
   and nothing it currently guarantees a reader is dropped silently.
   - **Product first:** what uBookIt is and who it's for, the booking-flow screenshot, then install,
     with the section-grant step kept but shorter.
   - **"What it does"** becomes one-sentence bullets grouped for **visitors**, **staff** and
     **developers**. The caveats move to the linked docs, which already carry them. The defaults a
     site must know before installing stay in the README: the delivery API is off, emails are off,
     approval is optional, and settings need a grant nobody has.
   - **Versioning** stays, shorter and lower down. Its pinned sentences stay.
   - **The accessibility section** stays nearly word for word. Its boundary-naming is the
     differentiator.
   - **"What it does not do yet"** stays, in plainer words.
   - **New: "How it's built."** It states the workflow the package is made with: a written spec
     before any code, an independent adversarial review, CI that fails if any suite didn't run, and
     releases published only after approval. It links to the specs. Every sentence in it has to be
     true of the process as it stands, so it avoids "every change since the start".

   The same rewrite goes to `dev/v18` in `release-18-1-2`, with that line's version and
   Requirements row. **The v18 README is the one the Marketplace shows.**
7. **Close-out.** After publication, the *Status* bullet "No release has gone through the
   publishing workflow yet" is false for this line and still true for 18. It gets rewritten to say
   exactly that. Removing the fallback waits for `18.1.2`.

A **patch**: no signature, schema or migration change. The one behavioural narrowing is (3).

## Non-goals

- **`18.1.2`.** It gets its own change on `dev/v18`, in the same sitting where possible. Items 1–3
  are on that line too, so it carries all three. A fix that reaches one line only has happened
  three times, and the close-out records what it must carry.
- **Removing the manual API-key fallback** from `docs/publishing.md`. That is a separate change,
  after a real workflow release has succeeded on **both** lines.
- **Hiding the Settings view from users without the settings verb.** It's a navigation change
  across view manifests, and the population is every view, not one. It gets its own change.
- **Classifier changes for the 409 wording.** If the observed text differs from `already exists at
  feed`, the step fails closed by design. The new wording is added to the classifier in a change,
  not patched in here.
- **Rewriting anything under `docs/`.** The README moves caveats *to* the docs pages, which
  already carry them. Where a caveat turns out to exist only in the README, it's added to the
  right docs page, and nothing else there changes.
- **The telephone-booker gap from ㊳ and `main`'s packaging debt.** These are feature and packaging
  work, not tidying.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `privacy-notice`: a MODIFIED requirement, *A usable policy link is defined once, and means the
  same on every host* (QA round 1, MAJOR: the change falsified it). Its italic note described the
  over-long write as a "known gap, not yet addressed" that fails at the database. It now points at
  the `site-settings` refusal. **Guarantees diffed:** all seven scenarios and every SHALL carry
  forward word for word except one, which is **deliberately narrowed**. "The screen's refusal
  SHALL name both accepted forms" now applies to a value *within the store's capacity*. An
  over-long link is refused for its length and states the limit instead, because naming the two
  forms there would tell the editor the wrong thing to fix. Nothing that was refused with the
  forms message before is affected: an over-long value used to fail at the database, with no
  message at all.
- `site-settings`: an ADDED requirement. A submitted value longer than the store can hold is
  refused, naming the limit, and is never passed to the store. The existing validation requirement
  is not rewritten.

## Impact

- `src/UBookIt.Persistence/UBookItDbContext.cs`: the literal `2048` becomes an `internal` constant,
  visible to `UBookIt.Backoffice` through the existing `InternalsVisibleTo`. The schema is
  unchanged, so there is no migration.
- `src/UBookIt.Backoffice/Settings/SettingValidation.cs`: the length check.
- `tests/UBookIt.Tests/PackageCompositionTests.cs`: the message.
- Tests: unit tests for the boundary, and an integration test through the real endpoint against
  SQL Server.
- `Directory.Build.props`, `README.md` (rewritten, see item 6), `docs/publishing.md`,
  `CHANGELOG.md`.
- README pins in `VersionTruthTests`, `DeliveryApiDocumentationTests`,
  `NotificationDocumentationTests`, `ChangelogTests` and `RetiredClaims`. Each pin is kept or
  re-judged on its own, never relaxed to make the suite pass.
- Five packages at `17.2.2` on nuget.org, frozen at push. **The first published by the workflow.**
