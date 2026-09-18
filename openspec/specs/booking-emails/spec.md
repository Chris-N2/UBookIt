# Booking Emails Specification

## Purpose

When the package sends a message about a booking, to whom, what that message
carries, and — more of the point — what it must never carry or promise. Sending is
off until a site asks for it — in configuration, or by assigning responsibility for a
resource or service — and configuring the host's mail server is not that asking: an
Umbraco site has SMTP long before it has any opinion about booking confirmations, so
upgrading the package must never begin writing to a site's customers.

Stated as its own capability because it is the package's first outbound path for a
booker's personal data, and it answers to `booker-erasure` and `sensitive-data` as
much as to the booking flow.

**What a message *carries* describes the messages the package composes.** A site can
supply its own content for any of them — see the `email-templates` capability — and
where it does, the words are the site's, accuracy included. What does not narrow is the
other half of the sentence above: a message to the site's own recipients carries no
booker name, address or telephone number however that content is written, because the
model such content receives has no member for them. Nor does whether a message is sent
at all, or to whom.

## Requirements

### Requirement: The package sends nothing until a site asks it to

The package SHALL send a message only where **both** of the following hold: the site has asked
for that direction of sending, and the host is able to send mail at all. Neither condition
SHALL be taken to imply the other.

**How a site asks is defined per direction** by the requirement below: an explicit setting for
the booker's direction; a configured recipient list **or a responsibility assignment** (see the
`responsibility` capability) for the site's own. Each is something the site did on purpose;
none is something an install or upgrade can do on the site's behalf.

**Because an Umbraco site configures SMTP for password resets and backoffice invites long before
it has any opinion about booking confirmations.** Treating a configured mail server as permission
to write to a site's customers would mean upgrading the package silently begins outbound contact
with people who booked while it sent nothing — the one change a package must never make on a
site's behalf.

Sending SHALL therefore be **off by default**, and installing or upgrading the package SHALL send
nobody anything until a site changes its configuration or assigns responsibility.

**Whether the host can send SHALL be established at the time of sending, not at startup**, since a
site's mail configuration can change while the site runs, and a value read once would answer for a
configuration that no longer exists.

**A host that cannot answer whether it can send SHALL NOT be treated as unable to send.** The
failure SHALL surface as a fault, on the same terms as any other failing handler. Converting an
unanswerable question into a quiet "no" would turn a misconfiguration into permanent silence with
nothing anywhere to find.

#### Scenario: A site that has not enabled sending is not written to
- **WHEN** a booking is placed, confirmed, declined or cancelled on a site that has asked for no direction of sending, whatever its mail configuration
- **THEN** no message is sent to anyone

#### Scenario: Enabling sending on a host that cannot send mail sends nothing
- **WHEN** a site enables sending and the host reports it cannot send mail
- **THEN** no message is sent, and the mismatch is reported once when the configuration is resolved

#### Scenario: Mail configuration appearing later is honoured without a restart
- **WHEN** a site has enabled sending, the host could not send mail, and its mail configuration subsequently becomes usable
- **THEN** a later booking produces a message, without the site being restarted

#### Scenario: A host that cannot answer produces a fault, not silence
- **WHEN** the host raises an error rather than reporting whether it can send mail
- **THEN** the error is reported and the booking is unaffected, rather than being treated as a decision not to send

### Requirement: The booker and the site are told independently

The package SHALL treat writing to the booker and writing to the site's own people as two
separately enabled directions, each with its own condition.

Writing to the booker SHALL be enabled by an explicit setting. Writing to the site's own people
SHALL be enabled by **the existence of at least one internal recipient**, arrived at through
either of two independent tiers:

- **the configured recipient list** — site-wide; every internal message goes to it while it is
  present, and an empty list remains a full opt-out of this tier;
- **responsibility** — the parties resolved for the booking under the `responsibility`
  capability's rules.

The two tiers SHALL be a union: neither one's presence or absence SHALL switch the other off,
so configuring the first responsibility assignment cannot silently stop the site-wide list
hearing about that subject's bookings. There SHALL be no second on/off switch for the site's
direction — a site that supplies addresses or assigns responsibility has said what it wants by
doing so, and a separate switch would add a way for the two to disagree.

