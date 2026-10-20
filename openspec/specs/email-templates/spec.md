# email-templates

## Purpose

How a site replaces the **words** of the messages the package sends without taking over
**sending** them. Content is supplied as Razor views discovered by convention at a published
path, one per message — and, where a service names an email template set, optionally one per message within that set, consulted first — each independently optional and each discoverable from an assembly as
readily as from the site's own files. A message nobody supplies content for is composed exactly
as the package's own message for that event would be without this capability. The package publishes the complete set of message
names and a typed model per audience, passes that model to the view, and lets the view state the
message's subject and whether its content is HTML — through typed members, so a mistake is a
build failure rather than a surprise in somebody's inbox.

The separation is between **wording and policy**, and only the wording moves. Whether a message
is sent, to whom, under what configuration, and whether an erased booker is written to stay the
package's — supplying content buys none of them. The one guarantee this capability makes
*stronger* rather than narrower is the exclusion of a booker's contact details from messages to
a site's own recipients: the model such a view receives has no member carrying them, so the
promise holds however the content is written, instead of depending on an author's restraint.

The models are a vocabulary of facts about a booking rather than a convenience for the package's
own views, because the compatibility promise freezes them at the first full release and a later
editor-facing feature would expose exactly these values. So they carry structure where a view
might present it differently — the resources as a collection, the booking's interval as instants
already converted to the zone it was placed against — on the rule that **the package converts and
the view formats**: the zone rule is a guarantee the package already makes and getting it wrong
is a real defect, while presentation is the author's.

Stated as its own capability rather than inside `booking-emails` because it is a contract with an
author — a set of names, two model types under the compatibility promise, and a rendering path
that must work where there is no web request at all, because the first sender that runs from a
timer must not discover otherwise in production. It is also where the package draws the boundary
of what it will claim: where a site supplies the words, their accuracy is the site's, the same
reasoning the accessibility and theming narrowings record. The package makes a correct message
available by handing the view everything one needs; it cannot make it compulsory. What it does
not leave to chance is reporting — a view that fails to render is never mistaken for a view that
is absent, and at startup the package says which messages have content supplied and which do not,
so a view saved under the wrong name is found when the site starts rather than by a customer who
never received what the site intended.

## Requirements

### Requirement: A site can supply the content of a message without owning its delivery
The package SHALL allow a site to replace the **body** of any message it sends with content the
site supplies, while the package continues to decide **whether** to send, **to whom**, and
**under what configuration**.

**This is the gap the existing seam leaves.** A site can already intercept Umbraco's outgoing-mail
notification, but the message it receives there is an immutable copy and the only available action
is to take over sending entirely — so a site wanting different wording inherits delivery, retries,
the sending conjunction and the personal-data rules along with it. Content and delivery SHALL
therefore be separable.

**A site that supplies nothing SHALL be unaffected by this capability.** With no content supplied,
every message the package sends SHALL be exactly the package's own message for that event — the same
wording, the same recipients and the same conditions as if this capability did not exist. What the
package's own wording says is governed by the capabilities that define each message, and may grow
with them: a cancellation link, or a service's instructions for a confirmed booker, appears in the
package's own message because those capabilities say so, not because this one changed it.

#### Scenario: Content is replaced without delivery being replaced
- **WHEN** a site supplies content for a message and a booking event occurs that would send it
- **THEN** the message sent carries the site's content, and the package still decides whether to send it, to whom, and under the same configuration as before

#### Scenario: A site that supplies nothing is unchanged
- **WHEN** no content is supplied for any message
- **THEN** every message the package sends is identical to the package's own message for that event, as it would be if this capability did not exist

### Requirement: Content is supplied as Razor views at a published path, one per message
Site-supplied content SHALL be **Razor views**, discovered by convention at a **published
path**, one view per message the package sends. Where a service names an email template set, a
site MAY also supply one view per message **within that set**, at the published path for sets; the
order in which the two are consulted is stated in "A service's template set is consulted before
the content for every service". A view outside any set remains the content for every service.

