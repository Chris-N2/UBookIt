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
All uBookIt tables SHALL carry the `uBookIt` prefix. The schema SHALL comprise: `uBookItResource` (id, type key, display name, description, constraint values), `uBookItResourceOpenHours` (per weekly window: day of week, start time, end time), `uBookItResourceException` (per exception window: date, nullable start/end times where a closure is a single row with NULL times), `uBookItResourceCapability` (resource id and capability key, unique per pair), `uBookItServiceRoleCapability` (service role id and capability key, unique per pair), `uBookItBooking` (**reference**, UTC start and end, IANA time zone id, status, created UTC, nullable member key, **nullable booker name, nullable email, nullable phone, nullable booker-erased UTC**, **nullable service id and nullable service name**), and `uBookItResourceClaim` (booking id, resource id, unique per pair). The mapping SHALL round-trip the Core value objects without loss.

Each capability table SHALL enforce uniqueness of its owner-and-key pair at the schema level, so that a duplicate capability is impossible in storage and not only in the domain. Capability rows SHALL be removed with their owning resource or service role.

**The booking's booker columns SHALL be nullable together, and their nullability SHALL mean
erased.** Name and email are required of every booker the domain places, so a NULL in either is
not a placement that omitted them — it is a booking whose personal data was removed. The
erased-UTC column is what says so: it SHALL be non-NULL exactly when the contact columns are
NULL, so that "erased" is recorded as a fact rather than inferred from missing values. **The
member key SHALL be cleared by the same operation**, so that no column of the row identifies
the person.

**Erasure SHALL be an UPDATE of the booking's row, never a DELETE.** The row keeps its
reference, interval, status and claims; only the person leaves. A schema-level cascade or
trigger that removed the row on erasure would return sold time to availability.

**The booking's reference SHALL be stored in canonical form and SHALL be uniquely indexed.**
Canonical form — a fixed-length, upper-case value with no separator — is what makes the index
mean anything: a reference stored as it was typed would let two rows differ only by case and
be, to every person who reads them, the same reference. **The uniqueness SHALL be enforced by
the schema**, because a check performed before writing is a race, and two bookings sharing a
reference makes both of them unquotable — which is the one thing a reference exists to prevent.
**Erasure SHALL NOT alter the reference**, so an erased booking remains quotable.

**The booking's service columns SHALL NOT carry a foreign key to the service table, and
SHALL NOT be affected by any cascade.** A booking is a historical fact that must survive
the service being renamed, retired or deleted. A nullable foreign key with `ON DELETE SET
NULL` would convert "placed for a service that no longer exists" into "placed directly",
which is the one meaning a NULL service id must keep.

**The stored service name SHALL be the name as it was at placement time**, not a value
resolved on read. Joining the service table on read would return the *current* name, and
would return nothing at all once the service is deleted — so a row would either retitle
itself after the fact or lose an attribution it definitely had. The id is stored alongside
for a caller that needs the service as it is now.

**The booker's email column SHALL be indexed.** The index exists so a subject's bookings can be
found from their address without a window — see the `booking-management` capability. Unindexed,
that read is a scan of a table which grows without limit, reintroducing the cost the list's window
guard exists to bound; indexed, it costs what that person's bookings cost.

**It SHALL NOT be unique.** One person may book many times, and a uniqueness constraint here would
refuse the second booking. It is also not a natural key: erasure sets the column NULL, so the
value is not stable for the life of the row.

*An index on personal data, added in order to build the tool that removes personal data — which
reads oddly and is therefore written down rather than left to be noticed. It is sound: an UPDATE
maintains the index with the row, so an erased booking leaves the index at the moment its address
does, and the index holds nothing the table does not.*

**The migration introducing the booker columns' nullability SHALL be additive and SHALL NOT
back-fill.** Widening a non-nullable column to nullable destroys nothing and cannot fail on
existing data; every existing row keeps its contact details and acquires a NULL erased-UTC,
which is the correct reading of a booking nobody has erased.

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
- **THEN** its interval UTC instants, zone id, status, booker (member key and contact details), created timestamp, service attribution, reference, and claims are value-equal to what was placed

