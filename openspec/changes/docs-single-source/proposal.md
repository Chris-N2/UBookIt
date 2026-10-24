## Why

A site owner upgrading to `17.6.0` to use per-service email templates could not find the view
names a template set needs, or the model those views are given, and ended up reading the source.
The documentation was there. It sits in `docs/notifications.md`, which the README lists as
*"Reacting to bookings: the notifications and how to handle them"*. It describes the model's members
in paragraphs, never shows the `@inherits` line for `InternalMessageModel`, and no page takes a
site from install to a working booking.

At the same time the maintainer's own site will start rendering uBookIt's documentation, as uSync's
author does for uSync. The repository has to become the **single source** that site pulls at each
release tag. The site must not keep a second copy that drifts from the code.

## What Changes

- **A documentation manifest, `docs/docs.json`.** It lists the published pages in order, grouped
  into a *site* section (people setting uBookIt up and running it) and a *developer* section (people
  extending it). Each page has a file, a title and a one-line summary. It is versioned (`"schema": 1`)
  and treated as a stable contract: fields may be added, but renaming or removing one is a breaking
  change. The maintainer pages, `mvp.md` and `publishing.md`, are deliberately left out.
- **Guards that keep the manifest and the pages in step.**
  - Every listed file exists.
  - Every page in `docs/` is either listed or named as deliberately unlisted.
  - Each listed title matches the page's `#` heading.
  - Every link between listed pages names a listed page and, where it carries a fragment, a heading
    that exists on that page.
- **A new page, `docs/getting-started.md`.** It covers the default booking flow end to end: install,
  requirements, granting the section and Sensitive data, a resource and its hours, a service, the
  booking page, turning emails on and checking one arrives, then where to go next. The Umbraco floor
  it states is guarded against `Directory.Packages.props`, and so is the README's.
- **A new page, `docs/email-templates.md`, split out of `docs/notifications.md`.** It holds:
  - the table of the seven view files;
  - both `@inherits` lines with full type names;
  - **a member reference table for each model**;
  - template sets;
  - what falls back and when;
  - templates supplied by a package;
  - replacing the whole message.

  Guards check that the file table lists exactly the `BookingMessageKind` values and that each
  model's table lists exactly its public properties. A member added to a model fails the build until
  it is documented.

  `notifications.md` keeps a short pointer at the old heading. `backoffice.md` links to the new page.
- **The existing documentation guards move with the content.** The documentation tests that read
  `notifications.md` for email content are pointed at the new page. Each one that asserts something
  is **absent** is shown to fail against a deliberately broken page. Otherwise a guard left reading
  a page the content has left would pass without testing anything.
- **README signposting.** The *Documentation* list gains Getting started and Email templates and
  renames the notifications entry so it no longer hides the emails. The email bullet under
  *For developers* links to the new page. All of these links are pinned to the release tag, as the
  existing ones are.
- **A documentation convention** in `CLAUDE.md` and in the proposal rules in `openspec/config.yaml`:
  - a change names the documentation page it updates;
  - a change to setup updates Getting started;
  - a new page is added to the manifest.
- **Both lines.** The change is cherry-picked to `dev/v18`, and each line's manifest lists its own
  pages.

No public API, schema, configuration or rendered output changes. Nothing is **BREAKING**.

## Non-goals

- **The site that renders the documentation.** Fetching, rendering, navigation, version switching
  and styling belong to the maintainer's site, in its own repository. This change defines the
  manifest that site reads and nothing on the site side.
- **A static site generator, docs theme or GitHub Pages build in this repository.** The repository
  carries Markdown and a manifest only.
- **A developer guide to building a headless front end on the delivery API.** The developer section
  starts with the existing reference pages. That walkthrough is a later change, and it will be one
  more page in the manifest.
- **Rewriting the other reference pages.** `booking-page.md`, `backoffice.md`, `configuration.md`,
  `delivery-api.md` and `theming.md` keep their content. Only links into the moved sections change.
- **A release of its own.** This ships with the next release, the Umbraco security floor raise, and
  not as a docs-only patch.
- **Redirects for old links.** Published readmes link to tagged commits, which never change. Links
  to `main` are served by the pointer left in `notifications.md`.

## Capabilities

### New Capabilities
- `documentation`: the published documentation set and its manifest. It covers what the manifest
  lists and the contract it offers a consumer; that the manifest and the pages agree; that links
  between published pages resolve; that a Getting started page covers the default flow; and that the
  email template reference is complete against the code it documents.

### Modified Capabilities

None. The requirements that state what *the documentation* must say (`packaging`, `email-templates`,
`booking-emails`, `booker-erasure`, `booking-retention`, `booking-management`) name the documentation,
not a file, and every statement they require is carried to its new page word for word. No
requirement is replaced, so no guarantee can be dropped by rewriting one.

## Impact

- **New:**
  - `docs/docs.json`;
  - `docs/getting-started.md`;
  - `docs/email-templates.md`;
  - a manifest/link test class and an email-reference test class in `tests/UBookIt.Tests`.
- **Edited:**
  - `docs/notifications.md` (the content moves out; a pointer stays);
  - `docs/backoffice.md` (links);
  - `README.md` (*Documentation* list, the email bullet, a floor guard);
  - `CLAUDE.md`;
  - `openspec/config.yaml`;
  - `docs/publishing.md` (the version-bump table gains Getting started's floor);
  - the `*DocumentationTests.cs` files that read the moved content.
- **Unchanged:**
  - every package's code and compiled output;
  - the packed readme's *form*: links stay absolute and pinned to the tag.
- **Downstream:** the maintainer's site reads `docs/docs.json` and the pages it lists at each tag
  from `raw.githubusercontent.com/Chris-N2/UBookIt/<tag>/docs/`.
