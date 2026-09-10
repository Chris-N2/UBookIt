## MODIFIED Requirements

### Requirement: What a site can subscribe to is documented
The package raises notifications a consuming site can handle, and those SHALL be documented
in the package's own documentation. **An extension point nobody is told about is not one** —
a site that does not know a hook exists will either go without or reach past the contracts
into the database, which is the outcome every port in this package exists to prevent.

The documentation SHALL name each notification, state when it is raised, say what it carries,
and show how a site subscribes.

**It SHALL state what the package sends, to whom, and under what configuration** — so that
neither "uBookIt notified them" nor "uBookIt notified nobody" is ever assumed. This is the
sentence most likely to be discovered the expensive way: by a customer arriving for a booking
that was cancelled, or by a customer receiving a message the site did not know it was sending.

Specifically, the documentation SHALL state that the package sends **nothing by default**, name
the configuration that enables each direction of sending, and state that a site's mail
configuration alone does not enable any of it. **It SHALL also state what those messages do not
carry**: that a message to a site's own recipients identifies a booking without the booker's
contact details, and that seeing those details remains governed by the backoffice.

**It SHALL state the limits of what a subscriber is promised**, and specifically that a
handler which throws is a notification nobody receives, because the package neither retries
nor queues. A half-stated promise about delivery is worse than none: it is relied on and then
found out during an incident. **The same limits SHALL be stated of the package's own messages** —
they are sent once, are not retried, are not queued, and a failure to send is not reported to the
person who booked.

#### Scenario: A site author can find the hooks
- **WHEN** a site author looks for a way to react to a booking
- **THEN** the documentation names the notifications, when each is raised, what it carries, and how to subscribe

#### Scenario: What the package sends is stated, not implied
- **WHEN** a site author reads what the package does when a booking is placed or cancelled
- **THEN** it says what the package sends, to whom, and what configuration enables it, and that anything else a booker receives is the site's to send

#### Scenario: The default is stated
- **WHEN** a site author reads the notification documentation without having configured anything
- **THEN** it says the package sends nothing until configured, and that configuring the host's mail server does not by itself enable sending

#### Scenario: What internal messages withhold is stated
- **WHEN** a site author reads what is sent to a site's own recipients
- **THEN** it says those messages carry no booker contact details, and that access to those details remains governed by the backoffice

#### Scenario: The delivery limit is stated
- **WHEN** a site author reads what they are promised about a notification or a message the package sends
- **THEN** it says a handler that throws will not be retried or queued, and that a message the package fails to send is not retried, not queued, and not reported to the booker
