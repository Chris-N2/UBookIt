# Design: management-api

## Context

Change ③ builds the first HTTP and UI surface over the existing domain (Core) and persistence. Constraints in force: backoffice is native Lit/uui (invariant 3); no widget-level abstractions (invariant 4); management endpoints carry backoffice authorization (convention); two QA obligations from the persistence archive must be discharged (write-path exception uniqueness; HTTP callers kept away from raw stores/`Rehydrate`); public surface is additive-only. The UX shape is decided: collection view → workspace editor, no tree, accessibility now, visual polish later.

## Goals / Non-Goals

**Goals:**

- Editors can create, find, edit, and delete resources — including the full availability model — without touching the database.
- The Management API is a clean, versioned, documented (swagger) contract that the separate DevExpress-UI repo could also consume.
- Validation behaviour is identical to the domain's: same factories, same stable failure codes, surfaced as problem details.
- The write path makes duplicate exception dates impossible, including under concurrent writers.

**Non-Goals:**

- Everything in the proposal's Non-goals; additionally no optimistic-concurrency tokens (see D3) and no caching.

## Decisions

### D1: New management port, not an extension of `IResourceStore`

Core gains `IResourceManagementStore`: `CreateAsync(Resource)`, `UpdateAsync(Resource)`, `DeleteAsync(Guid)`, `ListAsync(skip, take)` (items + total). `IResourceStore` is untouched.

- **Why**: adding members to a shipped interface breaks implementors; segregation also keeps ④'s delivery-facing reads from ever seeing write members. The port accepts only `Resource` aggregates — which can *only* be constructed through the validating Core factories — so "the store persists only validated state" holds by construction, the same property that made `Booking.Rehydrate` safe.
- **Alternative considered**: a `ResourceManagementService` wrapping the port — rejected: it would only forward calls; controllers map models → factories → port, and the factories are the validation layer. Add a service later if orchestration appears.

### D2: Availability writes are full-replace within one transaction, serialized by a per-resource app lock

`UpdateAsync` replaces the resource's open-hours and exception child rows wholesale from the validated aggregate inside a single transaction (delete children, reinsert), **after acquiring an exclusive per-resource configuration app lock** (`sp_getapplock`, transaction-owned, lock name distinct from the placement lock so config writes and bookings never contend). `DeleteAsync` takes the same lock so a racing update cannot reinsert child rows mid-delete.

- **Why full-replace**: the aggregate is already duplicate-free (`AvailabilityConfiguration.Create` rejects duplicate exception dates), and wholesale replacement means every committed write is one writer's complete set. Diff-based child updates would reintroduce the possibility the obligation exists to prevent.
- **Why the lock is required** *(corrected after the first QA review — the original claim that full-replace alone sufficed was proven false)*: at READ COMMITTED, deleting zero child rows takes no range locks, so two concurrent updates against a resource with an empty child set would each delete nothing, both insert, and commit a merged union — duplicate dates included. The app lock serializes configuration writers per resource, making last-writer-wins real.
- Proven by an integration test racing two conflicting updates against an **empty-configuration** resource across repeated iterations — the exact case that defeats lock-free full-replace.

### D3: Concurrent edits are last-writer-wins (documented, not tokened)

No rowversion/ETag in v1. Two editors saving the same resource: the later save wins entirely (D2 makes the result always internally consistent).

- **Why**: an rowversion column means a migration and conflict UX for a contention level (a handful of admins editing room configs) that doesn't warrant it. Revisit with evidence; the API shape (PUT full resource) doesn't preclude adding `If-Match` later additively.

### D4: Delete refuses claimed resources with a structured failure

`DeleteAsync` pre-checks for any claims on the resource and refuses with new stable code `resource-in-use` (additive `FailureCodes` entry); the `Restrict` FK remains the backstop, and a racing claim that beats the delete surfaces as the same structured failure (the store catches the FK violation and maps it).

