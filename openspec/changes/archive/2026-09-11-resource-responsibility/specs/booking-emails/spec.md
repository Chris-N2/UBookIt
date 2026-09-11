# Delta for booking-emails

Guarantee-diff note. For the first two MODIFIED requirements: every SHALL and every
scenario of the existing requirement is carried forward; the only change is that "the
site's own people are sent to" gains a second, independent way of being asked for —
responsibility assignments — whose resolution rules live in the `responsibility`
capability. Nothing is dropped.

The remaining four were added by the sync-time outward sweep (task 7.1): the phrase
"configured recipients" named the internal audience before responsibility widened it, so
each is replaced with its body byte-identical except that term — plus one scenario,
"Absent configuration disables sending", whose claim assignments falsified outright; it
becomes "Absent configuration enables nothing" with the assignment absence stated in its
WHEN. Every other SHALL and scenario is carried verbatim.

## MODIFIED Requirements

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
Four booking events SHALL be able to produce messages: placement, confirmation, decline, and
cancellation. Placement and cancellation SHALL address both directions — the booker and the
site's own recipients — as they always have. **Confirmation and decline SHALL address
the booker only**: the site's own people, or a colleague, performed the action, and the
bookings screen is where its state lives; a message telling the site what it just did would
be noise that trains recipients to skim.

Every message SHALL remain subject to the existing gating without exception: the direction
asked for AND the host able to send. A confirmation or decline on a site that
has not enabled writing to the booker SHALL send nothing at all.

**A booking placed under auto-confirm SHALL produce one message to the booker, not two.**
Auto-confirmation is not an event; it is what placement produced, and the placement message
already says so.

A confirmation or decline of a booking whose booker has been erased SHALL send nothing to
anyone — there is no address, and no internal message is due for these events.

#### Scenario: A confirmation is told to the booker only
- **WHEN** an operator confirms a requested booking, with both directions enabled
- **THEN** the booker receives a message whose wording derives from the booking's confirmed state, and the site's own recipients receive nothing

#### Scenario: A decline is told to the booker only
- **WHEN** an operator declines a requested booking, with both directions enabled
- **THEN** the booker receives a message whose wording derives from the booking's declined state, and the site's own recipients receive nothing

#### Scenario: A decline on a site that has not enabled booker emails is silent
- **WHEN** an operator declines a requested booking on a site that has configured recipients but not enabled writing to the booker
- **THEN** no message is sent to anyone

#### Scenario: Auto-confirmed placement sends one booker message
- **WHEN** a booking is placed while `AutoConfirm` is on, with booker emails enabled
- **THEN** the booker receives exactly one message, and its wording derives from the booking's confirmed state

#### Scenario: Confirming a booking whose booker was erased sends nothing
- **WHEN** a requested booking whose booker has been erased is confirmed or declined, with both directions enabled
- **THEN** no message is sent to anyone

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
