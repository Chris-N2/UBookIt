## 0. Settle before writing code

- [ ] 0.1 **Does the delivery API response carry the reference?** (design open question). A
  headless consumer building its own confirmation needs it for exactly the reasons the Razor
  view does. Widens the change into `delivery-api` with a delta of its own — decide, do not
  drift into it.
- [ ] 0.2 **Any prefix?** `BK-` aids recognition in an inbox and costs three characters.
  Display-only under D1's canonical/display split, so it can be decided late — but decide it
  rather than defaulting by omission.

## 1. The reference itself, in Core

- [ ] 1.1 A `BookingReference` value object: canonical form (uppercase, no separator),
  a parse that accepts any case with separators and whitespace stripped, equality that is
  case-insensitive, and a display form grouped `XXXX-XXXX`.
- [ ] 1.2 The alphabet is `BCDFGHJKMNPQRSTVWXYZ23456789`, 8 symbols long (design D1). **Assert
  the properties, not the constant**: that no vowel is present, that none of `0 1 L O I U`
  is, and that the alphabet therefore cannot spell a word. A test that restates the literal
  proves only that someone typed it twice.
- [ ] 1.3 A generation port (design D2), implemented outside Core. **`UBookIt.Core` keeps zero
  package references and stays deterministic** — `CoreIndependenceTests` guards that and must
  stay green.
- [ ] 1.4 `Booking` carries the reference. `Create` takes it the way it already takes `id`;
  `Rehydrate` gains it as a required parameter — **the breaking change**, called out in the
  proposal.
- [ ] 1.5 **Immutability (design D6).** Confirm, decline and cancel leave it untouched. Prove
  it by transitioning through every status and asserting the reference is the one assigned at
  placement, rather than by inspecting that no code assigns it.

## 2. Persistence

- [ ] 2.1 Column plus a **unique index** — the store enforces uniqueness, not a prior check
  (design D3).
- [ ] 2.2 Bounded retry on unique violation, then a real domain failure. **Test the collision
  path by supplying a generator that returns a known duplicate** — the port exists partly so
  this is testable rather than theoretical.
- [ ] 2.3 **Backfill existing bookings** (design D5), not deletion. Settle at apply whether the
  backfill is set-based or batched, and say which and why.
- [ ] 2.4 Migration is additive, per CLAUDE.md. No existing value is read or overwritten.

## 3. What people see

- [ ] 3.1 The confirmation view shows the reference where it currently prints a Guid under a
  `<dt>Reference</dt>` it already has.
- [ ] 3.2 The backoffice list row carries and displays it.
- [ ] 3.3 **Sweep the shipped views** for anywhere else a booking is identified to a human
  (design open question) — look rather than assume.
- [ ] 3.4 Accessibility: the reference is content, not a control, but check it does not become
  an unlabelled fragment and that grouping does not break its reading order.

## 4. Guards worth having

- [ ] 4.1 **A reference survives a round trip through persistence** unchanged, including case
  and canonical form.
- [ ] 4.2 **Uniqueness is enforced where it is claimed to be.** Mutation-check by removing the
  unique index and confirming something fails — a guard over a constraint must be shown to
  observe the constraint.
- [ ] 4.3 **The alphabet properties** (1.2), stated as properties.
- [ ] 4.4 **Immutability across every status transition** (1.5).
- [ ] 4.5 Mutation-check each of the above from a clean build.

## 5. Close

- [ ] 5.1 Clean build at **zero** warnings; full suite green against the 1755 baseline; client
  suite against 113.
- [ ] 5.2 `openspec validate --all --strict`.
- [ ] 5.3 **Guarantee diff for the two MODIFIED requirements — done at propose time, re-check
  at apply.** Both replace requirements wholesale, which deletes anything not restated:
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
- [ ] 5.4 Outward sweep for sentences this falsifies. Start with `bookings` and
  `default-frontend`, then `docs/`, then the README. **`docs/booking-page.md` and
  `docs/backoffice.md` both describe what a visitor and an operator see.**
- [ ] 5.5 **Do not write anything that promises one resource equals one occupancy unit.** A
  standing constraint from the composite-resources exploration, recorded because the failure
  mode is closing that door in prose without noticing. This change does not touch claims, so
  the risk is only in careless wording.
- [ ] 5.6 Hand to `qa-review` in a **fresh context or subagent**.