#### Scenario: A booking's reference cannot be duplicated in storage
- **WHEN** a booking is stored carrying a reference another booking already holds
- **THEN** the store refuses it, and no partial row remains

#### Scenario: A booking's service attribution survives deleting the service
- **WHEN** the service a stored booking records is deleted
- **THEN** the booking row remains, its service id and stored service name are unchanged, and no cascade removes or nulls them

#### Scenario: A directly placed booking stores no service
- **WHEN** a booking placed directly is reloaded through the store
- **THEN** its service columns are NULL, and it reports no service attribution rather than an empty one

#### Scenario: An erasure reaches the database
- **WHEN** a booking's booker is erased through the store and the row is then re-read in a fresh context
- **THEN** its booker name, email, phone and member key columns are NULL and its erased-UTC column carries the erasure instant

#### Scenario: An erased booking round-trips
- **WHEN** a booking whose booker was erased is reloaded through the store
- **THEN** it reports an erased booker with the stored erasure instant, and its interval, zone, status, created timestamp, service attribution, reference and claims are value-equal to what was placed

#### Scenario: Erasure keeps the row and its claims
- **WHEN** a booking's booker is erased
- **THEN** the booking row and every one of its claim rows remain, and no cascade removes them

#### Scenario: The booker email column is indexed and not unique
- **WHEN** the schema is inspected
- **THEN** an index covers the booker email column, and it does not enforce uniqueness

#### Scenario: One booker may hold several bookings
- **WHEN** two bookings are stored carrying the same booker email address
- **THEN** both are accepted

#### Scenario: An unerased booking has no erasure instant
- **WHEN** a booking that has never been erased is reloaded through the store
- **THEN** its erased-UTC column is NULL and it reports contact details rather than an erasure

### Requirement: Migrations apply at startup into a package-private history table
EF Core migrations SHALL be applied automatically during Umbraco application startup once the site database is configured, and SHALL be recorded in the `__uBookItEFMigrationsHistory` table, never EF Core's default history table. Migration application SHALL be idempotent. Migrations SHALL be additive-only per project convention; a destructive migration requires explicit spec approval.

#### Scenario: Fresh database receives the schema
- **WHEN** migrations run against a database without uBookIt tables
- **THEN** all uBookIt tables and `__uBookItEFMigrationsHistory` exist afterwards

#### Scenario: Re-running migrations is a no-op
- **WHEN** migrations run a second time against an up-to-date database
- **THEN** no error occurs and the schema is unchanged

### Requirement: Store implementations honour Core semantics
`UBookIt.Persistence` SHALL provide SQL Server implementations of `IResourceStore` and `IBookingStore`. `GetClaimsAsync` SHALL return claims of any status whose booking interval overlaps the queried half-open range for the resource, and SHALL be served by an index on the booking interval (no table scan of bookings by date). **`UpdateAsync` SHALL persist a booking's status, and SHALL NOT write its booker or its interval.** A booking's
booker is written by the erasure operation below and by nothing else; a booking's interval is
written by the move write, per *Atomic move on SQL Server*, and by nothing else after placement.

**The three writes SHALL touch disjoint columns.** Callers read, mutate and write back with no
re-read, so an aggregate handed to a store can be older than the stored row. A write that
carries columns its caller did not change makes that staleness everyone's problem: a
cancellation would restore a person somebody erased in between, an erasure would revert a
cancellation and re-block a slot that had been released, and a move written from a stale
aggregate would do either. Bounding each write to what its verb actually changes removes the
interaction rather than defending against it.

**A store SHALL expose an operation that erases a booking's booker, taking the booking's id and
the instant** — not an aggregate, so there is no stale copy of anything to write back.

**The erasure SHALL be absorbing at the point of storage.** Once a booking records an erasure, a
later erasure SHALL leave it exactly as it stands, including the first instant, and **no
operation any implementation offers SHALL return an erased booker to carrying contact details.**
This is a promise the package makes to a data subject; an implementation that let a later write
restore a person would falsify it while every test written against the port passed.

