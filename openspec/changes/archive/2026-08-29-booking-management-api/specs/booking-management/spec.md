## MODIFIED Requirements

### Requirement: The list is windowed, and the window is bounded
A list query SHALL require a date window and SHALL match a booking whose interval
**overlaps** that window, treating it as half-open, on the same terms as the claim reads
that serve availability. A booking beginning before the window and continuing into it is
within it.

The window SHALL be rejected when it spans more days than the site's configured maximum
query range, with the same stable failure code the availability queries use for the same
condition. Bookings accumulate without limit, so an unwindowed list is the cost hole that
setting already exists to close.

**Days SHALL be counted whole; a partial day over the maximum SHALL NOT be rejected.**
This is a deliberate relaxation of a stricter earlier reading, and the reason is that a
day is not always 24 hours. A window expressed in a site's local dates resolves to
slightly more than a whole number of days whenever it contains a daylight-saving
fall-back, so comparing raw elapsed time refused a calendar month — "show me October" on
any site in a European time zone running the default guardrail, predictably and every
year. The guard exists to stop an unbounded day-by-day walk, and an hour either way is
not what it protects against.

**The window SHALL NOT be optional.** An optional window makes the unbounded call the
easiest one to write, and the guarantee this requirement makes is one no caller can
decline.

A window that does not run forwards SHALL be refused with a stable failure code of its
own, distinct from the over-wide one, so a caller can tell "you asked for nothing" from
"you asked for too much".

**The cost SHALL be stated rather than left to be discovered:** a booking whose date is
not known cannot be found through this port. Locating a booking from a booker's name,
email or reference is a different query with different indexing, and is not provided here.

#### Scenario: A booking overlapping the window is listed
- **WHEN** a booking starts before the window and ends inside it
- **THEN** it appears in the results

#### Scenario: A booking outside the window is not listed
- **WHEN** a booking's interval does not overlap the window at all
- **THEN** it does not appear in the results

#### Scenario: An over-wide window is refused
- **WHEN** a list query's window spans a whole day more than the site's configured maximum
- **THEN** the query fails with the same stable failure code an over-wide availability query produces, and no results are returned

#### Scenario: A partial day over the maximum is accepted
- **WHEN** a list query's window spans the maximum number of days plus part of another, as a local calendar month containing a daylight-saving change does
- **THEN** the query is accepted, because the guard counts whole days

#### Scenario: The window cannot be omitted
- **WHEN** a caller attempts to list bookings without a window
- **THEN** it is not possible to express the request

#### Scenario: A backwards or empty window is refused
- **WHEN** a list query's window ends at or before it starts
- **THEN** the query fails with a stable failure code distinguishable from the over-wide one, and no results are returned

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

**A status the caller names SHALL be one the package publishes, or the request SHALL be
refused** with a stable failure code of its own. Statuses cross the boundary as **names**,
because the endpoint's contract is names, and a name the package does not recognise SHALL
NOT be ignored: dropping it silently returns a page filtered by something other than what
was asked for, which succeeds and is wrong — the worst pair of properties a response can
have.

The refusal SHALL cover every way a value can fail to be one of the published names,
**including forms that a permissive parse would accept**: a numeric value naming a status
by its underlying ordinal rather than by name, a value with surrounding whitespace, and
several names joined into one parameter. That last is the one worth naming: a caller
joining a repeated query parameter with commas is ordinary, and a parse that accepts it can
combine the values into a *different* status that is itself valid.

#### Scenario: Cancelled bookings are reachable over HTTP
- **WHEN** a caller asks for cancelled bookings
- **THEN** they are returned

#### Scenario: An unrecognised status name is refused
- **WHEN** a caller names a status the package does not publish
- **THEN** the request fails with a stable failure code identifying the offending value, and no results are returned

#### Scenario: A status cannot be named by ordinal or by a joined list
- **WHEN** a caller supplies a numeric value, a value with surrounding whitespace, or several status names joined into one parameter
- **THEN** each is refused as an unrecognised name, rather than resolving to a status the caller did not ask for
