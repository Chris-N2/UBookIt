## Context

The package renders fourteen Razor views and ships no CSS. Nineteen `ubookit-*`
classes exist in the markup as hooks that nothing uses, and eleven ids exist as
accessibility targets that several requirements depend on. A consuming site therefore
receives unstyled semantic HTML and must write every rule itself, against class names
the package has never promised to keep.

Two constraints shape everything below.

**The package is a component in someone else's page, not a page.** WCAG conformance is
defined for pages. The flow is placed inside a host layout, inside a host stylesheet,
beneath a host heading outline. Five AA criteria — 1.4.3, 1.4.11, 2.4.11, 2.4.13,
2.5.8 — are pure CSS and, with no stylesheet shipped, are already entirely the site's.

**Precompiled views cannot be replaced by a site, but they are callable.** Measured
previously, and re-measured for this change: a site-authored view can render the
package's precompiled partials by absolute path. That is why the theme mechanism which
follows this change can be all-or-nothing over shells while leaving the accessibility
partials in place — and it is why this change does not need to touch view resolution
at all.

Both unknowns this change depended on were measured before it was written, not during
it.

**Measurement 1 — the delivery mechanism works and needs no csproj change.**
`dotnet pack` on `UBookIt.Web` exactly as it stands emits `staticwebassets/ubookit.css`
together with `build/Microsoft.AspNetCore.StaticWebAssets.props` and
`build/Microsoft.AspNetCore.StaticWebAssetEndpoints.props`. A live request to
`_content/UBookIt.Web/ubookit.css` returned `200`, `Content-Type: text/css`, the
correct body, with `ETag` and `Last-Modified`. Note precisely what each half covers:
the package listing covers the NuGet consumer path, and the live `200` came through the
TestSite's `ProjectReference`. The same props wire both, but a NuGet-installed consumer
has not itself been hit with a request — see Risks.

**Measurement 2 — a site view resolves a precompiled package partial by absolute
path.** Probed by calling `Html.PartialAsync("~/Views/Shared/UBookIt/_Times.cshtml", new
object())` from the site's own template — a deliberately wrong model, so that the
failure mode is the answer. The engine complained about the *model type* and named
`UBookIt.Web.Rendering.IBookingFormView`, which it can only know by having found the
compiled view and read its `@model` directive; a resolution failure would have said
"was not found" and listed the searched locations. This is not needed by this change,
but it is the assumption the next one rests on and it was cheap to settle here.

## Goals / Non-Goals

**Goals:**

- A consuming site can make the flow look like the rest of its site, cheaply, without
  forking a view.
- The package makes no appearance claim it cannot substantiate, and says where its own
  responsibility ends.
- The emission seam a future theme needs in order to suppress package CSS exists from
  the start rather than being retrofitted.
- The class and token surface becomes a stated contract while changing it is still
  free.

**Non-Goals:**

- The Razor theme mechanism (one theme, package-wide, all-or-nothing over ten named
  views). Its remaining unknown — whether an RCL's precompiled theme view wins an
  `IViewLocationExpander`'s first candidate — is deliberately unsettled here.
- An opinionated default skin, a colour palette, an editor-facing stylesheet picker,
  JavaScript, restyling native controls, or changing any id.

## Decisions

### D1 — Ship layout and no colour decision ("posture A"), not a neutral palette

**Chosen:** the stylesheet styles arrangement, spacing and the time grid. Colour and
typography inherit from the host page. Emphasis uses `currentColor`, border weight and
font weight, plus colours *derived* from `currentColor`.

**Rejected:** a complete neutral palette the site re-tokens to rebrand.

The requirement is "make it possible for somebody to make it look like their site".
Inheriting *is* looking like their site, achieved with zero configuration; a
re-tokenable palette only wins for someone who wants a look unlike their own site,
which is not the ask. The accessibility consequence is the decisive one, and it
inverts an earlier idea worth recording as rejected: an intermediate design proposed
shipping a palette and asserting every foreground/background pair at ≥4.5:1 in a test.
Posture A produces a **stronger** guarantee with no test at all — *we ship no colour
pair that can fail, because we ship no colour pair* — and it is correct in dark mode
and forced-colors mode for free, which a shipped palette is not.

The honest cost: on a site with minimal CSS the flow looks plain. That is accepted
deliberately, and the repo owner's framing is the one to keep — *"we built it, you
style it how you want."* If a finished standalone appearance is wanted later it belongs
in a separate optional skin that nothing depends on, so that declining it costs
nothing.

### D2 — Token defaults are fallbacks at the point of use, never declarations on an element

**This is the decision most able to be wrong while looking right, so it is stated
before any token is chosen.**

