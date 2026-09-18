# self-service-cancellation Specification

## Purpose

Lets the person who made a booking cancel it themselves, authenticated by possession of the mailbox
the booking was placed against rather than by knowledge of the booking's reference. This is the
package's first authentication primitive, and the capability exists to state what the credential is,
what it may disclose, and what it must refuse.

## Requirements

### Requirement: The credential is the mailbox, never the reference

Self-service cancellation SHALL be authenticated by a **secret issued to the booker's email
address**, and SHALL NOT be authenticated by the booking's reference, in whole or in part.

**Because a reference is designed to be quoted.** It is printed on confirmations, read out over the
telephone and shown to any operator who can list bookings; the `booking-management` capability
requires it be presented in the form a person is expected to quote. A credential a customer is
expected to read aloud is not a credential.

The reference SHALL remain what it is — an identifier — and MAY be shown to a caller who already
holds a valid secret. It SHALL NOT be accepted as proof of anything.

**No part of this flow SHALL accept a reference as input.** A route that takes a reference and
answers differently according to whether it names a booking is an enumeration oracle, whatever it
then requires; the package refuses the input rather than hardening the answer.

#### Scenario: A reference is not a credential
- **WHEN** a caller presents a booking's reference to any part of the self-service cancellation flow
- **THEN** no booking is disclosed, none is cancelled, and the reference is not accepted as input

#### Scenario: The holder of a valid secret may be shown the reference
- **WHEN** a caller follows a valid, unredeemed cancellation link
- **THEN** the booking's reference is among what is shown

### Requirement: A cancellation secret is issued with the booker's message, and only then

Where the feature is enabled, the package SHALL issue a cancellation secret for a booking **at the
point a message to its booker is due**, and SHALL convey it to the booker only within that message.

**The message is the channel, so the message is the gate.** A secret issued where no message is sent
would be a credential nobody holds; conveying it by any other route would be a second channel this
capability has not reasoned about.

The secret SHALL be generated from a cryptographically secure source and SHALL be long enough that
guessing it is not a practical attack.

**The package SHALL store only a one-way hash of the secret**, never the secret itself. The secret
SHALL exist in the message sent to the booker and nowhere else that the package writes.

**Because a stored credential is a credential that can leak.** A database copy, a backup, or a
person with backoffice access would otherwise hold every outstanding cancellation link; hashing
means the package itself cannot act as a booker, which is the same reasoning that keeps booker
contact details out of messages to a site's own recipients.

#### Scenario: No message, no secret
- **WHEN** a booking is placed on a site where the booker is sent no message
- **THEN** no cancellation secret is issued for it

#### Scenario: The stored form is not usable as a credential
- **WHEN** a cancellation secret has been issued
- **THEN** what the package has stored cannot be presented to the flow to cancel the booking

### Requirement: A secret is single use and expires when the booking starts

A cancellation secret SHALL be accepted **at most once**, and SHALL NOT be accepted at or after the
**start of the booking it cancels**.

**The expiry is derived, not configured.** A cancellation link's usefulness ends when the thing it
cancels begins, so the booking already states when the secret should die; a configured lifetime
would be a second answer able to disagree with the first, and a setting a site would have to reason
about for no benefit.

Redemption SHALL be recorded such that a second presentation of the same secret is refused even
where the first attempt's cancellation could not be completed.

#### Scenario: A redeemed secret is refused
- **WHEN** a cancellation secret that has already been redeemed is presented again
- **THEN** it is refused, and the booking is unaffected

#### Scenario: A secret does not outlive the booking's start
- **WHEN** a cancellation secret is presented at or after the start of the booking it belongs to
- **THEN** it is refused

### Requirement: Following a link discloses, acting on it requires a deliberate submission

Retrieving the cancellation page SHALL NOT change any booking. Cancelling SHALL require a separate,
deliberate submission carrying anti-forgery protection.

**Because a link in an email is fetched by machines before a person sees it.** Mail security
products, corporate scanners and client previewers issue an unattended retrieval of every URL in a
message. A retrieval that cancelled would mean bookings cancelled by robots, on a schedule the
booker did not choose and cannot appeal.

A secret SHALL therefore be marked redeemed by the **submission**, not by the retrieval, so that an
unattended fetch neither cancels the booking nor consumes the booker's one use of their link.

#### Scenario: An unattended fetch changes nothing
- **WHEN** the cancellation page is retrieved without any submission following it
- **THEN** the booking is not cancelled, and the secret remains usable

#### Scenario: Cancelling requires a protected submission
- **WHEN** a submission to cancel arrives without valid anti-forgery protection
- **THEN** it is refused and the booking is unaffected

### Requirement: The page states which booking, and never who booked it

The cancellation page SHALL show the booking's **reference, what was booked where it can be
established, and when the booking is**, expressed in the time zone the booking was placed against.

It SHALL NOT show the booker's name, email address or telephone number, **and this SHALL be
structural rather than a matter of what the view is written to omit.**

**Because refusing to name the booking would be the worse failure.** A person holding several
bookings and several messages cannot tell which link they followed; a page saying only "confirm you
want to cancel your booking" invites cancelling the wrong one. The secret is already sufficient to
*destroy* the booking, so withholding a description of what is about to be destroyed protects
nothing and costs the reader the one fact they need.

