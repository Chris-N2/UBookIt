<!--
No wholesale replacement in this file: one requirement is ADDED. It exists because QA round 1
moved a booking placed for a 45–120 minute service to 30 minutes through the booking service's
move, which knows resources and nothing about services — falsifying "Service duration narrows
each candidate independently" for moved bookings. The proposal's "Deliberately unmodified:
service-booking" was wrong and is corrected.
-->

## ADDED Requirements

### Requirement: Moving a booking placed for a service applies the service's length rules
The service booking service SHALL expose a move that is the operator's single entry point for
moving any booking. For a booking placed for a service, the new length SHALL be validated
against the **intersection of the service's duration specification with each claimed
resource's range** — the highest floor and the lowest ceiling across the booking's own claims,
each bound moved inward to that resource's granularity — exactly as service placement
validates a requested length, and with the same stable codes: `duration-too-short` and
`duration-too-long`. Everything else about the move SHALL be delegated to the booking
service's move unchanged.

**The claims are the booking's own, not a candidate pool.** A move keeps every claim, so the
bounds are computed over the resources the booking holds rather than over what the service
could resolve to today.

**A booking whose recorded service no longer exists SHALL be moved on the resources' rules
alone.** The attribution is a snapshot; a specification that has been deleted cannot bind
anything. A claimed resource that can no longer provide the service at any length — the
intersection is empty — SHALL refuse the move with `service-unavailable`.

**A booking placed directly SHALL be delegated straight to the booking service's move.** The
service booking service adds nothing for it and refuses nothing.

**BREAKING — published port, declared.** `IServiceBookingService` gains the move member. A host
implementing it must add one; it lands in a minor release, as the observer and store additions
do.

#### Scenario: A service booking cannot be moved to a length the service forbids
- **WHEN** a booking placed for a service permitting 45–120 minutes is moved to a length of 30 minutes
- **THEN** it fails with `duration-too-short`, and the booking is unchanged

#### Scenario: A service booking cannot be moved to a length a claimed resource forbids
- **WHEN** a booking placed for a service permitting up to 240 minutes, on a resource whose maximum is 90, is moved to 120 minutes
- **THEN** it fails with `duration-too-long`, and the booking is unchanged

#### Scenario: A service booking moves to a length both permit
- **WHEN** a booking placed for a service permitting 45–120 minutes is moved to 90 minutes at a free interval
- **THEN** it succeeds on the booking service's terms, with the same claims

#### Scenario: A deleted service no longer binds a move
- **WHEN** a booking placed for a service that has since been deleted is moved to a length the resources accept
- **THEN** it succeeds on the resources' rules alone

#### Scenario: A direct booking is delegated untouched
- **WHEN** a booking placed directly is moved through the service booking service
- **THEN** the outcome is exactly the booking service's, for the same arguments
