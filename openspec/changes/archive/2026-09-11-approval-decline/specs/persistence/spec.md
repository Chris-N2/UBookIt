# persistence — delta for approval-decline

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
one DbContext for the life of the application.

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

#### Scenario: An absent AutoConfirm setting is on and silent
- **WHEN** the site configuration carries no `UBookIt:AutoConfirm` value
- **THEN** the settings report auto-confirm on and nothing is logged about it

#### Scenario: A malformed AutoConfirm value resolves to on and is reported
- **WHEN** the site configuration carries an `UBookIt:AutoConfirm` value that cannot be read as a boolean
- **THEN** the settings report auto-confirm on and an error is logged identifying the setting

#### Scenario: An explicit off is honoured
- **WHEN** the site configuration carries `UBookIt:AutoConfirm` readable as false
- **THEN** the settings report auto-confirm off

#### Scenario: The job does not capture a scoped dependency
- **WHEN** the retention job's construction is inspected
- **THEN** it holds no scoped service, and obtains the ones it needs within a scope it creates per unit of work

#### Scenario: An unusable privacy policy link does not reach a page
- **WHEN** the site configuration carries a privacy policy link that cannot be used as a link
- **THEN** the settings report no policy link, an error is logged identifying the setting, and no link is rendered

#### Scenario: An absent privacy policy link is silent
- **WHEN** the site configuration carries no privacy policy link
- **THEN** the settings report none and nothing is logged about it
