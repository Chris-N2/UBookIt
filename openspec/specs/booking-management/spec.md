# booking-management

## Purpose

How an **operator** sees the bookings a site has taken and calls one off: the windowed,
paged, filtered query behind any backoffice booking screen, what a summary carries, which
statuses come back by default, the limits the query refuses to exceed, and what cancelling
a booking does and does not do.

The capability is named for the actor, not for read-versus-write. `bookings` is the domain
a **visitor** places a booking in — its shape, status machine, validation pipeline,
conflict and atomicity. This is what somebody running the site can see of the result and
what they can do to it: recording a booking on a customer's behalf is a different
thing from a visitor making one, and belongs here rather than alongside the placement
pipeline.

**What "management" means is narrower than the word suggests, and the boundary is
deliberate.** The verbs are *see*, *find a subject's bookings by their email address*,
*confirm* or *decline* a requested booking, *cancel* and *erase a booker's contact details*.
Amending a booking's time is a change to the domain rather than a screen over an existing
one, and is still not here. This capability is about those verbs and the path each takes — the ports, the authorized endpoints over them, and the backoffice
views an operator works in — and the requirements below, not this paragraph, say what
exists.

*Confirm and decline joined the list with the `approval-decline` change, which made
`Requested` reachable: placement under `AutoConfirm` off produces a booking that waits for an
operator, and these are the two ways an operator resolves it. This paragraph previously said
placement auto-confirms and that no pathway produces `Requested` or `Declined`; that was true
until the setting existed, and remains the behaviour of an unconfigured site.*

*Finding by address joined the list with the `find-by-booker` change, and is here rather than
elsewhere for the reason erasure is: the endpoint an operator reaches it through is one of this
capability's, gated by this capability's authorization. It is a separate read from the list
because it must answer without a window, which the list's own requirement forbids and SHALL
continue to forbid.*

*Erasure joined the list with the `booker-erasure` change. It is a change to the domain on
exactly the terms the sentence above describes — a named operation on the aggregate, not a
screen over an existing one — and it is here rather than elsewhere because the endpoint an
operator reaches it through is one of this capability's, gated by this capability's
authorization. "Editing a booker" was previously named among the things that live elsewhere;
removing a booker's details is not editing them, and nothing here permits changing them.*

## Requirements
### Requirement: Bookings can be enumerated for management
The package SHALL provide a read port that **lists** bookings, distinct from the
availability reads that serve the front end. It SHALL be a Core port with a persistence
implementation, on the same terms as the existing resource and service management stores,
so that an alternative implementation is possible and has a stated contract to satisfy.

The port SHALL return, for each booking, everything a management list row displays
**without a further read per booking**: the booking's identity, **its quotable reference**,
its interval and the time zone it was made in, its status, when it was created, **the booker's
contact details or the fact and instant of their erasure**, each claimed resource's id **and
name**, and **the service it was placed for — id and name — or nothing, for a booking placed
directly**.

**The booker SHALL cross this port in exactly the two states the domain permits**: contact
details present, or erased with the instant recorded. The port SHALL NOT report an erasure by
supplying empty contact details, and a consumer SHALL NOT be able to read a name or an email
without having established that they are present. Whether a *caller* may see present details
is not this port's question — it lists what is stored, and the decision about who may read it
belongs to the endpoint.

Resource names are part of the guarantee rather than a convenience. A claim records only a
resource id, so a caller given ids alone must perform a lookup per claim, which is the
cost this port exists to avoid. **The service name is carried for the same reason and one
more**: it is the name recorded at placement time, so it remains answerable for a service
that has since been renamed or deleted, which a read-time join could not do.

**The reference is carried for a third reason: somebody is on the telephone.** The case this
port exists to serve is an operator finding the booking a caller is describing, and the caller
has a reference in front of them and not a machine identifier. A row that cannot be matched
against what the customer is reading out fails at the moment it is most needed. **An erased
booking is precisely the case this matters most for**, since the reference is then the only
thing left to call it by.

