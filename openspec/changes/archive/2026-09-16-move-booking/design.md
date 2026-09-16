## Context

See `proposal.md` for why. What shapes the how:

- **The aggregate has no mutator for its interval or claims.** `Booking.Interval` is get-only,
  `Claims` is a read-only view, and every verb the aggregate has (`Confirm`, `Decline`, `Cancel`,
  `EraseBooker`) is a named method that either applies a rule or refuses. There is no state
  machine class; the rule is one private `Transition` helper.
- **The store has one update, and it writes one column.** `IBookingStore.UpdateAsync` persists the
  status and nothing else, and both its remarks and the SQL implementation record three defects
  caused by widening it. Erasure has its own single-statement write whose condition is inside the
  statement. That is the doctrine any new write inherits: **one verb, one write, its own columns,
  its condition in the statement.**
- **The placement pipeline is one method with the rules in two halves.** `ResolveWindow` is rule 1
  and the calendar window; `ValidateAgainst(resource, window, duration)` is rules 2–7 for one
  resource, reading lead time and horizon straight from the resource's constraints. Rule 8 is the
  store. `CheckPlacementRules` already exposes rules 1–7 without a write.
- **The SQL placement takes `sp_getapplock` per claimed resource in ascending id order, then checks
  overlap against blocking claims.** The overlap query has no self-exclusion because nothing that
  is already stored has ever been re-placed.
- **The observation port has four members and no defaults**, adapted to four Umbraco
  notifications, consumed by one email handler whose one load-bearing line decides which events
  reach the site's own recipients. The composer maps event → booker kind; wording derives from
  status, with `Cancelled` special-cased by event.
- **The bookings list is a `uui-table` with a per-row actions cell**; every action so far is a
  button, and the only modal in the client is Umbraco's confirm modal wrapped by `confirmDestructive`.
  There is no custom modal, no form modal, and no detail view — the last by explicit decision.
- **Problem details from the management API carry stable codes**, and the client already reads
  them (`api-errors.ts`), so a refused move can be explained without a new error channel.

## Goals / Non-Goals

**Goals:**

- A move is a placement of the booking it already is: the same pipeline, the same locks, the same
  conflict semantics, and no second definition of "bookable" anywhere.
- The move write is a third narrow write over an existing row in the existing pattern, not a
  widening of the status write.
- Operator-versus-visitor terms are one explicit parameter of the pipeline, so booking-on-behalf
  reuses it rather than re-deriving it.
- No new table, no new column, no migration.
- The client explains a refusal in place, from the code, without inventing a second vocabulary.

**Non-Goals:**

- Anything in the proposal's Non-goals: resource change, service change, booker edit, history,
  status change, visitor move, availability read, telling anyone but the booker.
- A generic "update booking" path in any layer. Every layer gets a move and only a move.
- A detail view or workspace for a booking. The modal is opened from the row.

## Decisions

### Decision 1: The pipeline takes its terms as a value, not a flag

`ValidateAgainst` reads lead time and horizon from the resource. It will instead receive a
`PlacementTerms` value — `LeadTime` (a `TimeSpan`) and `Horizon` (days, or none) — that the
caller derives: **visitor terms** take both from the resource's constraints, exactly as today;
**operator terms** are `LeadTime = TimeSpan.Zero` and no horizon. Rules 2–4 and 7 read the
resource as before; only the two policy rules read the terms.

*Why a value rather than an `isOperator` boolean:* a boolean at the rule site encodes the policy
where it is hardest to see. A value named for what it is makes booking-on-behalf a one-line
caller (`PlacementTerms.Operator`) and keeps the rule bodies identical for both callers, so a
test that the visitor path is unchanged is a test that the two constructors differ and nothing
else does.

*Why zero lead rather than skipping the rule:* skipping the rule loses the past-start guard and
would need a new code for it. A lead of zero is precisely "not in the past", it already has a
stable code, and it keeps the pipeline's ordering and reporting untouched. The spec says this in
so many words.

*Alternative rejected:* a separate operator pipeline. Two pipelines drift; this project's
rule-pipeline is the one thing every placement path already shares and that property is worth
more than a shorter diff.

### Decision 2: `CanMoveTo`/`MoveTo` on the aggregate — checked before the store write, applied after it

`Booking.MoveTo(BookingInterval newInterval)` returns a `DomainResult`: refuses with
`invalid-status-transition` unless `Requested` or `Confirmed`; refuses with `interval-unchanged`
if `newInterval` equals the current one; otherwise sets `Interval` (a new private setter) and
returns success. It does not touch claims, status, booker, reference or service.

The service's `MoveAsync(Guid id, DateTimeOffset newStart, TimeSpan newLength)`:

1. loads the booking (`booking-not-found`) and refuses a status that does not permit a move;
2. resolves the zone and the window for the new interval (rule 1);
3. loads every claimed resource and runs `ValidateAgainst` with operator terms per resource
   (rules 2–7), ordering failures as placement does;
