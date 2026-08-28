## Context

Three tiers of customisation were agreed, with no overlap between them: *a different page
around the flow* → the site's own template (shipped ⑫); *make it look like my site* →
stylesheet and tokens (shipped ⑬); *different controls entirely* → a theme (this change).

The third tier was blocked on one unmeasured claim, since ⑫ established that a site's
**loose file** at the same path as a package view is never consulted — the package's views
carry no `RazorSourceChecksum`, so Umbraco's dev-mode compiler marks them
"used as-is" and a site's override loses in dev mode and loses harder in production.
The open question was whether a view precompiled into a *second RCL*, at a *different*
path, behaves differently.

**Measured 2026-08-28: it does.** A spike RCL's precompiled theme view won the expander's
first candidate over `UBookIt.Web`'s own, on both ViewComponents, under real Umbraco page
rendering. The full transcript is in the `umbraco-shipping-views-in-a-package` note; the
findings that shaped this design are carried into the Decisions below rather than assumed.

The driving consumer is `UBookIt.UI.DevExpress` — a separate package in a separate repo,
per invariant 2 — whose entire reason to exist is replacing the time and duration
controls with a scheduler.

## Goals / Non-Goals

**Goals:**

- A theme can replace the package's rendering with its own controls, and is a versionable,
  testable package rather than a folder of loose files.
- A theme that is registered but does not win is **impossible to ship unnoticed**, because
  that failure renders a correct, entirely unthemed site.
- A theme that is incomplete is caught in its author's build, and degrades rather than
  destroys a site that reaches production with one.
- ⑪'s 598 rendering tests remain untouched. If they need changing, the design has drifted.
- Every guarantee the package still makes, and every one it hands to a theme author, is
  stated rather than inferred.

**Non-Goals:**

- Per-view, per-page or per-document-type theme selection.
- Making the four shared partials individually replaceable.
- Shipping a theme, or a second rendering, from this repository.
- Any claim about a theme's accessibility, markup or behaviour.

## Decisions

### D1. A theme is an RCL supplying precompiled views at a theme-rooted path

`Views/Shared/UBookIt/Themes/<theme>/Components/{Booking,BookingFlow}/<View>.cshtml`,
compiled into the theme's own assembly and discovered through the ordinary
`ApplicationPart` mechanism.

*Why not a loose-file convention*, which is what `docs/booking-page.md` currently predicts
the fix will need ("a path the package deliberately does not compile into itself")? That
prediction was **wrong, and the spike disproved it**. It assumed the collision of ⑫ — a
site file competing with a compiled view at the *same* path. Two precompiled assemblies at
*different* paths do not collide at all, so there is nothing for the package to refrain
from compiling. Correcting the prediction matters as much as correcting the promise: it is
the reason the cheap architecture was thought unavailable.

*Why not loose files anyway?* They cannot be unit-tested, versioned, or shipped as a
NuGet package, and a DevExpress front-end must be all three.

### D2. Selection by `IViewLocationExpander`, one theme package-wide, with a `{1}`-free format

`/Views/Shared/UBookIt/Themes/<theme>/{0}.cshtml`.

ViewComponent resolution calls `GetView(executingFilePath, viewName)` first — which returns
NotFound for a bare name like `Catalogue`, since it only handles `~/`, `/` or a `.cshtml`
suffix — and then `FindView(context, "Components/{Component}/{View}")`. Only that second
call consults `ViewLocationFormats`. So the expander **is** consulted (the spike's third
possible outcome did not occur), and `{0}` already carries the `Components/X/Y` segment.

`{1}` is the *controller* name, which under this resolution path is whatever controller
happens to be executing — it rendered as `SpikeDiagnostics` in the spike — and `{2}` (area)
is empty. Neither is usable, which is also why Umbraco's own `App_Plugins` formats degrade
to `/App_Plugins//Views/...` here.

One theme package-wide keeps the expander trivial and its location cache unfragmented, and
means `PopulateValues` contributes nothing to the cache key. **The corollary is that the
theme is fixed at startup**: changing it at runtime would leave a populated location cache
keyed without it. Stated as a constraint rather than left to be discovered.

### D3. Registration must run LAST in the expander chain, and this is the design's sharpest edge

Umbraco registers two expanders of its own —
`RenderRazorViewEngineOptionsSetup+ViewLocationExpander` and
`PluginRazorViewEngineOptionsSetup+ViewLocationExpander` — which **prepend six locations**:

```
/App_Plugins/{2}/Views/{1}/{0}.cshtml
/App_Plugins/{2}/Views/Shared/{0}.cshtml
/App_Plugins/{2}/Views/Partials/{0}.cshtml
/Views/{0}.cshtml
/Views/Shared/{0}.cshtml        ← the package's own views live here
/Views/Partials/{0}.cshtml
```

