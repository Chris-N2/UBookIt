## Context

The universal placement primitive is `place(interval, booker, {claims})`. A service
never owns placement — it derives a claim set onto that primitive — and direct
booking is the same primitive with a claim set of one. That architecture is not in
question here and does not move.

What is in question is a **product** rule the architecture cannot supply: whether a
given resource means anything booked on its own. `BookingService` exposes two
overloads, and the shape of the seam falls out of them:

```
  DIRECT                                    VIA A SERVICE
  ──────                                    ─────────────
  DeliveryModelMapper ──┐                   ServiceBookingService
  BookingSurfaceCtrl  ──┤                            │
                        ▼                            │
        PlaceAsync(BookingRequest)                   │
                        │                            │
                        │  composes {one claim}      │  composes {one per slot}
                        └──────────┬─────────────────┘
                                   ▼
                    PlaceAsync(MultiClaimBookingRequest)
                                   │
                                   ▼
                      rules, claims, atomic store write
```

`PlaceAsync(BookingRequest)` has exactly two callers, both of them the direct path,
and it does nothing but compose a one-claim request and delegate. Service placement
builds its own `MultiClaimBookingRequest` and never passes through it.

Three standing decisions constrain the work:

- **"Everything optional" (services explore).** Services are opt-in sugar; the
  direct-resource path must never regress. This change makes the direct path
  *editor-controlled* rather than removing it, so the invariant is narrowed rather
  than inverted — and the narrowing is what the massage case requires.
- **Eligibility remains derivable from public reads (service-booking).** The
  `resource-not-eligible` failure is a pool-membership oracle, harmless only while a
  caller could have computed the answer. A refusal to book directly is the same
  shape of disclosure and takes the same remedy: publish the fact.
- **Report, don't conflate (⑨-1a, ⑨-2a).** Two different reasons for "you cannot
  book this" must not arrive as one sentence. That is the whole lesson of the two
  diagnostic changes, and this change creates a fresh opportunity to break it.

## Goals / Non-Goals

**Goals:**

- A resource that is meaningless alone cannot be booked alone.
- The same resource remains bookable as part of a service, always.
- The refusal is distinguishable from unavailability, by code and by wording.
- A conforming client can tell in advance and never provoke it.
- The editor can see and set it where they already work.

**Non-Goals:** as the proposal states — no removal of direct booking, no
auto-created services, no resource identity in roles, no pin, no bulk setting.

## Decisions

### D1 — The guard lives in Core, on the single-resource overload

Not in the delivery controller, and not in the surface controller. Both would leave
the other path open, and `BookingViewComponent` reaches Core in-process without
passing through any HTTP layer at all — a delivery-side guard would not see it.

Placed on `PlaceAsync(BookingRequest)` **before** it composes and delegates, so the
multi-claim overload is untouched and service placement cannot be caught by it. The
seam is structural rather than a flag threaded through: there is no parameter saying
"this is a direct booking", because *being a direct booking* is what calling that
overload means.

*Alternative considered — a `DirectPlacement` flag on `MultiClaimBookingRequest`.*
Rejected: it makes every service placement carry and pass a field asserting what it
is not, and a caller that forgets it gets the wrong answer silently. The overload
already carries the distinction; a flag would restate it in a way that can drift.

### D2 — The permission is a property of the resource, defaulting to withheld

Not of the type, and not of a service. Of the resource, because the editor's answer
is about a thing they own; a type-level rule would force every room to agree, and the
capability model already handles the case where one room differs from another.

Defaulting to withheld is the whole safety argument. A default of *permitted*
preserves today's behaviour exactly and leaves the massage parlour one forgotten
checkbox away from selling appointments it cannot deliver — a default that is safe
only if someone acts is not a safe default. The cost of the other direction is that a
newly created resource cannot be booked until someone says so, which is a
discoverability problem, addressed by D4, and not a correctness one.

The window for choosing this is now: nothing is published, so no installation exists
whose behaviour changes.

### D3 — A stable code of its own, never folded into unavailability

The refusal is a *configuration* fact — this resource is not offered on its own — and
it does not vary with the calendar, the time asked for, or how busy the resource is.
Reporting it as unavailability would tell a booker to try another time, which cannot
help, and would tell an editor their availability is misconfigured, which is false.

This is the same rule the deterministic-versus-transient classification follows for
`service-unavailable`, and the same one ⑨-1a and ⑨-2a follow for their reports:
distinct causes get distinct sentences, because the fix differs.

### D4 — Published on the read model, so a conforming client never sees the refusal

An anonymous caller can read a resource; it must be able to read whether that
resource can be booked directly. Otherwise the only way to discover it is to attempt
a booking and be refused, which is both a poor contract and the pool-membership
oracle problem in a new place.

This also answers the discoverability cost D2 accepts. The backoffice list and editor
show it, so "why can nothing book this room?" is answered on the screen where the
room is edited rather than by reading the delivery API's error responses.

### D5 — Availability reads stay open, deliberately

A resource that cannot be booked on its own still publishes free time, slots and
bookable starts. Two reasons, and either alone would be sufficient:

- The service-oriented front end must offer a booker a *person*, which means reading
  that person's availability, for a person who is not standalone-bookable.
- The standing eligibility-oracle guarantee rests on those reads.

The apparent oddity — bookable starts for something you cannot book — is only odd if
the read is understood as an offer. It is not: it is when this resource is free, which
is exactly what a composite booking needs to know.

### D6 — The no-JS flow states which fact it means

Pointed at a resource that is not offered on its own, it says so, and never reports
that there are no times available. The existing "unavailable" view gains a second
reason rather than a second view, because the page's shape is identical and only the
sentence differs.

This is the ⑨-1a lesson applied before the mistake rather than after: a configuration
fault that renders as ordinary emptiness is a fault nobody can find.

## Risks / Trade-offs

- **[Every existing resource stops being directly bookable on upgrade]** → True, and
  accepted with the reason stated in D2: there is no installation to upgrade. This
  risk is real for exactly as long as it takes to publish, which is why the decision
  is being taken now rather than at v1.

- **[A guard on the wrong overload silently breaks every service booking]** → The
  failure mode is severe and the test that would miss it is the obvious one. A test
  asserting "an unflagged resource cannot be booked directly" passes an
  implementation that has broken service placement entirely. The covering test must
  book **the same unflagged resource both ways** and assert opposite outcomes, and
  the mutation to run is moving the guard down into the multi-claim overload.

- **[The refusal reads as unavailability somewhere downstream]** → Three surfaces
  carry it — the domain failure, the problem-details mapping, and the no-JS view —
  and the constraint is one rule stated in three places, which is the shape this
  project has got subtly wrong before. Each needs its own assertion.

- **[The permission drifts from what the editor believes]** → It is a stored scalar
  with no derived twin and no synchronisation, so there is nothing for it to drift
  from. This is the benefit of not auto-creating a service: intent and mechanism are
  the same field.

- **[Someone later "improves" the guard into a rejection at save time]** → There is
  nothing to reject: the permission is not a validation rule and no configuration is
  invalid because of it. Worth stating so a future change does not invent one.
