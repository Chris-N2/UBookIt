## 1. The observation port (Core, still dependency-free)

- [x] 1.1 Declare the port in `UBookIt.Core` — one member per fact, each taking the `Booking`.
  Nothing derived, so there is one source of truth (design D1).
- [x] 1.2 `BookingService` reports **after** the store has committed and **only** on success
  (D2). Placement reports where `bookingStore.PlaceAsync` succeeded; cancellation where the
  transition was allowed *and* the update was written.
- [x] 1.3 **An observer cannot break a booking** (D3): exceptions are caught and logged, and
  the caller is told exactly what it would have been told with no observer at all.
- [x] 1.4 A no-op implementation registered by default, so a host that wants nothing pays
  nothing and `BookingService` never has to null-check.
- [x] 1.5 **Confirm `UBookIt.Core` still has zero package references.** This is the constraint
  the whole design is shaped around; assert it rather than assume it.

## 2. The Umbraco adapter

- [x] 2.1 Two notification types and an implementation publishing them through
  `IEventAggregator`, in `UBookIt.Persistence` beside the existing registrations (D1's stated
  odd-home trade-off).
- [x] 2.2 Register it in `UBookItPersistenceComposer`, replacing the no-op.
- [x] 2.3 **These types are a compatibility promise from the first subscriber.** Keep them
  minimal: the booking, nothing computed.

## 3. Cancel over HTTP

- [x] 3.1 `POST bookings/{id}/cancel` on the existing authorized base (D4). Not `DELETE` — a
  cancelled booking still exists.
- [x] 3.2 Return the booking as it now stands, so the row updates from the response.
  <br>**Changed during apply, and the requirement with it.** This path cannot honestly return
  a list-shaped booking: a row carries each resource NAME, joined by the management port,
  while cancellation goes through the domain, which knows resource IDs. There is no by-id
  read on the management port to fill them from, so the endpoint returns the booking''s
  identity and new status, and the screen reloads — which it must anyway, since cancelling
  changes what the query matches.
- [x] 3.3 Map failures as the section already does: `booking-not-found` → 404,
  `invalid-status-transition` → 400. **Do not** invent a "already cancelled is fine" success.
- [x] 3.4 Regenerate the TypeScript client and commit it.

## 4. Cancel on the screen

- [x] 4.1 A cancel action per row, offered only for `Requested` and `Confirmed` (D5).
- [x] 4.2 `confirmDestructive` for the confirmation — never a native dialog. Its three
  outcomes are already distinguished; a failed confirmation must not read as a refusal.
- [x] 4.3 Reload the list after a successful cancellation so the row shows its new state.
- [x] 4.4 Surface a refusal to the operator rather than letting the row appear to change.
- [x] 4.5 **Say on the screen that cancelling notifies nobody**, where the operator sees it
  while deciding.

## 5. Guards that would catch the real mistakes

- [x] 5.1 **A throwing observer does not break placement.** The sharpest risk in the change:
  the booking is already stored, so an escaping exception tells a visitor their booking failed
  when it did not, and they book again. Assert the caller's result *and* the stored booking.
- [x] 5.2 **Nothing is reported on failure** — neither a failed placement nor a refused
  cancellation. An event that fires on failure is a lie a subscriber cannot detect.
- [x] 5.3 **Reports happen after the commit**, not before. A test that only counts reports
  would pass with the call in the wrong place.
- [x] 5.4 **Exactly one report per successful operation**, including a service booking, which
  reaches placement through a different entry point and must not report twice or not at all.
- [x] 5.5 **Cancelling twice** is refused end to end — domain, endpoint, and screen — and
  reports nothing the second time.
- [x] 5.6 **A cancelled booking is still listed** and still carries its interval, booker and
  service. Cancelling is not deleting, and the previous change's attribution must survive it.
- [x] 5.7 **The screen offers no cancel control** for a cancelled or declined booking, and the
  endpoint refuses independently of what the screen believes.
- [x] 5.8 Mutation-check every guard. Particular attention to 5.1 and 5.3: both are about
  *when* and *whether* something happens rather than what it returns, which is the kind of
  assertion that passes for the wrong reason.

## 6. Documentation

- [x] 6.1 Document the notifications: each one, when it is raised, what it carries, how to
  subscribe.
- [x] 6.2 State that the package sends nothing itself, and that a handler which throws is a
  notification nobody receives and will not be retried.
- [x] 6.3 Update the backoffice documentation for the cancel action — including that it
  notifies nobody.
- [x] 6.4 `docs/mvp.md` steps 7 and 8 become done. **This is the document this change exists
  to satisfy; leaving it stale would be the exact fault the last change hit three rounds
  running.**

## 7. Close

- [x] 7.1 Full solution build at **zero** warnings from a clean `bin`/`obj` — not sweeping
  `node_modules`.
- [x] 7.2 Full suite green against the 1711 baseline; client suite against 104.
- [x] 7.3 `openspec validate --all --strict`.
- [x] 7.4 **Diff the guarantees of the MODIFIED requirement clause by clause.** *Availability
  and placement service ports* is long, carries eight scenarios, and is being widened at its
  most-quoted sentence — precisely the shape that loses something silently.
- [x] 7.5 Sweep sibling specs for sentences this falsifies. **Start with the capabilities this
  change touches**, then: anything saying Core depends on two ports or has no dependencies,
  anything saying the package raises no events, `docs/mvp.md`, and `booking-management`'s
  Purpose, which says *cancel* is the half still outstanding and will not be.
- [x] 7.6a **A subscriber receives the notification — verified end to end.** The dev site now
  registers a handler the way a consuming site would (composer,
  `INotificationAsyncHandler`, no privileged access), a booking was placed through the
  **anonymous delivery API**, and the handler logged it with the right id and
  `(booked directly)`. This is the part no unit test could show: Core reporting is
  assertable in the suite, a site *hearing* is not.
  <br>The handler is also the worked example in `docs/notifications.md`, kept as compiling
  code — and it earned that immediately: both it and the documented snippet had
  `INotificationAsyncHandler` imported from the wrong namespace (it lives in
  `Umbraco.Cms.Core.Events`, not `.Notifications`). The compiler caught the code; nothing
  would have caught the documentation.
- [ ] 7.6b **Cancel from the backoffice UI** — outstanding, needs a login I will not perform.
  Cancel a real booking, confirm the row leaves the default view, confirm a second attempt is
  refused, and confirm the cancellation notification reaches the same handler. The mechanism
  is identical to placement's and proven above; what is unverified is the screen.
- [ ] 7.7 Hand to `qa-review` in a **fresh context or subagent**.
- [ ] 7.8 **At sync: `booking-management`'s Purpose is falsified again.** It says *cancel* "is
  the half still outstanding", which this change completes — and that sentence is one **I
  wrote at the last sync**, one change ago.
  <br>The lesson is not "remember to update it". A Purpose that enumerates what is left is
  falsified by every change that finishes something, so it should stop keeping score: state
  what the capability covers, and let the requirements say what exists. Rewrite it that way
  rather than advancing the count.
