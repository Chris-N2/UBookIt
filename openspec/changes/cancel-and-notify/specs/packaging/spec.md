## ADDED Requirements

### Requirement: What a site can subscribe to is documented
The package raises notifications a consuming site can handle, and those SHALL be documented
in the package's own documentation. **An extension point nobody is told about is not one** —
a site that does not know a hook exists will either go without or reach past the contracts
into the database, which is the outcome every port in this package exists to prevent.

The documentation SHALL name each notification, state when it is raised, say what it carries,
and show how a site subscribes.

**It SHALL state plainly that the package sends nothing itself** — no email, no message of
any kind, to a booker or to anyone else — so that "uBookIt notified them" is never assumed.
This is the sentence most likely to be discovered the expensive way: by a customer arriving
for a booking that was cancelled.

**It SHALL state the limits of what a subscriber is promised**, and specifically that a
handler which throws is a notification nobody receives, because the package neither retries
nor queues. A half-stated promise about delivery is worse than none: it is relied on and then
found out during an incident.

#### Scenario: A site author can find the hooks
- **WHEN** a site author looks for a way to react to a booking
- **THEN** the documentation names the notifications, when each is raised, what it carries, and how to subscribe

#### Scenario: The package's silence is stated, not implied
- **WHEN** a site author reads what the package does when a booking is placed or cancelled
- **THEN** it says the package sends no email or message of its own, and that anything a booker receives is the site's to send

#### Scenario: The delivery limit is stated
- **WHEN** a site author reads what they are promised about a notification
- **THEN** it says a handler that throws will not be retried or queued
