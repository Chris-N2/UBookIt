# Design: persistence

## Context

`UBookIt.Core` defines two store ports and a contract: booking placement must be atomic with respect to conflict detection. This change implements those ports on EF Core + SQL Server inside an Umbraco 17 site. Constraints in force: additive-only migrations; SQL Server 2019+ is the required backend (explicit project decision — no provider abstraction); Umbraco packages coexist in one database, so nothing uBookIt does may collide with Umbraco's own schema or other packages' EF usage; the QA-mandated `Booking` rehydration obligation from core-domain must be discharged here.

## Goals / Non-Goals

**Goals:**

- Durable storage for resources (including availability configuration), bookings, and claims, shaped so future additive changes (persons-as-resources, approval, capacity) need no destructive migration.
- The atomic-placement guarantee enforced by SQL Server primitives and proven by a real concurrency test, not argued from code inspection.
- Zero-friction operations: migrations apply themselves at startup; the package uses the site's existing Umbraco connection string; one composer wires everything.
- Integration tests runnable by any contributor with LocalDB or a local SQL Server, no Docker required.

**Non-Goals:**

- Everything in the proposal's Non-goals. Additionally: no repository-pattern layer beyond the Core ports (the ports *are* the repository abstraction), and no caching — correctness first, performance changes come with evidence.

## Decisions

### D1: Umbraco's EF Core integration, site connection string

The DbContext registers via `Umbraco.Cms.Persistence.EFCore`'s `AddUmbracoDbContext<UBookItDbContext>(...)` (with `shareUmbracoConnection: true`), which supplies the site's `umbracoDbDSN` connection string and provider. *(Corrected during apply: the extension is named `AddUmbracoDbContext`, not `AddUmbracoEFCoreContext` as first drafted.)*

- **Why**: one database, one connection string, zero extra configuration for installers; it is the documented pattern for Umbraco packages using EF Core.
- **Alternative considered**: package-specific connection string key — rejected: real sites would nearly always point it at the same database anyway, and a second string is one more thing to misconfigure.

### D2: Package-private migrations history table

Migrations record into `__uBookItEFMigrationsHistory` (via `MigrationsHistoryTable`), not EF's default `__EFMigrationsHistory`.

- **Why**: multiple EF-using packages sharing the default history table in one Umbraco database corrupt each other's migration state. Isolation makes uBookIt a good citizen.

### D3: Migrations run from an Umbraco startup notification handler

A handler on `UmbracoApplicationStartedNotification` (the documented pattern; corrected from "application-starting" during apply) calls `Database.Migrate()` when the runtime level indicates a configured database (`Run` or `Upgrade`). Additive-only enforcement is procedural (spec + QA review gate), not tooling.

- **Why**: package consumers install a NuGet package and boot; requiring `dotnet ef database update` would be install friction Umbraco users do not expect. Umbraco's own migrations work the same way.
- **Alternative considered**: `IHostedService` — rejected: runs before Umbraco decides the database is ready/installed; the notification pattern sequences correctly with Umbraco's install pipeline.

### D4: Relational schema, `uBookIt` table prefix

Tables: `uBookItResource` (identity, type key, display name, description, constraint columns as integer minutes/days), `uBookItResourceOpenHours` (one row per weekly window: day-of-week, start `time`, end `time`), `uBookItResourceException` (one row per exception window; a closure is a single row with NULL times; unique filtered semantics enforced per (resource, date) in code and index), `uBookItBooking` (UTC start/end as `datetimeoffset`, IANA zone id, status `int`, created UTC, member key `uniqueidentifier` NULL, booker name/email/phone), `uBookItResourceClaim` (booking FK, resource FK; composite unique (BookingId, ResourceId)).

- **Why relational over JSON columns for availability**: change ③'s management screens query and edit windows/exceptions individually; relational rows are additive-migration-friendly and indexable. JSON would be simpler to map today and opaque tomorrow.
- **Why claims stay normalized** (no denormalized interval/status copy): the overlap query joins claim → booking; denormalizing invites update anomalies on status change. Indexes: `uBookItResourceClaim(ResourceId)` include `BookingId`, `uBookItBooking(StartUtc, EndUtc)` include `Status` — the availability date-range lookup hits an index (QA performance gate). Revisit only with measured evidence.

### D5: Atomic placement via per-resource application lock

