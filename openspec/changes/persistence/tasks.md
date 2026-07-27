# Tasks: persistence

## 1. Core rehydration surface (QA-mandated obligation)

- [x] 1.1 Public `Booking.Rehydrate` factory: structural invariants enforced, any status accepted without transition rules, documented as persistence-boundary API
- [x] 1.2 Unit tests: faithful rehydration (incl. `Declined`, multi-claim), structural invariant failures, transitions still enforced post-rehydration

## 2. Persistence project setup

- [x] 2.1 Package references via CPM: `Umbraco.Cms.Persistence.EFCore`, `Microsoft.EntityFrameworkCore.SqlServer` (versions aligned to Umbraco 17.5.x's EF Core major); `UBookIt.Persistence` references stay Core + these only
- [x] 2.2 `UBookItDbContext` + entity configurations mapping the spec'd schema (`uBookIt` prefix, times as `time`, instants as `datetimeoffset`, constraints as integer minutes/days)
- [x] 2.3 Design-time factory for `dotnet ef` migration authoring (SQL Server, dummy connection)
- [x] 2.4 Indexes per design D4: claims by resource, bookings by interval including status; unique (BookingId, ResourceId) on claims

## 3. Migrations

- [x] 3.1 Initial migration (baseline schema), history table `__uBookItEFMigrationsHistory`
- [x] 3.2 Startup migration runner: Umbraco notification handler, gated on configured database, idempotent

## 4. Store implementations

- [x] 4.1 `SqlResourceStore` (`IResourceStore.GetAsync`): loads resource + open hours + exceptions + constraints, assembles Core value objects via their factories
- [x] 4.2 `SqlBookingStore.GetClaimsAsync`: half-open overlap query over blocking-agnostic claims (status filtering stays in Core), index-served
- [x] 4.3 `SqlBookingStore.PlaceAsync`: transaction + `sp_getapplock` per claimed resource (ascending order) + conflict re-check + insert; structured `conflict` failure, no partial rows on failure
- [x] 4.4 `SqlBookingStore.GetBookingAsync` / `UpdateAsync`: rehydration via `Booking.Rehydrate`, status persistence

## 5. Composition

- [x] 5.1 Composer: DbContext via `AddUmbracoDbContext` (see design D1 correction), stores, `TimeProvider.System`, `IAvailabilityQueryService`, `IBookingService`
- [x] 5.2 `SiteBookingSettings` bound from `UBookIt` config section; UTC default with startup warning when absent

## 6. Integration tests (`UBookIt.Tests.Integration`)

- [x] 6.1 Project scaffold (xunit, CPM), added to solution; harness: `UBOOKIT_TEST_DB` env var with LocalDB default, unique database per run, migrate on start, drop on dispose; explicit skip-with-diagnostic when unreachable
- [x] 6.2 Migration tests: fresh-database schema creation (all tables + history table), second run is a no-op
- [x] 6.3 Round-trip fidelity: availability configuration (windows, closure, override, constraints) and booking (interval, zone, booker, claims, status, created)
- [x] 6.4 Claims query half-open boundary at the SQL layer (booking ending at range start excluded)
- [x] 6.5 Status-change persistence: cancel via service, reload, verify non-blocking
- [x] 6.6 Concurrency proof: ≥10 parallel conflicting placements through `SqlBookingStore` → exactly one success, others `conflict`, exactly one booking row + one claim row
- [x] 6.7 End-to-end through Core services against SQL stores: place → free time reflects it → cancel → free time restored

## 7. Site verification and housekeeping

- [x] 7.1 Boot `UBookIt.TestSite` against SQL Server: uBookIt tables + history table created; `IBookingService` resolvable (verified via startup log or minimal probe); commit the pending `UserSecretsId` csproj change with this change
- [x] 7.2 Document the SQL Server 2019+ requirement (README stub in `UBookIt.Persistence`)

## 8. Wrap-up

- [x] 8.1 Public-surface audit: Persistence exposes composer + DbContext as infrastructure; stores may stay internal if DI-only; Core additions limited to `Booking.Rehydrate`
- [x] 8.2 Full build green (only the documented pre-existing NU1903 baseline); all unit + integration tests pass locally; no dependencies beyond those named in the proposal