**The check SHALL NOT be a read followed by a write.** An implementation that reads the stored
state, decides, and then writes leaves a window in which an erasure can commit between the two
statements — which is not a smaller version of the guarantee but the absence of it. The
condition belongs inside the write. The same holds for the move write's status condition: the
status the move is permitted from SHALL be a predicate of the update statement, not a read
before it.

**A change SHALL be observable by re-reading.** Verification SHALL read the booking back from
storage rather than inspecting the instance that was passed in, because the instance carries the
change whether or not the store wrote it.

The multi-resource claims read SHALL be served by a single query over the same index, not by iterating the single-resource read, and SHALL return the same claims that per-resource reads would return for the same ids and range.

The type-filtered resource listing SHALL be a single query filtered on the resource type column, eagerly loading the same child collections as the existing resource reads so returned aggregates are complete enough for availability computation. It SHALL apply no paging.

**The claims reads and the type-filtered listing** SHALL require no schema change and no new migration: they read existing tables through existing indexes. (Previously stated of "these additions" and scoped by its change; restated against the reads it was always about, because as a standing sentence it read as a prohibition on the package ever adding a migration — which the booker columns do add.)

#### Scenario: Claims query uses half-open overlap
- **WHEN** a booking ends exactly at the queried range start
- **THEN** its claims are not returned

#### Scenario: Status change persists
- **WHEN** a booking is cancelled via the booking service and reloaded
- **THEN** its stored status is `Cancelled` and its claims no longer block placement

#### Scenario: A later write cannot restore an erased booker
- **WHEN** a booking is read, then erased by another caller, and the first caller then writes its stale copy back through the store
- **THEN** the stored booker remains erased with its original instant, and the rest of that caller's change is applied

#### Scenario: Re-writing an already-erased booking does not move the instant
- **WHEN** an erased booking is written back through the store
- **THEN** its stored erasure instant is unchanged

#### Scenario: A booker erasure persists
- **WHEN** a booking's booker is erased via the booking service and the booking is reloaded in a fresh context
- **THEN** its stored booker columns are NULL and its stored erasure instant is set

#### Scenario: Persistence is verified by re-reading, not by the passed instance
- **WHEN** the tests covering `UpdateAsync` are inspected
- **THEN** they assert against state read back from storage rather than against the aggregate handed to the store

#### Scenario: The two writes touch disjoint columns
- **WHEN** the booking store's write surface is inspected
- **THEN** the status write does not write the booker, and the erasure write does not write the status

#### Scenario: The three writes touch disjoint columns
- **WHEN** the booking store's write surface is inspected
- **THEN** the move write writes the interval columns only, and neither the status write nor the erasure write writes the interval

#### Scenario: The move write's condition is inside the statement
- **WHEN** the SQL move write is inspected
- **THEN** the permitted-status predicate is part of the update statement rather than a query issued before it

#### Scenario: The erasure write takes an id and an instant
- **WHEN** the erasure operation's signature is inspected
- **THEN** it takes the booking's id and the instant, and no aggregate whose other values it could write back

#### Scenario: Batched claims are one round trip
- **WHEN** claims are read for several resource ids over a range
- **THEN** a single database query serves them, and the result matches per-resource reads for the same ids

#### Scenario: Type listing returns complete aggregates
- **WHEN** resources are listed by type key
- **THEN** each returned resource carries its open hours and date exceptions, sufficient to compute its availability without a further load

#### Scenario: No migration is added
- **WHEN** the multi-resource claims read and the type-filtered listing are inspected
- **THEN** they read existing tables through existing indexes, requiring no schema change

