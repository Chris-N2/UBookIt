## MODIFIED Requirements

### Requirement: Bookings can be enumerated for management
The package SHALL provide a read port that **lists** bookings, distinct from the
availability reads that serve the front end. It SHALL be a Core port with a persistence
implementation, on the same terms as the existing resource and service management stores,
so that an alternative implementation is possible and has a stated contract to satisfy.

The port SHALL return, for each booking, everything a management list row displays
**without a further read per booking**: the booking's identity, its interval and the time
zone it was made in, its status, when it was created, the booker's name and email, each
claimed resource's id **and name**, and **the service it was placed for — id and name —
or nothing, for a booking placed directly**.

Resource names are part of the guarantee rather than a convenience. A claim records only a
resource id, so a caller given ids alone must perform a lookup per claim, which is the
cost this port exists to avoid. **The service name is carried for the same reason and one
more**: it is the name recorded at placement time, so it remains answerable for a service
that has since been renamed or deleted, which a read-time join could not do.

#### Scenario: A booking is listed with everything a row shows
- **WHEN** bookings are listed
- **THEN** each result carries its interval, time zone, status, creation time, booker name and email, every claimed resource as an id and a name, and its service attribution as an id and a name where it has one

#### Scenario: Resource names come back with the list
- **WHEN** a listed booking claims a resource
- **THEN** that resource's name is present in the result, without the caller reading the resource separately

#### Scenario: The service comes back with the list
- **WHEN** a listed booking was placed through a service
- **THEN** that service's id and its name as recorded at placement are present in the result, without the caller reading the service separately

#### Scenario: A directly placed booking reports no service
- **WHEN** a listed booking was placed directly
- **THEN** it carries no service attribution, distinguishable from a service whose name is empty

#### Scenario: The front-end reads are unaffected
- **WHEN** this port is added
- **THEN** the availability and placement reads behave exactly as before, and no booking is placed, cancelled or altered by any operation in this capability

### Requirement: Bookings are readable over an authorized management endpoint
The package SHALL expose a versioned backoffice endpoint, in the same swagger group as its
other management endpoints, returning the windowed, paged, filtered list of bookings the
management read port provides.

The endpoint SHALL require backoffice authorization on the same terms as every other
uBookIt management endpoint. It SHALL NOT be reachable anonymously under any configuration
the package ships.

Request and response bodies SHALL be purpose-built models. **Domain types SHALL NOT appear
in the HTTP contract**, on the same terms as the resource and service endpoints.

The response SHALL carry, per booking, exactly what the read port supplies: the booking's
identity, its interval and the time zone it was made in, its status, when it was created,
the booker's name and email, each claimed resource's id and name, and **the service it was
placed for, or null**. It SHALL report the unpaged total alongside the page, so a caller
can render a pager.

**The service SHALL cross the boundary as a single nullable object carrying both id and
name**, rather than as two parallel nullable fields. "A booking has a service, or it does
not" is then expressed in the shape, rather than as a rule that two fields must be null
together — which a client can observe violated and has no way to interpret.

**No field SHALL be added at the HTTP layer that the read port cannot supply.** A field the
port does not carry is one the screen must obtain another way, which is how a second read
path into bookings begins.

#### Scenario: A page of bookings is returned with its total
- **WHEN** an authorized caller requests bookings for a window
- **THEN** the matching page is returned with the unpaged total, each booking carrying its interval, time zone, status, creation time, booker name and email, its resources with their names, and its service where it has one

#### Scenario: A directly placed booking carries a null service
- **WHEN** a returned booking was placed directly
- **THEN** its service member is null, rather than an object with empty or placeholder values

#### Scenario: The endpoint is not anonymous
- **WHEN** the endpoint is called without backoffice authentication
- **THEN** the response is 401 and no handler logic executes

#### Scenario: No domain type crosses the HTTP boundary
- **WHEN** the endpoint's request and response models are inspected
- **THEN** they are purpose-built models, and no domain or store type appears in the contract
