## Context

Measured before designing.

- **The view already calls it a reference.** `Views/Shared/Components/Booking/Confirmation.cshtml`
  renders `<dt>Reference</dt><dd>@Model.BookingId</dd>`. The label is right; the value is a Guid.
- **Identity is supplied, not generated.** `Booking.Create` is `internal` and takes `Guid id`
  from its caller. Core therefore stays deterministic and has no need of a random source — the
  reference can arrive the same way the id does, and Core keeps its zero package references.
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

**Decision:** the alphabet is `BCDFGHJKMNPQRSTVWXYZ` plus `23456789` — 28 symbols. References
are 8 symbols, stored canonical (uppercase, no separator), displayed grouped as `XXXX-XXXX`,
and accepted on input in any case with separators and whitespace stripped.

**Why no vowels:** a random string containing vowels eventually spells something. A booking
system that emails a customer a reference which happens to be an obscenity is a story you only
get to have once. Removing vowels removes the entire class, and it costs almost nothing.

**Why no `0`, `1`, `L`, `O`, `I`, `U`:** the first four are the classic transcription
confusions, `O` and `I` go with the vowels anyway, and `U` is dropped so that dictating a
reference over the phone has no homophone traps. What remains is unambiguous spoken, written
and typed.

**Why eight:** 28⁸ ≈ 3.8 × 10¹¹. For a single site's bookings the collision probability is
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
happens when a generated reference is already taken — and at 28⁸ values, waiting for a real
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

**Decision:** a unique index on the reference column. Generation collides → catch the unique
violation → generate again, up to a small bounded number of attempts, then fail the placement
with a domain failure.

**Why not "generate and check first":** a check-then-insert is a race, and this codebase has
already been bitten by conflict logic that looked correct outside a transaction. The database
is the only thing that can actually enforce uniqueness, so it should be the thing that does.

**Why a bound rather than a loop:** an unbounded retry turns a bug — an exhausted or broken
generator — into a hang. A bound turns it into an error message.

The retry is a safety net, not a hot path: at 3.8 × 10¹¹ values, a site would need on the order
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

- **A breaking change to a public type.** → Deliberate, and taken now precisely because the
  window closes at 1.0. Called out in the proposal as CLAUDE.md requires.
- **Two identifiers to keep straight.** → D4. Named clearly, documented, and the Guid stays the
  one machines use so the split follows an obvious line.
- **A generator is a new failure mode.** → Bounded retry with a real domain failure, and the
  seam is a port so the failure is testable rather than theoretical.
- **The backfill runs against live data.** → Additive column plus generated values; no existing
  value is read or overwritten. Still to be settled at apply: whether the backfill is a single
  set-based statement or a batched one, which depends on how large a real site's booking table
  is expected to get.

## Open Questions

- **Does the delivery API response gain the reference?** A headless consumer building its own
  confirmation screen needs it, and the argument for adding it is the same as for the Razor
  view. Not scoped here because it widens the change into `delivery-api`; worth settling before
  apply rather than during.
- **Should the reference be shown anywhere other than the confirmation and the backoffice
  list** — the pending/unavailable views, for instance? Probably not, since nothing exists to
  reference until a booking does, but worth a deliberate look at the shipped views rather than
  an assumption.
- **Is `BK-` or any prefix wanted?** It aids recognition in an inbox and costs three
  characters. Deliberately left open: it is a display concern under D1's canonical/display
  split, so it can be decided late without a migration.
