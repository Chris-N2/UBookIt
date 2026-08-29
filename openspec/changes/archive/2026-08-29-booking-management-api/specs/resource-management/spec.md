## MODIFIED Requirements

### Requirement: Management endpoints require backoffice authorization
Every uBookIt management endpoint SHALL require an authenticated backoffice user via an
Umbraco backoffice authorization policy applied to the shared controller base.
Unauthenticated requests SHALL receive 401; the endpoints SHALL NOT be reachable
anonymously under any configuration shipped by the package.

**The policy SHALL grant access on the basis of the package's own backoffice section**, not
of an unrelated one. Authorizing uBookIt's endpoints against another section is wrong in
both directions at once: a user granted uBookIt but not that section is refused an API for
a section they can see, and a user granted that section but not uBookIt can call every
uBookIt endpoint for a section they cannot. Neither is a configuration a site chose.

This matters more than tidiness because these endpoints return **personal data** — a
booking carries the booker's name and email — and an endpoint that inherits its
authorization from whichever policy was nearest to hand is how such data becomes reachable
by people the site never granted it to.

#### Scenario: Anonymous request is rejected
- **WHEN** any management endpoint is called without backoffice authentication
- **THEN** the response is 401 and no handler logic executes

#### Scenario: Access follows the package's own section
- **WHEN** an authenticated backoffice user without access to the package's section calls a management endpoint
- **THEN** the request is refused, whatever other sections they hold

#### Scenario: Access to the package's section is sufficient
- **WHEN** an authenticated backoffice user with access to the package's section calls a management endpoint
- **THEN** the request is authorized, without requiring access to any other section

### Requirement: HTTP callers cannot reach raw booking storage
Management controllers SHALL depend only on the management **ports** and validated Core
services. `IBookingStore` and `Booking.Rehydrate` SHALL NOT be referenced by any controller
or API-layer type. (Discharges the containment obligation recorded at the persistence
archive.)

The plural is deliberate and is the only thing that changed here: the package now has more
than one management port, and a controller reading bookings for the backoffice depends on
the booking management port exactly as the resource controllers depend on the resource one.
**The guarantee is unchanged** — a management port and a validated Core service are the
only routes to storage an API-layer type may take, and raw booking storage is reachable
through neither.

#### Scenario: API layer has no raw store references
- **WHEN** the management API layer's dependencies are inspected
- **THEN** no controller or API model references `IBookingStore` or `Booking.Rehydrate`

#### Scenario: A management port is the only storage route
- **WHEN** a management controller reads or writes stored data
- **THEN** it does so through a management port or a validated Core service, and never through a raw store
