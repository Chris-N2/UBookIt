## MODIFIED Requirements

### Requirement: Site closures and their opt-outs are held in their own additive tables

The package SHALL store site closures and per-resource opt-outs in two tables of its own, created
by an additive migration that alters and drops nothing. Both SHALL carry the `uBookIt` prefix.

A closure row SHALL hold its id, its start date, its end date, and its label. **The end date SHALL
be added by a later additive migration** that adds one column, fills it from each existing row's
date so that every existing closure keeps covering exactly the one date it covered, and alters or
drops no existing column or index.

**The start date SHALL be uniquely indexed**, so that two closures starting on one date are
impossible in storage and not only in the domain. Because no date may be covered by two closures,
two closures can never share a start, so this index remains a true backstop. **Overlap across a
range SHALL be checked within the writing transaction, serialised against every other closure write
by an application lock**, because a check performed before writing is otherwise a race, and the
rule exists precisely because a second closure on a date can only repeat or contradict the first.

An opt-out row SHALL hold a resource id and a closure id and SHALL be unique per pair. **Deleting a
closure SHALL remove its opt-out rows**, and deleting a resource SHALL remove its own, so that no
opt-out can outlive the thing it exempts or the thing it exempts it for. The cascade SHALL reach
only rows these two tables own.

**No booker's personal data SHALL reach either table, and nothing in them SHALL be subject to
erasure.** A closure is a range and the site's own name for it; an opt-out is two identifiers.

Removing every row from both tables SHALL restore the package's behaviour from before they existed,
without a schema change.

#### Scenario: The migration adds and does not alter
- **WHEN** the migration that created the closure tables runs against a database from the version before it
- **THEN** both tables exist and no existing table, column or index has been altered or dropped

#### Scenario: The end-date migration preserves every existing closure
- **WHEN** the end-date migration runs against a database holding single-date closures and opt-outs
- **THEN** each closure's end date equals its date, every opt-out is unchanged, and availability resolves exactly as before the migration

#### Scenario: A site with no closures behaves as before
- **WHEN** the package runs with both tables empty
- **THEN** every availability projection and every placement resolves exactly as it did before this version

#### Scenario: A duplicate date is impossible in storage
- **WHEN** two closures for the same date are written concurrently
- **THEN** exactly one succeeds, and the other is reported as a duplicate rather than stored

#### Scenario: Concurrent overlapping ranges cannot both be stored
- **WHEN** two closures with different start dates but overlapping ranges are written concurrently
- **THEN** exactly one succeeds, and the other is reported with the code `duplicate-closure-date` rather than stored

#### Scenario: Deleting a closure removes its opt-outs
- **WHEN** a closure that several resources have opted out of is deleted
- **THEN** its opt-out rows are gone, and no other table is affected

#### Scenario: Deleting a resource removes its opt-outs
- **WHEN** a resource holding opt-outs is deleted
- **THEN** its opt-out rows are gone, and the closures themselves are unchanged

## ADDED Requirements

### Requirement: Resource closed periods are held in their own additive table

The package SHALL store each resource's closed periods in a table of its own, carrying the `uBookIt`
prefix and created by an additive migration. A row SHALL hold the resource id, the start date, the
end date and the optional note.

**A resource's closed periods SHALL be replaced wholesale within the same transaction as its
open-hours, exception and opt-out rows**, so that a write leaves a complete, internally consistent
set and the overlap and exception-conflict rules, judged over the complete submitted set, hold in
storage. Concurrent full updates are last-writer-wins, as for the rest of the resource. **An update
that omits the collection SHALL leave the rows untouched.**

Deleting a resource SHALL remove its closed-period rows. **Resource reads SHALL hydrate closed
periods** alongside exceptions, by a query per store call rather than per resource, so that
availability can be computed in `UBookIt.Core` from the resource alone. The storage layer SHALL NOT
evaluate precedence between closed periods, exceptions, closures and the weekly pattern.

**No booker's personal data SHALL reach the table.** The note is the site's own text about its
resource.

#### Scenario: Closed periods round-trip
- **WHEN** a resource is saved with two closed periods and reloaded through the read port
- **THEN** it carries both, with their dates and notes

#### Scenario: A failed update leaves closed periods unchanged
- **WHEN** a resource update fails after its opening hours are written
- **THEN** neither the opening hours nor the closed periods are left changed

#### Scenario: Deleting a resource removes its closed periods
- **WHEN** an unclaimed resource holding closed periods is deleted
- **THEN** no closed-period row for it remains

#### Scenario: Listing hydrates closed periods without N+1
- **WHEN** resources of a given type are listed through the read port
- **THEN** closed periods are read by a single query for the whole listing, not once per resource

#### Scenario: The migration is additive
- **WHEN** the migration runs against a database from the previous version
- **THEN** the closed-period table exists and no existing table, column or index has been dropped, and no existing column has been altered