`ViewLocationExpanders.Add` **appends**, and the chain runs in list order, so **registering
earlier makes uBookIt run first, which is wrong** — Umbraco then prepends its six in front
of the theme candidate, `/Views/Shared/{0}.cshtml` matches, and the package view wins. This
is not hypothetical: it is what the spike did on its first run, and the observable symptom
is a site that renders perfectly and is simply not themed.

Registration is therefore an **`IPostConfigureOptions<RazorViewEngineOptions>`**, added by
`AddUBookItTheme`. `OptionsFactory.Create` runs every `IConfigureOptions<T>` before any
`IPostConfigureOptions<T>`, **whatever order they were registered in**, and Umbraco's two
setups are `IConfigureOptions` (`AddWebsite()` calls `Services.ConfigureOptions<...>()` for
both). So the package's expander is appended after theirs no matter where in the chain the
site calls it. The guarantee is a property of the options system rather than of a call
order anybody has to get right.

**This replaced an `IComposer`, and the reason is worth keeping.** The composer was chosen
on the reasoning that composers run at `IUmbracoBuilder.Build()`, after `AddWebsite()`.
**That reasoning was false.** `AddComposers()` constructs a `ComposerGraph` and calls
`Compose()` immediately (`UmbracoBuilder.Composers.cs:20`, Umbraco **17.8.0-rc** source),
so composition happens where the site calls `AddComposers()`. A site writing the entirely natural
`.AddWebsite().AddComposers().AddUBookItTheme("x").Build()` got a composer that had already
run: no expander, no boot check, and — because the theme *options* were still configured —
the package's stylesheet suppressed as well. The site rendered the package's own views with
its own CSS switched off, silently. That is strictly worse than the failure this design
exists to prevent, and it was found by QA, not by this design's own guard.

Two lessons are recorded rather than absorbed:

- The sentence "that reasoning is a mechanism and must be verified rather than trusted"
  was already in this section, and the mechanism was restated in five places instead of
  being checked once. A claim about a framework is checkable in minutes; a design note
  saying it should be checked is not a substitute for checking it.
- **Composer ordering could not have rescued it.** `ComposeAfterAttribute` throws unless
  given a type implementing `IComposer`, and Umbraco's view-engine setups are not
  composers — they are registered inline by `AddWebsite()`. There was nothing to name.
  (The v8-era `[RuntimeLevel]` gate on composers is also gone; only
  `RequireRuntimeLevelAttribute`, an MVC action filter, remains.)

*Rejected: documenting the ordering and asking sites to register it themselves.* It is a
silent failure whose symptom is indistinguishable from "the theme isn't finished yet".

*Rejected: relying on the post-configure alone.* It is the same species of claim that
already failed once — "Umbraco does not post-configure `RazorViewEngineOptions`" is true
today and is not the package's to guarantee. So the boot check below verifies the outcome
in the running site, at every boot.

That "true today" is now evidenced rather than hoped for, against **both** the pinned
17.6.2 binaries and the 17.8.0-rc source: zero `IPostConfigureOptions<RazorViewEngineOptions>`
anywhere in Umbraco, exactly **two** `ViewLocationExpanders` mutations (the two setups
above, both `IConfigureOptions`), and only four files in the whole codebase that mention
`RazorViewEngineOptions` at all. **It does not make the boot check redundant, and the
reason is not "Umbraco might change it".** The enumeration is exhaustive over *Umbraco*,
not over the *application*: `RazorViewEngineOptions` is a public MVC options type, so any
other package a site installs — or the site itself — can post-configure it and land after
this one. Umbraco has no monopoly there, and the grep says nothing about it. The
enumeration also covers 17.8.0-rc while the invariant is "Umbraco 17+ (LTS)", i.e. every
future 17.x this package will never be rebuilt for.

**The guard states the guarantee, not the mechanism** — the lesson ⑬ paid two QA rounds
for. It does **not** assert that an expander is registered, nor where, nor that a composer
was used. It runs the *whole* expander chain as the framework runs it and asserts the
**first candidate resolves to the theme's view**. A guard asserting registration would have
passed on the spike's broken first run.

### D4. Runtime falls back per view and logs; the completeness check is public and runs in the theme author's build

The spike established that **per-file fallback is the framework's default and is entirely
silent**: a theme supplying `Booking/Default` but not `Booking/Unavailable` renders a mix of
two designs with no diagnostic at all. That silence is the thing being fixed; the fallback
itself is not.

So the two halves are separated:

- **At boot**, the package enumerates the registered theme's views and logs an error naming
  each missing view. The package's views fill the gaps and the site stays up.
- **The check is public API**, so a theme author calls it from their own test suite and an
  incomplete theme fails **their** build, before any site sees it.

