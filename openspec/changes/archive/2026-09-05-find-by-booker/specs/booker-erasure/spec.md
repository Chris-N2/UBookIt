## MODIFIED Requirements

### Requirement: What erasure does not reach is documented

The package's documentation SHALL state what erasure does, that it cannot be undone, who may
perform it, and **the two boundaries of what it achieves**:

- **It erases one booking.** A person who booked more than once has one booking erased per
  operation; erasing everything they booked means erasing each. The package **does** provide a
  way to find them — a search by the booker's email address, per `booking-management` — so the
  documentation SHALL direct an operator to search first and erase each result, and SHALL state
  that the search finds bookings made with **that** address: somebody who booked under two
  addresses has one set found.
- **It can be performed on a booking that has not yet happened**, and doing so leaves the site
  unable to contact somebody who is going to arrive. The package SHALL NOT refuse this — whether
  the basis for holding the details still applies is the site's judgement and not the package's
  — but the documentation SHALL state the consequence plainly.

Left unstated, the first arrives as a data-protection failure and the second as a support call
about a booking nobody can ring.

#### Scenario: The limits of one erasure are documented
- **WHEN** a reader consults the backoffice documentation
- **THEN** it states that erasure applies to one booking, that a person's other bookings are unaffected by it, and how to find them

#### Scenario: Erasing a future booking is permitted and its cost is documented
- **WHEN** an operator erases a booking whose interval has not yet started
- **THEN** the operation succeeds, and the documentation states that the site will no longer be able to contact that booker

*The sentence "The package does not claim to find them all" was true when erasure shipped without
a way to find anything, and this change makes it false — the boundary it described has moved
rather than disappeared. What survives is that erasure still reaches ONE booking per operation
and that a search matches ONE address, so a person with two addresses is still under-reported.
Corrected here rather than left, because a capability documenting a limit the package no longer
has teaches an operator to do more work than they need and to distrust a feature that works.*
