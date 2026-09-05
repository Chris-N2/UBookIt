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

## ADDED Requirements

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
