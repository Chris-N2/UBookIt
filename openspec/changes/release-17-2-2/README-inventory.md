# README rewrite: inventory (design D7)

The rewrite is checked against this list, not against memory. Each actionable claim in the README
as it stood at `d6f0199` is marked **kept**, **moved** (to a named docs page, quoting the line that
already says it) or **dropped** (with a reason).

## 1. Guards that read `README.md` (task 3R.1)

The list is taken from the test code, not from grep counts.

| Guard | What it needs from the README | Status after rewrite |
|---|---|---|
| `VersionTruthTests.Every_documented_version_is_the_declared_version` | every "uBookIt is at \`X\`" names `<Version>` | kept: one occurrence, under *Versions and the API promise* |
| `VersionTruthTests.ClaimsAreFoundWhereTheyAreKnownToLive` | README states a version in a known phrasing | kept (same sentence) |
| `VersionTruthTests.TheVersioningPolicyIsStated` | five phrases: "The major tracks the Umbraco major"; "the major is not a breaking-change signal, and this is where uBookIt departs from Semantic Versioning"; "it lands in a minor release"; "ships with sensible defaults or a documented upgrade path so a site that already works keeps working"; "A patch release never carries a breaking change" | kept, each verbatim (wrap-safe) |
| `VersionTruthTests.The_pre_release_framing_is_gone` | must NOT say "the public API may still move" / "treat contracts as settled from 1.0.0" | kept absent |
| `VersionTruthTests` publisher check | every mention beginning "Norwood" is the full declared name | kept: two mentions, both "Norwood Design & Development Ltd." |
| `VersionTruthTests.The_readme_links_resolve_from_anywhere` | every link and image absolute `https`; every `blob/` link resolves to a file; at least one does | kept. New `blob/` links: `CLAUDE.md`, `docs/configuration.md` (Documentation list), `docs/publishing.md`. One new external link, OpenSpec (returned 200 on 2026-09-26) |
| `VersionTruthTests.The_readme_images_render_and_show_what_their_release_shipped` | images on an allowed host, pinned to `<Version>`, resolving to a file, with described alt text | kept: the same four images and alt texts, pinned to `17.2.2`; `booking-flow.png` moved to the opening |
| `VersionTruthTests.The_readme_documentation_links_name_the_release_they_shipped_with` | every `blob/` link pinned to `<Version>` | kept: all pinned to `17.2.2` |
| `VersionTruthTests.Every_mention_of_the_feed_is_accounted_for` | README has no registered feed mention, so any new one fails | kept: none added ("published separately" matches no vocabulary pattern; it was there before) |
| `DeliveryApiDocumentationTests.Both_documents_state_the_api_is_off_by_default` | "The API is off by default" | kept, in *Off until you turn it on* |
| `DeliveryApiDocumentationTests.The_no_ddos_claim_boundary_is_stated` | "uBookIt makes no DDoS-protection claim" | kept. **Re-judged and kept deliberately**: its test records it as Chris's concern that "the README is where the concern arrives", so it is not QA voice to strip |
| `NotificationDocumentationTests` shipped-markdown sweep + `RetiredClaims` | no retired needle appears in README | kept: no needle's wording is reintroduced; run green |
| `ChangelogTests` | reads CHANGELOG, not README; only its failure message names README | unaffected |
| `PackageCompositionTests` | the readme is declared and packed in every package | unaffected (same file) |

**Blind spot found, recorded rather than fixed here.** Neither link guard checks a
`github.com/<owner>/<repo>/tree/<ref>/…` link: both skip anything outside the `blob/` prefix, so a
directory link pinned to `main` would pass. The rewrite uses no `tree/` link, and D7.6 said to link a
file instead. The guard gap is a candidate obligation, not part of this patch.

## 2. Claims (task 3R.2)

