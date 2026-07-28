# Tasks: management-api

## 1. Core port and failure code

- [x] 1.1 `IResourceManagementStore` port in Core (create, full update, delete, paged list with total); `FailureCodes.ResourceInUse` added
- [x] 1.2 Unit tests: port contract documentation-level checks live with implementation tests (no Core logic beyond the interface)

## 2. Persistence implementation

- [x] 2.1 `SqlResourceManagementStore`: create (aggregate → rows), full-replace update in one transaction (delete + reinsert child rows), delete with claims pre-check + FK-violation mapping to `resource-in-use`, paged list with total
- [x] 2.2 Register in the composer; keep `IResourceStore` untouched
- [x] 2.3 Integration tests: create-then-read value equality via `IResourceStore`; paged totals; replace-never-merge; racing conflicting updates → one complete set, no duplicate dates; delete removes all rows; delete refused for claimed resource (incl. FK backstop path)

## 3. Management API

- [x] 3.1 Remove the template example controller; confirm/carry the backoffice authorization policy on the shared base controller
- [x] 3.2 DTO models (resource details, opening hours, exceptions, constraints; paged list envelope) — no domain types in the contract
- [x] 3.3 Mapping: DTO → Core factories → domain (validation) and domain → DTO; failure mapping to problem details (400 with `{code,message,field}` errors extension, 404 `resource-not-found`, 409 `resource-in-use`)
- [x] 3.4 Controllers: paged list, get, create, update, delete under the `ubookitbackoffice` swagger group, versioned v1
- [x] 3.5 Unit tests: mapping both directions incl. every failure path from the spec scenarios (overlapping windows + blank name multi-code case, duplicate exception dates, unknown id, claimed delete)

## 4. Backoffice client

- [x] 4.1 Remove the example dashboard (manifests, elements, generated example client usage)
- [x] 4.2 Section manifest + `en-US` localization; section registered and visible to permitted users
- [x] 4.3 Regenerate the typed API client from swagger via the existing hey-api script (site running)
- [x] 4.4 Collection view element: paged `uui-table` (name, type, availability summary), create/edit/delete affordances
- [x] 4.5 Editor element: Details / Opening hours (per-weekday window rows, add/remove) / Exceptions (date + closure/override windows) / Constraints groups; full-resource save; per-field error surfacing by code
- [x] 4.6 Accessibility pass per the spec: labels, error association + announcement, keyboard operability, visible focus, semantic elements, uui-first
- [x] 4.7 `npm run build` clean; no new npm dependencies (any exception must be named with license in the specs)

## 5. Verification

- [x] 5.1 Boot TestSite: verified via automated browser session — uBookIt section granted to Administrators and appears in nav; "Meeting Room A" (room, Monday 09:00–17:00) created through the collection→editor UI; edit round-trip loads saved data; rows confirmed in SQL Server. Two defects found and fixed during this verification: (a) DayOfWeek JSON binding rejected the day names the swagger contract documents — fixed with a property-level string-enum converter + serialization regression test; (b) the backoffice http client's interceptors throw on non-2xx, hanging the save spinner — all client API calls now wrapped with error normalization (uBookIt error arrays AND ASP.NET binding-error dictionaries). Note: markup-level accessibility (labels, fieldsets, alert/focus behaviour, accessible action names) implemented and inspected; a human keyboard-only + screen-reader pass is still recommended before release and is flagged for QA's judgement. Delete-through-UI not exercised in the browser session (covered by integration tests + a native confirm dialog that automation avoids triggering).
- [x] 5.2 Swagger document lists exactly the resource endpoints; anonymous request rejected (401) — verified against the running site

## 6. Wrap-up

- [x] 6.1 Public-surface audit: additive only (port, DTOs); API layer has no `IBookingStore`/`Booking.Rehydrate` references (containment obligation)
- [x] 6.2 Full build green (NU1903 baseline only); all unit + integration tests pass; client build clean
