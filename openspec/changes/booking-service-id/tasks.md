## 1. The domain

- [x] 1.1 Add the optional service attribution to `Booking` — a nullable value carrying id
  and the display name snapshot, mirroring `BookedResource`'s id-and-name shape rather than
  two loose nullable properties.
- [x] 1.2 Thread it through `Booking.Create` and `Booking.Rehydrate`. Rehydration accepts it
  as stored and **does not revalidate it**: the service may no longer exist, which changes
  nothing about what the booking was placed for.
- [x] 1.3 Add the service-placement entry point to `IBookingService` (design D1), taking the
  service alongside a `MultiClaimBookingRequest`. **`MultiClaimBookingRequest` gains
  nothing** — that is the whole point of D1. Settle the name at this point, favouring a
  distinct name over an overload so the distinction cannot be missed at a call site.
- [x] 1.4 Implement it by delegating to the existing pipeline, so the only difference
  between the two multi-claim paths is the value recorded. Duplicating the pipeline is how
  the two drift.

## 2. Persistence

- [x] 2.1 `BookingRow` gains nullable `ServiceId` and the nullable stored service name.
- [x] 2.2 One additive migration: two nullable columns on `uBookItBooking`. **No foreign
  key, no cascade, no index** (design D2) — a booking outlives its service, and nothing in
  this change filters by service.
- [x] 2.3 Row mapper both ways, including the direct case: NULL means placed directly, and
  must round-trip as *absent* rather than as an empty attribution.
- [x] 2.4 The management projection returns the stored name — **not a join to the service
  table**, which would return the current name and would return nothing once the service is
  deleted.
- [x] 2.5 Clear the development database's existing booking rows (design D3), whose NULL
  would otherwise mean "not recorded" rather than "placed directly".

## 3. Service placement

- [x] 3.1 `ServiceBookingService` calls the new entry point, passing the service it already
  has in hand at `ServiceBookingService.cs:843`.
- [x] 3.2 Take the display-name snapshot at placement, from the service already loaded for
  resolution — not a second read.

## 4. The read port and the HTTP surface

- [x] 4.1 `BookingSummary` carries the optional service.
- [x] 4.2 The booking DTO carries it as a **single nullable object** with id and name
  (design D4), not two parallel nullable scalars.
- [x] 4.3 Regenerate the TypeScript client against the running TestSite (HTTPS on 44348) and
  commit it. Confirm the generated member is nullable.
- [x] 4.4 Extend `GeneratedClientTests` so the nullable service member is pinned the way the
  required window is — and remember what round 2 of the previous change established: the
  generated file is a **committed artifact**, so it is only ever the client half of a guard.

## 5. Guards that would catch the real mistakes

- [x] 5.1 **The column is populated, not merely present.** Place through a service and
  assert the *stored* value. A migration that adds a column nobody writes reads as "every
  booking was placed directly" — wrong, and completely silent.
- [x] 5.2 **A direct booking stores nothing**, and reads back as absent rather than as an
  empty attribution.
- [x] 5.3 **The general contract cannot name a service** — assert over
  `MultiClaimBookingRequest`'s members that no service member exists. This is the guarantee
  D1 exists to make, and it is exactly the one a later "small convenience" would undo.
- [x] 5.4 **A booking outlives its service**: delete the service, then assert the booking row
  survives with both columns unchanged. This is the guard that fails if anyone adds the
  foreign key that looks obviously correct.
- [x] 5.5 **The name is a snapshot**: rename the service after placing, and assert the
  booking still reports the old name.
- [x] 5.6 **Placement behaviour is unchanged** — the same request through the new entry point
  and the old one yields identical outcomes apart from the recorded service.
- [x] 5.7 Mutation-check every guard above, restoring by **edit** rather than by a
  timestamp-preserving copy. Pay particular attention to 5.1 and 5.4: both assert that
  something *is* stored or *survives*, and an assertion of presence is the kind that most
  easily passes for the wrong reason.

