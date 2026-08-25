## Why

Putting uBookIt on a page is currently undocumented manual work: create a document
type, create a template, write `@inherits UmbracoViewPage`, call
`Component.InvokeAsync("BookingFlow")`, and know that `ubBook` is the query key. The
only artefact showing any of this is `src/UBookIt.TestSite/Views/UbookitBookingTest.cshtml`
— a dev harness that ships with nothing.

Every other part of the package is now installable and tested. This is the last step
between "the code works" and "an editor can use it".

## What Changes

- A **Booking Page document type** ships with the package, created on install by an
  `AutomaticPackageMigrationPlan` reading an embedded `package.xml`.
- A **template** ships with it that is deliberately **one line**: a delegate into the
  booking ViewComponent, carrying no markup of its own.
- **Documentation of what an upgrade overwrites**, because it is not what the Umbraco
  documentation says and an editor cannot discover it safely.

The one-line template is not a stylistic choice. It is forced by measurement — see
below — and it is why this change can ship a template at all.

### The measured constraint this change is built on

Spiked against the pinned Umbraco 17.6.2 on 2026-08-25, by installing schema, editing
it as an editor would, then shipping a "v2" and restarting:

| On upgrade (any `package.xml` change re-runs the import) | Result |
|---|---|
| A template the editor customised | **destroyed**, reset to the shipped text |
| Doctype `Name`, `Icon`, `Description`, `AllowAtRoot` | **overwritten** |
| A property the **editor** added | survived |
| A property **we** add in v2 | reaches existing v1 installs |

The Umbraco documentation states *"Existing schema or content will not be overwritten
in this process."* That holds for **content** and is **false for schema**. The sharpest
case: the template's XML was **identical** between v1 and v2 and was destroyed anyway,
because the whole manifest re-imports whenever its hash changes for any reason. A
release that touches only the document type therefore wipes template customisation.

So a template with anything worth keeping in it cannot be shipped this way.
Customisation belongs instead in **overridable views** — a site placing its own file at
the same path under `Views/Shared/UBookIt/` wins by MVC view resolution. That is the
Umbraco Forms "themes" pattern, and uBookIt already has most of it.

## Non-goals

- **No `RenderController` owning GET+POST, and no removal of the TempData PRG
  workaround.** This is ⑤ design D3's stated intent and remains wanted. It is excluded
  because it is **unmeasured**: route hijacking dispatches by template name rather than
  by verb, Umbraco's own documentation still presents `Html.BeginUmbracoForm` + `ufprt`
  as the mechanism for form posts, and whether a verb-overloaded hijacked action carries
  a model and anti-forgery cleanly is exactly the kind of assumption that has cost this
  project QA rounds. It gets its own change, with its own spike first.
- **No closing of the `[ValidateAntiForgeryToken]` obligation.** It was carried here on
  the expectation that this change would rewrite the POST path. It does not, so the
  obligation travels with the POST-path change instead of being quietly discharged.
- **No deletion of the two Umbraco DI registrations ⑪ added to the rendering rig.**
  They go when `BeginUmbracoForm` goes, which is the same deferred change.
- **No content.** The package creates a document *type*, never a document. Which pages
  exist is the editor's decision, and `package.xml` can carry content — deliberately
  unused.
- **No backoffice UI.** Nothing is added to the uBookIt section.
- **No change to the shipped views or the ViewComponents.** The template delegates to
  what already exists.

## Capabilities

### New Capabilities

- `packaging`: what the package installs into an Umbraco site, what it owns versus what
  the editor owns, and what survives an upgrade.

### Modified Capabilities

None. `default-frontend` describes what the flows render, and that is unchanged — this
change adds a way to reach them, not a change to them.

## Impact

- **New**: an embedded `package.xml` and an `AutomaticPackageMigrationPlan` in
  `UBookIt.Web`, plus a `<EmbeddedResource>` entry. The plan's namespace and the
  resource name must match, which is a real coupling and needs a test.
- **New**: a shipped template (one line) and a shipped document type.
- **`src/UBookIt.TestSite/Views/UbookitBookingTest.cshtml`** becomes redundant as
  documentation, though it stays useful as a dev harness for the `?flow` and
  `?resourceId` query overrides no shipped page exposes. Decide explicitly rather than
  deleting it by reflex.
- **Installs schema into consumers' sites** — the first thing this package does that a
  consumer cannot undo by removing a NuGet reference. That, not code, is the risk in
  this change, and it is why the upgrade behaviour is documented rather than assumed.
- No public API change, no persistence or EF Core migration change, no delivery or
  management contract change. No breaking change.