4. asks `booking.CanMoveTo(window.Interval)` — the unchanged-interval check, without changing
   anything;
5. calls the store's move write with the id, the new interval and the permitted statuses;
6. only then applies `booking.MoveTo(window.Interval)` to the aggregate, and reports through the
   observer with the previous interval, wrapped so the observer cannot break the move.

*Amended during apply.* The first cut applied `MoveTo` before the store write, and the in-memory
tests caught it: that store hands out the instance it holds, so a refused move left the stored
aggregate claiming the refused interval. The split into `CanMoveTo` (check) and `MoveTo` (apply
after the store agrees) mirrors placement, where the aggregate is built, the store decides, and
only a stored booking is handed on.

**Service bookings go through `IServiceBookingService.MoveAsync`.** Added after QA round 1: the
booking service knows resources and nothing about services, exactly as direct placement does, so
a 45–120 minute service booking could be moved to 30 minutes. The service booking service's move
loads the booking, and for one placed for a service computes the highest floor and lowest ceiling
of the service's specification intersected with each claimed resource's range (the same arithmetic
as `ValidateAgainstPoolBounds`, over the booking's own claims), refuses with the placement codes,
then delegates. A deleted service no longer binds; a direct booking is delegated untouched. The
endpoint calls this and not the booking service. It is a third declared port break.

*Why the interval check is after the pipeline, not before:* the pipeline's answer should be
authoritative for every submitted interval, so an operator always hears the truth about the time
they typed. An unchanged interval is one the pipeline accepted once already, so "unchanged" is
then only ever said about a valid interval. The spec's "before any store write" is satisfied;
the lock and the write come after both.

*Why `MoveTo` takes the resolved interval rather than start+length:* the aggregate never resolves
zones; the service does, exactly as for placement.

### Decision 3: A third narrow write over an existing row, `MoveAsync(bookingId, newInterval, permittedStatuses)`

The store gains `Task<DomainResult> MoveAsync(Guid bookingId, BookingInterval newInterval,
IReadOnlyCollection<BookingStatus> permittedFrom, CancellationToken)`. It takes an id and values,
not the aggregate — the erasure write's shape — so there is no stale copy of any other column to
write back. (Counting rule, settled after QA: placement is the insert; status, erasure and move
are the three narrow writes over an existing row, which is how the persistence spec counts them.)

SQL implementation, one transaction:

1. read the booking's claimed resource ids (a `Claims` query by booking id — no aggregate needed);
2. `sp_getapplock` for each, ascending, via the existing `AppLock.ForResourcePlacement`;
3. conflict query identical to placement's **plus `existing.Id != bookingId`**;
4. `ExecuteUpdateAsync` on `Bookings` where `Id == bookingId && permittedFrom.Contains(Status)`,
   setting `StartUtc`, `EndUtc`, `TimeZoneId` — a single statement whose predicate is the
   status condition; zero rows affected → `invalid-status-transition`;
5. commit; conflict → rollback and `conflict`.

*Why the claims are read inside the store rather than passed in:* the lock set must be the stored
claims, not whatever the caller's aggregate believes; and claims are immutable after placement so
there is no race on that read.

*Why `TimeZoneId` is written too:* the spec says the zone becomes the one the new interval was
validated against, because a move is a placement. It is part of the interval's identity (the
`BookingInterval` value carries it), so "the interval columns" means all three.

*In-memory double:* `InMemoryBookingStore` gets the same member with the same self-exclusion and
status predicate, under its existing lock, so the Core-level tests for *Atomic move contract*
mean something.

### Decision 4: The observer carries the previous interval as a second argument

`Task BookingMovedAsync(Booking booking, BookingInterval previousInterval, CancellationToken)`.
Not a `MovedBooking` wrapper, not a property on the aggregate (the aggregate "carries the booking
and nothing derived from it", and a previous interval is neither current state nor derived — it
is a fact only the report holds).

Umbraco side: `BookingMovedNotification(Booking booking, BookingInterval previousInterval)`, the
adapter maps it like the other four, and the handler adds `BookingEvent.Moved`. The load-bearing
`siteEventApplies` line stays `Placed or Cancelled` — a move is not added to it, and the comment
above it gains the move alongside confirm and decline with the responsibility wrinkle noted.

### Decision 5: The moved message is composed from the event, worded from the status, and carries both intervals

- `BookingMessageKind.BookerMoved` — additive enum member, so the view path
  `~/Views/Partials/UBookIt/Emails/BookerMoved.cshtml` exists for a site to supply.
- `BookerMessageModel.PreviousStart`, `PreviousEnd` — nullable, converted to the booking's zone
  by the same code that converts `Start`/`End`; null for every other event. Additive on a frozen
  type.
- Subject: "Your booking has moved". Body: the details block as today, preceded by a line stating
  the previous time and followed by the closing line derived from status (a moved `Requested`
  booking still says it is not confirmed yet — that sentence is true and the composer already
  derives it).
