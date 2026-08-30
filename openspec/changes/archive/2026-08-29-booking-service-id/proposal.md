## Why

A booking does not record which service produced it. `ServiceBookingService` resolves
roles to resources, composes a `MultiClaimBookingRequest`, and the service's identity is
dropped at that boundary (`ServiceBookingService.cs:843`) — so once a booking is stored,
nothing can say whether it came from "Consultation, 45 minutes" or from someone booking
the room directly. Every question an operator will ask of the bookings screen beyond "who
and when" needs it: what was booked, how much of each service was sold, which service to
name when confirming.

**Now, because the only data is our own.** The column is nullable and needs no backfill,
but a *correct* value for existing rows cannot be recovered in general — inferring the
service from a booking's resources only works if every service's required-resource set is
disjoint from every other's, which two services sharing a room already breaks. Nothing is
released, so the existing rows are ours and can be cleared. That will never be true again.
It also lands **before the backoffice bookings screen**, so the screen is built against
the final row shape rather than gaining a column immediately after being reviewed.

## What Changes

- **`Booking` carries an optional service.** A booking placed through a service records
  which one; a booking placed directly records none.
- **`ServiceId` is nullable, and that is a statement about the domain rather than a
  migration concession.** A resource carries `DirectlyBookable` per resource, defaulting
  to withheld (`resources` spec), and a resource withholding it "remains fully usable as
  part of a service". So both kinds of booking coexist on any site, permanently: a direct
  booking has no service and never will. `NULL` means *placed directly*, not *unknown*.
- **The service identity reaches placement through its own entry point, not through a
  field on the existing request.** `MultiClaimBookingRequest` stays service-free. See
  design D1 — this is the one decision in this change worth arguing about.
- **The management read port carries the service the way it already carries resources**:
  id **and** display name, so a list row does not need a second lookup per row and the
  screen does not render a raw GUID. `BookedResource` sets this precedent explicitly.
- **The management API and its TypeScript client expose it**, nullable, as a name and id.
- **An additive EF Core migration** adds a nullable `ServiceId` column to
  `uBookItBooking`. No data is rewritten and no column is dropped.

## Non-goals

- **Backfilling historical service ids.** Not attempted, and not because it is hard:
  resource-set inference is *wrong* whenever two services can draw on the same resource,
  and a plausible-but-wrong service on a booking is worse than an honest `NULL`. The
  existing rows are test data in our own database.
- **A foreign key to the service table, or any cascade.** A booking is a historical fact
  and must survive the service being renamed, retired or deleted. The service the booking
  was placed for does not stop having been that service. Referential behaviour is design
  D2, and the display name is a **snapshot at placement time** for the same reason.
- **Exposing the service on the delivery API.** A visitor placing a booking already knows
  which service they chose, and widening the anonymous surface is scope this change has no
  need for. If a "my bookings" view ever needs it, that is its change.
- **Filtering or grouping bookings by service.** The column lands; the management list
  gains no `serviceId` filter here. That belongs with the screen that would use it.
- **Changing how services resolve roles to resources.** Placement behaviour is untouched;
  this change records an outcome it already produces.
- **The backoffice bookings screen.** Next change, against this row shape.

## Capabilities

### New Capabilities
- *(none — this extends existing capabilities)*

### Modified Capabilities

- `bookings`: the aggregate gains an optional service, and the placement contract gains
  the entry point that sets it. States that a booking placed directly carries none, that
  the recorded service is not revalidated on read, and that a booking outlives the service
  it names.
- `persistence`: the booking row gains a nullable `ServiceId` and the projection gains the
  service's display name. The additive-migration guarantee is restated, not relaxed.
- `booking-management`: the read port's `BookingSummary` carries the service, and the
  management endpoint's DTO carries it as a nullable id and name — under the existing
  requirement that **no field is added at the HTTP layer the port cannot supply**. **Its
  filter requirement is also MODIFIED**, found by the sweep at sync time: it explained the
  absent service filter by asserting that a booking does not record the service that
  produced it, which this change makes false, and its scenario required the package to
  *publish* that explanation. The conclusion is unchanged — no service filter here — and
  only the reason moves, from a limit of the data to a scope decision.
- `service-booking`: service placement SHALL record the service on the booking it
  produces. This is the requirement that makes the column populated rather than merely
  present. **Its "Direct-resource booking is unaffected" requirement is also MODIFIED**,
  found by the falsified-sentence sweep at apply time: it guaranteed a service booking was
  "indistinguishable in shape from a directly placed one", which this change makes false
  by design. The clause is **narrowed, not dropped** — every behavioural guarantee it was
  written for (one interval, the same claim rows, identical blocking, the same
  cancellation) is restated, and the single admitted difference is the recorded fact of
  where the booking came from.

## Impact

- **`UBookIt.Core`**: `Booking` (an optional `ServiceId`; `Rehydrate` and `Create` gain a
  parameter), a service-aware placement entry point on `IBookingService`,
  `BookingSummary` and a `BookedService`-shaped member, and `ServiceBookingService`
  passing the service through.
- **`UBookIt.Persistence`**: `BookingRow.ServiceId`, one additive migration, the row
  mapper, and the management projection's join for the display name.
- **`UBookIt.Backoffice`**: the booking DTO, its mapper, and the regenerated TypeScript
  client.
- **Public API surface**: `Booking.Rehydrate` and the placement contract change shape.
  **Called out explicitly per the conventions**: `Rehydrate` gains a parameter, which is a
  breaking change to a published signature — except that **nothing is published**, so
  there is no consumer to break and no compatibility promise yet in force. It is stated
  here rather than glossed, because the same edit after release would need a different
  answer.
- **Schema**: one nullable column added. **Additive, not destructive** — no data is
  rewritten, nothing is dropped, and the migration needs no upgrade path because there is
  no released version to upgrade from.
- **`UBookIt.Web`**: unchanged. Direct placement already means "no service", and the
  delivery API is deliberately untouched.
