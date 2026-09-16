## Why

A booking cannot be moved. The only way to change when a booking is happens to be a cancellation
and a new booking, and that loses the one thing the customer is holding: the reference they were
emailed, wrote down, and will quote on the phone. The package says so in eight places, on the
record as a decision rather than a gap, and the `bookings` spec already promises that a reference
survives "if the booking's time is later amended" — a promise nothing can yet exercise.

It is the linchpin of 17.1.0. Three consumers want the same operation: the README's own not-yet
list, drag-to-move in any front-end scheduler a site builds over the delivery API, and drag-to-move
in the backoffice scheduler the separate DevExpress package exists to provide. The Management API
already gives a scheduler confirm, decline and cancel; move is the verb it lacks.

## What Changes

- **A move operation on the booking domain.** A `Requested` or `Confirmed` booking can be given a
  new start and a new length. Its reference, status, resources, service and booker are unchanged.
  It is placed at the new interval through the same validation pipeline a visitor's placement runs,
  under the same locks, and its previous claim on its old interval is released in the same
  transaction — so at no instant does the booking hold two intervals or none.
- **Operator placement relaxes exactly two visitor rules.** Lead time and horizon exist to police
  visitors; an operator moving a booking an hour earlier on the day is the person the site trusts
  to decide that. A move therefore evaluates the lead-time rule with a lead of **zero** — which
  keeps the one guard nobody can waive, that a booking cannot be moved into the past, and reuses
  the existing `lead-time` code for it — and applies no horizon at all. Everything physical — open
  hours, granularity, duration bounds, conflict — applies unchanged. **This sets the precedent
  booking-on-someone's-behalf inherits.**
- **A move to the interval a booking already holds is refused**, with a new stable code, on the
  same grounds as cancelling twice: a caller told "moved" when nothing changed cannot tell a
  completed action from a rejected one.
- **The observation port reports a move, carrying the previous interval.** This is the first
  report that describes what changed, and it is a deliberate exception to the recorded doctrine
  that no report needs to: a booker's message that could only say "you are now at Y" instead of
  "you have moved from X to Y" would be worse than the doctrine is valuable. The previous interval
  is not persisted anywhere — see Non-goals.
- **BREAKING — published ports, three of them.** `IBookingObserver` gains a moved member, with no
  default implementation, by the port's own doctrine: an observer silently deaf to moves would be
  a worse outcome than a compile error. A host supplying its own observer must add it.
  **`IBookingStore` gains a move write**, on the same terms. **`IServiceBookingService` gains a
  move**, which is the operator's entry point for moving any booking and the only path that
  applies a service's length rule (added after QA round 1 moved a 45–120 minute service booking
  to 30 minutes). All three land in a minor (17.1.0), which is where a break belongs; none can
  ship as `17.0.x`. **No other public signature changes**: the composer's `ForBookerAsync` keeps
  its 17.0.0 shape and gains an overload.
- **The booker is told, and only the booker.** A new message kind, `BookerMoved`, additive to the
  frozen kind set with its own view and its own template. The site's own recipients are not
  written to, on exactly the reasoning confirm and decline settled: the site performed the action,
  and the bookings screen is where its state lives. Responsibility recipients are not told either,
  for the same reason — and the wrinkle that makes this worth revisiting (a *resource* swap, which
  a responsible party did not perform) is outside v1 by construction.
- **Move rides on `UBookIt.Bookings.Manage`.** No fifth verb: moving is less destructive than
  cancelling, and the verb already means "may act on a booking". The permissions requirement's
  definition of Manage is restated to say so.
- **A versioned management endpoint** that moves a booking, under the Manage policy, taking the new
  start in the site's own time and the new length, and answering with the booking's identity, its
  status and its new interval — not a list row, on the same grounds the cancel response is not.
- **A Move action on the bookings list**, for `Requested` and `Confirmed` rows, opening an
  accessible in-page modal with date, time and length. The modal submits and, where the domain
  refuses, shows the operator the stable failure code's reason rather than the row appearing to
  change. **There is no availability picker in v1** — an operator attempts and is told, which is
  also exactly what a scheduler's drag-drop does. That this is poor UX for an operator hunting for
  free time is acknowledged, not disputed; the read that would fix it is its own change.
- **Eight documented refusals fall**, plus the documentation test that guards one of them, and the
  README's not-yet list, `docs/backoffice.md`, `docs/mvp.md` and `docs/notifications.md` gain the
  operation and its limits.

### Non-goals

- **Changing which resources a booking claims.** A move keeps every claim. A service booking whose
  therapist is busy at the new time fails with `conflict`, and the operator picks another time.
  "Resource A is off sick, swap in B" is a real need and the next thing this operation grows; the
  store write and the lock set are designed so a claim change fits later without a second seam,
  but it is not built here.
- **Changing a booking's service, or editing its booker.** A different service is a different set
  of roles and is honestly a new booking. Editing a booker is a write over personal data with
  concerns of its own.
- **A move history.** Nothing persists where a booking used to be; the event carries the previous
  interval and that is all. A booking shows only where it is now, and the docs say so. Persisting
  history interacts with erasure and retention and is decided explicitly later rather than by
  silence here.
- **A status change on move.** A confirmed booking stays confirmed; a requested one stays
  requested. The status machine's "exactly four transitions" sentence remains true.
- **A visitor-facing move.** The delivery API gives a visitor no post-placement access to a
  booking, by decision; a self-service move has the same authentication problem as self-service
  cancellation, which is its own 17.1.0 exploration.
- **An availability read for operators**, showing where a booking could go. Named above as the
  fix for the modal's UX; not built here.
- **Telling responsibility recipients or the site's internal list about a move.**

## Capabilities

### Modified Capabilities

