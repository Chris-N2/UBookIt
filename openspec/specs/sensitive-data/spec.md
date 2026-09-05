# sensitive-data

## Purpose

Who may see the personal data uBookIt holds, how the package decides, where the decision is
enforced, what a withheld value looks like to a caller, and the guard that keeps a newly added
field from bypassing the rule.

Stated package-wide rather than per screen. Today one surface carries booker contact details —
the backoffice bookings list — but the rule is a property of the package rather than of that
screen, and the roadmap adds surfaces that must obey it: retention and erasure, then
confirmation emails. A rule written into the screen that happened to need it first would have to
be rediscovered by each of them.

The mechanism is Umbraco's own: membership of the built-in **Sensitive data** user group. The
package reads it and does not reinvent it.

## Requirements
### Requirement: Personal data is shown only to a backoffice user Umbraco permits to see it

The package SHALL show a booker's contact details to a backoffice user only when that user
has **sensitive-data access**, and SHALL determine that by asking Umbraco whether the user
belongs to the built-in **Sensitive data** user group.

**The package SHALL NOT invent a group, a flag or a setting of its own for this.** Umbraco
ships the group, ships the membership test, and applies the same concept to content
properties marked sensitive, so an editor meets one idea rather than two. A parallel
mechanism would be a second answer to the same question, free to disagree with the first.

**The decision SHALL be made server-side, before the response is composed.** A response that
carries contact details to a client which then declines to render them has disclosed them:
the values are in the payload, readable in the browser's network tools by the very user the
site meant to exclude. Withholding SHALL therefore remove the values, not hide them.

**This is a second gate, not a replacement for the first.** Section access decides whether a
user may reach uBookIt's management endpoints at all and SHALL continue to be required;
sensitive-data access decides what those endpoints tell them. A user with neither reaches
nothing; a user with section access alone reaches the bookings list without contact details.

#### Scenario: A permitted user sees contact details
- **WHEN** a backoffice user with sensitive-data access reads bookings whose details have not been erased
- **THEN** each of those bookings' booker name and email are present in the response

#### Scenario: Access does not resurrect erased details
- **WHEN** a backoffice user with sensitive-data access reads a booking whose details were erased
- **THEN** no name or email is present, and the response reports the booking as erased rather than as withheld

#### Scenario: A user without sensitive-data access does not
- **WHEN** a backoffice user with section access but without sensitive-data access reads bookings
- **THEN** no booker name or email appears anywhere in the response, in any field, whether the details exist and are withheld or have been erased

#### Scenario: The package uses Umbraco's own group
- **WHEN** the mechanism deciding sensitive-data access is inspected
- **THEN** it tests membership of Umbraco's built-in Sensitive data group, and the package defines no group, flag or configuration setting of its own for the purpose

#### Scenario: Section access is still required
- **WHEN** a user without access to the package's section calls a management endpoint
- **THEN** the request is refused as it was before, whether or not that user has sensitive-data access

*The first scenario said "each booking's booker name and email are present". Erasure falsified
it as written: a permitted user reading an erased booking is correctly given neither. The
requirement's body — details are shown **only when** the user has sensitive-data access — is
untouched and is what this capability is about; what changed is that having access is no longer
sufficient for a value that no longer exists. Narrowed to the bookings the scenario was always
about, with the erased case stated beside it rather than left to be inferred.*

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

#### Scenario: Null cannot be misread as "no booker"
- **WHEN** a client receives a booking carrying no contact details
- **THEN** the response states whether they were withheld or erased, and the package's contract states that a booking without a booker cannot exist

#### Scenario: Withheld and erased are not the same answer
- **WHEN** a client receives one booking whose details were withheld from it and one whose details were erased
- **THEN** the two responses differ in a member the client reads directly, rather than being distinguishable only by which fields are absent

#### Scenario: An erased booking is not reported by blanking either
- **WHEN** a response carries a booking whose contact details were erased
- **THEN** no empty string, placeholder or masked form of the name or email appears in the payload

