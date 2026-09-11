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

### Requirement: The internal message says when a booking awaits action
The message placement sends to the site's configured recipients SHALL state that the booking
awaits approval when the booking it announces is `Requested`, and SHALL NOT state it when the
booking is `Confirmed`.

**The statement SHALL derive from the booking's status, not from the setting.** The message
describes the booking it announces; deriving it from configuration would let the two drift
the day anything else decides a placement's status.

The message SHALL continue to satisfy everything already required of it: it carries the
reference, the time, what was booked and the backoffice link, and no booker name, address or
telephone number. Awaiting approval is a fact about the booking, not about the person.

**The wording obligations above describe the message the package composes.** Where a site
supplies its own content for an internal message, what it says — including whether it mentions
that the booking awaits approval, and whether it carries the reference, the time, what was
booked or the link — is the site's. The package SHALL NOT claim otherwise.

*This narrowing is the counterpart of the one on the booker's message, and it is stated here
rather than assumed because leaving it out made this capability contradict itself: a site could
supply an `InternalPlaced` view that mentioned no approval and no reference, and two
requirements would then disagree about what that message contains. The package still supplies
everything needed to satisfy them — `AwaitsApproval`, the reference, the interval, what was
booked and the backoffice link are all on the model — so a correct message is what an author
gets by rendering what they were given.*

**What does NOT narrow is the sentence about personal data**, and the distinction is the point
of the whole design. That a message to a configured recipient list carries no booker name,
address or telephone number is not a wording obligation an author could fail to honour: the
model such a view receives has no member for them, so it holds however the content is written.
A narrowing that swept it up with the rest would give away the one guarantee this capability
made structural.

#### Scenario: A requested placement flags the wait
- **WHEN** the internal message is composed for a booking placed as `Requested`
- **THEN** it states that the booking awaits approval, alongside the link to the backoffice screen where it can be acted on

#### Scenario: A confirmed placement does not flag a wait
- **WHEN** the internal message is composed for a booking placed as `Confirmed`
- **THEN** it does not state or imply that any action is awaited

#### Scenario: The flag carries no personal data
- **WHEN** the internal message for a requested placement is composed
- **THEN** it states no booker name, no address and no telephone number, exactly as for any other internal message

#### Scenario: A supplied internal view owns its own wording
- **WHEN** a site supplies content for an internal message and it states nothing about approval
- **THEN** the package does not add a statement of its own, and does not claim the message flags the wait

#### Scenario: A supplied internal view still cannot carry personal data
- **WHEN** a site supplies content for an internal message
- **THEN** that message carries no booker name, no address and no telephone number, whatever the content is written to do

### Requirement: A message to the site's own people carries no personal data

A message sent to a site's configured recipients SHALL identify the booking by its reference, when
it is and what was booked, and SHALL NOT carry the booker's name, address or telephone number.

**Because the package already decides who may see a booker's contact details, and a mailing list
is not that decision.** Backoffice access to a booker's details is governed by membership of the
group the `sensitive-data` capability tests; a list of addresses in configuration is a different
population, gated by who can edit configuration. Putting contact details into that message would
route personal data around a control this package built deliberately.

The message SHALL instead carry **a link to where the booking can be seen in the backoffice**, so
that whoever follows it is subject to that control as it stands. Where no such link can be built,
the message SHALL still be sent without one.

**The two halves of this requirement narrow differently, and that asymmetry is the design rather
than an oversight.**

- **What the message SAYS** — that it identifies the booking by reference, time and what was
  booked, and that it carries the backoffice link — describes the message the package composes.
  Where a site supplies its own content, those are the site's to include or omit, and the package
  SHALL NOT claim they are present.
- **What the message MUST NOT carry** does not narrow at all. The booker's name, address and
  telephone number are absent from a supplied internal message not by an author's restraint but
  because the model such content receives has no member for them. This holds however the content
  is written, and it SHALL remain structural rather than becoming an instruction.

*Put plainly: a site can supply an internal message that forgets to mention the reference. It
cannot supply one that names the booker. The first is a site choosing its own words; the second
would route personal data around a control this package built, which is not a choice the package
offers.*

#### Scenario: The internal message names no booker
- **WHEN** a message is composed for the site's own recipients
- **THEN** it carries the reference, the time and what was booked, and states no name, no address and no telephone number

#### Scenario: The internal message routes to the governed view
- **WHEN** a message is composed for the site's own recipients and the site's address is known
- **THEN** it carries a link to the backoffice screen where bookings are seen

#### Scenario: The site's address is not configured
- **WHEN** the site's own address cannot be established
- **THEN** the message is still sent, carrying the reference, the time and what was booked, and no link

#### Scenario: A supplied internal view chooses what it identifies the booking by
- **WHEN** a site supplies content for an internal message that omits the reference
- **THEN** the message is sent as written, and the package does not claim the reference is present

#### Scenario: No supplied internal view can name the booker
- **WHEN** any content supplied for a message to the site's own recipients is rendered
- **THEN** it carries no booker name, address or telephone number, because what it was given has no member carrying them

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
