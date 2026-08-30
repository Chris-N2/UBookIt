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
rather than a screen over an existing one. This capability now covers *see* in full — the
read port, the authorized endpoint over it, and the backoffice view an operator reads it
in — and *cancel* is the half still outstanding.

## Requirements
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

### Requirement: Bookings have a backoffice collection view
The package SHALL register a **Bookings** view in the uBookIt backoffice section, beside the
resource and service views and under the same section condition, listing the bookings the
management endpoint returns.

The list SHALL be a semantic table built from the backoffice UI library, showing per booking:
when it runs, the booker's name and email, the resources it claims by name, the service it
was placed for, and its status. It SHALL show the unpaged total and page through results, in
the same idiom the section's existing lists use.

**The view SHALL NOT compute anything the endpoint does not return.** A value the endpoint
cannot supply is a finding about the endpoint, not a calculation to add to a screen — the
same rule the endpoint already carries about fields the read port cannot supply, pointed one
layer further out.

No third-party widget framework SHALL be used, and every string the view displays SHALL come
from the package's localization with `en-US` provided.

#### Scenario: The section lists bookings
- **WHEN** a backoffice user with access to the uBookIt section opens the Bookings view
- **THEN** bookings in the default window are listed with their time, booker, resources, service and status, and the unpaged total is shown

#### Scenario: Paging reaches the rest
- **WHEN** more bookings match than fit on one page
- **THEN** the remaining bookings are reachable, and the total reported is the number matching the query rather than the number on the page

#### Scenario: A booking placed directly says so
- **WHEN** a listed booking has no service
- **THEN** the view states that it was booked directly, rather than leaving the cell blank as though the value were missing

### Requirement: The view opens on a usable window and lets an operator change it
The endpoint requires a date window, so the view SHALL open on one rather than on an empty
result or an error. The default SHALL be **the current week**.

The window SHALL be presented as two dates the operator can see and change, because the
site's guardrail refuses an over-wide window and reports the refusal **in terms of the dates
that were sent** — a failure that cannot be acted on if the dates are not visible.

**The view SHALL send dates, and SHALL NOT convert them to instants or apply any time-zone
rule of its own.** That conversion is the endpoint's, placed there so it happens once rather
than once per client; a view that reimplemented it would be the second implementation the
arrangement exists to prevent.

A window the site refuses SHALL be reported to the operator in the terms the endpoint used,
rather than as a generic failure.

#### Scenario: Opening the view shows this week
- **WHEN** an operator opens the Bookings view without choosing anything
- **THEN** the bookings of the current week are listed, and the window's two dates are shown

**A change to the query SHALL return the view to the first page.** A page number counts into
one result set and means nothing in another: an operator on page three who narrows the window
would otherwise be shown page three of a different question — plausibly empty, beneath a
"showing 41–60 of 12" that cannot be true — with nothing on screen indicating the page number
is stale.

#### Scenario: The operator changes the window
- **WHEN** an operator changes either date
- **THEN** the list reloads for the new window

#### Scenario: Changing the query returns to the first page
- **WHEN** an operator on a later page changes the window or the status filter
- **THEN** the first page of the new result set is shown, rather than a page counted into the previous one

**Only the most recently requested result SHALL be shown.** Changing one end of the window
and then the other starts two requests, and they can finish in either order. A superseded
request SHALL NOT write anything — not its results, not its failure, and not the end of its
loading state — because a stale failure landing after a fresh success puts an error on screen
directly above the data that contradicts it, which is worse than either alone: the reader
cannot tell which to believe.

#### Scenario: A superseded request does not overwrite a newer one
- **WHEN** two requests are in flight and the older one finishes last
- **THEN** the newer one's result stands, and the older one neither replaces it, nor reports a failure over it, nor ends its loading state

#### Scenario: An over-wide window is reported, not swallowed
- **WHEN** an operator asks for a window wider than the site permits
- **THEN** the view reports the refusal in terms of the dates asked for, and does not present an empty list as though nothing matched

#### Scenario: No time-zone conversion happens in the view
- **WHEN** the request the view issues is inspected
- **THEN** it carries the two dates as dates, with no instant, offset or zone computed by the view

### Requirement: Cancelled bookings are reachable from the view
The endpoint returns only the statuses that block time when none is asked for, so a cancelled
or declined booking is invisible by default. The view SHALL offer a status control that can
reach them.

**Selecting no status SHALL mean what the endpoint means by omitting it** — the statuses that
block time — and the view SHALL express that by **omitting the parameter** rather than by
naming a set of its own.

