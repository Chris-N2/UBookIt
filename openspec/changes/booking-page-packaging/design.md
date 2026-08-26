## Context

Everything uBookIt renders is installable and tested except the way a site puts it on a
page, which is undocumented manual work evidenced only by a dev harness
(`src/UBookIt.TestSite/Views/UbookitBookingTest.cshtml`). This change ships the missing
piece.

It is also the first thing the package installs into a consumer's site that removing a
NuGet reference does not undo. The risk in this change is therefore not the code — which
is small — but what it writes into other people's databases, and what a later release
does to it.

## The measurement this design rests on

Spiked 2026-08-25 against the pinned **Umbraco 17.6.2** (not the v18 source in `ref/`),
by installing schema through a package migration plan, editing it over the
management API as an editor would, shipping a "v2", and restarting:

| Editor's change | When an import runs |
|---|---|
| Customised the template's Razor | **destroyed** — reset to the shipped text |
| Changed the doctype's description | **overwritten** with ours |
| Added their own property | survived |
| — (we added a property in v2) | arrived on the existing install |
| Deleted the document type | **never restored** (see Risks) |

This is what an import *does*. **When** one runs is D1's subject, and the two were
conflated in the first version of this design.

Two details matter more than the table:

1. **The template's XML was identical in v1 and v2 and was destroyed anyway.** An import
   replaces the whole manifest, not the parts that changed — so a release touching only
   the document type still rewrites the template. A test that changed the template
   deliberately would have missed this.
2. **The Umbraco documentation says the opposite** — *"Existing schema or content will
   not be overwritten in this process."* True of content, false of schema. The v18
   source agrees with the measurement, so this is a documentation error rather than a
   version difference.

## Goals / Non-Goals

**Goals**

- A site author can publish a booking page without writing Razor.
- Nothing the package installs holds anything a site would mind losing.
- What an upgrade replaces is documented, not discovered.

**Non-Goals**

- The `RenderController` / TempData / anti-forgery-guard work — see D4.
- Creating content, backoffice UI, or any change to the shipped views.

## Decisions

### D1 — A run-once custom `PackageMigrationPlan`, not `AutomaticPackageMigrationPlan`

**This decision was made the wrong way round first, and the reasoning was inverted.
Both are recorded rather than tidied away, because the mistake is the instructive
part.**

The original text claimed a custom plan "defaults `IgnoreCurrentState` to `true`,
re-executing every migration — so the 'more control' route is the more dangerous one".
That is backwards. `MigrationPlan.cs:40` defaults it to **`false`**;
`AutomaticPackageMigrationPlan.cs:50` **overrides it to `true`**. The custom route is
the safer one. I took that claim from a documentation summary rather than from source I
had already read — the same failure as D5 below, in the same change.

| | Automatic | Custom (chosen) |
|---|---|---|
| Final state | hash of `package.xml` | explicit state ids |
| Re-runs when the manifest changes | **every time** | **never** |
| Consequence for a site | template and doctype fields overwritten on any release touching schema | site's copy left alone |

**Measured**, not read: with the custom plan installed, editing the shipped template
and then changing `package.xml` and restarting left the edit intact **on disk and in
the database**, and the manifest's changed description never reached the site.

The cost is real and is the right one: shipping new schema now needs an explicit new
step, and adding one re-imports the whole manifest. Overwriting becomes a decision
someone makes rather than a side effect of editing a file.

**The cost this decision does carry, found by QA and measured.** Run-once removes
self-healing. If `RunSchemaAndContentMigrations` is `false` at the boot that installs
uBookIt, Umbraco skips the import — `ImportPackageBuilderExpression.cs:97` returns
early — but the migration still **completes**, so the executor records the final state.
Under run-once that state is terminal: turning the setting back on changes nothing, and
the site has uBookIt installed with no schema, no error, and one INFO line in an old
boot log.

The automatic plan was self-healing here, because any manifest change produced a new
hash and a fresh import. So this is a genuine trade rather than a free win: run-once
converts a transient misconfiguration into a permanent one, in exchange for never
destroying a site's edits.

Taken anyway, and the reasoning is the asymmetry between the two failures. The
misconfiguration is **rare, detectable and recoverable** — one row deleted and a
restart, documented. The overwrite it replaces was **routine, silent and
unrecoverable**: it destroyed work on every release touching schema, with no way to get
it back. A rare recoverable failure beats a routine unrecoverable one.

What follows for the documentation is not optional: `docs/booking-page.md` previously
*recommended* that setting as a way to freeze schema. It now warns against having it set
at install time and gives the recovery. Shipping the recommendation unqualified would
have been this change actively steering sites into its own worst failure.

**A trap for anyone revisiting this: the plan type cannot be changed after release.**
Booting a custom plan against a site holding an automatic plan's hash state fails hard
— `BootFailedException: The migration plan "uBookIt" does not support migrating from
state "b2808c89-…"`, 500 on every request, recoverable only by deleting the
`umbracoKeyValue` row. Measured, because it happened here. Nothing had shipped, so it
cost nothing; after release it would have bricked every existing install.

