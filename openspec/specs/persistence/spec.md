# persistence Specification

## Purpose

Defines the SQL Server persistence layer for uBookIt: the required database backend, schema shape and naming, startup migrations into a package-private history table, store implementations honouring Core semantics, atomic placement on SQL Server, package composition, and integration test coverage against a real SQL Server instance.
## Requirements
### Requirement: SQL Server is the required backend
The package SHALL require SQL Server 2019 or later (including LocalDB and Azure SQL). No other database provider SHALL be supported or abstracted for. This requirement SHALL be stated in package documentation.

#### Scenario: Persistence targets SQL Server only
- **WHEN** the `UBookIt.Persistence` project's provider configuration is inspected
- **THEN** the only configured EF Core provider is SQL Server

### Requirement: Schema shape and naming
All uBookIt tables SHALL carry the `uBookIt` prefix. The schema SHALL comprise: `uBookItResource` (id, type key, display name, description, constraint values), `uBookItResourceOpenHours` (per weekly window: day of week, start time, end time), `uBookItResourceException` (per exception window: date, nullable start/end times where a closure is a single row with NULL times), `uBookItResourceCapability` (resource id and capability key, unique per pair), `uBookItServiceRoleCapability` (service role id and capability key, unique per pair), `uBookItBooking` (UTC start and end, IANA time zone id, status, created UTC, nullable member key, booker name, email, nullable phone), and `uBookItResourceClaim` (booking id, resource id, unique per pair). The mapping SHALL round-trip the Core value objects without loss.

Each capability table SHALL enforce uniqueness of its owner-and-key pair at the schema level, so that a duplicate capability is impossible in storage and not only in the domain. Capability rows SHALL be removed with their owning resource or service role.

#### Scenario: Resource availability round-trips
- **WHEN** a resource with weekly windows, a closure exception, an override exception, and non-default constraints is saved and reloaded through the store
- **THEN** the reloaded `AvailabilityConfiguration` is value-equal to the original

#### Scenario: Resource capabilities round-trip
- **WHEN** a resource carrying two capabilities is saved and reloaded through the store
- **THEN** the reloaded capability set is value-equal to the original

#### Scenario: Role required capabilities round-trip
- **WHEN** a service whose role requires two capabilities is saved and reloaded through the store
- **THEN** the reloaded required-capability set is value-equal to the original

#### Scenario: An empty capability set round-trips as empty
- **WHEN** a resource carrying no capabilities is saved and reloaded
- **THEN** the reloaded capability set is empty rather than null

#### Scenario: Deleting an owner removes its capability rows
- **WHEN** a resource carrying capabilities is deleted
- **THEN** its capability rows are removed and no orphan remains

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

The multi-resource claims read SHALL be served by a single query over the same index, not by iterating the single-resource read, and SHALL return the same claims that per-resource reads would return for the same ids and range.

The type-filtered resource listing SHALL be a single query filtered on the resource type column, eagerly loading the same child collections as the existing resource reads so returned aggregates are complete enough for availability computation. It SHALL apply no paging.

These additions SHALL require no schema change and no new migration: they read existing tables through existing indexes.

#### Scenario: Claims query uses half-open overlap
- **WHEN** a booking ends exactly at the queried range start
- **THEN** its claims are not returned

#### Scenario: Status change persists
- **WHEN** a booking is cancelled via the booking service and reloaded
- **THEN** its stored status is `Cancelled` and its claims no longer block placement

#### Scenario: Batched claims are one round trip
- **WHEN** claims are read for several resource ids over a range
- **THEN** a single database query serves them, and the result matches per-resource reads for the same ids

#### Scenario: Type listing returns complete aggregates
- **WHEN** resources are listed by type key
- **THEN** each returned resource carries its open hours and date exceptions, sufficient to compute its availability without a further load

#### Scenario: No migration is added
- **WHEN** the migrations folder is inspected after this change
- **THEN** it contains no new migration, and the existing schema is unchanged

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

### Requirement: Resource management write store
`UBookIt.Core` SHALL expose an `IResourceManagementStore` port (create, full update, delete, paged list with total) accepting only domain `Resource` aggregates — which are constructible solely via the validating Core factories — and `UBookIt.Persistence` SHALL implement it against SQL Server. `IResourceStore` SHALL remain unchanged.

