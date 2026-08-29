## Context

Placement has two entry points on `IBookingService`, and which one you call **is** what
the booking means:

- `PlaceAsync(BookingRequest)` — one resource, booked on its own. It is where the
  `DirectlyBookable` guard lives, and `BookingService.cs:101-105` states why there is no
  "this is a direct booking" flag on the request: *"Calling THIS overload is what being a
  direct booking means… A flag would restate the call site in a form a caller can get
  wrong."*
- `PlaceAsync(MultiClaimBookingRequest)` — several resources claimed atomically. Service
  placement composes one of these (`ServiceBookingService.cs:843`) after resolving roles to
  resources, and the service's identity goes no further.

Nothing else calls the multi-claim overload. Neither delivery controller reaches it:
`UBookIt.Web/Controllers/BookingsController.cs:34` takes the direct overload and
`ServicesController.cs:149` goes through `ServiceBookingService`. So today the multi-claim
overload has exactly one caller that is not the direct overload delegating to it.

The read side already answers the shape question. `BookedResource` carries a `DisplayName`
beside the id with an explicit rationale: *"a caller given ids alone must read each
resource separately — one lookup per claim, per row. Removing the name from here does not
simplify anything; it moves the cost to every caller."*

## Goals / Non-Goals

**Goals:**

- A booking records the service it was placed for, or records honestly that there was none.
- The recorded value cannot be set to something the placement did not actually do.
- A management list row can show the service without a second query per row.
- A booking survives the service being renamed, retired, or deleted.

**Non-Goals:**

- Backfilling existing rows (see the proposal — inference is wrong, not merely hard).
- Any change to how roles resolve to resources, or to placement outcomes.
- Filtering the management list by service, and any delivery-API exposure.

## Decisions

### D1. The service reaches placement through its own entry point, not a field on `MultiClaimBookingRequest`

**Decision:** add `PlaceForServiceAsync(Guid serviceId, MultiClaimBookingRequest request)`
(name to settle at apply) to `IBookingService`. `MultiClaimBookingRequest` gains nothing.
`ServiceBookingService` calls the new entry point; every other caller keeps the existing
one and produces a booking with no service, which is the truth about a bare multi-claim
placement.

**Why, rather than a nullable `ServiceId` on the request:** the codebase already made this
exact decision once, in the opposite direction, and wrote down the reasoning — a request
field "would restate the call site in a form a caller can get wrong". A `ServiceId`
property is that shape precisely: it lets any caller assert an association the placement
did not make, naming a service whose roles these resources do not satisfy. The failure is
silent and permanent — it is not a wrong answer to a query, it is a wrong **fact**,
discovered much later as a number in a report that nobody can reconcile.

**Alternatives considered:**

- *Nullable `ServiceId` on `MultiClaimBookingRequest`.* Smallest diff, and the forging risk
  is not currently reachable over HTTP. Rejected because it contradicts a documented
  decision on the same type for a weaker reason than the original, and because "no HTTP
  caller can reach it today" is an argument about today.
- *Validate the supplied `ServiceId` against the service store.* Catches "no such service",
  not "the wrong service" — which is the case that matters. It also adds a read to every
  placement to defend against a shape we do not have to adopt.
- *Have `ServiceBookingService` construct the `Booking` directly.* It is in the same
  assembly and `Booking.Create` is `internal`, so it could. Rejected outright: it would
  bypass the entire rule pipeline and the atomic-placement contract.

### D2. The service is recorded as an id **and a display-name snapshot**, with no foreign key

**Decision:** the booking row stores `ServiceId` (nullable, no FK constraint) and the
service's display name **as it was at placement time**. The management projection returns
both.

**Why no FK:** a booking is a historical fact. Deleting or retiring a service must not
cascade to, or block, bookings that already happened — the booking does not stop having
been placed for that service. A nullable FK with `ON DELETE SET NULL` would silently
convert "placed for a service that no longer exists" into "placed directly", which is the
one meaning `NULL` must keep.

**Why a name snapshot rather than a join:** the alternative is joining the service table on
read, which returns the *current* name and yields nothing at all once the service is
deleted — so a row would show a service name that changed after the fact, or show none for
a booking that definitely had one. A snapshot says what was sold. This mirrors the reason
`BookedResource` carries a name, but reaches the opposite conclusion about *when* to read
it, and the difference is deliberate: a resource claim is a live association, a service
attribution is history.

**Trade-off accepted:** renaming a service does not retitle old bookings. That is the
intent, and it is what the spec will say.

### D3. `NULL` means *placed directly*, and the pre-existing rows are cleared rather than reinterpreted

`NULL` is load-bearing: it is the permanent, correct value for every direct booking, so it
cannot also be allowed to mean "we did not record this". The rows in our development
database predate the column and would carry the second meaning. They are test data, so
applying this change includes clearing them rather than leaving rows whose `NULL` is a lie.
The alternative — a third state distinguishing "none" from "unknown" — buys a distinction
that becomes permanently vestigial the moment the only rows carrying it are deleted.

### D4. The DTO carries the service as a nullable object, mirroring `BookedResourceModel`

`{ "service": { "id": ..., "displayName": ... } | null }` rather than two parallel nullable
scalars, so the invariant "a booking has a service, or it does not" is expressed in the
shape rather than in a rule that two fields must be null together. The existing endpoint
requirement — *no field added at the HTTP layer that the read port cannot supply* — is
satisfied because the port supplies both parts.

## Risks / Trade-offs

- **A third placement entry point on `IBookingService` is more surface to keep coherent.**
  → It is the smallest surface that makes the wrong value unconstructible, and the
  interface's existing shape already means "which method you call is what the booking is",
  so this extends a pattern rather than introducing one.
- **The name snapshot can drift from the service's current name, and someone will report
  it as a bug.** → Documented in the spec and in the backoffice doc as intended
  behaviour: the row shows what was booked at the time. The id is carried alongside for
  anyone who needs to reach the service as it is now.
- **`Rehydrate` gains a parameter — a breaking signature change.** → Nothing is published,
  so there is no consumer. Stated in the proposal rather than glossed, because after
  release the same edit would need an overload instead.
- **Two ways to place a multi-claim booking could drift in behaviour.** → The new entry
  point must delegate to the existing pipeline rather than duplicating it; the only
  difference between them is the value recorded on the resulting booking. A test asserts
  the two produce identical outcomes for the same request apart from the service.
- **A partially-applied change leaves the column present but never populated**, which reads
  as "all bookings are direct" — wrong, and quiet. → The `service-booking` requirement that
  placement *records* the service is what makes the column populated, and it gets a test
  that places through a service and asserts the stored value, not merely that the column
  exists.

## Migration Plan

1. Additive EF Core migration: nullable `ServiceId` on `uBookItBooking`, plus the stored
   display name. No FK, no data rewrite, no index initially — the management list does not
   filter by service in this change (adding one later is itself additive).
2. Clear the development database's existing booking rows (D3). No production data exists.
3. Rollback is the migration's `Down`: drop the column. Nothing else depends on it, and no
   released version can have written to it.

## Open Questions

- **The entry-point name.** `PlaceForServiceAsync` reads well but pairs oddly with the two
  existing `PlaceAsync` overloads. An overload taking `(Guid serviceId,
  MultiClaimBookingRequest)` is more symmetrical but makes the distinction easier to miss
  at a call site — which is the opposite of D1's goal. Settle at apply, favouring the
  distinct name.
- **Whether the stored display name belongs on `BookingRow` or is resolved into
  `BookingSummary` only.** Storing it is what makes it a snapshot (D2), so this is decided
  in principle; what is open is whether anything other than the management projection ever
  reads it.
