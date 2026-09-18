## MODIFIED Requirements

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
- **It does not withdraw a cancellation link already issued**, on a site running self-service
  cancellation. The link is a credential held in the booker's mailbox; erasure removes the person's
  details from the booking, and the booking itself survives erasure by design, so a link issued
  before the erasure continues to do the one thing it does — cancel that booking — until it expires
  at the booking's start. **The documentation SHALL state this where an operator performing an
  erasure will meet it.**

  *This is a deliberate decision rather than an omission, and the alternative was considered:
  revoking outstanding links on erasure. It was rejected because the link carries no contact
  detail, discloses none when followed, and cancelling is an action the booker was told they could
  take — withdrawing it would take away an ability the package promised while protecting nothing.
  The rule that erasure keeps the booking is what makes this coherent: a booking that still exists
  is a booking that can still be cancelled.*

**The documentation SHALL state that erasure also happens on a timer where a site has configured
one**, so that a booking's details disappearing is not read as a fault or as somebody's action.
An operator who finds a booking erased and no record of anyone erasing it SHALL be able to
discover from the documentation that a configured retention period is the other explanation. The
retention setting itself is documented under `booking-retention`; what belongs here is that
erasure is not exclusively something a person does.

Left unstated, the first arrives as a data-protection failure, the second as a support call about
a booking nobody can ring, the third as an answer given in good faith to a data subject that turns
out to be untrue, the fourth as a bug report about vanishing data, and the fifth as a cancellation
nobody can account for.

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

#### Scenario: An outstanding cancellation link survives erasure, and is documented
- **WHEN** a booking whose booker has been erased is cancelled by a cancellation link issued before the erasure
- **THEN** the cancellation succeeds, and the documentation states that erasure does not withdraw a link already issued