## 6. Documentation and close

- [x] 6.1 State in the backoffice documentation that the service shown is the name recorded
  at booking time, and does not change when a service is renamed. Someone will report the
  drift as a bug otherwise.
- [x] 6.2 Full solution build at **zero** warnings, from a clean `bin`/`obj` — and do not
  sweep `node_modules` while doing it.
- [x] 6.3 Full test suite green, compared against the 1690 baseline.
- [x] 6.4 `openspec validate --all --strict`.
- [x] 6.5 Re-read: trace every delta clause to the code implementing it, and diff the
  guarantees of all four MODIFIED requirements clause by clause — `Booking shape`, `Booking
  rehydration`, `Schema shape and naming`, and the two `booking-management` requirements.
- [x] 6.6 Sweep sibling specs for sentences this change falsifies. **It found one, and the
  sweep is the only thing that would have.** `service-booking`'s "Direct-resource booking is
  unaffected" guaranteed that a booking placed through a service is "indistinguishable in
  shape from a directly placed one" — which this change makes false *by design*, in a
  requirement nothing else about the change touches. It is now MODIFIED: the clause is
  **narrowed, not dropped**, restating every behavioural guarantee it was written for (one
  interval, the same claim rows, identical blocking, the same cancellation) and admitting
  the single difference. Also checked and **not** falsified: `delivery-api`'s `POST
  /bookings` response enumeration and its "unchanged in route, request model, response
  model, and semantics" clause (the delivery surface is deliberately untouched, and direct
  placement still records no service), the service-booking placement requirement at :373
  (which enumerates claims, not attribution), and `bookings`' claim-plurality rule.
- [x] 6.7 Hand to `qa-review` in a **fresh context or subagent**.

## 7. What went beyond the task list

- [x] 7.1 **`ResolveCandidatesAsync` was split rather than widened.** Placement needed the
  service's name and only had its id; the service is already loaded inside that method to
  read its roles and duration. A private `ResolveServiceAndCandidatesAsync` now returns
  both and the public method projects the pools out of it — so the snapshot costs no second
  read, and cannot disagree with the pools by being read at a different moment. The
  published signature is unchanged: no caller of `ResolveCandidatesAsync` wants the service.
- [x] 7.2 **Two test doubles had to move with the call site.** `CountingBookingService` and
  `RacingBookingService` in `MultiRolePlacementTests` intercept multi-claim placement, which
  service placement no longer calls. Left alone they would have compiled, passed, and
  quietly stopped doing anything — the counter counting zero, the race never raced, and
  every assertion still green. Both now implement the new entry point with the same
  behaviour.
- [x] 7.3 **The migration list in `Multi_claim_placement_needed_no_schema_change` gained an
  entry**, which that test exists to force. Checked as it asks: two additive nullable
  columns on `uBookItBooking`, no foreign key, no index, `uBookItResourceClaim` untouched —
  so the claim-shape guarantee it names still holds. It is the first entry on that list to
  touch the booking table at all, which is why it was checked rather than appended.
- [x] 7.4 **Nine mutations, all caught**: the recorded name drifting from the service; the
  pipeline not recording it; the entry point discarding it; a `ServiceId` appearing on
  `MultiClaimBookingRequest`; the row mapper dropping either column; the DTO mapper dropping
  the service; the client splitting it into two scalars; and the documentation dropping the
  snapshot warning.
- [x] 7.5 **`ServiceName` is bounded at 512** to match the service's own name column. EF's
  default was `nvarchar(max)` — the only unbounded string in the schema, and a shorter bound
  would silently truncate a name the service itself accepts.
- [x] 7.6 **The dev database's booking rows are gone**: 78 bookings and 87 claims deleted,
  the 29 resources and 25 services left alone. Every one of those bookings carried a NULL
  service meaning "not recorded", which is the one thing NULL must not mean.
