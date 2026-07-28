## Context

`UBookIt.Core` exposes `IAvailabilityQueryService` and `IBookingService`, both already DI-registered by the persistence composer, and both returning `DomainResult<T>` with stable `FailureCodes`. `UBookIt.Backoffice` established the HTTP-edge patterns this change mirrors: attribute-routed controllers grouped under a `MapToApi` name with their own Swagger document, view-model mappers in a `Mapping/` folder, and `ApiResults.ToProblemResult` turning domain failures into RFC 7807 responses. `UBookIt.Web` is an empty Razor class library referencing only Core and `Umbraco.Cms.Web.Website` — the delivery API is greenfield here.

Two forces shape the design. First, invariant 4: every UI (the shipped default ⑤, a separate-repo DevExpress UI, a SPA, mobile) plugs in at *this* API — so the contract must be anonymous, self-contained, and view-model-shaped, never a serialized domain aggregate. Second, the deferred obligation from the core-domain archive: `FreeTimeCalculator.OpenIntervals` walks day-by-day, so an unbounded queried range is a computation-cost hole that QA will treat as a hard finding.

The anonymous/body-only auth stance and its rationale are recorded in the `ubookit-frontend-contract-rationale` project memory; this design implements it.

## Goals / Non-Goals

**Goals:**

- A public, anonymous, versioned JSON contract for resource read, availability/slot read, and booking placement, expressed entirely in delivery-defined view models.
- Discharge the date-range obligation intrinsically in Core, so it protects every caller (HTTP and in-process), not just this edge.
- Reuse the backoffice HTTP-edge shape (controllers, `MapToApi`, problem-details envelope) so the two APIs are consistent without being coupled.
- Keep `UBookIt.Core` ASP.NET-free and depend, from the delivery edge, only on read/service ports — never the management store.

**Non-Goals:**

- Everything in the proposal's Non-goals: HTTP cancellation, a separate capability token, member/bearer auth, the default UI (⑤), rate limiting/CORS hardening, and result pagination.
- Any change to the placement pipeline, conflict semantics, or domain models. This change adds an edge and one query guardrail; it does not touch booking behaviour.

## Decisions

### D1: Public route + separate OpenAPI document, anonymous controllers in `UBookIt.Web`

Delivery endpoints are `[ApiController]` MVC controllers under a public versioned route (`umbraco/ubookit/api/v{version:apiVersion}`), grouped under a new `MapToApi` name with its own Swagger document — mirroring the backoffice composer, but with **no** `[Authorize]` and **no** `[BackOfficeRoute]`. A shared base controller carries the route/versioning/`MapToApi` attributes; per-resource controllers add their operations.

- **Why**: mirrors the team's established, QA-approved backoffice pattern, so reviewers and consumers see one idiom. A separate API document keeps the public contract cleanly generable into a standalone TypeScript/OpenAPI client, independent of the backoffice document.
- **Alternatives considered**: (a) Minimal APIs — rejected for inconsistency with the controller-based backoffice. (b) Umbraco's built-in Delivery API extension — rejected: that surface is for content delivery, not a custom booking domain; bending it would be a widget-level abstraction in spirit.

### D2: A public list needs a read-port list — extend `IResourceStore`, never touch the management store

`GET /resources` requires enumerating resources, but the read port `IResourceStore` has only `GetAsync`. Rather than reach into `IResourceManagementStore` (a *write*/management port), add a read-only `ListAsync(skip, take)` returning the existing `ResourcePage` to `IResourceStore`, implemented on `SqlResourceStore`.

- **Why**: preserves the HTTP-caller containment rule QA enforced in ③ — the delivery edge depends only on `IResourceStore`, `IAvailabilityQueryService`, and `IBookingService`, and never on management writes. Additive to the read port; no schema or migration.
- **Alternatives considered**: reuse `IResourceManagementStore.ListAsync` — rejected: couples the anonymous public edge to the management surface, the exact containment violation the theme forbids.
- **Trade-off**: v1 has no "published/bookable" visibility flag, so the list returns *every* resource. Accepted for v1; a visibility filter is an additive change when content-level presentation (⑤) arrives.

### D3: A Web-local problem-details mapper — deliberate duplication, not a shared library

`UBookIt.Web` gets its own `ToProblemResult`-style mapper rather than sharing the backoffice one. The two status tables genuinely differ (delivery maps `date-range-too-large` and the full placement-pipeline code set; the backoffice maps `resource-in-use`), and the mapper depends on ASP.NET MVC types, which must not enter Core (domain purity).

- **Why**: ~20 lines duplicated keeps the two HTTP edges independent and Core clean. The stable `FailureCodes` and `DomainFailure` in `Core.Common` are the shared contract; the *HTTP projection* is legitimately per-edge.
- **Alternatives considered**: a shared `UBookIt.Http` mapping library — rejected as premature for one small function across two consumers; revisit if a third edge appears.

### D4: The range cap lives in Core, enforced before resource load