- `bookings`: The requirement *"Availability and placement service ports"* enumerates what the
  booking service does and gains the move operation. *"Placement and status changes are
  observable"* names four events and says no report needs to describe what changed; a fifth event
  joins, and the doctrine gains its stated exception. Two new requirements: *"Moving a
  booking"* states the operation — what it changes, what it keeps, which rules it runs and on
  whose terms, the unchanged-interval refusal; *"Atomic move contract"* states the store port's
  obligations — self-exclusion, release-and-claim in one step under the same locks, and the
  status condition inside the write. *"Atomic placement contract"* is deliberately **not**
  replaced: a sibling requirement carries less risk than a wholesale rewrite of the one placement
  depends on.
- `booking-management`: The purpose paragraph's sentence that amending "is still not here" is
  false. Two new requirements: an operator can move a booking (the endpoint), and the bookings
  view can move a booking (the action and the modal), on the same terms confirm, decline and
  cancel set.
- `booking-emails`: *"Which events produce messages, and for whom"* says four events; a fifth
  joins, booker-only, and its message carries the previous interval in the booking's own zone.
- `email-templates`: *"Content is supplied as Razor views at a published path, one per message"*
  publishes the complete set of message names; the set gains `BookerMoved` for the booker and
  nothing for the site's own recipients. The booker model gains the previous interval.
- `permissions`: *"Access within the section is decided by four verbs"* defines Manage as
  "cancelling, confirming and declining"; move joins the definition. The verb count is unchanged.
- `service-booking`: a new requirement, *"Moving a booking placed for a service applies the
  service's length rules"* — the service booking service's move, the operator's single entry
  point, binds a service booking's length by the intersection of the service's specification
  with each claimed resource's range, as placement does. Without it, *"Service duration narrows
  each candidate independently"* was falsified for moved bookings (QA round 1, live).
- `persistence`: *"Store implementations honour Core semantics"* states that `UpdateAsync` writes
  the status and nothing else; the move write is a further narrow write over its own columns, and
  the requirement says so. A sibling requirement, *"Atomic move on SQL Server"*, is added for the
  move's transaction shape — self-excluding conflict check under the same application locks, and
  the status predicate inside the update statement; *"Atomic placement on SQL Server"* is not
  replaced, for the reason above.

**Seven of these are wholesale replacements** (two in `bookings`, one each in `booking-emails`
and `permissions`, two in `email-templates`, one in `persistence`); the rest are additions. Per the
project rule, the specs artifact carries each replaced requirement's existing SHALLs and scenarios
forward explicitly, in a guarantee diff at the top of every delta file, or records a deliberate
drop with its reason. No drop is expected; one supersession is the point of the change: the
sentence "no status-change report SHALL need to describe what changed" is restated for the four
reports it was true of, and the move report is named as the one that must.

### New Capabilities

None. Moving is a verb over the existing booking domain and its existing management surface; a new
capability would split what `bookings` and `booking-management` already own between them.

### Deliberately unmodified

- `availability`: *"Booking constraints"* defines lead time and horizon per resource; nothing about
  their definition changes, only which callers they bind, and that is stated in `bookings`.
- `booker-erasure`, `booking-retention`: an erased booker's booking can move and sends nothing; a
  moved `EndUtc` moves the booking within the retention index, which is the index doing its job.
  Neither requirement changes.
- `delivery-api`: no visitor surface changes.

## Impact

### The concurrency shape this change must get right

The existing store update writes one column, after three defects caused by widening it. A move
writes `StartUtc` and `EndUtc`, and must not go through that method. It needs its own write, and
the write has three obligations the placement write does not:

1. **The conflict check must exclude the booking being moved.** Today's query has no such
   predicate; a booking moved in place would conflict with its own claim rows.
2. **Release and claim are one transaction.** The new interval is checked and written under the
   application locks for every claimed resource, in ascending id order, so no concurrent placement
   can take the old interval before the move commits nor the new one after the check passes.
3. **The write is conditional on the status still permitting a move.** The service reads the
   booking, then locks, checks and writes. A cancellation landing between the read and the write
   would otherwise let a cancelled booking move. The write must only succeed where the stored
   status is still `Requested` or `Confirmed`, and a zero-row update is reported as the domain's
   invalid-transition failure rather than as success.

### Code

- `UBookIt.Core` — a `MoveTo` on the aggregate that permits `Requested` and `Confirmed` only; a
  move operation on `IBookingService`, running the whole pipeline with lead time evaluated as zero
  and no horizon; a move write on `IBookingStore`; a moved member on `IBookingObserver`; one new
  failure code, `interval-unchanged`.
- `UBookIt.Persistence` — the SQL move write; a `BookingMovedNotification` and the adapter's
  mapping; the email handler's fifth event, booker-only; the composer's `BookerMoved` subject,
  body and kind mapping; the booker model's previous interval.
- `UBookIt.Backoffice` — `POST bookings/{id}/move` under `VerbPolicies.BookingsManage`; the row
  action, the modal, its localisation, the regenerated client SDK.
- `UBookIt.Web` — the `BookerMoved` template and its entry in the boot check.

### Tests

The atomic-placement integration suite is the model for the move write's tests: racing a move
against a placement on the old interval, on the new interval, against a cancel of the same
booking, and against another move of the same booking. `BackofficeDocumentationTests` asserts the
sentence this change removes, and must be turned around to assert its replacement.

### Docs

README not-yet list, `docs/backoffice.md`, `docs/mvp.md`, `docs/notifications.md` (the move joins
what raises a notification and what the site is not told), `docs/notifications.md`'s message-kind
table, and the upgrade note for the two port breaks.

### Release

17.1.0, batched with the settings screen already merged. A feature cannot ship as `17.0.x`, and
this one also carries two port breaks, which only a minor may.
