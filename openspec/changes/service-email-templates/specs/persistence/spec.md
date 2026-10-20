## ADDED Requirements

### Requirement: A service's email template set is stored in an additive nullable column

The service table SHALL gain one nullable text column holding the service's email template set,
sized to the 64-character limit the `services` capability sets. The migration that adds it SHALL
be additive: it SHALL add the column and nothing else, and it SHALL NOT alter, rebuild or drop any
existing column, index or table.

A service with no set SHALL be stored as `NULL`, never as an empty string, so storage and the
domain have one representation for "none". A service written before the column existed SHALL read
back with no set.

**The normalisation is applied when stored data is read, as well as when it is written.** A value
placed in the column by other means, such as upper case or surrounding whitespace, SHALL read back
normalised. A stored value that cannot be normalised to a valid name SHALL read back as **no set**,
and the read SHALL NOT fail. One bad row must not stop a service loading, or stop the messages for
its bookings being sent.

The column SHALL NOT be indexed or constrained to be unique. Several services MAY share a set.

#### Scenario: A template set round-trips through the store
- **WHEN** a service with template set `remote` is saved and reloaded through the store in a fresh context
- **THEN** its template set is `remote`

#### Scenario: No set is stored as NULL
- **WHEN** a service without a template set is saved
- **THEN** its template set column is `NULL`

#### Scenario: A stored value is normalised on read
- **WHEN** the column holds ` Remote ` for a service written outside the package, and the service is loaded
- **THEN** its template set is `remote`

#### Scenario: An invalid stored value reads as no set
- **WHEN** the column holds `../x` for a service written outside the package, and the service is loaded
- **THEN** the service loads, with no template set

#### Scenario: An existing database upgrades without loss
- **WHEN** the migration runs against a database holding services and bookings created before it
- **THEN** every existing row is unchanged, and every existing service reads back with no template set

#### Scenario: The migration only adds
- **WHEN** the migration's operations are inspected
- **THEN** they consist of adding the one nullable column to the service table and nothing else