### Requirement: The visibility decision cannot be forgotten

Composing a response that would carry personal data SHALL **require** the visibility decision
as an input. It SHALL NOT be possible to produce such a response and then apply withholding
to it as a subsequent step, and there SHALL be no route to one that skips the decision
entirely.

**This is stronger than a rule every caller must remember, and that is the reason for it.** A
withholding step applied after the fact is correct only for as long as every present and
future endpoint remembers to call it; a decision the composition cannot proceed without leaves
nothing to remember. The cost of forgetting here is disclosure of personal data to somebody the
site excluded, which is not a class of mistake worth leaving to discipline.

#### Scenario: There is no unguarded route
- **WHEN** the surface that composes a response carrying booker contact details is inspected
- **THEN** every route to it requires the visibility decision, and none produces a response that must be corrected afterwards

#### Scenario: Omitting the decision is not expressible
- **WHEN** a caller attempts to compose such a response without stating whether contact details may be seen
- **THEN** the code does not compile, rather than defaulting to either answer
### Requirement: A newly added field cannot reach an unauthorized caller undecided

A response model that carries personal data SHALL be **guarded against acquiring a field that
nobody decided about**. Adding a member to such a model SHALL cause an existing check to fail
until an author has stated, explicitly, whether the new member is subject to withholding.

**The guard SHALL enumerate the model rather than a list of known fields.** A check naming the
fields that exist today passes unchanged when a fourth is added, which is the only case it
needed to catch. The check therefore compares the model's members against a recorded set and
fails on any difference.

**What the guard actually detects SHALL be stated rather than overclaimed.** It cannot
recognise personal data; nothing in a property's type or name makes it so. It detects
*change*, and converts a silent addition into a deliberate decision. That is the guarantee on
offer, and a stronger-sounding one would be false.

#### Scenario: An added field fails the guard
- **WHEN** a member is added to a response model that carries booker contact details
- **THEN** a check fails, naming the member, until the recorded set is updated

#### Scenario: The guard does not depend on today's field names
- **WHEN** the guard is inspected
- **THEN** it compares the model's full membership against a recorded set, rather than asserting the presence or treatment of individually named fields
### Requirement: Withheld data SHALL NOT be reachable by asking about it

No endpoint SHALL offer a filter, search, sort or count over booker contact details.

The reason is that such a facility answers questions about values the caller was not given: a
caller who may not read a booker's email but may filter by one can confirm an address by
observing whether a row comes back, and can enumerate a list of candidates the same way.
Withholding a value while answering questions about it is not withholding it.

**This constraint is stated rather than left as an accident of what has been built.** No such
filter exists today; the point of writing it down is that adding one later would silently
undo this capability, and nothing in the code would look like a removal.

#### Scenario: No filter over contact details is offered
- **WHEN** the management endpoints' parameters are inspected
- **THEN** none accepts a booker name or email as a filter, search term or sort key

#### Scenario: A withheld booking still appears in results
- **WHEN** a user without sensitive-data access lists bookings
- **THEN** every booking matching the query is returned with its total unchanged, withheld details being removed from each row rather than the rows being removed
### Requirement: The membership requirement is documented, including who is in the group by default

The package's documentation SHALL state that booker contact details are visible only to
backoffice users in Umbraco's Sensitive data group, and SHALL state how a site grants that.

It SHALL also state that **Umbraco's installer places only the site's original super user in
that group**. A second Administrator, created afterwards, is not in it and will see every
booker's details withheld despite holding the highest role the site offers. Left undocumented,
that arrives as a defect report against uBookIt: the screen looks broken, and nothing on it
explains why.

#### Scenario: A site owner can find out how to grant access
- **WHEN** a reader consults the backoffice documentation
- **THEN** it states that contact details require membership of Umbraco's Sensitive data group, and how to add a user to it

#### Scenario: The administrator surprise is documented
- **WHEN** a reader consults the backoffice documentation
- **THEN** it states that being an administrator does not by itself grant sensitive-data access, and that only the original super user is placed in the group at install
