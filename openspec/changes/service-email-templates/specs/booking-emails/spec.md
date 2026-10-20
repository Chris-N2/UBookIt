## MODIFIED Requirements

### Requirement: A confirmed booker is given the service's instructions

Where a booking was placed for a service, and that service has instructions for the booker (see
the `services` capability), a message to the booker SHALL carry those instructions **if and only
if the booking's status is `Confirmed` at the moment the message is composed**.

The rule SHALL be derived from the booking's status, not from which message is being sent or from
the site's `AutoConfirm` setting. That gives these outcomes:
- the placement message carries the instructions on a site that confirms on placement;
- the placement message does not carry them for a booking still awaiting approval;
- the confirmation message carries them;
- the move message carries them for a confirmed booking, and not for a request still awaiting
  approval;
- a decline or a cancellation never carries them.

A site that requires approval SHALL NOT thereby hand the instructions, which may include a link
to join a meeting, to a person it has not accepted.

**The instructions SHALL be read from the service when the message is composed**, not recorded
when the booking was placed. Instructions exist to be acted on, so the current text is the useful
one. A booking whose service no longer exists, or has no instructions, SHALL carry none. This is
unlike the service's **name** in the same message, which remains the name recorded at placement.

**A failure to read the service SHALL cost the instructions, and SHALL NOT cost the message.** The
message SHALL still be sent, carrying everything else the package's own message for that event
carries, and the failure SHALL be logged without any booker detail. Where the site supplies its own
content, the same failure also means the package's own content is used for that message instead
of the site's: the service's email template set cannot be established without the read, and the
`email-templates` capability decides that case. That is the one further thing a failed read
costs.

**A message to the site's own recipients SHALL NOT carry the instructions.** They are written for
the booker.

In the package's own wording, the instructions SHALL be introduced as instructions for the booking
and reproduced as written, line breaks included. They SHALL be placed after what was booked and
when, and before any cancellation link.

**Where a site supplies its own content for the message, the package SHALL NOT claim that it
carries the instructions.** This is the same narrowing, for the same reason, that already applies
to the cancellation link: the words are the site's. The package makes the instructions available
to the view (see the `email-templates` capability) on exactly the terms above.

#### Scenario: Placed and confirmed on placement
- **WHEN** a booking for a service with instructions is placed on a site that confirms on placement
- **THEN** the booker's placement message carries the instructions

#### Scenario: Placed and awaiting approval
- **WHEN** a booking for a service with instructions is placed on a site that requires approval
- **THEN** the booker's placement message does not carry the instructions

#### Scenario: Confirmed by an operator
- **WHEN** a requested booking for a service with instructions is confirmed
- **THEN** the booker's confirmation message carries the instructions

#### Scenario: A confirmed booking is moved
- **WHEN** a confirmed booking for a service with instructions is moved
- **THEN** the booker's move message carries the instructions

#### Scenario: A requested booking is moved
- **WHEN** a booking still awaiting approval, for a service with instructions, is moved
- **THEN** the booker's move message does not carry the instructions

#### Scenario: Declined or cancelled
- **WHEN** a booking for a service with instructions is declined or cancelled
- **THEN** the booker's message does not carry the instructions

#### Scenario: The site's own recipients are not given them
- **WHEN** the message to the site's own recipients is composed for a booking for a service with instructions
- **THEN** it does not carry the instructions

#### Scenario: The current instructions are sent
- **WHEN** a service's instructions are changed after a booking was placed, and that confirmed booking is then moved
- **THEN** the move message carries the instructions as they are now

#### Scenario: A booking placed directly against a resource
- **WHEN** a message is composed for a confirmed booking placed directly against a resource
- **THEN** it carries no instructions

#### Scenario: The service cannot be read
- **WHEN** reading the service fails while a confirmed booking's message is being composed, on a site that supplies no content
- **THEN** the message is sent without the instructions, carrying everything else the package's own message for that event carries, and the failure is logged without any booker detail

#### Scenario: The service cannot be read on a site that supplies content
- **WHEN** reading the service fails while a confirmed booking's message is being composed, and the site supplies a view for that message
- **THEN** the message is sent carrying the package's own content without the instructions, rather than the site's content, and the failure is logged once without any booker detail

#### Scenario: The instructions sit between the booking and the cancellation link
- **WHEN** the package's own wording is composed for a confirmed booking that carries instructions and a cancellation link
- **THEN** the instructions appear as written, line breaks included, after what was booked and when and before the cancellation link
