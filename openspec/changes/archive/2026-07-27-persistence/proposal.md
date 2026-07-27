# Proposal: persistence

## Why

The core-domain change gave uBookIt a tested booking domain, but it exists only in memory: the store ports (`IResourceStore`, `IBookingStore`) have no real implementations, and the atomic-placement contract — the package's central correctness promise — is proven only against an in-memory fake. This change makes the domain durable: EF Core + SQL Server persistence implementing the ports, migrations that run on site startup, and the double-booking guarantee enforced and proven against a real database. Changes ③ (management API) and ④ (delivery API) both require this to exist first.

## What Changes

- `UBookIt.Persistence` (currently an empty class library) gains:
  - A `DbContext` mapping resources (with their availability configuration: weekly open hours, date exceptions, booking constraints), bookings, and resource claims to SQL Server tables with a `uBookIt` prefix.
  - EF Core migrations, applied automatically at site startup via an Umbraco notification handler, recorded in a package-private migrations-history table so uBookIt never collides with other EF-using packages. Migrations are additive-only per project convention.
  - SQL Server implementations of `IResourceStore` and `IBookingStore`. Booking placement implements the atomic-placement contract with a per-resource SQL Server application lock inside the placement transaction.
  - An Umbraco composer registering the DbContext, the stores, the Core services (`IAvailabilityQueryService`, `IBookingService`, `TimeProvider`), and `SiteBookingSettings` bound from configuration.
- `UBookIt.Core` gains one **additive** public API: a `Booking` rehydration factory so persistence can materialize `Booking` aggregates from storage. This discharges the QA-mandated obligation recorded when core-domain was archived (`Booking.Create` is internal; design decision D9 requires downstream changes to add surface via their own specs — this is that addition).
- New integration test project `UBookIt.Tests.Integration` running against a real SQL Server (developer instance or LocalDB, connection via environment variable with a LocalDB default): migration application, store round-trip fidelity, conflict semantics at the SQL layer, and a concurrency proof that racing conflicting placements yield exactly one success.
- **SQL Server 2019+ becomes an explicit, documented runtime requirement** of the package (project decision: no SQLite/other-provider support; sites running a booking system run a real database).
- Housekeeping folded in: the `UserSecretsId` added to `UBookIt.TestSite.csproj` when the dev site was rewired from SQLite to SQL Server via user secrets.
- No breaking changes: all Core surface added is additive; there is no published package yet, and no existing schema to migrate from (the initial migration is the baseline).

## Capabilities

### New Capabilities

- `persistence`: how booking data is stored — schema shape, migration execution and history isolation, store implementations, the SQL-level atomic-placement mechanism, DI composition, and the SQL Server requirement.

### Modified Capabilities

- `bookings`: one added requirement — a public, additive `Booking` rehydration surface in `UBookIt.Core` for materializing bookings from storage without weakening the invariants creation enforces.

## Non-goals

- **Management or delivery HTTP surface**: changes ③ and ④.
- **Resource write/CRUD surface**: `IResourceStore` stays read-only in this change; change ③ adds write surface via its own spec. Integration tests seed resources through the `DbContext` directly.
- **Any UI**: nothing visible changes in the backoffice or front end.
- **Seeding/sample data**: none shipped.
- **Data retention / GDPR tooling**: bookings persist personal contact details by design; retention and erasure tooling is a named future change, not this one. (Convention still holds here: no personal data in logs or test fixtures — tests use invented example.com identities.)
- **Multi-database support**: SQLite, PostgreSQL, etc. are explicitly unsupported; the availability date-range cap remains change ④'s obligation.

## Impact

- `src/UBookIt.Persistence`: gains all persistence code plus package references `Umbraco.Cms.Persistence.EFCore` (MIT) and `Microsoft.EntityFrameworkCore.SqlServer` (MIT), versions pinned in central package management.
- `src/UBookIt.Core`: additive public rehydration factory on `Booking`; nothing else moves.
- `tests/UBookIt.Tests.Integration`: new project using `xunit.v3` (Apache-2.0; adds runtime `Assert.Skip`, required for the skip-loudly-when-unreachable behaviour the `persistence` spec mandates — the unit test project stays on xunit v2); added to the solution and runnable locally against Developer Edition/LocalDB. Azure Pipelines viability: Windows-hosted agents include LocalDB, and a `mssql` service container is the Linux alternative — CI wiring itself stays with the future CI change.
- `src/UBookIt.TestSite`: no code change beyond the already-made user-secrets wiring; on next boot after this change, the uBookIt tables appear in the site database.
- Public API surface: additions only (rehydration factory in Core; the Persistence assembly's composer and DbContext become public infrastructure types). Called out per convention; nothing published yet, so this extends the pre-release baseline.
