## Why

A booking arrives by telephone, at a reception desk, or in a corridor. Today the package
has no way to record it: every placement path in the product is a *visitor* placing their
own booking, and an operator holding a customer on the phone can only ask them to go to
the website. A site whose bookings mostly arrive that way cannot use uBookIt at all.

The domain is already most of the way there. `move-booking` (17.1.0, unreleased) introduced
`PlacementTerms.Operator` — a lead of zero and no horizon — and *Moving a booking* says in
terms why:

> **That relaxation is a property of operator placement, not of moving**, and it is stated
> here so that recording a booking on a customer's behalf, when that arrives, inherits it
> rather than inventing a third set of terms.

This change is that arrival. It spends the value rather than adding a second one.

## What Changes

- **An operator places a booking from the bookings screen**, choosing a service or a
  resource, a date, a start and a length, and entering the booker's contact details. The
  booking is an ordinary booking in every later respect: same reference format, same
  status machine, same claims, same cancellation, confirmation, move and erasure.

- **Operator terms are extended from two waivers to three, and each keeps its existing
  home.** Lead time and horizon are waived by `PlacementTerms.Operator`, which is reused
  unchanged. `AutoConfirm` is waived by a new member on that value, so that the single
  site deciding a new booking's status keeps deciding it. `DirectlyBookable` is waived
  **structurally**, by which entry point the caller reaches — the operator's direct
  placement does not contain the check — preserving the existing rule that "this is a
  direct booking" must never become a parameter a caller can get wrong.

- **Two entry points, mirroring the two visitor flows.** `IBookingService` gains operator
  placement of a named resource; `IServiceBookingService` gains operator placement of a
  service and is the operator's single entry point for one, so a service's duration rules —
  which live a layer above the booking service — bind an operator exactly as they bind a
  visitor. This repeats, deliberately, the shape `move` arrived at.

- **An operator's placement lands `Confirmed`**, even on a site whose `AutoConfirm` is off.
  Approval exists to police strangers; the operator placing the booking has approved it.

- **An operator's placement tells the booker and nobody else.** `BookerPlaced` is sent as
  for any placement; `InternalPlaced` is **not**, on exactly the reasoning the capability
  already gives for confirmation, decline and a move — the site's own people just performed
  the action, and the bookings screen is where its state lives.

- **A new management endpoint**, gated on the section, on `UBookIt.Bookings.Manage`, **and
  on sensitive-data access**, since it accepts a booker's contact details as input.

- **A second, small management read — what there is to book — gated on `UBookIt.Bookings.Manage`
  alone.** Found necessary during apply and recorded here rather than left as an implementation
  detail, because it is a permissions decision: listing resources and listing services both
  require `UBookIt.Configure`, which the person taking a telephone booking has no reason to
  hold, so without it the placement dialog is empty for exactly the user it exists for. The
  alternatives were to grant receptionists the configuration privilege or to drop the feature.
  It carries names and ids only — no open hours, constraints, capabilities or roles — and
  nothing about any person, so it needs no sensitive-data gate.

- **No new message kind, no new failure code, no migration, no schema change.** The booker
  receives the message a placement has always sent.

- **The observation port gains one member, `BookingPlacedOnBehalfAsync`, with a default
  implementation that reports an ordinary placement** — so it is additive rather than breaking,
  and a host that implemented the port before this change keeps compiling and keeps being told
  that a booking was placed. It is needed because the booking deliberately carries no marker of
  who placed it, which makes the moment of placing the only moment that fact exists.

### Deliberately not changed

- `PlacementTerms` is **widened, not broken**: it shipped in no release, so the addition is
  free — but only until 17.1.0 publishes. Stated here so the window is a decision rather
  than an accident.
- A booker's email remains **required**. The walk-in with no email address is a real case
  and is not served here; serving it would reopen `Booker`'s two-state invariant and every
  notification guarantee that rests on it.

## Non-goals

- **No availability picker.** The operator types a time and is told whether it can be
  taken, exactly as `move` does. Offering bookable starts under operator terms would push
  `PlacementTerms` into the read side that the public delivery API shares, which is a
  larger and riskier change than this one. The natural home for a "what is free on
  Tuesday?" read is the backoffice scheduler, not this modal.
