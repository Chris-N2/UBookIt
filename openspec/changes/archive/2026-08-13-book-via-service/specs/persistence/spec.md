## MODIFIED Requirements

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
