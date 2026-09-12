# Delta for sensitive-data

Guarantee-diff note: every SHALL and every scenario is carried verbatim except two
sentences in the second-gate paragraph — "a user with section access alone reaches the
bookings list without contact details" gains the read verb the `permissions` capability
now requires, and the italic postscript is carried untouched. The withholding guarantees
do not move.

## MODIFIED Requirements

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

**This is a second gate, not a replacement for the first.** Section access — and, since the
`permissions` capability, the booking read verb it requires — decides whether a user may
reach uBookIt's booking endpoints at all and SHALL continue to be required; sensitive-data
access decides what those endpoints tell them. A user with neither reaches nothing; a user
who may reach the bookings list without sensitive-data access reaches it without contact
details.

#### Scenario: A permitted user sees contact details
- **WHEN** a backoffice user with sensitive-data access reads bookings whose details have not been erased
- **THEN** each of those bookings' booker name and email are present in the response

#### Scenario: Access does not resurrect erased details
- **WHEN** a backoffice user with sensitive-data access reads a booking whose details were erased
- **THEN** no name or email is present, and the response reports the booking as erased rather than as withheld

#### Scenario: A user without sensitive-data access does not
- **WHEN** a backoffice user permitted to read bookings but without sensitive-data access reads them
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