```css
/* WRONG — and every line of it reads as correct */
.ubookit-booking { --ubookit-color-error: currentColor; }
/* a site writes :root { --ubookit-color-error: … }  →  LOSES.
   Custom properties inherit, so our declaration on the CLOSER element wins. */

/* RIGHT — nothing to beat */
.ubookit-field-error { color: var(--ubookit-color-error, currentColor); }
```

"A site's tokens drop over our defaults cleanly" is the change's headline claim, so
the wrapper-declaration form would make it false *at the mechanism level* while each
individual declaration passed review. It fails silently, in the direction of appearing
to work — which is the failure shape this project keeps meeting. Hence a spec scenario
whose job is to fail when the wrong form is used, and a guard test rather than a
convention.

### D3 — Deliver as a static web asset, not through the package manifest

**Chosen:** `wwwroot/ubookit.css` in `UBookIt.Web`, served at
`_content/UBookIt.Web/ubookit.css`.

**Rejected:** the manifest `<Stylesheets>` route, which is what Clean does — and Clean
ships ~466KB of stylesheets that way.

Clean is a starter kit: it hands you the whole site and its published advice is to
uninstall the view-shipping package afterwards. uBookIt is a functional package that
stays installed. The decisive difference is what happens on upgrade. A manifest import
writes a real file into the site, and because the migration plan is run-once by design
that file is then never touched again. Some of this CSS carries accessibility weight —
focus visibility, derived-colour contrast, minimum target size — so a site-owned
stylesheet is one where an accessibility fix shipped later reaches nobody. A static web
asset is replaced by the upgrade like any other part of the assembly.

It also keeps `packaging`'s existing fence intact rather than widening it: the asset is
installed nowhere and creates no file the site owns.

### D4 — One partial emits the stylesheet, and it is deliberately also the theme's off-switch

A site adds one line to its layout. This buys three things: `<head>` placement (a
ViewComponent cannot reach `<head>` — `@section` does not cross that boundary, the same
boundary that forced the existing TempData workaround); cascade order, so package
defaults always precede site overrides; and a single place a future theme replaces in
order to turn package CSS off without the site changing anything.

That last point is the real reason this change precedes the theme change. An earlier
argument — that the Razor override contract cannot be sized until we know what CSS
covers — is **weak under D1**, because CSS ends up covering very little. Recording the
weak argument as rejected matters: the ordering is justified by the seam, not by the
sizing.

### D5 — Do not restyle native controls

`input[type=date]`, `select` and `button` keep their platform appearance. Platform
controls are accessible by construction, respect user preferences, and behave correctly
in forced-colors mode; restyling them is the most common way a booking UI loses its
accessibility, and a date input in particular has assistive-technology behaviour that
varies by platform and is not ours to reinvent. The only exception is a minimum target
size, which is a floor rather than an appearance. Radios and checkboxes are brandable
through `accent-color` alone, where the browser computes the indicator's contrast
itself.

### D6 — Do not touch focus indication in this change

Browser default focus rings adapt to the underlying background; overriding focus is a
common way to lose 2.4.11/2.4.13. The one case not automatically answered is the
`tabindex="-1"` wrappers the error summary links into, where a visible indicator on
arrival is desirable. Browsers do apply `:focus-visible` to programmatically focused
elements following keyboard interaction, so this may already hold. **Observe first,
decide after** — recorded as an open question rather than guessed at, because guessing
here means shipping a focus override we did not need.

### D7 — Keep flat class names, add a variant separator; one rename, two additions

The nineteen existing classes are already about 80% consistent — container-plural /
item-singular holds in both places it appears. There is exactly one structural
ambiguity: `ubookit-booking` alongside `ubookit-service-booking` cannot say whether the
second is a sibling block or a variant of the first. It is a variant. So the minimum
rule that removes the ambiguity is adopted — flat hyphenation for parts, `--` for
variants — rather than a wholesale move to BEM, which would rewrite names that are
already fine.

Work: rename `ubookit-service-booking` → `ubookit-booking--service`; add
`ubookit-field` to the six bare wrapper `div`s in `_DateAndLength` and `_YourDetails`;
add `ubookit-submit` to all three buttons. One submit name rather than three, because
the containing form already distinguishes them
(`.ubookit-date-form .ubookit-submit` versus `.ubookit-details .ubookit-submit`) — a
hook that costs no vocabulary.

**Granularity is kept deliberately.** Under D1 the classes are an API for other
people's CSS rather than a vehicle for ours, so collapsing `no-times`, `no-choices`,
`notice` and `hint` into one `message` class would remove override power to buy
tidiness nobody benefits from. Missing hooks are the defect here; abundance is not.

