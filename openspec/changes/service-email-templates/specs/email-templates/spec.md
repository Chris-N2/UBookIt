## ADDED Requirements

### Requirement: A service's template set is consulted before the content for every service

Where a booking was placed for a service, and that service names an email template set (see the
`services` capability), the package SHALL look for each message's content **in that set first**.

For each message, the content used SHALL be decided in this order:
1. the view for that message **in the service's set**, at the published path for sets: the set's
   name as a folder beneath the folder the per-message views live in, holding views named exactly
   as the published message names;
2. where the set supplies no view for that message, the view for that message **for every
   service**, exactly as before this requirement;
3. where neither is supplied, the package's own content.

**Only "nothing supplied" moves down this order.** A view in the set that is supplied and **fails
to render** SHALL NOT fall through to the view for every service. The package's own content SHALL
be used instead, and the failure SHALL be reported as any failure to render is. The view for every
service may have been written for a different kind of service, and sending one service's wording
to the booker of another is worse than sending the package's neutral wording.

**A set is resolved per message.** A set SHALL NOT be required to supply every message. A set that
supplies one message SHALL change that message only.

**Every message is eligible, for both audiences.** A view in a set for the site's own recipients
SHALL receive the model for that audience, which has no member for the booker's contact details,
so that guarantee holds for content in a set exactly as it does for any other content.

**The set SHALL be read from the service when the message is composed**, not recorded when the
booking was placed. A service whose set is changed, removed or added SHALL affect the next message
about any of its bookings. A booking whose service no longer exists, or names no set, SHALL use
the order above from step 2. A booking placed directly against a resource SHALL use it from step 2.

**Where the service cannot be read**, the package cannot establish whether a set applies.
The package's own content SHALL then be used for that message, rather than the content for every
service. The message SHALL still be sent, and the failure SHALL be logged without any booker
detail.

**A set name that is not a valid set name SHALL NOT be used as a path.** Content SHALL be looked
up only for a name that satisfies the rule the `services` capability sets. Where the model handed
to the renderer carries any other name, that SHALL be treated as a failure to render: the package's
own content is used and the failure is reported.

Where the host provides no means of supplying content at all, the package SHALL NOT read the
service merely to establish a set.

#### Scenario: The set's view is used
- **WHEN** a booking is placed for a service naming set `remote`, and views for the placement message exist both in set `remote` and for every service
- **THEN** the booker's placement message carries the content of the view in set `remote`

#### Scenario: A message the set does not supply uses the view for every service
- **WHEN** set `remote` supplies only the confirmation message, and a view for the placement message exists for every service
- **THEN** the placement message for a booking of that service carries the content of the view for every service

#### Scenario: A message supplied nowhere uses the package's own content
- **WHEN** neither set `remote` nor the views for every service supply the cancellation message
- **THEN** the cancellation message for a booking of that service carries the package's own content

#### Scenario: A failing set view does not fall through to the view for every service
- **WHEN** the view for the placement message in set `remote` fails to render, and a view for the placement message exists for every service
- **THEN** the message is sent carrying the package's own content, not the content for every service, and the failure is reported

#### Scenario: The site's own recipients are covered by the set
- **WHEN** set `remote` supplies the placement message for the site's own recipients
- **THEN** that message carries the set's content, and the view was given the model that has no booker contact details

#### Scenario: A service with no set behaves as before
- **WHEN** a booking is placed for a service naming no set
- **THEN** each message uses the view for every service where one is supplied, and the package's own content otherwise, exactly as before this requirement

#### Scenario: A booking placed directly against a resource uses no set
- **WHEN** a message is composed for a booking placed directly against a resource
- **THEN** no set is consulted, and the message uses the view for every service or the package's own content

#### Scenario: A changed set reaches the next message
- **WHEN** a booking is placed for a service naming set `in-person`, the service's set is then changed to `remote`, and the booking is then confirmed
- **THEN** the confirmation message is looked up in set `remote`

#### Scenario: A deleted service uses no set
- **WHEN** a message is composed for a booking whose service has since been deleted
- **THEN** no set is consulted, and the message uses the view for every service or the package's own content

#### Scenario: An unreadable service gives the package's own content
- **WHEN** reading the service fails while a message is being composed, and a view for that message exists for every service
- **THEN** the message is sent carrying the package's own content, and the failure is logged without any booker detail

