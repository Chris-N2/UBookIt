## Why

The package ships **zero CSS**. There are 19 `ubookit-*` classes in the views and
nothing styles any of them, so a consuming site gets unstyled semantic HTML and the
only route to "make it look like my site" is for the site to write the whole
stylesheet from its own reading of our markup — against class names we have never
promised to keep.

That absence also means the accessibility claim in `default-frontend` is already
wider than anything we can deliver. Five WCAG 2.2 AA criteria — 1.4.3 and 1.4.11
(contrast), 2.4.11 and 2.4.13 (focus appearance), 2.5.8 (target size) — are
determined entirely by CSS, and we ship none. The requirement says every flow "SHALL
meet WCAG 2.2 AA"; today those five are 100% the site's, whatever we say. This change
is the first honest opportunity to state the bar we actually hold.

Doing it now, before the Razor theme mechanism that follows it, is deliberate: the
emission seam this change builds is the same seam a theme uses to suppress our CSS,
and building it later means retrofitting it.

## What Changes

- **A default stylesheet ships** as a static web asset at
  `_content/UBookIt.Web/ubookit.css`. Measured: `dotnet pack` on `UBookIt.Web`
  already emits `staticwebassets/` plus the wiring props with **no csproj change**,
  and the asset serves `200 text/css` with `ETag`/`Last-Modified`.
- **It ships layout and spacing, and no colour decision.** Colour and type inherit
  from the host page, so the flow looks like the surrounding site with zero
  configuration. Emphasis that would normally need colour is carried by
  `currentColor`, border weight and font weight.
- **Native controls are not styled** — `input[type=date]`, `select` and `button` keep
  their platform appearance, which is accessible by construction and respects user
  preferences and forced-colors mode. The single exception is a minimum target size.
- **~14 design tokens** are published as the override contract, with every fallback
  declared **at the use site** rather than on a wrapper element (a default declared on
  a wrapper silently defeats every site override — see design).
- **One documented layout partial**, `UBookIt/Styles`, emits the asset. It gives us
  `<head>` placement and cascade order, and is the hook by which a future theme
  suppresses our CSS.
- **The class vocabulary becomes a stated contract**: one rename
  (`ubookit-service-booking` → `ubookit-booking--service`), two additions
  (`ubookit-field` on six bare wrapper divs, `ubookit-submit` on all three buttons),
  and a naming rule for variants. Ids are untouched — they are the accessibility
  contract, not the styling contract.
- **BREAKING (guarantee narrowing, deliberate): the WCAG 2.2 AA requirement is
  narrowed** to criteria determined by markup. Criteria determined by CSS are met by
  the shipped defaults and become the site's once it overrides a token. The published
  claim gains an ACR-style statement — met by the shipped default / depends on your
  tokens / depends on your page.
- **BREAKING (rendered markup): one class is renamed and two are added.** Free now and
  breaking after first release, which is why it is in this change; the package has not
  been published. From this change on, the class vocabulary and token names are a
  compatibility surface.
- **Not breaking, but new**: a site must add one line to its layout to receive any
  styling. Nothing regresses — no styles ship today — and a site that adds nothing
  renders exactly as it does now.

## Capabilities

### New Capabilities

None. The stylesheet is the shipped front end's own rendering rather than a separate
concern, so it belongs in `default-frontend`; only the delivery mechanism is
`packaging`'s business.

### Modified Capabilities

- `default-frontend`: **one requirement modified** — *Accessible, semantic markup
  (WCAG 2.2 AA)* is narrowed to markup-determined criteria. Its nine SHALLs and five
  scenarios have been enumerated in advance and each accounted for; eight SHALLs and
  all five scenarios carry forward unchanged. **Three requirements added** — the
  shipped stylesheet and its posture, the token override contract, and the class
  vocabulary as a stable contract.
- `packaging`: **one requirement added** — front-end assets ship as static web assets
  and never through the package manifest. This does not modify the existing fence
  (*The package installs nothing on paths the site owns*); it states the property that
  keeps the fence true once the package has a stylesheet to ship, and guards against a
  later change "helpfully" moving it into `<Stylesheets>`, which would hand the file to
  the site and strand every future accessibility fix.

## Non-goals

- **The Razor theme mechanism.** One theme, package-wide, all-or-nothing over ten
  named views is the next change. Its one remaining unknown — whether an RCL's
  precompiled theme view wins an `IViewLocationExpander`'s first candidate — is not
  settled and is not settled here.
- **An opinionated default skin.** Deliberate: shipping a look we cannot validate in
  an unknown host page is the thing this change exists to avoid. If a finished
  standalone appearance is wanted later, it belongs in a separate optional skin that
  nothing depends on.
- **An editor-facing stylesheet picker.** A ViewComponent cannot reach `<head>`, so a
  page-level choice can only emit a body `<link>` and buys a flash of unstyled content
  on the no-JS flow. If ever built: a central setting, never per-document-type.
- **A colour palette, or any token whose contrast we cannot compute.**
- **JavaScript.** The progressive-enhancement layer remains deferred.
- **Restyling native controls** beyond minimum target size.
- **Heading-level configurability.** The seven `<h2>` and one `<h3>` keep no class;
  `.ubookit-booking > h2` is unambiguous today. Revisit only if heading level ever
  becomes configurable to fit a host page's outline.
- **Changing any id.** Ids are aria and error-summary link targets and stay exactly as
  they are.

## Impact

**Code** — `src/UBookIt.Web`: a new `wwwroot/ubookit.css`; a new
`Views/Shared/UBookIt/_Styles.cshtml`; class edits to `_DateAndLength`,
`_YourDetails`, `Catalogue` and the two flow shells. No csproj change (measured). No
change to any view model, controller, endpoint or the manifest.

**Tests** — the views change, so `UBookIt.Tests.Rendering`'s inventory,
markup-invariant and every-state rules all re-run over edited views; additive
attribute edits, low risk but not zero. New coverage is needed for the class
vocabulary and, importantly, for the use-site fallback mechanic, which is the one part
of this change that can be wrong while looking entirely correct.

**Docs** — a new page for the styling contract (tokens, classes, the layout line) and
the ACR-style accessibility statement. `docs/booking-page.md` currently says the
flow's internals are not customisable; that remains true of markup and now needs to
distinguish appearance from markup.

**Requires a human decision, flagged not assumed** — CLAUDE.md invariant 5 states the
wider claim this change narrows. Amending a project invariant is the repo owner's
call; the wording that survives is recorded but deliberately not applied here.
