# persistence

## ADDED Requirements

### Requirement: SQL Server is the required backend
The package SHALL require SQL Server 2019 or later (including LocalDB and Azure SQL). No other database provider SHALL be supported or abstracted for. This requirement SHALL be stated in package documentation.

#### Scenario: Persistence targets SQL Server only
- **WHEN** the `UBookIt.Persistence` project's provider configuration is inspected
- **THEN** the only configured EF Core provider is SQL Server

### Requirement: Schema shape and naming
All uBookIt tables SHALL carry the `uBookIt` prefix. The schema SHALL comprise: `uBookItResource` (id, type key, display name, description, constraint values), `uBookItResourceOpenHours` (per weekly window: day of week, start time, end time), `uBookItResourceException` (per exception window: date, nullable start/end times where a closure is a single row with NULL times), `uBookItBooking` (UTC start and end, IANA time zone id, status, created UTC, nullable member key, booker name, email, nullable phone), and `uBookItResourceClaim` (booking id, resource id, unique per pair). The mapping SHALL round-trip the Core value objects without loss.

#### Scenario: Resource availability round-trips
- **WHEN** a resource with weekly windows, a closure exception, an override exception, and non-default constraints is saved and reloaded through the store
- **THEN** the reloaded `AvailabilityConfiguration` is value-equal to the original

#### Scenario: Booking round-trips
- **WHEN** a placed booking is reloaded through the store
- **THEN** its interval UTC instants, zone id, status, booker (member key and contact details), created timestamp, and claims are value-equal to what was placed

### Requirement: Migrations apply at startup into a package-private history table
EF Core migrations SHALL be applied automatically during Umbraco application startup once the site database is configured, and SHALL be recorded in the `__uBookItEFMigrationsHistory` table, never EF Core's default history table. Migration application SHALL be idempotent. Migrations SHALL be additive-only per project convention; a destructive migration requires explicit spec approval.

#### Scenario: Fresh database receives the schema
- **WHEN** migrations run against a database without uBookIt tables
- **THEN** all uBookIt tables and `__uBookItEFMigrationsHistory` exist afterwards

#### Scenario: Re-running migrations is a no-op
- **WHEN** migrations run a second time against an up-to-date database
- **THEN** no error occurs and the schema is unchanged

### Requirement: Store implementations honour Core semantics
`UBookIt.Persistence` SHALL provide SQL Server implementations of `IResourceStore` and `IBookingStore`. `GetClaimsAsync` SHALL return claims of any status whose booking interval overlaps the queried half-open range for the resource, and SHALL be served by an index on the booking interval (no table scan of bookings by date). `UpdateAsync` SHALL persist status changes.

#### Scenario: Claims query uses half-open overlap
- **WHEN** a booking ends exactly at the queried range start
- **THEN** its claims are not returned

#### Scenario: Status change persists
- **WHEN** a booking is cancelled via the booking service and reloaded
- **THEN** its stored status is `Cancelled` and its claims no longer block placement

### Requirement: Atomic placement on SQL Server
`IBookingStore.PlaceAsync` SHALL execute within a single database transaction that (1) acquires an exclusive per-resource application lock (`sp_getapplock`, transaction-owned, lock resource derived from the resource id) for every claimed resource in ascending resource-id order, (2) re-checks conflicts (half-open overlap against blocking-status claims) under that lock, and (3) inserts the booking and its claims. A detected conflict SHALL produce the structured `conflict` failure and leave the database unchanged. Under concurrent conflicting placements, exactly one SHALL succeed.

#### Scenario: Concurrency proof against real SQL Server
- **WHEN** at least 10 conflicting placements for the same resource and interval execute concurrently against a real SQL Server database
- **THEN** exactly one succeeds, all others fail with code `conflict`, and exactly one booking with exactly one claim exists afterwards

#### Scenario: Failed placement leaves no rows
- **WHEN** a placement fails with `conflict`
- **THEN** no booking or claim row from the failed attempt exists

### Requirement: Package composition registers persistence and Core services
An Umbraco composer in `UBookIt.Persistence` SHALL register: the DbContext via Umbraco's EF Core integration (using the site's Umbraco connection string), the two store implementations, `TimeProvider.System`, `IAvailabilityQueryService`, `IBookingService`, and `SiteBookingSettings` bound from the `UBookIt` configuration section. When `UBookIt:TimeZoneId` is absent, the settings SHALL default to `UTC` and a warning SHALL be logged at startup.

#### Scenario: Site boots with services resolvable
- **WHEN** an Umbraco site referencing the package starts with a configured SQL Server database
- **THEN** `IBookingService` and `IAvailabilityQueryService` are resolvable from the container and the uBookIt tables exist

#### Scenario: Missing time zone setting defaults safely
- **WHEN** the site configuration has no `UBookIt:TimeZoneId` value
- **THEN** `SiteBookingSettings.TimeZoneId` is `UTC` and a warning is logged

### Requirement: Integration test coverage on real SQL Server
Integration tests SHALL run against a real SQL Server instance (connection from the `UBOOKIT_TEST_DB` environment variable, defaulting to LocalDB), creating and dropping a uniquely named database per run. They SHALL cover: migration application and idempotency, resource and booking round-trip fidelity, half-open overlap at the SQL layer, status-change persistence, and the concurrency proof. When no SQL Server is reachable the tests SHALL skip with an explicit diagnostic, never silently pass.

#### Scenario: Test database lifecycle
- **WHEN** the integration test run completes
- **THEN** the per-run database has been dropped

#### Scenario: Unreachable server is visible
- **WHEN** no SQL Server is reachable at the configured connection
- **THEN** integration tests report skipped with a reason, not passed
