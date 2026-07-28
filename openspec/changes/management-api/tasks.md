# Tasks: management-api

## 1. Core port and failure code

- [ ] 1.1 `IResourceManagementStore` port in Core (create, full update, delete, paged list with total); `FailureCodes.ResourceInUse` added
- [ ] 1.2 Unit tests: port contract documentation-level checks live with implementation tests (no Core logic beyond the interface)

## 2. Persistence implementation

- [ ] 2.1 `SqlResourceManagementStore`: create (aggregate → rows), full-replace update in one transaction (delete + reinsert child rows), delete with claims pre-check + FK-violation mapping to `resource-in-use`, paged list with total
- [ ] 2.2 Register in the composer; keep `IResourceStore` untouched
- [ ] 2.3 Integration tests: create-then-read value equality via `IResourceStore`; paged totals; replace-never-merge; racing conflicting updates → one complete set, no duplicate dates; delete removes all rows; delete refused for claimed resource (incl. FK backstop path)

## 3. Management API

- [ ] 3.1 Remove the template example controller; confirm/carry the backoffice authorization policy on the shared base controller
- [ ] 3.2 DTO models (resource details, opening hours, exceptions, constraints; paged list envelope) — no domain types in the contract
- [ ] 3.3 Mapping: DTO → Core factories → domain (validation) and domain → DTO; failure mapping to problem details (400 with `{code,message,field}` errors extension, 404 `resource-not-found`, 409 `resource-in-use`)
- [ ] 3.4 Controllers: paged list, get, create, update, delete under the `ubookitbackoffice` swagger group, versioned v1
- [ ] 3.5 Unit tests: mapping both directions incl. every failure path from the spec scenarios (overlapping windows + blank name multi-code case, duplicate exception dates, unknown id, claimed delete)

## 4. Backoffice client

- [ ] 4.1 Remove the example dashboard (manifests, elements, generated example client usage)
- [ ] 4.2 Section manifest + `en-US` localization; section registered and visible to permitted users
- [ ] 4.3 Regenerate the typed API client from swagger via the existing hey-api script (site running)
- [ ] 4.4 Collection view element: paged `uui-table` (name, type, availability summary), create/edit/delete affordances
- [ ] 4.5 Editor element: Details / Opening hours (per-weekday window rows, add/remove) / Exceptions (date + closure/override windows) / Constraints groups; full-resource save; per-field error surfacing by code
- [ ] 4.6 Accessibility pass per the spec: labels, error association + announcement, keyboard operability, visible focus, semantic elements, uui-first
- [ ] 4.7 `npm run build` clean; no new npm dependencies (any exception must be named with license in the specs)

## 5. Verification

- [ ] 5.1 Boot TestSite: section appears, create a room with availability through the UI, edit it, delete an unclaimed one; confirm rows in SQL; manual keyboard-only pass over the flow
- [ ] 5.2 Swagger document lists the new endpoints; anonymous request rejected (401)

## 6. Wrap-up

- [ ] 6.1 Public-surface audit: additive only (port, DTOs); API layer has no `IBookingStore`/`Booking.Rehydrate` references (containment obligation)
- [ ] 6.2 Full build green (NU1903 baseline only); all unit + integration tests pass; client build clean
