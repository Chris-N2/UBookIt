## Context

`UBookIt.Web` hosts the delivery API and is the designated home for the default rendering. It is an `Sdk.Razor` project with `AddRazorSupportForMvc` and references `Umbraco.Cms.Web.Website` (which provides `SurfaceController`), so Razor views and ViewComponents compile into the assembly and are discoverable by a consuming Umbraco site; no new package is needed. There is no rendering scaffolding yet — this is greenfield.

Two forces shape the design. First, invariant 5: the default rendering is dependency-free, semantic, WCAG 2.2 AA, and must work with **no JavaScript**. Second, the anti-forgery decision deferred from the delivery API resolves here — the default form is a same-origin, cookie-context POST, which is exactly where an anti-forgery token belongs (the delivery API stayed anonymous so it could). The site owner confirmed the ViewComponent approach and the single-resource scope (discovery, JS enhancement, and visual polish are deferred).

## Goals / Non-Goals

**Goals:**

- A working, accessible, no-JS booking flow for one resource, provable end-to-end (render → choose date → choose time → submit → confirm) with anti-forgery and PRG.
- Keep the render path in-process on Core ports; keep the submission on a SurfaceController that owns anti-forgery and Core placement.
- Make repopulation and error rendering explicit and unit-testable, not reliant on framework magic.

**Non-Goals:**

- Everything in the proposal's Non-goals: resource discovery UI, JS enhancement, theming/CSS polish, editor-facing doc-type packaging, multi-claim bookings, cancellation UI.
- Any change to Core, Persistence, or the delivery API. This change only adds a rendering surface.

## Decisions

### D1: ViewComponent renders; SurfaceController submits

A `BookingViewComponent` (invoked as `@await Component.InvokeAsync("Booking", new { resourceId })` from a template) renders the flow. ViewComponents are render-only, so the form POSTs to an Umbraco `SurfaceController` (`BookingSurfaceController`). Views live at `Views/Shared/Components/Booking/Default.cshtml` (+ a confirmation view), compiled into the assembly via the Razor SDK.

- **Why**: the canonical Umbraco pattern for package-shipped front-end forms, and the shape the owner asked for. A ViewComponent is a render abstraction over *data*, not a widget wrapper — invariant-clean.
- **Alternative considered**: a single `RenderController`/route owning GET+POST — rejected: departs from the requested ViewComponent shape and from Umbraco's content-routed page model.

### D2: Render data comes from Core in-process

The ViewComponent injects `IResourceStore` and `IAvailabilityQueryService` and reads the resource + its slots for the chosen date directly. It never calls the delivery API over HTTP to render itself.

- **Why**: an HTTP self-call inside the same app is wasted latency and a new failure mode. The delivery API is for JS/external consumers; the default UI is a peer consumer of the *same Core ports*, not of the HTTP surface.

### D3: Full Post-Redirect-Get, both success and failure, with `TempData`