**This port SHALL remain read-only.** It lists; it does not change what it lists. That is
what makes it substitutable and what keeps a caller's reads free of side effects — the
cancel and erase endpoints in this same capability reach the domain, never this port.

#### Scenario: A booking is listed with everything a row shows
- **WHEN** bookings are listed
- **THEN** each result carries its reference, interval, time zone, status, creation time, its booker's contact details or its erasure, every claimed resource as an id and a name, and its service attribution as an id and a name where it has one

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

#### Scenario: An erased booking is listed as erased
- **WHEN** a booking whose booker was erased is listed
- **THEN** it appears in the results carrying the fact and instant of erasure, no name or email, and every other field it always carried

#### Scenario: The port does not blank an erased booker
- **WHEN** an erased booking crosses the port
- **THEN** no empty or placeholder name or email is supplied in place of the erased values

#### Scenario: The port stays read-only
- **WHEN** the port's surface is inspected
- **THEN** it offers no operation that changes a booking, erasure included

*The front-end scenario above said "by any operation in this capability" until cancellation
joined it. The narrowing is to what the scenario was always guarding — that adding a read port
changes nothing — and the guarantee it protected is now carried explicitly by the read-only
SHALL above, which is stronger than an aside in a scenario's THEN.*

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
not known cannot be found **by this read**. Locating a booking from a booker's name or reference
is a different query with different indexing, and is not provided by it.

**Locating a booking by a booker's email address IS provided, as a separate read of its own** —
see *A subject's bookings can be found by their email address*. It is separate precisely because
it is unwindowed: this requirement's window is not optional and SHALL NOT be made so to
accommodate it. A query that must answer without a date belongs beside this one, never inside it,
because the guarantee here is one no caller can decline and a nullable window would make the
unbounded call the easiest one to write.

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
**Every paged read this capability offers** SHALL be paged, and SHALL report the **total**
number of bookings matching the query, so a caller can render a pager. For the windowed list
that is the window and its filters; for the by-address search it is the address.

*Widened from "a list query … matching the window and filters" when the by-address search
arrived. The guarantee was never about windows: it is that a pager is told how many rows exist
rather than how many it was handed, and that paging over a non-total order cannot repeat or drop
them. A search reporting its page size as its total would tell an operator honouring an erasure
request that a person has fewer bookings than they do, which is the worst version of this
failure — so the requirement had to reach it.*

Results SHALL be ordered by start time and then by booking identity, both ascending. The
identity tiebreak is required, not decorative: two bookings may share a start time, and
paging over an order that is not total silently repeats or drops rows between pages.

**The ordering is a total order rather than a specified sequence of ids.** How a store
orders two identities is its own concern — SQL Server, for instance, compares
`uniqueidentifier` by its last six bytes rather than in the order a caller's language
would sort the same values. What this requirement guarantees is that the order is total
and stable across pages, not that it matches any particular caller-side sort.

A page size SHALL be bounded, so that a caller asking for an unreasonable page receives a
capped one rather than the whole result. The bound SHALL match the one the other
management list reads already apply, so that page sizes do not differ per capability — or per
read within one capability, which is the same argument one level down.

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
- **THEN** the reported total is the number of bookings the query matched — the window and its filters for the list, the address for the search — not the number in the page

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
The package SHALL expose a **versioned** backoffice endpoint, **in the same swagger group as
its other management endpoints**, returning the **windowed, paged, filtered** list of bookings
the management read port provides.

The endpoint SHALL require backoffice authorization on the same terms as every other
uBookIt management endpoint. **It SHALL NOT be reachable anonymously under any configuration
the package ships.**

**Request and response bodies SHALL be purpose-built models.** **Domain types SHALL NOT appear
in the HTTP contract**, on the same terms as the resource and service endpoints.

The response SHALL carry, per booking: the booking's identity, **its quotable reference**, its
interval and the time zone it was made in, its status, when it was created, **the booker as one
of three stated conditions — shown, withheld, or erased**, each claimed resource's id and name,
and **the service it was placed for, or null**. It SHALL report the unpaged total alongside the
page, so a caller can render a pager.

