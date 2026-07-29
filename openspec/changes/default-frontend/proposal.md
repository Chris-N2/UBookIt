## Why

uBookIt has a domain, persistence, a backoffice, and a delivery API, but no shipped way for a site visitor to actually make a booking. The default front-end is the package's out-of-the-box booking experience — and, per the project's invariants, its accessibility is a headline feature: booking UIs are notorious WCAG failures, so a dependency-free, screen-reader-friendly default is a differentiator, not a checkbox.

## What Changes

- Introduce the **default booking front-end** in `UBookIt.Web`: a server-rendered, dependency-free, WCAG 2.2 AA booking flow for a **single resource**, built as an Umbraco **ViewComponent** + Razor views, with **no JavaScript required**.
- **Flow** (progressive-enhancement baseline, no JS): choose a date → see that day's available start times → pick one, enter contact details → submit → confirmation page showing the booking reference. Rendered as accessible semantic HTML (labelled fields, a slots radio group in a fieldset, an error summary region, hint/error text wired via `aria-describedby`).
- **Anti-forgery lives here** (the decision deferred from the delivery API): the booking submission is a **same-origin Umbraco `SurfaceController` POST** protected by an anti-forgery token — *not* the anonymous delivery API. It calls Core's `IBookingService` **in-process**; the delivery API stays the anonymous path for JS/external UIs.
- **Data comes from Core in-process** (`IResourceStore`, `IAvailabilityQueryService`) — the server render does not make an HTTP round-trip to its own delivery API.
- **Post-Redirect-Get**: a successful placement 303-redirects to a confirmation view; the booking id (the confirmation reference chosen in the delivery-api change) is carried in `TempData`, not the URL.
- **Placement failures are announced accessibly**: domain failure codes are mapped to user-facing messages in an error summary (e.g. a slot taken between page load and submit → a clear "no longer available" message), with the form redrawn and the user's input preserved.

## Capabilities

### New Capabilities
- `default-frontend`: the shipped, dependency-free, accessible server-rendered booking UI — the ViewComponent + Razor views, the anti-forgery-protected SurfaceController submission, the PRG confirmation, and the WCAG 2.2 AA / semantic-HTML requirements. Consumes the existing domain via Core ports in-process; introduces no widget-level abstraction.

### Modified Capabilities
<!-- None. This capability consumes availability/bookings/resources through existing Core ports and the delivery-api contract unchanged; no existing requirement changes. -->

## Impact

- **New code** in `UBookIt.Web`: a `BookingViewComponent`, an Umbraco `SurfaceController` for the submission, Razor views (component view + confirmation view), front-end view models, and a domain-failure-code → user-message mapping. Possibly a minimal semantic stylesheet in `wwwroot` (unstyled must still be usable). `UBookIt.Web` already has `AddRazorSupportForMvc` and references `Umbraco.Cms.Web.Website` (which provides `SurfaceController`).
- **No changes** to `UBookIt.Core`, `UBookIt.Persistence`, the delivery API, or any existing spec: the front-end consumes existing ports and contracts. No schema change, no migration.
- **Public surface**: the ViewComponent name/arguments and the SurfaceController route become a consumer-facing contract (a site author invokes the ViewComponent from a template with a resource id). Net-new; no breaking change.

## Non-goals

- **Resource discovery / listing UI.** v1 books a single resource whose id is supplied by the invoking template. A "browse and choose a resource" page is a deferred follow-up (it also anticipates the future multi-resource-type model — e.g. a booking that needs a room *and* a person — which the domain already supports structurally).
- **JavaScript progressive enhancement.** The no-JS baseline ships first; a JS layer that calls the delivery API for a smoother experience is a later change. Nothing here may *require* JS.
- **Visual design / theming / substantial CSS.** Semantic HTML that is fully usable unstyled is the bar; polish is deferred.
- **Editor-facing packaging** — a "Booking Page" document type + template an editor drops in. v1 exposes the ViewComponent for a site author to wire into their own template.
- **Multi-resource / multi-claim bookings, payments, notifications, and member-authenticated booking** — all out of scope, consistent with the domain's v1 behaviour (single claim, auto-confirm, anonymous booker).
- **Cancellation / self-service management UI** — no HTTP cancellation exists yet (deferred with the delivery API).
