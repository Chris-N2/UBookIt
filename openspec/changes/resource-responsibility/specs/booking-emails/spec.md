# Delta for booking-emails

Guarantee-diff note for both MODIFIED requirements: every SHALL and every scenario of the
existing requirement is carried forward; the only change is that "the site's own people are
sent to" gains a second, independent way of being asked for — responsibility assignments —
whose resolution rules live in the `responsibility` capability. Nothing is dropped.

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
