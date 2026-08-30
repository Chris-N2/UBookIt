## MODIFIED Requirements

### Requirement: Schema shape and naming
All uBookIt tables SHALL carry the `uBookIt` prefix. The schema SHALL comprise: `uBookItResource` (id, type key, display name, description, constraint values), `uBookItResourceOpenHours` (per weekly window: day of week, start time, end time), `uBookItResourceException` (per exception window: date, nullable start/end times where a closure is a single row with NULL times), `uBookItResourceCapability` (resource id and capability key, unique per pair), `uBookItServiceRoleCapability` (service role id and capability key, unique per pair), `uBookItBooking` (UTC start and end, IANA time zone id, status, created UTC, nullable member key, booker name, email, nullable phone, **nullable service id and nullable service name**), and `uBookItResourceClaim` (booking id, resource id, unique per pair). The mapping SHALL round-trip the Core value objects without loss.

Each capability table SHALL enforce uniqueness of its owner-and-key pair at the schema level, so that a duplicate capability is impossible in storage and not only in the domain. Capability rows SHALL be removed with their owning resource or service role.

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
- **THEN** its interval UTC instants, zone id, status, booker (member key and contact details), created timestamp, service attribution, and claims are value-equal to what was placed

#### Scenario: A booking's service attribution survives deleting the service
- **WHEN** the service a stored booking records is deleted
- **THEN** the booking row remains, its service id and stored service name are unchanged, and no cascade removes or nulls them

#### Scenario: A directly placed booking stores no service
- **WHEN** a booking placed directly is reloaded through the store
- **THEN** its service columns are NULL, and it reports no service attribution rather than an empty one
