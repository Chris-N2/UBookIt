## ADDED Requirements

### Requirement: Bookings can be enumerated for management
The package SHALL provide a read port that **lists** bookings, distinct from the
availability reads that serve the front end. It SHALL be a Core port with a persistence
implementation, on the same terms as the existing resource and service management stores,
so that an alternative implementation is possible and has a stated contract to satisfy.

The port SHALL return, for each booking, everything a management list row displays
**without a further read per booking**: the booking's identity, its interval and the time
zone it was made in, its status, when it was created, the booker's name and email, and
each claimed resource's id **and name**.

Resource names are part of the guarantee rather than a convenience. A claim records only a
resource id, so a caller given ids alone must perform a lookup per claim, which is the
cost this port exists to avoid.

#### Scenario: A booking is listed with everything a row shows
- **WHEN** bookings are listed
- **THEN** each result carries its interval, time zone, status, creation time, booker name and email, and every claimed resource as an id and a name

#### Scenario: Resource names come back with the list
- **WHEN** a listed booking claims a resource
- **THEN** that resource's name is present in the result, without the caller reading the resource separately

#### Scenario: The front-end reads are unaffected
- **WHEN** this port is added
- **THEN** the availability and placement reads behave exactly as before, and no booking is placed, cancelled or altered by any operation in this capability

### Requirement: The list is windowed, and the window is bounded
A list query SHALL require a date window and SHALL match a booking whose interval
**overlaps** that window, treating it as half-open, on the same terms as the claim reads
that serve availability. A booking beginning before the window and continuing into it is
within it.

The window SHALL be rejected when it spans more days than the site's configured maximum
query range, with the same stable failure code the availability queries use for the same
condition. Bookings accumulate without limit, so an unwindowed list is the cost hole that
setting already exists to close.

**The window SHALL NOT be optional.** An optional window makes the unbounded call the
easiest one to write, and the guarantee this requirement makes is one no caller can
decline.

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
- **WHEN** a list query's window spans more days than the site's configured maximum
- **THEN** the query fails with the same stable failure code an over-wide availability query produces, and no results are returned

#### Scenario: The window cannot be omitted
- **WHEN** a caller attempts to list bookings without a window
- **THEN** it is not possible to express the request

### Requirement: Validation is the service's job, not the store's
The window guard SHALL be enforced by a query **service**, and the store SHALL receive an
already-valid query and return its page directly.

This SHALL follow the split the package already uses rather than introduce a second
convention: management store reads return a page, mutations return a result carrying
failures, and the equivalent range guard for availability is enforced by a service.

The guard SHALL NOT be left to the eventual API layer. A port whose safety depends on
every caller remembering to check first is not safe, and the API is only one caller.

#### Scenario: An invalid query never reaches the store
- **WHEN** a list query fails the window guard
- **THEN** the failure is returned by the service and the store is not asked for results

#### Scenario: The store does not re-validate
- **WHEN** the store is given a query
- **THEN** it returns the matching page rather than a result that may carry failures, on the same terms as the other management stores' list reads

### Requirement: Results are paged in a stable order
A list query SHALL be paged, and SHALL report the **total** number of bookings matching
the window and filters, so a caller can render a pager.

Results SHALL be ordered by start time and then by booking identity, both ascending. The
identity tiebreak is required, not decorative: two bookings may share a start time, and
paging over an order that is not total silently repeats or drops rows between pages.

**A test for this SHALL include bookings that share a start time**, because a fixture of
distinct start times passes against an ordering that has no tiebreak at all.

#### Scenario: Pages do not overlap or lose rows
- **WHEN** results are read one page at a time and then concatenated
- **THEN** every matching booking appears exactly once, in start-time order

#### Scenario: Bookings sharing a start time page stably
- **WHEN** several bookings share a start time and are read across a page boundary
- **THEN** none is repeated and none is skipped

#### Scenario: The total counts matches, not the page
- **WHEN** a page of results is returned
- **THEN** the reported total is the number of bookings matching the window and filters, not the number in the page

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

Filtering by the **service** that produced a booking SHALL NOT be offered, and the reason
SHALL be stated rather than implied: a booking does not record the service it came from.
A service is used to choose the resources a booking claims and is not retained, so this is
a limit of the data rather than a choice about the query.

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
- **THEN** the package states that a booking does not record the service that produced it, rather than leaving the omission unexplained