**The response SHALL carry what the read port supplies, less what the caller may not see, and
never more.** The subtraction is the whole of the difference: no field is added here that the
port cannot fill, and no field the port fills reaches a caller who is not permitted it. A
field the port does not carry is one the screen must obtain another way, which is how a
second read path into bookings begins.

**BREAKING (unpublished): the booker member SHALL NOT be nullable, and SHALL state its own
condition.** It SHALL always be present and SHALL say which of *shown*, *withheld* or *erased*
applies; contact details SHALL be carried only in the first case, and the erasure instant only
in the last. This replaces the nullable member whose null meant "withheld from you", which was
added after `0.1.0` and never published.

**The reason is that absence now has two causes and they demand different actions.** A null
could mean the caller may not see the details or that nobody can; *"ask a colleague who has
access"* and *"this is gone permanently"* are different next steps, and a client made to
distinguish them by inference would be inferring something the server already knows. A member
whose absence is already meaningful must not be overloaded to carry withholding as well, per
the `sensitive-data` capability — so the null is spent, and the condition is stated instead.

**Contact details SHALL remain clumped in a single member**, on the same terms and for the same
reason as the service: two parallel nullable strings could be observed half-populated and leave
a client with no correct reading. **A booking without a booker SHALL remain inexpressible** —
the domain requires a booker of every booking, and erasure removes the person's details, not
the booker.

**The condition SHALL cross the boundary by name rather than as a domain enum**, on the same
terms as the status, because an enum's members are a versioning commitment and domain types do
not appear in this contract.

**Erasure SHALL take precedence over withholding.** Where details have been erased there is
nothing to withhold, so a caller without sensitive-data access SHALL be told the booking is
erased. That a record was erased is a fact about the record and not about the person, and it
discloses nothing.

**Whether the caller may see present details SHALL be decided from Umbraco's sensitive-data
access**, per the `sensitive-data` capability, and SHALL be settled before the response is
composed. The endpoint SHALL NOT return the details and rely on a client to suppress them.

**The reference SHALL cross the boundary in canonical form.** How it is grouped for reading is
the client's decision, and a client that searches or compares needs the value exactly as it is
stored — the same reasoning that keeps widgets out of every other contract this package
publishes. **The reference is not personal data and SHALL NOT be withheld or erased**: it
identifies a booking, not a person, and withholding it would leave a row with nothing to call
it by.

**The service SHALL cross the boundary as a single nullable object carrying both id and
name**, rather than as two parallel nullable fields. "A booking has a service, or it does
not" is then expressed in the shape, rather than as a rule that two fields must be null
together — which a client can observe violated and has no way to interpret.

#### Scenario: A page of bookings is returned with its total
- **WHEN** an authorized caller with sensitive-data access requests bookings for a window
- **THEN** the matching page is returned with the unpaged total, each booking carrying its reference, interval, time zone, status, creation time, a booker stated as shown with its name and email, its resources with their names, and its service where it has one

#### Scenario: A caller without sensitive-data access receives the same rows without contact details
- **WHEN** an authorized caller lacking sensitive-data access requests bookings for a window
- **THEN** the same bookings are returned with the same total, each carrying its reference, interval, time zone, status, creation time, resources and service, and a booker stated as withheld

#### Scenario: A withheld booker leaves nothing behind in the payload
- **WHEN** a booking's booker is withheld
- **THEN** neither the name nor the email appears anywhere in the response, including as an empty or masked value

#### Scenario: An erased booking is reported as erased, to anyone
- **WHEN** a booking whose booker was erased is returned to a caller with sensitive-data access, and to a caller without it
- **THEN** both receive a booker stated as erased, carrying the erasure instant and no name or email

#### Scenario: Erased is not reported as withheld
- **WHEN** a caller without sensitive-data access receives a booking whose booker was erased
- **THEN** its booker is stated as erased rather than as withheld

