## MODIFIED Requirements

### Requirement: Status and resource filter the list, and the status default excludes what is not booked
A list query SHALL be filterable by **status** and by **resource**. With the window, those
are the only filters this capability offers; every other dimension is either impossible
from the stored data or deferred until something renders it.

The resource filter SHALL accept a **set** of resource ids and SHALL match a booking that
claims **any** of them. A set rather than a single id is a compatibility decision: the
published query type would have to change incompatibly to widen one into the other later,
and a set costs nothing now while behaving identically when given one id. An empty or
absent set SHALL mean no resource filter rather than matching nothing.

A booking claiming more than one resource SHALL be returned **once**, whether or not a
resource filter is applied. Filtering and projecting across a join must not multiply the
booking it selects.

When statuses are supplied they SHALL be used exactly as given, including asking only for
cancelled or declined bookings, which is how a reader answers what was called off.

When no status is supplied the query SHALL return only the statuses that **block** time —
the same set the domain already uses to decide whether a booking claims its interval. The
default answer to "what is booked" SHALL NOT silently include bookings that are not.

**This default SHALL be documented rather than discovered.** Nothing is hidden
irrecoverably: an excluded booking is one filter away, and a reader who cannot find a
cancelled booking must be able to learn why from the package rather than by experiment.

Filtering by the **service** that produced a booking SHALL NOT be offered by this
capability, and the reason SHALL be stated rather than implied. **The reason is now a
scope decision, and this requirement previously stated a different one that has become
false.** It said a booking does not record the service it came from — that the service is
used to choose resources and is not retained — and a booking now records exactly that. The
conclusion is unchanged and the justification is not: the data exists, and a filter over it
belongs with the screen that would offer it, so that the filter and the control that drives
it are designed together rather than the filter being added on the guess that one will want
it.

**A statement of a limit SHALL NOT outlive the limit.** This one was published on the read
port itself, so a reader inspecting the package would have been told bookings do not record
their service by the same package that records it. The requirement is restated here rather
than deleted because "we do not offer this" is still true and still worth explaining.

#### Scenario: The default omits cancelled and declined bookings
- **WHEN** bookings are listed with no status filter
- **THEN** only bookings whose status blocks time are returned

#### Scenario: Cancelled bookings are reachable
- **WHEN** a caller asks for cancelled bookings
- **THEN** they are returned

#### Scenario: Filtering by one resource
- **WHEN** a caller lists bookings naming a single resource
- **THEN** only bookings claiming that resource are returned

#### Scenario: Filtering by several resources matches any of them
- **WHEN** a caller lists bookings naming more than one resource
- **THEN** a booking claiming any one of them is returned

#### Scenario: A booking claiming several resources is returned once
- **WHEN** a booking claims two resources and both are within the filter
- **THEN** it appears once in the results and once in the total, carrying both resources

#### Scenario: No resource filter means no filtering
- **WHEN** a caller supplies no resources, or an empty set
- **THEN** bookings are returned regardless of what they claim

#### Scenario: The absence of a service filter is explained
- **WHEN** a reader asks why bookings cannot be filtered by service
- **THEN** the package states that the filter is not offered yet, rather than leaving the omission unexplained, and SHALL NOT state that a booking does not record its service

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
