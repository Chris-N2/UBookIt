## Context

`docs/` holds eight Markdown pages. Six are for users (`booking-page`, `backoffice`,
`configuration`, `delivery-api`, `notifications`, `theming`) and two are for the maintainer (`mvp`,
`publishing`). Links between the user pages are already relative (`notifications.md#template-sets`),
use no raw HTML, and never leave `docs/`. The only `../README.md` link is in a maintainer page. The
README links each page absolutely, pinned to the release tag, and `VersionTruthTests` enforces that
pin.

The pages are guarded heavily. The documentation tests read files by path through
`RepoFiles.Read`, which fails when a file is missing, and many assert sentences through
`DocumentationAssert`. `RetiredClaims` sweeps named files for claims that must never come back.
`NotificationDocumentationTests` asserts which files that sweep covers.

The email content to move is `notifications.md` from *Writing your own message content*
(line 187) to the end of *Replacing the whole message, delivery included* (line 472). The models
are `BookingMessageModel` (abstract, ten members) with `BookerMessageModel` (seven more) and
`InternalMessageModel` (two more), in `UBookIt.Core.Notifications`. The view path comes from
`RazorBookingTemplateRenderer.TemplateFolder` and `PathFor(BookingMessageKind)`, with the seven
`BookingMessageKind` values.

See proposal.md for motivation, and specs/documentation/spec.md for the requirements.

## Goals / Non-Goals

**Goals:**
- Every guard the spec asks for is a unit test in `tests/UBookIt.Tests`, and each is shown able
  to fail.
- No new package dependency in any project, test projects included.
- The diff for `notifications.md` reads as a move: the content arrives in `email-templates.md`
  unchanged except where the new layout requires it (headings promoted one level, the member prose
  turned into tables).

**Non-Goals:**
- Changing what any page *says*, beyond Getting started (new), the member tables (a new form for
  existing facts), the pointer, and link targets.
- Making `CHANGELOG.md` part of the published set. It is not in `docs/` and stays where it is.

## Decisions

### D1. The manifest is a hand-written JSON file, not generated, and not front matter

`docs/docs.json` is edited by hand and checked by tests.

Two alternatives were rejected:
- **YAML front matter in each page**, giving the title, section and order. GitHub renders front
  matter as a table at the top of the page, which the README's readers would then see. Order would
  also have to be spread across eight files as numbers.
- **A manifest generated at release** by a workflow. That adds a build step whose output is never
  reviewed, for a file that changes about once a feature.

JSON over YAML because the consumer is a .NET site, which can read it with `System.Text.Json` and no
dependency. The test project reads it the same way.

### D2. The deliberately unlisted pages live in the test, not the manifest

The manifest describes what is published. A list of what is *not* published would be data the
consumer is meant to ignore, so it would be noise in the contract. The test holds
`{"mvp.md", "publishing.md"}`, each with a comment giving the reason. Adding a maintainer page then
takes a deliberate test edit, which is the point.

### D3. `headingIds` is a field, not prose

Links carry fragments, and a fragment lands only if the consumer gives headings the same
identifiers GitHub does. That is a requirement on the consumer, so it belongs in the data the
consumer reads (`"headingIds": "github"`), not in a page it might never open. If GitHub's rule ever
diverges from what the site can do, the field is where a second value goes.

### D4. GitHub's identifier rule is implemented in the test project and pinned against GitHub

The rule, as applied to these pages:
- lower-case the heading's text, with inline code markers removed;
- remove every character that is not a letter, digit, space, hyphen or underscore;
- turn each space into a hyphen;
- suffix a repeated identifier with `-1`, `-2` and so on.

So `` `CancellationUrl` — and what happens if your view leaves it out `` becomes
`cancellationurl--and-what-happens-if-your-view-leaves-it-out`: the em dash is removed and the
spaces either side of it give a double hyphen.

A rule written from memory is a guess. So the implementation is pinned by tests whose expected
values were **read from GitHub's rendering** of the current pages: the `id` attributes on the
headings of `notifications.md` and `booking-page.md` at the `17.6.0` tag, including at least one em
dash, one inline code span, one apostrophe and one repeated heading if the pages have one. Task 2.2
records where each expected value came from. This follows `DocumentationAssertTests`: a helper
that decides whether other tests pass is checked itself.

No Markdown library is used. Headings are ATX lines (`#`…`######`) outside fenced code blocks, and
links are `](target)` outside fenced code blocks. The pages use nothing else, and a test asserts
that no page uses setext headings or reference-style links. A page that started to use them would
fail loudly instead of being silently mis-parsed.

### D5. The email reference tables are checked by reflection in the test project

The guarantee spans two languages: C# properties and Markdown tables. So the crossing guard sits in
the test project, which references both `UBookIt.Core` and `UBookIt.Web` and can read the pages.

- **Member tables.** Each model's table sits under a heading naming the model. Its first column is
  the member name in backticks. The test compares that set with the model's public instance
  properties, inherited ones included (a record's compiler-generated `EqualityContract` is
  protected, so it is not among them), and fails naming the extras on either side. A second column
  must be non-empty.
- **File table.** Its rows are compared with `PathFor(kind)` for every `BookingMessageKind` value.
- **Folder.** The folder the page states is compared with `TemplateFolder`.
- **`@inherits` lines.** The type names are resolved by reflection.

The common members are documented once, in a table for the base model, `BookingMessageModel`.
Each derived model's table lists only what it adds and says so. The test checks the union for
each concrete model. This avoids two tables that repeat the same ten rows, and repeated rows
are exactly where descriptions drift apart.

### D6. What moves, and what stays in `notifications.md`

These move to `email-templates.md`:
- *Writing your own message content*, with every subsection: the model, `CancellationUrl`,
  `BookerInstructions`, template sets, HTML, what becomes yours, package-supplied templates, and
  when something is wrong;
