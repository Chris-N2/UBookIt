# bookings

## ADDED Requirements

### Requirement: Booking rehydration
`UBookIt.Core` SHALL expose a public, additive rehydration factory (`Booking.Rehydrate`) that materializes a `Booking` from stored state: id, interval, booker, claims, status, and created timestamp. Rehydration SHALL enforce structural invariants (at least one claim; no duplicate resource per booking) and SHALL accept any `BookingStatus` without applying transition rules — the stored status is historical fact, not a transition. Rehydration SHALL NOT be usable to bypass placement validation: it is documented as a persistence-boundary API, and placement remains the only pathway that creates new bookings. (Discharges the deferred obligation recorded at core-domain archive, per design decision D9: downstream changes add Core surface via their own specs.)

#### Scenario: Rehydrated booking is faithful
- **WHEN** a booking is rehydrated with a `Declined` status and two claims on distinct resources
- **THEN** the resulting `Booking` reports exactly that status and those claims, and its transition methods still enforce the status machine from the current state

#### Scenario: Structural invariants still hold
- **WHEN** rehydration is attempted with zero claims or with two claims on the same resource
- **THEN** rehydration fails; no `Booking` is produced