### Requirement: Atomic placement on SQL Server
`IBookingStore.PlaceAsync` SHALL execute within a single database transaction that (1) acquires an exclusive per-resource application lock (`sp_getapplock`, transaction-owned, lock resource derived from the resource id) for every claimed resource in ascending resource-id order, (2) re-checks conflicts (half-open overlap against blocking-status claims) under that lock, (3) verifies the booking's reference is unused, and (4) inserts the booking and its claims. A detected conflict SHALL produce the structured `conflict` failure, and a reference already in use SHALL produce `reference-taken`; either SHALL leave the database unchanged. **The reference check is for reportability, not for correctness** — the unique index is what guarantees uniqueness, and step (3) exists so that a collision can be answered with another reference instead of a database exception. Under concurrent conflicting placements, exactly one SHALL succeed.

#### Scenario: Concurrency proof against real SQL Server
- **WHEN** at least 10 conflicting placements for the same resource and interval execute concurrently against a real SQL Server database
- **THEN** exactly one succeeds, all others fail with code `conflict`, and exactly one booking with exactly one claim exists afterwards

#### Scenario: Failed placement leaves no rows
- **WHEN** a placement fails with `conflict`
- **THEN** no booking or claim row from the failed attempt exists

#### Scenario: A reference already in use is refused inside the same transaction
- **WHEN** a placement carries a reference another booking already holds
- **THEN** it fails with `reference-taken` and no booking or claim row from the attempt exists

### Requirement: Package composition registers persistence and Core services

