## MODIFIED Requirements

### Requirement: A withheld value is absent, not blanked

Where personal data is withheld, the response SHALL **omit** it rather than carry it as an
empty, zeroed or placeholder value, and the omission SHALL be expressed in the shape of the
response so that a client can tell "you were not given this" from "there is nothing here".

Contact details that belong together SHALL be withheld together, as a **single member**,
rather than as several fields that must be null in agreement. Two parallel nullable fields can
be observed half-populated, and a client meeting that state has no correct way to interpret it
— the same reasoning that already governs how a booking's service crosses the boundary.

**Why a caller has no contact details SHALL be stated, not inferred.** There is now more than
one reason a response may carry none — the caller may not see them, or they may have been
erased — and those call for different actions by whoever reads the screen. A response SHALL
therefore carry an explicit indication of **which** applies, and a client SHALL NOT be required
to deduce it from which fields are present.

**A null SHALL be unambiguous wherever it is still used.** It may carry a meaning only where
the underlying value cannot legitimately be absent for any other reason. **A member whose
absence is already meaningful SHALL NOT be overloaded to carry a second meaning as well.** This
is why withholding is no longer expressed by a null booker: erasure makes a missing booker
meaningful, so the null now has two candidate readings and is spent.

**Blanking SHALL NOT be used, even where a field is not nullable.** A default value in a
response is not evidence of the underlying state, and a client cannot distinguish one from the
other. This applies to erasure exactly as it applies to withholding: an erased booking SHALL
NOT be reported by empty or masked contact details.

#### Scenario: Withheld details are absent from the payload
- **WHEN** a response withholds a booker's contact details
- **THEN** no empty string, placeholder or masked form of the name or email appears in the payload

#### Scenario: Withholding is all-or-nothing per booker
- **WHEN** a response withholds a booker's contact details
- **THEN** the name and the email are withheld together, and no state exists in which one is present and the other is not

#### Scenario: A response cannot be misread as "no booker"
- **WHEN** a client receives a booking carrying no contact details
- **THEN** the response states whether they were withheld or erased, and the package's contract states that a booking without a booker cannot exist

#### Scenario: Withheld and erased are not the same answer
- **WHEN** a client receives one booking whose details were withheld from it and one whose details were erased
- **THEN** the two responses differ in a member the client reads directly, rather than being distinguishable only by which fields are absent

#### Scenario: An erased booking is not reported by blanking either
- **WHEN** a response carries a booking whose contact details were erased
- **THEN** no empty string, placeholder or masked form of the name or email appears in the payload
