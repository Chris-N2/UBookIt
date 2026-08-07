## 1. Core: Service aggregate

- [ ] 1.1 Add `ServiceRole` (record: `ResourceType`, `Count`) and `Service` (id, name, `TimeSpan? Duration`, `IReadOnlyList<ServiceRole> Roles`) to `UBookIt.Core`, pure (no Umbraco/EF). Model `Roles` as a collection though v1 enforces one.
- [ ] 1.2 Add a validating `Service.Create` factory (and a persistence-boundary `Rehydrate` mirroring `Booking`): name required; duration positive when supplied; exactly one role; role count = 1; role resource-type normalized (reuse the resource type-key rule). Return `DomainResult<Service>` with one code per failed rule.
- [ ] 1.3 Add stable `FailureCodes`: `service-name-required`, `service-role-invalid`, `service-duration-invalid`, `service-not-found` (reuse `type-key-invalid` for the role's type key). Do not alter existing values.

## 2. Persistence: entities, migration, stores

- [ ] 2.1 Add `ServiceRow` (id, name, nullable duration-minutes) and `ServiceRoleRow` (id, serviceId FK cascade, resourceType, count) entities + DbContext config (`uBookItService`, `uBookItServiceRole`).
- [ ] 2.2 Add an additive EF Core migration creating the two tables. No changes to existing tables.
- [ ] 2.3 Add `IServiceStore` (read: get by id, paged list) and `IServiceManagementStore` (create/update/delete) to Core `Stores`, and SQL implementations mirroring the resource stores (accepting only validated `Service` aggregates; deterministic list ordering; `ResourcePage`-style `ServicePage`).
- [ ] 2.4 Register the service stores in the persistence composer.

## 3. Management API (reuse ③'s edge)

- [ ] 3.1 Add service DTOs (`ServiceRequestModel`, `ServiceResponseModel`, `ServiceRoleModel`, `PagedServicesModel`) — duration as integer minutes; no domain types on the wire.
- [ ] 3.2 Add a `ServiceModelMapper` (DTO↔domain through the Core factory, accumulating failures).
- [ ] 3.3 Add a `ServicesController : UBookItBackofficeApiControllerBase` (`[ApiVersion("1.0")]`, `ubookitbackoffice` group): paged list, get, create, update, delete; `service-not-found` → 404 via `ToProblemResult`. Depends only on the service ports — never `IBookingStore`/`Booking.Rehydrate`.

## 4. Tests

- [ ] 4.1 Core unit tests for `Service.Create`/`Rehydrate`: valid single-role service (with and without duration); blank name → `service-name-required`; non-positive duration → `service-duration-invalid`; zero/duplicate/multi role and count ≠ 1 → `service-role-invalid`; non-normalized role type → `type-key-invalid`; multiple failures reported together.
- [ ] 4.2 Integration tests (LocalDB) for the service stores: create → get round-trip (name, duration, role); paged list with total; update; delete removes service + roles (cascade).
- [ ] 4.3 API-level tests (controller): round-trip DTO; paged list; unknown id → 404 `service-not-found`; invalid submit → 400 with the stable codes; anonymous stance is enforced by the shared base (structural assertion, as for resources).
- [ ] 4.4 Containment test: the service API layer references no `IBookingStore`/`Booking.Rehydrate`.

## 5. Verification & housekeeping

- [ ] 5.1 Build with `--no-incremental`; only the accepted NU1903 transitive advisories may warn (fix any compiler/analyzer warning).
- [ ] 5.2 Run the full test suite green (unit + integration).
- [ ] 5.3 Live-verify the CRUD endpoints against the running TestSite via the Umbraco MCP / direct authenticated HTTP (create a service, list, get, update, delete); Chris handles any login. Confirm defining services does not disturb existing resource/booking behaviour.