- *Replacing the whole message, delivery included*.

The second moves because it is the last rung of the same ladder: wording, then a set, then the
whole message. It already introduces itself as "a bigger step than writing a template".

These stay in `notifications.md`:
- turning emails on;
- who is told;
- what the messages contain;
- instructions for the booker;
- approval;
- bookings recorded by your own people;
- the notifications themselves.

These are about **whether and to whom** uBookIt sends, which is configuration and behaviour, not
authoring.

**The page title stays *Reacting to bookings*.** It still describes what the page is about, and
changing it would only move the problem. The README's description of it changes instead (see D9).

The heading `### Writing your own message content` stays where it is, with two sentences and a
link beneath it, so `notifications.md#writing-your-own-message-content` still resolves on `main`.
Links to the old *subsections* on `main` land at the top of the page. Every link that is already
published is pinned to a tag, where the old layout survives.

### D7. The documentation tests follow the content, not the file name

The tests that read moved content are changed to read `docs/email-templates.md`: parts of
`NotificationDocumentationTests`, `ServiceTemplateSetDocumentationTests`,
`ServiceBookerInstructionsDocumentationTests`, `SelfServiceCancellationDocumentationTests` and
`ResponsibilityDocumentationTests`. A test that asserts something about *both* halves reads both
pages.

`RetiredClaims` and the sweep assertion in `NotificationDocumentationTests` stop naming files.
They sweep **every Markdown file in `docs/`**, and the assertion becomes "the sweep covers every
page the manifest lists, plus the deliberately unlisted ones". A sweep that named files would not
see the two new pages, which is the failure described in *a guard scoped wrongly reports green*.

**Every repointed test that asserts an absence is mutation-checked.** It is run once against a
temporary copy of its new page with the forbidden claim inserted, it must fail, and the copy is
reverted. Task 3.4 records each result. A positive assertion fails by itself when it reads the
wrong page, so it needs no mutation.

### D8. The Umbraco floor guard reads `Directory.Packages.props`

The lower bound of the `Umbraco.Cms` range (`[17.7.1,18.0.0)` → `17.7.1`) is parsed from the
central package file. The test compares it with the version in the README's *Get started*
sentence, the README's *Requirements* row and Getting started's requirements. On `dev/v18` the
same test reads that line's own props file and finds `18.x`.

`docs/publishing.md` step 3's table gains a row for Getting started, so the procedure lists what
the guard checks. The floor raise next week is this guard's first real use.

### D9. README changes

- The *Documentation* list gains *Getting started* at the top and *Email templates* after
  *Reacting to bookings*.
- *Reacting to bookings* gets a new description: "whether and to whom uBookIt sends emails, and the
  notifications your own code can handle".
- The email bullet under *For developers* links Email templates.

Every new link uses the release-tag form `blob/17.6.0/docs/…`, which the next release's bump moves
with the rest. `VersionTruthTests` already fails a link that names a branch or a previous version.

The README's *Get started* section stays. It is what nuget.org shows, and a package page that
starts by sending readers elsewhere is worse. It gains one link to the full Getting started guide.

### D10. Getting started is verified by following it

A guide nobody has followed is a draft. Before QA, the guide is followed **literally** on a new
Umbraco site, created with the pinned Umbraco template from the local package build against a
LocalDB database. Every place where it was wrong, or needed knowledge it did not give, is fixed
and recorded in the tasks. The email step uses a pickup-directory SMTP setting so delivery can be
confirmed without a mail server, and the guide says that this is a way to check, not a
recommendation for production.

### D11. The convention is in two places, briefly

- A short *Documentation* section in `CLAUDE.md` gives the three rules and points at
  `docs/docs.json`.
- One proposal rule goes in `openspec/config.yaml`: "Name the documentation pages this change
  updates; if it changes setup, include Getting started; if it adds a page, add it to
  `docs/docs.json`".

The rule in the config reaches every proposal through `openspec instructions`. `CLAUDE.md` reaches
every session.

## Risks / Trade-offs

- **[Risk] The identifier rule differs from GitHub's in a case the pages do not yet use** →
  The pinning tests use real GitHub output. A future heading with an unusual character could still
  differ. The failure would be a link the test calls valid that GitHub does not resolve, which is
  the dangerous direction. It is accepted because the alternative is a runtime dependency on
  GitHub's renderer. Task 2.2's comment says so, and the next unusual heading should be checked
  against GitHub by hand.
- **[Risk] A repointed absence guard passes without testing anything** → D7's mutation check, one
  record per guard.
- **[Risk] Moving about 285 lines hides an edit inside the move** → Move first, in its own commit, with
  the moved block byte-identical (checked by extracting it from both revisions and diffing). The
  tables, headings and pointer are later commits. QA can then see the content arrive unchanged.
- **[Risk] The manifest contract is frozen before its consumer exists** → It is deliberately
  minimal, and the evolution rule lets the site's needs be added later without a break. The site
  author is the maintainer, who has agreed to the shape.
- **[Trade-off] Getting started duplicates the README's *Get started*** → Intentionally partial:
  the README keeps its four lines because nuget.org renders it, and links the full guide. Only the
  version is duplicated, and D8 guards it.
- **[Trade-off] A consumer's site lags the repository** → The site renders what a tag carries, so
  documentation for an unreleased feature is never published early. That is the point of following
  tags, not a defect.

## Migration Plan

There is nothing to migrate on a site. The change ships with the next release on both lines,
cherry-picked to `dev/v18` with that line's version literals. The maintainer's site starts reading
`docs/docs.json` from the first tag that carries it. Earlier tags have no manifest, and the site
should not offer them. Rollback is a revert: no shipped artefact depends on the new files.