#### Scenario: The booker member is never absent
- **WHEN** any booking is returned by the endpoint, in any of the three conditions
- **THEN** the booker member is present and states its condition, rather than being null

#### Scenario: An erased booker leaves nothing behind in the payload
- **WHEN** a booking's booker was erased
- **THEN** no name, email, phone or member key appears anywhere in the response, including as an empty or masked value

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

#### Scenario: The reference survives withholding
- **WHEN** a booking's booker is withheld from the caller
- **THEN** its reference is still returned, in canonical form

#### Scenario: The reference survives erasure
- **WHEN** a booking's booker has been erased
- **THEN** its reference is still returned, in canonical form

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

### Requirement: An operator can cancel a booking
The package SHALL expose a versioned backoffice endpoint that cancels a booking, in the same
swagger group and under the same section authorization as every other uBookIt management
endpoint.

**It SHALL apply the domain's status machine rather than its own rule.** Cancellation
succeeds from `Requested` and `Confirmed` and from nothing else; a second attempt on an
already-cancelled booking SHALL be refused with the domain's stable code rather than treated
as a no-op success. A caller that is told "cancelled" when nothing changed cannot tell a
completed action from a rejected one.

**A cancelled booking SHALL remain.** It keeps its row, its interval, its booker and the
service it was placed for; it stops holding its time; and it is still returned by the
management list when asked for. The endpoint SHALL therefore not be a deletion, in verb or in
effect.

The response SHALL carry the booking's identity and its new status, so a caller knows what
happened without reading it back.

**It SHALL NOT carry the shape the list carries.** A list row includes each claimed
resource's *name*, which the management port supplies by joining; cancellation goes through
the domain, which knows a booking's resource *ids* and nothing more. A response shaped like a
list row but with those names blank would be quietly less true than the thing it resembles,
and there is no by-id read on the management port to fill them from. What a caller needs
next — whether the booking still belongs in the filter it is looking at, and what the total is
now — are properties of the query rather than of the booking, so they come from asking again.

*This requirement said "the booking as it now stands" until implementation showed that could
not be honoured on this path. The sentence was written before the shape was known; it is
corrected rather than satisfied with blanks.*

An unknown booking SHALL be reported as not found, distinctly from a booking that exists and
cannot be cancelled — they call for different actions from the operator.

#### Scenario: A confirmed booking is cancelled
- **WHEN** an authorized operator cancels a confirmed booking
- **THEN** it becomes cancelled, stops holding its time, and the response names it and reports its new status

#### Scenario: The response does not imitate a list row
- **WHEN** a cancellation's response model is inspected
- **THEN** it carries no resource collection, rather than one whose names this path cannot fill

#### Scenario: Cancelling twice is refused, not silently accepted
- **WHEN** an operator cancels a booking that is already cancelled
- **THEN** the request fails with the domain's invalid-transition code, and the booking is unchanged

#### Scenario: An unknown booking is distinguishable from an uncancellable one
- **WHEN** cancellation is requested for an id no booking has
- **THEN** the failure says the booking was not found, distinctly from the failure a booking that cannot be cancelled produces

#### Scenario: Cancelling is not deleting
- **WHEN** a booking has been cancelled
- **THEN** it is still returned by the management list for its window when cancelled bookings are asked for, carrying the same interval, booker and service it always had

#### Scenario: The endpoint is not anonymous
- **WHEN** the endpoint is called without backoffice authentication
- **THEN** the response is 401 and no booking changes

### Requirement: Bookings have a backoffice collection view
The package SHALL register a **Bookings** view in the uBookIt backoffice section, beside the
resource and service views and under the same section condition, listing the bookings the
management endpoint returns.

The list SHALL be a semantic table built from the backoffice UI library, showing per booking:
**its quotable reference**, when it runs, **the booker — their contact details, or a statement
of why the row carries none**, the
resources it claims by name, the service it was placed for, and its status. **The reference
SHALL come first**, because it is the column an operator scans while somebody reads it out —
which is the case the whole identifier exists for. It SHALL show the unpaged total and page
through results, in the same idiom the section's existing lists use.

