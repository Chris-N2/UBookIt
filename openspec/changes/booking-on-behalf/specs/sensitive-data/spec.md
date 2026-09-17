## MODIFIED Requirements

### Requirement: Withheld data SHALL NOT be reachable by asking about it

**No endpoint SHALL answer a question about booker contact details to a caller who may not read
them.**

The reason is unchanged and is what this requirement has always been about: such a facility
answers questions about values the caller was not given. A caller who may not read a booker's
email but may filter by one can confirm an address by observing whether a row comes back, and can
enumerate a list of candidates the same way. Withholding a value while answering questions about
it is not withholding it.

**An endpoint that accepts contact details as input SHALL require sensitive-data access as its
own authorization**, per the first requirement of this capability, and SHALL NOT obtain that
access by a condition evaluated inside a handler that would otherwise proceed. A filter added as
a parameter to an endpoint gated only on section access violates this, whatever check its handler
performs.

**An endpoint that MATCHES ON a contact detail SHALL match exactly.** It SHALL offer no partial,
prefix, substring, fuzzy or
wildcard form, no ordering by a contact detail, and no count-only or existence-only response. An
exact match answers whether a given person is in the records; a partial match answers *which
people match a fragment*, which is an enumeration facility rather than a lookup, and no request
this package serves needs one.

**An endpoint that accepts a contact detail in order to STORE it has nothing to match, and the
exactness obligation binds it vacuously rather than not at all.** The two are distinguished here
because a placement taking a booker's name and address would otherwise have to claim an exact
match over a value it never compares, and a requirement satisfied by nothing is a requirement a
later reader will discard. **What binds such an endpoint instead:**

- it SHALL carry the sensitive-data gate exactly as a matching endpoint does — the sentence above
  is unchanged and covers both, and the reason it is not relaxed for a write is recorded below;
- it SHALL NOT report the detail back, in its response or in an error; and
- **its outcome SHALL NOT depend on whether any existing record holds the value it was given.**
  An endpoint that refused a booking because that address already had one, or that differed in
  timing or wording according to whether it did, would be an oracle over exactly the values this
  capability protects, wearing a write's clothes.

**A response SHALL NOT reveal more than the caller could already read.** A caller permitted to
see contact details may be told which bookings carry a given one, because they could reach the
same conclusion by reading the rows; a caller who may not see them SHALL learn nothing from
asking, including from a count, a total, an error or a difference in timing between a match and a
miss.

**This constraint is stated rather than left as an accident of what has been built.** The point
of writing it down is that adding an ungated filter later would silently undo this capability,
and nothing in the code would look like a removal.

*The distinction between matching on a detail and storing one is drawn here rather than left
implicit, and the gate is deliberately NOT relaxed for the storing case even though such an
endpoint discloses nothing. Relaxing it is arguable — the stated reason for this requirement is
entirely about a caller learning a value they were not given — but it would let a group trusted
only to take bookings be assembled without the site deciding anything about personal data, and
that is a judgement for a change that examines it rather than a side effect of one that needed a
placement endpoint. Recorded so it is revisited rather than rediscovered.*

*Previously this forbade any filter, search, sort or count over contact details, at any endpoint.
That was broader than its own stated reason, which is entirely about a caller who may not read the
values — and it forbade a mechanism where the guarantee is about a disclosure. The guarantee is
unchanged and is now the operative sentence; what is permitted is a lookup by a caller who could
already read every value it matches on. Reopened deliberately, with sign-off, because a data
subject's erasure request arrives as an email address and the package otherwise cannot honour it.*

#### Scenario: No filter over contact details is offered
- **WHEN** the management endpoints' parameters are inspected
- **THEN** any endpoint accepting a booker contact detail as input requires sensitive-data access as its own authorization, and no endpoint gated on section access alone accepts one

#### Scenario: A lookup matches exactly and offers no partial form
- **WHEN** an endpoint that accepts a booker contact detail is inspected
- **THEN** it matches the value exactly, and offers no prefix, substring, wildcard or fuzzy form, no ordering by a contact detail, and no count-only or existence-only response

#### Scenario: A caller who may not read the values learns nothing by asking
- **WHEN** a caller without sensitive-data access attempts to reach an endpoint that answers about contact details
- **THEN** the request is refused, and the refusal is identical whether or not any booking holds the value asked about

#### Scenario: A withheld booking still appears in results
- **WHEN** a user without sensitive-data access lists bookings
- **THEN** every booking matching the query is returned with its total unchanged, withheld details being removed from each row rather than the rows being removed

#### Scenario: An endpoint that stores a contact detail is gated like one that matches
- **WHEN** a caller without sensitive-data access reaches an endpoint that accepts a booker's name and email address in order to store them
- **THEN** the request is refused by the endpoint's own authorization, not by a check inside a handler that would otherwise proceed

#### Scenario: A storing endpoint reports no contact detail back
- **WHEN** an endpoint that accepts a booker's contact details succeeds, and when it fails
- **THEN** neither its response nor its error carries the name, email address or telephone number it was given

#### Scenario: A storing endpoint is not an oracle over existing records
- **WHEN** the same request is made twice, once with an email address no booking holds and once with one that several bookings hold
- **THEN** the outcome is decided by the booking's own rules alone, and nothing in the response distinguishes the two cases