`BookingSurfaceController` owns the POST. On success it places via `IBookingService`, stashes the booking id in `TempData`, and `RedirectToUmbracoPage(confirmationPage)` (or the origin page's confirmation state). On failure it maps the domain failures to messages, stashes the submitted values + messages in `TempData`, and `RedirectToCurrentUmbracoPage()`. The ViewComponent reads any `TempData` failed-submission to repopulate inputs and render the error summary.

- **Why**: PRG on both paths means a refresh never re-posts (satisfies the confirmation-refresh requirement and avoids the browser resubmit prompt on errors too). Crucially, it makes repopulation **explicit via a plain model** rather than relying on `ModelState`/tag-helper propagation into a ViewComponent (which is unreliable and hard to test).
- **Alternative considered**: `CurrentUmbracoPage()` with `ModelState` for the error case (the common Umbraco idiom) — rejected here: ViewComponents don't cleanly inherit the controller's `ModelState` for `asp-for` repopulation, and it re-posts on refresh. Trade-off accepted: a refresh after an error shows a cleared form (TempData is one-shot) — acceptable since nothing was booked.
- **First-pass only**: `TempData` is a state hack outside the MVC pattern, tolerated because a render-only ViewComponent cannot own the POST. The deferred doctype/template iteration — a `RenderController` (or route hijack) owning both GET and POST — supersedes this: the error case reverts to the idiomatic `CurrentUmbracoPage()` + `ModelState`, and `TempData` disappears. This design does not entrench it.

### D4: Anti-forgery via the Umbraco form + `[ValidateAntiForgeryToken]`

The form is emitted with `Html.BeginUmbracoForm<BookingSurfaceController>(nameof(Submit))`, which renders the anti-forgery hidden field; the `Submit` action carries `[ValidateAntiForgeryToken]`. A POST without a valid token is rejected by the framework before any Core call.

- **Why**: honours the CLAUDE.md "booking form submissions use anti-forgery" convention at the exact layer it applies. Verified live (a tokenless POST is rejected).

### D5: Two-step, date-scoped rendering

The flow is date-scoped: the page renders a date `<input type="date">` and a "show times" submit that reloads the same page with the chosen date as a query value (GET). With a date selected, the page renders that date's start times as a **radio group** plus the contact fields and the booking submit (POST). Defaults: the first open date within the horizon, or today.

- **Why**: no JS, so revealing times is a page reload; a native date input is accessible and dependency-free. Radio group is the accessible, no-JS way to pick one of N times.

### D6: Time on the wire vs on screen

Slots come from Core as UTC instants. The view displays them as wall-clock in the site zone (mapping via the site `TimeZoneInfo`); each radio's *value* is the exact UTC instant (round-tripped as an ISO-8601 string), so the POST re-selects the precise slot without re-parsing display text. The confirmation shows the same local wall-clock.

- **Why**: consistent with the delivery-api time contract (UTC + site zone); submitting the instant keeps placement unambiguous across the DST edges the availability spec already pins.

### D7: Failure-code → message mapping is a testable unit

A small `BookingMessages` map turns stable domain codes (`conflict`, `outside-open-hours`, `lead-time`, `email-invalid`, …) into user-facing sentences. `conflict` in particular renders as "that time is no longer available" with refreshed availability. This map and the view-model assembly are plain functions, unit-tested without a host.

- **Why**: the a11y-critical error text and the conflict-recovery behaviour are the parts most worth pinning with fast tests; the full rendered-HTML/anti-forgery/PRG behaviour is verified live (as with the delivery API, no WebApplicationFactory host exists).

## Risks / Trade-offs

- **[ViewComponent ↔ SurfaceController coordination is the fiddly part]** → D3 removes the reliance on ModelState propagation by carrying the failed submission explicitly in TempData; the exact Umbraco redirect/TempData wiring is confirmed live during apply.
- **[Accessibility is a headline claim, only partially machine-verifiable]** → structural a11y (labels, fieldset/legend radio group, error-summary association, unstyled usability) is asserted on rendered markup; a human keyboard-only + screen-reader pass is an explicit verification task (also a standing pre-release recommendation from change ③'s QA).
- **[TempData depends on a temp-data provider]** → the ASP.NET Core cookie TempData provider is available by default in an Umbraco site; confirmed during apply. If absent, PRG state would be lost — verified live.
- **[No WebApplicationFactory host]** → same posture as the delivery API: extractable logic (message map, view-model assembly, slot/date selection) is unit-tested; the HTTP-pipeline behaviour (anti-forgery rejection, PRG redirects, rendered a11y markup) is verified live against the TestSite.

## Migration Plan

Purely additive. New files in `UBookIt.Web` (ViewComponent, SurfaceController, views, view models, message map, optional minimal stylesheet in `wwwroot`). No Core/Persistence/spec changes, no migration. The TestSite gets a template invoking the ViewComponent with a seeded resource id for verification (TestSite is not shipped). Rollback is removing the new files; nothing else references them.

## Open Questions

- Confirmation destination: a dedicated confirmation view rendered by the SurfaceController redirect, vs a confirmation *state* of the same page. Settle during apply against how cleanly Umbraco routing carries the PRG target; either satisfies the spec.
- Whether a minimal default stylesheet ships now or with the deferred polish pass — leaning "tiny or none" so the unstyled-usability requirement is what's proven first.
