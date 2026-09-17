# Design — booking on a booker's behalf

## Context

See `proposal.md` — *Why*. The relevant current state, and nothing more:

"Who is placing" is encoded in exactly three places today, and this change presses on all
three:

```
IBookingService
 ├─ PlaceAsync(BookingRequest)            ① DirectlyBookable guard lives HERE, in the
 │     │                                     overload, deliberately not as a flag
 │     ▼
 ├─ PlaceAsync(MultiClaimBookingRequest)  → private PlaceAsync(request, service, ct)
 │        ├─ ValidateAgainst(..., PlacementTerms.Visitor(constraints))          ②
 │        └─ Booking.Create(..., settings.AutoConfirm ? Confirmed : Requested)  ③
 │
IServiceBookingService
 ├─ PlaceAsync(ServiceBookingRequest)     resolves roles → delegates to PlaceForServiceAsync
 └─ MoveAsync(...)                        the operator's single entry point for a move
```

Three constraints the existing code states about itself, which this design must honour
rather than rediscover:

- ① *"Calling THIS overload is what being a direct booking means… A flag would restate the
  call site in a form a caller can get wrong."*
- ③ *"The one site that decides what a new booking IS, for the direct and the service path
  alike — both funnel through here, so they cannot disagree."*
- `PlacementTerms`' own doc: *"recording a booking on somebody's behalf is a one-line caller
  of `Operator`… the rule bodies stay identical for both callers."*

`PlacementTerms` shipped in `move-booking`, which is merged but **unreleased**
(`Directory.Build.props` still declares `17.0.1`). It can therefore be reshaped without a
break — until 17.1.0 publishes.

## Goals

- Operator placement reuses the visitor pipeline in full; the only differences are the terms.
- Every "is this an operator?" decision is answerable from one place at each layer, and none
  of them is a boolean a caller supplies.
- The visitor paths are provably unchanged.

## Non-Goals

Beyond the proposal's: no change to `IBookingStore`, no change to `UBookIt.Web`, and no new
failure code.

*An earlier draft listed "no change to the observer port" and "no change to
`UBookIt.Persistence`" here. Both were wrong, and D5 records why — an operator's placement has
to be distinguishable at the moment of placing or not at all.*

## Decisions

### D1 — `PlacementTerms` grows an approval member; it does **not** grow a direct-bookability one

`PlacementTerms` becomes `(LeadTime, HorizonDays, ApprovalApplies)`. `Visitor(constraints)`
sets `ApprovalApplies = true`; `Operator` sets it `false`.

*Why the approval member belongs there.* ③ must keep being the one site that decides a new
booking's status. If the operator entry point chose a status and passed it in, there would be
two deciders and a day when they disagree. Reading the terms at ③ — `terms.ApprovalApplies &&
!settings.AutoConfirm ? Requested : Confirmed` — keeps one decider and makes the rule visible
where the existing comment already promises it lives.

*Why direct-bookability does **not**.* ① is not evaluated on the terms; it is not reached at
all on the operator path. A `DirectBookingApplies` member would be a rule stated in a value
and enforced in an overload — two places, free to disagree — and it would reintroduce exactly
the flag ①'s comment argues against. The spec states this as a requirement, so the absence is
guarded rather than merely intended.

*Alternative rejected:* a separate `OperatorPlacement` type alongside `PlacementTerms`. It
duplicates lead and horizon, and a reader would have to know which of two values a given rule
consults.

### D2 — Two public entry points, each on the layer that owns the rules it must apply

| | Entry point | Applies |
|---|---|---|
| Resource | `IBookingService.PlaceOnBehalfAsync(BookingRequest, …)` | pipeline under `Operator`; **no** `DirectlyBookable` check |
| Service | `IServiceBookingService.PlaceOnBehalfAsync(ServiceBookingRequest, …)` | role resolution + the service's length rules, then delegates |

This is `move-booking`'s lesson applied before it can bite again: `IBookingService` knows
resources and nothing about services, so an operator placement routed only through it would
apply the resources' duration bounds and silently not the service's. `IServiceBookingService`
is therefore the operator's single entry point, and a request naming a resource rather than a
service delegates straight through — identical in shape to `MoveAsync`.

*Alternative rejected:* one entry point taking `PlacementTerms` as a parameter on the existing
`PlaceAsync`. Source-compatible only if appended after the cancellation token, and the
move-booking handover records what that costs: **widening a frozen signature in place is a
`MissingMethodException` for anything compiled against 17.0.x.** New members, not widened ones.

### D3 — The operator's resource path is a sibling of the direct overload, not a caller of it

`PlaceOnBehalfAsync(BookingRequest)` composes its own `MultiClaimBookingRequest` and calls the
private pipeline with `PlacementTerms.Operator`. It does **not** call
`PlaceAsync(BookingRequest)`, which carries ①.

This is what makes the waiver structural. There is no branch reading a flag; there is a call
that does not pass through the guard. It also means the visitor overload is untouched, so
"the visitor path is unchanged" is a property of the diff rather than a claim about it.

*Cost, accepted:* the two overloads both compose a `MultiClaimBookingRequest`, which is three
lines duplicated. Sharing them would mean hoisting ① down into the shared path — precisely what
①'s comment forbids.

### D4 — The private pipeline takes the terms as a parameter, and reads no ambient state