The package SHALL publish the **complete set of message names**, so an author discovers what may
be supplied from the package rather than by reading its source. The set SHALL be exactly the
messages the package can send: for the booker, placement, confirmation, decline, cancellation
and a move; for the site's own recipients, placement and cancellation. Confirmation, decline
and a move SHALL NOT appear for the site's own recipients, because no such message is sent.

**The set grows additively.** A name, once published, is a file an author has on disk, and
SHALL NOT be renamed or removed; a message the package newly sends is a new name, and an author
who has supplied nothing for it gets the package's own content, exactly as for every other.

**Each view SHALL be independently optional.** Supplying one SHALL NOT require supplying any
other, and each message the site has not supplied SHALL use the package's own content.

**A view SHALL be discoverable from an assembly as well as from the site's own files**, so that
content can be distributed as a package rather than copied between sites.

#### Scenario: The set of message names is published
- **WHEN** an author asks what content may be supplied
- **THEN** the package names every message, and the model each receives, without the author reading the package's source

#### Scenario: The set names the booker's move
- **WHEN** the published set of message names is inspected
- **THEN** it contains an entry for a move addressed to the booker, and a site that has supplied nothing for it receives the package's own content for that message

#### Scenario: One supplied view replaces one message
- **WHEN** a site supplies a view for one message only
- **THEN** that message uses it and every other message uses the package's own content

#### Scenario: No view is offered for a message that is never sent
- **WHEN** the published set of message names is inspected
- **THEN** it contains no entry for a confirmation, a decline or a move addressed to the site's own recipients

#### Scenario: Content can be distributed in an assembly
- **WHEN** views are supplied by a referenced assembly rather than by files in the site
- **THEN** they are discovered and used on the same terms as files in the site

### Requirement: What a view receives is published, typed, and fit to be frozen
The package SHALL publish a **model type per audience** and SHALL pass an instance to the view.
These types are **public API and are frozen by the compatibility promise at the first full
release**, so they SHALL be named and shaped for a reader rather than for the convenience of the
package's own views.

**Frozen means additive.** The booker's model gains, for the move message, the interval the
booking held before it moved; every existing member keeps its name, type and meaning. The new
member SHALL be absent for every message that is not a move, and its absence SHALL mean exactly
that — this message is not about a move — rather than "not recorded". A view written before the
member existed renders unchanged.

**The booker's model likewise gains the cancellation link**, on the same terms. It SHALL be absent
wherever no link has been issued — which is every message on a site with the feature off, and every
message about a booking that can no longer be cancelled this way — and its absence SHALL mean
exactly that: **there is no self-service cancellation for this booking**, rather than that a link
was expected and could not be built. A view written before the member existed renders unchanged.

**The link SHALL be supplied as a complete address the view can render without assembling it.**
Unlike an instant, a link has no presentational choice worth leaving to an author: a view that had
to build one from parts could build a wrong one, and a wrong cancellation link is indistinguishable
from a working one until a customer needs it.

**A model SHALL expose structure rather than pre-composed text** wherever a view might reasonably
present it differently. Specifically: what was booked SHALL be available as the service's recorded
name and as the **collection** of resource names, not as a single joined string; and the booking's
start and end SHALL be available as instants **already converted to the time zone the booking was
placed against**, together with that zone's id, rather than as formatted strings. The previous
interval, where present, SHALL be held to the same terms: instants already converted to the
booking's zone, never formatted strings.

**The package converts, the view formats.** Converting is the package's because the zone rule is a
guarantee it already makes and getting it wrong is a real defect; formatting is the view's because
presentation is the author's. *The cancellation link is the stated exception to the second half:
there is nothing to format, only an address to render or omit.*

**The models SHALL read as a vocabulary of the facts about a booking**, because a later
editor-facing feature would expose exactly these as the values an editor may reference. Nothing in
this capability SHALL assume a supplied view is the only possible source of content.

#### Scenario: Resources are a collection, not a sentence
- **WHEN** a view renders a booking whose service resolved to more than one resource
- **THEN** it can enumerate the resources individually, without parsing a joined string

#### Scenario: Times arrive in the booking's own zone
- **WHEN** a view renders a booking placed against one time zone while the site is configured with another
- **THEN** the instants it receives are expressed in the zone the booking was placed against, and the zone's id is available to state alongside them