| # | Claim in the old README | Status | Where now |
|---|---|---|---|
| 1 | It's for Umbraco 17 | kept | opening line |
| 2 | Configure, publish a page and take bookings without code | kept | opening ("You set up what can be booked in the backoffice …") |
| 3 | Visitors can book with JavaScript turned off | kept | opening, and *For the people booking* |
| 4 | Your site is told when a booking is placed or cancelled | kept | *For developers*, notifications |
| 5 | `dotnet add package UBookIt`; schema installs on first boot | kept | *Get started* |
| 6 | The section is hidden until granted; the path; grant deliberately | kept | *Get started* |
| 7 | Sensitive data governs name and email; only the original super user at first; new admins see them hidden | kept | *Get started* |
| 8 | uBookIt is at X; the API is a promise; changes are deliberate, named first, never in a patch; the changelog leads with what upgrading asks | kept | *Versions and the API promise* |
| 8a | "`17.1.0` itself added members to five published interfaces" (illustration) | **dropped** | Illustrative history. The `17.1.0` changelog entry records it ("Five published interfaces gained members", `CHANGELOG.md`) |
| 9 | Versioning policy and table | kept | verbatim pins and table |
| 9a | "the public interface is kept as consistent as possible, and additions are preferred to changes" | kept | *Versions and the API promise*. Dropped in the first draft without being listed; restored in QA round 1 |
| 10 | Requirements table (Umbraco, .NET, SQL Server) and the SQLite reason | kept | *Requirements*, table row unchanged |
| 11 | Resources: hours, exceptions, min/max duration, capabilities | kept | *For your staff* |
| 12 | Services from roles; uBookIt finds free combinations | kept | *For your staff* |
| 13 | Closures: one list, every resource including later ones, precedence over hours and exceptions, open anyway, grants for editing and opting out | capability **kept**; mechanics **moved** | `docs/backoffice.md` §Closures: "A closure closes every resource for that date, including resources created after it, and it wins over both a resource's weekly hours and its own date exceptions"; "Open anyway" tick; grants table |
| 14 | Public holidays: your code supplies the source, no data shipped; fetch and tick; ordinary closure; nothing scheduled; no source means no control | "no data" and "your code" **kept**; the rest **moved** | `docs/backoffice.md` §Bringing in public holidays: "If it does not, the panel is not there"; "Nothing is ever imported on its own. There is no schedule and no startup job" |
| 15 | Booking page like any page; catalogue, service or resource; site layout | kept | opening and *For the people booking* |
| 16 | Delivery API off by default; halves separately enabled; anonymous; volume protection is the host's; no DDoS claim | kept | *Off until you turn it on* |
| 16a | "cannot know who is calling (no header check or CORS …)"; the breaking change for existing API users | **moved** | `docs/delivery-api.md`: "the API cannot know who is calling" and "it is a breaking change for existing API consumers" (both pinned there by `DeliveryApiDocumentationTests`) |
| 17 | Bookings: see, cancel, move, confirm or decline; per-group permissions, three grants | capability **kept**; grant names **moved** | `docs/backoffice.md` permissions table: See bookings / Act on bookings / Configure resources and services |
| 18 | Find by reference (needs See bookings) or email (needs Sensitive data too) | capability **kept**; grant rule **moved** | `docs/backoffice.md` §finding a booking |
| 19 | Telephone booking; notice and horizon waived; needs Act on bookings and Sensitive data | capability **kept**; rules **moved** | `docs/backoffice.md` §Recording a booking somebody made by telephone: "You need two things to see it", with the horizon exception |
| 20 | Self-service cancellation: off by default, needs booker emails, the link is the credential | kept | *Off until you turn it on* |
| 21 | Settings screen; configuration-only settings shown read-only; needs Change site settings, which nobody has, administrators included | kept, apart from the read-only detail, which **moved** | `docs/configuration.md`: "Shown on the settings screen so you can see what is in effect, never editable there" |
| 22 | Approval: `AutoConfirm` on by default; off, bookings wait and hold their time | choice **kept**; setting name and hold **moved** | `docs/configuration.md` (`UBookIt:AutoConfirm` row); `docs/backoffice.md`: "A requested booking waits for you, indefinitely", and the statuses "that hold their time" |
| 23 | Emails off until asked; site-wide list plus responsible users and groups; the mail-server line | kept | *Off until you turn it on* |
| 24 | Notifications on placed, confirmed, declined, cancelled | kept | *For developers* |
| 25 | Restyle with custom properties; theme with an RCL | kept | *For developers* |
| 26 | Four screenshots with captions | kept | first in the opening, three under *What it looks like* |
| 26a | First screenshot's caption: the header, navigation and typeface are the site's; uBookIt supplies markup and one stylesheet and sets no text colour | **dropped** | Duplicated: "in your own layout" in the opening; "sets no text colour of its own" in the accessibility section |
| 27 | Accessibility claim and boundary | kept | nearly verbatim, same link and fragment |
| 28 | Four not-yet items | kept | all four, in plainer words |
| 29 | Documentation list | kept, plus *Configuration* | the list now links `docs/configuration.md` too. The booking-page entry's "the deployment note about committing the installed template" was shortened to "deployment" in the first draft, which weakened an actionable trap. It was restored in QA round 1 |
| 30 | Packages table; Backoffice without Web fails silently; install `UBookIt` | kept | *The packages* |
| 31 | Licence and publisher | kept | verbatim |

