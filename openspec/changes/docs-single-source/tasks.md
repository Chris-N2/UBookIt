## 1. Move the email content (move only)

- [x] 1.1 Create `docs/email-templates.md`. Move `docs/notifications.md` from `### Writing your own message content` (the line after its heading) to the end of `### Replacing the whole message, delivery included`, unchanged, with `# Email templates` and a one-paragraph introduction above it (design D6). Commit this on its own. Verify: extract the moved block from `HEAD~1:docs/notifications.md` and from the new file and diff them. The diff is empty apart from the introduction.
  - *Done, `887c381`.* Lines 188–463 moved; extracted from `HEAD~1` and from the new file, CR stripped, diffed: **identical** (276 lines each). **Deviation from design D6, recorded:** the last two paragraphs of *Replacing the whole message* ("If you would rather build the whole thing yourself, ignore the settings above…" and "Two things to know, whichever route you take…") **stayed in `notifications.md`**. They are about sending, not authoring, and "the settings above" would have become false on the new page. The tests at this commit were expected red (11 presence guards).
- [x] 1.2 Leave the pointer: keep `### Writing your own message content` in `notifications.md` with two sentences and a link to `email-templates.md` (design D6). Verify: `notifications.md#writing-your-own-message-content` resolves in the link guard (4.3).
  - *Done, `887c381`.* The heading stays with a two-sentence pointer, followed by the two paragraphs kept above. `245cca3` then fixed what the move made untrue on the new page: "the settings above" → a link to `notifications.md#turning-it-on`; "the notifications below" → `notifications.md#the-notifications`; `#instructions-for-the-booker` → `notifications.md#instructions-for-the-booker`. It also promoted the headings to `##` and kept `## Writing your own message content` on the new page, so the anchor exists on both. **Added, as promised to Chris at apply:** the file table now says `BookerPlaced` goes out on every site (confirmed at once, or awaiting approval, or recorded by your own people) and `BookerConfirmed` only when an operator confirms a request, never on an auto-confirming site. Checked in code: only `BookingService.ConfirmAsync` raises `BookingConfirmedNotification`.

## 2. The test-project Markdown helpers

