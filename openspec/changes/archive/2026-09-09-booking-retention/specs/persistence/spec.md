## ADDED Requirements

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

## MODIFIED Requirements

### Requirement: Package composition registers persistence and Core services
An Umbraco composer in `UBookIt.Persistence` SHALL register: the DbContext via Umbraco's EF Core integration (using the site's Umbraco connection string), the two store implementations, `TimeProvider.System`, `IAvailabilityQueryService`, `IBookingService`, `SiteBookingSettings` bound from the `UBookIt` configuration section, and **the retention background job**. When `UBookIt:TimeZoneId` is absent, the settings SHALL default to `UTC` and a warning SHALL be logged at startup.

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

**The job SHALL obtain the scoped services it needs per unit of work**, rather than holding them.
Umbraco's background jobs are singletons resolved from the root container while the package's
stores, services and DbContext are scoped; a singleton capturing a scoped dependency would hold
one DbContext for the life of the application.

#### Scenario: Site boots with services resolvable
- **WHEN** an Umbraco site referencing the package starts with a configured SQL Server database
- **THEN** `IBookingService` and `IAvailabilityQueryService` are resolvable from the container and the uBookIt tables exist

#### Scenario: Missing time zone setting defaults safely
- **WHEN** the site configuration has no `UBookIt:TimeZoneId` value
- **THEN** `SiteBookingSettings.TimeZoneId` is `UTC` and a warning is logged

#### Scenario: The retention job is registered
- **WHEN** an Umbraco site referencing the package starts
- **THEN** the retention job is registered as scheduled background work, whether or not a retention period is configured

#### Scenario: A malformed retention period does not become a default
- **WHEN** the site configuration carries a retention period that cannot be read as a positive whole number of days
- **THEN** the settings report no retention period, no default is substituted, and an error is logged identifying the setting

#### Scenario: An absent retention period is silent
- **WHEN** the site configuration carries no retention period
- **THEN** the settings report no retention period and nothing is logged about it

#### Scenario: The job does not capture a scoped dependency
- **WHEN** the retention job's construction is inspected
- **THEN** it holds no scoped service, and obtains the ones it needs within a scope it creates per unit of work
