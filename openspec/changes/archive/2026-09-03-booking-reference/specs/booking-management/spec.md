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

### Requirement: Bookings are readable over an authorized management endpoint
The package SHALL expose a **versioned** backoffice endpoint, **in the same swagger group as
its other management endpoints**, returning the **windowed, paged, filtered** list of bookings
the management read port provides.

The endpoint SHALL require backoffice authorization on the same terms as every other
uBookIt management endpoint. **It SHALL NOT be reachable anonymously under any configuration
the package ships.**

**Request and response bodies SHALL be purpose-built models.** **Domain types SHALL NOT appear
in the HTTP contract**, on the same terms as the resource and service endpoints.

The response SHALL carry, per booking, exactly what the read port supplies: the booking's
identity, **its quotable reference**, its interval and the time zone it was made in, its
status, when it was created, the booker's name and email, each claimed resource's id and name,
and **the service it was placed for, or null**. It SHALL report the unpaged total alongside the
page, so a caller can render a pager.

**The reference SHALL cross the boundary in canonical form.** How it is grouped for reading is
the client's decision, and a client that searches or compares needs the value exactly as it is
stored — the same reasoning that keeps widgets out of every other contract this package
publishes.

**The service SHALL cross the boundary as a single nullable object carrying both id and
name**, rather than as two parallel nullable fields. "A booking has a service, or it does
not" is then expressed in the shape, rather than as a rule that two fields must be null
together — which a client can observe violated and has no way to interpret.

**No field SHALL be added at the HTTP layer that the read port cannot supply.** A field the
port does not carry is one the screen must obtain another way, which is how a second read
path into bookings begins.

#### Scenario: A page of bookings is returned with its total
- **WHEN** an authorized caller requests bookings for a window
- **THEN** the matching page is returned with the unpaged total, each booking carrying its reference, interval, time zone, status, creation time, booker name and email, its resources with their names, and its service where it has one

#### Scenario: A directly placed booking carries a null service
- **WHEN** a returned booking was placed directly
- **THEN** its service member is null, rather than an object with empty or placeholder values

#### Scenario: The endpoint is not anonymous
- **WHEN** the endpoint is called without backoffice authentication
- **THEN** the response is 401 and no handler logic executes

#### Scenario: No domain type crosses the HTTP boundary
- **WHEN** the endpoint's request and response models are inspected
- **THEN** they are purpose-built models, and no domain or store type appears in the contract

#### Scenario: The reference crosses the boundary unformatted
- **WHEN** a booking's reference is returned by the endpoint
- **THEN** it is the canonical stored value, leaving the client to group it for display

### Requirement: Bookings have a backoffice collection view
The package SHALL register a **Bookings** view in the uBookIt backoffice section, beside the
resource and service views and under the same section condition, listing the bookings the
management endpoint returns.

The list SHALL be a semantic table built from the backoffice UI library, showing per booking:
**its quotable reference**, when it runs, the booker's name and email, the resources it
claims by name, the service it was placed for, and its status. **The reference SHALL come
first**, because it is the column an operator scans while somebody reads it out — which is
the case the whole identifier exists for. It SHALL show the unpaged total and page through results, in
the same idiom the section's existing lists use.

**The view SHALL NOT compute anything the endpoint does not return.** A value the endpoint
cannot supply is a finding about the endpoint, not a calculation to add to a screen — the
same rule the endpoint already carries about fields the read port cannot supply, pointed one
layer further out.

**Formatting a value the endpoint did return is not computing one.** The reference crosses
the boundary canonical and is grouped for reading on the way to the screen, exactly as an
instant crosses as UTC and is rendered in the booking's zone. The rule above is about the
view inventing data; presentation of data it was given is the view's own business, and
stating that here keeps a reader of this requirement alone from seeing a prohibition being
broken.

No third-party widget framework SHALL be used, and every string the view displays SHALL come
from the package's localization with `en-US` provided.

#### Scenario: The section lists bookings
- **WHEN** a backoffice user with access to the uBookIt section opens the Bookings view
- **THEN** bookings in the default window are listed with their reference, time, booker, resources, service and status, and the unpaged total is shown

#### Scenario: Paging reaches the rest
- **WHEN** more bookings match than fit on one page
- **THEN** the remaining bookings are reachable, and the total reported is the number matching the query rather than the number on the page

#### Scenario: A booking placed directly says so
- **WHEN** a listed booking has no service
- **THEN** the view states that it was booked directly, rather than leaving the cell blank as though the value were missing
