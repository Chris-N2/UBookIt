## ADDED Requirements

### Requirement: A booking can be found by its reference
The management read port SHALL provide a read that returns the booking holding a given
reference, and the package SHALL expose it over a **versioned** backoffice endpoint in the same
swagger group as its other management endpoints, **gated by the bookings read verb** — a reference
is not personal data, it is the identifier designed to be quoted, so the read discloses nothing
the list would not.

**It SHALL return the row the list returns.** The same summary, carrying the booker in the same
three stated conditions, so that a found booking withholds or shows contact details by the
identical rule and no second description of a booking exists to disagree with the first.

**It SHALL be unwindowed, and that SHALL be legitimate rather than a relaxation.** The list's
window exists to bound the cost of a scan over a table that grows without limit. A reference is
a unique key, served by a unique index; its read is a seek, and a seek incurs none of that cost.
The window requirement is untouched — see *The list is windowed, and the window is bounded* —
and this read SHALL NOT be reachable through it.

**It SHALL accept a reference as a person types it** — any case, with or without the display
separator — and SHALL match the canonical form exactly. A partial reference is not a reference:
the read SHALL offer no prefix, substring or wildcard form, because the parser defines what a
reference is and nothing looser does.

**A miss and a malformed input SHALL be distinguishable.** Input that is not a well-formed
reference SHALL fail with a stable code of its own, `reference-invalid`; a well-formed reference
no booking holds SHALL fail with `booking-not-found`. They call for different corrections — *"you
mistyped it"* and *"there is no such booking"* — and an operator on the telephone needs to know
which.

**It SHALL ignore the list's status filter.** A caller who quotes a reference wants *that*
booking, whatever its status; a cancelled booking is still the booking they asked about.

**An erased booking SHALL still be found by its reference.** Erasure removes the person, not the
booking, and `booker-erasure` guarantees the reference survives so that an operator can match
what a caller reads out — this read is what that guarantee was written for.

#### Scenario: A booking is found by its reference
- **WHEN** a caller with the bookings read verb looks up the reference a booking holds
- **THEN** that booking is returned as the list would return it, whatever dates it falls on

#### Scenario: The reference is accepted as typed
- **WHEN** a caller looks up a reference in lower case, or with the display separator, or without it
- **THEN** the same booking is returned

#### Scenario: A malformed reference is distinguishable from a miss
- **WHEN** a caller looks up a value that is not a well-formed reference, and separately a well-formed one no booking holds
- **THEN** the first fails with `reference-invalid` and the second with `booking-not-found`

#### Scenario: A partial reference finds nothing
- **WHEN** a caller looks up a prefix or fragment of a reference a booking holds
- **THEN** it is not returned, and the endpoint offers no parameter that would make it so

#### Scenario: Contact details are withheld by the list's rule
- **WHEN** a caller without sensitive-data access looks up a reference
- **THEN** the booking is returned with its contact details withheld, exactly as the list would show it

#### Scenario: A cancelled booking is still found
- **WHEN** a caller looks up the reference of a cancelled booking
- **THEN** it is returned, with its status

#### Scenario: An erased booking is still found
- **WHEN** a caller looks up the reference of a booking whose booker was erased
- **THEN** it is returned, showing that the booker was erased

#### Scenario: The read is not anonymous
- **WHEN** the endpoint is called without backoffice authentication
- **THEN** the response is 401 and no handler logic executes

### Requirement: The bookings view can find a booking
The bookings view SHALL offer a single **Find** control that locates a booking from what a
caller can say: a reference, or the email address they booked with. The view SHALL decide which
read to use from the shape of what was typed, and SHALL NOT ask the operator to choose a mode.

**The email route SHALL be offered only to a user who holds sensitive-data access**, and the
screen's judgement SHALL NOT be the rule — the endpoint refuses independently. A user who does
not hold it SHALL be told, in a sentence, that finding by email needs the Sensitive data group,
so that they know who to ask rather than concluding the feature is broken.

**The shape test SHALL be a convenience, never the rule.** A value the view classifies as a
reference and the server refuses SHALL be shown the server's own code, in the operator's words.
The view's reference-shape rule SHALL agree with the domain's parser, and that agreement SHALL be
asserted rather than assumed.