**A site SHALL be able to be told about bookings without any message being sent to its
customers**, and the reverse. Neither direction SHALL be reachable only through the other.

#### Scenario: Internal notification without writing to customers
- **WHEN** a site configures recipients and does not enable writing to the booker
- **THEN** a booking produces a message to those recipients and none to the booker

#### Scenario: Responsibility alone enables the site's direction
- **WHEN** a site configures no recipient list, a booking's resource has a responsible user that resolution reaches, and writing to the booker is not enabled
- **THEN** the booking produces an internal message to that user and none to the booker

#### Scenario: The tiers are a union, not a precedence
- **WHEN** a site has both a configured recipient list and a responsibility assignment the booking resolves
- **THEN** the internal message reaches the configured recipients and the resolved parties, deduplicated by address

#### Scenario: Writing to customers without internal notification
- **WHEN** a site enables writing to the booker, configures no recipients and has no responsibility assignment the booking resolves
- **THEN** a booking produces a message to the booker and none to anyone else

#### Scenario: Neither is configured
- **WHEN** a site enables neither direction — no booker setting, no recipient list, no responsibility assignment the booking resolves
- **THEN** a booking produces no message at all

### Requirement: An erased booker is never written to, and the site is still told

Where a booking's booker has been erased, the package SHALL send that booker no message, and
SHALL still send any message the site's own recipients are due.

**This is reachable rather than theoretical.** The retention sweep erases a booker a configured
period after their booking ends, and the booking can still be cancelled afterwards — so a
cancellation whose booker has no contact details is an ordinary event, not a corrupt state.

The absence of contact details SHALL be established by their absence and not by inspecting their
contents, so that a booker carrying an empty or default value can never be mistaken for one
carrying an address.

#### Scenario: Cancelling a booking whose booker was erased
- **WHEN** a booking whose booker has been erased is cancelled, with both directions enabled
- **THEN** no message is addressed to the booker, and the configured recipients are told

#### Scenario: The erased booker is not reconstructed
- **WHEN** a message is composed for a booking whose booker has been erased
- **THEN** nothing in it states or implies a name, an address or a telephone number for that booker

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

### Requirement: A message to the site's own people carries no personal data

A message sent to the site's own recipients SHALL identify the booking by its reference, when
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

### Requirement: Sending cannot harm a booking, and the package exposes no booker in reporting it

A failure to send SHALL NOT affect the booking it concerns, SHALL NOT be reported to the person
who placed or cancelled it, and SHALL NOT be retried or queued.

**The booking is already stored by the time anything is sent.** A message is not a booking, and a
mail server's fault must not become the booker's problem.

**No report of a sending failure that the package composes SHALL contain a booker's name, address
or telephone number.** A failure to send is not a reason to write into a log the very details the
rest of the package takes care to govern — the same rule the notification adapter already applies
to a failing handler.

**The package SHALL NOT claim more than that, and SHALL disclose the difference.** A mail server
rejecting an address commonly quotes it back, and that text arrives inside an exception the host
logs. It is not text the package wrote and not text it can reliably redact — an attempt would as
readily destroy the diagnostic that makes a failed send findable at all. So the documentation
SHALL state that on a site which sends messages, contact details can appear in the application
log by that route. An absolute promise here would be the more dangerous failure: it is the kind
a site repeats to a data subject.

A recipient address a **site** configured MAY be reported, and reporting one SHALL NOT be read as
licence to report a booker's. They are different populations: one is a staff address typed into
configuration by whoever administers the site, the other is a customer's personal data.

#### Scenario: A failing send leaves the booking alone
- **WHEN** sending fails for a booking that has been placed
- **THEN** the booking remains exactly as stored, and the person who placed it is told nothing about the failure

#### Scenario: A failing send is not retried
- **WHEN** sending fails
- **THEN** it is not retried and not queued for a later attempt

#### Scenario: A failure report the package composes carries no booker
- **WHEN** the package reports a sending failure
- **THEN** the text the package composes contains no booker name, address or telephone number

