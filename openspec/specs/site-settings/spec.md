# site-settings Specification

## Purpose

Where a uBookIt setting's value comes from, who may change it, and what the backoffice
settings screen promises about what it shows.

A setting has two sources: the site's own configuration, and values the package stores when
somebody saves them in the backoffice. **A stored value wins, and the absence of a stored
value is the only way to say "not overridden"** — several settings treat *no value* as
meaningful, so a sentinel could not be told apart from them. Stored values are composed
beneath the resolution the configuration already uses, so both sources are read by one
implementation and a value that cannot be read falls back identically whichever place it
came from.

Settings are tiered by **who is competent to judge them**: policy and communication belong
to whoever runs the bookings; cost, safety and data lifetime belong to whoever deploys the
site. The tier is enforced by the server rather than merely rendered by the client, and
the retention period is read-only deliberately — **nothing reachable from this screen
destroys data**, and that is a property of which settings are editable rather than of any
warning shown. The one editable setting that reinterprets existing data, the time zone,
states its consequence before the change is made.

The screen always presents the configured value beside the effective one, and restoring
removes the stored value rather than freezing the configured one, so the configuration file
never silently stops describing what runs. Access requires a verb of its own, distinct from
the verb governing resources and services.

## Requirements

### Requirement: A setting's value comes from the store first and the configuration second

The package SHALL resolve each setting from two sources: values stored by the package, and the
site's own configuration. A stored value SHALL win; where no value is stored for a key, the
configured value SHALL show through unchanged.

**The absence of a stored value SHALL be the only way to express "not overridden".** There SHALL be
no sentinel, because several settings treat *no value* as meaningful — no retention period, no
privacy link — and a stored representation of "unset" would be indistinguishable from a stored
representation of those.

**Stored values SHALL be composed beneath the same resolution the configuration already uses**, so
that a stored value and a configured value of the same setting are read, validated and fallen back
on identically. The package SHALL NOT carry a second set of fallback rules for stored values.

The site's whole configuration stack SHALL keep working underneath: a value supplied by an
environment variable, a per-environment file or a secret store is the configured value, and is what
shows through when nothing is stored.

#### Scenario: A stored value overrides a configured one
- **WHEN** a setting is present in both the site's configuration and the package's store
- **THEN** the stored value is the effective value

#### Scenario: An unstored setting falls through to configuration
- **WHEN** a setting is present in the site's configuration and absent from the store
- **THEN** the configured value is the effective value

#### Scenario: An empty store changes nothing
- **WHEN** no setting has been stored
- **THEN** every setting resolves exactly as it did before the store existed

#### Scenario: A stored value that cannot be read falls back exactly as a configured one would
- **WHEN** a stored value for a setting cannot be read as that setting's type
- **THEN** the setting resolves to the same value, and reports the same way, as if a configured value of the same text had been read

#### Scenario: Configuration from outside appsettings still shows through
- **WHEN** a setting's configured value is supplied by an environment variable rather than a file, and nothing is stored for it
- **THEN** that value is the effective value

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

### Requirement: The screen shows what a stored value is overriding, and can restore it

Wherever a stored value is in effect, the package SHALL also present **the configured value it is
overriding**, and SHALL offer an action restoring the setting to that configured value.

**This is what keeps the configuration file honest.** Once anything is stored, the file no longer
describes what runs, and a deployment that changes the file has no visible effect — so the
divergence SHALL be visible at the place someone would look for it, rather than discoverable only by
reading the database.

Restoring SHALL remove the stored value rather than store the configured one, so that a later change
to the configuration takes effect.

#### Scenario: The overridden value is visible
- **WHEN** a setting has a stored value and a different configured value
- **THEN** both are presented, identified as the effective value and the configured value

#### Scenario: Restoring returns the setting to configuration
- **WHEN** a stored setting is restored
- **THEN** the setting resolves to the configured value, and a subsequent change to the site's configuration changes it again

#### Scenario: A setting with nothing configured beneath it
- **WHEN** a setting has a stored value and the site's configuration does not carry that setting at all
- **THEN** it is presented as having nothing configured beneath it, distinctly from a setting the site has configured, and restoring it returns the setting to its documented default

#### Scenario: Having nothing configured is not inferred from the value
- **WHEN** a setting the site has not configured resolves to a non-empty documented default
- **THEN** it is still presented as having nothing configured beneath it, rather than as overriding that default

### Requirement: A value the screen stores is validated when it is written

The package SHALL validate a submitted value against its setting's type and constraints before
storing it, and SHALL refuse an invalid one, reporting the failure against the setting it concerns.

**Validation on write SHALL NOT replace the resolution fallbacks.** The two guard different things:
validation stops the screen creating a value that would silently fall back, while the fallbacks
still cover a value that reached the store by another route or stopped being valid after it was
written. They SHALL remain separate checks.

#### Scenario: An invalid value is refused rather than stored and fallen back on
- **WHEN** a value that cannot be read as its setting's type is submitted
- **THEN** the write is refused, the failure names the setting, and no value is stored

#### Scenario: A previously valid stored value still falls back
- **WHEN** a stored value that is present but unreadable is resolved
- **THEN** the setting falls back as its resolution rules require, rather than failing

### Requirement: Changing the time zone states its consequence

Where the site's time zone is offered for editing, the package SHALL present a statement of what
changing it does, **without computing anything about the site's own data**.

The statement SHALL say that availability rules are wall-clock in the site's time zone and so are
reinterpreted by the change, and that existing bookings keep the times they were made for and may
therefore no longer fall within their resource's hours. This is true of every site unconditionally:
open hours and exceptions are stored as day-and-time without a zone, while bookings are stored as
absolute instants.

**The statement SHALL be programmatically associated with the control it concerns**, not merely
placed near it.

Changing the time zone SHALL rewrite no stored value, and setting it back SHALL restore the previous
meaning of every rule.

#### Scenario: The consequence is stated before the change is made
- **WHEN** the time zone is presented for editing
- **THEN** the consequence statement is presented with it, and is associated with the time zone control programmatically

#### Scenario: The statement needs no query
- **WHEN** the consequence statement is produced
- **THEN** it is the same on every site, and producing it reads no booking, resource or availability data

#### Scenario: The change is reversible
- **WHEN** the time zone is changed and then changed back
- **THEN** every availability rule and every booking means exactly what it meant before

### Requirement: The settings screen is reached through its own verb

Access to the settings screen and to every endpoint behind it SHALL require a permission verb of its
own, distinct from the verb governing resources and services.

**Configuring a bookable resource and configuring the site are different privileges.** The settings
reach the site's retention posture, its anonymous exposure and the addresses its bookers' details are
sent to; a grant meaning "may add a meeting room" SHALL NOT carry them.

The section SHALL explain its own absence: where a user holds the section but not the settings verb,
the package SHALL indicate that the screen exists and requires a grant, rather than rendering
nothing.

#### Scenario: The section grant alone does not reach the settings
- **WHEN** a user holding the uBookIt section and the configure verb, but not the settings verb, requests the settings
- **THEN** the request is refused

#### Scenario: The absence is explained rather than hidden
- **WHEN** a user holding the section but not the settings verb opens the uBookIt section
- **THEN** they are told the settings screen requires a grant, and where it is granted

#### Scenario: The verb reaches the settings
- **WHEN** a user whose groups hold the settings verb requests the settings
- **THEN** the settings are served
