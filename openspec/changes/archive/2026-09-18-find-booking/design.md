# Design — find a booking

## Context

See `proposal.md` — *Why*. The state that shapes the approach:

```
  bookings screen today            reads that exist
  ─────────────────────            ────────────────────────────────────────────
  From / To  (≤ 31 days)  ──────▶  IBookingManagementStore.ListAsync(BookingQuery)
  Status / Resource                    windowed by construction, paged, Read-gated

  (nothing)               · · ·    IBookingManagementStore.FindByBookerEmailAsync
                                       unwindowed, exact, paged, indexed,
                                       Sensitive-data-gated ON THE ACTION — no UI

  (nothing)                        by reference: does not exist. The reference is
                                       unique-indexed, stored canonically, and
                                       BookingReference.TryParse accepts what a
                                       person types.
```

Three constraints the codebase already states, which this design honours rather than
rediscovers:

- *"The window is not optional and SHALL NOT be relaxed to serve this"* — `find-by-booker`'s
  reason for being a separate read. Same reason applies to a reference.
- *"A filter added as a parameter to an endpoint gated only on section access violates this,
  whatever check its handler performs"* — why any contact-detail read is its own action. A
  reference is not a contact detail, but the shape is the same and we keep it.
- `BookingsViewReferenceTests`: the detail-rendering path consults one source. The view may read
  `hasAccessToSensitiveData` to decide which *controls* to offer (narrowed in ㊳); it may not
  consult it to decide what a *row* shows.

## Goals

- An operator with a reference or an email finds the booking in one action, from the screen
  they are already on.
- Neither lookup weakens, widens or restates the list's window guarantee or the withholding
  rule.
- Every row action (move, cancel, confirm, decline, erase) works identically on a found row.

## Non-Goals

Beyond the proposal's: no change to `BookingSummary`, no change to `BookingModel`, no change to
`IBookingStore`, no change to the delivery API.

## Decisions

### D1 — By-reference is a key seek on the management store, returning the list's own row

`IBookingManagementStore.FindByReferenceAsync(BookingReference reference, …)` →
`BookingSummary?`. Not on `IBookingStore`: the controller contract guard forbids the API layer
depending on the rehydration store, and `BookingSummary` (resource names, service name, contact
in its three stated conditions) is what the screen renders. Returning the list's own record is
what makes "same row, same withholding" a property of the type rather than a claim.

*Alternative rejected:* a `reference` parameter on `BookingQuery`. It would make the window
optional-in-practice for one path, which is the exact thing the window requirement forbids.

### D2 — The endpoint is `GET`, and the reference travels in the route

`GET bookings/by-reference/{reference}`. `find-by-booker` is POST-for-a-read because an email in
a URL reaches every log between the browser and the server. A reference is designed to be
written on things; a URL is a thing. The action calls `BookingReference.TryParse` and answers:

| input | response |
|---|---|
| not a well-formed reference | 400, `reference-invalid` |
| well-formed, no booking has it | 404, `booking-not-found` |
| found | 200, `BookingModel` — the list's row model, mapped by the list's mapper |

Two codes because they call for different corrections. Read-gated; no sensitive-data gate, and
the response withholds exactly as the list does because it *is* a list row.

### D3 — One Find control; the client dispatches by shape; the server stays the rule

A single labelled input and a Find button. The client classifies what was typed:

```
  typed text ──▶ contains "@" ? ──yes──▶ find-by-email (if offered)
                     │
                     no
                     ▼
              parses as a reference ? ──yes──▶ by-reference
                     │
                     no
                     ▼
              "That is not a booking reference or an email address."
```

The classification is a **convenience**, not the rule: a value that passes the client's shape
test and fails the server's is shown the server's code. The reference shape test is a port of
`TryParse`'s rule to TypeScript — alphabet, length, strip separators, upper-case — and is
tested as a pure function against the same vectors the C# tests use, so the two cannot drift
without a test noticing.

*Alternative rejected:* a mode switch (reference / email). It is a second control for a choice
the input already makes, and it puts the "you may not use this" state in a disabled radio rather
than in a sentence.

### D4 — The email route is offered only to sensitive-data holders, and refused honestly otherwise

`hasAccessToSensitiveData` from the current-user context, exactly as ㊳ gates the New booking
button. For a holder, the label reads *"Find a booking by reference or email address"*. For
anyone else it reads *"Find a booking by reference"*, and an email typed anyway gets a sentence
saying finding by email needs the Sensitive data group — telling them who to ask, which is what
the withheld-details note already does for rows. This is control-gating, not detail-gating, so it
is on the side of the `BookingsViewReferenceTests` narrowing that permits it.

### D5 — The table becomes the result set; the window view is one "Back" away

The view gains a mode: `window` (today's behaviour), `reference`, `email`. In a lookup mode:

- the window and filter controls are hidden — they do not apply, and a visible filter that
  changes nothing teaches an operator that filters are decorative;
- a `role="status"` line above the table says what is shown — *"Booking BJQ4-ZP5C"* or
  *"Bookings for ada@example.com, all dates"* — and carries the **Back to dates** control;
- the rows render through the same `#renderRow`, so every action button is the same button;
- the pager works unchanged for email results (already paged) and is absent for a reference hit
  (one row).

**After a row action in a lookup mode, the lookup is re-run, not the window.** The existing
`#settleAfterRowAction` re-fetches the window; it becomes mode-aware. A moved booking stays
found by its reference whatever date it moved to, which is the honest outcome — it is still that
booking — and is a nicer property than the window view has.

**Back to dates restores the window view and puts focus on the From input.** Focus is managed
explicitly for the reason every dialog in this client records.

### D6 — Not-found is a sentence in the status line, not an empty table

An empty table under a window means "nothing booked"; under a reference it would mean "no such
booking", and the two look identical. So a miss renders the status line only — *"No booking has
the reference BJQ4-ZP5C"* / *"No bookings for ada@example.com"* — with Back to dates, and no
table. An empty *email* result is genuinely empty and is stated as such, not as an error.

## Risks / Trade-offs

- **`BookingsViewReferenceTests` will see `hasAccessToSensitiveData` used a second time.** →
  It is in the same place as ㊳'s use (control-gating, outside `#bookerCell`) and the guard is
  scoped to the cell body. If it fires, the guard is telling us something and we do not widen it.
- **The TypeScript reference-shape test can drift from `TryParse`.** → Shared test vectors: the
  C# suite exports its accept/reject cases as a fixture the client test reads. If that is more
  plumbing than it is worth, the fallback is the client test *quoting* the alphabet constant and
  a server-side guard asserting the two files agree.
- **`#settleAfterRowAction` becoming mode-aware is the seam most likely to hide a defect.** → A
  client test per mode for the after-action refetch, and the live probe moves a found booking to
  a date outside the original window and confirms it is still shown.
- **The `SensitiveDataRedactionTests` contact-parameter scan is name-based.** The route
  parameter is `reference`; it must NOT be recorded as a contact detail, because it is not one.
  → Record the action as a read; leave `recordedContactParameters` alone; say so in the note.
- **Every fix is new code.** → Each QA round's fixes go back as new code, not corrections.

## Migration Plan

No schema change, no migration. One published interface gains one member
(`IBookingManagementStore.FindByReferenceAsync`), declared, landing in the 17.1.0 minor beside
the previous additions; a host that substitutes its own management store must add it. One new
stable failure code, additive. Rollback is removing the endpoint, the member and the control —
nothing persisted depends on any of them.

## Open Questions

- Whether the Find control sits beside the heading or inside the filter bar. Cosmetic; decidable
  with the screen in front of us; changes no spec, task or contract.
