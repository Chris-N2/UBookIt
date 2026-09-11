# Delta for persistence

Deliberately an ADDED requirement rather than a wholesale MODIFIED of "Schema shape and
naming", following the retention-index precedent: the new table is a new concern, and nothing
about the existing schema changes.

## ADDED Requirements

### Requirement: Responsibility assignments are stored in one additive table

The schema SHALL gain one table, `uBookItResponsibility`, holding responsibility assignments
for resources and services alike: a subject discriminator and id, and a party discriminator
and key. The four columns together SHALL be the primary key — an assignment either exists or
does not, carries no payload, and writing the same assignment twice SHALL be indistinguishable
from writing it once. The table SHALL carry an index serving lookup by subject, which is the
only query shape the package runs against it.

The table SHALL hold **keys only**: no names, no email addresses, nothing copied from
Umbraco's user store — the parties are resolved from Umbraco at the moment they are needed, so
nothing here can go stale except the reference itself, which resolution and the editors are
specified to handle.

The migration SHALL be additive, applied by the package's own startup pipeline into its
package-private history table, exactly as every migration before it.

#### Scenario: An assignment round-trips
- **WHEN** an assignment of a user to a resource is stored and read back by subject
- **THEN** the same subject and party come back, and nothing else was stored about either

#### Scenario: Writing an assignment twice stores it once
- **WHEN** the same assignment is written twice
- **THEN** the table holds it once and the second write is not an error

#### Scenario: Deleting the owner removes its rows
- **WHEN** a resource or service with assignments is deleted through its store
- **THEN** its assignment rows are removed in the same operation

#### Scenario: A fresh database receives the table
- **WHEN** migrations run against a database created before this change
- **THEN** the table exists afterwards and every pre-existing table is untouched
