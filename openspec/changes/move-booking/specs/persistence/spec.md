<!--
GUARANTEE DIFF for the one wholesale replacement below.

"Store implementations honour Core semantics" — 9 SHALL blocks, 11 scenarios:
  SHALL 1  SQL implementations; claims read by index; UpdateAsync   → CARRIED; the move write is
           writes status, not booker; booker by erasure only          named as a THIRD narrow write
                                                                      over the interval columns
  SHALL 2  the two writes touch disjoint columns, and why           → CARRIED, amended to three
                                                                      writes over three disjoint
                                                                      column sets
  SHALL 3  erasure takes id + instant, not an aggregate             → CARRIED unchanged
  SHALL 4  erasure absorbing at storage; nothing restores a booker  → CARRIED unchanged
  SHALL 5  the check is inside the write, not read-then-write       → CARRIED, and stated to
                                                                      apply to the move write's
                                                                      status condition too
  SHALL 6  a change is observable by re-reading                     → CARRIED unchanged
  SHALL 7  batched claims read is one query                         → CARRIED unchanged
  SHALL 8  type-filtered listing is one query, complete aggregates  → CARRIED unchanged
  SHALL 9  the claims reads and listing need no migration           → CARRIED unchanged
  Scenarios 1-11 ALL CARRIED verbatim. Two ADDED: the three writes touch disjoint columns; the
  move write's status condition is inside the statement.

  DELIBERATE DROPS: none.

"Atomic placement on SQL Server" is NOT modified. A sibling requirement, "Atomic move on SQL
Server", is ADDED for the move write's transaction shape; placement's own is untouched.
-->

## ADDED Requirements

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

## MODIFIED Requirements

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