#### Scenario: The previous interval arrives in the booking's own zone
- **WHEN** a view renders the move message
- **THEN** the interval the booking held before the move is available as instants in the booking's zone, alongside the interval it now holds

#### Scenario: The previous interval is absent for every other message
- **WHEN** a view renders a placement, confirmation, decline or cancellation message
- **THEN** the previous-interval member is absent, and a view written before it existed renders exactly as before

#### Scenario: The view chooses the format
- **WHEN** two views render the same booking with different date formatting
- **THEN** both are possible without the package changing, because what is supplied is an instant rather than a formatted string

#### Scenario: The cancellation link arrives ready to render
- **WHEN** a view renders a message for a booking that has been issued a cancellation link
- **THEN** the link is available as a complete address, with nothing for the view to assemble

#### Scenario: The link is absent where there is none
- **WHEN** a view renders a message for a booking with no self-service cancellation
- **THEN** the cancellation-link member is absent, and a view written before it existed renders exactly as before

### Requirement: The model for the site's own recipients cannot express a booker's contact details
The model passed to a view for the **site's own recipients** SHALL have **no member carrying the
booker's name, email address or telephone number**.

**This makes an existing guarantee structural instead of advisory.** The package already promises
that a message to the site's own recipients carries no booker contact details, because such a
list is not the population the **Sensitive data** control governs. If one model served both
audiences with the booker merely absent or null, that promise would silently weaken from *what
the package does* to *what the package does unless a site writes a view* — and nothing would
report the difference.

A site that genuinely wants contact details in its internal mail SHALL still be able to send such
a message **itself**, through the booking notifications. What it SHALL NOT be able to do is have
the package send one.

#### Scenario: The internal model offers nothing to leak
- **WHEN** the model for the site's own recipients is inspected
- **THEN** it exposes no booker name, no email address and no telephone number

#### Scenario: A view for the site's own recipients cannot render contact details
- **WHEN** an author attempts to render a booker's contact details in a view for the site's own recipients
- **THEN** there is no member to render, and the attempt fails before the view can be used rather than at a recipient's inbox

#### Scenario: The booker's own message is unaffected
- **WHEN** a view for the booker renders
- **THEN** the booker's own contact details are available to it, as they are in the package's own content

### Requirement: A view states its own subject and content type, through typed members
A view SHALL be able to state the message's **subject** and whether its content is **HTML**, and
SHALL do so through **typed members supplied by the package** rather than by writing into an
untyped dictionary, so that a mistake is reported when the view is built rather than ignored when
it runs.

**Where a view states no subject, the package's own subject SHALL be used**, so that supplying
content is not the same as taking responsibility for everything about the message.

**Content SHALL be treated as plain text unless the view says otherwise, and the package SHALL NOT
infer it.** The package's own content is plain text, so that is the default an author inherits;
an author writing HTML declares it. Inspecting content to guess at its type SHALL NOT be done —
a guess that is wrong is wrong silently, and the failure appears in somebody's inbox.

#### Scenario: A view sets its own subject
- **WHEN** a view states a subject
- **THEN** the message is sent with it

#### Scenario: A view that states no subject keeps the package's
- **WHEN** a view states no subject
- **THEN** the message is sent with the subject the package would have used

#### Scenario: Plain text is the default
- **WHEN** a view states nothing about its content type
- **THEN** the message is sent as plain text

#### Scenario: Content type is never inferred
- **WHEN** a view emits content containing markup but states nothing about its content type
- **THEN** the message is still sent as plain text, rather than being reclassified by inspection

#### Scenario: A mistyped member does not silently do nothing
- **WHEN** an author misspells the member that states the subject
- **THEN** the view fails to build, rather than building and sending with a subject the author did not intend

### Requirement: Content renders without a request, and a failure to render is never silent
Rendering SHALL NOT require an ambient web request. Messages are sent from work that has no
request — an unattended sweep already runs that way — so a mechanism that worked only inside a
request would fail exactly where it is least observable.

