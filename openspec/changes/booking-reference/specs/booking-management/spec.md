## MODIFIED Requirements

### Requirement: Bookings can be enumerated for management
The package SHALL provide a read port that **lists** bookings, distinct from the
availability reads that serve the front end. It SHALL be a Core port with a persistence
implementation, on the same terms as the existing resource and service management stores,
so that an alternative implementation is possible and has a stated contract to satisfy.

The port SHALL return, for each booking, everything a management list row displays
**without a further read per booking**: the booking's identity, **its quotable reference**,
its interval and the time zone it was made in, its status, when it was created, the booker's
name and email, each claimed resource's id **and name**, and **the service it was placed for —
id and name — or nothing, for a booking placed directly**.

Resource names are part of the guarantee rather than a convenience. A claim records only a
resource id, so a caller given ids alone must perform a lookup per claim, which is the
cost this port exists to avoid. **The service name is carried for the same reason and one
more**: it is the name recorded at placement time, so it remains answerable for a service
that has since been renamed or deleted, which a read-time join could not do.

**The reference is carried for a third reason: somebody is on the telephone.** The case this
port exists to serve is an operator finding the booking a caller is describing, and the caller
has a reference in front of them and not a machine identifier. A row that cannot be matched
against what the customer is reading out fails at the moment it is most needed.

**This port SHALL remain read-only.** It lists; it does not change what it lists. That is
what makes it substitutable and what keeps a caller's reads free of side effects — the
cancel endpoint in this same capability reaches the domain, never this port.

#### Scenario: A booking is listed with everything a row shows
- **WHEN** bookings are listed
- **THEN** each result carries its reference, interval, time zone, status, creation time, booker name and email, every claimed resource as an id and a name, and its service attribution as an id and a name where it has one

#### Scenario: Resource names come back with the list
- **WHEN** a listed booking claims a resource
- **THEN** that resource's name is present in the result, without the caller reading the resource separately

#### Scenario: The service comes back with the list
- **WHEN** a listed booking was placed through a service
- **THEN** that service's id and its name as recorded at placement are present in the result, without the caller reading the service separately

#### Scenario: A directly placed booking reports no service
- **WHEN** a listed booking was placed directly
- **THEN** it carries no service attribution, distinguishable from a service whose name is empty

#### Scenario: An operator can match what a caller reads out
- **WHEN** a listed booking is displayed to an operator
- **THEN** its quotable reference is among what is shown, so a booking can be identified from what the customer has in front of them

#### Scenario: The front-end reads are unaffected
- **WHEN** this port is added
- **THEN** the availability and placement reads behave exactly as before, and no booking is placed, cancelled or altered **by this port**

*The scenario above said "by any operation in this capability" until cancellation joined it.
The narrowing is to what the scenario was always guarding — that adding a read port changes
nothing — and the guarantee it protected is now carried explicitly by the read-only SHALL
above, which is stronger than an aside in a scenario's THEN.*
