## Context

`Booker` (`UBookIt.Core/Bookings/Booker.cs`) is a sealed record whose factory refuses an empty
name and requires a well-formed email. Contact details are therefore not blankable, and the
domain has no state meaning "these were erased". `Booking.Booker` is non-null and get-only.

Three facts established by reading the code, each of which shapes a decision below:

1. **Booker PII has exactly one durable home.** `BookingRow` carries `MemberKey`, `BookerName`,
   `BookerEmail`, `BookerPhone`. The delivery API has no endpoint that reads a booking back —
   enumerated: `free-time`, `slots`, `bookable-starts` ×2, `resources` ×2, `services` ×2, and
   two `POST` placements whose booker echo is the request's own body, in-request. So erasing
   the row erases the data, with no second copy to chase.
2. **`IBookingStore.UpdateAsync` writes only `row.Status`.** An erasure routed through it as it
   stands would be a silent no-op that every test asserting on the in-memory aggregate would
   pass.
3. **`TimeProvider` is already injected into `BookingService`.** No new clock abstraction is
   needed for the erasure instant.

The wire shape is constrained by the `sensitive-data` capability, which spent
`BookingModel.Booker`'s null on "withheld from you" and stated that a member whose absence is
already meaningful must not be overloaded to carry withholding as well. Erasure makes absence
meaningful, so that null cannot be reused.

## Goals / Non-Goals

**Goals:**

- A booking's personal data can be removed while the booking itself survives intact — same
  interval, same claims, same reference, same status.
- The erased state is expressible in the domain by construction, not by convention.
- A reader of a booking, at any layer, cannot silently skip the question of whether the details
  are present.
- A caller can distinguish *shown*, *withheld from you*, and *erased* without inference.
- The erase verb is defined once, so retention (change ③) reuses it rather than duplicating it.

**Non-Goals:**

- Locating bookings by email (change ②), retention (③), a privacy notice (④).
- An audit trail of who erased what — it must not record the erased address, and designing that
  properly is not this change.
- Reversal.

## Decisions

### D1 — The erased state lives inside `Booker`, not on `Booking`

`Booking.Booker` stays non-null. `Booker` becomes a record that carries **either** contact
details **or** an erasure instant:

```csharp
public sealed record BookerContact(string Name, string Email, string? Phone);

public sealed record Booker
{
    public Guid? MemberKey { get; }          // null once erased
    public BookerContact? Contact { get; }   // null  <=>  erased
    public DateTimeOffset? ErasedUtc { get; }
}
```

Private constructor; two factories — `Create(...)` as today, and `Erased(DateTimeOffset)` — so
the combination "no contact and no erasure instant" is never produced.

*Alternatives considered.*

- **Nullable `Booking.Booker`.** Rejected. It makes "a booking with no booker" representable,
  which is the exact premise the `sensitive-data` capability relies on to give its null a
  single meaning, and it would change `Booking.Rehydrate`'s contract. A booking always has a
  booker; what it may lack is their details.
- **`Booking.ErasedUtc` beside an intact `Booker`.** Rejected. Two members that must agree, and
  half-populated states observable — the pattern `sensitive-data` rejects by name.
- **A closed hierarchy (`abstract record Booker` with `Identified` / `Erased` subtypes).**
  Genuinely attractive: it makes the invalid state unrepresentable rather than merely
  unproduced. Rejected because **nullable reference types with warnings-as-errors already
  deliver the property the hierarchy was wanted for** — every site that reads `Contact.Name`
  fails to compile until it handles the null. The house convention does the union's work, and
  the hierarchy's cost lands on `Rehydrate`, the EF projection and every mapper.

**Only one null remains and it has one meaning.** `Contact is null` means erased, in the domain,
and D2 removes the wire's null entirely. An earlier sketch had a domain null meaning *erased*
and a wire null meaning *withheld* at the same time; that is the hazard D2 exists to avoid.

### D2 — The wire carries an explicit state, and `BookingModel.Booker` stops being nullable

```jsonc
"booker": { "state": "Shown",    "contact": { "name": "…", "email": "…" } }
"booker": { "state": "Withheld" }
"booker": { "state": "Erased",   "erasedUtc": "2026-01-03T09:15:00Z" }
```

`state` is a **string**, not the domain enum, for the reason `BookingModel.Status` is a string:
an enum's members are a versioning commitment and domain types do not appear in the HTTP
contract.

The discriminator is what makes this legitimate under the "no parallel nullables" rule. That
rule objects to fields a client must infer meaning from by observing which are present; here
the client reads `state` and never infers. Contact details stay **clumped in one nested object**
so name and email cannot be observed half-populated, which is the guarantee the 0.2.0 shape was
built for and which survives unchanged.

**BREAKING, unpublished** — the nullable `Booker` member was added after `0.1.0` shipped.