An Umbraco composer in `UBookIt.Persistence` SHALL register: the DbContext via Umbraco's EF Core
integration (using the site's Umbraco connection string), the two store implementations,
`TimeProvider.System`, `IAvailabilityQueryService`, `IBookingService`, the settings store,
`SiteBookingSettings`, and **the retention background job**. When no `UBookIt:TimeZoneId` value is
available from any source, the settings SHALL default to `UTC` and a warning SHALL be logged at
startup.

**`SiteBookingSettings` SHALL be resolved per scope rather than once at startup**, from the site's
configuration with the package's stored settings composed over it. This replaces a registration that
read the configuration once and held the result for the application's lifetime, so that a setting
changed through the backoffice takes effect without a restart. The record's shape and every
consumer's constructor are unchanged; only the registered lifetime and the source are.

**The stored settings SHALL be composed as a configuration layer beneath the existing resolution,
not resolved separately.** Every fallback below SHALL therefore apply identically to a stored value
and a configured one, from a single implementation — because two implementations of rules this
carefully asymmetric would drift, and the drift would be silent.

**The retention job SHALL be registered whether or not retention is configured**, and SHALL do
nothing when it is not. Registering it conditionally would make the setting's effect depend on
the state of configuration at startup in a second, invisible way, and would mean a site that
fixed a mistyped value still had no job to run until somebody noticed a restart was needed for a
different reason than they thought.

**The retention period SHALL be resolved on different terms from the package's other numeric
setting, and the difference is deliberate.** `UBookIt:MaxQueryRangeDays` falls back to a working
default when it cannot be read, because the cost of being wrong is a rejected query. The
retention period SHALL NOT: a value that is absent, blank, non-numeric, zero or negative SHALL
resolve to **no retention**, never to a default period, because the cost of being wrong is the
irreversible destruction of personal data. A value that was written and could not be read SHALL
be logged as an **error** at startup; an absent value SHALL NOT be complained about, being an
ordinary choice. See the `booking-retention` capability, which owns the meaning of the setting.

**`UBookIt:AutoConfirm` SHALL resolve to on unless a readable value turns it off.** An absent
value SHALL resolve to on, silently — it is the default and an ordinary choice. A value that
was written and cannot be read as a boolean SHALL resolve to on **and** be logged as an error
identifying the setting, on the retention period's precedent: the site wrote something and is
not getting what it wrote, so it is told. The fallback direction is on — today's behaviour —
because neither misreading is safe and only one of them is silent: a site accidentally *on*
sends confirmations it can see and correct, while a site accidentally *off* parks customers'
bookings in a state nobody is watching for. See the `bookings` capability, which owns the
setting's meaning.

**The job SHALL obtain the scoped services it needs per unit of work**, rather than holding them.
Umbraco's background jobs are singletons resolved from the root container while the package's
stores, services and DbContext are scoped; a singleton capturing a scoped dependency would hold
one DbContext for the life of the application. **`SiteBookingSettings` is now among the scoped
services this forbids the job to hold**, and the guard enforcing it SHALL determine what is scoped
from the container's own registrations rather than from a fixed list of type names — a list cannot
see a type whose lifetime changed after the list was written, which is exactly what happens here.

**A configured privacy policy link that cannot be used as a link SHALL be treated as absent**, and
reported at startup, rather than carried through to a public page. It is resolved on the same
principle as the retention period and for a related reason: the cost of being wrong is visible to
visitors rather than to an operator. A site that configured nothing is silent, as with every other
setting; a site that configured something unusable is told, because the alternative is a broken
link on the page where the package asks people for their contact details. See the `privacy-notice`
capability, which owns the meaning of the setting.

#### Scenario: Site boots with services resolvable
- **WHEN** an Umbraco site referencing the package starts with a configured SQL Server database
- **THEN** `IBookingService` and `IAvailabilityQueryService` are resolvable from the container and the uBookIt tables exist

#### Scenario: Missing time zone setting defaults safely
- **WHEN** neither the site configuration nor the store carries a `UBookIt:TimeZoneId` value
- **THEN** `SiteBookingSettings.TimeZoneId` is `UTC` and a warning is logged

#### Scenario: The retention job is registered
- **WHEN** an Umbraco site referencing the package starts
- **THEN** the retention job is registered as scheduled background work, whether or not a retention period is configured

#### Scenario: A malformed retention period does not become a default
- **WHEN** a retention period that cannot be read as a positive whole number of days is in effect from either source
- **THEN** the settings report no retention period, no default is substituted, and an error is logged identifying the setting

#### Scenario: An absent retention period is silent
- **WHEN** neither source carries a retention period
- **THEN** the settings report no retention period and nothing is logged about it

#### Scenario: An absent AutoConfirm setting is on and silent
- **WHEN** neither source carries a `UBookIt:AutoConfirm` value
- **THEN** the settings report auto-confirm on and nothing is logged about it

#### Scenario: A malformed AutoConfirm value resolves to on and is reported
- **WHEN** a `UBookIt:AutoConfirm` value that cannot be read as a boolean is in effect from either source
- **THEN** the settings report auto-confirm on and an error is logged identifying the setting

#### Scenario: An explicit off is honoured
- **WHEN** a `UBookIt:AutoConfirm` value readable as false is in effect from either source
- **THEN** the settings report auto-confirm off

#### Scenario: The job does not capture a scoped dependency
- **WHEN** the retention job's construction is inspected against the container's registrations
- **THEN** it holds no service registered as scoped, including `SiteBookingSettings`, and obtains the ones it needs within a scope it creates per unit of work

#### Scenario: The captive-dependency guard fails when the job captures one
- **WHEN** the retention job is given a constructor parameter whose service is registered as scoped
- **THEN** the guard fails, whether or not that type was known when the guard was written

#### Scenario: An unusable privacy policy link does not reach a page
- **WHEN** a privacy policy link that cannot be used as a link is in effect from either source
- **THEN** the settings report no policy link, an error is logged identifying the setting, and no link is rendered

#### Scenario: An absent privacy policy link is silent
- **WHEN** neither source carries a privacy policy link
- **THEN** the settings report none and nothing is logged about it

#### Scenario: The settings are resolved per scope
- **WHEN** `SiteBookingSettings` is resolved in two separate scopes with a stored value changed between them
- **THEN** the second scope sees the new value, without the application having restarted

#### Scenario: A stored value and a configured value take the same path
- **WHEN** the same unreadable text is supplied once as a configured value and once as a stored value
- **THEN** the setting resolves identically and reports identically in both cases

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

### Requirement: The booking table carries an index supporting the retention sweep

The booking table SHALL carry an index that allows the retention sweep to find bookings due for
erasure without scanning the table. The index SHALL be keyed on the booking's **end** instant,
and SHALL be **filtered to rows whose erased-UTC column is NULL**.

**Keyed on end, because that is what retention compares.** The existing index over the interval
leads on the start instant, so it cannot seek on the end — a sweep relying on it would scan a
table that grows without limit, which is the cost the query-range guardrail exists elsewhere to
bound.

**Filtered, because the due set is defined by rows that have not been erased.** The filter is
what keeps the index small in the steady state: on a site running retention it holds roughly one
retention period's worth of bookings and stops growing, while an unfiltered index would keep an
entry for every booking the site has ever taken, including the erased ones the sweep must never
select again.

The index SHALL be added by an **additive** migration that moves no data. Adding it neither
rewrites a row nor changes what any existing query returns; a site that never configures
retention carries it unused.

*Stated as its own requirement rather than folded into `Schema shape and naming`. That
requirement is long and enumerates the whole schema; replacing it wholesale to add one index
would put every guarantee it carries at risk of being dropped in the restatement, for no gain.
Nothing it says becomes false here — the booking table's columns are unchanged, and this adds a
concern rather than altering one.*

**The cost of that choice, stated rather than left to be discovered.** `Schema shape and naming`
already carries the booker-email index that `find-by-booker` added inside it, so index guarantees
now live in two requirements and a reader of that one gets an incomplete picture of what the
booking table is indexed for. **Any future reader deriving the schema's indexes SHALL read both**,
and a change adding a third index SHOULD decide deliberately which of the two homes it belongs in
rather than inheriting this one by default. Splitting was still the right call here — a wholesale
replacement risks silent deletion, which is worse than a cross-reference — but it is a trade, not
a free win.

#### Scenario: The retention index exists and is filtered
- **WHEN** the schema is inspected
- **THEN** an index covers the booking's end instant and is filtered to rows whose erased-UTC column is NULL

#### Scenario: The sweep's query is served by the index
- **WHEN** the retention sweep selects bookings due for erasure
- **THEN** the selection is served by that index rather than by a scan of the booking table

#### Scenario: The migration adding it moves no data
- **WHEN** the migration introducing the index is applied to a database holding existing bookings
- **THEN** every booking row is unchanged, and the migration is idempotent on re-application

### Requirement: Responsibility assignments are stored in one additive table

The schema SHALL gain one table, `uBookItResponsibility`, holding responsibility assignments
for resources and services alike: a subject discriminator and id, and a party discriminator
and key. The four columns together SHALL be the primary key — an assignment either exists or
does not, carries no payload, and writing the same assignment twice SHALL be indistinguishable
from writing it once. The table SHALL carry an index serving lookup by subject, which is the
only query shape the package runs against it.

The table SHALL hold **keys only**: no names, no email addresses, nothing copied from
Umbraco's user store — the parties are resolved from Umbraco at the moment they are needed, so
nothing here can go stale except the reference itself, which resolution and the editors are
specified to handle.

The migration SHALL be additive, applied by the package's own startup pipeline into its
package-private history table, exactly as every migration before it.

#### Scenario: An assignment round-trips
- **WHEN** an assignment of a user to a resource is stored and read back by subject
- **THEN** the same subject and party come back, and nothing else was stored about either

#### Scenario: Writing an assignment twice stores it once
- **WHEN** the same assignment is written twice
- **THEN** the table holds it once and the second write is not an error

#### Scenario: Deleting the owner removes its rows
- **WHEN** a resource or service with assignments is deleted through its store
- **THEN** its assignment rows are removed in the same operation

#### Scenario: A fresh database receives the table
- **WHEN** migrations run against a database created before this change
- **THEN** the table exists afterwards and every pre-existing table is untouched

### Requirement: One-shot operations are recorded in a package-private flag table

The schema SHALL gain one table, `uBookItFlag`, holding a key and the instant it was
applied — a general marker store for operations the package must perform at most once per
installation, of which the permissions seed is the first. The key SHALL be the primary
key; a flag either exists or does not, and carries nothing else.

The table SHALL hold no personal data of any kind, and the migration SHALL be additive,
applied by the package's own startup pipeline exactly as every migration before it.

A one-shot operation SHALL write its flag only on full success, so an interrupted
operation retries at the next startup; each such operation SHALL therefore be idempotent
over a partial earlier run, and that burden lies with the operation, not the table.

#### Scenario: A flag round-trips

- **WHEN** a flag is written and the table is read back
- **THEN** the key is present with the instant it was applied, and nothing else was stored

#### Scenario: A fresh database receives the table

- **WHEN** migrations run against a database created before this change
- **THEN** the table exists afterwards and every pre-existing table is untouched

#### Scenario: An absent flag means the operation runs

- **WHEN** a one-shot operation starts on an installation whose flag is absent
- **THEN** the operation runs, and the flag exists afterwards only if it fully succeeded

### Requirement: Stored settings are held in their own additive table

The package SHALL store overridden settings in a table of its own, created by an additive migration
that alters and drops nothing.

Each row SHALL hold one setting's key, exactly as the configuration spells it, and its value as
text — the same text a configuration source would supply. **There SHALL be at most one row per
key, and a key with no row SHALL be the representation of "not overridden".**

**No booker's personal data SHALL reach this table, and nothing in it SHALL be subject to
erasure.** The narrower claim is the honest one: the recipient list a site stores here is its own
staff distribution list, which is personal data about staff, and a requirement saying "nothing here
is personal data" would be contradicted by a setting the screen deliberately offers. What is
guaranteed is that no booker's details reach it — the key side is a closed vocabulary of the
package's own setting names and the server refuses anything outside it, so no caller can invent a
key naming a person.

Removing every row SHALL restore the package's behaviour from before the table existed, without a
schema change.

#### Scenario: The migration adds and does not alter
- **WHEN** the migration runs against a database from the previous version
- **THEN** the settings table exists and no existing table, column or index has been altered or dropped

#### Scenario: A fresh install resolves from configuration alone
- **WHEN** the package starts with an empty settings table
- **THEN** every setting resolves from the site's configuration exactly as it did before this version

#### Scenario: One row per key
- **WHEN** a setting already holding a stored value is stored again
- **THEN** the table holds one row for that key, carrying the new value

### Requirement: Atomic move on SQL Server
The move write SHALL execute within a single database transaction that (1) acquires the same
exclusive per-resource application lock placement acquires, for every resource the booking
claims, in ascending resource-id order, (2) checks conflicts for the new interval — half-open
overlap against blocking-status claims — **excluding the claims of the booking being moved**,
under those locks, and (3) updates the booking's interval columns in one statement whose
predicate requires the stored status to be one the move is permitted from. A detected conflict
SHALL produce the structured `conflict` failure and leave the database unchanged. A statement
that updates no row because the status no longer permits a move SHALL be reported as
`invalid-status-transition` and leave the database unchanged. The write SHALL touch no column
but the interval's — not the status, not the booker.

**No new table, and no new column.** The interval columns exist; the move writes them. The
retention index is keyed on the interval's end, so a moved booking is found by the sweep at its
new end, which is the index doing its job.

#### Scenario: Concurrency proof against real SQL Server
- **WHEN** a move of one booking to interval I and at least 10 placements at interval I on the same resource execute concurrently against a real SQL Server database
- **THEN** exactly one of them succeeds, every other fails with code `conflict`, and afterwards exactly one blocking booking holds I

#### Scenario: A move does not conflict with its own claims
- **WHEN** a booking holding 09:00–10:00 is moved to 09:30–10:30 on a resource with no other booking
- **THEN** the move succeeds and the booking holds 09:30–10:30

#### Scenario: A failed move leaves the old interval
- **WHEN** a move fails with `conflict`
- **THEN** the booking's stored interval is the one it held before, and its claims are unchanged

#### Scenario: The status condition is inside the statement
- **WHEN** the booking is cancelled after the move has read it and before the move's update statement runs
- **THEN** the update changes no row, the move reports `invalid-status-transition`, and the stored booking is `Cancelled` at its original interval

#### Scenario: The move write leaves the booker alone
- **WHEN** a booking's booker is erased between the move's read and its write
- **THEN** the booking moves and the booker remains erased with its original instant