#### Scenario: What the package cannot keep out of a log is documented
- **WHEN** a site author reads what the package promises about personal data in logs
- **THEN** it states both that the package writes no contact details itself and that a mail server's error may quote an address into the site's own logging

### Requirement: A site can replace what the package sends

The package SHALL send through the host's own mail abstraction in a way that lets a site
intercept a message and substitute its own, and SHALL identify its messages on that seam so a
site can act on the package's mail specifically.

**Because the alternative is a site forking the package to change a sentence.** The host already
publishes this seam; using it costs nothing and means a site can change wording, add its own
branding or route a message elsewhere without waiting for the package to make it configurable.

The package SHALL NOT supply a sender address of its own, and SHALL let the host apply the address
the site has already configured for its mail. **A sender address of the package's own would be a
second source of truth** for a fact the site has already stated once, able to disagree with it and
able to name an address the configured mail server will not relay for.

#### Scenario: A site substitutes its own message
- **WHEN** a site handles the host's outgoing-mail seam and marks a booking message as handled
- **THEN** the package's own message is not sent

#### Scenario: The package's mail is identifiable on the seam
- **WHEN** a site inspects a booking message on that seam
- **THEN** it can tell the package's booking mail from the host's other mail

#### Scenario: The sender is the site's
- **WHEN** a message is sent
- **THEN** the package supplies no sender address and the host applies the site's configured one

### Requirement: Configuration is absent, or it is valid

Each of the package's notification settings SHALL resolve to *not configured* when it is absent,
blank or unusable, and SHALL NOT fall back to a default that enables anything.

**A typo SHALL NOT turn sending on**, and SHALL NOT silently change who is written to.

Where a configured recipient list contains an unusable address, **that address SHALL be dropped
and the remaining usable ones kept**, rather than the list being refused entirely. One mistyped
address silencing every internal notification would be a worse failure than one address not
receiving, and it would be far harder to diagnose. Each dropped address SHALL be reported.

#### Scenario: Absent configuration enables nothing
- **WHEN** no notification configuration is present and no responsibility assignment resolves for a booking
- **THEN** neither direction is enabled and no message is sent

#### Scenario: An unusable enabling value does not enable
- **WHEN** the setting enabling messages to the booker is present but not a usable value
- **THEN** messages to the booker are not enabled

#### Scenario: One unusable address does not silence the list
- **WHEN** a configured recipient list contains both usable and unusable addresses
- **THEN** the usable ones receive messages, and each unusable one is reported

#### Scenario: A list of only unusable addresses configures nothing
- **WHEN** every address in a configured recipient list is unusable
- **THEN** no internal message is sent and each unusable address is reported

### Requirement: Which events produce messages, and for whom
Five booking events SHALL be able to produce messages: placement, confirmation, decline,
cancellation, and a move. **A visitor's** placement, and cancellation, SHALL address both
directions — the booker and the site's own recipients — as they always have. **Confirmation,
decline, a move, and an operator's placement on a booker's behalf SHALL address the booker
only**: the site's own people, or a colleague, performed the action, and the
bookings screen is where its state lives; a message telling the site what it just did would
be noise that trains recipients to skim.

**Placement is therefore the one event whose recipients depend on who placed it**, and that
SHALL be derived from the placement itself rather than from any setting or from the booking's
status. A booking an operator took at the desk and a booking a visitor placed are the same kind
of thing afterwards — *Placing a booking on a booker's behalf* requires that they carry no
marker distinguishing them — so the decision belongs to the act of placing and cannot be
recovered from the row later.

*The case that would move an operator's placement back to both directions is recorded here for
the same reason it is recorded for a move, and it is the same case: a responsible party for a
resource did not take the booking, and would reasonably want to know their diary has gained one.
It bites harder here than for a move, because a placement adds a commitment rather than shifting
one that was already theirs to see. It is nevertheless decided the same way, because the
alternative tells every internal recipient about an action one of their colleagues performed
deliberately, seconds earlier, on the screen that already lists it. Whichever change gives
responsible parties a diary of their own decides this again.*