- **No choosing which resource fulfils a service role.** The resolver assigns, as for a
  visitor. Pinning a role's resource (`PinnedResourceId` already exists on the request) is
  a plausible fast-follow and is not built here.
- **No member linkage.** `Booker.MemberKey` stays absent for an operator placement; picking
  an Umbraco member is its own feature.
- **No "notify the booker?" choice.** The booker is told, or the site has not configured
  booker emails and nobody is. An operator-suppressible send would make "was the customer
  told?" unanswerable from the booking.
- **No narrowing of the sensitive-data input rule.** See below.
- **No back-dating.** A start that has already passed is refused with `lead-time`, as for a
  move. Recording a booking that already happened is not placement.

### A deferred obligation this change creates

`sensitive-data` requires every endpoint **accepting** contact details to carry sensitive-data
access, and its guard enforces that mechanically over every endpoint parameter. The rule's
stated reason is entirely about an *oracle* — a caller who may not read an email confirming one
by watching whether a row comes back — and a create endpoint answers no question about an
existing value. Distinguishing "accepts a detail as a query term" from "accepts one to store"
would let a receptionist take bookings without also being granted the whole customer database.

**That narrowing is deliberately not attempted here.** It reopens a constraint the capability
says was "stated rather than left as an accident of what has been built", and it deserves its
own change with its own explore. Until then this endpoint carries the broader gate, and the
over-broad grant is a known, recorded cost rather than an oversight.

## Capabilities

### New Capabilities

None. Every behaviour here extends a capability the package already has.

### Modified Capabilities

- `bookings`: operator placement as a first-class domain operation, its terms, and the
  status it produces; the direct-bookability rule restated for a world with two
  single-resource entry points rather than one; and the **status machine**, which today
  says a placement's status "SHALL be derived from the site's `AutoConfirm` setting" —
  a sentence an operator's placement falsifies, found by the sibling sweep rather than by
  anything that looked like a deletion.
- `service-booking`: operator placement of a service, as the operator's single entry point,
  applying the service's length rules exactly as its move does.
- `booking-management`: a new endpoint that places a booking on a booker's behalf, and the
  bookings view's control and modal for it.
- `booking-emails`: *Which events produce messages, and for whom* — placement splits, so
  that a visitor's placement addresses both directions as it always has and an operator's
  addresses the booker only.
- `permissions`: `UBookIt.Bookings.Manage` gains creating a booking on a booker's behalf,
  and that endpoint's sensitive-data gate joins the verb rather than replacing it.
- `sensitive-data`: *Withheld data SHALL NOT be reachable by asking about it* — the exact-match
  obligation is stated as binding an endpoint that **matches on** a contact detail, since an
  endpoint that accepts one **to store** has nothing to match. The gate itself is unchanged
  and still binds both.

## Impact

**Code**

- `UBookIt.Core`: `PlacementTerms` gains an approval member; `IBookingService` and
  `IServiceBookingService` each gain an operator placement operation — **two declared port
  breaks**, additive in shape but new members on published interfaces, landing in a minor
  with the upgrade note in `docs/configuration.md` beside `move`'s three.
- `UBookIt.Backoffice`: a new action on `BookingsController`; a request model carrying the
  booker's details; the client's second custom modal, opened from a toolbar control rather
  than a row.
- `UBookIt.Web`, `UBookIt.Persistence`: unchanged. No migration; the store's existing
  atomic placement is what an operator placement uses.

**Guards that must be told about a new endpoint by hand, none of which fail helpfully**

- `PermissionsTests` classification map;
- `SensitiveDataRedactionTests` — `recordedActions`, `recordedContactParameters`, and the
  recorded redaction snapshot;
- `BackofficeDocumentationTests` capability-summary route map.

**Documentation**

- `docs/notifications.md` gains the operator-placement case — and, since this change touches
  that document, discharges the recorded obligation that a sentence pinned there appears
  twice, so its guard pins nothing (`SaysOnce` plus a paraphrase).
- `docs/configuration.md`: the port additions.

**Version**

17.1.0, batched with `admin-settings-screen` and `move-booking`. Nothing publishes here, and
`Directory.Build.props` still declares `17.0.1` deliberately.
