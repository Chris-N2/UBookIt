## Why

A data subject's erasure request arrives as an **email address**, and the package deliberately
offers no way to find a booking from one. `booker-erasure` shipped the verb; an operator can only
use it on a booking they have already located, and the management list is windowed to 31 days and
filterable only by date, status and resource. Honouring "please remove my details" therefore means
guessing when somebody booked, or paging through months.

That gap is documented as a boundary in `docs/backoffice.md` rather than hidden, but it is the
half that makes the feature usable, and the `booking-management` capability already anticipates
it: *"Locating a booking from a booker's name, email or reference is a different query with
different indexing, and is not provided here."* This change provides it.

**It requires reopening a requirement, deliberately and with sign-off.** `sensitive-data` says
*"No endpoint SHALL offer a filter, search, sort or count over booker contact details."* Read its
stated reason, though: *"such a facility answers questions about values the caller was not
given"* — the whole argument is about a caller **without** sensitive-data access. When it was
written no filter existed and no gated case was in view, so the letter reaches further than the
reasoning. For a caller already permitted to read every email on the page, an exact-match lookup
discloses nothing they could not obtain by scrolling.

## What Changes

- **A search that finds every booking for one email address**, exposed as its own backoffice
  endpoint rather than as a parameter on the existing list.
- **Gated wholesale on Umbraco's Sensitive data group**, as a policy — the same gate erasure
  already carries. Not a conditional inside a handler on an otherwise section-gated endpoint: a
  parameter whose authorization differs from its endpoint's is a rule somebody has to remember.
- **Exact match only.** No prefix, contains, fuzzy, sort or count-only form. Exact match answers
  the question the law actually asks — *"is this person in your records?"* — and answers nothing
  else. A `contains` search over email addresses is an enumeration tool wearing the same clothes:
  *everyone at a given domain*, which is a different capability nobody asked for.
- **Unwindowed, because it must be.** A subject request carries no date. This is why it cannot be
  a parameter on the list endpoint: `BookingQuery` refuses a window wider than
  `MaxQueryRangeDays` **by construction**, and making that optional would demolish the guardrail
  for every caller to serve one.
- **An index on the booker's email column**, which is what makes the unwindowed lookup
  affordable. Without it the search is a table scan, which reintroduces the very cost the window
  guard exists to bound.
- **Erased bookings are found by their reference, not by an address they no longer have.** A
  search returns only bookings still holding that email, so erasing a subject's bookings removes
  them from their own search results — the feature's usefulness is bounded by the act it exists
  to enable.
- **`sensitive-data`'s no-filter requirement is restated** to forbid what it was protecting —
  answering questions about values *the caller may not read* — rather than to forbid a mechanism.

## Capabilities

### New Capabilities

*(none — this extends existing capabilities rather than introducing a concept of its own. The
search is a management read, and who may perform it is a sensitive-data question; giving it a
capability of its own would split one rule across two places.)*

### Modified Capabilities

- `sensitive-data`: **Withheld data SHALL NOT be reachable by asking about it** currently forbids
  any filter, search, sort or count over contact details, at any endpoint. It is restated to
  forbid answering such a question to a caller who may not read the values, and to require that
  any endpoint accepting contact details as input carries sensitive-data access as **its own**
  authorization, matches exactly, and offers no partial, prefix, sort or count form.
- `booking-management`: **Bookings can be enumerated for management** records that locating a
  booking from an email "is not provided here". The port gains a second read that does provide
  it, on its own terms. A new requirement covers the endpoint.
- `persistence`: **Schema shape and naming** gains an index on the booker email column, and the
  store requirement gains the lookup's contract.

## Non-goals

- **Search by name, phone or member key.** A name is not unique and does not identify a data
  subject; a phone number is not what a request arrives as. Adding either widens the enumeration
  surface for no gain the request needs.
- **Any partial-match form.** Stated as a non-goal rather than merely left unbuilt, because
  "just add `contains`" is the obvious next request and it is the one thing that turns this from
  a lookup into an enumeration tool.
- **Erasing everything the search finds, in one call.** A bulk irreversible destruction needs its
  own design and its own confirmation; this change makes the bookings findable, and each is then
  erased through the verb that already exists.
- **Exposing the search on the delivery API.** It is a backoffice operation over personal data;
  the delivery API is anonymous.
- **A UI for it.** Same reasoning as `booker-erasure`'s missing erase button — the endpoint is
  the deliverable, and a screen that searches for people needs its own thinking.
- **Case-sensitivity as a configurable.** Email comparison follows the database collation, which
  is stated rather than made an option.

## Impact

- `UBookIt.Core`: `IBookingManagementStore` gains a lookup; a small result type. **BREAKING —
  published port**, on the same terms as `booker-erasure`'s addition to `IBookingStore`.
- `UBookIt.Persistence`: `SqlBookingManagementStore` implementation, plus **one additive
  migration** adding the email index. No column changes.
- `UBookIt.Backoffice`: one endpoint on `BookingsController`, carrying the existing
  `SensitiveDataAccessPolicy`; response models reusing the three-state booker.
- `UBookIt.Web`: untouched — to be re-verified by enumeration during apply rather than assumed.
- Docs: `docs/backoffice.md`'s erasure section states that finding a person's bookings is now
  possible and what it does and does not match.