*A move is placed on that side deliberately, and the case that would move it is recorded so the
decision is revisited rather than rediscovered: a responsible party for a resource did not
perform an operator's move of a booking on it, and would reasonably want to know their diary
changed. That case bites hardest when a move changes* which *resource is claimed, and a move
here changes only when. Whichever change lets a booking change resource decides this again.*

**The message a move sends SHALL say where the booking moved from as well as where it now is**,
both expressed in the booking's own zone. A customer holding an old confirmation needs to be
told which of the two times in their inbox is the real one, and a message stating only the new
time leaves them to work that out.

Every message SHALL remain subject to the existing gating without exception: the direction
asked for AND the host able to send. A confirmation, decline or move on a site that
has not enabled writing to the booker SHALL send nothing at all.

**A booking placed under auto-confirm SHALL produce one message to the booker, not two.**
Auto-confirmation is not an event; it is what placement produced, and the placement message
already says so.

**An operator's placement SHALL likewise produce one message to the booker, not two**, and for
the same reason: it is confirmed because of what placing it meant, not because anybody
subsequently confirmed it, and no confirmation event has occurred.

A confirmation, decline or move of a booking whose booker has been erased SHALL send nothing to
anyone — there is no address, and no internal message is due for these events.

#### Scenario: A confirmation is told to the booker only
- **WHEN** an operator confirms a requested booking, with both directions enabled
- **THEN** the booker receives a message whose wording derives from the booking's confirmed state, and the site's own recipients receive nothing

#### Scenario: A decline is told to the booker only
- **WHEN** an operator declines a requested booking, with both directions enabled
- **THEN** the booker receives a message whose wording derives from the booking's declined state, and the site's own recipients receive nothing

#### Scenario: A move is told to the booker only, and says where from
- **WHEN** an operator moves a booking, with both directions enabled and a responsibility assignment on a claimed resource
- **THEN** the booker receives a message carrying the reference, the previous interval and the new interval in the booking's own zone, and neither the site's own recipients nor the responsible party receives anything

#### Scenario: A decline on a site that has not enabled booker emails is silent
- **WHEN** an operator declines a requested booking on a site that has configured recipients but not enabled writing to the booker
- **THEN** no message is sent to anyone

#### Scenario: A move on a site that has not enabled booker emails is silent
- **WHEN** an operator moves a booking on a site that has configured recipients but not enabled writing to the booker
- **THEN** no message is sent to anyone

#### Scenario: Auto-confirmed placement sends one booker message
- **WHEN** a booking is placed while `AutoConfirm` is on, with booker emails enabled
- **THEN** the booker receives exactly one message, and its wording derives from the booking's confirmed state

#### Scenario: Confirming a booking whose booker was erased sends nothing
- **WHEN** a requested booking whose booker has been erased is confirmed or declined, with both directions enabled
- **THEN** no message is sent to anyone

#### Scenario: Moving a booking whose booker was erased sends nothing
- **WHEN** a booking whose booker has been erased is moved, with both directions enabled
- **THEN** the move succeeds and no message is sent to anyone

#### Scenario: An operator's placement is told to the booker only
- **WHEN** an operator places a booking on a booker's behalf, with both directions enabled
- **THEN** the booker receives the placement message carrying the reference, and the site's own recipients receive nothing

#### Scenario: A visitor's placement still tells both
- **WHEN** a visitor places a booking, with both directions enabled
- **THEN** the booker receives the placement message and the site's own recipients receive theirs, exactly as before

#### Scenario: An operator's placement sends the booker one message
- **WHEN** an operator places a booking on a booker's behalf, with booker emails enabled
- **THEN** the booker receives exactly one message, and its wording derives from the booking's confirmed state

#### Scenario: An operator's placement on a site without booker emails is silent
- **WHEN** an operator places a booking on a booker's behalf on a site that has configured recipients but not enabled writing to the booker
- **THEN** no message is sent to anyone, and the placement still succeeds

### Requirement: The internal message says when a booking awaits action
The message placement sends to the site's own recipients SHALL state that the booking
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
of the whole design. That a message to the site's own recipients carries no booker name,
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
