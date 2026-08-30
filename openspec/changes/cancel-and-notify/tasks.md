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
  read on the management port to fill them from, so the endpoint returns the booking's
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
  and placement service ports* is long, carries six scenarios, and is being widened at its
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
- [x] 7.6b **Cancel from the backoffice UI — verified by Chris, and the whole chain with it.**
  He cancelled a real booking through the screen. The database shows that booking moved from
  `Confirmed` to `Cancelled`, and the site's log carries:
  <br>`UBOOKIT-DEV-NOTIFICATION cancelled booking 35bddb1b… starting 2026-08-31T09:00`, with
  `SourceContext: UBookIt.TestSite.LogBookingCancelled` and
  `ActionName: UBookIt.Backoffice.Controllers.BookingsController…`.
  <br>**The `ActionName` is what makes it conclusive**: the notification was raised by the
  backoffice endpoint handling a real request, not by a script or a test. Click → endpoint →
  domain → observer → adapter → a handler the site registered exactly as any consumer would.
  Both halves of this change, end to end, in one line of evidence.
  <br>**A correction to what this leaves untested.** I first recorded "cancelling twice
  through the UI was not exercised" as an omission. Chris pointed out it is **unreachable from
  a current view by design** — a cancelled row offers no control, which is `canCancel` and is
  tested. So there was nothing to exercise.
  <br>The case that *is* residue is the **stale list**: a second operator, or one tab left
  open, still showing a button for a booking somebody else has since cancelled. That is
  exactly what D5 has the endpoint refuse independently for, and the refusal is tested at the
  endpoint (`Cancelling_an_uncancellable_booking_is_a_400_carrying_the_domains_code`). What is
  not exercised is the **screen rendering that particular refusal** — though it renders it
  through the same `_error` path a failed load uses, which is covered.
  <br>Reaching it needs two sessions or a deliberately stale tab. Recorded as residue rather
  than dressed up as coverage.
- [x] 7.7 Hand to `qa-review` in a **fresh context or subagent**.

## 8. QA round 1 — REJECT: two MAJORs, both coverage rather than behaviour

Nothing behavioural was wrong. Both must-fix items were guarantees this change **itself
added** and then left unenforced.

- [x] 8.1 **MAJOR — the scenario I added about Core's independence had no test, and task 1.5
  had already told me so.** It says, in its own words, "**assert it rather than assume it**".
  I asserted it in a spec scenario and assumed it in code. Nothing in the repo inspected
  Core's references.
  <br>This is the constraint the entire design rests on: the port lives in Core *because*
  Core carries no framework, and the adapter is exiled to Persistence at an admitted cost for
  the same reason. Remove the constraint and every one of those decisions becomes arbitrary,
  with a green suite — and **the pressure is already in the code**, because Core's catch
  around an observer is silent precisely for want of a logger. Three tests now: the csproj
  declares no package reference, it declares no project reference either (a transitive route
  to the same place), and the built assembly binds to nothing outside the framework.
  Mutation-checked with a package that central package management will actually resolve —
  the first attempt used an unversioned one and failed at restore, which is a different
  signal from the test firing.
- [x] 8.2 **MAJOR — the one view scenario that needed no DOM was the one left unpinned.**
  "The operator is told the customer is not" is a string, and this suite already has the
  pattern for pinning strings. The equivalent sentence in `docs/backoffice.md` was guarded on
  the .NET side; the sentence an operator actually reads, at the moment of deciding, was not.
  Now pinned by meaning ("does not tell the person who booked", "contact them") plus a
  presence check over every key the cancel flow can emit. Mutation-checked by softening the
  sentence to say uBookIt *will* notify.
- [x] 8.3 **`.visually-hidden` was used and never defined.** Markup copied from the resource
  and service lists; the CSS rule not. Shadow DOM inherits no page classes and this element
  adopts no shared stylesheet, so the class meant nothing and the "Actions" heading rendered
  to everyone. Exactly the class of defect the missing DOM environment cannot see.
- [x] 8.4 **Focus was dropped after cancelling.** The button holding it is removed with its
  row — on the default filter the booking leaves the view entirely — so focus fell to the
  document and a keyboard operator cancelling several bookings restarted their traversal each
  time. Focus now moves to the heading, which is where the list begins; not to another row,
  because which row is "next" depends on a filter that just changed under them.
- [x] 8.5 **NITs recorded rather than fixed:** the adapter accepts a `CancellationToken` and
  drops it (correct — a cancelled request must not abort notification of a committed booking
  — and now said so in the code); and adding an optional constructor parameter to
  `BookingService` is binary-breaking, which is irrelevant while nothing is published and
  will not be after step 1 of `docs/mvp.md`. The "eight scenarios" count in 7.4 was six.
- [x] 8.6 Clean-build gates, then QA round 2.
- [ ] 7.8 **At sync: `booking-management`'s Purpose is falsified again.** It says *cancel* "is
  the half still outstanding", which this change completes — and that sentence is one **I
  wrote at the last sync**, one change ago.
  <br>The lesson is not "remember to update it". A Purpose that enumerates what is left is
  falsified by every change that finishes something, so it should stop keeping score: state
  what the capability covers, and let the requirements say what exists. Rewrite it that way
  rather than advancing the count.
