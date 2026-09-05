## MODIFIED Requirements

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
