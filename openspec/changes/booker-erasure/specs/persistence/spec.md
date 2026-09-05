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

#### Scenario: An unerased booking has no erasure instant
- **WHEN** a booking that has never been erased is reloaded through the store
- **THEN** its erased-UTC column is NULL and it reports contact details rather than an erasure

### Requirement: Store implementations honour Core semantics
`UBookIt.Persistence` SHALL provide SQL Server implementations of `IResourceStore` and `IBookingStore`. `GetClaimsAsync` SHALL return claims of any status whose booking interval overlaps the queried half-open range for the resource, and SHALL be served by an index on the booking interval (no table scan of bookings by date). **`UpdateAsync` SHALL persist every part of a booking that the domain permits to change after placement — its status and its booker — and not merely the status.**

**This is a widening, and it closes a real hole.** The previous wording described the only mutation that existed at the time; an implementation faithful to it writes the status column and nothing else, so erasing a booker through the same port would change the aggregate in memory, report success, and leave the row untouched. A test asserting against the returned aggregate would agree it had worked. What a caller needs guaranteed is that a persisted change is persisted, not that one named column is.

**A change SHALL be observable by re-reading.** Verification of `UpdateAsync` SHALL read the booking back from storage rather than inspecting the instance that was passed in, because the instance carries the change whether or not the store wrote it.

**A stored erasure SHALL NOT be overwritten by any later write.** Once a booking's row records
an erasure, a write carrying booker contact details SHALL leave the stored booker untouched —
including the recorded instant — and SHALL apply only the rest of what it carries.

**This is a guarantee about the row, not about detecting a conflict.** Callers do
read-modify-write with no re-read, so an aggregate can be older than the row it overwrites.
For a status that is harmless: a lost transition is refused on the next attempt. For a booker
it is catastrophic, because the stale value is a person and the fresh one is their absence — an
operator who opened a cancellation before a colleague erased the booking would, on completing
it, write the name back over the NULLs, with both requests reporting success and nothing
logging it. Erasure is absorbing at the point of storage or it is not absorbing at all.

**There SHALL be exactly one write path for a booking's mutable state.** Erasure SHALL NOT be given a store method of its own issuing a targeted update — a second path to the same row is free to disagree with the first about what a booking's persisted state is.

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

#### Scenario: Erasure uses the same write path as a status change
- **WHEN** the booking store's write surface is inspected
- **THEN** no separate erasure-only update method exists alongside `UpdateAsync`

#### Scenario: Batched claims are one round trip
- **WHEN** claims are read for several resource ids over a range
- **THEN** a single database query serves them, and the result matches per-resource reads for the same ids

#### Scenario: Type listing returns complete aggregates
- **WHEN** resources are listed by type key
- **THEN** each returned resource carries its open hours and date exceptions, sufficient to compute its availability without a further load

#### Scenario: No migration is added
- **WHEN** the multi-resource claims read and the type-filtered listing are inspected
- **THEN** they read existing tables through existing indexes, requiring no schema change
