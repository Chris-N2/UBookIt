## 1. The domain

- [ ] 1.1 Add the optional service attribution to `Booking` — a nullable value carrying id
  and the display name snapshot, mirroring `BookedResource`'s id-and-name shape rather than
  two loose nullable properties.
- [ ] 1.2 Thread it through `Booking.Create` and `Booking.Rehydrate`. Rehydration accepts it
  as stored and **does not revalidate it**: the service may no longer exist, which changes
  nothing about what the booking was placed for.
- [ ] 1.3 Add the service-placement entry point to `IBookingService` (design D1), taking the
  service alongside a `MultiClaimBookingRequest`. **`MultiClaimBookingRequest` gains
  nothing** — that is the whole point of D1. Settle the name at this point, favouring a
  distinct name over an overload so the distinction cannot be missed at a call site.
- [ ] 1.4 Implement it by delegating to the existing pipeline, so the only difference
  between the two multi-claim paths is the value recorded. Duplicating the pipeline is how
  the two drift.

## 2. Persistence

- [ ] 2.1 `BookingRow` gains nullable `ServiceId` and the nullable stored service name.
- [ ] 2.2 One additive migration: two nullable columns on `uBookItBooking`. **No foreign
  key, no cascade, no index** (design D2) — a booking outlives its service, and nothing in
  this change filters by service.
- [ ] 2.3 Row mapper both ways, including the direct case: NULL means placed directly, and
  must round-trip as *absent* rather than as an empty attribution.
- [ ] 2.4 The management projection returns the stored name — **not a join to the service
  table**, which would return the current name and would return nothing once the service is
  deleted.
- [ ] 2.5 Clear the development database's existing booking rows (design D3), whose NULL
  would otherwise mean "not recorded" rather than "placed directly".

## 3. Service placement

- [ ] 3.1 `ServiceBookingService` calls the new entry point, passing the service it already
  has in hand at `ServiceBookingService.cs:843`.
- [ ] 3.2 Take the display-name snapshot at placement, from the service already loaded for
  resolution — not a second read.

## 4. The read port and the HTTP surface

- [ ] 4.1 `BookingSummary` carries the optional service.
- [ ] 4.2 The booking DTO carries it as a **single nullable object** with id and name
  (design D4), not two parallel nullable scalars.
- [ ] 4.3 Regenerate the TypeScript client against the running TestSite (HTTPS on 44348) and
  commit it. Confirm the generated member is nullable.
- [ ] 4.4 Extend `GeneratedClientTests` so the nullable service member is pinned the way the
  required window is — and remember what round 2 of the previous change established: the
  generated file is a **committed artifact**, so it is only ever the client half of a guard.

## 5. Guards that would catch the real mistakes

- [ ] 5.1 **The column is populated, not merely present.** Place through a service and
  assert the *stored* value. A migration that adds a column nobody writes reads as "every
  booking was placed directly" — wrong, and completely silent.
- [ ] 5.2 **A direct booking stores nothing**, and reads back as absent rather than as an
  empty attribution.
- [ ] 5.3 **The general contract cannot name a service** — assert over
  `MultiClaimBookingRequest`'s members that no service member exists. This is the guarantee
  D1 exists to make, and it is exactly the one a later "small convenience" would undo.
- [ ] 5.4 **A booking outlives its service**: delete the service, then assert the booking row
  survives with both columns unchanged. This is the guard that fails if anyone adds the
  foreign key that looks obviously correct.
- [ ] 5.5 **The name is a snapshot**: rename the service after placing, and assert the
  booking still reports the old name.
- [ ] 5.6 **Placement behaviour is unchanged** — the same request through the new entry point
  and the old one yields identical outcomes apart from the recorded service.
- [ ] 5.7 Mutation-check every guard above, restoring by **edit** rather than by a
  timestamp-preserving copy. Pay particular attention to 5.1 and 5.4: both assert that
  something *is* stored or *survives*, and an assertion of presence is the kind that most
  easily passes for the wrong reason.

## 6. Documentation and close

- [ ] 6.1 State in the backoffice documentation that the service shown is the name recorded
  at booking time, and does not change when a service is renamed. Someone will report the
  drift as a bug otherwise.
- [ ] 6.2 Full solution build at **zero** warnings, from a clean `bin`/`obj` — and do not
  sweep `node_modules` while doing it.
- [ ] 6.3 Full test suite green, compared against the 1690 baseline.
- [ ] 6.4 `openspec validate --all --strict`.
- [ ] 6.5 Re-read: trace every delta clause to the code implementing it, and diff the
  guarantees of all four MODIFIED requirements clause by clause — `Booking shape`, `Booking
  rehydration`, `Schema shape and naming`, and the two `booking-management` requirements.
- [ ] 6.6 Sweep sibling specs for sentences this change falsifies. Named candidates:
  anything in `bookings` or `service-booking` asserting what a booking carries, and
  `delivery-api`'s booking response shape — which this change deliberately does **not**
  extend, so it should still be true as written.
- [ ] 6.7 Hand to `qa-review` in a **fresh context or subagent**.
