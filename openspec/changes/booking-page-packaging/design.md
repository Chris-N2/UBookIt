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
by installing schema through an `AutomaticPackageMigrationPlan`, editing it over the
management API as an editor would, shipping a "v2", and restarting:

| Editor's change | After upgrade |
|---|---|
| Customised the template's Razor | **destroyed** — reset to the shipped text |
| Changed the doctype's description | **overwritten** with ours |
| Added their own property | survived |
| — (we added a property in v2) | arrived on the existing install |

Two details matter more than the table:

1. **The template's XML was identical in v1 and v2 and was destroyed anyway.** The
   manifest re-imports whole whenever its hash changes for any reason, so a release
   touching only the document type still wipes template customisation. A test that
   changed the template deliberately would have missed this.
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

### D1 — `AutomaticPackageMigrationPlan`, not a custom `PackageMigrationPlan`

The automatic plan tracks state as a hash of the embedded manifest and re-runs when it
changes. A custom `PackageMigrationPlan` offers finer control and defaults
`IgnoreCurrentState` to `true`, re-executing every migration — so the "more control"
route is the more dangerous one here, which is the opposite of the usual trade.

Take the simple route. It has one behaviour, it is the documented path, and its
behaviour is now measured.

### D2 — The shipped template is a one-line delegate, and that is a constraint

The template delegates to the booking ViewComponent and carries nothing else.

This is **forced**, not chosen. Any content in that template is destroyed by the next
release that changes the manifest. A one-line delegate makes the overwrite a non-event,
and the shape is already idiomatic here — `BookingFlow/Default.cshtml` is exactly such a
delegate.

The rule to carry forward, because it will be tempting to add "just a wrapper div"
later: **anything worth keeping in the shipped template is worth not shipping there.**

### D3 — Customisation is by view override; this is the Forms "themes" pattern, mostly already built

Umbraco Forms solves this with themes, because Forms renders the fields itself. uBookIt
renders through ViewComponents and Razor views under `Views/Shared/UBookIt/`, and MVC
view resolution already lets a site win by placing its own file at the same path. That
*is* the theme mechanism, and it is what the parked branding work was for.

So this change ships no theming machinery. It ships a template thin enough that the
existing override path is the only customisation surface — which is also the only one an
upgrade cannot touch, since the package installs nothing there.

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

### D5 — The manifest/namespace coupling is silent when wrong, so it is asserted

`AutomaticPackageMigrationPlan` resolves its manifest as an embedded resource named for
the plan type's namespace. Get it wrong — wrong folder, missing `<EmbeddedResource>`,
renamed namespace — and there is no error: the plan finds nothing and the site comes up
without the schema.

That is the failure mode this project keeps meeting: a thing that reports success by
doing nothing. It gets an explicit test that the resource exists and its name matches
the plan's namespace, and the test must fail if either moves.

## Risks / Trade-offs

- **This writes schema into consumers' databases.** Uninstalling the NuGet package does
  not remove it. Nothing here mitigates that beyond keeping what is installed small and
  documented; it is inherent to shipping a document type, and it is why D2 is a
  constraint rather than a preference.
- **The doctype's name, icon and description are reverted on upgrade.** Minor, but an
  editor who renames the type will see it renamed back, and only documentation prevents
  that being alarming.
- **An editor deleting a shipped property or the whole document type is untested.** It
  is likely re-created, since the import creates what it does not find — but that is
  inference, not measurement, and it is recorded as unmeasured rather than asserted.
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
