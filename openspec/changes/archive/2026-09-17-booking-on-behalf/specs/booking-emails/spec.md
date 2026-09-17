## MODIFIED Requirements

### Requirement: Which events produce messages, and for whom
Five booking events SHALL be able to produce messages: placement, confirmation, decline,
cancellation, and a move. **A visitor's** placement, and cancellation, SHALL address both
directions — the booker and the site's own recipients — as they always have. **Confirmation,
decline, a move, and an operator's placement on a booker's behalf SHALL address the booker
only**: the site's own people, or a colleague, performed the action, and the
bookings screen is where its state lives; a message telling the site what it just did would
be noise that trains recipients to skim.

**Placement is therefore the one event whose recipients depend on who placed it**, and that
SHALL be derived from the placement itself rather than from any setting or from the booking's
status. A booking an operator took at the desk and a booking a visitor placed are the same kind
of thing afterwards — *Placing a booking on a booker's behalf* requires that they carry no
marker distinguishing them — so the decision belongs to the act of placing and cannot be
recovered from the row later.

*The case that would move an operator's placement back to both directions is recorded here for
the same reason it is recorded for a move, and it is the same case: a responsible party for a
resource did not take the booking, and would reasonably want to know their diary has gained one.
It bites harder here than for a move, because a placement adds a commitment rather than shifting
one that was already theirs to see. It is nevertheless decided the same way, because the
alternative tells every internal recipient about an action one of their colleagues performed
deliberately, seconds earlier, on the screen that already lists it. Whichever change gives
responsible parties a diary of their own decides this again.*

*A move is placed on that side deliberately, and the case that would move it is recorded so the
decision is revisited rather than rediscovered: a responsible party for a resource did not
perform an operator's move of a booking on it, and would reasonably want to know their diary
changed. That case bites hardest when a move changes* which *resource is claimed, and a move
here changes only when. Whichever change lets a booking change resource decides this again.*

**The message a move sends SHALL say where the booking moved from as well as where it now is**,
both expressed in the booking's own zone. A customer holding an old confirmation needs to be
told which of the two times in their inbox is the real one, and a message stating only the new
time leaves them to work that out.

Every message SHALL remain subject to the existing gating without exception: the direction
asked for AND the host able to send. A confirmation, decline or move on a site that
has not enabled writing to the booker SHALL send nothing at all.

**A booking placed under auto-confirm SHALL produce one message to the booker, not two.**
Auto-confirmation is not an event; it is what placement produced, and the placement message
already says so.

**An operator's placement SHALL likewise produce one message to the booker, not two**, and for
the same reason: it is confirmed because of what placing it meant, not because anybody
subsequently confirmed it, and no confirmation event has occurred.

A confirmation, decline or move of a booking whose booker has been erased SHALL send nothing to
anyone — there is no address, and no internal message is due for these events.

#### Scenario: A confirmation is told to the booker only
- **WHEN** an operator confirms a requested booking, with both directions enabled
- **THEN** the booker receives a message whose wording derives from the booking's confirmed state, and the site's own recipients receive nothing

#### Scenario: A decline is told to the booker only
- **WHEN** an operator declines a requested booking, with both directions enabled
- **THEN** the booker receives a message whose wording derives from the booking's declined state, and the site's own recipients receive nothing

#### Scenario: A move is told to the booker only, and says where from
- **WHEN** an operator moves a booking, with both directions enabled and a responsibility assignment on a claimed resource
- **THEN** the booker receives a message carrying the reference, the previous interval and the new interval in the booking's own zone, and neither the site's own recipients nor the responsible party receives anything

#### Scenario: A decline on a site that has not enabled booker emails is silent
- **WHEN** an operator declines a requested booking on a site that has configured recipients but not enabled writing to the booker
- **THEN** no message is sent to anyone

#### Scenario: A move on a site that has not enabled booker emails is silent
- **WHEN** an operator moves a booking on a site that has configured recipients but not enabled writing to the booker
- **THEN** no message is sent to anyone

#### Scenario: Auto-confirmed placement sends one booker message
- **WHEN** a booking is placed while `AutoConfirm` is on, with booker emails enabled
- **THEN** the booker receives exactly one message, and its wording derives from the booking's confirmed state

#### Scenario: Confirming a booking whose booker was erased sends nothing
- **WHEN** a requested booking whose booker has been erased is confirmed or declined, with both directions enabled
- **THEN** no message is sent to anyone

#### Scenario: Moving a booking whose booker was erased sends nothing
- **WHEN** a booking whose booker has been erased is moved, with both directions enabled
- **THEN** the move succeeds and no message is sent to anyone

#### Scenario: An operator's placement is told to the booker only
- **WHEN** an operator places a booking on a booker's behalf, with both directions enabled
- **THEN** the booker receives the placement message carrying the reference, and the site's own recipients receive nothing

#### Scenario: A visitor's placement still tells both
- **WHEN** a visitor places a booking, with both directions enabled
- **THEN** the booker receives the placement message and the site's own recipients receive theirs, exactly as before

#### Scenario: An operator's placement sends the booker one message
- **WHEN** an operator places a booking on a booker's behalf, with booker emails enabled
- **THEN** the booker receives exactly one message, and its wording derives from the booking's confirmed state

#### Scenario: An operator's placement on a site without booker emails is silent
- **WHEN** an operator places a booking on a booker's behalf on a site that has configured recipients but not enabled writing to the booker
- **THEN** no message is sent to anyone, and the placement still succeeds