- The boot check enumerates the enum, so the new kind is checked for free; the `email-templates`
  documentation table gains a row.

### Decision 6: A custom modal opened from the row, registered as an Umbraco `modal` extension

The client's only modal precedent is Umbraco's confirm modal. A move needs inputs, so this adds
the first custom modal: a Lit element registered with a `modal` manifest entry and a
`UmbModalToken` carrying `{ bookingId, reference, start, end, zone }` in and `{ moved: true }`
out, opened with `umbOpenModal` like `confirmDestructive`. The token's value is the new interval
(`{ startUtc, endUtc, timeZoneId }`), not a bare flag, so the list can say where the booking went.
Inside: a `<form>` with a date input, a time input and a length input (minutes), each a native
`<input>` with a real `<label for>` — **not** `uui-label`, which the bookings-screen handover
records is not a label — plus the conditional-notification sentence and Cancel/Move buttons.
Focus is managed explicitly (first input on open and after a refusal; the row's Move button on
dismissal), because the modal container leaves it on `<body>` — measured live.

Submission calls the generated SDK's `moveBooking`; a refused move reads the problem-details code
via `api-errors.ts` and maps it to a localisation term shown in an element associated with the
inputs by `aria-describedby`. The modal stays open on refusal and closes on success, returning
`{ moved: true }`; the list then refetches the current page and applies the existing step-back
and focus rules (`#applyRowAction` already does this for the other verbs).

*Alternative rejected — inline editing in the row:* a `uui-table` row with three inputs and a
submit is hostile to keyboard users and to the 400px width the section already supports.

*Alternative rejected — a booking workspace:* the seam `bookings-view.element.ts` refused to build
would be built for one form. When a scheduler or a detail view arrives, the modal's form can move
into it; the endpoint is what a scheduler needs, not the modal.

### Decision 7: The endpoint takes the site's wall-clock start, and converts once

`POST bookings/{id:guid}/move` with body `{ start: "2026-09-20T09:00", lengthMinutes: 60 }` —
a local date-time with no offset, interpreted in the site's zone, exactly the convention the list
window already uses for dates. The controller resolves the zone the same way the window does and
hands the service a UTC instant. Response `MovedBookingModel { bookingId, status, startUtc,
endUtc, timeZoneId }`. `lengthMinutes` rather than an ISO duration because the client is a
number input and the domain is minute-granular; the service still takes a `TimeSpan`.

### Decision 8: The documentation test turns around, not off

`BackofficeDocumentationTests` asserts "It does not amend a booking's time". It becomes an
assertion that the docs say a booking **can be moved** and **what a move does not do** (change
resources, keep history, notify the site). The falsified-sentence sweep in
`NotificationDocumentationTests` gains the retired sentence "the shape of that operation is a
cancellation and a new booking" so it cannot come back.

## Risks / Trade-offs

- **[The conflict query's self-exclusion is a one-token change that is easy to get wrong in the
  in-memory double and right in SQL, or vice versa]** → the *Atomic move contract* scenarios run
  against both stores from the same test body, the way the placement contract's already do in
  `ConcurrencyTests`; the "does not conflict with itself" case is the first test written.
- **[`ExecuteUpdateAsync` bypasses the change tracker; a later refactor that loads the row and
  saves it would silently reintroduce read-then-write]** → the persistence spec's "condition is
  inside the statement" scenario is a source-level assertion over the store, like the existing
  erasure guard; and the cancel-between-read-and-write integration test exercises it.
- **[Operator terms leaking into the visitor path]** → a test that constructs both terms and
  asserts the visitor terms equal the resource's constraints; and the delivery-API placement
  tests are unchanged and must stay green.
- **[The email handler's `siteEventApplies` line is the kind of line a fix "helpfully"
  widens]** → the booking-emails scenario "a move is told to the booker only" runs with a
  responsibility assignment present, so a widening fails it.
- **[Two port breaks in one minor]** → both are called out in the proposal and the upgrade note;
  `IBookingObserver` and `IBookingStore` have taken additive members before and the pattern is
  known.
- **[A moved booking leaves the operator's window and vanishes from the list]** → by design and
  in the spec; the modal's success path could show the new date in a notice so the operator knows
  where it went. Cheap, and worth doing in the same change.
- **[The modal's date/time inputs are native and unstyled]** → consistent with the section's
  existing inputs and with invariant 5's stance; not a scheduler, and not meant to look like one.
- **[QA will treat the pipeline-terms refactor as new code]** → it is; the placement tests are the
  regression surface and must pass unchanged before any move test is written.

## Migration Plan

No schema change. Deploy is the package upgrade to 17.1.0. Two compile-time breaks for hosts that
implement `IBookingObserver` or `IBookingStore` themselves, documented in the upgrade note with
the one-member addition each needs. Rollback is downgrading the package; nothing a move wrote is
unreadable by 17.0.x, since it wrote only columns that version already reads.