**A withheld booker SHALL be rendered as a stated absence, not as an empty cell.** The view
SHALL show a localized indication that contact details are hidden, for the same reason a
booking with no service says so rather than leaving its cell blank: a blank cell reads as
missing data, and here it would read as a defect in the package.

**Where any row's booker is withheld, the view SHALL explain why**, once, where an operator
reading the list will see it — naming Umbraco's Sensitive data group as what grants access.
An operator who has just been made an administrator and finds every contact detail hidden has
no way to reach that explanation from the screen otherwise, and the most likely next action is
a defect report. The explanation SHALL be exposed to assistive technology on the same terms as
the view's other messages, rather than only rendered.

**The view SHALL NOT compute anything the endpoint does not return.** A value the endpoint
cannot supply is a finding about the endpoint, not a calculation to add to a screen — the
same rule the endpoint already carries about fields the read port cannot supply, pointed one
layer further out. **It SHALL NOT ask any other source whether contact details may be shown**:
the response already says so by what it carries, and a second source could disagree with the
first while looking authoritative.

**Formatting a value the endpoint did return is not computing one.** The reference crosses
the boundary canonical and is grouped for reading on the way to the screen, exactly as an
instant crosses as UTC and is rendered in the booking's zone. The rule above is about the
view inventing data; presentation of data it was given is the view's own business, and
stating that here keeps a reader of this requirement alone from seeing a prohibition being
broken.

No third-party widget framework SHALL be used, and every string the view displays SHALL come
from the package's localization with `en-US` provided.

#### Scenario: The section lists bookings
- **WHEN** a backoffice user with access to the uBookIt section and to sensitive data opens the Bookings view
- **THEN** bookings in the default window are listed with their reference, time, booker, resources, service and status, and the unpaged total is shown

#### Scenario: A withheld booker says so
- **WHEN** a listed booking's booker was withheld by the endpoint
- **THEN** the cell states that contact details are hidden, rather than being blank, and the row is still identifiable by its reference

#### Scenario: The operator is told what grants access
- **WHEN** any row on the page has a withheld booker
- **THEN** the view explains once that contact details require membership of Umbraco's Sensitive data group, exposed to assistive technology rather than only rendered

#### Scenario: A page with nothing withheld carries no explanation
- **WHEN** no row's booker was withheld from the caller
- **THEN** the explanation is not shown, whether the rows carry contact details or record an erasure

#### Scenario: Visibility is read from the response, not asked elsewhere
- **WHEN** the view's decision to show or hide contact details is inspected
- **THEN** it follows from what the endpoint returned, with no separate query about the current user's permissions

#### Scenario: Paging reaches the rest
- **WHEN** more bookings match than fit on one page
- **THEN** the remaining bookings are reachable, and the total reported is the number matching the query rather than the number on the page

#### Scenario: A booking placed directly says so
- **WHEN** a listed booking has no service
- **THEN** the view states that it was booked directly, rather than leaving the cell blank as though the value were missing

*Two clauses above were restated for the three-condition contract. "The booker where the
endpoint supplied one" described a member that could be absent; it is now always supplied and
states its own condition, so the sentence described nothing. And the no-explanation scenario
said "every row's booker was supplied", which after erasure is vacuously true of every page
including an all-erased one — it no longer selected the case it was written to guard. Both are
restatements of the same guarantees against the new shape, not narrowings.*

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

### Requirement: The bookings view can cancel a booking
The bookings view SHALL offer cancellation for a booking that can be cancelled, and SHALL NOT
offer it for one that cannot — a control that is always refused teaches an operator to ignore
failures.

**The screen's judgement SHALL NOT be the rule.** The endpoint SHALL refuse an invalid
transition independently, so a screen showing a stale list cannot talk the domain into one.

