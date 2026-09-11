## MODIFIED Requirements

### Requirement: Booker contact details SHALL have exactly one durable home

The package SHALL keep a booker's contact details in **one** durable location among the stores it
owns, so that erasing that location erases the data it holds. No feature SHALL introduce a second
durable copy **inside the package** — an audit record, a log entry, a report, a cached projection
or a queued message — without erasing it on the same terms.

**This constraint is stated rather than left as an accident of what has been built.** It holds
today: the booking row is the only durable home the package owns, and the delivery API has no
endpoint that reads a booking back. The point of writing it down is that the roadmap adds features
which would each naturally make a copy — an erasure audit trail, retention reporting — and each
would silently reduce erasure to a partial gesture with nothing in the code that looks like a
removal of this guarantee.

**Where the package hands contact details to something it does not own, it SHALL disclose that
rather than claim to erase it.** A message the package sends is delivered to a mailbox, and a
mail server's rejection may quote the recipient into text the host then logs. Neither is a store
the package can enumerate, reach or erase, and pretending otherwise would be the more dangerous
failure: an operator told erasure is complete stops looking.

So the boundary is drawn by **ownership**, and it is drawn explicitly because the alternative
readings both fail. Read as "no copy anywhere", this requirement forbids sending email at all —
which is not what it was written to prevent, since it names confirmation emails as a *feature to
be reconciled*, not as one to be refused. Read as silence, a feature ships that reduces erasure
without anything looking like a decision.

**What the package SHALL therefore do for any such hand-off is:** send the minimum that serves the
purpose, never route contact details anywhere they are not already required, and state the
boundary in the documentation where an operator performing an erasure will meet it.

**An audit record of an erasure SHALL NOT record the erased contact details.** Recording who
erased what is legitimate; recording the address that was erased re-creates the data the
operation exists to remove.

#### Scenario: The single home is verifiable
- **WHEN** the package's durable stores are enumerated
- **THEN** booker contact details appear in exactly one, and erasing there leaves no other copy

#### Scenario: A feature adding a second copy must erase it too
- **WHEN** a change introduces a durable record carrying a booker's contact details inside the package
- **THEN** it either erases that record with the booking or is rejected

#### Scenario: An erasure record names the booking, not the person
- **WHEN** the package records that an erasure occurred
- **THEN** the record identifies the booking and the actor, and carries no erased name, email, phone or member key

#### Scenario: A hand-off outside the package is disclosed rather than claimed
- **WHEN** a feature hands a booker's contact details to something the package does not own, such as the site's mail infrastructure
- **THEN** the package does not claim to erase it, and the documentation states that erasure does not reach it

#### Scenario: A hand-off carries no more than its purpose requires
- **WHEN** the package composes something that leaves it carrying booker contact details
- **THEN** it addresses only the recipient the purpose requires, and no message to any other recipient carries a booker's name, address or telephone number

### Requirement: What erasure does not reach is documented

The package's documentation SHALL state what erasure does, that it cannot be undone, who may
perform it, **that it may also happen without anybody performing it**, and **the boundaries of
what a single erasure achieves**:

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
- **It does not reach anything already sent, on a site that sends messages.** Where a site has
  configured the package to write to bookers, a message delivered before the erasure is in a
  mailbox the package cannot reach, and a mail server's rejection may have quoted the address
  into the host's error log. The documentation SHALL state this boundary **where an operator
  performing an erasure will meet it**, not only where sending is described — an operator
  honouring a right-to-be-forgotten request is reading about erasure, and being told there that
  erasure is complete when it is not is the failure this requirement exists to prevent.

**The documentation SHALL state that erasure also happens on a timer where a site has configured
one**, so that a booking's details disappearing is not read as a fault or as somebody's action.
An operator who finds a booking erased and no record of anyone erasing it SHALL be able to
discover from the documentation that a configured retention period is the other explanation. The
retention setting itself is documented under `booking-retention`; what belongs here is that
erasure is not exclusively something a person does.

Left unstated, the first arrives as a data-protection failure, the second as a support call about
a booking nobody can ring, the third as an answer given in good faith to a data subject that turns
out to be untrue, and the fourth as a bug report about vanishing data.

#### Scenario: The limits of one erasure are documented
- **WHEN** a reader consults the backoffice documentation
- **THEN** it states that erasure applies to one booking, that a person's other bookings are unaffected by it, and how to find them

#### Scenario: Erasing a future booking is permitted and its cost is documented
- **WHEN** an operator erases a booking whose interval has not yet started
- **THEN** the operation succeeds, and the documentation states that the site will no longer be able to contact that booker

#### Scenario: Erasure without an actor is documented
- **WHEN** a reader consults the backoffice documentation
- **THEN** it states that a booking's personal data may also be erased automatically where the site has configured a retention period, and points to where that is described

#### Scenario: What erasure cannot reach on a sending site is documented where erasure is
- **WHEN** a reader consults the backoffice documentation on a site that has configured the package to send messages
- **THEN** it states that erasure does not reach a message already delivered, nor a booker's address quoted by a mail server into the site's own logs
