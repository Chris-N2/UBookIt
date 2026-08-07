## Context

Real booking systems sell *services* (a 60-minute massage; a day's excavator hire) that bundle a duration and a required resource composition, and later constrain which resources qualify. The full model was explored and agreed (2026-08-01): a service **derives a claim-set** over the universal placement primitive that already exists (`Booking.Create`/`IBookingStore.PlaceAsync` already take a claim *set*), so services never own placement and the direct-resource path (⑤) stays zero-config. That model is the north-star; this change is only its **first slice** — *defining and managing* services — deliberately small, with all booking behaviour deferred.

The management shape is a near-exact parallel of change ③ (resource-management): an authorized versioned CRUD surface with purpose-built DTOs, server-side validation via Core factories with stable codes, and HTTP-caller containment. This change reuses ③'s controller base, auth policy, and problem-details mapper.

## Goals / Non-Goals

**Goals:**
- A pure `Service` aggregate shaped for additive growth (multi-role, required capabilities) with no breaking change later.
- Persist and manage services through an authorized API, mirroring the resource store/controller patterns.
- Change nothing about existing behaviour — direct-resource booking is untouched (an explicit spec requirement).

**Non-Goals:**
- Everything in the proposal's Non-goals: booking-via-service (eligibility resolution, union availability, candidate-loop placement), capabilities, multi-role/composite availability/multi-claim placement, the backoffice Lit editor, delivery-API exposure, and the service-oriented front-end.

## Decisions

### D1: A service derives a claim-set; it does not own placement (roadmap spine)
The universal primitive is `place(interval, booker, {claims})`. A service is an optional layer that resolves its roles to concrete resources and hands the resulting claim-set to that primitive. This change establishes the aggregate only; the resolution/placement lands in the next slice. Recording it here keeps the aggregate's shape honest.
- **Why**: keeps the simple case zero-config and every services layer additive. Placement never learns what a service is.

### D2: `Service` / `ServiceRole` shape — plural-ready, v1 single-role count-1
`Service` = `{ Guid Id, string Name, TimeSpan? Duration, IReadOnlyList<ServiceRole> Roles }`; `ServiceRole` = `{ string ResourceType, int Count }`. v1 factory enforces exactly one role, count 1, and no capability requirements — but the collection and the future `RequiredCapabilities`/`Count` fields are modelled so multi-role and capability constraints are additive (same philosophy as `Booking`'s plural claims and the extensible resource-type key).
- **Why**: the expensive-to-reverse cardinality/shape bets are made now, cheaply, while the aggregate is born.
- **Alternative considered**: a flat single-role `Service` (no `Roles` collection) — rejected: multi-role (⑧) would then be a breaking shape change.

### D3: Persistence — two tables, additive migration, store split mirroring resources
`uBookItService` (id, name, duration-minutes nullable) + `uBookItServiceRole` (id, serviceId FK cascade, resourceType, count). An additive EF Core migration. An `IServiceStore` (read) + `IServiceManagementStore` (CRUD) split, mirroring `IResourceStore`/`IResourceManagementStore`.
- **Why**: consistency with the resource persistence patterns (and their QA lessons). Cascade delete of roles with their service.
- **Note**: no delete-protection analogue to `resource-in-use` is needed yet — nothing references a service until booking-via-service ships; delete simply removes the service and its roles. (A future "service-in-use" guard arrives with booking-via-service.)

### D4: Management API reuses ③'s edge
A `ServicesController` under the existing `ubookitbackoffice` group and `UBookItBackofficeApiControllerBase` (so auth/versioning/`MapToApi` come for free), with service DTOs and a mapper, and the existing `ApiResults.ToProblemResult` for failures. Containment holds: the controller depends only on the service ports and Core factories, never `IBookingStore`/`Booking.Rehydrate`.
- **Why**: the management edge is a solved problem; don't reinvent it.

### D5: Backoffice Lit editor deferred to a fast-follow
Unlike ③ (which shipped its editor), this slice ships the management **API** only; services are API-managed until the editor lands. The editor will mirror the existing resource collection/editor closely.
- **Why**: the Lit/TS editor is the single largest chunk; deferring it keeps this slice genuinely small while leaving a complete, testable capability. The API is the contract; the UI is additive on top.
- **Trade-off**: no backoffice UI for services in this slice. Accepted — the editor is a well-understood fast-follow, and the roadmap's booking-via-service slice is the higher-value next step.

### D6: New stable failure codes
Add `service-name-required`, `service-role-invalid` (missing role / count ≠ 1), `service-duration-invalid` (non-positive when supplied), and `service-not-found`. Reuse the existing `type-key-invalid` for a role's non-normalized resource-type key (same rule as resources). Codes are contract; never change an existing value.

## Risks / Trade-offs

- **[A service you can't book yet is inert]** → accepted: it is the foundational aggregate, exactly as resource-management (③) preceded booking (④). It is fully testable and unblocks the next slice.
- **[No backoffice UI this slice (D5)]** → services are API-manageable; the editor is a fast-follow mirroring the resource editor. Documented, not silent.
- **[Shape guesses for the additive future]** → mitigated by the north-star design; `Roles` is a collection and role fields are chosen so multi-role/capabilities are additive, reviewed against the roadmap.

## Migration Plan

Additive only: two new tables via one EF Core migration; new Core types; a new controller. No change to existing tables, types, or endpoints. Rollback is dropping the new files/migration; nothing existing depends on them.

## Open Questions

- Whether a service carries its own normalized **key/slug** (like resource type) in addition to a display name — deferred; not needed until services are referenced externally (booking-via-service / delivery API). Add additively then if wanted.
- DTO field naming (duration as integer minutes, to match the delivery-API convention) — settle during apply.