The reason is that a default restated in the view is a second default, and the one an
operator meets would then depend on which layer answered first. It is **not** that an empty
set would return nothing: the endpoint treats an empty set exactly as it treats an absent
one, so that particular request is harmless. What is not harmless is the view naming **all
four** statuses to mean "the default", which is a genuinely different request — it includes
cancelled and declined bookings, which the default excludes — and would silently answer a
question the operator did not ask.

Status values SHALL cross the wire as the names the package publishes. A localized label SHALL
NOT be sent as a value.

#### Scenario: Cancelled bookings can be listed
- **WHEN** an operator asks for cancelled bookings
- **THEN** they are listed

#### Scenario: Choosing no status shows what is booked
- **WHEN** an operator has selected no status
- **THEN** the result is what the endpoint returns for that window with no status filter, rather than an empty list

#### Scenario: The wire carries published names
- **WHEN** the view requests a status it displays under a localized label
- **THEN** the value sent is the name the package publishes, not the label shown

### Requirement: A booking's time is shown in the zone it was made in
Each booking carries the time zone it was placed against, and the view SHALL render its
interval in **that** zone rather than in the reader's or the site's — the booking is a record
of a local time, and restating it in another zone reports something that was never agreed.

The zone SHALL be shown beside the time **when the page contains more than one distinct
zone**, so two rows can never be read against each other as though they shared a clock. Where
every row shares a zone — every ordinary site — the label SHALL be omitted rather than
repeated on every row.

**It SHALL also be shown when a booking's recorded zone could not be resolved and its time is
therefore being displayed in some other zone.** A time shown in a zone the booking does not
name is exactly the misattribution this requirement exists to prevent, and it is worse
unlabelled than a mixed page is: the reader has no cue at all that the clock is not the
booking's own. Distinctness is judged on the zone a row is **displayed in** rather than the
identifier it carries, so two unresolvable identifiers showing the same clock count as one.

**The limit of this SHALL be stated rather than implied:** the view compares the bookings on
the page with each other, not with the site's configured zone, because the endpoint does not
report the site's zone and adding a field the read port cannot supply is forbidden. A page
where every booking shares one zone that is no longer the site's is therefore not labelled.
That is reachable only by changing a site's zone after it has taken bookings.

#### Scenario: A single-zone page is not cluttered
- **WHEN** every booking on the page was placed in the same zone
- **THEN** times are shown in that zone without a zone label on each row

#### Scenario: A mixed-zone page is disambiguated
- **WHEN** the page contains bookings placed in different zones
- **THEN** each row shows the zone its time is expressed in

#### Scenario: A time shown in a substituted zone says so
- **WHEN** a booking's recorded zone cannot be resolved and its time is shown in another
- **THEN** the zone it is displayed in is shown, even where every row on the page is affected alike

### Requirement: The bookings view meets the section's accessibility bar
The view's markup SHALL meet the same bar the section's editors do: every control labelled,
failures **announced** rather than only rendered, full keyboard operability with visible
focus, semantic table markup with header cells associated to their columns, and components
from the backoffice UI library preferred over hand-rolled controls. Focus order SHALL match
visual order.

**Where an explanation changes what a control does, it SHALL be associated with that
control** — not merely placed beside it, and not attached to a grouping element, which is
announced on entry as a name rather than a description and is not inherited by the controls
inside it. A native control SHALL be preferred over a library one where the library one
cannot carry the association: the preference for library components is about not
hand-rolling behaviour, and it does not extend to dropping a guarantee the section's editors
already keep by the same means.

*The requirement says failures are announced rather than "associated with their fields",
which is what the resource editor's version says. That is deliberate and narrower: this view
has no per-field failures to associate — its one failure is a whole-request one — and
promising an association it has nothing to associate would be a requirement written for a
different screen.*

This restates the bar rather than extending the existing requirement, which is written about
the resource editor and its scenarios: replacing it wholesale to reach a list view would put
six editing scenarios at risk of being lost to say something about a table.

#### Scenario: Keyboard-only operation
- **WHEN** an operator uses the view with only a keyboard
- **THEN** the window dates, the status control, paging and every row are reachable and operable, with visible focus throughout

#### Scenario: A failed load is announced
- **WHEN** loading the list fails
- **THEN** the failure is exposed to assistive technology rather than only rendered, and the view does not present an empty list as though nothing matched

#### Scenario: An explanation is associated with the control it explains
- **WHEN** an operator reaches the status filter, by keyboard and without reading the surrounding page
- **THEN** the explanation of what the filter does is associated with the controls themselves, rather than only positioned near them

#### Scenario: The table is semantic
- **WHEN** the rendered list is inspected
- **THEN** it is a table with header cells associated to their columns, not a grid of generic elements
