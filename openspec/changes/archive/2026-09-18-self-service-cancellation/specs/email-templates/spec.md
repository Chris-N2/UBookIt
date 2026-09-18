## MODIFIED Requirements

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
