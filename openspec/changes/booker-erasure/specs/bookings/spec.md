## MODIFIED Requirements

### Requirement: Booker identity
A booking's booker SHALL carry an optional opaque member key (`Guid?`, never an Umbraco type) and contact details: a non-empty name, a well-formed email address, and an optional phone number. Contact details SHALL be required regardless of whether a member key is present.

**BREAKING — published API.** The members carrying those details change shape: `Booker.Name`,
`Booker.Email` and `Booker.Phone`, all public in `0.1.0`, are replaced by a single nullable
`Booker.Contact`. The behaviour this requirement describes is unchanged for every booking a
placement creates; what changes is how a caller reads it. The break is stated here rather than
left to be discovered, because `UBookIt.Core` is published and its surface is a compatibility
promise.

**A booker SHALL be in one of exactly two states: carrying contact details, or erased.** An
erased booker carries no name, no email address, no phone number and no member key, and records
the instant at which the erasure happened. There is no third state, and in particular no state
in which contact details are present but empty.

**Placement SHALL only ever produce the first state.** The validation above is unchanged for
every booking the domain creates: a booking cannot be placed without a non-empty name and a
well-formed email. The erased state is reachable only by erasing an existing booking (see
"Erasing a booker's contact details") and by rehydrating one already erased from storage.

**The two states SHALL be distinguished by construction, not by inspecting values.** A booker
that neither carries contact details nor records an erasure SHALL NOT be constructible, and
reading a name or an email SHALL require having established that details are present.

#### Scenario: External booker without member record
- **WHEN** a booking is placed with no member key but with name and valid email
- **THEN** the booking is accepted

#### Scenario: Missing email is rejected
- **WHEN** a booking is placed with a name but no email address
- **THEN** placement fails with a validation failure identifying the email field

#### Scenario: Placement cannot produce an erased booker
- **WHEN** a booking is placed
- **THEN** its booker carries contact details and is not erased, whatever the request contained

#### Scenario: An erased booker carries nothing of the person
- **WHEN** a booker is erased
- **THEN** it reports its erasure and the instant of it, and carries no name, email, phone or member key

#### Scenario: Neither-state is not constructible
- **WHEN** a caller attempts to construct a booker with neither contact details nor an erasure instant
- **THEN** no such value can be produced

## ADDED Requirements

### Requirement: Erasing a booker's contact details

`UBookIt.Core` SHALL expose an operation on a booking that replaces its booker with an erased
one, recording the instant of erasure, and a service-level verb that performs it against stored
state.

**The operation SHALL change nothing else about the booking.** Its id, reference, interval,
time zone, status, creation time, claims and service attribution SHALL be unaffected — in
particular, erasing a booker SHALL NOT change whether the booking blocks time.

**It SHALL mirror the status machine's shape.** Status is mutated by named transition methods
rather than by a setter; the booker is mutated the same way, by a single named operation on the
aggregate, so that there is one way a booking's booker can change and it is named after what it
does.

**The erasure instant SHALL come from the same clock as placement**, so that a booking's
creation time and its erasure time are comparable and neither is taken from ambient system
time at an arbitrary layer.

**Erasing an already-erased booking SHALL succeed and change nothing**, including the recorded
instant, which remains that of the first erasure. See the `booker-erasure` capability for why
this is deliberately unlike cancellation.

**Rehydration SHALL accept an erased booker** on the same terms it accepts any stored status:
stored state is historical fact, and a booking that was erased must remain readable. Its
structural invariants — at least one claim, no duplicate resource — are unchanged, and it
SHALL NOT become a route to producing an erased booker for a booking that was not erased.

#### Scenario: Erasing replaces the booker and nothing else
- **WHEN** a booking's booker is erased
- **THEN** the booking's id, reference, interval, time zone, status, creation time, claims and service attribution are unchanged, and its booker is erased

#### Scenario: An erased booking still blocks
- **WHEN** a booking with a blocking status has its booker erased
- **THEN** it still reports that it blocks, and still conflicts with an overlapping placement on a claimed resource

#### Scenario: The erasure instant comes from the injected clock
- **WHEN** a booking is erased with the clock set to a known instant
- **THEN** the recorded erasure instant is that instant

#### Scenario: A second erasure keeps the first instant
- **WHEN** an erased booking is erased again at a later instant
- **THEN** the operation succeeds and the recorded instant is still the earlier one

#### Scenario: An erased booking rehydrates
- **WHEN** a booking whose booker was erased is materialized from stored state
- **THEN** it rehydrates successfully, reporting an erased booker and the stored erasure instant

#### Scenario: Erasing a booking that does not exist fails
- **WHEN** the erase verb is called with an id no booking has
- **THEN** it fails with a stable failure code and nothing is changed
