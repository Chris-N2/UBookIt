## ADDED Requirements

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
the booker's name and email, and each claimed resource's id and name. It SHALL report the
unpaged total alongside the page, so a caller can render a pager.

**No field SHALL be added at the HTTP layer that the read port cannot supply.** A field the
port does not carry is one the screen must obtain another way, which is how a second read
path into bookings begins.

#### Scenario: A page of bookings is returned with its total
- **WHEN** an authorized caller requests bookings for a window
- **THEN** the matching page is returned with the unpaged total, each booking carrying its interval, time zone, status, creation time, booker name and email, and its resources with their names

#### Scenario: The endpoint is not anonymous
- **WHEN** the endpoint is called without backoffice authentication
- **THEN** the response is 401 and no handler logic executes

#### Scenario: No domain type crosses the HTTP boundary
- **WHEN** the endpoint's request and response models are inspected
- **THEN** they are purpose-built models, and no domain or store type appears in the contract

### Requirement: The window is expressed in the site's own time
The endpoint SHALL accept its window as **dates**, and SHALL resolve them to instants using
the site's configured time zone before querying. A caller SHALL NOT be required to know the
site's time zone, perform its own conversion, or reason about daylight saving in order to
ask for a day or a week.

The window SHALL be inclusive of both named dates as an operator would mean them: naming a
Monday and a Sunday covers all of that Sunday.

**The conversion SHALL happen once, server-side.** The site's time zone is a server
setting, and a rule reimplemented per client is a rule that will differ per client at a
daylight-saving boundary.

A window the site's guardrail refuses SHALL be reported in terms of **the dates the caller
sent**, not of the instants they were converted into, so the failure names something the
caller can act on.

#### Scenario: A window is asked for in site-local dates
- **WHEN** a caller requests a window by naming two dates
- **THEN** the bookings returned are those overlapping that span of site-local time, with no time zone supplied by the caller

#### Scenario: The last named date is included in full
- **WHEN** a caller names a window ending on a given date
- **THEN** bookings later that day are within the window

#### Scenario: An over-wide window is refused in the caller's own terms
- **WHEN** a caller requests a window wider than the site permits
- **THEN** the failure identifies the dates that were asked for, rather than converted instants the caller never supplied

### Requirement: Filters and paging are the port's, expressed over HTTP
The endpoint SHALL expose the read port's filters — status and resource — and its paging,
without adding rules of its own. Omitting a filter SHALL mean what omitting it means at the
port: no status filter yields the statuses that block time, and no resource filter yields
bookings regardless of what they claim.

**The endpoint SHALL NOT reimplement a default the port already settles.** A default stated
in two places is two defaults, and the one a caller meets is decided by which layer they
reach first.

#### Scenario: Omitted filters behave as they do at the port
- **WHEN** a caller supplies no status and no resource filter
- **THEN** the result is the same as the read port returns for that window with no filters — blocking statuses only, and no restriction by resource

#### Scenario: Cancelled bookings are reachable over HTTP
- **WHEN** a caller asks for cancelled bookings
- **THEN** they are returned
