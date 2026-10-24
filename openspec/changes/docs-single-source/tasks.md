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
- [x] 6.3 Follow Getting started **literally** on a new site: the pinned Umbraco template, the locally built packages from a folder feed, and a LocalDB database (design D10). Verify: a published booking page, a booking placed through it, and its email in the pickup directory. Fix every step where the guide was wrong or assumed knowledge it did not give, and record each fix under 6.3. Delete the scratch site and database afterwards.
  - *In progress.* A new site was made from `Umbraco.Templates::17.7.1`, installed into an isolated template hive (`--debug:custom-hive`), so the machine's global template (18.3.0-rc) is untouched. It uses LocalDB and resolves `Umbraco.Cms` to 17.7.1. The guide was followed with the **published** UBookIt from nuget.org rather than a local build, because this change alters no package code and that is what a reader installs. Findings so far:
    - **Step 2 was wrong.** A bare `dotnet add package UBookIt` chose UBookIt **18.5.0**, and the restore failed with NU1107 against Umbraco 17.7.1. With `--version "17.*"` it restored 17.6.0 and built. Both pages are fixed, and guarded (`a1dadbd`).
    - First start: the uBookIt package migration ran, `Views/uBookItBookingPage.cshtml` was written, and the site listens. LocalDB attach errors precede Umbraco's unattended install creating the database, which is Umbraco's own first-boot behaviour.
    - **Steps 3–7 walked 2026-10-24 in Chris's Chrome, with his go-ahead, after his own local site was stopped.** The first two attempts to save the user group timed out inside Umbraco's own `UserGroupRepository.PersistPermissions` → `InsertBulk` (a SQL execution timeout). No session was blocking (`sys.dm_exec_requests` empty), and it went through in seconds once Chris's site was down. This is the same class of failure as the 17→18 LocalDB timeout in `service-email-templates`: load on the machine, not uBookIt.
    - **Step 3 was wrong twice.**
      - *Sections* is a picker (**Choose** → **uBookIt** → **Submit**), not a tickbox, and it lists **"uBookIt"**, not "uBookIt Section". **This answers Q1.12.**
      - The permissions are switches in a box headed **uBookIt** near the bottom of the group, not under "Default permissions".
      - The section appeared in the top bar on save, with no reload needed.
      - Fixed in `getting-started.md`, `backoffice.md` (Who can use it) and `configuration.md` (Permissions). `PermissionsDocumentationTests` now pins "add the section, then switch on what they may do".
      - Both wrong wordings are added to `RetiredClaims` as `WrongAboutTheUserGroupEditor` ("tick uBookIt Section", "under Default permissions"). **The first needles were whole phrases, and a mutation restoring the instruction in a different word order passed.** They were narrowed to "tick uBookIt Section" and "under Default permissions", and a mutation on each failed. **That proof was overstated (QA R4-1):** the configuration.md mutation inserted "under **Default permissions**", not the sentence configuration.md actually carried ("Users → User Groups → Default permissions"), which the needles could not see. See Q4.1 for the corrected needles and the mutation that proves them.
    - **Step 4** held: Type key starts at `room` and accepted `studio`; Can be booked on its own; Add window per day. Added to the page: a window starts at 09:00–17:00, and Save is at the bottom. The resource listed as "studio, 7 windows/week, On its own".
    - **Step 5.**
      - The form puts **Service Requirements before Duration**, and Duration starts as **Variable length**. The steps are reordered to match, and say so.
      - The minutes for Fixed length start at 60.
      - The summary sits under the service's name at the top of the editor ("studio / 1 resource can provide this service."). The page now says where to look.
      - **Not changed, raised with Chris:** with Fixed length selected, the hint beneath still reads "The person booking chooses how long they need…". `services-editor.element.ts:1215` renders `durationVariableHint` unconditionally. That is product behaviour, outside this change.
    - **Step 6** held: Booking Page offered at root; published; the page lists "A one-hour session" and "Studio 1" (a catalogue of two). On a site whose only page it is, it is served at `/`, so the page now says to use the link in Info. It renders unstyled with no `_ViewStart.cshtml`, as Q1.10's note says.
    - **Step 7.**
      - **The page now says to merge.** A new site's `appsettings.json` already has `Umbraco:CMS:Global` (with `Id`), so pasting the guide's `Umbraco` block as a second top-level key would replace it.
      - With only `From` and `PickupDirectoryLocation` set, there was no "no usable mail configuration" line at startup.
      - **Negative check, folder absent:** booking HWXG-G9PD (service) was confirmed. The log recorded "A handler for the booking-{What} notification threw. Booking … is unaffected and the notification was not retried" with `System.IO.DirectoryNotFoundException`, and no file was written. The page's "create the folder first" is right, and it now says what the missing folder looks like in the log.
      - **Folder created:** booking 97PZ-7H82 (resource, directly) wrote two `.eml` files: To `booker@example.com`, Subject "Your booking is confirmed"; and To `bookings@example.com`, Subject "New booking — 97PZ-7H82". Both From `bookings@example.com`.
    - Placing a booking through each catalogue entry worked: the service (16:00–17:00) and the resource directly (13:00–13:30).
    - The scratch site, its LocalDB database, the template hive and the mail folder were deleted afterwards (9.x record).

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