`SiteBookingSettings` gains `MaxQueryRangeDays` (default **31**); `AvailabilityService` rejects an inclusive range wider than the cap with a new `FailureCodes.DateRangeTooLarge`, evaluated in `ResolveAsync` *before* the zone is resolved or the resource is loaded — so both the free-time and slot paths (which share `ResolveAsync`) are covered, and an over-wide range costs nothing. The setting binds from `UBookIt:MaxQueryRangeDays`, defaulting safely like `TimeZoneId` does.

- **Why**: discharges the obligation intrinsically — ⑤ and any in-process caller are protected, not just the delivery endpoint. Additive: one new field, one new code, no signature change.
- **Alternatives considered**: cap only at the Web edge — rejected: leaves every other Core caller exposed and contradicts the obligation's intent.
- **Trade-off**: a misconfigured very-large `MaxQueryRangeDays` re-opens the cost hole. It's an admin-only guardrail; documented as such. A malformed/negative configured value falls back to the default rather than throwing.

### D5: BCL time on the wire — UTC ISO-8601 + top-level zone id, duration as integer minutes

Instants serialize as `DateTimeOffset` (ISO-8601; since they are UTC the offset is `+00:00`). Each response carries the site zone id once at the top level; consumers use it as the *display* zone, not as the instant's offset. Slot/booking durations serialize as whole integer minutes. The placement response returns the booking id as the confirmation reference and echoes booker contact details.

- **Why**: consistent with core-domain D3 (BCL types at the boundary, no NodaTime leaking into the contract). Integer minutes are the most JS-client-friendly duration shape and match the domain's minute-granular constraints.
- **Alternatives considered**: pre-rendered local wall-clock strings (rejected — bakes formatting into the contract; the zone id lets any client format); ISO-8601 duration strings (rejected — needlessly awkward for the minute-granular values in play).

### D6: Anonymous, body-only writes — no anti-forgery, no ambient member identity

Placement is anonymous. The delivery placement request model exposes name/email/optional phone and **no member key**; the controller builds the Core `Booker` from the body alone, always with a null member key. No cookie anti-forgery token is required, and no identity is read from an ambient authentication cookie.

- **Why**: an external-repo UI is a first-class consumer of this POST, so cookie anti-forgery would break the intended clients; and an anonymous body-only endpoint carries no ambient authority to forge, so CSRF is not in scope. Accepting a member key from an anonymous body would be identity spoofing. (See the frontend-contract-rationale memory; the CLAUDE.md anti-forgery convention is satisfied by ⑤'s no-JS fallback form, a later change.)
- **Alternatives considered**: cookie anti-forgery / member-cookie identity — rejected: reintroduces a CSRF surface and breaks the symmetric-consumer model.

### D7: Input validation returns the same problem-details envelope as domain failures

Route ids use `{id:guid}` constraints; request bodies are validated (required fields, well-formed values) at the model layer and rejected with 400 problem details *before* reaching the domain, using the same `errors[] = { code, message, field }` envelope the domain-failure mapper produces. Where the domain already owns a rule (e.g. `email-invalid`), the domain's code is authoritative; model validation covers structural/transport concerns (missing body, unparseable values).

- **Why**: one uniform error contract for clients regardless of whether a rejection is structural or domain-level; avoids passing malformed input into services with undefined results.

## Risks / Trade-offs

- **[Anonymous placement is a spam/abuse surface]** → rate limiting and CORS are explicitly deferred to the CI/hardening change; called out in the proposal Non-goals rather than silently omitted. Input validation and the atomic conflict guarantee still hold.
- **[Mapper duplication (D3) drifts from the backoffice mapper]** → the shared contract is the `FailureCodes` constants, not the HTTP mapping; each edge's status table is intentionally its own. Extract a shared library only if a third edge appears.
- **[Unfiltered resource list (D2)]** → every resource is publicly listable in v1 (no visibility flag). Acceptable pre-⑤; additive to filter later. Documented, not silent.
- **[Config guardrail can be misconfigured (D4)]** → `MaxQueryRangeDays` is admin-controlled; a malformed/negative value falls back to the default (31) rather than throwing, matching the `TimeZoneId` safe-default precedent.
- **[UTC offset vs display zone confusion (D5)]** → the instant's `+00:00` offset is not the booking's local offset; the top-level `zoneId` is the display zone. Documented in the contract so clients don't mis-render.

## Migration Plan

Purely additive. Deploy order within the change: Core (settings field + failure code + cap enforcement + `IResourceStore.ListAsync`) → Persistence (`SqlResourceStore.ListAsync`) → Web (controllers, view models, mapper, composer/Swagger registration). No data migration. Rollback is removing the Web registration; the Core additions are inert if unused. No existing public surface changes, so no consumer migration is required.

## Open Questions

- Final field naming on the wire (`startUtc`/`endUtc`/`durationMinutes`, response envelope property names) — cosmetic; settle during apply against the generated OpenAPI document.
- Whether `GET /resources` should eventually filter to "published/bookable" resources — deferred to ⑤/content-presentation; not blocking.
