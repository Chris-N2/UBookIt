# booking-emails — delta for email-templates

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

**Everything above describes the messages the package composes.** Where a site supplies its own
content for a message — see the `email-templates` capability — the words are the site's, and so is
their accuracy: the package SHALL NOT claim that supplied content states the booking's state
correctly, presents the reference in the quotable form, or expresses times in any particular zone.

*This is a narrowing, not a lowering, and it is the same reasoning the accessibility narrowing
records: we do not take responsibility for text we did not write. It is honest rather than an
escape hatch because of what the package still supplies — the model a supplied view receives
carries the booking's state, the reference in its quotable form, and instants already converted
to the booking's own zone, so a correct message is what an author gets by rendering what they were
given. The package makes accuracy available; it cannot make it compulsory.*

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

## ADDED Requirements

### Requirement: What a site may replace about a message, and what it may not
A site SHALL be able to replace a message's **body**, and to state its **subject** and whether it
is HTML. A site SHALL NOT be able, through supplied content, to change **whether** a message is
sent, **who** receives it, or **what a message to the site's own recipients may carry**.

**The separation is between wording and policy**, and the guarantees on the policy side are
enumerated here so that the narrowing above cannot be read wider than it is. None of the
following narrows when content is supplied:

- The conjunction that gates sending — the site's configuration **and** the host's ability to
  send — governs a supplied message exactly as it governs the package's own.
- Which audiences each event addresses. Supplying content for a message the package does not send
  SHALL NOT cause it to be sent.
- That an erased booker is never written to. Supplied content SHALL NOT be rendered for, or sent
  to, a booker with no contact details.
- That a message to the site's own recipients carries no booker name, address or telephone
  number. The `email-templates` capability makes this structural — the model such a view receives
  has no member for them — so it holds without depending on what an author writes.

#### Scenario: Supplied content does not bypass the gate
- **WHEN** a site supplies content for a message and has not enabled that direction of sending
- **THEN** nothing is sent

#### Scenario: Supplied content does not create a message
- **WHEN** a site supplies content for a message that this package does not send to that audience
- **THEN** no message is sent to that audience

#### Scenario: Supplied content is not rendered for an erased booker
- **WHEN** a booking whose booker has been erased reaches a message the site has supplied content for
- **THEN** nothing is sent to that booker, on the same terms as for the package's own content

#### Scenario: The internal guarantee survives supplied content
- **WHEN** a site supplies content for a message to its own recipients
- **THEN** that message carries no booker name, no address and no telephone number, because the content had no access to them