- **Why**: bookings are legal records; silently cascading them away would be destructive. Editors cancel bookings first (a later change adds that UI; until ④ ships, claims only exist via tests/seed data, so the constraint costs nothing now).

### D5: API contract shape

Versioned controllers in the existing `ubookitbackoffice` swagger group. Routes (v1): paged `GET /resource`, `GET /resource/{id}`, `POST /resource`, `PUT /resource/{id}`, `DELETE /resource/{id}`. Request/response models are purpose-built DTOs (never domain types): details + `openingHours[]` (day, start, end), `exceptions[]` (date, `windows[]` — empty means closure), `constraints` (integer minutes + horizon days). `TimeOnly`/`DateOnly` serialize via System.Text.Json's ISO defaults. All endpoints carry the backoffice authorization policy on the shared base controller.

- **Why DTOs**: the HTTP contract is a compatibility surface for external UI implementations (invariant 4 — data contracts, not widgets); domain types must stay free to evolve internally.

### D6: Failure mapping

`DomainResult` failures → RFC 7807 problem details: 400 (validation, with an `errors` extension listing `{code, message, field}` per failure), 404 (`resource-not-found`), 409 (`resource-in-use`). Codes pass through verbatim — clients localize by code, mirroring how ⑤ will announce errors accessibly.

### D7: Backoffice client is a section with custom Lit elements, not full workspace machinery

A `uBookIt` section (manifest + localization) hosting a root element that routes between the collection view (`uui-table` list, paged) and the editor view (Details / Opening hours / Exceptions / Constraints as `uui-box` groups of labelled uui inputs). Umbraco's full entity-type/workspace/repository registration is deliberately not used in v1.

- **Why**: the workspace machinery buys deep-linking, breadcrumbs, and entity actions at the cost of substantial plumbing that is still the least-stable extension area of the backoffice; the agreed UX (list ↔ editor) is fully achievable with plain elements and the typed API client. Migrating to real workspaces later is a UI-internal refactor — invisible to the API contract, so nothing downstream breaks.
- **Alternative considered**: full workspace/entity registration — rejected for v1 scope; revisit when reservations management arrives.

### D8: Typed client via the existing hey-api generation

The template's `generate-client` script (swagger → TypeScript) is the source of the API client; the checked-in generated client is refreshed whenever the API changes (site must be running for regeneration — documented in the client README). No hand-written fetch code.

### D9: Authorization policy is `SectionAccessContent` for v1 *(recorded after the first QA review)*

The management endpoints keep the template's `SectionAccessContent` policy rather than a custom uBookIt-section policy. QA correctly observed the mismatch: a user granted only the uBookIt section gets a UI whose calls 403, and a Content-only user can call the API without seeing the section. Decision: accepted for v1 — in practice the people managing bookable resources are content editors, and a custom section-access policy (registering a `SectionRequirement` for the uBookIt alias) is a small additive change best made when user-group granularity becomes a real requirement (likely alongside reservations management). Recorded here so it is a choice, not an accident.

## Risks / Trade-offs

- [Backoffice extension APIs drift across 17.x minors] → pin `@umbraco-cms/backoffice` to the same 17.5.x line as the server packages; D7's minimal use of extension points shrinks the exposed surface.
- [Last-writer-wins can lose an admin's edit] → accepted for v1 contention (D3); the loss is always a *consistent* state, never a corrupt merge (D2).
- [Removing the template example may leave dead manifest references] → the example's manifests, elements, and controller are removed in the same task that adds the real section; the client build failing on dangling imports is the safety net.
- [Full-replace writes delete/reinsert child rows on every save] → trivial row counts (a week of windows + a handful of exceptions); correctness over micro-optimization, revisit only with evidence.
- [Swagger-generated client requires a running site to refresh] → local-dev-only friction, documented; the generated client is checked in so CI never needs a live site.

## Open Questions

- None blocking. The exact backoffice authorization policy constant is confirmed at apply time from the template base controller (requirement stated in the spec; constant is implementation detail).
