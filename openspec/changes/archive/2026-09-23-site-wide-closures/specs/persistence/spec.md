## ADDED Requirements

### Requirement: Site closures and their opt-outs are held in their own additive tables

The package SHALL store site closures and per-resource opt-outs in two tables of its own, created
by an additive migration that alters and drops nothing. Both SHALL carry the `uBookIt` prefix.

A closure row SHALL hold its id, its date, and its label. **The date SHALL be uniquely indexed**, so
that two closures on one date are impossible in storage and not only in the domain — the check
performed before writing is a race, and the rule exists precisely because a second closure on a
date can only repeat or contradict the first.

An opt-out row SHALL hold a resource id and a closure id and SHALL be unique per pair. **Deleting a
closure SHALL remove its opt-out rows**, and deleting a resource SHALL remove its own, so that no
opt-out can outlive the thing it exempts or the thing it exempts it for. The cascade SHALL reach
only rows these two tables own.

**No booker's personal data SHALL reach either table, and nothing in them SHALL be subject to
erasure.** A closure is a date and the site's own name for it; an opt-out is two identifiers.

Removing every row from both tables SHALL restore the package's behaviour from before they existed,
without a schema change.

#### Scenario: The migration adds and does not alter
- **WHEN** the migration runs against a database from the previous version
- **THEN** both tables exist and no existing table, column or index has been altered or dropped

#### Scenario: A site with no closures behaves as before
- **WHEN** the package runs with both tables empty
- **THEN** every availability projection and every placement resolves exactly as it did before this version

#### Scenario: A duplicate date is impossible in storage
- **WHEN** two closures for the same date are written concurrently
- **THEN** exactly one succeeds, and the other is reported as a duplicate rather than stored

#### Scenario: Deleting a closure removes its opt-outs
- **WHEN** a closure that several resources have opted out of is deleted
- **THEN** its opt-out rows are gone, and no other table is affected

#### Scenario: Deleting a resource removes its opt-outs
- **WHEN** a resource holding opt-outs is deleted
- **THEN** its opt-out rows are gone, and the closures themselves are unchanged

### Requirement: Closure hydration on the read path

Resource reads SHALL hydrate the site closures applicable to each resource — every closure the
resource has not opted out of — so that availability can be computed in `UBookIt.Core` from the
resource alone, without a further load and without the caller knowing closures exist.

**Closures SHALL be read once per store call, not once per resource.** A listing that serves a
service's candidate pool would otherwise issue one closure query per candidate, turning a single
availability question into an N+1.

The storage layer SHALL NOT implement closure precedence as a query predicate. Precedence between a
closure, a resource's own exception and the weekly pattern SHALL have exactly one implementation,
in the domain: a second one in SQL is free to disagree with it, and an availability answer that
disagrees with the booking path's is worse than no answer.

#### Scenario: A resource read carries its applicable closures
- **WHEN** a resource is read through the read port on a site holding closures
- **THEN** it carries the closures it has not opted out of, sufficient to compute its availability without a further load

#### Scenario: An opted-out closure is absent from the hydrated resource
- **WHEN** a resource that has opted out of a closure is read
- **THEN** that closure is absent from what the resource carries, and the date resolves from the resource's own rules

#### Scenario: Listing by type is not N+1
- **WHEN** resources of a given type are listed through the read port
- **THEN** closures are read by a single query for the whole listing, not once per resource

#### Scenario: Precedence is not evaluated in SQL
- **WHEN** the persistence layer is inspected
- **THEN** no query decides between a closure, an exception and the weekly pattern as a substitute for the domain's rule

### Requirement: Write-path opt-out uniqueness

A resource's opt-out rows SHALL be replaced wholesale, within the same transaction as its
open-hours and exception rows, so that a duplicate opt-out for a resource cannot exist after any
write, including under concurrent updates. Concurrent full updates are last-writer-wins, each
writing a complete, internally consistent set.

#### Scenario: Racing updates never produce duplicate opt-outs
- **WHEN** two updates with different opt-out sets for the same resource run concurrently
- **THEN** the final state equals exactly one writer's complete set and contains at most one row per closure

#### Scenario: Update replaces, never merges
- **WHEN** a resource holding two opt-outs is updated to hold one
- **THEN** reloading shows exactly that one

#### Scenario: Opt-outs are written in the resource's own transaction
- **WHEN** a resource update fails after its opening hours are written
- **THEN** neither the opening hours nor the opt-outs are left changed
