## Why

Every backoffice user who can open the uBookIt section can currently read every booker's
name and email address. Section access is the only gate, and it is the wrong shape for this
question: it answers *may you use uBookIt at all*, not *may you see the contact details of
people who booked*. A site that wants a receptionist to work the bookings list has no way to
grant that without also handing over the mailing list.

Umbraco already ships the instrument — a built-in **Sensitive data** user group — so the
package does not need to invent a parallel one, and an editor who has marked a document-type
property as sensitive already knows the concept.

This is roadmap `0.2.0`, and it is first deliberately. Everything that protects personal data
comes before anything that sends it: `0.3.0` adds retention and erasure and `0.5.0` starts
emailing bookers, and both are easier to reason about once "who may see this" has an answer.

## What Changes

- **A new package-wide rule**: booker contact details are shown to a backoffice user only
  when that user has Umbraco's sensitive-data access. Withholding happens **server-side**,
  before the response is composed — never by a client that received the data and declined to
  render it.
- **BREAKING (unpublished)**: `BookingModel`'s flat `BookerName` and `BookerEmail` strings are
  replaced by a single nullable `Booker` object. `null` means *withheld from you*; it can
  never mean *this booking has no booker*, because a booking cannot exist without one. This
  mirrors the shape already used for `Service` in the same model and for the same reason: a
  fact about the whole is expressed in the shape rather than as a rule that two fields must be
  blank together. Nothing is published until `17.0.0`, so the compatibility promise is not
  engaged.
- **The decision becomes a required input to mapping**, not a step applied afterwards. A
  `BookingModel` cannot be produced without stating whether contact details may be seen.
- **The backoffice list renders a withheld row legibly** — a localized placeholder rather than
  an empty cell — and tells the operator why, once per page, where a row is withheld. The row
  stays identifiable by its quotable reference.
- **The cancel confirmation and the per-row cancel control identify a booking by its
  reference**, for every user, rather than by the booker's name.
- **A guard that enumerates the class, not a sample**: a check over the response model's
  properties, so a contact-detail field added later cannot reach an unauthorized caller
  silently.
- **Documentation of the membership gotcha**: Umbraco's installer seeds only the original
  super user into the Sensitive data group, so a second Administrator created later sees
  every row withheld. Undocumented, that arrives as a bug report against uBookIt.

## Capabilities

### New Capabilities
- `sensitive-data`: who may see personal data the package holds, how the package decides,
  where the decision is enforced, what a withheld value looks like to a caller, and the
  guard that keeps a newly added field from bypassing the rule. Stated package-wide rather
  than per screen, because `0.3.0` and `0.5.0` will each add a surface that must obey it.

### Modified Capabilities
- `booking-management`: the management endpoint's response shape (booker contact details
  become a single nullable object, present only for a caller permitted to see them), and the
  backoffice collection view's rendering of a withheld row.

## Non-goals

- **The management read port is not changed.** `BookingSummary` keeps carrying the booker's
  name and email. The port answers *what is stored*; who is asking is an HTTP-layer question,
  and pushing it into Core would put an Umbraco identity type into a project that keeps
  Umbraco types out.
- **Phone number and member key are not added.** They are stored and rehydrated into `Booker`
  but have never reached the management port, so there is nothing to withhold. The obligation
  here is the guard that stops either arriving unguarded later — not a redaction of something
  currently exposed.
- **Notification handlers are not filtered.** `BookingPlacedNotification` and friends carry the
  whole `Booking`, contact details included, to server-side code the site wrote. That is the
  point of them: `docs/notifications.md` tells a site to email the booker from there, and
  `0.5.0` will do exactly that. Sensitive-data access is a property of a signed-in backoffice
  user, and there is no such user in a notification handler.
- **The delivery API and the front-end views are untouched.** The delivery API has no booking
  read endpoint; booker details appear only in the response to the POST that submitted them,
  and in the Razor confirmation views — in both cases to the person who just booked, about
  themselves.
- **No finer granularity than Umbraco's group.** Sensitive-data access is all-or-nothing per
  user and we cannot create, rename or reconfigure the group. Per-field or per-resource
  visibility is not proposed; the roadmap's `0.9.0` permissions work is where granularity
  within the section is decided, if at all.
- **Not a booker search.** Nothing here adds a way to find a booking by name or email; the
  absence of such a filter is promoted from an accident to a stated constraint, because a
  filter over withheld data would let an unauthorized caller confirm a value by probing.

## Impact

**Code**
- `src/UBookIt.Backoffice/Models/BookingModels.cs` — new `BookerModel`; `BookingModel.Booker`
  replaces two string properties. **Breaking to the generated client.**
- `src/UBookIt.Backoffice/Mapping/BookingModelMapper.cs` — mapping takes the visibility
  decision as a required parameter.
- `src/UBookIt.Backoffice/Controllers/BookingsController.cs` — resolves the current user and
  asks Umbraco whether they have sensitive-data access.
- `src/UBookIt.Backoffice/Client/src/` — regenerated API types; `bookings-list.element.ts`
  renders the withheld placeholder and the explanatory note; the cancel control and its
  confirmation switch to the reference; new `en-US` localization entries.
- `tests/UBookIt.Tests/` — endpoint tests for both user shapes, a mapper test, and the
  reflection guard over the response model.

**No change**: `UBookIt.Core`, `UBookIt.Persistence`, `UBookIt.Web`, the database schema (no
migration), and every delivery endpoint.

**Dependencies**: none added. `IUser.HasAccessToSensitiveData()` and `IAuthorizationHelper`
are already available through the Umbraco packages the Backoffice project references, and
`UBookItSectionHandler` already resolves an `IUser` by the same route.

**Docs**: `docs/backoffice.md` gains the Sensitive data group and the super-user gotcha.
`docs/notifications.md` gains a sentence stating that the notification payload is deliberately
unfiltered, so its existing description of what a booking carries is not read as an oversight.
