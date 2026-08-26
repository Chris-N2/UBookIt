## Why

Putting uBookIt on a page is currently undocumented manual work: create a document
type, create a template, write `@inherits UmbracoViewPage`, call
`Component.InvokeAsync("BookingFlow")`, and know that `ubBook` is the query key. The
only artefact showing any of this is `src/UBookIt.TestSite/Views/UbookitBookingTest.cshtml`
— a dev harness that ships with nothing.

Every other part of the package is now installable and tested. This is the last step
between "the code works" and "an editor can use it".

## What Changes

- A **Booking Page document type** ships with the package, created on install by a
  **run-once** `PackageMigrationPlan` reading an embedded `package.xml`.
- A **template** ships with it that is deliberately **one line**: a delegate into the
  booking ViewComponent, carrying no markup of its own.
- **Documentation of what the package owns and what the site owns**, because it is not
  what the Umbraco documentation says and a site author cannot discover it safely.

### The measured constraints this change is built on

Spiked against the pinned Umbraco 17.6.2 on 2026-08-25 and revised after QA, by
installing schema, editing it as a site author would, then shipping a "v2" and
restarting.

**What an import does when it runs:**

| | Result |
|---|---|
| A template the site customised | **destroyed**, reset to the shipped text |
| Doctype `Name`, `Icon`, `Description`, `AllowAtRoot` | **overwritten** |
| A property the **site** added | survived |
| A property **we** add later | reaches existing installs |
| A document type the site **deleted** | **never restored** |

The Umbraco documentation states *"Existing schema or content will not be overwritten
in this process."* That holds for **content** and is **false for schema**.

**When an import runs** is the decision that matters, and the first version of this
proposal got it wrong. An `AutomaticPackageMigrationPlan` keys its state on a hash of
the manifest, so it re-imports on **every** change to that file — including a release
that never mentions templates. A custom plan keys on explicit ids and is **run-once**:
measured, a manifest change after install did not reach the site at all.

So the plan is custom, and after install the shipped template is **the site's own
file**. That is also how the Clean starter kit ships 14 templates, its partials and
466KB of stylesheets without destroying its users' work.

**There is no view-override mechanism, and this change does not pretend otherwise.**
The first version of this proposal claimed customisation belonged in overridable views.
QA disproved it: the package's views are compiled into `UBookIt.Web.dll` without source
checksums, so a same-path file in a site is never consulted — in development or
production. Restyling the flow's internals is not possible yet, the documentation says
so plainly, and theming is scheduled as its own change.

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

- **New**: an embedded `package.xml`, a run-once `PackageMigrationPlan` and its import
  migration in `UBookIt.Web`, plus a `<EmbeddedResource>` entry. The migration's
  namespace and the resource name must match, which is a real coupling and needs a test.
- **New**: a shipped template (one line) and a shipped document type.
- **`src/UBookIt.TestSite/Views/UbookitBookingTest.cshtml`** becomes redundant as
  documentation, though it stays useful as a dev harness for the `?flow` and
  `?resourceId` query overrides no shipped page exposes. Decide explicitly rather than
  deleting it by reflex.
- **Installs schema into consumers' sites** — the first thing this package does that a
  consumer cannot undo by removing a NuGet reference. That, not code, is the risk in
  this change, and it is why the upgrade behaviour is documented rather than assumed.
- **Public API surface grows** by two public types — `BookingPagePackageMigrationPlan`
  and `ImportBookingPageSchema` — which are a compatibility promise once published, and
  must be public for Umbraco's type finder to discover them. The plan's state ids are a
  stronger commitment still: they cannot be changed, and the plan type cannot be
  swapped, without breaking every installed site.
- No persistence or EF Core migration change, no delivery or management contract
  change. No breaking change to anything already published.
