## Why

`UBookIt.Core` already exposes the availability and booking services, but nothing exposes them over HTTP, so no front-end — not the shipped default (⑤), not a separate DevExpress or SPA UI — can browse resources, read availability, or place a booking. The public delivery API is the contract every UI plugs into; without it the package has a backoffice and a domain but no way to actually take a booking.

## What Changes

- Introduce the public **delivery API** in `UBookIt.Web`: anonymous, JSON, versioned, with strongly-typed request/response view models and an RFC 7807 problem-details failure map. This is the contract all front-ends (default ⑤, DevExpress, React/mobile) consume symmetrically.
- **Resource read** — `GET /resources` (paged) and `GET /resources/{id}` returning a public read model (identity, type, display name, description, booking constraints, site zone id). Distinct from the backoffice management model; it carries only what a consumer needs to drive a booking UI, never internal/management fields.
- **Availability read** — `GET` free-time intervals and projected slots for a resource over a bounded date range. Instants are ISO-8601 UTC; the response carries the site zone id once.
- **Booking placement** — `POST /bookings`, anonymous, booker contact details supplied in the request body. Runs the existing Core placement pipeline; the response returns the booking id as the confirmation reference.
- **Availability query date-range cap** (discharges the deferred obligation from the core-domain archive): a configurable `MaxQueryRangeDays` on site booking settings, enforced by the availability query service so an unbounded day-by-day computation cannot be requested. Surfaced by the delivery endpoints as a stable failure. Protects **every** caller, not just the HTTP edge.
- No cookie anti-forgery on the delivery API and no trust of an ambient member cookie for writes — anonymous, body-only. (Anti-forgery remains a concern of ⑤'s no-JS fallback form, a later change.)

## Capabilities

### New Capabilities
- `delivery-api`: the public HTTP surface — anonymous JSON endpoints, versioned routing, strongly-typed view models, input validation, and the domain-failure → problem-details mapping. Covers resource read, availability/slot read, and booking placement. Establishes the anonymous/body-only auth stance (no cookie anti-forgery, no ambient member identity for writes).

### Modified Capabilities
- `availability`: adds a **bounded query range** requirement — the free-time and slot queries reject a requested date range wider than a configurable maximum with a stable failure code, so day-by-day computation cannot run over an unbounded span.

## Impact

- **New code**: `UBookIt.Web` gains controllers, view models, a failure→problem-details mapper, an OpenAPI/Swagger document, and DI/route registration (mirroring the backoffice composer pattern but on a public, non-backoffice route). Currently `UBookIt.Web` contains only its csproj.
- **`UBookIt.Core`**: additive only — `SiteBookingSettings` gains `MaxQueryRangeDays`; the availability query service enforces it; one new stable `FailureCodes` value; the read port `IResourceStore` gains a read-only `ListAsync(skip, take)` (so the public list depends only on the read port, never the management store). No change to existing service signatures or the booking/resource domain models.
- **`UBookIt.Persistence`**: additive read-only method — `SqlResourceStore` implements `IResourceStore.ListAsync`. No schema change and no migration (placement uses the existing store; the id-as-confirmation-reference decision requires no new column).
- **Public API surface**: net-new HTTP contract; once published it becomes a compatibility promise. No breaking changes to any existing surface; no destructive schema changes.
- **Cancellation over HTTP is out of scope** (see Non-goals) — deferred until an ownership model ships.

## Non-goals

- **Booking cancellation / self-service management over HTTP.** Core's `CancelAsync` exists, but exposing it needs an ownership check; the chosen model (booking id + matching booker email) is recorded for a later change and adds no storage now.
- **A separate opaque confirmation/capability token.** v1 uses the booking id (a 122-bit random Guid) as the confirmation reference; a dedicated token can be added later as an additive response field + column without a breaking change.
- **Member-authenticated placement / bearer-token auth.** v1 placement is anonymous with body-only contact details. Deriving identity from a logged-in member is a later additive concern.
- **The default front-end rendering (⑤)** — ViewComponents/Razor, accessibility, the no-JS fallback form and its anti-forgery. This change ships the data contract only, no UI.
- **Rate limiting, CORS hardening, and abuse controls beyond input validation** — deferred to the CI/hardening change.
- **Pagination of availability/slot results.** The bounded query range makes an unpaged result set safe; paging is unnecessary and not introduced.
