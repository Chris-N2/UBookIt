## Context

The bookings list is the package's only surface that shows one person's contact details to
another. `GET /ubookitbackoffice/api/v1/bookings` returns `BookerName` and `BookerEmail` on
every row, and the only gate in front of it is the uBookIt section policy added by
`booking-management`. That policy is correct for what it does — `resource-management` already
records that these endpoints return personal data and that is why they authorize on the
package's own section — but it answers a different question from the one this change asks.

Measured before proposing, so none of it need be re-derived:

- **The exposure surface is one endpoint.** `POST /bookings/{id}/cancel` returns only an id
  and a status. The delivery API has no booking read path at all: booker details appear only
  in the response to the POST that submitted them, and in the Razor confirmation views —
  in both cases to the person who just booked, about themselves.
- **`BookingSummary` carries name and email only.** `Booker.Phone` and `Booker.MemberKey` are
  persisted and rehydrated but have never reached the management read port.
- **`ListBookings` filters on window, statuses and resource ids.** There is no booker-name or
  email filter, so there is no oracle to probe.
- **Umbraco's mechanism**: `IUser.HasAccessToSensitiveData()` tests membership of a built-in
  group with a fixed key. The package cannot create, rename or reconfigure that group, and
  does not need to.
- **`UBookItSectionHandler` already resolves an `IUser` per request**, so nothing new is
  needed to know who is asking.

The constraint that shapes most of what follows: Umbraco's installer places **only the
original super user** in the Sensitive data group. Every later user, administrators included,
starts outside it.

## Goals / Non-Goals

**Goals**

- Booker contact details reach only backoffice users with sensitive-data access, decided and
  applied server-side.
- The withheld state is unambiguous on the wire and legible on screen.
- A future field cannot join the response and reach an unauthorized caller without somebody
  deciding it should.
- An operator who cannot see contact details learns why from the screen, not from a support
  ticket.

**Non-Goals**

- Changing `UBookIt.Core` or the management read port. See the proposal's non-goals; the short
  version is that the port answers *what is stored* and would need an Umbraco identity type to
  answer *who is asking*.
- Filtering notification payloads. There is no signed-in backoffice user in a notification
  handler, and `docs/notifications.md` deliberately tells sites to email the booker from one.
- Any granularity finer than Umbraco's group.

## Decisions

### D1 — Umbraco's Sensitive data group, not a setting of our own

`IUser.HasAccessToSensitiveData()`, called through Umbraco, rather than reading the group key
ourselves or adding a uBookIt permission.

*Alternatives.* A package-defined user-group flag or `appsettings` list would give us
per-field control, and would be a second answer to a question Umbraco already answers — free
to disagree with the sensitive-data marker an editor has already used on a member type. A
hardcoded comparison against `8C6AD70F-…` reimplements one line of Umbraco and inherits none
of its future corrections.

### D2 — Withholding happens in `BookingModelMapper`, as a required argument

`BookingModelMapper.ToModel(BookingSummary summary, BookerVisibility visibility)`.

*Alternatives, and why not.* **A post-processing step on the composed model** is Umbraco's own
shape — `responseModel.ClearSensitiveValuesFor(currentUser)` — and its doc comment states the
weakness plainly: *"Every response model carrying member account state must be passed through
this before it reaches the user, whichever endpoint produced it."* That is a rule somebody has
to remember, on a path whose failure mode is disclosing personal data. A required argument has
nothing to remember. **The store** is a substitutable port with a stated contract, and putting
an identity decision behind it would make every implementation responsible for enforcement.
**The client** is not withholding at all — the values would be in the payload. **An MVC action
filter or serialization contract resolver** would work and would be invisible at the point of
use, which is the wrong property here: this decision should be legible in the code that makes
it.

*A two-valued enum rather than a `bool`.* `ToModel(summary, BookerVisibility.Withheld)` cannot
be read backwards at the call site; `ToModel(summary, false)` can. There is one call site
today, so this is cheap insurance rather than a necessity — but the failure it prevents is
silent and the fix is one small type.

### D3 — `BookerModel? Booker` replaces two flat strings

Null means *withheld from you*.

*Why null is available here.* A null is only usable for withholding where the underlying value
cannot legitimately be absent. The domain requires a booker with a non-empty name and a
well-formed email on every booking, so "no booker" is not a state that exists, and the null
carries exactly one meaning.

*Alternatives.* **Blank strings** are what Umbraco does, and their own doc concedes the cost —
*"a default value in a response is not evidence of the member's actual state"*. They were
constrained by a published contract; we are not. **A server-side placeholder string** puts UI
copy in an API and hands every client a value it may render as though it were a name.
**Omitting the properties by serialization attribute** makes the OpenAPI document describe
fields that sometimes do not exist, which is a contract that cannot be generated from
faithfully. **A separate details endpoint** is a larger design than the problem, and leaves the
list needing a second call per row.