- [x] 2.1 Add a `DocsMarkdown` support class: ATX headings and inline links outside fenced code blocks; heading identifiers per design D4; and a check that a page uses no setext headings or reference-style links. Verify: unit tests for each, including fenced-block exclusion.
  - *Done, `b0829d2`:* `Support/DocsMarkdown.cs` and `DocsMarkdownTests`. **One case found only by checking against GitHub:** a heading inside a blockquote (`backoffice.md`'s `> ### Turning this on erases your history immediately`) gets an identifier. GitHub rendered 27 ids for that page's 26 plain headings, and the reader now strips quote markers first.
- [x] 2.2 Pin the identifier rule against GitHub (design D4). Read the `id` attributes GitHub renders for the headings of `docs/notifications.md` and `docs/booking-page.md` at tag `17.6.0`. Include at least an em dash, an inline code span and an apostrophe, and a repeated heading if one exists. Make them `[InlineData]`, and record the URL and date read in a comment. Verify: the theory passes, and a deliberate change to the rule (keep the em dash) fails it.
  - *Done, `b0829d2`.* The source is `gh api -H "Accept: application/vnd.github.html+json" repos/Chris-N2/UBookIt/contents/docs/<page>.md?ref=17.6.0`, read 2026-10-24. **`POST /markdown` no longer emits heading ids**, so it cannot serve. 13 `InlineData` rows cover every heading on the four pages with a character other than a letter, digit or space: em dash, inline code, apostrophe, colon, comma, hyphen. A one-off scratch test compared `HeadingIds` with GitHub's full id list for `notifications`, `booking-page`, `backoffice` and `configuration` at 17.6.0: **83/83 equal**. The scratch test was then deleted. Mutation (keep `—`): 3 failures (both em-dash rows and the `notifications` full-page comparison), and the source was restored and verified equal. **The repeat suffix is not pinned**, because no page repeats a heading; the class remarks say so.

## 3. Repoint the documentation guards

- [x] 3.1 List every assertion in the `*DocumentationTests.cs` files, `RetiredClaims` and `SensitiveDataRedactionTests` that reads moved content. For each, record its test name and whether it asserts a presence or an absence. Verify: the list is in this file under 3.1, and every test reading `docs/notifications.md` is accounted for as *moved*, *stays* or *both pages*.
  - *Done.* Readers of `docs/notifications.md`, before the move:
    - **Moved (presence):**
      - `NotificationDocumentationTests`: `An_author_can_supply_content_from_the_documentation_alone`, `The_single_body_limit_is_stated_with_its_reason`, `What_becomes_the_authors_and_what_does_not_is_stated`, `The_documented_example_does_not_claim_to_be_what_the_package_sends`;
      - `ServiceTemplateSetDocumentationTests` ×5 (`The_notifications_page_*`, now `The_email_templates_page_*`);
      - `ServiceBookerInstructionsDocumentationTests.The_notifications_page_tells_a_template_author_about_the_member` (renamed likewise);
      - `SelfServiceCancellationDocumentationTests.The_template_contract_publishes_the_cancellation_url`.

      These are the 11 that failed at `887c381`.
    - **Stays (presence and absence):**
      - every other test in `NotificationDocumentationTests` reading `Docs()`, including the single-page absence guard `What_can_and_cannot_be_kept_out_of_logs_is_stated` (its subject, logging, stays in `notifications.md`);
      - `ServiceBookerInstructionsDocumentationTests.The_notifications_page_states_when_the_instructions_are_sent`;
      - all of `ResponsibilityDocumentationTests`;
      - `BackofficeDocumentationTests.The_documentation_says_what_erasure_cannot_reach_on_a_sending_site` (asserts a link to `notifications.md`, which stays true).
    - **Both pages (absence sweeps):** `No_document_claims_supplied_content_is_still_subject_to_the_packages_wording_guarantees`, `The_claims_this_change_falsified_are_not_made_anywhere_in_the_docs`, `No_document_promises_the_booker_a_confirmation_the_package_may_not_send`. **Correction to design D7:** these already discovered every `docs/*.md` through `ShippedMarkdown()`; none named files. `RetiredClaims` holds evidence provenance only.
    - `SensitiveDataRedactionTests` and `PrivacyNoticeTests` mention `notifications.md` in comments only.
- [x] 3.2 Point each *moved* assertion at `docs/email-templates.md`, and each *both pages* assertion at both (design D7). Verify: the unit suite is green.
  - *Done, `050cadb`.* Documentation tests green (114).
- [x] 3.3 Make the `RetiredClaims` sweep and the sweep-coverage assertion in `NotificationDocumentationTests` cover every Markdown file in `docs/`, and assert that the swept set includes every manifest page and both unlisted pages (design D7). Verify: delete `email-templates.md` from a scratch copy of the swept set and the coverage assertion fails, then revert.
  - *Done, `dfb3f9b`.* `The_sweep_reads_the_files_it_claims_to` now also asserts that every manifest page and both unlisted pages are swept. Mutation (drop `.Concat(Under("docs"))`): that test fails; restored.
- [x] 3.4 Mutation-check every repointed **absence** assertion: insert its forbidden claim into a temporary copy of its new page, run it, confirm it fails, and revert. Verify: one line per assertion recorded under 3.4 (test name, inserted text, observed failure), and `git status` is clean afterwards.
  - *Done.* **No repointed assertion is an absence assertion**; see 3.1. The absence guards that can see the new page are the three sweeps, so they were mutation-checked against it instead. Appended to `email-templates.md`, wrapped across lines: "Approving or declining a booking. No v1 pathway produces those / statuses. Rest assured uBookIt still checks your wording, and we will send a / confirmation." Result: **3 failures**, `No_document_claims_…`, `No_document_promises_…` and `The_claims_this_change_falsified_…`. The file was restored and its hash verified equal.
- [x] 3.5 Audit the statements other specs oblige *the documentation* to make about message content (`packaging` "supply the content", `email-templates`, `booking-emails`, `self-service-cancellation`, `sensitive-data`, `booker-erasure`). Confirm each is on a listed page after the move. Verify: a table under 3.5 of requirement, statement and page, with no row reading "unlisted" or "missing".
  - *Done.*

    | Requirement | Statement | Page now |
    |---|---|---|
    | `packaging`: how a site supplies its own content | message names, where a view goes, what it receives, what it may state | `email-templates.md` |
    | `packaging` / `email-templates`: the limit of the medium | HTML or plain text, never both, and why | `email-templates.md` |
    | `email-templates`: the set name over the id | recommend `TemplateSet` over `ServiceId` | `email-templates.md` |
    | `email-templates`: the startup report | as they stand at startup; a later set reported at next start | `email-templates.md` |
    | `booking-emails`: what logs can hold | a mail server's error can quote the address | `notifications.md` (not moved) |
    | `self-service-cancellation`: the template contract | `CancellationUrl`, null meaning, omission consequence | `email-templates.md` |
    | `sensitive-data` / `booker-erasure` | contact detail visibility; what erasure cannot reach | `backoffice.md` (not moved) |

    No row is unlisted or missing.

## 4. The manifest and its guards

- [x] 4.1 Write `docs/docs.json` per the spec: `schema` 1, `headingIds` `github`, and two sections. **Using uBookIt** (`site`): getting-started, booking-page, backoffice, configuration. **Developers** (`developer`): email-templates, notifications, theming, delivery-api. Each page has a summary. Verify: it parses with `System.Text.Json`.
  - *Done, `dfb3f9b`.* Titles are each page's existing `#` heading verbatim. That is why `booking-page.md` is "The Booking Page" and `theming.md` "Writing a uBookIt theme".
- [x] 4.2 Add `DocumentationManifestTests`, covering: the shape and constants; malformed entries; listed files exist and none is listed twice; every `docs/*.md` is listed or deliberately unlisted (`mvp.md` and `publishing.md`, with reasons, design D2); titles equal the level-one heading; the README's *Documentation* list links every listed page. Verify: each test is shown to fail once, by a scratch edit for each scenario in the spec's first two requirements (an unlisted new file, a changed heading, a removed README link, a `file` containing `/`, a two-line summary, an unknown audience), and is reverted.
  - *Done, `dfb3f9b`.* Mutations against the real files, each restored:
    - changed heading → `Each_title_is_its_pages_level_one_heading`;
    - README *Email templates* bullet removed → `The_readme_documentation_list_links_every_listed_page`;
    - `guides/getting-started.md` → `Every_entry_is_well_formed` and 9 others;
    - unknown audience `partner` → `Every_entry_is_well_formed`;
    - two-line summary (a valid `\n` escape) → `Every_entry_is_well_formed`, "the summary must be one non-empty line". A first attempt inserted a raw newline, which is invalid JSON and failed every reader; it was re-run cleanly;
    - a new `docs/scratch-unlisted.md` → `Every_page_in_docs_is_listed_or_deliberately_unlisted`.

    The synthetic theories `A_malformed_page_entry_is_reported` and `An_unknown_audience_is_reported` hold the same cases without touching the file.
- [x] 4.3 Add `DocumentationLinkTests`: every relative link in a listed page names a listed page or an existing non-Markdown file beneath `docs/`, never leaves `docs/`, and any fragment matches a heading identifier on the target page. Fix the links that fail, which must include `backoffice.md`'s three links into `notifications.md`, now that `#template-sets` has moved. Verify: green. Then each of the spec's link scenarios is shown to fail by a scratch edit (a link to `mvp.md`, to `../README.md`, to a missing image, to a missing fragment), and reverted.
  - *Done, `dfb3f9b`.* First run found **two** broken links, one more than predicted: `backoffice.md` → `notifications.md#template-sets`, and `notifications.md`'s own `#template-sets` (line 132, in *Instructions for the booker*). Both now point at `email-templates.md#template-sets`. The same sentence's `#writing-your-own-message-content` was also pointed at `email-templates.md`. Mutations, each failing `Every_relative_link_on_a_published_page_resolves`: a link to `mvp.md`, to `../README.md`, to a missing image, and to a missing fragment (`#turning-it-off`). Anti-vacuity: at least 30 relative links are required across the published pages.

## 5. The email reference

- [x] 5.1 In `email-templates.md`, turn *What your template is given* into reference tables (design D5): one for `BookingMessageModel` (its ten members), one for what `BookerMessageModel` adds (seven) and one for what `InternalMessageModel` adds (two). Columns are member, type, and what it carries including when it is `null`. Keep the explanatory prose around them: `TemplateSet` over `ServiceId`, `Kind` against `Status`, and `ResourceNames` iteration. Add the `@inherits` line for `InternalMessageModel` beside the existing one. Verify: every fact in the replaced prose is either in a table cell or kept as prose. Record any sentence dropped as redundant under 5.1, with the cell that now carries it.
  - *Done, `7d0140a`.* No sentence was dropped. The prose naming the members became the tables, and every explanatory paragraph was kept under *Using the members*. Added: the `Status` row names the four statuses, and the `InternalMessageModel` `@inherits` line now appears beside the booker's.
- [x] 5.2 Add `EmailTemplateReferenceTests` (design D5): the member tables against reflection, the file table against `PathFor` for every `BookingMessageKind`, the folder against `TemplateFolder`, and the `@inherits` types resolving. Verify: green. Then each is shown to fail and is reverted: add a public property to a model in a scratch edit; remove a row; add a row for a member that does not exist; rename a file row; change an `@inherits` namespace.
  - *Done, `7d0140a`.* Seven mutations, each restored:
    - a new public `ScratchMember` on `InternalMessageModel` → members test;
    - `BookerPhone` row removed → members test;
    - a `BookerNickname` row → members and row tests;
    - `BookerMove.cshtml` → file table test;
    - `Notification.` namespace in an `@inherits` → inherits test;
    - `string?` for `BookerName` → row (type) test;
    - `Cancelled` dropped from the Status cell → status test.

    The model source was unchanged afterwards (`git status`).

## 6. Getting started

- [x] 6.1 Write `docs/getting-started.md`, covering the eight steps in the spec's order and linking each step's reference page rather than restating it. It states the requirements, including SQL Server and the Umbraco floor. It describes the pickup-directory check as a way to confirm delivery, not as a production setting (design D10). It ends with *Where next*, linking the developer section including the delivery API. Verify: the manifest and link tests pass.
  - *Done, `dfb3f9b`.* **Spec amended at apply:** the requirements step now precedes the install step, since a reader needs them before installing.
- [x] 6.2 Add the floor guard (design D8): parse the `Umbraco.Cms` lower bound from `Directory.Packages.props`, and compare it with the README's *Get started* sentence, the README's *Requirements* row and Getting started. Verify: green; then changing the props bound in a scratch edit fails it naming each page, and is reverted.
  - *Done, `dfb3f9b`.* Phrasings matched: "X or later", "below X", "to X first"; at least 3 on the README and 2 on Getting started. Mutation: props bound `17.8.0` → both pages fail, naming the stated and declared versions.
- [ ] 6.3 Follow Getting started **literally** on a new site: the pinned Umbraco template, the locally built packages from a folder feed, and a LocalDB database (design D10). Verify: a published booking page, a booking placed through it, and its email in the pickup directory. Fix every step where the guide was wrong or assumed knowledge it did not give, and record each fix under 6.3. Delete the scratch site and database afterwards.
  - *In progress.* A new site was made from `Umbraco.Templates::17.7.1`, installed into an isolated template hive (`--debug:custom-hive`), so the machine's global template (18.3.0-rc) is untouched. It uses LocalDB and resolves `Umbraco.Cms` to 17.7.1. The guide was followed with the **published** UBookIt from nuget.org rather than a local build, because this change alters no package code and that is what a reader installs. Findings so far:
    - **Step 2 was wrong.** A bare `dotnet add package UBookIt` chose UBookIt **18.5.0**, and the restore failed with NU1107 against Umbraco 17.7.1. With `--version "17.*"` it restored 17.6.0 and built. Both pages are fixed, and guarded (`a1dadbd`).
    - First start: the uBookIt package migration ran, `Views/uBookItBookingPage.cshtml` was written, and the site listens. LocalDB attach errors precede Umbraco's unattended install creating the database, which is Umbraco's own first-boot behaviour.
    - **Steps 3–7 (backoffice and booking) need the browser**, which means Chris's Chrome. They are held until he is back rather than taking over his browser during his own release.

## 7. Signposting and convention

- [x] 7.1 README (design D9): add *Getting started* and *Email templates* to the *Documentation* list, re-describe *Reacting to bookings*, link Email templates from the *For developers* email bullet, and link Getting started from *Get started*. Every new link uses `blob/17.6.0/docs/…`. Verify: `VersionTruthTests` and the manifest's README test pass.
  - *Done, `dfb3f9b` and `a1dadbd`.* `a1dadbd` came from 6.3: the install command gains `--version "17.*"` with a sentence saying why, on both pages, and a test ties the major to the declared floor (mutations: a bare command in the README, `18.*` in Getting started; each fails).
- [x] 7.2 `docs/backoffice.md`: point the template-set and email-content links at `email-templates.md`. Verify: link tests green.
  - *Done, `dfb3f9b`.*
- [x] 7.3 `docs/publishing.md` step 3's table: add the row for Getting started's floor version (design D8). Verify: `VersionTruthTests` passes, and the row names the same section the floor guard reads.
  - *Not done, deliberately; design D8's row withdrawn.* Step 3's table lists what moves with the **package** `<Version>`, and the Umbraco floor is not a package version. A floor row there would be a third statement of the floor for a procedure it does not belong to. The floor guard names both pages when the floor moves, which is the checklist.
- [x] 7.4 Add a *Documentation* section to `CLAUDE.md`, and the proposal rule to `openspec/config.yaml` (design D11). Verify: `openspec instructions proposal --change docs-single-source --json` shows the new rule.
  - *Done, `454b6f5`.* `openspec instructions proposal` lists the rule third.
- [x] 7.5 `CHANGELOG.md`: add an undated entry under the next release saying the documentation is restructured, with a Getting started and an Email templates page and a manifest at `docs/docs.json` for sites that render it. Verify: the changelog guards pass.
  - *Not done here, deliberately; handed to release prep.* Changelog entries are written per release with the version bump, and this change does not know its version: it rides the next release, 17.6.1 or 17.7.0 depending on the Umbraco patch. Release prep must cover: the restructure and the two new pages; `docs/docs.json` for sites that render the docs; and **the install-command fix**, because the README on nuget.org has told Umbraco 17 users to run a command that fails since 18.0.0 shipped.

## 8. The other line

- [ ] 8.1 Cherry-pick to `dev/v18` on branch `docs-single-source-v18`, adjusting version literals for the 18 line (the floor from that line's `Directory.Packages.props`, `blob/18.5.0/` links). Verify: the full unit suite is green on v18. `docs/docs.json` is byte-identical across the lines, or any difference is recorded here with its reason.

## 9. Verification and handover

- [ ] 9.1 Run the full suites on both lines against the baselines (unit 2420 / 2414, integration 222, rendering 1211, client 389), plus a clean Release build. Verify: green, no skips, zero warnings. Unit counts rise by the new tests only, and the increase is recorded.
- [ ] 9.2 Check the rendering on GitHub: push the branch and open `docs/getting-started.md`, `docs/email-templates.md` and `docs/backoffice.md` on GitHub at the branch. Follow every fragment link on the new pages and each repointed link in `backoffice.md`. Verify: each lands on its heading. Record any that does not, as a defect in 2.2's rule.
- [ ] 9.3 At sync: grep the sibling specs for sentences this change falsifies. Candidates: "notifications documentation", "`docs/notifications.md`", "the notifications page", "Reacting to bookings". Verify: each hit is recorded as true-as-is or fixed.
- [ ] 9.4 Run CI's command, `npx @fission-ai/openspec@1.13.0 validate --all --strict`, after the last commit on each line. Verify: it passes.
- [ ] 9.5 Hand over to the maintainer's site: a short note for the site's agent giving the manifest URL pattern (`raw.githubusercontent.com/Chris-N2/UBookIt/<tag>/docs/docs.json`), the contract (spec requirement 1, `headingIds`), and that tags before this release carry no manifest. Verify: the note is given to Chris; nothing is written into the site's repository from here.
