## MODIFIED Requirements

### Requirement: Settings are tiered, and the tier is enforced by the server

Each setting SHALL belong to exactly one tier, declared once by the server:

- **Editable** — `UBookIt:AutoConfirm`, `UBookIt:Notifications:SendBookerEmails`,
  `UBookIt:Notifications:InternalRecipients`, `UBookIt:PrivacyPolicyUrl`.
- **Editable with a stated consequence** — `UBookIt:TimeZoneId`.
- **Read-only** — `UBookIt:RetentionDays`, `UBookIt:MaxQueryRangeDays`,
  `UBookIt:DeliveryApi:EnableReads`, `UBookIt:DeliveryApi:EnablePlacement`,
  `UBookIt:SelfServiceCancellation:Enabled`.

**`UBookIt:Frontend:PreservedQueryParameters` is deliberately not presented either.** It is a
developer's setting about their own page's URLs — which query parameters the shipped forms carry
forward — and an operator has no basis on which to judge it. It is recorded here as a removal rather
than left silent, so that a reader can tell a deliberate omission from an oversight.

**The active theme is deliberately not presented at all.** It is not a setting in the sense the
others are: it has no configuration key, it is established by a code call in the site's own
composer, and it is fixed at startup by design. Presenting it would mean coupling the management
assembly to the rendering assembly in order to tell a developer something they wrote themselves.

The tier boundary divides settings by **who is competent to judge them**: policy and communication
belong to whoever runs the bookings; cost, safety and data lifetime belong to whoever deploys the
site.

**A write to a read-only setting SHALL be refused by the server**, whatever the client rendered. The
boundary exists to keep irreversible erasure and the package's anonymous exposure out of an
operator's reach, and a boundary the client alone holds is reachable by anyone who can call the
endpoint.

**`UBookIt:SelfServiceCancellation:Enabled` is read-only for the anonymous-exposure reason**, and
not because an operator would judge it badly. It opens a route that cancels a site's bookings for a
caller the package cannot identify beyond a secret, which is the same class of decision as whether
the delivery API answers at all.

**No editable setting SHALL be capable of destroying data**, and this SHALL be a property of which
settings are editable rather than of any warning shown. `UBookIt:RetentionDays` is read-only for
this reason: erasure is irreversible, its effect is deferred to a later sweep rather than visible
when the value is saved, and an accurate warning would have to compute a count that could be wrong.

The read-only settings that cannot take effect without an application restart SHALL be shown as
such, distinguished from those that are read-only by policy alone.

**A setting whose effect depends on another setting SHALL be shown with that dependency stated when
the dependency is not met.** `UBookIt:SelfServiceCancellation:Enabled` has no effect while
`UBookIt:Notifications:SendBookerEmails` is off, because the cancellation link travels in the
booker's message and there is then no message; the screen SHALL say so rather than presenting the
feature as on and working. **Presenting a setting as enabled while the site's configuration prevents
it from doing anything would be a readout describing a configuration the site does not have** —
which is the failure this screen exists to avoid.

#### Scenario: A read-only setting cannot be written
- **WHEN** a write is submitted for a read-only setting by a user holding the settings verb
- **THEN** the write is refused and no value is stored

#### Scenario: The retention period is not editable
- **WHEN** the settings are presented
- **THEN** the retention period is shown as read-only, and no path through the screen stores a value for it

#### Scenario: Restart-bound settings are distinguished
- **WHEN** the settings are presented
- **THEN** the delivery API exposure settings are shown as requiring a restart, and the read-only settings that do not require one are not so marked

#### Scenario: The theme is absent from the settings entirely
- **WHEN** the settings are presented
- **THEN** no theme setting appears, neither editable nor read-only

#### Scenario: Self-service cancellation is read-only and restart-bound
- **WHEN** the settings are presented
- **THEN** self-service cancellation is shown as read-only and as requiring a restart, and no path through the screen stores a value for it

#### Scenario: An unmet dependency is stated
- **WHEN** the settings are presented on a site where self-service cancellation is enabled and booker emails are off
- **THEN** the screen states that the feature cannot run, and that it needs booker emails
