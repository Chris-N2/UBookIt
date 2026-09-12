# Delta for persistence

An ADDED requirement (the retention-index and responsibility-table precedent): a new
concern, not a schema-shape replacement.

## ADDED Requirements

### Requirement: One-shot operations are recorded in a package-private flag table

The schema SHALL gain one table, `uBookItFlag`, holding a key and the instant it was
applied — a general marker store for operations the package must perform at most once per
installation, of which the permissions seed is the first. The key SHALL be the primary
key; a flag either exists or does not, and carries nothing else.

The table SHALL hold no personal data of any kind, and the migration SHALL be additive,
applied by the package's own startup pipeline exactly as every migration before it.

A one-shot operation SHALL write its flag only on full success, so an interrupted
operation retries at the next startup; each such operation SHALL therefore be idempotent
over a partial earlier run, and that burden lies with the operation, not the table.

#### Scenario: A flag round-trips

- **WHEN** a flag is written and the table is read back
- **THEN** the key is present with the instant it was applied, and nothing else was stored

#### Scenario: A fresh database receives the table

- **WHEN** migrations run against a database created before this change
- **THEN** the table exists afterwards and every pre-existing table is untouched

#### Scenario: An absent flag means the operation runs

- **WHEN** a one-shot operation starts on an installation whose flag is absent
- **THEN** the operation runs, and the flag exists afterwards only if it fully succeeded
