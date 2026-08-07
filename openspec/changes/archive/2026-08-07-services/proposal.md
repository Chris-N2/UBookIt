## Why

uBookIt can book a single resource directly, but real booking systems sell **services** — "a 60-minute massage", "hire the excavator for a day" — which bundle a duration and a required resource composition, and later constrain *which* resources can fulfil them. This change lays the foundation: a `Service` domain concept that can be defined and managed. It is the first, deliberately small slice of the services roadmap (see design); it introduces no booking behaviour yet, so it is low-risk and additive.

## What Changes

- Introduce a **`Service` domain aggregate** in `UBookIt.Core`: an id, a non-empty name, an *optional* fixed duration, and exactly **one** required role (a resource-type key, count 1 in v1). Validating factory; pure domain, no Umbraco/EF types. Deliberately shaped for additive growth (multiple roles, required capabilities) without a breaking change.
- **Persistence** for services: new `uBookItService` + `uBookItServiceRole` tables, an additive EF Core migration, and a management store (CRUD) plus a read store — mirroring the resource store split.
- **Management API**: authorized, versioned CRUD endpoints for services in the existing `ubookitbackoffice` swagger group (paged list, get, create, update, delete), with purpose-built DTOs and server-side validation surfacing stable failure codes — reusing the change ③ controller base, auth policy, and problem-details mapper.
- **The everything-optional invariant holds**: defining services changes nothing about the existing single-resource booking path (⑤). Services are an opt-in layer; a site that never creates one behaves exactly as today.

## Capabilities

### New Capabilities
- `services`: the `Service` aggregate (name, optional duration, single required role), its persistence, and the authorized management API for defining and managing services. Booking *via* a service is explicitly out of scope for this change (next slice).

### Modified Capabilities
<!-- None. Reuses the resource-management controller base / auth / mapper as implementation, but changes no existing requirement; the direct-resource booking path is unchanged. -->

## Impact

- **New code**: `UBookIt.Core` (`Service`, `ServiceRole`, factory, new failure codes); `UBookIt.Persistence` (two entities, a migration, service management + read stores, DbContext config); `UBookIt.Backoffice` (a `ServicesController` + service DTOs + mapper, reusing the existing base/auth/`ToProblemResult`). Tests in `UBookIt.Tests` / `UBookIt.Tests.Integration`.
- **No changes** to `UBookIt.Core`'s existing types, the resource/booking/availability domains, the delivery API, or the default front-end. Additive EF migration only; no destructive schema change.
- **Public API surface**: net-new `Service` domain surface and management endpoints — a compatibility promise once published, but additive. No breaking change.

## Non-goals

- **Booking via a service** — eligibility resolution, composite/union availability, and the candidate-loop placement. That is the next slice; this change only *defines* services.
- **Capabilities / capability-constrained eligibility** — services here match by resource **type** only; a role carries no required capabilities yet (a later additive slice adds capabilities to both resources and roles).
- **Multiple roles per service** (room + person) and composite availability / multi-claim atomic placement — a later, isolated slice. v1 role count is fixed at 1.
- **Backoffice section / editor for services** (Lit UI) — deferred to an immediate fast-follow; services are managed via the API for now. (The eventual editor mirrors the existing resource editor.)
- **Delivery-API exposure of services** and the service-oriented front-end — later slices.
- **Fixed-duration enforcement at booking time** — the *meaning* of a service's optional duration (override the resource's minimum, or fall back when unset) is specified here but only *takes effect* when booking-via-service ships.