#### Scenario: An invalid set name on the model is never used as a path
- **WHEN** the renderer is given a model whose set name is `../partials`
- **THEN** no view is looked up under that name, the message carries the package's own content, and the failure is reported

#### Scenario: Two services share one set
- **WHEN** two services both name set `remote`, and a booking is placed for each
- **THEN** both bookings' messages are looked up in set `remote`

### Requirement: Both models identify the service and its template set

Both message models SHALL gain two members, on the terms the existing members are frozen by:

- **the service's id**, taken from what the booking recorded when it was placed. It SHALL be
  absent for a booking placed directly against a resource, and present for a service booking
  even where the service has since been deleted;
- **the template set**, as read from the service when the message is composed. It SHALL be absent
  where the service names no set, where it no longer exists, and for a booking placed directly
  against a resource.

**Frozen means additive.** Each member's absence SHALL mean exactly what is stated above. Every
existing member SHALL keep its name, type and meaning, and a view written before the members
existed SHALL render unchanged.

**The set name is the portable way for content to tell services apart.** A service's id differs
between a site's environments, because services are held in the package's own tables rather than
in content. A set name is whatever the site typed, and is the same wherever the site is
deployed. The documentation SHALL say so, and SHALL recommend the set name over the id for
branching inside a view.

**No resource ids are added.** The collection of resource names omits any resource that could not
be read, so a parallel collection of ids could not be relied on to line up with it. A site that
needs content specific to a resource books that resource through a service of its own.

#### Scenario: A service booking's view is given the service's id and set
- **WHEN** a view renders a message for a booking of a service naming set `remote`
- **THEN** the model carries the service's id and the set name `remote`

#### Scenario: The set member is absent where there is no set
- **WHEN** a view renders a message for a booking of a service naming no set
- **THEN** the model carries the service's id, the set member is absent, and a view written before these members existed renders exactly as before

#### Scenario: A direct booking carries neither
- **WHEN** a view renders a message for a booking placed directly against a resource
- **THEN** both the service id and the set members are absent

#### Scenario: A deleted service keeps its id and loses its set
- **WHEN** a view renders a message for a booking whose service has since been deleted
- **THEN** the model carries the service id the booking recorded, and the set member is absent

#### Scenario: The site's own model carries both too
- **WHEN** the model for the site's own recipients is inspected
- **THEN** it has the service id and the set members, and still has no member for the booker's contact details

### Requirement: The startup report covers template sets

The startup report SHALL also report, for **each template set named by a service**, which messages
that set supplies content for. It SHALL establish this the way sending will, by looking for each
message's view in the set.

**A set that supplies no message at all SHALL be reported as a warning**, naming the set and the
folder where its views were expected, and stating that set folders are lower case. A set named by
a service and supplying nothing is almost always a misspelt name or a folder whose case differs.
Without a warning it is indistinguishable from a set that is working, because every message
quietly falls back.

**The report SHALL NOT fail startup.** Where services cannot be read, for example because the
database is not yet available, the report SHALL say that sets could not be checked and SHALL
continue. The report for the views for every service SHALL be unaffected.

The report describes the services **as they stand at startup**. A set typed in the editor later is
reported at the next start, and the documentation SHALL say so.

**The report SHALL be taken only once the package's own storage is up to date for that start.** On
the first start after an upgrade that changes what is stored about services, a report taken before
the package's own migrations would read the old schema, fail, and say that sets could not be
checked, which is untrue and lands on exactly the start where somebody is reading the log. Where the
package's migrations fail, the report SHALL NOT be taken.

#### Scenario: A set's supplied messages are reported
- **WHEN** a service names set `remote` and the set supplies the placement and confirmation messages
- **THEN** the startup report names set `remote` with those two messages as supplied and the rest as not

#### Scenario: A set supplying nothing is a warning
- **WHEN** a service names set `remte` and no view exists in that set's folder
- **THEN** the startup report carries a warning naming `remte` and the folder its views were expected in

#### Scenario: A shared set is reported once
- **WHEN** two services name set `remote`
- **THEN** the startup report lists set `remote` once

#### Scenario: The report is taken after the package's storage is brought up to date
- **WHEN** a site starts with one of the package's migrations pending, and that migration adds what the report reads about services
- **THEN** the migration is applied first, the report is then taken against the migrated storage, and it reports no failure to read services

#### Scenario: Unreadable services do not fail startup
- **WHEN** reading services fails during the startup report
- **THEN** the site starts, the report says sets could not be checked, and the report for the views for every service is still given