Cancellation SHALL require confirmation through an accessible in-page modal provided by the
backoffice, never a native browser dialog, on the same terms as deleting a resource.
Dismissing or cancelling the confirmation SHALL leave the booking untouched, and a
confirmation that fails to appear SHALL NOT be treated as a refusal — those are different
outcomes and only one of them is something the operator chose.

After a cancellation the list SHALL show the booking's new state without the operator
reloading the page.

**A cancellation SHALL NOT leave the operator on a page that no longer exists.** Removing the
last row of a page leaves the list positioned past the end of its own results, which renders
as an empty table under a count that cannot be true. The view SHALL step back rather than
returning to the first page: a filter change is a different question and resets, while a
cancellation is the same question with one fewer answer, and an operator working through a
later page should not be thrown to the start each time.

**Focus SHALL NOT be lost when the cancelled row is removed.** The control the operator was
using goes with its row, so focus SHALL be placed somewhere deliberate rather than falling to
the document — a keyboard operator cancelling several bookings would otherwise restart their
traversal every time.

#### Scenario: Cancelling the last row of a page does not strand the operator
- **WHEN** an operator cancels the only booking shown on a later page
- **THEN** the list shows the preceding page rather than an empty table, and never reports a range beyond its own total

#### Scenario: Focus survives the cancelled row
- **WHEN** a keyboard operator confirms a cancellation and the row is removed
- **THEN** focus is placed deliberately within the view rather than lost to the document

**The view SHALL state that cancelling notifies nobody by itself**, where the operator can see
it at the moment they are deciding. A customer who is not told is the predictable consequence
of the button, and an operator who assumes the package sends something will not find out until
somebody arrives for a booking that no longer exists.

#### Scenario: An operator cancels from the list
- **WHEN** an operator confirms cancellation of a booking in the list
- **THEN** the booking is cancelled and the list shows its new state without a page reload

#### Scenario: Dismissing the confirmation changes nothing
- **WHEN** an operator activates cancel and then dismisses or cancels the confirmation
- **THEN** no request is issued and the booking is unchanged

#### Scenario: A booking that cannot be cancelled offers no control
- **WHEN** the list shows a cancelled or declined booking
- **THEN** no cancel control is offered for it

#### Scenario: A refused cancellation is reported
- **WHEN** the endpoint refuses a cancellation the screen believed was possible
- **THEN** the failure is shown to the operator rather than the row appearing to change

#### Scenario: The operator is told the customer is not
- **WHEN** an operator is deciding whether to cancel
- **THEN** the view states that the package notifies nobody by itself
### Requirement: A booking is identified to an operator by its reference
Wherever the bookings view names a particular booking — the accessible name of a per-row
control, and the confirmation shown before an action is carried out — it SHALL identify that
booking by its **quotable reference**.

**It SHALL do so for every operator, not only where the booker is withheld.** A screen that
names the booker when it can and the reference when it cannot has two behaviours to reason
about and two to test, and the branch exists only to preserve a habit. The reference is also
the better identifier for the purpose: it is unique, where two bookings may share a booker's
name, and it is what the person on the telephone is holding.

This restates nothing about *when* an action is offered, refused or confirmed — those are the
cancellation requirement's, unchanged. It constrains only what the operator is shown the
booking as.

#### Scenario: A per-row control is distinguishable
- **WHEN** a screen reader user reaches a per-row control in the bookings list
- **THEN** its accessible name identifies the booking by its reference, distinguishing it from the same control on every other row

#### Scenario: A confirmation names the booking by reference
- **WHEN** an operator is asked to confirm an action on a booking
- **THEN** the booking is identified by its reference

#### Scenario: The identification does not depend on visibility
- **WHEN** an operator with sensitive-data access uses the view
- **THEN** bookings are identified by reference exactly as they are for an operator without it

### Requirement: An operator can erase a booking's booker

The package SHALL expose a **versioned** backoffice endpoint, in the same swagger group as its
other management endpoints, that erases one booking's booker contact details.

