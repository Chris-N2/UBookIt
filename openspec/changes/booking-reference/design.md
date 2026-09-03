## Context

Measured before designing.

- **The view already calls it a reference.** `Views/Shared/Components/Booking/Confirmation.cshtml`
  renders `<dt>Reference</dt><dd>@Model.BookingId</dd>`. The label is right; the value is a Guid.
- **Identity is supplied to the aggregate, and generated one level up.** `Booking.Create` is
  `internal` and takes `Guid id` from its caller — but that caller is `BookingService`, inside
  Core, calling `Guid.NewGuid()`. So the reference can arrive the same way the id does, and Core
  is **not** the deterministic assembly the first draft of D2 claimed it was.
- **`Booking.Rehydrate` is `public`** and is the persistence boundary. It is the breaking part.
- **`Booker` holds `Name`, `Email`, optional `Phone`, optional `MemberKey`.** Nothing in the
  booking survives their removal today except the interval and the claims — which is why the
  reference matters to the later erasure work.
- The claim conflict rule is untouched by this change.

## Goals / Non-Goals

**Goals:** a booking can be quoted, matched and discussed by a human; the identifier is stable
for the life of the booking; existing bookings get one.

**Non-Goals:** search, emails, customer self-service, or any change to the Guid, the routes or
the conflict model.

## Decisions

### D1. Eight characters, no vowels, no ambiguous glyphs

**Decision:** the alphabet is `BCDFGHJKMNPQRSTVWXZ` plus `23456789` — 27 symbols. References
are 8 symbols, stored canonical (uppercase, no separator), displayed grouped as `XXXX-XXXX`,
and accepted on input in any case with separators and whitespace stripped.

**Why no vowels:** a random string containing vowels eventually spells something. A booking
system that emails a customer a reference which happens to be an obscenity is a story you only
get to have once. Removing vowels removes the entire class, and it costs almost nothing.

**Why no `0`, `1`, `L`, `O`, `I`, `U`:** the first four are the classic transcription
confusions, `O` and `I` go with the vowels anyway, and `U` is dropped so that dictating a
reference over the phone has no homophone traps. What remains is unambiguous spoken, written
and typed.

**Why eight:** 27⁸ ≈ 2.8 × 10¹¹. For a single site's bookings the collision probability is
negligible and the reference still reads in one breath. Grouping into two blocks of four is
purely for the eye; the stored value has no separator, so the display format can change later
without a migration.

### D2. Generation is a port — for testability, not for determinism

**Corrected during apply. The original justification was wrong on its facts.**

It said a random source "would put non-determinism into the one assembly that has none".
`BookingService` already contains `Booking.Create(Guid.NewGuid(), …)`. Core has generated
random identity inline since bookings existed, so there was no determinism to protect, and the
argument as written would have been rejected by anyone who looked. `CoreIndependenceTests`
guards Core's **package references**, not its purity — a different property.

**The decision survives, on the half of the reasoning that holds:** a port makes the collision
path reachable. Uniqueness is enforced by the store (D3), so the interesting behaviour is what
happens when a generated reference is already taken — and at 27⁸ values, waiting for a real
collision is not a test strategy. A factory a test can point at a known-duplicate value is the
only practical way to exercise it.

**Decision:** `IBookingReferenceFactory` in `UBookIt.Core`, with a default implementation in
Core drawing from `RandomNumberGenerator` (a `System` type, so the zero-package-reference
invariant is untouched). `BookingService` takes it as an **optional** parameter defaulting to
that implementation.

**Why optional rather than required**, which is the opposite of the call made for
`Booking.Create`'s service attribution: omitting the attribution silently recorded a wrong
*fact* — a service booking claiming it was placed directly. Omitting a reference factory
records nothing wrong; it produces a perfectly good random reference, exactly as
`Guid.NewGuid()` does today. The failure mode that made attribution required does not exist
here, and making it required would mean editing 29 construction sites to no safety benefit.

Cryptographic randomness rather than `Random.Shared` costs nothing and forecloses a footgun: a
reference is the natural thing to put in a "manage my booking" link later, and a predictable
one would be guessable. That is not a claim that a reference is a secret — it is not, and
nothing here should be built as though it were.

### D3. Uniqueness is enforced by the database, with a bounded retry

**Decision:** a unique index on the reference column. Generation collides → the store reports
it → generate again, up to a small bounded number of attempts, then fail.

**Two corrections made during apply, because the code does not do what this originally said:**

- ~~"catch the unique violation"~~. The store **pre-checks inside the placement transaction** and
  returns a `ReferenceTaken` failure. The index is still the guarantee; the check is what makes
  the ordinary case reportable without parsing SQL error numbers and index names to work out
  which constraint fired. The residue is real and worth stating: a genuine race loses to the
  index and surfaces as a `DbUpdateException` — a 500 for that booker — rather than being
  retried. With the pre-check in front of it and 27⁸ values behind it, nobody will meet that;
  the alternative is a fragile string-matching layer whose own failure mode is worse.
- ~~"fail the placement with a domain failure"~~. Exhaustion **throws**. A `DomainResult` failure
  is for something the caller did; this is the generator being broken. Putting it in the result
  type would also oblige the delivery API's failure mapping — a published contract — to gain a
  code meaning "our generator is broken", which no consumer can act on.

