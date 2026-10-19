## ADDED Requirements

### Requirement: A service's instructions are stored in an additive nullable column

The service table SHALL gain one nullable text column holding the service's instructions for the
booker, sized to the 2000-character limit the `services` capability sets. The migration that adds
it SHALL be additive: it SHALL add the column and nothing else, and it SHALL NOT alter, rebuild or
drop any existing column, index or table.

Absent instructions SHALL be stored as `NULL`, never as an empty string, so storage and the domain
have one representation for "none". A service written before the column existed SHALL read back
with no instructions.

#### Scenario: Instructions round-trip through the store
- **WHEN** a service with instructions over two lines is saved and reloaded through the store in a fresh context
- **THEN** its instructions are equal to what was saved, line break included

#### Scenario: No instructions are stored as NULL
- **WHEN** a service without instructions is saved
- **THEN** its instructions column is `NULL`

#### Scenario: An existing database upgrades without loss
- **WHEN** the migration runs against a database holding services and bookings created before it
- **THEN** every existing row is unchanged, and every existing service reads back with no instructions

#### Scenario: The migration only adds
- **WHEN** the migration's operations are inspected
- **THEN** they consist of adding the one nullable column to the service table and nothing else