The seven `<h2>` and one `<h3>` get no class: `.ubookit-booking > h2` is unambiguous
and stable. One thing would change that — if heading *level* ever becomes configurable
to fit a host page's outline, the element selector stops being reliable and a class
earns its place.

### D8 — Narrow the accessibility claim, and treat the no-stylesheet clause as its anchor

The requirement is narrowed to markup-determined criteria rather than left overclaiming
or dropped. The carried-forward guarantee list was compiled *before* the delta was
drafted, because a `## MODIFIED` entry replaces a requirement wholesale and this
requirement carries nine SHALLs and five scenarios behind a one-clause change — the
exact shape in which this project has previously lost a guarantee with nothing in the
diff resembling a deletion. Eight SHALLs and all five scenarios carry forward
unchanged.

**The clause most at risk is the one that looks most like a casualty.** *"Each page
SHALL remain usable, with a logical reading and focus order, when no author stylesheet
is applied"* reads as a CSS clause, so a delta whose whole purpose is to move CSS
criteria to the site is precisely the drafting context in which it gets folded away as
superseded. It is the opposite: because the markup is operable with no CSS at all, no
stylesheet a site ships can render the flow *inoperable*, only unreadable. Drop it and
the narrowing becomes an excuse rather than a boundary.

### D9 — Do this before first publication, deliberately

Renaming a class is free now and a breaking change afterwards. The package has not been
published, so this is the last moment the vocabulary can be corrected without cost, and
that timing is a reason for the scope rather than an accident of it.

## Risks / Trade-offs

- **The D2 mechanic is easy to violate in a later edit, and violating it is silent.**
  A future contributor adding a token will reach for a wrapper declaration because it
  is the obvious form → a guard test that fails on a token default declared on any
  package element, mutation-checked by introducing exactly that.
- **The rendering suite re-runs over edited views.** Six wrapper `div`s, three buttons
  and two shells change, so the inventory, markup-invariant and every-state rules all
  re-evaluate. Additive attribute edits, low risk but not zero → run the full suite
  before and after and account for any delta rather than assuming additivity.
- **The NuGet consumer path is proven at package level, not by a request.** The
  `staticwebassets/` entry and both wiring props are present, and the same props serve
  the ProjectReference case that returned `200` → worth one request against a
  package-installed consumer before release; not worth blocking this change.
- **`color-mix()` is used for derived colours.** Widely available in current browsers,
  but it is the one modern-CSS dependency here → every use must degrade to something
  legible, which under D1 means the property simply inherits rather than breaking.
- **A host site's reset or normalize can fight package layout**, and we cannot see it.
  Accepted: it is the direct cost of D1's inheritance, and the alternative is the
  higher-specificity arms race D1 exists to avoid.
- **The published accessibility statement can drift from what the stylesheet does.** It
  is prose asserting a per-criterion split → tie it to the token list, so that adding a
  colour token forces the statement to be revisited.
- **Documented-but-unread and read-but-undocumented tokens** both make the contract a
  lie in different directions → a scenario checks both directions, not just one.

## Migration Plan

Additive and reversible. No schema change, no migration step, no manifest change, no
public C# API change. A site that adds nothing to its layout renders exactly the markup
it renders today — the only behavioural difference for an existing install is the class
rename, which nothing yet depends on because no stylesheet exists to depend on it.
Rollback is deleting the asset and the partial.

## Open Questions

1. **Do the `tabindex="-1"` wrappers get a visible focus indicator for free?** (D6.)
   Observe on the TestSite once the stylesheet exists, then decide. Do not pre-empt it
   with an override.
2. **CLAUDE.md invariant 5 states the wider claim this change narrows.** Amending a
   project invariant is the repo owner's call and is deliberately not done here. The
   wording that survives: the shipped default meets every AA criterion determined by
   markup; criteria determined by CSS are met by the shipped default tokens and become
   the site's once overridden.
3. **Where the styling documentation lives** — a new page, or a section of
   `docs/booking-page.md`. That page currently says the flow's internals are not
   customisable, which stays true of markup and now needs to separate appearance from
   markup.

## Deferred obligations — none are discharged here, and one only looks adjacent

No standing obligation is closed by this change, and none should be recorded as
closed by adjacency.

- **The `persistence` spec's requirement that the SQL Server requirement appear in
  package documentation** is *not* discharged. This change writes documentation, which
  makes it look like the moment — but a database requirement does not belong in a page
  about appearance, and the package's install/requirements documentation (the repo
  still has no README) is its own piece of work.
- **`[ValidateAntiForgeryToken]` being asserted by nothing** is not discharged: it is
  to be fixed by the next change that legitimately touches those controllers, and this
  change touches no controller.
- **The installation-path regression guard** and the **`delivery-api` Purpose
  correction** are untouched; neither is in this change's path.