This was Chris's call on 2026-08-28, revisited once against the original all-or-nothing
decision. It keeps the mistake caught where it is made while a packaging error degrades a
production site rather than taking it down. The residue to be honest about: a theme author
who never runs the check still ships a mixed rendering, and only a log line reports it.

The check is over **names and model types**, not names alone. A theme view at the right path
with the wrong `@model` satisfies a name check and then throws at render time, on a visitor's
request — which is a worse failure than the one being prevented.

Mechanism is proven: `ApplicationPartManager.PopulateFeature(new ViewsFeature())` yields each
`ViewDescriptor.RelativePath` — the exact theme identifier — and `descriptor.Type.Assembly`,
so both the enumeration and the "which assembly supplied this" reporting are available.

*Rejected: failing boot.* It was the original decision and remains defensible, but it turns a
theme packaging slip into a site outage.
*Rejected: a Development-fails / Production-logs split.* The two environments would disagree
about whether a theme is valid, so the failure surfaces only where it is least likely to be
seen before a deploy.

### D5. When a theme is active the styles partial emits nothing, unless the theme opts in

`~/Views/Shared/UBookIt/_Styles.cshtml` was built as exactly this off-switch, and its own
comments say so. A theme owns the markup, so the package's classes largely do not exist in
the rendered document and the stylesheet would style nothing while still being able to
collide. A theme that reuses the package's partials as building blocks opts back in.

This preserves ⑬'s guarantee that the partial is the **only** route by which the package
emits its own styling — a second `link` element anywhere would survive the theme and fight
it — and it is why that requirement was written that way before this change existed.

### D6. The partials stay loaded by absolute path, and become promised surface

A theme's own `Service.cshtml` is free to simply **not call** the package's partials.
Precompiled views cannot be *replaced* but are perfectly *callable* — already spike-verified:
a site file rendering a package partial by absolute path resolved, and the engine's complaint
about a deliberately wrong model named `UBookIt.Web.Rendering.IBookingFormView`, which it
could only know by finding the compiled view and reading its `@model`.

So the four partials go from "protected" to **optional building blocks**, the absolute paths
stay exactly as they are, and ⑪'s 598 rendering tests are untouched. `ViewInventory` scans
`src/UBookIt.Web/Views` on disk, so a theme fixture living under `tests/` cannot enter the
shipped set — but a fixture placed under `src/UBookIt.Web/Views` **would**, which is a
landmine worth naming.

The cost, accepted deliberately: `~/Views/Shared/UBookIt/_X.cshtml`, `IBookingFormView` and
`ServiceFormModel` become a compatibility promise. They are already `public`, but
public-by-accident is not promised.

*Rejected: making each partial independently overridable.* That is Umbraco Forms' model. It
requires changing how the flow views load their partials, which is precisely what ⑪'s tests
assert over — and a design that protects `_Times` and `_DateAndLength` lets a DevExpress
theme restyle headings and nothing else, which is the opposite of the point.

### D7. The accessibility claim gains a fourth boundary

Invariant 5's three-way split is: ours always (markup) / ours by default and theirs on token
override (contrast, focus, target size) / never ours (host page). A theme replaces the
markup, so the first category — labelling, grouping, programmatic relationships, keyboard
operability, reading and focus order — is not ours for a themed rendering.

The fourth category is **theirs entirely when a theme replaces the markup**, on exactly the
reasoning already agreed for CSS: we do not take responsibility for code we did not write.
Approved by Chris on 2026-08-28.

Two clauses that must survive, because they are what keep this a narrowing rather than an
escape hatch:

- The **no-author-stylesheet** anchor stays load-bearing **for the shipped views**. It is
  what makes the CSS narrowing defensible, and it is untouched here.
- The package **still SHALL NOT claim** conformance for a themed rendering — in either
  direction. It does not assert a theme is accessible, and it does not quietly let its
  published accessibility statement keep reading as though the shipped markup always renders.

### D8. Proving it without runtime compilation

The spike ran on a development site, which carries
`Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation` via `Umbraco.Cms.DevelopmentMode.Backoffice`.
⑫'s override finding had exactly this gap, and `packaging` already carries a requirement that
documentation **SHALL NOT assert more than has been measured** because of it. Repeating the
habit while citing the lesson would be worse than not citing it.

`UBookIt.Tests.Rendering` is the vehicle, and it is already built for this: it references
`UBookIt.Web` and **deliberately not** `UBookIt.TestSite`, precisely so runtime compilation is
absent from its dependency closure. An end-to-end theme test there proves precedence in the
configuration a production site actually runs, using a fixture theme RCL under `tests/`.

## Risks / Trade-offs

- **Registration order is wrong and nothing says so** → the guard runs the whole expander
  chain and asserts the theme candidate resolves first; the TestSite additionally exercises
  the failure by asserting a deliberately mis-ordered registration is detected.