`PlacementTerms` is threaded through the private `PlaceAsync` to both ② and ③. Nothing infers
"operator" from an HTTP context, a service, or a thread-local: `UBookIt.Core` keeps Umbraco
types out, and an ambient answer would make the rule untestable in isolation and unstable under
`IBookingService` being called from the retention sweep or a host's own code.

### D5 — Emails: the composer is told by the caller, not by the booking

`booking-emails` requires an operator's placement to write to the booker only. The booking
carries **no marker** of how it was taken (`bookings`, *Placing a booking on a booker's
behalf*), which is deliberate — so the recipient decision cannot be recovered from the row and
must be made at the moment of placing.

**This forced a correction to an earlier draft of this design, recorded rather than quietly
fixed.** That draft said the observation port must gain nothing *and* that the booking carries no
marker — which left the email layer no way to know, and the two cannot both hold. The resolution
follows the shape already in the code: confirm, decline and move each have their own observation
because each is a distinct **act**, and "the site's own people just did this" is a fact about the
act. So `IBookingObserver` gains `BookingPlacedOnBehalfAsync`, **with a default implementation
delegating to `BookingPlacedAsync`** — an addition rather than a break, and one whose default
under-reports a distinction instead of inventing one.

The package's own adapter overrides it to publish `BookingPlacedOnBehalfNotification`, which the
email handler treats as a placement for the booker and as nothing at all for internal
recipients.

*This is a seam between two well-tested halves, which `testing-both-halves-is-not-testing-the-seam`
says is tested by neither.* The guard therefore runs through the production entry point and
asserts on what a recording mail sender actually received — not on the composer's inputs.

### D6 — Endpoint shape: one action, an exclusive-or body, both gates on the action

`POST bookings` on `BookingsController`, carrying
`[Authorize(Policy = …SensitiveDataAccessPolicy)]` **on the action** (a handler check does not
satisfy `sensitive-data`, and cannot be seen by its guard) alongside the `Manage` verb policy.

The body carries exactly one of `serviceId` / `resourceId`; both or neither is a 400 before the
domain is reached. Start is a site-local wall-clock `date` + `time` pair or a zoneless string,
refused with `interval-invalid` if it carries an offset or `Z` — the same rule, and the same
parsing, the move endpoint already uses. Reuse its helper rather than writing a second parser:
two parsers is how the two paths come to disagree about what `Z` means.

The response model carries id, reference, status and interval, and **no booker member at all**.
Structural, per the spec — a caller holding sensitive-data access today must not be the reason
the guarantee holds, because the deferred narrowing may remove that coincidence.

### D7 — Client: a second custom modal, opened from the view rather than a row

Follows `move-booking-modal.element.ts` exactly, including the two things it paid for:

- **native inputs with real `<label for>`, not `uui-*`** — `uui-label` is not a label, and no
  `uui-*` control carries `aria-describedby`;
- **focus is managed explicitly** — Umbraco's modal container does not move focus into the
  content it hosts (measured: `document.activeElement` was `<body>` on open *and* after a
  refusal). First field on open, the error summary or offending field after a refusal, the
  opening control on dismissal.

The "what to book" stage is a single `<select>` listing services and resources in two
`<optgroup>`s, which is one control, one label, and no custom keyboard handling. A two-step
wizard was considered and rejected: it doubles the focus-management surface for a choice of
one value.

## Risks / Trade-offs

- **The `SensitiveDataRedactionTests` contact-parameter guard is name-based and will fail the
  moment the request model binds `BookerEmail`.** → That is the guard working. Record the new
  parameters in `recordedContactParameters` *with* a note that this one stores rather than
  matches, and record the action in `recordedActions` as a write. Do **not** rename the field
  to something the scan does not recognise.
- **`recordedContactParameters`' failure message says a recorded parameter must "match the
  whole value exactly", and ours matches nothing.** → The spec delta makes that obligation
  explicitly vacuous for a storing endpoint; the note in the test must say so in the same
  words, or the next reader will think exactness was checked and was not.
- **An operator places a booking outside the list's window and sees nothing happen.** →
  Required to be stated in the view, not left to the operator to infer. Easy to forget because
  every other action in this screen operates on a row already on screen.
- **`PlacementTerms` is public surface the moment 17.1.0 publishes.** → The approval member
  lands now or costs a break later. Flagged in tasks as a thing to get right rather than to
  revisit.
- **A fix is where the next defect lives** — on this project a QA round's fix has caused the
  next round's defect three changes running. → Each round's fixes are handed back as new code
  for review, not as corrections.

## Migration Plan

No schema change, no migration, no data backfill. Two published interfaces gain a member
(`IBookingService`, `IServiceBookingService`) and `PlacementTerms` gains one — all additive to
consumers that do not implement those interfaces, and all landing in a **minor** (17.1.0),
which is the only place the versioning policy permits a break. The upgrade note goes in
`docs/configuration.md` beside `move`'s three.

Rollback is removing the endpoint and the members; nothing persisted depends on them, and a
booking placed on a booker's behalf is indistinguishable afterwards from any other — which is
also why there is nothing to migrate back.

## Open Questions

- Whether the resource `<optgroup>` should be ordered by name or by type. Cosmetic, decidable
  when the list is in front of us, and it changes no spec, no task and no contract.