*Cost.* Breaking to the generated TypeScript client and to any consumer of the management API.
Accepted: nothing is published until `17.0.0`, and `delivery-api` has the same
`BREAKING (unpublished)` precedent.

### D4 — Fail closed when the current user cannot be resolved

The controller reads `IBackOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser`. If it is
null, contact details are **withheld**.

The endpoint is already authorized, so a null user should not occur — and "should not occur"
is how a `NullReferenceException` or, worse, a default of `true` ships. The controller's
existing code makes the same argument about a query that "cannot fail" and maps it anyway.
Withholding on an unresolvable user is the only default whose failure mode is a support
question rather than a disclosure.

### D5 — The client derives visibility from the payload, not from Umbraco

The list shows the hidden-details placeholder when `booking.booker == null`, and shows the
explanatory note when any row on the page is null. It does **not** call
`getHasAccessToSensitiveData()` on the current-user context.

*Why not, given that the context is right there.* Two sources answering one question can
disagree, and the payload is the one that reflects what actually happened. A client that asks
Umbraco and gets "yes" while the response withheld the data would render an empty cell with no
explanation — the exact failure this change exists to prevent — and the bug would be invisible
until the two paths diverged. The payload is also sufficient: null already means withheld.

### D6 — The guard is a membership snapshot over the response models

A test asserts that `BookingModel`'s and `BookerModel`'s public property sets equal a recorded
set, failing on any addition or removal and naming the difference.

*What it detects.* Change — not personal data. Nothing in a property's name or type makes it
personal, and a guard claiming otherwise would be the kind of rule that checks a mechanism
while appearing to promise a guarantee. What this buys is that adding a field to the booking
response is impossible to do silently: the author must edit the recorded set, and the comment
attached to it asks the one question that matters.

*Alternatives.* Asserting three named fields are withheld passes unchanged when a fourth
arrives — precisely the case worth catching. An attribute-driven scheme (`[SensitiveData]` on
properties plus a serialization filter) is the more general design and is the wrong size for
one model; it also reintroduces the post-processing weakness of D2.

### D7 — The reference identifies a booking on screen, always

The per-row cancel control's accessible name and the cancellation confirmation both name the
booking's reference, for every operator, rather than branching on whether the booker is
visible.

Besides removing a branch, this is the better identifier: it is unique where a booker's name
may not be, and it is what the customer on the telephone is reading out — which is why
`booking-management` already requires the reference to be the first column.

### D8 — Proposed copy (for approval)

| Key | English |
|:--|:--|
| `ubookitBookings_bookerHidden` | Contact details hidden |
| `ubookitBookings_bookerHiddenNote` | Booker contact details are shown only to backoffice users in Umbraco's **Sensitive data** group. Being an administrator does not grant this on its own. |

Rejected: asterisks or a masked form, which imply a value of a particular length and invite
guessing at it; and "Not permitted", which describes the reader rather than the data and reads
as an accusation.

## Risks / Trade-offs

- **A newly created administrator sees every row hidden and reports a bug.** → The in-view note
  (D8) names the group at the moment of confusion, and `docs/backoffice.md` states the
  installer's behaviour. This is the single most likely support outcome of the change and gets
  two mitigations for that reason.
- **The generated client breaks.** → Regenerate types in the same change; the client is in this
  repo and has no external consumers before `17.0.0`.
- **The guard's recorded set gets updated reflexively, without the decision being made.** →
  The failure message states the question rather than the mechanic, so the person reading it is
  asked "is this personal data, and who may see it?" rather than "update the list".
- **The mapper gate protects one model; `0.3.0` and `0.5.0` will add surfaces.** → The rule is
  written package-wide in the `sensitive-data` capability rather than inside
  `booking-management`, so the next change inherits a requirement rather than a precedent. The
  mechanism does not generalize itself, and the spec says so.
- **Tests could pass with the decision inverted if only one path is exercised.** → Both paths
  are required by the specs and both must be tested; the enum of D2 removes the call-site half
  of that risk.

## Migration Plan

No database migration; no schema change; no persisted data is altered. Deployment is a package
upgrade. The only breaking surface is the management API response shape, which is regenerated
into the backoffice client within this change.

Rollback is reverting the package version. A site that had granted Sensitive data membership
to users keeps that membership; the group is Umbraco's and is unaffected either way.

## Open Questions

1. **The copy in D8** — Chris asked to approve the exact strings. The note's second sentence is
   the one carrying real weight; it can be dropped if the note reads long, at the cost of the
   administrator confusion it exists to pre-empt.
2. **Should the column header change when details are hidden?** Currently "Booker". Leaving it
   is the assumption; a header that changed per page state would be worse.