- **Precedence might not hold without runtime compilation** → D8; measured in the rendering
  suite, which has no runtime compilation by construction. If it does not hold, the design
  needs rethinking rather than adjusting, and that must surface at apply time rather than
  after release.
- **A theme author ships an incomplete theme without running the check** → a mixed rendering
  and a log line. Accepted residue of D4, recorded rather than argued away.
- **A theme view with the wrong `@model` throws on a visitor's request** → the completeness
  check covers model types, not just names.
- **Changing the theme at runtime leaves a stale view-location cache** → the theme is fixed
  at startup, stated as a constraint of D2.
- **A theme fixture placed under `src/UBookIt.Web/Views` silently enters the shipped view
  set** and every ⑪ rule starts asserting over a test fixture → fixtures live under `tests/`;
  named here because the failure looks like passing tests.
- **The published accessibility statement becomes misleading for themed sites** → D7, and the
  statement gains the themed boundary explicitly rather than by omission.
- **Reversing a documented promise** → no external consumers yet (confirmed by Chris), so this
  is an internal-consistency obligation, not a compatibility one: `docs/booking-page.md`, the
  `packaging` spec and the accessibility statement must agree afterwards.

## Guarantee diff for the three MODIFIED requirements

A `## MODIFIED Requirements` entry **replaces its requirement wholesale**, and anything the
old version guaranteed and the new one forgets to restate is deleted with nothing in the
diff that looks like a deletion. Change ⑧a lost a guarantee exactly this way. So each
replaced requirement is diffed by guarantee, not by prose.

**`default-frontend` → Accessible, semantic markup (WCAG 2.2 AA)** — 11 scenarios and every
prose clause **carried forward**, including the two the narrowing would most plausibly have
dropped: the **no-author-stylesheet** clause and the paragraph explaining why it is
load-bearing, and the **"one bar, stated once"** clause (extended to cover the new split
rather than replaced by it). The contrast-guarantee paragraph, its two-defeated-versions
history, and the 1.4.11 decorative-border exemption are carried verbatim. **Changed:** six
scenarios gain "by the package's own views" so they say which rendering they assert over —
a qualification, not a weakening, since a themed view is not a rendering the package
produced. `The narrowing does not reach the markup clauses` is likewise qualified and its
"before this change" re-anchored to the stylesheet change, which is what it referred to.
**Added:** the shipped-versus-themed scoping clause, its "reaches themed views and nothing
else" counterpart, and one scenario. **Deliberately dropped: nothing.**

**`default-frontend` → A default stylesheet ships, and makes no colour decision** — all six
scenarios and every clause carried, including both typography floors, the
no-restyling-native-controls clause and its sole target-size exception, and the
single-emission-route obligation. **Changed:** `Opting in is one line` now says "with no
theme active", because with a theme active and no opt-in the partial emits nothing — this
is the one scenario theming genuinely falsifies. `There is one emission route` is
*strengthened* to hold "with or without a theme active". Two "before this change" phrases
re-anchored to "the stylesheet change" now that a later change is in flight. **Deliberately
dropped: nothing.**

**`default-frontend` → The styling contract is a stable class vocabulary** — all four
scenarios and both prose clauses carried, including the id clause protecting the
accessibility contract from a restyle. **Changed:** subject qualified to the package's own
views; the id clause's "by this change" re-anchored to the change that introduced it, since
wholesale replacement would otherwise make it read as *this* change. **Added:** one clause
and one scenario stating a theme is not held to the vocabulary. **Deliberately dropped:
nothing.**

**`packaging` → What the package owns and what the site owns is documented** — the ownership
list, the not-claiming-a-route-we-lack clause, the not-asserting-beyond-measurement clause
and its dev-site caveat, and the first two scenarios all carried. **Changed:** the ownership
list gains "how a site changes the markup inside the flow". The not-measured clause gains a
second limit binding the *theme* route to the same standard — the honest reading of the
lesson, since the theme was measured on a development site too. **Deliberately dropped: one
scenario**, `An unavailable route is stated as unavailable`, whose THEN was "the
documentation says plainly that this is not yet supported". It is dropped because it is now
false, and it is **superseded by two stronger scenarios**, not simply deleted: the theme
route is documented and works, *and* the site-file override is still documented as not
working. Dropping only the first half would have removed the warning about the trap a site
author still meets first.

## Open Questions

- Whether the theme's view-name set should be published as constants, as a manifest the theme
  declares, or both. Leaning constants plus the check; settle at apply time — it is mechanism,
  not guarantee.
- Forced-colors mode remains **unverified** from ⑬ and is not claimed. Untouched by this
  change, and must not acquire a tick by adjacency.
- ⑤(d)'s human keyboard-only and screen-reader pass is still owed and is likewise untouched.