### D2 — The shipped template is a one-line delegate, and that is a constraint

The template delegates to the booking ViewComponent and carries nothing else.

Under D1's run-once plan the template is not destroyed by ordinary releases, so this is
no longer forced by *every* release — but it is still the right shape. A release
carrying a migration step re-imports the whole manifest, and the template is the only
thing in there a site would plausibly have edited. Keeping it a delegate keeps the cost
of that decision near zero.

It also keeps the decision honest: if the shipped template held something valuable, the
pressure would be to avoid adding migration steps at all, which would mean never
shipping schema changes existing sites need.

The shape is already idiomatic here — `BookingFlow/Default.cshtml` is exactly such a
delegate.

The rule to carry forward, because it will be tempting to add "just a wrapper div"
later: **anything worth keeping in the shipped template is worth not shipping there.**

### D3 — There is no view-override mechanism, and the package must not claim one

**This decision asserted the opposite and was wrong. QA disproved it by measurement,
and the original claim is left visible because the error is the useful part.**

It said: "uBookIt renders through ViewComponents and Razor views under
`Views/Shared/UBookIt/`, and MVC view resolution already lets a site win by placing its
own file at the same path. That *is* the theme mechanism."

It is not. A site file at the same path as one of the package's views is **never
consulted** — verified live, in both directions. The mechanism, in
`CollectibleRuntimeViewCompiler.cs:209-218`: if
`!ChecksumValidator.IsRecompilationSupported(precompiledView.Item)` the compiler
returns `SupportsCompilation = false` and uses the precompiled view *as-is*, with
`ExpirationTokens = Array.Empty` ("Never expire because we can't recompile").
`UBookIt.Web.dll` carries `RazorCompiledItem` entries but **zero
`RazorSourceChecksum`** entries, so that branch always wins.

