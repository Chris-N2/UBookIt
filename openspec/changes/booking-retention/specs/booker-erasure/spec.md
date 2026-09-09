## RENAMED Requirements

- FROM: `### Requirement: Only a caller permitted to read contact details may erase them`
- TO: `### Requirement: Erasure is gated on the access to read what it destroys`

The old name says *caller*, and retention erases with no caller at all. Left as it was, the
requirement's name would assert something the package no longer does, and the first reader
looking for the rule that governs the timer would conclude there wasn't one. The guarantee is
unchanged and is stated below; only its name has stopped excluding the case it has to cover.

## MODIFIED Requirements

### Requirement: Erasure is gated on the access to read what it destroys

Erasure requested by a backoffice user SHALL require the same **sensitive-data access** that
reading a booker's contact details requires, per the `sensitive-data` capability, **in addition
to** the section access every management endpoint requires.

**The gate SHALL be a property of the operation, not a condition inside it.** A caller without
sensitive-data access SHALL be refused before any erasure occurs, rather than reaching a handler
that decides.

The reasoning is that erasure is the most consequential thing the package will do to a booking
and it cannot be undone. A user the site has decided may not so much as read a booker's name
should not be able to destroy it.

**A site's own configured retention policy erases with no user, and is permitted to.** The gate
above governs a person asking the package to erase somebody's data. It cannot govern unattended
work, because there is nobody to test — and it must not be read as forbidding it, or a site could
never operate a retention policy at all. What replaces the gate is not trust: an unattended
erasure path SHALL meet **all** of the following, and the guarantee the gate exists to deliver is
that **nothing reaches or destroys a booker's contact details without the access to read them**,
which the obligations deliver by leaving nothing to read.

- It SHALL select bookings by **time and erasure state alone**, and SHALL accept no contact
  detail as input.
- It SHALL receive **booking identifiers only** from storage — no name, email address, phone
  number or member key.
- It SHALL NOT read, log, report or otherwise handle a contact detail at any point.
- It SHALL NOT be reachable from any HTTP request, and SHALL be configured only where a site's
  configuration is — which is to say by somebody with more access than any backoffice group
  confers, not less.

**Every other erasure path SHALL carry the caller gate.** A new way to erase SHALL either require
sensitive-data access as its own authorization, or satisfy every obligation above. There is no
third option, and in particular an unattended path that handles contact details for any reason is
not one. This clause is the tripwire: without it, "the timer is exempt" becomes a precedent that
any caller-less code can claim.

#### Scenario: A permitted user can erase
- **WHEN** a backoffice user with section access and sensitive-data access erases a booking's booker
- **THEN** the operation succeeds

#### Scenario: Section access alone is not enough
- **WHEN** a backoffice user with section access but without sensitive-data access attempts an erasure
- **THEN** the request is refused and no booking is changed

#### Scenario: The gate belongs to the operation
- **WHEN** the erase operation's authorization is inspected
- **THEN** sensitive-data access is required to reach it at all, rather than tested within a handler that would otherwise proceed

#### Scenario: The unattended path handles no contact detail
- **WHEN** the package's unattended erasure path is inspected
- **THEN** it selects bookings by time and erasure state, receives only booking identifiers, and handles no name, email address, phone number or member key

#### Scenario: The unattended path is not reachable from a request
- **WHEN** the package's endpoints are enumerated
- **THEN** none of them performs an unattended erasure sweep or erases more than one booking

#### Scenario: Every erasure path is accounted for
- **WHEN** the package's erasure paths are enumerated
- **THEN** each one either requires sensitive-data access as its own authorization or satisfies every obligation on an unattended path, and none is exempt on any other ground

### Requirement: What erasure does not reach is documented

The package's documentation SHALL state what erasure does, that it cannot be undone, who may
perform it, **that it may also happen without anybody performing it**, and **the two boundaries
of what a single erasure achieves**:

- **It erases one booking.** A person who booked more than once has one booking erased per
  operation; erasing everything they booked means erasing each. The package **does** provide a
  way to find them — a search by the booker's email address, per `booking-management` — so the
  documentation SHALL direct an operator to search first and erase each result, and SHALL state
  that the search finds bookings made with **that** address: somebody who booked under two
  addresses has one set found.
- **It can be performed on a booking that has not yet happened**, and doing so leaves the site
  unable to contact somebody who is going to arrive. The package SHALL NOT refuse this — whether
  the basis for holding the details still applies is the site's judgement and not the package's
  — but the documentation SHALL state the consequence plainly.

**The documentation SHALL state that erasure also happens on a timer where a site has configured
one**, so that a booking's details disappearing is not read as a fault or as somebody's action.
An operator who finds a booking erased and no record of anyone erasing it SHALL be able to
discover from the documentation that a configured retention period is the other explanation. The
retention setting itself is documented under `booking-retention`; what belongs here is that
erasure is not exclusively something a person does.

Left unstated, the first arrives as a data-protection failure, the second as a support call about
a booking nobody can ring, and the third as a bug report about vanishing data.

#### Scenario: The limits of one erasure are documented
- **WHEN** a reader consults the backoffice documentation
- **THEN** it states that erasure applies to one booking, that a person's other bookings are unaffected by it, and how to find them

#### Scenario: Erasing a future booking is permitted and its cost is documented
- **WHEN** an operator erases a booking whose interval has not yet started
- **THEN** the operation succeeds, and the documentation states that the site will no longer be able to contact that booker

#### Scenario: Erasure without an actor is documented
- **WHEN** a reader consults the backoffice documentation
- **THEN** it states that a booking's personal data may also be erased automatically where the site has configured a retention period, and points to where that is described
