# email-templates — new capability

## ADDED Requirements

### Requirement: A site can supply the content of a message without owning its delivery
The package SHALL allow a site to replace the **body** of any message it sends with content the
site supplies, while the package continues to decide **whether** to send, **to whom**, and
**under what configuration**.

**This is the gap the existing seam leaves.** A site can already intercept Umbraco's outgoing-mail
notification, but the message it receives there is an immutable copy and the only available action
is to take over sending entirely — so a site wanting different wording inherits delivery, retries,
the sending conjunction and the personal-data rules along with it. Content and delivery SHALL
therefore be separable.

**A site that supplies nothing SHALL be unaffected.** With no content supplied, every message the
package sends SHALL be exactly what it sent before this capability existed — same wording, same
recipients, same conditions.

#### Scenario: Content is replaced without delivery being replaced
- **WHEN** a site supplies content for a message and a booking event occurs that would send it
- **THEN** the message sent carries the site's content, and the package still decides whether to send it, to whom, and under the same configuration as before

#### Scenario: A site that supplies nothing is unchanged
- **WHEN** no content is supplied for any message
- **THEN** every message the package sends is identical to what it sent before this capability existed

### Requirement: Content is supplied as Razor views at a published path, one per message
Site-supplied content SHALL be **Razor views**, discovered by convention at a **published
path**, one view per message the package sends.

The package SHALL publish the **complete set of message names**, so an author discovers what may
be supplied from the package rather than by reading its source. The set SHALL be exactly the
messages the package can send: for the booker, placement, confirmation, decline and cancellation;
for the site's own recipients, placement and cancellation. Confirmation and decline SHALL NOT
appear for the site's own recipients, because no such message is sent.

**Each view SHALL be independently optional.** Supplying one SHALL NOT require supplying any
other, and each message the site has not supplied SHALL use the package's own content.

**A view SHALL be discoverable from an assembly as well as from the site's own files**, so that
content can be distributed as a package rather than copied between sites.

#### Scenario: The set of message names is published
- **WHEN** an author asks what content may be supplied
- **THEN** the package names every message, and the model each receives, without the author reading the package's source

#### Scenario: One supplied view replaces one message
- **WHEN** a site supplies a view for one message only
- **THEN** that message uses it and every other message uses the package's own content

#### Scenario: No view is offered for a message that is never sent
- **WHEN** the published set of message names is inspected
- **THEN** it contains no entry for a confirmation or a decline addressed to the site's own recipients

#### Scenario: Content can be distributed in an assembly
- **WHEN** views are supplied by a referenced assembly rather than by files in the site
- **THEN** they are discovered and used on the same terms as files in the site

### Requirement: What a view receives is published, typed, and fit to be frozen
The package SHALL publish a **model type per audience** and SHALL pass an instance to the view.
These types are **public API and are frozen by the compatibility promise at the first full
release**, so they SHALL be named and shaped for a reader rather than for the convenience of the
package's own views.

**A model SHALL expose structure rather than pre-composed text** wherever a view might reasonably
present it differently. Specifically: what was booked SHALL be available as the service's recorded
name and as the **collection** of resource names, not as a single joined string; and the booking's
start and end SHALL be available as instants **already converted to the time zone the booking was
placed against**, together with that zone's id, rather than as formatted strings.

**The package converts, the view formats.** Converting is the package's because the zone rule is a
guarantee it already makes and getting it wrong is a real defect; formatting is the view's because
presentation is the author's.

**The models SHALL read as a vocabulary of the facts about a booking**, because a later
editor-facing feature would expose exactly these as the values an editor may reference. Nothing in
this capability SHALL assume a supplied view is the only possible source of content.

#### Scenario: Resources are a collection, not a sentence
- **WHEN** a view renders a booking whose service resolved to more than one resource
- **THEN** it can enumerate the resources individually, without parsing a joined string

#### Scenario: Times arrive in the booking's own zone
- **WHEN** a view renders a booking placed against one time zone while the site is configured with another
- **THEN** the instants it receives are expressed in the zone the booking was placed against, and the zone's id is available to state alongside them

#### Scenario: The view chooses the format
- **WHEN** two views render the same booking with different date formatting
- **THEN** both are possible without the package changing, because what is supplied is an instant rather than a formatted string

### Requirement: The model for the site's own recipients cannot express a booker's contact details
The model passed to a view for the **site's own recipients** SHALL have **no member carrying the
booker's name, email address or telephone number**.

**This makes an existing guarantee structural instead of advisory.** The package already promises
that a message to a configured recipient list carries no booker contact details, because such a
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
