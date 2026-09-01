## 0. Settle before writing code

- [x] 0.1 **Yes — the delivery API carries it.** Measured: `PlacementResponseModel` and
  `ServicePlacementResponseModel` both return `BookingId` and nothing else identifying, so a
  headless consumer building its own confirmation screen shows a person a Guid — **the identical
  defect this change exists to fix, on the other surface**. Fixing one and not the other would
  ship the bug to exactly the consumers the delivery API exists for.

  Note the urgency argument does *not* apply: adding a field to a response is additive and could
  be done after 1.0, unlike `Booking.Rehydrate`. It is included because it is the same defect,
  not because the window is closing. Widens the change with a `delivery-api` delta.
- [x] 0.2 **No prefix.** It costs three characters on every reference for recognition value that
  only pays off in an inbox — and emails are not in this change. It is display-only under D1's
  canonical/display split, so it can be added later without a migration, and a prefix invites
  people to parse the reference. Decided rather than defaulted.

## 1. The reference itself, in Core

- [x] 1.1 A `BookingReference` value object: canonical form (uppercase, no separator),
  a parse that accepts any case with separators and whitespace stripped, equality that is
  case-insensitive, and a display form grouped `XXXX-XXXX`.
- [x] 1.2 The alphabet is `BCDFGHJKMNPQRSTVWXYZ23456789`, 8 symbols long (design D1). **Assert
  the properties, not the constant**: that no vowel is present, that none of `0 1 L O I U`
  is, and that the alphabet therefore cannot spell a word. A test that restates the literal
  proves only that someone typed it twice.
- [x] 1.3 A generation port (design D2). ~~Implemented outside Core~~ — **the default
  implementation lives in Core**, drawing on `RandomNumberGenerator`, a `System` type. The
  original wording rested on the determinism argument D2 retracts: `BookingService` has called
  `Guid.NewGuid()` inline since bookings existed, so there was never any purity to preserve.
  `CoreIndependenceTests` guards Core's **package references** — a different property, and it
  stays green.
- [x] 1.4 `Booking` carries the reference. `Create` takes it the way it already takes `id`;
  `Rehydrate` gains it as a required parameter — **the breaking change**, called out in the
  proposal.
- [x] 1.5 **Immutability (design D6).** Confirm, decline and cancel leave it untouched. Prove
  it by transitioning through every status and asserting the reference is the one assigned at
  placement, rather than by inspecting that no code assigns it.

## 2. Persistence

- [x] 2.1 Column plus a **unique index** — the store enforces uniqueness, not a prior check
  (design D3).
- [x] 2.2 Bounded retry, then a fault. **Two corrections to what this task originally said**,
  both found by QA reading the code against the design rather than against the prose:
  - ~~"a real domain failure"~~ — exhaustion throws `InvalidOperationException`. A `DomainResult`
    failure would be wrong: nothing the caller did caused it, nothing they can do fixes it, and
    the delivery API's failure mapping is a published contract that should not gain a code
    meaning "our generator is broken". Recorded here and in design D3 rather than left as a
    comment in a test arguing with its own specification.
  - ~~"on unique violation"~~ — the store does not catch a unique-index violation. It
    **pre-checks inside the placement transaction** and returns `ReferenceTaken`. The index
    remains the guarantee; the check is what makes the ordinary case reportable. The residue,
    stated rather than hidden: a genuine race loses to the index and surfaces as a
    `DbUpdateException` — a 500 for that booker — instead of being retried. At 28^8 with the
    pre-check in front of it, that is not a scenario anyone will meet, and the alternative
    (parsing SQL error numbers and index names to tell one violation from another) is a
    fragility with a worse failure mode.

  **Test the collision path by supplying a generator that returns a known duplicate** — the port
  exists partly so this is testable rather than theoretical.
- [x] 2.3 **Backfill existing bookings** (design D5), not deletion. Settle at apply whether the
  backfill is set-based or batched, and say which and why.
- [x] 2.4 Migration is additive, per CLAUDE.md. No existing value is read or overwritten.
- [x] 2.5 **The backfill run against a real database with real rows**, which no automated test
  covers — every unit and integration test creates the column on an empty table.

  500 bookings were inserted into the installed check site under the **pre-migration** schema,
  then the new packages were installed over it with `-KeepExisting` and the site booted:
  `uBookIt applied 1 database migration(s): 20260901184959_AddBookingReference`. Measured
  afterwards, straight out of SQL Server:

  | | |
  |---|---|
  | rows | 500 |
  | **distinct references** | **500** |
  | null or blank | 0 |
  | wrong length | 0 |
  | outside the alphabet | 0 |
  | unique index present | yes |
  | column nullable | no |

  **This settles the one thing about the migration that was reasoned rather than measured:
  that `NEWID()` re-evaluates per occurrence and not once per row.** Had it evaluated once, every
  reference would have been the same symbol eight times over — `GGGGGGGG` — and 500 rows would
  have collapsed to a handful of distinct values, or hung in the de-duplication loop. Samples:
  `G3Y3CNTF`, `X8K9PM4Q`, `6N83HNVY`, `KG2H9JFX`. Eight independent symbols, no vowels, nothing
  from `0 1 L O I U`.

  Performed by hand, once. It is not a regression test and nothing here makes it one.

