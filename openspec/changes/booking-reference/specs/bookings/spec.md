## ADDED Requirements

### Requirement: A booking carries an identifier a person can use
Every booking SHALL carry, in addition to its identifier, a **reference**: a short string that
a person can read aloud, write down, type back, and quote to somebody else.

The two identifiers are deliberate and SHALL NOT be collapsed. The existing identifier remains
what machines use — the primary key, and the value carried in routes and payloads — and SHALL
remain opaque. The reference exists for the reader who is holding a telephone.

**The reference SHALL be unambiguous when transcribed.** It SHALL NOT contain characters that
are confused with one another when spoken, handwritten or typed, and SHALL NOT be
case-sensitive: a reference read back in lower case is the same reference. A reference that has
to be spelled out twice has failed at the one job it has.

**The reference SHALL NOT be capable of spelling a word.** A booking system that emails a
customer a reference which happens to read as an obscenity has a problem it cannot apologise
its way out of, and the class is removed by construction rather than by a filter list.

**The reference SHALL be unique within a site**, and that uniqueness SHALL be enforced by the
store rather than by checking before writing — a check followed by a write is a race, and two
bookings sharing a reference makes both unquotable.

**The reference SHALL be assigned when the booking is placed and SHALL never change
thereafter** — not when a booking is confirmed, declined or cancelled, and not if the booking's
time is later amended. The customer is holding the reference they were given; a system that
changes it denies all knowledge of the booking the person is asking about.

**A reference SHALL remain meaningful when the booker's details do not.** A booking whose
personal data has been removed still occupies its interval and still has to be discussable, so
the reference SHALL NOT be derived from, or depend on, anything about the person.

#### Scenario: A reference can be read out
- **WHEN** a booking is placed
- **THEN** it carries a reference composed only of characters that are unambiguous when spoken or typed, which a person could dictate over a telephone without spelling anything out

#### Scenario: Case does not change which booking is meant
- **WHEN** a reference is compared with one written in a different case
- **THEN** they identify the same booking

#### Scenario: A reference cannot spell a word
- **WHEN** references are generated
- **THEN** the alphabet they are drawn from cannot produce a word, so no reference can be an accidental obscenity

#### Scenario: Two bookings cannot share a reference
- **WHEN** a reference that already exists would be assigned to a new booking
- **THEN** the store refuses it, and the placement either receives a different reference or fails — it never succeeds with a duplicate

#### Scenario: The reference outlives every change to the booking
- **WHEN** a booking is confirmed, declined or cancelled
- **THEN** its reference is the one it was given when it was placed

#### Scenario: The machine identifier is unaffected
- **WHEN** a booking gains a reference
- **THEN** its existing identifier is unchanged, and remains what routes and payloads carry