**What it shows is the description the package already trusts to a reader who may not see contact
details** — the same reference, interval and subject a message to the site's own recipients carries
— rather than a second opinion about what is safe to disclose.

#### Scenario: The booking is identified
- **WHEN** a valid cancellation link is followed
- **THEN** the page states the booking's reference, when it is, and what was booked where that can be established

#### Scenario: Contact details are unreachable, not merely omitted
- **WHEN** the cancellation page is rendered for any booking
- **THEN** the booker's name, email address and telephone number are absent, and no content supplied for that page can express them

### Requirement: Every unusable secret gets the same answer

A secret that is expired, already redeemed, belongs to a booking that can no longer be cancelled, or
was never issued SHALL produce **one indistinguishable response**.

**Because the distinctions are exactly what an attacker wants.** "Already used" says a booking
exists and somebody cancelled it; "expired" says a booking exists and when it was; "never issued"
says the guess was wrong. A page that tells the four apart rebuilds, at the end of the flow, the
oracle the design removed at the start.

The response SHALL say that the link can no longer be used and SHALL direct the reader to the site,
and SHALL disclose nothing about whether any booking exists.

#### Scenario: The four refusals are indistinguishable
- **WHEN** an expired secret, an already-redeemed secret, a secret for a booking that cannot be cancelled, and a value that was never a secret are each presented
- **THEN** each produces the same response, disclosing nothing about whether a booking exists

### Requirement: A visitor may not cancel a booking that has started

Self-service cancellation SHALL refuse a booking whose start has passed, **while the operator's
cancellation remains able to reach one.**

**The difference is one of terms, and it SHALL be carried by which entry point is called rather than
by a flag on a shared one.** An operator cancelling a booking that has already begun is tidying a
no-show, which is legitimate work; a visitor doing so is asking to undo something that has already
happened. The package already expresses exactly this distinction for placement, where visitor and
operator terms differ and the waiver is structural — a caller cannot reach operator terms by setting
a parameter, only by calling the entry point that carries them.

This SHALL NOT be configurable in this capability. A site's own policy about how late a booking may
be cancelled is a separate concern with its own vocabulary, and stating a fixed rule here does not
pre-empt it.

#### Scenario: A started booking is refused to a visitor
- **WHEN** a valid cancellation secret is redeemed for a booking whose start has passed
- **THEN** the cancellation is refused

#### Scenario: The operator's route is unchanged
- **WHEN** an operator cancels a booking whose start has passed
- **THEN** the cancellation succeeds, as it did before this capability existed

### Requirement: A self-service cancellation is an ordinary cancellation

A cancellation performed this way SHALL produce the same booking state, the same observer
notification and the same messages as a cancellation performed any other way.

**Because a booking cancelled by the person who made it is not a different kind of cancelled.** A
distinct state or a distinct event would have to be handled everywhere the existing one is, and
every place that forgot would be a defect; it would also let a reader of the booking work out how it
was cancelled, which is not a fact the package undertakes to record.

#### Scenario: The cancellation is indistinguishable afterwards
- **WHEN** a booking is cancelled through the self-service flow
- **THEN** its state, and what is sent about it, are the same as for a cancellation made from the backoffice

### Requirement: The feature is off until a site turns it on, and says why it cannot run

Self-service cancellation SHALL be **off by default**, and SHALL be enabled only by configuration —
never by an operator through the settings screen, and never as a consequence of installing or
upgrading the package.

**Because it is anonymous exposure**, in the same sense the delivery API's switches are: it opens a
route that changes a site's data to a caller the package cannot identify beyond a secret. The
`site-settings` capability already puts such switches beyond an operator's reach.

Enabling it SHALL have no effect where the site sends the booker no message, since there is then no
channel to carry a link. **Where the feature is enabled but cannot run for that reason, the package
SHALL say so where the setting is presented**, and SHALL NOT present it as enabled and working.

**Because a setting that reports itself on while doing nothing is the failure this project has
already paid for**: a readout that describes a configuration the site does not have.

While the feature is off, no secret SHALL be issued, no link SHALL appear in any message the package
composes, and the cancellation route SHALL NOT be served.

#### Scenario: Off by default
- **WHEN** the package is installed or upgraded with no configuration for this feature
- **THEN** no cancellation secret is issued, no message carries a link, and the cancellation route is not served

#### Scenario: Enabled without a channel is reported, not silent
- **WHEN** the feature is enabled on a site that sends the booker no message
- **THEN** the settings screen states that it cannot run, and why

#### Scenario: An operator cannot turn it on
- **WHEN** a write enabling the feature is submitted through the settings screen by a user holding the settings verb
- **THEN** the write is refused and no value is stored

### Requirement: Turning the feature off stops issuing links, and outstanding ones lapse

Turning the feature off SHALL stop new secrets being issued. Secrets already issued SHALL become
unusable, and the package SHALL NOT be required to honour them.

**The consequence SHALL be documented rather than mitigated.** A booker was told they would need
their link; a site that withdraws the feature takes that away, and the honest thing is to say so in
the documentation so the decision is made knowingly. The fallback is the position before the feature
existed — the booker contacts the site — which is a degradation rather than a loss of data.

#### Scenario: No new links after the feature is withdrawn
- **WHEN** the feature is turned off
- **THEN** no further cancellation secret is issued, and no message the package composes carries a link