**It SHALL require sensitive-data access in addition to section access**, and that requirement
SHALL be a property of the endpoint rather than a condition evaluated inside it: a caller
without sensitive-data access SHALL be refused before any erasure occurs. A user the site has
decided may not read a booker's name SHALL NOT be able to destroy it.

**It SHALL be a POST, not a DELETE.** An erased booking is not gone: it keeps its row, its
reference, its interval, its claims and its status, and it still blocks time. `DELETE` would
say the opposite of all of that, exactly as it would for a cancellation.

**It SHALL reach the domain, not the read port.** The erase verb is a domain operation on the
same terms as cancellation, and the management read port stays read-only.

**It SHALL be idempotent.** Erasing a booking whose booker is already erased SHALL succeed and
change nothing, including the recorded instant. This is deliberately unlike the cancel
endpoint, which refuses a second attempt; the reasoning is in the `booker-erasure` capability.

**Its response SHALL carry what the operation knows** — the booking's identity and the fact and
instant of erasure — rather than a whole list row, on the same terms and for the same reason as
cancellation: this path reaches the booking through the domain, which knows resource ids and
not resource names, and a response shaped like the list's would be quietly less true than it.

**The response SHALL NOT echo what was erased.** Returning the removed name or address as
confirmation would hand back the data the operation exists to remove.

#### Scenario: A permitted operator erases a booking's booker
- **WHEN** a backoffice user with section access and sensitive-data access posts an erasure for a booking
- **THEN** the operation succeeds and the response carries the booking's id and the instant of erasure

#### Scenario: Section access alone cannot erase
- **WHEN** a backoffice user with section access but without sensitive-data access posts an erasure
- **THEN** the request is refused, and the booking's booker is unchanged

#### Scenario: The endpoint is not anonymous
- **WHEN** the endpoint is called without backoffice authentication
- **THEN** the response is 401 and no handler logic executes

#### Scenario: Erasing twice succeeds
- **WHEN** an erasure is posted for a booking whose booker is already erased
- **THEN** the response is a success and the recorded erasure instant is unchanged

#### Scenario: Erasing an unknown booking is refused
- **WHEN** an erasure is posted for an id no booking has
- **THEN** the response reports that it was not found, and nothing is changed

#### Scenario: The response does not echo the erased details
- **WHEN** an erasure succeeds
- **THEN** no name, email, phone or member key appears anywhere in the response

#### Scenario: The booking survives the erasure
- **WHEN** a booking's booker is erased through the endpoint and the booking is then listed
- **THEN** it is still returned with the same reference, interval, time zone, status, creation time, resources and service

### Requirement: The bookings view distinguishes an erased booker from a withheld one

The backoffice bookings view SHALL render a booking whose booker was erased as **its own stated
condition**, distinct from a withheld booker and from an empty cell, and SHALL NOT present it
with the explanation offered for withheld rows.

**The distinction is the point.** A withheld row tells an operator to ask a colleague who has
sensitive-data access; an erased row tells them there is nobody to ask. Rendering the second as
the first sends them on an errand that cannot succeed, and rendering either as a blank cell
reads as a defect in the package.

**The view SHALL read the condition from the response**, which states it, rather than inferring
it from which fields are absent and rather than asking any other source. **It SHALL NOT compute
anything the endpoint does not return**, on the same terms as the rest of this view.

**The explanation about the Sensitive data group SHALL be shown only where rows were actually
withheld.** A page on which every absent booker was erased SHALL NOT tell the operator that
membership of a group would reveal them, because it would not.

Every string SHALL come from the package's localization with `en-US` provided, and SHALL be
exposed to assistive technology on the same terms as the view's other messages.

#### Scenario: An erased booker says so
- **WHEN** a listed booking's booker was erased
- **THEN** the cell states that the contact details were erased, rather than being blank or stating that they are hidden

#### Scenario: An erased row does not offer the group explanation
- **WHEN** every row with no contact details on the page was erased rather than withheld
- **THEN** the explanation about Umbraco's Sensitive data group is not shown

