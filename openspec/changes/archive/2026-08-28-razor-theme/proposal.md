## Why

uBookIt's markup is currently fixed. A site can put the flow on a different page (⑫) and
restyle it through tokens and classes (⑬), but it cannot render *different controls* —
and that is the whole reason the front-end contract exists. `UBookIt.UI.DevExpress` is a
separate package whose entire purpose is to replace `_Times` and `_DateAndLength` with a
scheduler; without a theme mechanism there is nothing for it to plug into.

The blocker was never a missing idea, it was an unmeasured one. It is now measured: a
view precompiled into a **second RCL does win** an `IViewLocationExpander`'s first
candidate over the package's own precompiled view, confirmed on both ViewComponents
under real Umbraco page rendering. The three-tier customisation story — own template /
tokens and CSS / own controls — has been waiting for this last tier, and the tier below
it shipped last week.

## What Changes

- **A theme mechanism.** A theme is an RCL supplying Razor views at
  `Views/Shared/UBookIt/Themes/<theme>/Components/{Booking,BookingFlow}/<View>.cshtml`.
  A site registers one theme, package-wide, and the package's own views are used for
  anything the theme does not supply.
- **Registration that cannot be silently wrong.** The spike found that Umbraco registers
  two view-location expanders of its own which **prepend** six locations — including
  `/Views/Shared/{0}.cshtml`, where the package's own views live. Registering uBookIt's
  expander earlier therefore puts the theme *second* and the theme silently does nothing:
  the site renders correctly, just unthemed. Registration is a package-owned API whose
  guard asserts the theme candidate resolves first, not merely that an expander exists.
- **A completeness check the theme author can run.** The package publishes the set of
  view names a complete theme supplies and a check over a theme's own assembly. At boot
  an incomplete theme is **logged and the package's views fill the gaps**; the site stays
  up. The check is public so a theme author fails their own build instead of discovering
  it in production.
- **The shipped stylesheet stops emitting when a theme is active**, unless the theme asks
  for it. `~/Views/Shared/UBookIt/_Styles.cshtml` was built as this off-switch.
- **The four shared partials and the view models become a compatibility promise.**
  `~/Views/Shared/UBookIt/_{DateAndLength,ErrorSummary,Times,YourDetails}.cshtml`,
  `IBookingFormView` and `ServiceFormModel` are already `public`; a theme calls them as
  optional building blocks, so public-by-accident becomes public-by-promise.
- **The accessibility claim gains a fourth boundary.** Markup guarantees are guarantees
  about the views the package ships. When a theme replaces them, they are the theme's.
  This is the same narrowing already agreed for CSS, applied to the one category
  previously held unconditionally — and it requires an edit to **CLAUDE.md invariant 5**,
  approved by Chris on 2026-08-28.
- **A live documented promise is reversed.** `docs/booking-page.md` §"What you still
  cannot change" states the flow's markup is not customisable. It also predicts the fix
  would need "a path the package deliberately does not compile into itself" — which the
  spike disproved: the theme path *is* precompiled, into the theme's own assembly, and
  wins anyway. Both the promise and its predicted mechanism are corrected.

No **BREAKING** change: nothing existing changes behaviour for a site that registers no
theme. A site that adds nothing renders exactly what it renders today.

## Non-goals

- **Per-view theme selection, or a theme chosen per page or per document type.** One
  theme, package-wide. Per-form theming is Umbraco Forms' model, not this one; it
  fragments the view-location cache and buys nothing the three tiers do not already
  cover.
- **Making the four shared partials individually replaceable.** Under this design a theme
  simply does not call them. Precompiled views cannot be *replaced* but are perfectly
  *callable*, so ⑪'s 598 rendering tests are untouched — and any design that protected
  `_Times` and `_DateAndLength` would let a DevExpress theme restyle headings and nothing
  else, which is the opposite of the point.
- **Shipping a second theme, or any theme, from this repository.** The package ships the
  mechanism and the contract. Themes live elsewhere; the spike's throwaway RCL was
  removed.
- **A backoffice or editor-facing theme picker.** Inconsistent with one theme per site,
  and a ViewComponent cannot reach `<head>` anyway.
- **Claiming WCAG conformance on a theme's behalf**, or requiring a theme to meet the
  package's markup bar. The package cannot test code it did not write.
- **Changing the delivery API.** A headless consumer is unaffected; themes are a Razor
  concern.

## Capabilities

### New Capabilities
- `theming`: what a theme is, how one is registered so that it actually wins, what a
  complete theme supplies, what happens when one is incomplete, and which of the
  package's guarantees survive a theme and which pass to its author.

### Modified Capabilities
- `default-frontend`: the WCAG 2.2 AA markup requirement and the stable-class-vocabulary
  requirement are scoped to the views the package ships, since a theme replaces them; and
  the shipped stylesheet gains its documented suppression behaviour when a theme is
  active.
- `packaging`: the ownership-documentation requirement currently obliges the docs to say
  restyling the flow's markup is *not supported*. It now is, by a route that works — the
  scenario asserting the unavailable route is replaced, and the still-true statement that
  a **loose file override** never works is carried forward, because that remains the trap
  a site author will otherwise fall into.

## Impact

- **`UBookIt.Web`**: a view-location expander, theme registration and options, a boot-time
  check covering **both** resolution order and theme completeness with its logging, the
  public theme-contract constants, and a change to `_Styles.cshtml`'s emission condition.
- **Public API surface** (additive): `AddUBookItTheme` on `IUmbracoBuilder`, the theme
  options, the published view-name set, the completeness check, and the boot check type
  with its `Run()`. Plus the promotion of the four shared partial paths and
  `IBookingFormView` / `ServiceFormModel` to promised surface. The `IServiceCollection`
  registration seam is deliberately **internal** — it is the one path with no boot check,
  and publishing it would publish a silent gap in a capability about silent failure.
- **`UBookIt.Tests` / `UBookIt.Tests.Rendering`**: new coverage for resolution order,
  fallback, completeness and suppression. The existing 598 rendering tests are expected
  to be untouched — if a change to them appears necessary, that is a signal the design
  has drifted into replacing partials rather than bypassing them.
- **`UBookIt.TestSite`**: registers a theme so the mechanism is exercised live, calling
  `AddUBookItTheme` **after** `AddComposers()` — the order that silently produced an
  unthemed, unstyled site under the rejected composer mechanism, and the order a site
  author is most likely to write. It is now a supported order and is exercised as one.
- **Docs**: `docs/booking-page.md` §"What you still cannot change" reversed and corrected;
  the accessibility statement gains the themed-rendering boundary.
- **`CLAUDE.md`**: invariant 5 gains a fourth category.
- **No new dependencies, no schema change, no migration.** Nothing installs into the site.
