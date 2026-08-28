# booking-management

## Purpose

How an **operator** reads the bookings a site has taken: the windowed, paged, filtered
query behind any backoffice booking screen, what a summary carries, which statuses come
back by default, and the limits the query refuses to exceed.

The capability is named for the actor, not for read-versus-write. `bookings` is the domain
a **visitor** places a booking in — its shape, status machine, validation pipeline,
conflict and atomicity. This is what somebody running the site can see of the result, and
in time what they can do to it: recording a booking on a customer's behalf is a different
thing from a visitor making one, and belongs here rather than alongside the placement
pipeline.

**What "management" means in v1 is narrower than the word suggests, and the boundary is
deliberate.** Placement auto-confirms — `bookings` states that no v1 pathway produces
`Requested` or `Declined` — so the honest verbs are *see* and *cancel*. Approving,
declining, amending a booking's time and editing a booker are each a change to the domain
rather than a screen over an existing one. This capability currently covers only the
reading half of *see*.

## Requirements
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

A window that does not run forwards SHALL be refused with a stable failure code of its
own, distinct from the over-wide one, so a caller can tell "you asked for nothing" from
"you asked for too much".

#### Scenario: The window cannot be omitted
- **WHEN** a caller attempts to list bookings without a window
- **THEN** it is not possible to express the request

#### Scenario: A backwards or empty window is refused
- **WHEN** a list query's window ends at or before it starts
- **THEN** the query fails with a stable failure code distinguishable from the over-wide one, and no results are returned

### Requirement: An unusable query cannot be constructed
The window guard SHALL be enforced when a query is **created**, and a query that fails it
SHALL NOT come into existence. The store SHALL therefore receive only queries it can serve,
and SHALL return its page directly rather than a result that may carry failures — on the
same terms as the other management stores' list reads.

**This is stronger than checking before the call, and that is the reason for it.** A guard
in front of the store is something a caller can route around; a guard in the constructor
leaves no invalid value to pass. The eventual API layer is only one caller, and a port
whose safety depends on every caller remembering to check first is not safe.

**No Core service SHALL depend on the management store to achieve this.** The `bookings`
capability requires that the read ports remain the only pathway anonymous delivery traffic
reaches storage through, and a validating service in Core would have been the first
exception to it. Putting the guard in the query type satisfies both obligations at once.

The query type SHALL NOT offer a copy-and-modify facility that bypasses creation, since
that would reproduce exactly the state creation exists to prevent.

Creation SHALL also normalise what it accepts — resolving the default statuses, treating an
absent resource set as empty, and bounding the page — so that every implementation of the
port sees the same already-settled values. A default each store decided for itself would be
several guarantees wearing one name.

#### Scenario: An invalid window yields no query
- **WHEN** a caller attempts to create a query whose window is unusable
- **THEN** creation fails with the relevant failure code and no query is produced

#### Scenario: The store cannot be handed an invalid query
- **WHEN** the query type's construction surface is inspected
- **THEN** there is no route to an instance that skips the window guard, including by copying an existing instance with altered values

#### Scenario: The store does not re-validate
- **WHEN** the store is given a query
- **THEN** it returns the matching page rather than a result that may carry failures

#### Scenario: Defaults are settled once, at creation
- **WHEN** a query is created without statuses or resources
- **THEN** it already carries the default statuses and an empty resource set, rather than leaving each store to decide

### Requirement: Results are paged in a stable order
A list query SHALL be paged, and SHALL report the **total** number of bookings matching
the window and filters, so a caller can render a pager.

Results SHALL be ordered by start time and then by booking identity, both ascending. The
identity tiebreak is required, not decorative: two bookings may share a start time, and
paging over an order that is not total silently repeats or drops rows between pages.

**The ordering is a total order rather than a specified sequence of ids.** How a store
orders two identities is its own concern — SQL Server, for instance, compares
`uniqueidentifier` by its last six bytes rather than in the order a caller's language
would sort the same values. What this requirement guarantees is that the order is total
and stable across pages, not that it matches any particular caller-side sort.

A page size SHALL be bounded, so that a caller asking for an unreasonable page receives a
capped one rather than the whole window. The bound SHALL match the one the other
management list reads already apply, so that page sizes do not differ per capability.

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
