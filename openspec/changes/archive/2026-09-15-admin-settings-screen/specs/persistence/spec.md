<!--
GUARANTEE DIFF for the wholesale replacement below. The existing requirement carries six SHALL
blocks and eleven scenarios. Disposition of each, per the project rule that a MODIFIED requirement
deletes whatever it forgets to restate:

  SHALL 1  composer registers DbContext/stores/TimeProvider/services/settings/job
           → CARRIED, amended: the settings' SOURCE (configuration → store over configuration)
             and LIFETIME (singleton → scoped) change. Everything else restated verbatim.
  SHALL 1b absent TimeZoneId defaults to UTC with a warning        → CARRIED unchanged
  SHALL 2  retention job registered unconditionally, does nothing  → CARRIED unchanged
  SHALL 3  retention resolved unlike MaxQueryRangeDays; no default → CARRIED unchanged
  SHALL 4  AutoConfirm resolves on unless readably turned off      → CARRIED unchanged
  SHALL 5  job obtains scoped services per unit of work            → CARRIED and STRENGTHENED
             (the guard must derive the scoped set from registrations, not a fixed list)
  SHALL 6  unusable privacy link treated as absent and reported    → CARRIED unchanged

  Scenarios 1-11 ALL CARRIED. Wording made source-agnostic where the behaviour is identical for a
  stored and a configured value — which is all of them, because Decision 2 in design.md keeps the
  resolution a single code path. Four scenarios ADDED for the lifetime and the two-source path.

  DELIBERATE DROPS: none.
-->

## MODIFIED Requirements

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

## ADDED Requirements

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
