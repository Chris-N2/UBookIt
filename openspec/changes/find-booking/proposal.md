## Why

The bookings screen cannot find a booking from anything a caller can actually tell you. It
offers a date window, a status filter and a resource filter — so an operator on the telephone
with somebody who says *"it's Ada, I booked something next week"* has to guess the week and
scroll. The confirmation email carries a **reference** designed to be read down a phone, and the
package already has a **find-by-email** read that was built, spec'd and QA'd across four rounds —
and neither is reachable from the screen. Two of the three routes to Ada's booking exist and
one of them has no user interface at all.

## What Changes

- **A booking can be found by its reference.** A new management read and endpoint,
  `GET bookings/by-reference/{reference}`, gated on the bookings *read* verb — a reference is not
  personal data, it is the thing designed to be quoted — returning the booking as **the same row
  the list returns**, so contact details are withheld or shown by the identical rule. It is a
  unique-index seek and therefore bypasses the list's window *legitimately*: the window guard
  exists to bound scan cost, and a key lookup incurs none.

- **The existing find-by-email read gets a screen.** No endpoint change. Its stated purpose —
  *"so an erasure request can be honoured"* — widens honestly to include an operator finding a
  caller's bookings; its guarantees (sensitive-data gate on the action, exact match, unwindowed,
  paged, indexed) do not move.

- **One Find control on the bookings view**, dispatching by the shape of what was typed: a
  reference goes to the reference lookup, an email address to find-by-email. The email route is
  *offered* only to users who hold sensitive-data access — never a control that would always be
  refused. **The table becomes the result set**, with a status line saying what is shown and a
  *Back to dates* control that restores the window view, so one mental model serves both
  lookups and every row action keeps working on a found row.

- **A sentence the spec makes about itself becomes false and is restated.** *The list is
  windowed, and the window is bounded* says locating a booking by reference "is a different
  query with different indexing, and is not provided by it." After this change it is provided —
  by a separate read, exactly as `find-by-booker` was added beside the same sentence. The
  reference clause gets the treatment the email clause already got.

- **One new stable failure code, `reference-invalid`**, for input that is not a well-formed
  reference. Distinct from `booking-not-found` because they call for different corrections:
  *"you mistyped it"* versus *"no booking has that reference"*.

### Deliberately not changed

- **Search by booker name is not built**, and the decision is recorded with its reasoning
  rather than deferred in silence: exact-match on a name is nearly useless, anything useful is
  substring, and substring over a contact detail is precisely what `sensitive-data` bans by
  name. The defensible shape — a *windowed* substring filter, for a caller who could already
  read every row in the window — exists and is worked out, but it needs that rule reopened
  deliberately and its own change. Chris: *"we might not need it at all, unless somebody has
  forgotten their email address."*
- **The list's window is untouched.** Not optional, not relaxed, not widened. Both lookups are
  separate reads for the reason `find-by-booker` recorded.
- **No delivery-API lookup.** A reference on the anonymous side is a bearer token over 27⁸ and
  belongs to the self-service-cancellation explore, not here.

## Non-goals

- No search by name (above).
- No partial or prefix reference matching. A partial reference is not a reference; the parser is
  `BookingReference.TryParse` and nothing looser.
- No change to what a row shows to whom. Both lookups return list rows and inherit the list's
  withholding rule without restating it.
- No new verb. A by-reference read is *"every read over bookings"*, which `UBookIt.Bookings.Read`
  already means.
- No cross-window "highlight in place". A hit always becomes a result set; two behaviours for one
  control would be one too many, and a highlight has no keyboard story without more work.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `booking-management`: two ADDED requirements (the by-reference read and endpoint; the view's
  Find control and result-set model) and two MODIFIED — *The list is windowed, and the window is
  bounded* (the reference clause, restated the way the email clause was) and *A subject's
  bookings can be found by their email address* (purpose widened to operator lookup; guarantees
  unchanged; the view now reaches it).

Sibling sweep, done at explore time: `booker-erasure` carries an HTML comment citing the
"not provided" sentence as the reason a scenario was reworded — a claim that goes stale, edited
directly since it is not a requirement. `permissions` needs nothing: the Read verb is defined as
"the bookings list and every read over bookings". `sensitive-data` needs nothing: a reference is
not a contact detail, and the email read is unchanged.

## Impact

**Code**

- `UBookIt.Core`: `IBookingManagementStore` gains `FindByReferenceAsync` — **a declared port
  addition** on a published interface, in a minor, with the upgrade note in
  `docs/configuration.md`. One new `FailureCodes` member.
- `UBookIt.Persistence`: the store implementation — a seek on the existing unique index, no
  migration.
- `UBookIt.Backoffice`: one new action on `BookingsController`; the client's Find control,
  result-set mode and status line; the API client regenerated for the new endpoint.
- `UBookIt.Web`: unchanged.

**Guards that must be told by hand** — the same set as every new management endpoint, none of
which fail helpfully: `PermissionsTests` classification map; `SensitiveDataRedactionTests`
`recordedActions` (a **read**, taking a reference — which is not a contact detail and must not be
recorded as one); `BackofficeDocumentationTests` route map, now keyed by method and route; and the
`booking-management` Purpose paragraph, which lists the capability's verbs.

**Documentation**: `docs/backoffice.md` gains the Find control; `docs/configuration.md` the port
addition.

**Version**: 17.1.0, batched. Nothing publishes here.
