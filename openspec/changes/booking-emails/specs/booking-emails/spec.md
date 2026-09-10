## ADDED Requirements

### Requirement: The package sends nothing until a site asks it to

The package SHALL send a message only where **both** of the following hold: the site has enabled
that direction of sending in its own configuration, and the host is able to send mail at all.
Neither condition SHALL be taken to imply the other.

**Because an Umbraco site configures SMTP for password resets and backoffice invites long before
it has any opinion about booking confirmations.** Treating a configured mail server as permission
to write to a site's customers would mean upgrading the package silently begins outbound contact
with people who booked while it sent nothing — the one change a package must never make on a
site's behalf.

Sending SHALL therefore be **off by default**, and installing or upgrading the package SHALL send
nobody anything until a site changes its configuration.

**Whether the host can send SHALL be established at the time of sending, not at startup**, since a
site's mail configuration can change while the site runs, and a value read once would answer for a
configuration that no longer exists.

**A host that cannot answer whether it can send SHALL NOT be treated as unable to send.** The
failure SHALL surface as a fault, on the same terms as any other failing handler. Converting an
unanswerable question into a quiet "no" would turn a misconfiguration into permanent silence with
nothing anywhere to find.

#### Scenario: A site that has not enabled sending is not written to
- **WHEN** a booking is placed or cancelled on a site that has not enabled sending, whatever its mail configuration
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
SHALL be enabled by **the presence of a configured recipient list** — a site that supplies
addresses has said what it wants by supplying them, and a second switch would add a way for the
two to disagree.

**A site SHALL be able to be told about bookings without any message being sent to its
customers**, and the reverse. Neither direction SHALL be reachable only through the other.

#### Scenario: Internal notification without writing to customers
- **WHEN** a site configures recipients and does not enable writing to the booker
- **THEN** a booking produces a message to those recipients and none to the booker

#### Scenario: Writing to customers without internal notification
- **WHEN** a site enables writing to the booker and configures no recipients
- **THEN** a booking produces a message to the booker and none to anyone else

#### Scenario: Neither is configured
- **WHEN** a site enables neither direction
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
produces a confirmed booking today; a message whose wording assumes it would become false the day
a site can require approval, silently, in a message already sent to a customer.

**A message SHALL be sent even where what was booked cannot be established**, carrying the
reference and the time without it. The reference and the time are the parts a person cannot
reconstruct for themselves; withholding them because a name could not be resolved would be the
wrong trade.

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

#### Scenario: The internal message names no booker
- **WHEN** a message is composed for the site's own recipients
- **THEN** it carries the reference, the time and what was booked, and states no name, no address and no telephone number

#### Scenario: The internal message routes to the governed view
- **WHEN** a message is composed for the site's own recipients and the site's address is known
- **THEN** it carries a link to the backoffice screen where bookings are seen

#### Scenario: The site's address is not configured
- **WHEN** the site's own address cannot be established
- **THEN** the message is still sent, carrying the reference, the time and what was booked, and no link

### Requirement: Sending cannot harm a booking, and reporting it cannot expose a booker

A failure to send SHALL NOT affect the booking it concerns, SHALL NOT be reported to the person
who placed or cancelled it, and SHALL NOT be retried or queued.

**The booking is already stored by the time anything is sent.** A message is not a booking, and a
mail server's fault must not become the booker's problem.

**No report of a sending failure SHALL contain a booker's name, address or telephone number.** A
failure to send is not a reason to write into a log the very details the rest of the package takes
care to govern — the same rule the notification adapter already applies to a failing handler.

A recipient address a **site** configured MAY be reported, and reporting one SHALL NOT be read as
licence to report a booker's. They are different populations: one is a staff address typed into
configuration by whoever administers the site, the other is a customer's personal data.

#### Scenario: A failing send leaves the booking alone
- **WHEN** sending fails for a booking that has been placed
- **THEN** the booking remains exactly as stored, and the person who placed it is told nothing about the failure

#### Scenario: A failing send is not retried
- **WHEN** sending fails
- **THEN** it is not retried and not queued for a later attempt

#### Scenario: A failure report carries no booker
- **WHEN** a sending failure is reported
- **THEN** the report contains no booker name, address or telephone number

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

#### Scenario: Absent configuration disables sending
- **WHEN** no notification configuration is present
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
