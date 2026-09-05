## Context

`booker-erasure` shipped `POST bookings/{id}/erase-booker`, gated on Umbraco's Sensitive data
group. It takes an id. A subject request carries an email address.

The management list cannot be extended to close that gap. `BookingQuery.Create` requires a window
and refuses one wider than `SiteBookingSettings.MaxQueryRangeDays` (default 31), and it does so
**in the constructor, so an invalid query is unrepresentable** — the type's own documentation says
that is the point, and that putting the check in a service would let a caller route around it. A
subject search must be unwindowed, so bolting `bookerEmail` onto that query means making the
window optional and demolishing the guarantee for every caller.

Two facts about the current schema, established by reading it:

1. `uBookItBooking` has indexes on `Reference` (unique) and `(StartUtc, EndUtc)`, and **nothing on
   `BookerEmail`**. An exact-match lookup today is a table scan.
2. `BookerEmail` is `nvarchar(320)`, nullable since `booker-erasure` — and its nullability means
   **erased**. An erased row holds no address, so it cannot match any search.

## Goals / Non-Goals

**Goals:**

- An operator holding a subject's email address can find every booking still holding it.
- The search cannot answer questions for a caller who may not read the values it matches on.
- It cannot become an enumeration tool by increment.
- The unwindowed query is affordable, not merely permitted.

**Non-Goals:**

- Partial matching of any kind; searching by name, phone or member key; bulk erasure; a UI; a
  delivery-API surface.

## Decisions

### D1 — A separate endpoint, not a parameter on the list

Two independent reasons, either sufficient.

**The query is a different shape.** The list is windowed by construction and must stay so.

**The authorization is different, and must be a property of the endpoint.** The list is gated on
section access; this must additionally require the Sensitive data group. Expressed as a parameter,
that becomes a conditional inside a handler — correct only while every future edit remembers it,
and leaving a route that reaches the handler having established nothing. `booker-erasure` settled
this exact question for the erase verb and the reasoning transfers unchanged: **a policy on the
endpoint is a property; an `if` in a handler is a habit.**

It reuses `Constants.SensitiveDataAccessPolicy`, which already composes the section requirement
*and* the sensitive-data requirement, so naming it can only ever narrow.

*Alternative considered:* `GET bookings?bookerEmail=…` with a conditional gate. Rejected on both
counts above.

### D2 — Exact match, and that is a security property rather than a simplification

`contains` or a prefix search answers *"which of your bookers are at this domain"*, which no
erasure request needs and which turns a lookup into an enumeration tool. Exact match answers
*"is this person in your records"* and nothing else.

Stated as a **requirement** rather than left as what happens to be implemented, because "just add
a wildcard" is the obvious next request, it looks like a convenience, and nothing in the code
would look like a removal of a guarantee when somebody grants it.

Comparison follows the **database collation** — SQL Server's default is case-insensitive, which
matches how people treat email addresses and how the domain already stores them trimmed. Stated
rather than configurable: an option here is a second answer to "did these two addresses match".

### D3 — The gate is the same group, and this does not weaken the rule it reopens

`sensitive-data`'s no-filter requirement is restated, not deleted. Its **guarantee** — that the
package never answers a question about a value the caller may not read — is preserved exactly and
made the operative sentence. What changes is that the rule stops naming a mechanism (*no filter,
search, sort or count*) and starts naming the condition that made the mechanism dangerous.

The old form was over-broad relative to its own stated reason, which is entirely about a caller
*without* access. For a caller who may read every address on the page, an exact-match lookup
discloses nothing scrolling would not.

**The restatement must keep a tripwire.** The requirement's second purpose was to make a future
*ungated* filter a visible violation rather than an unremarkable addition, so the new form
requires any endpoint accepting contact details as input to carry sensitive-data access as its
own authorization — which an added query parameter on the list would fail.

Also carried forward unchanged: **A withheld booking still appears in results** — rows are never
removed from a listing to hide their details.

### D4 — The index is a precondition, not an optimisation

An unwindowed exact-match search without an index is a full table scan of a table that grows
without limit — reintroducing precisely the cost the window guard exists to bound, which would
make the change a worse trade than the gap it closes. With an index it is O(that person's
bookings).

**Worth stating plainly, since it reads oddly:** this adds an index on personal data in order to
build the tool that removes personal data. It is fine — an `UPDATE` maintains the index with the
row, so an erased booking leaves the index the moment its address does — and it is written down so
that a reader meets the reasoning rather than the surprise.

### D5 — The search is bounded by the act it enables

A search matches on the stored address. Erasure nulls it. So a subject's bookings leave their own
search results as they are erased, and a second search returns nothing — the feature cannot
accumulate a standing index of who has asked to be forgotten.

The corollary is a **boundary the docs must state**: this finds bookings made with *that* address.
Somebody who booked twice under two addresses has one of them found. Nothing can fix that, and
implying completeness would be worse than stating the limit.

### D6 — The result is a page of the same rows the list returns

Same `BookingSummary` and the same three-state booker, so one mapping composes both responses and
the erased/withheld/shown rendering already built applies unchanged. A bespoke row type would be a
second description of a booking, free to disagree with the first.

Paged for the same reason the list is: a prolific booker is not a bounded result set.

## Risks / Trade-offs

- **The restatement is read as permission to add filters.** → The new requirement names the
  authorization condition explicitly and keeps the "stated rather than left as an accident"
  clause, so an ungated filter still violates it in writing.
- **Somebody adds `contains` later.** → Forbidden by requirement with its reason attached, and
  the endpoint's parameters are guarded.
- **The index changes query plans for existing reads.** → It is an additional non-clustered index
  on one column; the availability path is served by `(StartUtc, EndUtc)` and its plan is already
  covered by a QA gate. To be re-checked during apply rather than assumed.
- **A wholesale requirement replacement drops a guarantee.** → `sensitive-data`'s requirement has
  two scenarios and both must survive; the guarantee diff goes in `tasks.md` §8, and the
  requirement's end is established before it is replaced. Both are recorded failures from
  `booker-erasure`.
- **The search is the obvious place to bolt bulk erasure onto.** → Named as a non-goal now, so
  adding it is a decision rather than a slide.
