## Why

The shipped confirmation view says this to every visitor:

```razor
<dt>Reference</dt>
<dd>@Model.BookingId</dd>
```

That is a `Guid`. We already call it a reference; we simply made it one nobody can use. A
booker cannot read `4f2a8c11-9e3d-4b7a-8c21-5d6e7f809a1b` down a phone, copy it into an email,
or quote it at a desk — and those are the only things a reference is for. Chris noticed it in
the installed site within minutes of looking.

It is small, but three things lean on it and none can be done well first:

- **Emails** (planned, as a satellite package over the notifications we already publish) have
  to name the booking in the subject line. A Guid in a subject line is a support problem.
- **Finding a booking without knowing its date** — on `docs/mvp.md`'s explicitly-not-v1 list —
  is a search, and a reference is the thing people search by.
- **Erasure** (planned) removes the booker's name and email. What remains still occupies a
  slot and still has to be discussable: *"what is in room 2 at three o'clock?"* The reference
  is the handle that outlives the person.

It is also a **model change**, which is why it is being done now rather than later: CLAUDE.md's
compatibility promise starts at publication, the public API is deliberately still moving at
`0.1.0`, and adding a required identifier to a booking after 1.0 would be a breaking change
nobody wants to make.

## What Changes

- **Bookings gain a reference**: a short, random, case-insensitive, unambiguous string, unique
  within a site, assigned at placement and never changing afterwards.
- **The Guid stays.** Two identifiers, on purpose: the Guid remains the primary key, the API
  identifier and the thing URLs and payloads carry; the reference is for people. Replacing the
  Guid would break every consumer for a readability gain that does not need it.
- **The visitor is shown the reference** on the confirmation, in place of the Guid.
- **The operator sees it** in the backoffice bookings list, because answering the phone is the
  case this exists for.
- **Existing bookings are backfilled** rather than deleted (see design D5) — unlike ⑰, where
  the missing value was genuinely unknowable, a reference can simply be generated.

## Non-goals

- **Searching by reference.** The identifier has to exist before anything can look it up, and
  the backoffice search is its own piece of work with its own indexing questions. This change
  makes search possible and does not do it.
- **Emails.** Separately scoped, and deliberately a satellite package: Umbraco already provides
  `IEmailSender`, so uBookIt ships no SMTP configuration of its own.
- **Changing the Guid, the primary key, or any route.**
- **A customer-facing "manage my booking" link.** That needs a capability token, which is a
  different thing from a quotable reference and carries security questions this does not.

## Capabilities

### Modified Capabilities

- `bookings`: a booking gains a second identifier, with rules about its shape, its uniqueness
  and its immutability.
- `default-frontend`: what the confirmation shows a visitor under "Reference" becomes something
  they can use.
- `booking-management`: the backoffice row carries the reference, so an operator can match what
  a caller is reading out.

## Impact

- **Public API — breaking, and called out as CLAUDE.md requires.** `Booking.Rehydrate` is
  public and is the persistence boundary; it gains a required reference parameter. `Booking`
  gains a `Reference` property. The backoffice row model and the confirmation view model gain a
  field. This is the window for that: `0.1.0` says the API may still move, and after 1.0 it
  could not.
- **Schema**: a new non-nullable column with a unique index, plus a data migration that
  backfills existing rows. Additive, per CLAUDE.md's migration convention.
- **Notifications**: `BookingPlacedNotification` and `BookingCancelledNotification` carry a
  `Booking`, so subscribers get the reference without any change to the notification types.

## Decisions taken

- **Random, not sequential** (Chris, 2026-09-01): a counter leaks how many bookings a site has
  taken, needs contention handling, and gains only a friendlier-looking number. Umbraco itself
  uses random keys for media. Settled; design D1 records the shape.
- **The consent tickbox is not part of this change.** Raised in the same conversation and
  correctly belongs to the data-protection work.