#### Scenario: A page with both conditions distinguishes them
- **WHEN** a page carries one withheld booker and one erased booker
- **THEN** the two rows read differently, and the group explanation is shown for the withheld one

#### Scenario: The condition is read from the response
- **WHEN** the view's decision between erased and withheld is inspected
- **THEN** it follows from the condition the endpoint stated, with no inference from absent fields and no separate query

#### Scenario: An erased row is still identifiable
- **WHEN** a listed booking's booker was erased
- **THEN** the row still shows its reference, time, resources, service and status

### Requirement: A subject's bookings can be found by their email address

The management read port SHALL provide a read that returns the bookings whose booker holds a
given email address, and the package SHALL expose it over a **versioned** backoffice endpoint in
the same swagger group as its other management endpoints.

**It SHALL require sensitive-data access as the endpoint's own authorization**, per the
`sensitive-data` capability, in addition to the section access every management endpoint
requires — and by the same policy the erase endpoint carries, so that reading a booker's details,
finding them and destroying them are one decision rather than three that may drift.

**It SHALL match the address exactly**, offering no prefix, substring, wildcard or fuzzy form, no
ordering by a contact detail and no count-only response. Comparison SHALL follow the store's
collation, and that SHALL be stated rather than made configurable: an option here would be a
second answer to whether two addresses are the same.

**A page of zero SHALL NOT be treated as a count-only form.** Asking for no rows returns none,
with the real total — and that is not the disclosure the exactness rule forbids, because this
caller may read every row it would have returned. The prohibition is on offering a count to
somebody who may not see what is counted; a page size is not that.

**It SHALL be unwindowed**, because a subject's request carries no date, and **SHALL be paged**,
because a prolific booker is not a bounded result set. The absence of a window is why this is a
separate read: the list's window is not optional and SHALL NOT be relaxed to serve this.

**Its rows SHALL be the rows the list returns** — the same summary, carrying the booker in the
same three stated conditions — so that one composition serves both and no second description of a
booking exists to disagree with the first.

**An erased booking SHALL NOT be returned by any search**, because it holds no address to match.
This follows from erasure rather than being enforced separately, and it means a subject's bookings
leave their own results as they are erased.

**The read SHALL be served by an index on the stored address.** Unwindowed and unindexed, it is a
scan of a table that grows without limit — reintroducing the cost the list's window exists to
bound, which would make this a worse trade than the gap it closes.

#### Scenario: A subject's bookings are found by their address
- **WHEN** a caller with sensitive-data access searches for an address two bookings hold
- **THEN** both are returned, with their booker's contact details, whatever dates they fall on

#### Scenario: The search is not windowed
- **WHEN** a booking lies outside any window the list endpoint would accept
- **THEN** it is still returned by a search for its booker's address

#### Scenario: A different address matches nothing
- **WHEN** a caller searches for an address no booking holds
- **THEN** an empty page is returned rather than an error

#### Scenario: Matching is exact
- **WHEN** a caller searches for a fragment, prefix or wildcard form of an address a booking holds
- **THEN** that booking is not returned, and the endpoint offers no parameter that would make it so

#### Scenario: An erased booking is not found by the address it once held
- **WHEN** a booking's booker has been erased and a caller searches for the address it previously held
- **THEN** it is not returned

#### Scenario: Section access alone cannot search
- **WHEN** a backoffice user with section access but without sensitive-data access calls the search
- **THEN** the request is refused, and the refusal does not depend on whether any booking holds the address

#### Scenario: The search is not anonymous
- **WHEN** the endpoint is called without backoffice authentication
- **THEN** the response is 401 and no handler logic executes

#### Scenario: Results page like the list
- **WHEN** more bookings hold an address than fit on one page
- **THEN** the remaining ones are reachable, and the total reported is the number matching rather than the number on the page

#### Scenario: The list's window is untouched
- **WHEN** the list endpoint's parameters are inspected after this read exists
- **THEN** its window is still required and still bounded, and no parameter accepts a booker contact detail