Confirmed as precedence rather than discovery: runtime compilation is live (editing the
site's own template with the site running took effect with no rebuild) and the override
still lost.

**Measured on a development site only, and this is stated rather than glossed.** The
code path above belongs to `Umbraco.Cms.DevelopmentMode.Backoffice`; a production site
uses the standard non-runtime compiler, where precompiled views are all there is, so
overrides should fail there too — but that is reasoning, not measurement. It was not
measured because a published site needs a connection string that lives in a
user-secrets file, and reading it to settle a point already settled in the direction
that matters was not worth it: the mechanism only gets *more* absolute without runtime
compilation.

Recorded explicitly because this change has already shipped one confident, wrong claim
about exactly this, and the correction must not quietly ship a second.

**Why this is not simply a gap we failed to fill.** The Clean starter kit ships front-end
files the same way — its manifest carries 14 templates, its partial views and 466KB of
stylesheets — and `Clean.Core` contains **zero** `.cshtml`, so nothing is precompiled and
the precedence problem cannot arise. Clean's published answer to customisation is to
**uninstall the view-shipping package** (`dotnet remove package Clean`). That is an honest
admission that no override mechanism exists, from a mature and widely used package.

uBookIt cannot copy that: its assembly holds the ViewComponents and the delivery API, and
⑪'s 598 rendering tests render the *compiled* views.

**What this change does instead**: says so, plainly, in `docs/booking-page.md`, and offers
the route that does work — a site adds its own template and makes it the document type's
default. The package never touches a template it did not declare.

**What the theming change will need** (its own change, scoped after this): keep compiled
views as the working default and add a convention path the package deliberately does
**not** precompile, so nothing competes. That needs an `IViewLocationExpander` **and** a
change to how the shared partials are loaded — they use absolute paths
(`~/Views/Shared/UBookIt/_X.cshtml`) that no expander can redirect.

### D4 — Why the POST-path rework is not here, and what the next change should test first

⑤ design D3 recorded the intent as "a `RenderController` owning GET+POST, which also
removes the TempData PRG workaround". Two reasons that is not attempted here:

**It may rest on a false premise.** Route hijacking dispatches by template name rather
than by verb, and Umbraco's documentation presents `Html.BeginUmbracoForm` + `ufprt` as
the mechanism for form posts. Chris's recollection, from hitting this directly, is that
`RenderController` is not designed to take a POST and a surface controller is still
needed. Nothing here is measured, and a design resting on unmeasured behaviour is the
defect QA caught last change.

**The real obstacle is the ViewComponent boundary, and it is not what ⑤ recorded.**
Established here by reading the code rather than assuming, after Chris confirmed from a
live v17 project that a surface controller *can* return `CurrentUmbracoPage()`:

- The form is rendered by a **ViewComponent**, invoked from the page's template.
- `BookingViewComponent` reads the failure payload out of **TempData itself**
  (`BookingViewComponent.cs:23,34`), and the surface controller stashes it and redirects
  (`BookingSurfaceController.cs:121,167`).
- On a re-render the template invokes the component with only `resourceId`. **ModelState
  does not cross a ViewComponent boundary**, and the template has no model to pass.

So returning `CurrentUmbracoPage()` instead of redirecting **would not remove TempData**.
TempData is currently the only channel from the POST handler to the thing that renders
the form. Any design that treats "stop redirecting" as the fix will find this out late.

**Which rehabilitates the `RenderController` — for a reason ⑤ did not state.** Its value
is not that it owns the POST (it does not, and a surface controller is still needed for
that). Its value is that it gives the **page a model**, which is the only way a template
can hand a failed submission to the ViewComponent. The alternatives are to have the
surface controller return a view that renders the flow directly with an explicit model,
bypassing the template's invocation, or to give the ViewComponent a parameter the
template fills — and both still need the page to carry the payload somehow.

So the deferred change's likely shape, to be **measured before it is designed**:

1. The surface controller keeps the POST and the anti-forgery token.
2. On failure it returns `CurrentUmbracoPage()` rather than redirecting.
3. Something gives the page a model so the failure reaches the ViewComponent without
   TempData — a `RenderController` being the obvious candidate.
4. `BeginUmbracoForm` therefore **stays**, and with it ⑪'s two DI registrations. They
   should stop being described as scaffolding awaiting demolition.

The spike for that change should start at (3), since (1) and (2) are now known to be
possible and (3) is the part nobody has measured.

### D5 — The manifest/namespace coupling is asserted, but it does NOT fail silently

**Corrected. The original text claimed the opposite and was wrong in four places,
including two shipped source comments.**

It said: "there is no error: the plan finds nothing and the site comes up without the
schema… the failure mode this project keeps meeting: a thing that reports success by
doing nothing."

Measured: removing the `<EmbeddedResource>` entry and booting produces
`BootFailedException → IOException: Missing embedded files for planType: …` and **HTTP
500 on every request**. `PackageMigrationResource.GetEmbeddedPackageDataManifestHash`
throws, and it is reached while the plan is constructed during boot. Loud, named,
unmissable.

I asserted a failure mode I had not tried, in a change whose entire premise is
measuring rather than assuming, and then wrote it into the source. The correction is
kept visible so the next reader sees the shape of the mistake and not just its fix.

The test stays, for a different and smaller reason: a red test names the cause in a
second, where a failed boot costs a deploy to diagnose. It is a convenience, not a
safety net, and it now says so.

**What genuinely does fail silently is what the manifest may contain** — a template
that grows markup, or a manifest that starts writing files into a site. Those raise
nothing at all and surface on someone else's site later. `PackagingTests` guards those,
and that is where the real value is.

## Risks / Trade-offs

- **This writes schema into consumers' databases.** Uninstalling the NuGet package does
  not remove it. Nothing here mitigates that beyond keeping what is installed small and
  documented; it is inherent to shipping a document type, and it is why D2 is a
  constraint rather than a preference.
- **The doctype's name, icon and description are reverted on upgrade.** Minor, but an
  editor who renames the type will see it renamed back, and only documentation prevents
  that being alarming.
- **An editor who deletes the document type never gets it back. Measured by QA, and the
  earlier inference here was wrong in the dangerous direction.** This previously read
  "likely re-created, since the import creates what it does not find". The import never
  runs: pending is decided by comparing the stored state to the plan's final state, and
  deleting the type does not change the stored state. QA demonstrated it on a virgin
  database — type installed, node removed, restart, still absent, key-value row
  unchanged.

  **D1's run-once plan makes this worse, not better.** Under the automatic plan any
  manifest change would eventually have restored it; now nothing short of a new
  migration step will, and recovery otherwise means deleting a `umbracoKeyValue` row by
  hand. The site is left with a template pointing at a type that does not exist.

  Not fixed here, and the reason is that the honest fix is not obvious: re-creating
  deleted schema on every boot would override a deliberate act by an editor. Recorded
  as an obligation, and `docs/booking-page.md` must not imply that deletion is
  recoverable.

- **An editor deleting a shipped *property* is still untested.** Probably re-added on
  the next import, since properties are additive — but that is inference, and it is
  recorded as unmeasured rather than asserted.
- **`UbookitBookingTest.cshtml` becomes redundant as documentation** while remaining
  useful as a harness for `?flow` and `?resourceId`, which no shipped page exposes.
  Decide its fate explicitly; deleting it by reflex would remove the only way to reach
  those overrides.

## Migration Plan

None required of consumers on first install. On upgrade, the behaviours in the table
above apply, and the documentation shipped by this change is what makes them
predictable. No EF Core migration, no schema change to uBookIt's own tables, no public
API change.

## Open Questions

- Should the shipped document type allow itself at the root, or expect composition into
  a site's own page types? Allow-at-root is friendlier for a first install and is what
  the spike used; composition is tidier for real sites. Settle at apply, and note that
  changing the answer later is an upgrade-visible change to `AllowAtRoot`.
- Does the shipped type need any properties at all, or is the page's existence the whole
  configuration? The flow reads its state from the query string today.