*Alternative considered:* keep `Booker: null` for withheld and use `{ state: "erased" }` for
erased. Rejected: half the states in the shape and half in a field, and it keeps a null whose
meaning must be documented rather than read.

### D3 — Erasure outranks withholding, and is shown to everyone with section access

Composition is one expression at one call site:

```csharp
Booker = summary.ErasedUtc is { } erased ? Erased(erased)
       : visibility is BookerVisibility.Shown ? Shown(contact)
       : Withheld
```

The `BookerVisibility` argument stays **required**, so `sensitive-data`'s "the visibility
decision cannot be forgotten" requirement is untouched — it is simply unused on the branch
where there is nothing to disclose.

That a record was erased is a fact about the **record**, not about the person: it discloses
nothing about who they were. And the two absences demand different actions — *"ask a colleague
who can see it"* versus *"this is gone; nobody can retrieve it"* — which is the same test that
rejected blanking in the first place. Withholding erasure from a section user would recreate
the ambiguity one level down.

### D4 — `Booking.EraseBooker(instant)` mirrors the status machine

`Status` is `{ get; private set; }` mutated by `Confirm()` / `Decline()` / `Cancel()`. `Booker`
becomes `{ get; private set; }` mutated by `EraseBooker(DateTimeOffset)`. Same shape, same file,
nothing new to learn.

The verb on the service is `IBookingService.EraseBookerAsync(Guid bookingId, …)`, alongside
`CancelAsync`, taking its instant from the already-injected `TimeProvider`.

### D5 — `UpdateAsync` must persist the booker columns, and a test must prove it

`SqlBookingStore.UpdateAsync` sets `row.Status` and nothing else. Erasure through it would
change the aggregate in memory, return success, and leave the database untouched — and every
test that asserted against the returned `Booking` would agree it worked.

`UpdateAsync` therefore writes the booker columns as well as the status, and its port
documentation stops saying "a status change". The guard is a **round-trip through storage**:
erase, then re-read the booking from a fresh context, and assert the columns are null. Asserting
on the returned aggregate proves nothing here, which is precisely the unobservable-mutation trap
this project has hit before.

*Alternative considered:* a dedicated `EraseBookerAsync` on the store issuing a targeted
`UPDATE`. Rejected — a second write path to the same row, free to disagree with the first about
what a booking's persisted state is.

### D6 — Erasure is idempotent, unlike cancellation

Erasing an erased booking succeeds and changes nothing.

This is deliberately the **opposite** conclusion from `CancelBooking`, which refuses a second
attempt because "a caller told 'cancelled' when nothing changed cannot tell a completed action
from a rejected one". The difference is real:

- Cancelling is a **transition**, and which state it came from matters — a second attempt often
  means the operator is acting on a stale list.
- Erasing is **terminal and absorbing**. The observable outcome is identical either way: the
  details are gone. There is no prior state a caller could be wrong about.
- Change ③ runs this verb from a retryable background job. A verb that fails on
  already-done would make the job's normal steady state an error.

### D7 — The package does not refuse to erase a future booking

Erasing a booking that has not happened yet leaves a site unable to contact somebody who will
turn up. That is a real operational hazard — and refusing it would be the package making a legal
judgement it is not qualified to make, since whether the contractual basis still applies is the
site's call, not ours. So: permitted, no domain rule, and the documentation states the
consequence plainly. The decision belongs to the operator, who has the context.

### D8 — Erasure clears the member key

An Umbraco member key identifies a person as reliably as their email. Leaving it while removing
the name and address would make the erasure a gesture: the identity survives, and re-attaching
a name to it needs one lookup.

## Risks / Trade-offs

- **A future change re-homes booker PII and quietly falsifies the erasure claim.** Emails
  (0.5.0) and any audit trail are the obvious candidates. → The single-durable-home constraint
  is written into the `booker-erasure` spec as a requirement, so a change that adds a second
  copy has to confront it rather than pass beside it.
- **`UpdateAsync` grows silently again.** A later field added to `Booking` and not to
  `UpdateAsync` repeats D5's fault. → The round-trip guard is written against re-read state, so
  it fails for the next field too, rather than only for this one.
- **The membership-snapshot guard fails during apply.** By design. → Its message names the
  members; update the recorded set once the shape is final, not before.
- **Making `BookerName`/`BookerEmail` nullable is a widening `ALTER COLUMN`.** Non-destructive
  and it cannot fail on existing data, but it is a schema change to a published table. → An
  additive migration; no backfill; existing rows keep their values and a null erasure instant.
- **Erasing by booking, not by person.** An operator honouring a subject request must erase each
  booking, and until change ② they must find them by date. → Stated as a boundary in the docs
  rather than implied to be complete.
- **The three-state model reaches the Lit view.** A third rendering, distinct from the withheld
  one and from an empty cell, and it must be localized like its neighbour.
