<!--
GUARANTEE DIFF for the one wholesale replacement below.

"Which events produce messages, and for whom" — 5 SHALL blocks, 5 scenarios:
  SHALL 1  four events; placement + cancellation both directions;   → CARRIED, amended to five;
           confirmation + decline booker only, with the reasoning     move joins the booker-only
                                                                      side, with the same reasoning
  SHALL 2  every message subject to the existing gating             → CARRIED unchanged
  SHALL 3  auto-confirmed placement is one message, not two         → CARRIED unchanged
  SHALL 4  confirmation/decline of an erased booker sends nothing   → CARRIED, amended to include
                                                                      a move of an erased booker
  Scenarios 1-5 ALL CARRIED verbatim. Three ADDED: a move is told to the booker only and says
  where from; a move on a site without booker emails is silent; a move of an erased booker
  sends nothing.

  DELIBERATE DROPS: none.

"What a message tells the booker" is NOT modified: the moved message carries the reference,
when the booking is, and what was booked, exactly as that requirement demands, and adds the
previous interval on top. Nothing there is falsified.
-->

## MODIFIED Requirements

### Requirement: Which events produce messages, and for whom
Five booking events SHALL be able to produce messages: placement, confirmation, decline,
cancellation, and a move. Placement and cancellation SHALL address both directions — the booker
and the site's own recipients — as they always have. **Confirmation, decline and a move SHALL
address the booker only**: the site's own people, or a colleague, performed the action, and the
bookings screen is where its state lives; a message telling the site what it just did would
be noise that trains recipients to skim.

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