## 3. What people see

- [x] 3.1 The confirmation view shows the reference where it currently prints a Guid under a
  `<dt>Reference</dt>` it already has.
- [x] 3.2 The backoffice list row carries and displays it.
- [x] 3.3 **Sweep the shipped views** — and it found one. `ServiceConfirmation.cshtml` had the
  same defect as the direct confirmation: `<dt>Reference</dt>` over `@Model.BookingId`. Fixing
  only the direct flow would have left half the product broken in exactly the way this change
  was written to fix. Both are now guarded, and no other shipped view identifies a booking to a
  person.
- [x] 3.4 Accessibility: the reference is content, not a control, but check it does not become
  an unlabelled fragment and that grouping does not break its reading order.

## 4. Guards worth having

- [x] 4.1 **A reference survives a round trip through persistence** unchanged, including case
  and canonical form.
- [x] 4.2 **Uniqueness is enforced where it is claimed to be.** Mutation-check by removing the
  unique index and confirming something fails — a guard over a constraint must be shown to
  observe the constraint.
- [x] 4.3 **The alphabet properties** (1.2), stated as properties.
- [x] 4.4 **Immutability across every status transition** (1.5).
- [x] 4.5 Mutation-check each of the above from a clean build. Four run, and **one found a
  real hole**:
  - alphabet reverted to A–Z0–9 → 4 failures (both property tests, plus the vowel and
    excluded-digit parse cases);
  - collision retry removed → the retry and exhaustion tests both fail;
  - unique index dropped **and** the store's check disabled → both integration tests fail;
  - **confirmation view reverted to `@Model.BookingId` → 747 rendering tests PASSED.** Nothing
    guarded the actual defect, on the view where it was found. `ConfirmationReferenceTests` now
    does, asserting the negative as well as the positive — "the reference appears" is satisfied
    by a page printing both, which is not the fix. Re-run after: both flows fail.

## 5. Close

- [x] 5.1 Clean build at **zero** warnings; full suite green against the 1755 baseline; client
  suite against 113.
- [x] 5.2 `openspec validate --all --strict`.
- [x] 5.3 **Guarantee diff for the THREE MODIFIED requirements — done at propose time, re-check
  at apply.** Written when there were two; `delivery-api` joined when task 0.1 was settled and
  was missing from this record until QA noticed. Each replaces a requirement wholesale, which
  deletes anything not restated:
  - `default-frontend` / *Post-Redirect-Get confirmation*: three guarantees (303 redirect not a
    rendered POST; the confirmation shows reference + resource + time + contact details;
    refresh does not re-book) and two scenarios. All carried forward. **The only deliberate
    change is `(its id)`**, which is the defect.
  - `booking-management` / *Bookings can be enumerated for management*: six guarantees (a Core
    read port distinct from availability reads; substitutable with a stated contract; the full
    row payload without a further read per booking; resource names as a guarantee with its
    rationale; the service name as the placement-time name; read-only) and five scenarios.
    All carried forward, including the trailing note explaining why one scenario was narrowed
    when cancellation joined the capability. **The only change is the reference joining the
    row payload**, plus a scenario for it.
  - `delivery-api` / *Booking placement* — **added after this task was first written**: one
    endpoint SHALL, four request-body guarantees (resource id, start, duration, booker contact;
    no member key; the pipeline unchanged), five response elements (id, status, resource id,
    ISO-8601 UTC interval, echoed booker) and three scenarios. All carried forward. **The only
    deliberate change is `(the confirmation reference)` after the id**, which is the same false
    equation `default-frontend` carried and is the defect itself; the response gains the
    reference alongside.
- [x] 5.4 Outward sweep for sentences this falsifies. Start with `bookings` and
  `default-frontend`, then `docs/`, then the README. **`docs/booking-page.md` and
  `docs/backoffice.md` both describe what a visitor and an operator see.**
- [x] 5.5 **Do not write anything that promises one resource equals one occupancy unit.** A
  standing constraint from the composite-resources exploration, recorded because the failure
  mode is closing that door in prose without noticing. This change does not touch claims, so
  the risk is only in careless wording.
- [ ] 5.6 Hand to `qa-review` in a **fresh context or subagent**.