#### Scenario: Created resource is readable through the existing store
- **WHEN** a resource is created through the management store
- **THEN** `IResourceStore.GetAsync` returns a value-equal resource

#### Scenario: Paged list reports totals
- **WHEN** 25 resources exist and the management store lists with skip 20, take 10
- **THEN** 5 items and a total of 25 are returned

### Requirement: Write-path exception uniqueness
Availability writes SHALL replace a resource's open-hours and exception rows wholesale within a single transaction, so that duplicate exception dates for a resource cannot exist after any write, including under concurrent updates (each transaction writes a complete, internally consistent set; concurrent full updates are last-writer-wins). (Discharges the write-surface uniqueness obligation recorded at the persistence archive.)

#### Scenario: Racing updates never produce duplicates
- **WHEN** two updates with different exception sets for the same resource run concurrently
- **THEN** the final state equals exactly one writer's complete set and contains at most one exception row group per date

#### Scenario: Update replaces, never merges
- **WHEN** a resource with a Monday window and one exception is updated to have only a Tuesday window and no exceptions
- **THEN** reloading shows exactly the Tuesday window and zero exceptions

### Requirement: Delete semantics at the store
Deleting an unclaimed resource SHALL remove it and its availability child rows. Deleting a resource with any booking claims SHALL fail with stable code `resource-in-use` (a new additive `FailureCodes` entry) and change nothing — including when a claim is placed concurrently with the delete (the restrictive foreign key is the backstop and its violation maps to the same structured failure).

#### Scenario: Delete refused for claimed resource
- **WHEN** the management store deletes a resource that has a booking claim
- **THEN** the result is failure with code `resource-in-use` and the resource and its configuration remain

#### Scenario: Delete removes configuration rows
- **WHEN** an unclaimed resource with open-hours and exception rows is deleted
- **THEN** no rows for that resource remain in any uBookIt table

### Requirement: Capability hydration on the read path
Resource reads used by candidate resolution SHALL hydrate each resource's capability set, so that the eligibility subset test can be evaluated in `UBookIt.Core`. The storage layer SHALL NOT implement a capability-filtered query as an alternative route to eligibility: one implementation of the rule, in the domain, is what prevents storage and domain from disagreeing about which resources are eligible.

#### Scenario: Resources listed by type carry their capabilities
- **WHEN** resources of a given type are listed through the read port
- **THEN** each returned resource carries its capability set, hydrated from storage

#### Scenario: Eligibility is not evaluated in SQL
- **WHEN** the persistence layer is inspected
- **THEN** no query filters resources by required capabilities as a substitute for the domain's subset test

### Requirement: Capability projections on the management store
The management store SHALL provide the distinct capability keys in use with their resource counts, as a projection over existing storage. It SHALL aggregate server-side and SHALL be ordered deterministically by key.

The management store SHALL NOT provide a projection that selects resources by required capabilities. Matching resources to a role is an eligibility question, and eligibility SHALL have exactly one implementation, in `UBookIt.Core`, over resources the read port hydrates. A storage-layer projection applying the same rule is a second implementation free to diverge — in the predicate itself, in its ordering, or in the test doubles that stand in for it — and a backoffice answer that diverges from the booking path's is worse than no answer.

This projection SHALL live on the management store only. The read port used by booking SHALL NOT gain it: the backoffice's needs are not the booking path's needs, and keeping the ports separate is what stopped one reaching across the other previously.

#### Scenario: Usage projection aggregates server-side
- **WHEN** the capability usage projection runs
- **THEN** the counts are computed by the database, and the result scales with the number of distinct keys rather than the number of resources

#### Scenario: Usage projection is deterministically ordered
- **WHEN** the capability usage projection is run twice against unchanged data
- **THEN** both results present the same keys in the same order

#### Scenario: No capability matching in storage
- **WHEN** the persistence layer is inspected
- **THEN** no projection or query selects resources by a required-capability set

#### Scenario: Projections stay off the read port
- **WHEN** the read port used by candidate resolution is inspected
- **THEN** it does not expose the usage projection