**A view that fails to render SHALL NOT be treated as a view that is absent.** Absence has a
defined meaning — use the package's own content — and silently applying it to a broken view would
turn an author's mistake into a message nobody can account for. A failure SHALL be reported, and
SHALL be distinguishable in the report from a message for which nothing was supplied.

**A failure to render SHALL NOT prevent the message being sent.** The package's own content SHALL
be used, on the same terms as everywhere else in this package: a fault in content must not become
a booker's problem, and a booking already placed must not produce silence.

#### Scenario: A message sent from unattended work still renders
- **WHEN** a message is composed from work that has no ambient web request
- **THEN** the supplied content renders exactly as it does within a request

#### Scenario: A broken view is reported, not silently ignored
- **WHEN** a supplied view fails to render
- **THEN** the failure is reported, distinguishably from a message for which no view was supplied

#### Scenario: A broken view does not cost the message
- **WHEN** a supplied view fails to render
- **THEN** the message is still sent, carrying the package's own content

### Requirement: What was supplied is reported at startup
At startup the package SHALL report **which messages have content supplied and which do not**, so
that a misnamed or misplaced view is discovered when a site starts rather than when a customer
does not receive what the site intended.

**This changes the silence, not the fallback.** Falling back per message is the behaviour this
capability requires anyway; without a report, a view saved under the wrong name is
indistinguishable from a deliberate decision to leave that message alone. The report is where that
mistake is meant to be caught.

**The report SHALL be of the outcome, not of the mechanism** — what the package will actually use
for each message, established the way it will establish it when sending, rather than a restatement
of what was registered.

#### Scenario: A misnamed view is visible at startup
- **WHEN** a site supplies a view under a name that is not one of the published message names
- **THEN** the startup report shows that message as having no content supplied

#### Scenario: The report distinguishes supplied from not
- **WHEN** a site supplies content for some messages and not others
- **THEN** the startup report names which are which

### Requirement: The limits of the medium are stated rather than discovered
The package SHALL state, in its documentation, that a message carries **either** HTML **or**
plain text and never both, and that a site supplying HTML content therefore sends **no plain-text
alternative**.

**This is a limit of the mail abstraction the package sends through, not a decision this
capability makes.** The abstraction accepts a single body and a flag saying what it is. Producing
a multipart message would mean bypassing the site's own mail configuration, which would cost the
site its transport and its ability to intercept what the package sends — a worse trade than the
limitation.

Stating it is the point: an author who chooses HTML is choosing this, and the alternative to
saying so is a site discovering it from recipients who read mail as text.

#### Scenario: The limitation is documented
- **WHEN** an author reads how to supply HTML content
- **THEN** the documentation states that no plain-text alternative accompanies it, and why the package does not produce one

### Requirement: The booker's model carries the service's instructions on the same terms as the message

The booker's model SHALL gain the service's instructions for the booker, as text, on exactly the
terms the `booking-emails` capability sets for the package's own wording. The model SHALL carry
the instructions where the booking is confirmed when the message is composed and its service has
them, and SHALL NOT carry them otherwise.

**Frozen means additive.** The member SHALL be absent wherever the message does not carry the
instructions, and its absence SHALL mean exactly that. Every existing member SHALL keep its name,
type and meaning, and a view written before the member existed SHALL render unchanged.

The instructions SHALL be supplied as the text the site wrote, already normalised, with line breaks
preserved. They SHALL NOT be pre-formatted for a medium. Presenting them is the view's, and Razor
encodes them in an HTML view like any other value.

The model for the site's own recipients SHALL NOT gain the instructions.

#### Scenario: A view is given the instructions for a confirmed booking
- **WHEN** a view renders the booker's message for a confirmed booking whose service has instructions
- **THEN** the instructions are available on the model as the text the site wrote, line breaks included

#### Scenario: The member is absent where the message carries none
- **WHEN** a view renders the booker's message for a booking awaiting approval, a declined or cancelled booking, or a booking whose service has no instructions
- **THEN** the instructions member is absent, and a view written before it existed renders exactly as before

#### Scenario: The internal model does not gain them
- **WHEN** the model for the site's own recipients is inspected
- **THEN** it has no member for the instructions

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