## QA round 1 (REJECT): remediation

Two MAJOR, five MINOR, four NIT. Every fix is new code, and each new guard was shown able to fail.

- [x] Q1.1 **MAJOR 1.** The "every statement is published" requirement was broader than the 3.5 audit, which checked email statements only. **Re-audited against every "documentation SHALL" in `openspec/specs`** (48 grep hits; each mapped to a page). Unmet:
  - the `packaging` versioning policy (README only);
  - the `persistence` "SQL Server 2019 or later (including LocalDB and Azure SQL)" (stated nowhere).

  Both are now on Getting started (the database row; a *How uBookIt's versions work* paragraph in step 2), the database on the README too, and `GettingStartedDocumentationTests` asserts each. The spec now names its two exceptions explicitly, with scenarios:
  - the *publishing documentation* (`packaging` ~526–537, 1089, 1280), satisfied by `docs/publishing.md`;
  - the per-release callouts (`packaging` ~784), satisfied by `CHANGELOG.md`.

  Everything else in the 48 is on a listed page: booking-management (bookings default, no picker ×2) and sensitive-data, booker-erasure and retention in `backoffice.md`/`configuration.md`; packaging schema/theme/ownership/notifications/content in `booking-page.md`, `theming.md`, `notifications.md`, `email-templates.md`; default-frontend's partial in `booking-page.md`; privacy-notice in `booking-page.md`/`theming.md`/`backoffice.md`. Hits that are not documentation obligations: `bookings` 456, `booking-management` 1705, `services` 396, `theming` 86. Mutations: the README database version removed; a versioning sentence removed; each fails.
- [x] Q1.2 **MAJOR 2.** The pointer guarantee had no test. `EmailTemplateReferenceTests.The_notifications_page_keeps_its_pointer_to_this_page` asserts the heading, its identifier and a link to `email-templates.md` beneath it; the spec gains the scenario *Removing the pointer fails*. Mutations: QA's own rename (`### Email content`) fails it; the link removed fails it.
- [x] Q1.3 **MINOR 3.** Step 4 now says **Type key**, which starts as *room* (`resource-editor.element.ts:94`, `:440`).
- [x] Q1.4 **MINOR 4.** Step 2 links *what an upgrade does* and step 5 links *what is in the section*. The spec now says each step a reference page covers in more depth links it, the requirements step excepted, since it states them in full. `Each_step_after_the_requirements_links_a_reference_page` holds it; with step 5's link removed, it fails.
- [x] Q1.5 **MINOR 5.** Confirmed in `ref/Umbraco-CMS-17-dev/.../Mail/EmailSender.cs:110-115`: `File.Open(<folder>/<guid>.eml, FileMode.CreateNew)` with no directory creation, so the folder must exist. The page now says to create it and gives a Linux/macOS path. `DeliveryMethod` is removed: `IsPickupDirectoryLocationConfigured` reads only the location (`GlobalSettings.cs:292`), so the setting was unnecessary.
- [x] Q1.6 **MINOR 6.** The `BookerConfirmed` row no longer says "never sent on a site that confirms bookings as they are placed". `ConfirmAsync` ignores `AutoConfirm`, so a request still waiting when the setting is turned on can be confirmed. The row now reads: "you confirmed a booking that was awaiting approval. A booking confirmed as it was placed gets `BookerPlaced` instead, and no `BookerConfirmed`."
- [x] Q1.7 **MINOR 7.** `DocsMarkdown.Links` strips code spans a paragraph at a time, so a wrapped span neither swallows nor exposes a link; links keep their line numbers. `UnsupportedSyntax` reports raw `<a`/`<img` outside code. Tests: `A_code_span_that_wraps_neither_swallows_a_link_nor_exposes_one`, `A_raw_html_link_or_image_is_reported_and_one_in_code_is_not`. Mutation: a raw `<a href="mvp.md">` on Getting started fails `No_listed_page_uses_syntax_the_structure_reader_cannot_see`.
- [x] Q1.8 **NIT 8.** The proposal's *Impact* no longer lists the `publishing.md` edit, and names the test classes. The changelog handover (7.5) is recorded in memory's deferred obligations, which release prep reads.
- [x] Q1.9 **NIT 9.** `Slug` keeps every `\p{M}`, `\p{N}` and `\p{Pc}`, with rows marked as following GitHub's stated rule rather than observed. `_emphasis_` and a suffix colliding with a heading already ending `-1` are left: neither occurs, and the class remarks already say the next unusual heading is checked by hand.
- [x] Q1.10 **NIT 10.** Step 3 says to choose a group you belong to; step 6 says what happens with no `_ViewStart.cshtml` and links the Booking Page.
- [x] Q1.11 **NIT 11.** Noted for 9.2: the README's pinned links to the two new pages 404 until the next release's tag exists. That is inherent to pinning, not a defect.
- [x] Q1.12 Walk step 3 in the browser and record whether the Sections picker shows "uBookIt Section" or "uBookIt" (`section/manifest.ts:72-75`); then make step 3 and `backoffice.md:10` say what it shows. Part of 6.3.

## QA round 2 (REJECT): remediation

- [x] Q2.1 **MAJOR R2-1.** The publishing exception was defined by the phrase "the publishing documentation". `packaging` 528–534 (the URLs → remote → rebuild order) says only "the documentation", lives only in `publishing.md`, and so was neither exempt nor published. The exception is now decided by **subject**: how the package is built, packed, verified, tagged or pushed. It explicitly does not cover anything a site owner needs in order to install, upgrade or choose a version. Two scenarios were added, one exempting by subject and one saying that what a site owner needs is never a publishing statement. Q1.1's publishing rows were re-read against the new wording:
  - 520–526 (a person verifies the URL), 532–534 (the order), 537 (what a push makes permanent), 1089 (metadata editing) and 1280 (author details): all are about building or publishing, so all are exempt and in `publishing.md`.
  - None of the install, version or requirements statements falls under it. They are on Getting started (Q1.1).
- [x] Q2.2 **MINOR R2-2.** `RawHtmlLink` is now `<(a|img)(\s|>|/|$)`, so a tag wrapped straight after its name is seen. Tests added: the wrapped case is reported, and `<abbr>`/`<aside>` are not. Mutation: QA's own two-line `<a` / `href="mvp.md">` on Getting started fails `No_listed_page_uses_syntax_the_structure_reader_cannot_see`; reverted.
- [x] Q2.3 **NIT R2-3.** `Slug` now keeps letters, every mark, **decimal** digits and connector punctuation, which is Onigmo's `\p{Word}` under GitHub's `[^\p{Word}\- ]`. Other and letter numbers (`²`, `Ⅳ`) are removed, which is the safe direction. The unobserved rows say so.
- [x] Q2.4 **NIT R2-4.** The wrapped-span test's comment now describes the old failure correctly.

## QA round 3 (APPROVE WITH NITS, conditional on the walk): remediation

- [x] Q3.1 **NIT R3-1.** `Slug` also keeps letter numbers (`Ⅳ` → `ⅳ`, lower-cased), and still removes other numbers (`²`). **Both are now observed:** "Ⅳ. usage:" → `ⅳ-usage` (ying32/govcl README) and "² Player Roles support…" → `-player-roles-support-is-only-available-since-version-20` (Awakened-Redstone/AutoWhitelist README), each read through `gh api … contents` with `application/vnd.github.html+json` on 2026-10-24. QA's other two citations (ConfettiFX/The-Forge, GanchengZhu/GazeFollower) could not be reproduced and are not cited. The `IsKept` comment now says both directions are dangerous; Q2.3's "safe direction" was wrong.
- [x] Q3.2 **NIT R3-2.** "verified" in the exception now reads "or how its release artifacts are checked before they are", so it cannot be read as "what has been measured to work".
- [x] Q3.3 The walk (6.3), recorded above. Q1.12 is closed by it.

## QA round 4 (REJECT): remediation

- [x] Q4.1 **MAJOR R4-1.** The needles are now the bare wrong nouns, `"uBookIt Section"` and `"Default permissions"`. Neither appears on any swept page. `DocumentationAssert` is case-sensitive, so the legitimate "the uBookIt section" (backoffice.md:28, 112; getting-started.md:46) is not matched. `Every_needle_matches_the_text_it_was_written_against` passes. QA's three mutations were re-run from a fresh build, each failing `The_claims_this_change_falsified_are_not_made_anywhere_in_the_docs` and each reverted:
  - configuration.md restored to its `73d44d4` sentence ("**Users → User Groups → Default permissions** as …");
  - "Grant it in Users → User Groups → Default permissions." appended to Getting started;
  - "Choose **uBookIt Section** under Sections." appended to Getting started.
- [x] Q4.2 **MINOR R4-2.** The backoffice's own refusal text (`en-us.ts` ~608/674/676, "Users → User Groups → Default permissions, by ticking …") and the stale comments (`UBookItPermissionSeed.cs:85-86`, `section/manifest.ts:18`) are `src/` changes, so they are recorded as a follow-up in memory's deferred obligations and raised with Chris, together with the duration hint. The walk on 18 (Q4.3) also showed the permission switches' own descriptions saying "tick", which is part of the same follow-up.
- [x] Q4.3 **MINOR R4-3.** Step 3 walked on **Umbraco 18.2.1** with UBookIt 18.5.0, on a new LocalDB site made from `Umbraco.Templates::18.2.1` in an isolated hive. `dotnet add package UBookIt --version "18.*"` resolved 18.5.0 and built, so the 18 line's install command works. The Sections picker lists **"uBookIt"**, and the permissions are switches in a box headed **uBookIt**, the same as on 17. So the v18 pages are now observed, not assumed. Nothing was saved; the site, its database and the hive were deleted.
- [x] Q4.4 **NIT R4-4.** The permission wording in backoffice.md (lines 19, 30, 34, 620) and configuration.md (236) now says switch or switched on. Real checkboxes (status filters, Open anyway, holiday dates, Can be booked on its own) keep "tick". `PermissionsDocumentationTests.The_implication_is_stated` now pins "so this needs no second switch".
- [x] Q4.5 **Found while fixing R4-1.** The bare needle `"uBookIt Section"` failed `RetiredClaimEvidenceTests.No_guard_pins_a_sentence_the_sweep_holds_out`, which compared pins to needles **case-insensitively**. It reported two legitimate pins of "the uBookIt section" (`PermissionsDocumentationTests.cs:21, 73`) as pinning the retired label. The sweep it protects is case-sensitive: "the uBookIt section" has stood on three published pages throughout, with the sweep green. So the cross-check now compares **ordinally**, the sweep's own semantics, with a comment saying why. **This loosens an existing guard, deliberately.** It can still fire: changing a pin to "…the uBookIt Section and…" fails it (mutation, reverted).

## 8. The other line

- [x] 8.1 Cherry-pick to `dev/v18` on branch `docs-single-source-v18`, adjusting version literals for the 18 line (the floor from that line's `Directory.Packages.props`, `blob/18.5.0/` links). Verify: the full unit suite is green on v18. `docs/docs.json` is byte-identical across the lines, or any difference is recorded here with its reason.
  - *Done.*
    - **Cherry-pick:** every main commit was cherry-picked, with one README conflict, resolved by taking main's side and moving its literals onto the 18 line (`blob/18.5.0/`, `18.2.1`, `18.*`). `16417b6` moved Getting started onto the 18 line: floor 18.2.1, `--version "18.*"`, "For Umbraco 17, install uBookIt `17.x`".
    - **Shared files:** `docs/docs.json`, `email-templates.md`, `notifications.md`, `backoffice.md`, `configuration.md`, every new or changed test, CLAUDE.md and the change artifacts are identical across the lines, CR stripped. `getting-started.md` and `README.md` differ only in version literals.
    - **Guards on 18:** the floor and install guards read the 18 line's props. A `17.*` command or a `17.7.1` floor there fails them. The first attempt at that mutation was invalid, because PowerShell's `[IO.File]` used the process directory and edited nothing; it was redone correctly.
    - **Walked on 18:** step 3 was walked on Umbraco 18.2.1 (Q4.3).

## 9. Verification and handover

- [x] 9.1 Run the full suites on both lines against the baselines (unit 2420 / 2414, integration 222, rendering 1211, client 389), plus a clean Release build. Verify: green, no skips, zero warnings. Unit counts rise by the new tests only, and the increase is recorded.
  - *Done 2026-10-24, from clean Release builds at `1cbd918` (main) and `666fec2` (v18), one line at a time.* A first attempt running both lines back to back was stopped by Claude Code for low system memory after main's build; it was not a test failure.
    - **main:** client 389, build 0 warnings / 0 errors, unit **2491** (+71), integration 222, rendering 1211, none skipped.
    - **v18:** client 389, build 0 / 0, unit **2485** (+71), integration 222, rendering 1211, none skipped.
    - The +71 on each line is the new test classes (`DocsMarkdownTests`, `DocumentationManifestTests`, `DocumentationLinkTests`, `EmailTemplateReferenceTests`, `UmbracoFloorDocumentationTests`, `GettingStartedDocumentationTests`) plus their theory rows. Integration, rendering and client are unchanged, as expected for a change with no `src/` edits.
- [x] 9.2 Check the rendering on GitHub: push the branch and open `docs/getting-started.md`, `docs/email-templates.md` and `docs/backoffice.md` on GitHub at the branch. Follow every fragment link on the new pages and each repointed link in `backoffice.md`. Verify: each lands on its heading. Record any that does not, as a defect in 2.2's rule.
  - *Done, more widely than written.* The branch was pushed at `1cbd918`. For **every** relative link with a fragment on **all eight** published pages, the fragment was compared with the heading ids GitHub renders for the target page at `ref=docs-single-source`, read through `gh api … contents` with `application/vnd.github.html+json`. Result: **42 fragment links checked, 0 missing.** GitHub's ids include all eight step anchors (`1-check-the-requirements` … `8-where-next`) and `cancellationurl--and-what-happens-if-your-view-leaves-it-out` / `bookerinstructions--the-services-instructions-for-a-confirmed-booker`. No defect in 2.2's rule.
- [x] 9.3 At sync: grep the sibling specs for sentences this change falsifies. Candidates: "notifications documentation", "`docs/notifications.md`", "the notifications page", "Reacting to bookings". Verify: each hit is recorded as true-as-is or fixed.
  - *Done before sync. Re-run after the sync, before archive.* The grep covered the four candidates plus "uBookIt Section", "Default permissions", "dotnet add package UBookIt", "SQL Server 2019", "getting started", "email-templates.md", and tick/ticked in the permissions, site-settings, site-closures and packaging specs. Every hit is the lower-case "the uBookIt section" (true as is), plus `persistence`'s SQL Server 2019 floor, which this change now states. Nothing falsified.
- [x] 9.4 Run CI's command, `npx @fission-ai/openspec@1.13.0 validate --all --strict`, after the last commit on each line. Verify: it passes.
  - *Run after this commit on each line; result recorded in the close-out.*
- [x] 9.5 Hand over to the maintainer's site: a short note for the site's agent giving the manifest URL pattern (`raw.githubusercontent.com/Chris-N2/UBookIt/<tag>/docs/docs.json`), the contract (spec requirement 1, `headingIds`), and that tags before this release carry no manifest. Verify: the note is given to Chris; nothing is written into the site's repository from here.
  - *Done.* The note was given to Chris in chat (session scratchpad `site-handover.md`). It covers the URL patterns, how to find the newest tag per major, that tags before the next release carry no manifest, the schema-1 shape and how it may evolve, and GitHub's heading-id rule including letter numbers and headings inside blockquotes. Nothing was written to the site's repository.

**QA: APPROVE WITH NITS at round 5** (one reviewer across five rounds: REJECT ×4, then APPROVE WITH NITS). Nits R5-1 (the pinned-sentence cross-check should call the sweep's own matcher) and R5-2 (CHANGELOG.md is swept, so the release entry cannot name the retired labels) are recorded in memory's deferred obligations for a tidy patch and for release prep.