**A successful lookup SHALL replace the list with the result set**, rendered through the same
rows with the same actions, under a status line stating what is shown — the reference, or the
address and that all dates are included — and offering a **Back to dates** control that restores
the window view. Every action the list offers on a row SHALL work identically on a found row,
and after such an action the lookup SHALL be re-run rather than the window, so a booking moved
to another date is still shown as the booking that was found.

**A miss SHALL be a sentence, not an empty table.** An empty table under a window means nothing
was booked; under a lookup it would mean no such booking, and the two are indistinguishable. A
reference no booking holds, and an address no booking holds, SHALL each be stated in the status
line with the Back control and no table.

**The control SHALL be labelled, keyboard operable and programmatically associated with any
error**, on the same terms as the rest of the section, and Back to dates SHALL place focus
deliberately on the window controls rather than losing it to the document.

#### Scenario: An operator finds a booking by its reference
- **WHEN** an operator types a reference into the Find control and activates it
- **THEN** the table shows that one booking, whatever window was showing, under a status line naming the reference, with Back to dates offered

#### Scenario: An operator finds a caller's bookings by email
- **WHEN** an operator holding sensitive-data access types an email address and activates Find
- **THEN** the table shows every booking that address holds, all dates, paged as the list is, under a status line naming the address

#### Scenario: A user without sensitive-data access is told why email is unavailable
- **WHEN** a user without sensitive-data access types an email address and activates Find
- **THEN** no request is issued, and the view states that finding by email needs the Sensitive data group

#### Scenario: Something that is neither is refused in place
- **WHEN** an operator types a value that is neither a reference nor an email address
- **THEN** no request is issued, and the view says so

#### Scenario: A miss is stated rather than shown as an empty table
- **WHEN** a lookup finds nothing
- **THEN** the status line says no booking has that reference, or no bookings hold that address, and no table is rendered

#### Scenario: A found row keeps its actions
- **WHEN** an operator moves a booking found by its reference to a date outside the window that was showing
- **THEN** the move succeeds, and the found booking is still shown with its new date

#### Scenario: Back to dates restores the window view
- **WHEN** an operator activates Back to dates after a lookup
- **THEN** the window, filters and list return exactly as they were, and focus lands on the window's first control

#### Scenario: The shape rule agrees with the parser
- **WHEN** the view's reference-shape rule and the domain's parser are given the same inputs
- **THEN** they accept and reject the same ones

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
not known cannot be found **by this read**. Locating a booking from a booker's name is a
different query with different indexing, and is not provided by it.

**Locating a booking by its reference IS provided, as a separate read of its own** — see *A
booking can be found by its reference*. It is separate for the same reason the email read is:
this requirement's window is not optional and SHALL NOT be made so to accommodate it. A
reference is a unique key, so its read is a seek that incurs none of the cost the window exists
to bound — which is what makes bypassing the window *legitimate* there and would make relaxing
the window here a different thing entirely.

*This paragraph previously said that locating a booking by its reference "is not provided". It
is now, and the sentence is corrected rather than left for a reader to notice against the
requirement that provides it.*

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

#### Scenario: The window still cannot be omitted after the reference read exists
- **WHEN** the list endpoint's parameters are inspected after the by-reference read exists
- **THEN** its window is still required and still bounded, and it accepts no reference parameter

### Requirement: A subject's bookings can be found by their email address

The management read port SHALL provide a read that returns the bookings whose booker holds a
given email address, and the package SHALL expose it over a **versioned** backoffice endpoint in
the same swagger group as its other management endpoints.

**It SHALL serve two purposes, and its guarantees SHALL NOT differ between them.** It was built
so that an erasure request — which arrives as an address, not a date — can be honoured. It also
serves an operator finding a caller's bookings from the address they booked with, which is the
same question asked for a different reason. Nothing below is relaxed for the second purpose:
the gate, the exactness, the absence of a window and the paging are properties of the read, not
of why it was called.

**The bookings view SHALL reach it** — see *The bookings view can find a booking*. A read that
exists only as an endpoint serves an erasure workflow and nobody else; an operator on the
telephone was never going to compose a request by hand.

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

#### Scenario: An operator's lookup gets exactly the erasure lookup's answer
- **WHEN** the same address is searched from the bookings view and by a caller honouring an erasure request
- **THEN** the results, the gate, the matching rule and the paging are identical, because they are one read
