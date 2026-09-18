## ADDED Requirements

### Requirement: Cancellation secrets are held in their own additive table

The package SHALL store outstanding cancellation secrets in a table of its own, created by an
additive migration that alters and drops nothing.

Each row SHALL hold the booking it belongs to, **a one-way hash of the secret rather than the
secret**, the instant the secret stops being usable, and whether it has been redeemed. **The secret
itself SHALL NOT be written to this table, to any log, or to any other store.**

**A row SHALL NOT be a credential.** Everything the table holds SHALL be insufficient to cancel a
booking, so that a database copy, a backup, or a person with read access to it cannot act as a
booker. This is the same reasoning that keeps booker contact details out of messages to a site's own
recipients: a control the package built deliberately must not be reachable by a route around it.

**No booker's personal data SHALL reach this table.** A booking's identifier, a hash, an instant and
a flag name nobody. The table is therefore outside erasure's reach, and `booker-erasure` records
that consequence where an operator will meet it.

**Redemption SHALL be recorded durably before the cancellation it authorises is reported as done**,
so that a secret cannot be redeemed twice by presenting it twice in quick succession.

Removing every row SHALL leave every booking exactly as it was, and SHALL cost only that outstanding
links stop working — no booking state depends on this table.

#### Scenario: The migration adds and does not alter
- **WHEN** the migration runs against a database from the previous version
- **THEN** the cancellation-secret table exists and no existing table, column or index has been altered or dropped

#### Scenario: The stored form cannot cancel a booking
- **WHEN** everything held in a row is presented to the cancellation flow
- **THEN** the booking is not cancelled

#### Scenario: A fresh install behaves as before
- **WHEN** the package starts with an empty cancellation-secret table and the feature off
- **THEN** no row is written, and every booking behaves exactly as it did before this version

#### Scenario: Losing the table costs links and nothing else
- **WHEN** every row is removed
- **THEN** no booking's state, interval, booker or reference has changed
