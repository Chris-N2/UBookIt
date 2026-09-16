<!--
GUARANTEE DIFF for the two wholesale replacements below.

"Content is supplied as Razor views at a published path, one per message" — 4 SHALL blocks,
4 scenarios:
  SHALL 1  Razor views, published path, one per message             → CARRIED unchanged
  SHALL 2  complete set published; exactly what the package sends;  → CARRIED, the set amended
           no confirmation/decline for the site's recipients          to add the booker's move and
                                                                      to state that no move is
                                                                      offered for the site's own
                                                                      recipients either
  SHALL 3  each view independently optional                         → CARRIED unchanged
  SHALL 4  discoverable from an assembly                            → CARRIED unchanged
  Scenarios 1-4 ALL CARRIED; scenario 3 amended to name the move alongside confirmation and
  decline. One ADDED: the set names the booker's move.

"What a view receives is published, typed, and fit to be frozen" — 4 SHALL blocks, 3 scenarios:
  SHALL 1  model type per audience; public, frozen at 17.0.0        → CARRIED, with the statement
                                                                      that the booker model grows
                                                                      ADDITIVELY and how absence
                                                                      of the new member reads
  SHALL 2  structure not text; resources as collection; instants    → CARRIED, the previous
           in the booking's zone                                      interval held to the same
                                                                      terms
  SHALL 3  the package converts, the view formats                   → CARRIED unchanged
  SHALL 4  models read as a vocabulary of facts                     → CARRIED unchanged
  Scenarios 1-3 ALL CARRIED verbatim. Two ADDED: the previous interval arrives in the booking's
  zone; it is absent for every message but the move.

  DELIBERATE DROPS: none.
-->

## MODIFIED Requirements

### Requirement: Content is supplied as Razor views at a published path, one per message
Site-supplied content SHALL be **Razor views**, discovered by convention at a **published
path**, one view per message the package sends.

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

**A model SHALL expose structure rather than pre-composed text** wherever a view might reasonably
present it differently. Specifically: what was booked SHALL be available as the service's recorded
name and as the **collection** of resource names, not as a single joined string; and the booking's
start and end SHALL be available as instants **already converted to the time zone the booking was
placed against**, together with that zone's id, rather than as formatted strings. The previous
interval, where present, SHALL be held to the same terms: instants already converted to the
booking's zone, never formatted strings.

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

#### Scenario: The previous interval arrives in the booking's own zone
- **WHEN** a view renders the move message
- **THEN** the interval the booking held before the move is available as instants in the booking's zone, alongside the interval it now holds

#### Scenario: The previous interval is absent for every other message
- **WHEN** a view renders a placement, confirmation, decline or cancellation message
- **THEN** the previous-interval member is absent, and a view written before it existed renders exactly as before

#### Scenario: The view chooses the format
- **WHEN** two views render the same booking with different date formatting
- **THEN** both are possible without the package changing, because what is supplied is an instant rather than a formatted string