**D7.2's must-stay list, every item kept:** SQL Server/SQLite (5, 10), versions (10), section grant
(6), Sensitive data and new admins (7), API off (16), emails off (23), Settings grant nobody has
(21), install `UBookIt` (30), link is the credential (20), accessibility boundary (27).

## 3. "How it's built": the evidence for each sentence (task 3R.4)

| Sentence | Evidence |
|---|---|
| Built with AI assistance under a spec-driven workflow | `CLAUDE.md` invariant 6; Chris, 2026-09-26, asked for it to be said |
| Every change starts as a proposal and task list, with a design where needed, in OpenSpec; no code until approved; the requirements are in the repository (linked: `openspec/specs/site-settings/spec.md`). **Round 1:** "design" dropped from the universal claim, because `archive/2026-09-23-release-17-2-0/` has none | `CLAUDE.md` invariant 6, "No code without an approved change"; `openspec/changes/` |
| A separate agent that didn't write the change reviews it adversarially against its spec; it can reject; findings go back through implementation; it never fixes code | `CLAUDE.md` invariant 6 and *How QA actually runs*: "QA never fixes code itself", "A REJECT is a gate"; `.claude/skills/qa-review` |
| Every push to a release line is built and tested on Linux against SQL Server; the run fails if any .NET test project didn't report or any of its tests was skipped | `.github/workflows/ci.yml` (push to `main`/`dev/v18`); `docs/publishing.md` *Continuous integration*: Linux, SQL Server started for the run, `Assert-TestResults.ps1` fails "if any test project on disk did not report or any test was skipped". **Round 1 corrected two overclaims.** (a) "any test suite": the check reads TRX files from `tests/*.csproj` only, and the client suite (`npm test`, vitest) fails only on zero test files, so an `it.skip` passes CI. It is now scoped to .NET test projects. (b) "every commit": a push runs once, on its head, so it now says "every push". |
| The suite checks the version, the versioning policy, the API default, and every link into the repository | §1 above: `Every_documented_version…`, `TheVersioningPolicyIsStated`, `Both_documents_state_the_api_is_off_by_default`, and the link guards for `blob/` links. Scoped to "into the repository" because external links are unchecked |
| Releases are published by a workflow that packs the tagged commit on a clean CI runner, verifies the packages there, and publishes nothing until the maintainer approves; a manual route is documented as the fallback | `docs/publishing.md` *Publishing a release* (clean clone, `Assert-PackedRelease.ps1`, the `release` environment's required reviewer) and *Fallback: pushing by hand*. **Round 1 found the previous evidence false.** It said the fallback "pushes the same run's artifact by hand", but the runbook's fallback pushes a local pack (`src/**/bin/Release/*.nupkg`), and only this change's D5 meant the artifact. The sentence now names the fallback instead of claiming every release goes through the workflow. |

No history ("since …"), counts, or model name.
