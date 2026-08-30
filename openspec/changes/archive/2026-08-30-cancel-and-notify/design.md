## Context

Measured before designing:

- **`UBookIt.Core` has zero `PackageReference` entries.** Not "few" — none. It is a
  dependency-free domain assembly, which is stronger than CLAUDE.md's "keep Umbraco types out
  where practical" and is worth keeping true.
- **`IBookingService.CancelAsync` already exists and works**: it loads the booking, applies
  `Booking.Cancel()` (the status machine refuses anything but `Requested → Cancelled` and
  `Confirmed → Cancelled`), persists the status, and returns the updated booking. Cancelling
  twice fails with `invalid-status-transition`.
- **`SqlBookingStore.UpdateAsync` writes only `Status`.** Cancellation cannot disturb anything
  else on the row.
- **Nothing resolves the concrete `BookingService`**: `IBookingService → BookingService` is
  registered once in `UBookItPersistenceComposer`, and every caller — including
  `ServiceBookingService` — takes the interface.
- **The client already has `confirmDestructive`**, which uses the backoffice's own modal and
  distinguishes *cancelled* from *failed* so a broken confirmation cannot look like a user
  backing out.

## Goals / Non-Goals

**Goals:**

- An operator can release a slot from the screen that shows it.
- A site can react to a booking being placed or cancelled, without the package owning any
  channel.
- Core stays dependency-free.
- Nothing a subscriber does can damage a booking.

**Non-Goals:**

- Email, templates, SMTP, retries, or delivery guarantees.
- Visitor-facing cancellation.
- Events beyond placed and cancelled.

## Decisions

### D1. Core owns the observation port; the Umbraco adapter lives outside it

**Decision:** `UBookIt.Core` defines a port — one method per fact, taking a `Booking` — and
`BookingService` calls it after the store has committed. `UBookIt.Persistence` registers an
implementation that publishes Umbraco notifications through `IEventAggregator`. Core gains no
package reference.

**The alternative I expected to choose, and did not:** a **decorator** around
`IBookingService` registered in the Umbraco layer, leaving Core completely untouched. It is
tempting — nothing resolves the concrete service, so a decorator would be airtight, and it
needs no spec change at all.

It loses on two counts. First, *"a booking was placed"* is a domain fact, and a decorator puts
it nowhere in the domain: the guarantee becomes a property of DI wiring, unassertable in
Core's own suite, and invisible to anyone reading the domain. Second — the one that decides
it — a decorator only notifies **Umbraco** hosts. This package's whole shape is that a
headless consumer can build a full booking flow against the contracts; an observation seam
that only exists when Umbraco composed it contradicts that, quietly.

**Rejected for a worse reason too:** publishing from the delivery controller, the surface
controller and the backoffice controller. Three call sites, and a fourth path notifies
nobody — the "restate the call site in a form a caller can get wrong" fault this codebase has
already refused twice.

**Cost, stated:** `bookings`' *Availability and placement service ports* says the services
depend on **two** store ports. A third port falsifies the sentence, so that requirement is
MODIFIED — carrying forward what it protects (Core owns its dependencies; implementations are
swappable without changing Core; everything is exercisable without a database) and widening
the enumeration rather than dropping it.

### D2. Notification happens after the commit, and only on success

An event means *this happened*, so it cannot be raised before the store has agreed. Placement
notifies only where `bookingStore.PlaceAsync` succeeded; cancellation only where the status
machine allowed the transition **and** the update was written.

This is what makes the cancellation payload unambiguous, and it answers the question this
change was flagged with — *does the event need to say what changed?* **No.** `CancelAsync`
succeeds only from `Requested` or `Confirmed`, so a cancellation event is by construction
"this booking has just become cancelled". Cancelling an already-cancelled booking fails and
publishes nothing. A before/after payload would carry a difference the transition rules
already guarantee.

### D3. A subscriber cannot break a booking

**Decision:** an exception from an observer is caught, logged, and does not change what the
caller is told. A booking that is stored reports success.

**Why this is the sharpest risk in the change:** the booking is already committed when the
observer runs. Letting the exception propagate means a visitor who successfully booked is told
it failed, and books again — a double booking caused by somebody's mail handler throwing. The
failure mode is worse than the feature is valuable, so the guarantee is absolute rather than
best-effort, and it gets a test with a deliberately throwing observer.

**The cost is honest:** a subscriber that throws is a notification nobody receives, and the
package will not retry or queue it. That is stated in the docs rather than implied — "you
will be told, unless your handler throws" is the kind of half-promise that gets discovered
during an incident.

### D4. Cancel is `POST .../{id}/cancel`, not `DELETE .../{id}`

A cancelled booking still exists: it keeps its row, its history and its service attribution,
and the management list can still return it. `DELETE` says the opposite, and the specs are
explicit that a cancelled booking is retained and merely stops blocking time.

The response is the booking as it now stands, so the row can be updated from what the request
returns rather than re-read. Failures map as the section already maps them:
`booking-not-found` → 404, `invalid-status-transition` → 400.

### D5. The row offers cancel only where it is possible, and the endpoint refuses anyway

The action appears for `Requested` and `Confirmed` bookings and not for `Cancelled` or
`Declined` ones — a control that is always refused is a control that teaches an operator to
ignore failures.

The endpoint still refuses independently. The screen's judgement is a convenience; the status
machine is the rule, and a screen reading a stale list must not be able to talk the domain
into an invalid transition.

## Risks / Trade-offs

- **The notification types become a compatibility promise on first subscribe.** → They carry
  the booking and nothing derived, so there is one source of truth and no computed field to
  keep in step.
- **A site with no subscriber pays for events that go nowhere.** → Publishing to no handlers
  is what `IEventAggregator` already does everywhere in Umbraco; the cost is a virtual call.
- **`Persistence` is an odd home for a notification adapter.** → It is where the composer
  already lives and the one project every consumer loads; splitting an assembly to house one
  adapter would be worse. Named here so the next reader knows it was chosen rather than
  drifted into.
- **A stale list can offer cancel on a booking someone else already cancelled.** → The
  endpoint refuses with `invalid-status-transition` and the screen surfaces it. Not a lost
  update: the second cancel changes nothing and says so.

## Open Questions

- **Whether the cancelled event should carry who cancelled it.** The endpoint knows the
  backoffice user; Core does not, and threading an actor through the domain for one event is
  a larger change than it looks. Deferred: v1 says *what* happened, and a site that needs
  *who* can read the Umbraco audit trail. Revisit if an actual subscriber wants it.
- **Whether placement events should distinguish direct from service bookings.** They need
  not: the booking carries its own service attribution, so a subscriber can already tell.
  Noted only because it will be asked.