`PlaceAsync` runs one transaction: for each claimed resource id (sorted ascending — deadlock-proof for future multi-claim bookings), acquire `sp_getapplock @Resource='ubookit:resource:<guid>', @LockMode='Exclusive', @LockOwner='Transaction'`; then run the overlap check (half-open interval, blocking statuses); insert booking + claims; commit. Lock release is implicit at transaction end. Conflict → the structured `conflict` failure from the contract.

- **Why**: an app lock serializes exactly the contended unit (one resource) without touching isolation levels or relying on range-lock subtleties; it is easy to reason about and easy to prove.
- **Lock timeout semantics** *(added during QA remediation)*: failure to acquire the lock within 15 s surfaces as an exception (SQL error 51000), not a structured domain failure — a saturated lock is an infrastructure fault (something is holding a resource's calendar far beyond any placement's duration), not an expected booking outcome, so the structured-results convention (core design D5) deliberately does not apply.
- **Alternatives considered**: SERIALIZABLE isolation with range predicates — rejected: correct but deadlock-prone under contention and harder to review; unique constraint on quantized slots — rejected in core-domain D2 (interval model).

### D6: Booking rehydration is a distinct, validating factory (Core, additive)

`Booking.Rehydrate(id, interval, booker, claims, status, createdUtc)` — public, static, documented as a persistence-boundary API. It enforces structural invariants (≥1 claim, no duplicate resources) but accepts any status without transition-machine involvement (the stored status is history, not a transition).

- **Why distinct from placement creation**: placement semantics (auto-confirm, v1 single claim) are service policy; rehydration must faithfully restore whatever was legally stored, including statuses no v1 pathway creates. Keeping `Booking.Create` internal preserves D9 minimalism.

### D7: Integration tests on real SQL Server, database-per-run, no Docker requirement

New project `UBookIt.Tests.Integration`: connection string from `UBOOKIT_TEST_DB` environment variable, defaulting to LocalDB (`(localdb)\MSSQLLocalDB`); each run creates a uniquely named database, migrates it, and drops it afterwards. The concurrency proof runs N parallel `PlaceAsync` calls for the same interval through the real store and asserts exactly one success.

- **Why**: the atomic-placement guarantee is a claim about SQL Server behaviour; only SQL Server can prove it. LocalDB ships with VS and Windows CI agents; the env var accommodates Developer Edition locally and a `mssql` container on Linux CI later. Unit tests in `UBookIt.Tests` stay database-free and fast.

### D8: Composition in a Persistence composer; settings from configuration

An `IComposer` in `UBookIt.Persistence` registers the DbContext (D1), both stores, `TimeProvider.System`, the two Core services, and `SiteBookingSettings` bound from the `UBookIt` configuration section (`UBookIt:TimeZoneId`, default `"UTC"` with a startup log warning when defaulted).

- **Why default-UTC rather than fail-fast**: a package that crashes a site on install because a setting is missing is hostile; a wrong-but-safe default plus a warning is recoverable. ③'s backoffice can surface the setting later.

## Risks / Trade-offs

- [App-lock serializes all placements per resource] → intended: contention unit is a single resource's calendar; throughput across resources is unaffected. Accepted for v1; measured evidence required before anything cleverer.
- [`Database.Migrate()` at startup can race in web-farm scenarios] → EF acquires its own migration locks on SQL Server 2019+; additionally the operation is idempotent. Documented as "first boot after upgrade should be single-instance" in the README when packaging arrives.
- [LocalDB default may be absent on non-Windows dev machines] → env var override is the escape hatch; tests skip with an explicit diagnostic (not silent pass) when no server is reachable — a skipped concurrency proof is visible in QA gate 2.
- [Relational availability schema makes ③'s editing model rigid if the shape was wrong] → the shape mirrors the Core value objects one-to-one, which QA-approved specs already fixed; risk is low and changes remain additive (new columns/tables).
- [Booker personal data now at rest] → named non-goal for tooling, but schema keeps contact fields on the booking row only (no separate person table to orphan), which keeps future erasure tooling a single-table concern.

## Open Questions

- None blocking. Table/column naming is fixed by the spec below; EF package versions pin to the EF Core major that `Umbraco.Cms.Persistence.EFCore` 17.5.x ships against (resolved concretely during apply via CPM).
