## 1. Move the email content (move only)

- [ ] 1.1 Create `docs/email-templates.md`. Move `docs/notifications.md` from `### Writing your own message content` (the line after its heading) to the end of `### Replacing the whole message, delivery included`, unchanged, with `# Email templates` and a one-paragraph introduction above it (design D6). Commit this on its own. Verify: extract the moved block from `HEAD~1:docs/notifications.md` and from the new file and diff them. The diff is empty apart from the introduction.
- [ ] 1.2 Leave the pointer: keep `### Writing your own message content` in `notifications.md` with two sentences and a link to `email-templates.md` (design D6). Verify: `notifications.md#writing-your-own-message-content` resolves in the link guard (4.3).

## 2. The test-project Markdown helpers

- [ ] 2.1 Add a `DocsMarkdown` support class: ATX headings and inline links outside fenced code blocks; heading identifiers per design D4; and a check that a page uses no setext headings or reference-style links. Verify: unit tests for each, including fenced-block exclusion.
- [ ] 2.2 Pin the identifier rule against GitHub (design D4). Read the `id` attributes GitHub renders for the headings of `docs/notifications.md` and `docs/booking-page.md` at tag `17.6.0`. Include at least an em dash, an inline code span and an apostrophe, and a repeated heading if one exists. Make them `[InlineData]`, and record the URL and date read in a comment. Verify: the theory passes, and a deliberate change to the rule (keep the em dash) fails it.

## 3. Repoint the documentation guards

- [ ] 3.1 List every assertion in the `*DocumentationTests.cs` files, `RetiredClaims` and `SensitiveDataRedactionTests` that reads moved content. For each, record its test name and whether it asserts a presence or an absence. Verify: the list is in this file under 3.1, and every test reading `docs/notifications.md` is accounted for as *moved*, *stays* or *both pages*.
- [ ] 3.2 Point each *moved* assertion at `docs/email-templates.md`, and each *both pages* assertion at both (design D7). Verify: the unit suite is green.
- [ ] 3.3 Make the `RetiredClaims` sweep and the sweep-coverage assertion in `NotificationDocumentationTests` cover every Markdown file in `docs/`, and assert that the swept set includes every manifest page and both unlisted pages (design D7). Verify: delete `email-templates.md` from a scratch copy of the swept set and the coverage assertion fails, then revert.
- [ ] 3.4 Mutation-check every repointed **absence** assertion: insert its forbidden claim into a temporary copy of its new page, run it, confirm it fails, and revert. Verify: one line per assertion recorded under 3.4 (test name, inserted text, observed failure), and `git status` is clean afterwards.
- [ ] 3.5 Audit the statements other specs oblige *the documentation* to make about message content (`packaging` "supply the content", `email-templates`, `booking-emails`, `self-service-cancellation`, `sensitive-data`, `booker-erasure`). Confirm each is on a listed page after the move. Verify: a table under 3.5 of requirement, statement and page, with no row reading "unlisted" or "missing".

## 4. The manifest and its guards

- [ ] 4.1 Write `docs/docs.json` per the spec: `schema` 1, `headingIds` `github`, and two sections. **Using uBookIt** (`site`): getting-started, booking-page, backoffice, configuration. **Developers** (`developer`): email-templates, notifications, theming, delivery-api. Each page has a summary. Verify: it parses with `System.Text.Json`.
- [ ] 4.2 Add `DocumentationManifestTests`, covering: the shape and constants; malformed entries; listed files exist and none is listed twice; every `docs/*.md` is listed or deliberately unlisted (`mvp.md` and `publishing.md`, with reasons, design D2); titles equal the level-one heading; the README's *Documentation* list links every listed page. Verify: each test is shown to fail once, by a scratch edit for each scenario in the spec's first two requirements (an unlisted new file, a changed heading, a removed README link, a `file` containing `/`, a two-line summary, an unknown audience), and is reverted.
- [ ] 4.3 Add `DocumentationLinkTests`: every relative link in a listed page names a listed page or an existing non-Markdown file beneath `docs/`, never leaves `docs/`, and any fragment matches a heading identifier on the target page. Fix the links that fail, which must include `backoffice.md`'s three links into `notifications.md`, now that `#template-sets` has moved. Verify: green. Then each of the spec's link scenarios is shown to fail by a scratch edit (a link to `mvp.md`, to `../README.md`, to a missing image, to a missing fragment), and reverted.

## 5. The email reference

