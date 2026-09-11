# booking-emails — delta for approval-decline

## ADDED Requirements

### Requirement: Which events produce messages, and for whom
Four booking events SHALL be able to produce messages: placement, confirmation, decline, and
cancellation. Placement and cancellation SHALL address both directions — the booker and the
site's configured recipients — as they always have. **Confirmation and decline SHALL address
the booker only**: the site's own people, or a colleague, performed the action, and the
bookings screen is where its state lives; a message telling the site what it just did would
be noise that trains recipients to skim.

Every message SHALL remain subject to the existing gating without exception: the direction
enabled in configuration AND the host able to send. A confirmation or decline on a site that
has not enabled writing to the booker SHALL send nothing at all.

**A booking placed under auto-confirm SHALL produce one message to the booker, not two.**
Auto-confirmation is not an event; it is what placement produced, and the placement message
already says so.

A confirmation or decline of a booking whose booker has been erased SHALL send nothing to
anyone — there is no address, and no internal message is due for these events.

#### Scenario: A confirmation is told to the booker only
- **WHEN** an operator confirms a requested booking, with both directions enabled
- **THEN** the booker receives a message whose wording derives from the booking's confirmed state, and the configured recipients receive nothing

#### Scenario: A decline is told to the booker only
- **WHEN** an operator declines a requested booking, with both directions enabled
- **THEN** the booker receives a message whose wording derives from the booking's declined state, and the configured recipients receive nothing

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
The message placement sends to the site's configured recipients SHALL state that the booking
awaits approval when the booking it announces is `Requested`, and SHALL NOT state it when the
booking is `Confirmed`.

**The statement SHALL derive from the booking's status, not from the setting.** The message
describes the booking it announces; deriving it from configuration would let the two drift
the day anything else decides a placement's status.

The message SHALL continue to satisfy everything already required of it: it carries the
reference, the time, what was booked and the backoffice link, and no booker name, address or
telephone number. Awaiting approval is a fact about the booking, not about the person.

#### Scenario: A requested placement flags the wait
- **WHEN** the internal message is composed for a booking placed as `Requested`
- **THEN** it states that the booking awaits approval, alongside the link to the backoffice screen where it can be acted on

#### Scenario: A confirmed placement does not flag a wait
- **WHEN** the internal message is composed for a booking placed as `Confirmed`
- **THEN** it does not state or imply that any action is awaited

#### Scenario: The flag carries no personal data
- **WHEN** the internal message for a requested placement is composed
- **THEN** it states no booker name, no address and no telephone number, exactly as for any other internal message

## MODIFIED Requirements

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
- **WHEN** a booking is placed, confirmed, declined or cancelled on a site that has not enabled sending, whatever its mail configuration
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
