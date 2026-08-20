## Why

No test in this repository renders a Razor view, so **no test can fail on a
markup defect**. Three have now shipped into a fully green suite:

- ⑤'s `<partial name="~/…" />` tag helper, which — because `UBookIt.Web` has no
  `_ViewImports.cshtml` — degraded to **visible text on the page** rather than to
  a build error. QA restored it with 690 tests green.
- ⑩'s covering test for that defect, which scanned view *source* for the partial's
  path. A path appears in the file whether the construct around it renders or is
  emitted as literal text, so the test could not see the thing it was written for.
- ⑩-1's stale-choice notice, placed inside a branch where one of its three causes
  could never reach it. A bookmarked link quietly became a booking for anyone.
  QA found it by reading the code; no test could have.

Two of the three are the *same* defect class: **markup that is present in the
source and absent from the page**. Source scanning cannot distinguish those, and
it is the only tool the suite has.

The accessibility bar makes this sharper. `default-frontend` states one bar over
every flow — labels programmatically associated, start times a `fieldset` with a
`legend`, hints and errors associated with their controls — and every clause of it
is a property of *rendered output*. Today it is verified by eye, twice, by two
contexts, with nothing to catch it breaking later. Accessibility is a
differentiator for this package, and it is the one guarantee held up entirely by
review.

## What Changes

- **A rendering test project** — `UBookIt.Tests.Rendering` — renders views through
  the real Razor view engine and asserts over the resulting HTML.

  The views are **already compiled into `UBookIt.Web.dll`** (`Microsoft.NET.Sdk.Razor`
  with `AddRazorSupportForMvc`), so the rig renders the very artefact the site
  serves rather than re-parsing `.cshtml` files. No runtime compilation, no content
  root, no copy of the views that could drift.

- **Rendered markup must resolve its own references.** Every `label[for]`, every
  `aria-describedby` and `aria-labelledby` resolves to an element that exists; no
  id is emitted twice; every control has an accessible name; no tag-helper residue
  (`<partial`, `asp-*`) survives into the output. Asserted over every view in scope
  and every model state exercised, by one test that names the view it failed on.

  This is the accessibility bar made executable. It does not replace the WCAG 2.2
  AA requirement — a document can satisfy every clause here and still be
  inaccessible — but every clause here is one the bar already states and no test
  has ever checked.

- **A view must render every state its model can express.** For each model property
  a view references, varying that property must change the rendered output — and
  for a flag whose job is to say something is shown, that must hold **wherever the
  model sets it**, not merely somewhere.

  That last clause is not a refinement, it is the requirement. As first written —
  "changes the output in some state" — the rule **did not catch ⑩-1's defect**,
  which apply proved by reintroducing it and watching the rule stay green.
  `ResourceChoiceWasReset` was live wherever a choice control existed and dead only
  where none did, so an exists-a-state rule finds the live states and passes. See
  design D8.

  The property list is **derived from the view's own source**, not hand-maintained.
  A hand-written list has the obvious hole: someone adds a branch, forgets to list
  it, and the unreachable-message defect walks back in.

- **Every branch a view carries must be takeable.** A property can be live while a
  branch it selects is dead — forcing a condition always-true leaves the property
  changing the output while the alternative becomes unreachable — so branches are
  checked separately, by requiring every literal `id` and `class` a view can emit to
  appear in some rendered state.

  Found by mutation rather than by reasoning (design D9), and it immediately found
  two branches nothing rendered: the refused-pin redraw and the conflict redraw,
  both real pages, both fixture gaps rather than dead markup.

- **Eleven views come into scope; three do not.** The partition is by *transitive*
  dependency on Umbraco, not by which file mentions it:

  | | views |
  |---|---|
  | **In scope (11)** | the four shared partials; `Catalogue`; `Booking/Confirmation`; `Booking/Unavailable`; `BookingFlow/Confirmation`; `BookingFlow/Unavailable`; `ServiceConfirmation`; `ServiceUnavailable` |
  | **Deferred (3)** | `Booking/Default` and `BookingFlow/Service` (both call `Html.BeginUmbracoForm`), and `BookingFlow/Default`, a one-line delegate into the first |

  `BookingFlow/Default` reads as Umbraco-free and is not: it is a single
  `PartialAsync` into `Booking/Default`, so it inherits the dependency.

- **No product behaviour changes** — unless the rules find a defect, in which case
  fixing it is in scope. That is the point of building them.

## Non-goals

- **The three Umbraco-dependent views.** Whether `BeginUmbracoForm` needs a booted
  Umbraco context or merely data protection and anti-forgery is an open question
  worth a timeboxed spike, and the answer decides whether that suite stays fast.
  Settling it here would hold eleven views hostage to three. A follow-up takes them,
  and with them the **composition** of flow view and partial, which this change
  leaves untested: the four partials are only ever included by the three deferred
  views, so here they are rendered standalone.
- **Full HTTP through the TestSite.** `WebApplicationFactory` over `UBookIt.TestSite`
  would exercise routing, anti-forgery and Post-Redirect-Get for real, and needs SQL
  Server and an Umbraco boot per run. A different change with a different cost.
- **The ViewComponents.** `BookingFlowViewComponent` and `BookingViewComponent` need
  `TempData` and an `HttpContext`; their decisions already live in the flow classes,
  which are host-free and tested.
- **Replacing the human accessibility passes.** A screen-reader and keyboard-only
  pass over both surfaces is still owed and is not something a DOM assertion can
  discharge. These rules check what a machine can check, which is the half that
  regresses silently.
- **Snapshot or approval testing.** It would catch everything and assert nothing:
  a diff a reviewer approves without reading is worse than no test, and this suite's
  value is that each rule states a property in words.

## Capabilities

### New Capabilities

None. Both new requirements are guarantees about what the default front end
renders, so they belong to the capability that already owns that.

### Modified Capabilities

- `default-frontend`: two ADDED requirements — that rendered markup resolves its
  own internal references, and that a view renders every state its model can
  express. No existing requirement is modified: the WCAG bar is unchanged and these
  sit beside it, which also avoids replacing a requirement wholesale for an
  adjacent guarantee.

## Impact

**Code**

- **New**: `tests/UBookIt.Tests.Rendering/` — the Razor rig, the two rule suites,
  and the model fixtures. Added to the solution and to CI.
- **New test-only dependency**: AngleSharp, for parsing rendered HTML. Assertions
  like "every `aria-describedby` resolves" want a real DOM rather than a regex. It
  has no bearing on the package's dependency-free promise: nothing ships.
- `src/UBookIt.Web`: no change expected. `InternalsVisibleTo` may need the new
  project if a fixture reaches an internal type; the view models are public, so it
  probably will not.
- `tests/UBookIt.Tests`: `Support/RepoFiles.cs` is shared by link rather than
  copied — it resolves the repo root from its own compile-time path, so linking
  works unchanged.

**Risk carried in**

The fast unit suite is 768 tests in under a second, and that is a property worth
protecting. The rendering suite boots an MVC service provider and is expected to be
slower; it lives in its own project so it cannot slow the suite that runs on every
keystroke.