**Why the index is the guarantee and the check is not:** a check-then-insert *outside* a
transaction is a race, and this codebase has already been bitten by conflict logic that looked
correct until it was placed under load. The database is the only thing that can actually
enforce uniqueness, so it is the thing that does — the pre-check above runs inside the
placement transaction and exists to make the outcome *reportable*, never to establish it.
Delete the index and the check does not save you; delete the check and correctness is
untouched, only the error message gets worse.

**Why a bound rather than a loop:** an unbounded retry turns a bug — an exhausted or broken
generator — into a hang. A bound turns it into an error message.

**The migration's de-duplication loop is deliberately unbounded, and that is not a
contradiction.** Its population strictly shrinks: each pass rewrites only the rows it found
duplicated, so it converges for the same reason the placement retry cannot be trusted to. The
retry depends on an external generator that may be broken and return one value forever; the
loop depends only on itself.

The retry is a safety net, not a hot path: at 2.8 × 10¹¹ values, a site would need on the order
of a million bookings before a collision is even worth thinking about.

### D4. The Guid stays, and this is two identifiers on purpose

**Decision:** the reference is added; nothing is replaced.

**Why:** they are for different readers. The Guid is the primary key, the value in routes and
payloads, and the thing a headless consumer already binds to; it should be opaque and it should
not change. The reference is for a person reading it aloud. Collapsing them would mean either
putting a human-readable value in the primary key — which invites people to parse it — or
asking humans to read a Guid, which is where we came in.

**The cost is honest and worth stating:** two identifiers means every model that carries one
must decide whether it carries both, and a future reader has to know which is which. The
mitigation is naming — `Id` and `Reference` — and documentation, not cleverness.

### D5. Existing bookings are backfilled, not deleted

**Decision:** the migration generates a reference for every existing booking.

**Why this differs from ⑰**, where the decision was to delete rather than backfill: there, the
missing value was the **service a booking was placed for**, which is genuinely unknowable after
the fact — any backfill would have been a fabrication presented as history. A reference is not
like that. It carries no information about the booking; it is an arbitrary label whose only
requirements are uniqueness and stability, and one generated today is exactly as valid as one
generated at placement.

Deleting bookings to avoid generating labels for them would also be the wrong trade in a
booking system, where a lost booking is somebody turning up to a room that is not theirs.

### D6. The reference is immutable

Once assigned it never changes — not on confirm, not on decline, not on cancel, and not if a
"move" feature later changes a booking's time. A reference that changed would be worse than no
reference: the customer holds the old one, and the system denies all knowledge of it.

This is the property the planned erasure work depends on, and it is stated here so that the
dependency is on something written down rather than on an implementation detail.

## Risks / Trade-offs

- **Breaking changes to public types — five, not one.** `Booking.Rehydrate` gains a required
  parameter; `Booking` gains `Reference`; **`BookingSummary` gains a required positional
  parameter**, which is source-breaking for any alternative `IBookingManagementStore` — a port
  the `booking-management` spec explicitly promises is substitutable — and
  **`BookingConfirmationModel` and `ServiceConfirmationModel` gain `required` members**, which
  breaks any theme constructing them. The proposal originally called out only the first.
  Deliberate, and taken now precisely because the window closes at 1.0.
- **Two identifiers to keep straight.** → D4. Named clearly, documented, and the Guid stays the
  one machines use so the split follows an obvious line.
- **A generator is a new failure mode.** → Bounded retry, then a **throw** — see D3, which
  corrects this entry's original "a real domain failure". The seam is a port so both the retry
  and the exhaustion are testable rather than theoretical.
- **The backfill runs against live data.** → Additive column plus generated values; no existing
  value is read or overwritten. **Settled at apply: set-based** — one `UPDATE … WHERE Reference
  IS NULL`, then a de-duplication loop, then the constraint. Batching would buy nothing a
  booking table will ever need: this runs once, inside the migration transaction a site is
  already waiting on at startup, and a table large enough to justify chunking would have a
  worse problem than this statement. Measured at 500 rows on a real install and 2000 in QA's
  own database; both immediate.

## Open Questions — all answered at apply

- ~~**Does the delivery API response gain the reference?**~~ **Yes** (task 0.1). Both placement
  responses returned only a Guid, so a headless consumer's confirmation screen had the identical
  defect this change exists to fix. Widened the change with a `delivery-api` delta.
- ~~**Should the reference be shown anywhere other than the confirmation and the backoffice
  list?**~~ **Looked, and found one** (task 3.3). `ServiceConfirmation.cshtml` carried the same
  `<dt>Reference</dt>` over a Guid. No other shipped view identifies a booking to a person —
  the pending and unavailable views describe a booking that does not exist yet.
- ~~**Is `BK-` or any prefix wanted?**~~ **No** (task 0.2, confirmed by Chris). Three characters
  on every reference for recognition value that only pays off in an inbox, and emails are not in
  this change. Still reversible without a migration — but note `B` and `K` are both in the
  alphabet, so a prefix would have to be stripped explicitly by `TryParse`; the display-parse
  round-trip test would catch anyone who forgot.