- [ ] 5.1 In `email-templates.md`, turn *What your template is given* into reference tables (design D5): one for `BookingMessageModel` (its ten members), one for what `BookerMessageModel` adds (seven) and one for what `InternalMessageModel` adds (two). Columns are member, type, and what it carries including when it is `null`. Keep the explanatory prose around them: `TemplateSet` over `ServiceId`, `Kind` against `Status`, and `ResourceNames` iteration. Add the `@inherits` line for `InternalMessageModel` beside the existing one. Verify: every fact in the replaced prose is either in a table cell or kept as prose. Record any sentence dropped as redundant under 5.1, with the cell that now carries it.
- [ ] 5.2 Add `EmailTemplateReferenceTests` (design D5): the member tables against reflection, the file table against `PathFor` for every `BookingMessageKind`, the folder against `TemplateFolder`, and the `@inherits` types resolving. Verify: green. Then each is shown to fail and is reverted: add a public property to a model in a scratch edit; remove a row; add a row for a member that does not exist; rename a file row; change an `@inherits` namespace.

## 6. Getting started

- [ ] 6.1 Write `docs/getting-started.md`, covering the eight steps in the spec's order and linking each step's reference page rather than restating it. It states the requirements, including SQL Server and the Umbraco floor. It describes the pickup-directory check as a way to confirm delivery, not as a production setting (design D10). It ends with *Where next*, linking the developer section including the delivery API. Verify: the manifest and link tests pass.
- [ ] 6.2 Add the floor guard (design D8): parse the `Umbraco.Cms` lower bound from `Directory.Packages.props`, and compare it with the README's *Get started* sentence, the README's *Requirements* row and Getting started. Verify: green; then changing the props bound in a scratch edit fails it naming each page, and is reverted.
- [ ] 6.3 Follow Getting started **literally** on a new site: the pinned Umbraco template, the locally built packages from a folder feed, and a LocalDB database (design D10). Verify: a published booking page, a booking placed through it, and its email in the pickup directory. Fix every step where the guide was wrong or assumed knowledge it did not give, and record each fix under 6.3. Delete the scratch site and database afterwards.

## 7. Signposting and convention

- [ ] 7.1 README (design D9): add *Getting started* and *Email templates* to the *Documentation* list, re-describe *Reacting to bookings*, link Email templates from the *For developers* email bullet, and link Getting started from *Get started*. Every new link uses `blob/17.6.0/docs/…`. Verify: `VersionTruthTests` and the manifest's README test pass.
- [ ] 7.2 `docs/backoffice.md`: point the template-set and email-content links at `email-templates.md`. Verify: link tests green.
- [ ] 7.3 `docs/publishing.md` step 3's table: add the row for Getting started's floor version (design D8). Verify: `VersionTruthTests` passes, and the row names the same section the floor guard reads.
- [ ] 7.4 Add a *Documentation* section to `CLAUDE.md`, and the proposal rule to `openspec/config.yaml` (design D11). Verify: `openspec instructions proposal --change docs-single-source --json` shows the new rule.
- [ ] 7.5 `CHANGELOG.md`: add an undated entry under the next release saying the documentation is restructured, with a Getting started and an Email templates page and a manifest at `docs/docs.json` for sites that render it. Verify: the changelog guards pass.

## 8. The other line

- [ ] 8.1 Cherry-pick to `dev/v18` on branch `docs-single-source-v18`, adjusting version literals for the 18 line (the floor from that line's `Directory.Packages.props`, `blob/18.5.0/` links). Verify: the full unit suite is green on v18. `docs/docs.json` is byte-identical across the lines, or any difference is recorded here with its reason.

## 9. Verification and handover

- [ ] 9.1 Run the full suites on both lines against the baselines (unit 2420 / 2414, integration 222, rendering 1211, client 389), plus a clean Release build. Verify: green, no skips, zero warnings. Unit counts rise by the new tests only, and the increase is recorded.
- [ ] 9.2 Check the rendering on GitHub: push the branch and open `docs/getting-started.md`, `docs/email-templates.md` and `docs/backoffice.md` on GitHub at the branch. Follow every fragment link on the new pages and each repointed link in `backoffice.md`. Verify: each lands on its heading. Record any that does not, as a defect in 2.2's rule.
- [ ] 9.3 At sync: grep the sibling specs for sentences this change falsifies. Candidates: "notifications documentation", "`docs/notifications.md`", "the notifications page", "Reacting to bookings". Verify: each hit is recorded as true-as-is or fixed.
- [ ] 9.4 Run CI's command, `npx @fission-ai/openspec@1.13.0 validate --all --strict`, after the last commit on each line. Verify: it passes.
- [ ] 9.5 Hand over to the maintainer's site: a short note for the site's agent giving the manifest URL pattern (`raw.githubusercontent.com/Chris-N2/UBookIt/<tag>/docs/docs.json`), the contract (spec requirement 1, `headingIds`), and that tags before this release carry no manifest. Verify: the note is given to Chris; nothing is written into the site's repository from here.
