## MODIFIED Requirements

### Requirement: What a message tells the booker

A message to a booker SHALL carry the booking's reference, when the booking is, and — where it can
be established — what was booked.

**The reference SHALL be presented in the form a person is expected to quote**, which is the form
the confirmation screen shows, so that the message and the screen cannot disagree about what the
customer is holding.

**Times SHALL be expressed in the time zone the booking was placed against**, which the booking
carries, and not in whatever the site is configured with when the message is composed. A site that
changes its time zone SHALL NOT thereby restate when existing bookings are.

**What the message says about the booking's state SHALL be derived from that state**, and SHALL
NOT be written on the assumption that a placed booking is in any particular one. Placement
produces a confirmed booking under auto-confirm and a requested one where a site requires
approval; a message whose wording assumed either would become false, silently, in a message
already sent to a customer.

**A message SHALL be sent even where what was booked cannot be established**, carrying the
reference and the time without it. The reference and the time are the parts a person cannot
reconstruct for themselves; withholding them because a name could not be resolved would be the
wrong trade.

**Where a cancellation link has been issued for the booking, the message SHALL carry it, and SHALL
say that it is the means of cancelling.** A link a reader discards because nothing told them what it
was for is a link they do not have when they need it, and the `self-service-cancellation` capability
issues each one exactly once, through this message and no other route.

**A message SHALL be sent whether or not a link exists**, and its absence SHALL mean exactly that —
no self-service cancellation is available for this booking — rather than that one was expected and
could not be built. A link is absent where the feature is off, and for a message about a booking
that can no longer be cancelled this way.

**Times, the reference and the link SHALL NOT be reconstructed by any other message.** Where a
booking produces more than one message to its booker, only the message that carries a newly issued
link states one; a later message SHALL NOT restate a link, because restating it would put the same
single-use credential into a second mailbox copy without the reader being able to tell which is
live.

**Everything above describes the messages the package composes.** Where a site supplies its own
content for a message — see the `email-templates` capability — the words are the site's, and so is
their accuracy: the package SHALL NOT claim that supplied content states the booking's state
correctly, presents the reference in the quotable form, expresses times in any particular zone, or
carries the cancellation link.

*This is a narrowing, not a lowering, and it is the same reasoning the accessibility narrowing
records: we do not take responsibility for text we did not write. It is honest rather than an
escape hatch because of what the package still supplies — the model a supplied view receives
carries the booking's state, the reference in its quotable form, instants already converted
to the booking's own zone, and the cancellation link where one exists, so a correct message is what
an author gets by rendering what they were given. The package makes accuracy available; it cannot
make it compulsory.*

*A site whose own content omits the link leaves its bookers without the self-service route, and that
is the site's decision to make. The package SHALL NOT compensate by sending a second message, which
would be the package overriding a choice a site made deliberately.*

**The narrowing reaches supplied content and nothing else.** For every message a site has not
supplied content for — which is all of them until it does — this requirement holds exactly as
written above.

#### Scenario: A booking placed for a service
- **WHEN** a message is composed for a booking placed for a service
- **THEN** it carries the reference as displayed, the booking's start in the booking's own time zone, and the service name recorded on the booking

#### Scenario: A booking placed directly against a resource
- **WHEN** a message is composed for a booking placed directly against a resource
- **THEN** it carries the reference as displayed, the booking's start in the booking's own time zone, and that resource's name

#### Scenario: What was booked cannot be established
- **WHEN** what was booked cannot be established for a message
- **THEN** the message is still sent, carrying the reference and the time, and states nothing about what was booked

#### Scenario: The site's time zone changed after the booking
- **WHEN** a message is composed for a booking placed against one time zone while the site is configured with another
- **THEN** the time is expressed in the zone the booking was placed against

#### Scenario: The wording follows the booking's state
- **WHEN** a message is composed for a placed booking
- **THEN** what it says about the booking's state is determined by that state rather than fixed in the message

#### Scenario: An unsupplied message is composed exactly as before
- **WHEN** a site supplies content for one message and a different message is composed
- **THEN** that message carries the reference, the time and what was booked exactly as it did before any content was supplied

#### Scenario: What the package still supplies to a supplied view
- **WHEN** a site supplies content for a message
- **THEN** what it is given includes the booking's state, the reference in its quotable form, and the interval already expressed in the booking's own time zone

#### Scenario: The link is carried and explained
- **WHEN** a message is composed for a booking that has been issued a cancellation link
- **THEN** the message carries the link and states that it is the means of cancelling the booking

#### Scenario: A message without a link is still sent
- **WHEN** a message is composed for a booking that has been issued no cancellation link
- **THEN** the message is sent, carrying everything else it is due, and states nothing about cancelling this way

#### Scenario: A later message does not restate the link
- **WHEN** a booking that was issued a cancellation link produces a further message to its booker
- **THEN** that message does not carry the link
