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
- [x] 2.3 **Backfill existing bookings** (design D5), not deletion. **Set-based**, and here is
  the why the task asked for and the first pass did not give: one `UPDATE … WHERE Reference IS
  NULL`, then the de-duplication loop, then the constraint. Batching would buy nothing — it runs
  once, inside a migration a site is already waiting on at startup, and a booking table large
  enough to need chunking has a worse problem than this statement. Measured at 500 rows against
  a real install and 2000 in QA's own database; both immediate.
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

  **Accuracy note added at round 6:** this run measured the pre-round-5 expression,
  `ABS(CHECKSUM(NEWID()) % 28)`. The SQL has since changed to remove a distribution bias
  (10.3), so what this establishes is the *mechanism* — the column is added, filled and
  constrained against real rows, and `NEWID()` re-evaluates per occurrence — not the current
  expression's output. Round 5's 28,000-draw measurement covers the distribution. Re-running
  the end-to-end backfill would add nothing either measurement does not already give.

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

  **This task was recorded as done and had not been performed as written** (found at round 6).
  Task 4.5 ran a *compound* mutation — index dropped **and** the store's pre-check disabled —
  which cannot attribute the failure. QA ran the single one: with the index removed consistently
  from the model, the migration and both snapshots, **1796 of 1796 tests passed**. The shipped
  database had no unique index and nothing noticed. Both existing tests assert `ReferenceTaken`,
  which only the pre-check can produce, so they watched the check and never the constraint —
  while three normative statements say the constraint is the guarantee and the check explicitly
  is not.

  `The_schema_refuses_a_duplicate_reference_even_when_nothing_checks_first` now writes the row
  the way something that is not our code would: a raw `INSERT` performing no check, asserted to
  fail with SQL Server's duplicate-key error specifically, so it cannot pass because the insert
  failed for an unrelated reason. Re-run with the index-only mutation: it dies, alone.

  **A trap worth carrying**, because it produces a false green that reads exactly like a real
  one: removing the index from the **model alone** fails all 78 integration tests with EF's
  `PendingModelChangesWarning`. That fires for any model/snapshot divergence and says nothing
  whatever about uniqueness. A mutation check that stops there concludes the constraint is
  guarded when nothing observes it.
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
- [x] 5.3 **Guarantee diff for every MODIFIED requirement — TWELVE, across five capabilities.**
  The number has been wrong at every single round: written for two, corrected to three,
  corrected to six, and still wrong. It is not a clerical problem — **round 3's MAJOR happened
  because a requirement restated by hand was absent from this list, so nothing prompted anyone
  to diff it, and four guarantees went missing.** QA round 4 found the count wrong for a third
  time.

  So the list is no longer trusted to be maintained by hand.
  `ChangeDeltaIntegrityTests.Every_modified_requirement_is_named_in_its_change_tasks` fails the
  build if any `## MODIFIED Requirements` entry in this change's deltas is not named somewhere
  in this file — a machine cannot check that the prose survived, but it can refuse to let a
  wholesale replacement go unlisted where the person doing that reading will look. Its sibling
  asserts each one names a requirement that actually exists upstream, so a typo cannot sync as
  a new requirement beside the one it meant to replace.

  The requirements, enumerated from the deltas rather than from memory:

  | capability | requirement |
  |---|---|
  | `bookings` | Booking shape |
  | `bookings` | Booking rehydration |
  | `default-frontend` | Post-Redirect-Get confirmation |
  | `default-frontend` | The confirmation reports every resource a service resolved to |
  | `booking-management` | Bookings can be enumerated for management |
  | `booking-management` | Bookings are readable over an authorized management endpoint |
  | `booking-management` | Bookings have a backoffice collection view |
  | `delivery-api` | Booking placement |
  | `delivery-api` | Service booking placement |
  | `persistence` | Schema shape and naming |
  | `persistence` | Atomic placement on SQL Server |
  | `bookings` | Availability and placement service ports |

  **Regenerate this table from the deltas; do not patch it.** Patching is what produced every
  previous wrong count — including one in the first draft of this very entry, which said ten
  while the table below it listed eleven. The generator is four lines: walk
  `openspec/changes/<change>/specs/*/spec.md`, track the current `## ` section, and collect
  `### Requirement:` headings while that section is `MODIFIED Requirements`. This entry has been wrong at
  every round — written for two, corrected to three, and still saying three when round 2 added
  two more. **That drift is exactly how MAJOR 1 of round 3 happened**: the endpoint requirement
  round 2 restated by hand was not in this list, so nobody re-diffed it, and it had silently
  dropped four guarantees. The list is now the population, and the technique changed with it:
  round 3's restatements are **programmatic copies of the original text with one sentence
  patched**, not retyped from what was on screen, because retyping is what dropped them.

  Each replaces a requirement wholesale, which deletes anything not restated:
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
  - `bookings` / *Booking shape* — 7 SHALLs, 4 scenarios, all carried; the enumeration of what a
    booking has gains the reference. `bookings` / *Booking rehydration* — 5 SHALLs, 3 scenarios,
    all carried; the factory's parameter list gains it, stated as **required**. Both counted
    before and after the patch.
  - `booking-management` / *Bookings are readable over an authorized management endpoint* — the
    one round 2 got wrong. Restated from the file this time: the versioned-endpoint-in-the-same-
    swagger-group SHALL, the not-anonymous-under-any-configuration SHALL, purpose-built models as
    a SHALL rather than only inside a scenario, and windowed/paged/filtered — all four were
    dropped and are back.
  - `delivery-api` / *Service booking placement* — 17 SHALLs, 13 scenarios, all carried. It said
    `POST /bookings` "SHALL remain unchanged in route, request model, **response model**, and
    semantics", which this change falsifies; corrected so the guarantee it was actually making
    (service placement leaks nothing into the direct endpoint) survives intact.
  - `persistence` / *Schema shape and naming* — 8 scenarios carried, one added for
    storage-level uniqueness.
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
- [x] 5.6 Hand to `qa-review` in a **fresh context or subagent**. Two rounds, both REJECT.

## 6. QA round 1

- [x] 6.1 **CRITICAL, and self-inflicted.** I ran `git add -A` while the QA subagent was
  mid-mutation and committed its in-flight edit — a vowel into the reference alphabet — into
  `4677b01`. Invisible to `git status` because the edit preserved byte length and defeated
  git's stat cache. Amended; verified against `HEAD` rather than the working tree, which is the
  check that would have caught it. Two rules recorded: never stage broadly while an agent is
  running, and a commit touching source is not finished until the tests have run again.
- [x] 6.2 **MAJOR — the reference *value* was unguarded on three surfaces.** Substituting a
  constant passed 1786/1786. Guards added comparing against the reference the store or port
  actually holds.
- [x] 6.3 **MAJOR — the backoffice list cell was unguarded at every layer.** Deleting it left
  seven headers over six cells and passed all 116 client tests. Source-level guard added, with
  its limits stated: there is no DOM environment, so this is a stand-in.
- [x] 6.4 **MINORs** — design D3 described catching a unique violation and returning a domain
  failure when the code pre-checks and throws; the factory's own doc comment and task 1.3 still
  carried the determinism claim D2 had already retracted; task 5.3 diffed two MODIFIED
  requirements when there are three; the proposal named one breaking change when there are four.
  All corrected. **Every one of these is the same fault**: prose that stopped being true and was
  not swept.

## 7. QA round 2

- [x] 7.1 **MAJOR — I fixed three projections because round 1 named three. There were six.**
  The three left unguarded were the SQL read port, and *both* confirmation model builders — the
  confirmation being the surface this entire change exists for. A constant substituted into any
  of them passed 1791/1791.

  **The lesson, which is the useful part: when a finding enumerates instances, the count is a
  sample, not the population.** The right response to "these three are unguarded" was to sweep
  for every site that projects a reference and check each, which takes one grep. Done now:
  `grep -rn "\.Reference" src` is the sweep. **It found six; QA round 3 refused to trust that
  number and enumerated ten independently mutable sites** — the same grep plus reading each
  surface, counting the two Razor views and both directions of the persistence mapping. All ten
  are guarded, each one killed by a constant substitution from a clean build. The count being
  wrong twice, in a task about counting things properly, is the joke this entry has to live with.
- [x] 7.2 **MAJOR — no `persistence` delta.** `persistence/spec.md` enumerates the schema
  exhaustively and the enumeration no longer matched: no `Reference` column, no unique index.
  The precedent was exact and in-repo — ⑰ shipped a `persistence` MODIFIED requirement for the
  same reason — and task 5.4's sweep never looked at `persistence`. Delta added, every
  guarantee restated, plus a scenario for storage-level uniqueness.
- [x] 7.3 **MINOR — `booking-management`'s endpoint enumeration** listed the response payload in
  a second place the delta had not touched. Extended, with the canonical-form rule stated where
  a client will read it.
- [x] 7.4 **MINORs from round 1's own fixes** — design Risks still promised "a real domain
  failure" eighty lines below D3's retraction of exactly that phrase; task 2.3 was ticked while
  still asking a question it never answered; the `bookings` delta — the artifact that gets
  archived — said uniqueness is enforced "rather than by checking before writing" while the
  store checks before writing. All three corrected, the last by separating the guarantee from
  the report rather than by deleting the sentence.
- [x] 7.5 **NITs** — the three answered Open Questions marked answered; the count guard's
  spanning-row blind spot documented in the test that has it.

## 8. QA round 3

Code clean — QA enumerated **ten** independently mutable reference projections (refusing to
trust round 2's "six") and found all ten guarded, each killed by a constant substitution from a
clean build. Every finding this round is an artifact defect.

- [x] 8.1 **MAJOR — the fix for a wholesale-replacement problem was itself a wholesale
  replacement, done wrong.** Round 2 restated `booking-management`'s endpoint requirement by
  hand and silently dropped four guarantees: the versioned-endpoint-in-the-same-swagger-group
  SHALL, "SHALL NOT be reachable anonymously **under any configuration the package ships**"
  (weakened to a sentence about the section's authorization, with a surviving scenario that
  tests one unauthenticated call), purpose-built models as a SHALL rather than only inside a
  scenario's THEN, and windowed/paged/filtered.

  **Cause, precisely:** I restated it from the part of the requirement I had on screen. I read
  from line 281 and never scrolled to 271, so four guarantees I never saw could not be carried.
  All four restored.

  **Method changed as a result.** Round 3's restatements are produced by reading the original
  requirement out of the file programmatically and patching one sentence, then asserting the
  SHALL and scenario counts match before and after. Retyping from a screenful is what dropped
  them; a copy cannot.
- [x] 8.2 **MAJOR — `delivery-api` said `POST /bookings` "SHALL remain unchanged in route,
  request model, response model, and semantics".** This change adds `Reference` to that response
  model, so after sync the capability would have contradicted itself: its own ADDED requirement
  says every placement response carries the reference. Corrected so the guarantee the sentence
  was actually making — service placement leaks nothing into the direct endpoint — survives
  intact. Its scenario "Direct placement is unchanged" was falsified the same way and is now
  "Direct placement still claims one resource".
- [x] 8.3 **MAJOR — the two `bookings` requirements that enumerate what a booking *is*.**
  *Booking shape* listed everything a booking has and did not list the reference; *Booking
  rehydration* listed `Booking.Rehydrate`'s parameters and did not either — the change's
  headline breaking change, absent from the sentence that enumerates that factory's inputs.
  ⑰ edited both of these for the same reason and the precedent was not followed. Both now
  MODIFIED, restated by copy, with the reference stated as **required** in the factory.
- [x] 8.4 **MINOR — the proposal's Modified Capabilities named three of five**, in a change
  whose entire review history is understated scope. Now five, each with why it is there.
- [x] 8.5 **MINOR — task 5.3 said "THREE MODIFIED requirements" when there were six.** Not a
  clerical error: 8.1 happened *because* the requirement round 2 restated was never added to
  this list, so nothing prompted a re-diff. The list is now the population.
- [x] 8.6 **NITs** — `default-frontend`'s service-confirmation requirement said "the booking
  reference" while its sibling had been deliberately disambiguated to "the identifier a person
  can quote, not its machine identifier"; the one sentence pair this change exists to
  disambiguate was disambiguated in one place only. Fixed. The client's hardcoded `8` and `4`
  are now named constants beside a note that nothing carries them across the boundary. And the
  claim that the sweep "finds six" now records that it found ten.

## 9. QA round 4

Round 3's three MAJORs verified fixed, all eleven MODIFIED requirements re-diffed by QA with
**zero dropped guarantees**, and the programmatic-copy method verified by inspecting the text
rather than believing the claim. Not over-scoped. One MAJOR remained.

- [x] 9.1 **MAJOR — the falsified-sibling class, for the FOURTH consecutive round.**
  `booking-management` / *Bookings have a backoffice collection view* enumerates the screen's
  columns and had stopped matching: the reference is now the first column, and `docs/backoffice.md`
  was updated to say so while the spec was not. After sync, the capability and the package's own
  documentation would have contradicted each other about what the screen shows.

  The proposal's **What Changes** names this surface explicitly — *"The operator sees it in the
  backoffice bookings list"* — while its Capabilities bullet named only the read port and the
  endpoint. The change described the surface it then failed to specify.

  Restated by copy. Its *"The view SHALL NOT compute anything the endpoint does not return"*
  clause also needed reconciling: the view derives `7QX4-M2NP` from the canonical wire value,
  and the permission for that lived only in the endpoint requirement, so a reader of the view
  requirement alone saw a prohibition being broken. Formatting a value you were given is now
  distinguished from inventing one.
- [x] 9.2 **MINOR — `persistence` / *Atomic placement on SQL Server*** enumerates the placement
  transaction as three numbered steps; there are now four, and a second failure code. Restated.
- [x] 9.3 **MINOR — the `delivery-api` ADDED requirement never said which form crosses the wire.**
  `booking-management` guarantees canonical with a scenario; the delivery API — the more public
  contract, and the one justified by a headless consumer's confirmation screen — said nothing,
  while the package's own views render the grouped form. Now stated, with a scenario.
- [x] 9.4 **NITs** — "breaking in four places" while listing five types, corrected to five; the
  client declared `reference?: string` optional where the generated model has it required, which
  would have rendered a silently empty cell rather than failing a type check.
- [x] 9.5 **MINOR, and the one that mattered: task 5.3 miscounted for the THIRD round running.**
  Said six; there were nine; it now says eleven. **This is not clerical — round 3's MAJOR was
  caused by it.** A requirement restated by hand was absent from the list, so nothing prompted a
  re-diff, and four guarantees went missing.

  **A hand-maintained list has now failed four times, so it is no longer maintained by hand.**
  `ChangeDeltaIntegrityTests` fails the build if any `## MODIFIED Requirements` entry in an
  active change's deltas is not named in that change's `tasks.md`, and asserts each names a
  requirement that actually exists upstream. Mutation-checked by deleting the very requirement
  round 3 lost: the guard names it and fails.

  A machine cannot check that the prose survived a wholesale replacement — that is a reading
  job. It can refuse to let a replacement go **unlisted where the person doing the reading will
  look**, which is the step that actually broke.

## 10. QA round 5

**The code is clean — QA found nothing wrong with the implementation.** Both MAJORs are
artifact/guard defects, and the falsified-sibling sweep finally has a verdict rather than
another instance.

- [x] 10.1 **MAJOR — the falsified-sibling class, fifth round, and now exhausted.**
  `bookings` / *Availability and placement service ports* enumerates what the booking service
  depends on: two stores plus the observation port. It now takes a fourth, and **the precedent
  is written inside the requirement itself** — an italic note explaining that the enumeration
  was widened rather than dropped when the observation port arrived, because "the constraint
  was never about the number two". The same move was available and was not made.

  Worse than a stale sentence: `BookingReference`, `IBookingReferenceFactory` and
  `RandomBookingReferenceFactory` are new **public Core API** and appeared in no requirement
  anywhere. Design D2 decides the port exists; design is not what gets archived into a
  capability. The one requirement that would have carried it is the one that went stale, so the
  guarantee vanished on both sides at once. The requirement now enumerates the port and states
  what it does and does not promise, with a scenario for substituting it.

  **Why it took five rounds, which is the useful part:** every earlier sweep looked at *payload*
  axes — what a row, response, column or table carries. This is the only axis that is about a
  **constructor**. It was never on anyone's list, including mine. QA's round-5 sweep derived the
  axes from `git diff aec2952..HEAD` rather than from the proposal, checked every requirement in
  all ten capabilities against each axis, and reports the class **exhausted**.
- [x] 10.2 **MAJOR — the anti-vacuity guard had no anti-vacuity guard.**
  `ChangeDeltaIntegrityTests`, written in round 4 to end a four-round failure, shipped with four
  silent-empty paths. QA measured it: renaming `## MODIFIED Requirements` to
  `## Modified Requirements` made three wholesale replacements invisible — including the very
  requirement round 4 rejected over — and **both tests passed**, as did strict validation.

  This project has written the rule down twice: `RepoFiles.Paths` carries *"a scan over nothing
  passes every assertion made about it"*, and `default-frontend` makes an anti-vacuity guard a
  **SHALL**, noting that the fault has shipped before. It shipped again, inside the guard meant
  to stop a different recurrence of it.

  Fixed with a third test that separates the two ways of seeing nothing. *"There is no active
  change"* is legitimate and true for most of a repository's life. *"The parser stopped
  working"* is not — so the parser is proved against the **archive**, which is never empty and
  never changes, and each active delta must have a `tasks.md`, non-empty sections, and a
  section this guard recognises. Mutation-checked with QA's own probe: the reworded heading now
  fails, naming it.

  **And it caught me the same hour.** Its first run against 10.1's new requirement failed —
  I had added the MODIFIED entry and not listed it in 5.3, which is precisely the round-3
  failure it exists to prevent, reproduced by me while fixing round 5.
- [x] 10.3 **NIT — a measurable distribution bias in the backfill.** `ABS(CHECKSUM(NEWID()) % 28)`
  folds ±n onto n, so `B` was drawn at half the rate of the other 27 symbols: backfilled and
  newly-placed references came from different distributions where the design describes one
  alphabet. Now `(CHECKSUM(NEWID()) & 0x7FFFFFFF) % 28`, which avoids both the overflow and the
  fold. **Measured on real SQL Server over 28,000 draws**: 28 distinct symbols, counts 898–1062
  against an expected 1000, `B` at 996. Safe to amend because nothing is published — the
  migration has never shipped.
- [x] 10.4 **NIT — the "1755 baseline" claim, checked rather than assumed.** QA's notes said
  1752 and flagged it as unverified. The merge commit `aec2952` records *"1755 tests"*, so the
  baseline stands.
- [x] 10.5 **Recorded, not fixed: `persistence` / "Scenario: No migration is added".** Literally
  false — this change adds one — but it was already falsified by ⑰ and is self-scoped by "after
  this change". It is inherited history, not something this change broke, and the real question
  QA raises is whether change-scoped scenarios should sync into a capability at all. On the
  deferred-obligations list; fixing it here would be scope creep into a defect predating the
  branch.

## 11. QA round 6

The feature code is clean. Both MAJORs are guards that did not observe what they claimed to.

- [x] 11.1 **MAJOR — the anti-vacuity guard was vacuous on a case adjacent to the one it was
  measured against.** `recognised || addedOnly` named a variable for a condition it did not
  compute: `addedOnly` was true whenever an ADDED section was *present*, not when it was the
  only one, so **every mixed delta was exempt** — and two of this change's five deltas are
  mixed, carrying five of the twelve MODIFIED requirements between them. Round 5's own probe,
  applied to `bookings` instead of `default-frontend`, passed all three tests while hiding
  three wholesale replacements, including the one round 5 rejected over.

  **The fault is fourth-generation and the pattern is now named: testing a guard against the
  single mutation that motivated it, rather than against the shapes of input it will meet.**
  Delta files come in three shapes — ADDED-only, MODIFIED-only, both — and only one had ever
  been tried.

  Rewritten to ask a question no shape can dodge: **is every requirement attributed to a
  section OpenSpec understands?** A reworded heading orphans everything beneath it whatever
  else the file contains, and orphans are reported by name. Mutation-checked against **all
  three shapes**; all three fail.
- [x] 11.2 **MAJOR — the unique index had no covering test, and 4.2's mutation was never run as
  written.** See 4.2 above: removing the index consistently passed 1796/1796.
- [x] 11.3 **NITs** — a client comment describing a type as optional after round 4 made it
  required (the same unswept-prose class as most of this review history); a failure message
  claiming exact matching that matched on `Contains`; and task 2.5's live measurement predating
  the round-5 expression change, now noted for what it does and does not establish.
- [x] 11.5 **Found by the suite while fixing 11.2: two integration tests shared a literal
  reference.** The new schema test and `The_projected_row_carries_the_bookings_own_reference`
  both used `QF7M3XKB`, and the integration tests share one database — so whichever ran second
  was refused, and the failure depended on ordering. Distinct literals now, with the hazard
  named in the file rather than left for the next person to rediscover. This is the
  shared-fixture ordering trap this project has hit before; it is cheap to cause and expensive
  to diagnose, because it appears and disappears.
- [x] 11.4 **Environment note from QA, worth keeping**: `find src tests -type d -name bin` matches
  `Client/node_modules/*/bin` and destroys the npm binstubs, producing a misleading "cannot find
  module typescript/bin/tsc". It cost the reviewer four build cycles. The sweep used in this
  change prunes `node_modules` explicitly; anyone writing a fresh one should.

## 12. QA round 7

Feature code clean again. Round 6's index guard verified genuine by an independent single
mutation. Three findings, two of them mine from round 6.

- [x] 12.1 **MAJOR — the guard was vacuous a FIFTH time**, on `##MODIFIED Requirements` (no
  space) and an indented `  ## MODIFIED Requirements`. Both parsers recognised a section only
  via `StartsWith("## ")`, so a near-miss was not a boundary at all and everything beneath it
  **inherited the previous section** — which in a mixed delta is `ADDED Requirements`, and
  therefore recognised. The exemption round 6 rejected over came back through a different door.

  And it is not cosmetic. QA asked OpenSpec itself: with `##MODIFIED`, all four `bookings`
  requirements come back from `openspec show --json` as `"operation": "ADDED"` — three
  wholesale replacements that would sync **beside** the requirements they replace, with every
  gate green.

  Two fixes, because the mechanism and the consequence both needed one:
  - **Fail closed.** Any line attempting a heading that is not exactly `## <Kind> Requirements`
    now *clears* the section rather than being ignored, so a near-miss orphans what follows it
    loudly. **Where that line falls was measured against `openspec show --json`, not reasoned
    about**: two spaces (`##  MODIFIED`) is a real heading to OpenSpec and must be accepted; no
    space is not and must be rejected. My first attempt trimmed before matching and was
    therefore *more* permissive than the tool — disagreement in the other direction, and just
    as useless.
  - **`No_added_requirement_already_exists_upstream`** guards the consequence instead of the
    spelling: an ADDED requirement whose heading already exists upstream is the mistake,
    whatever caused it, and detecting it needs no knowledge of markdown at all. This is the
    assertion that survives input shapes nobody thought to try.

  Probed against seven heading shapes on a **mixed** delta, which is where the previous two
  versions were blind. All seven now agree with OpenSpec.
- [x] 12.2 **MAJOR — `docs/notifications.md` enumerated what a booking carries and omitted the
  reference**, while this change's own proposal says subscribers get it for free. Its reader is
  the person writing the confirmation email — the *first* of the three motivations in **Why** —
  and the package's own documentation told them it was not there. Task 5.4's sweep named
  `booking-page.md` and `backoffice.md` and stopped.
- [x] 12.3 **MINOR — the twin placement scenarios were treated asymmetrically.** *Booking
  placement*'s success scenario gained "its reference"; *Service booking placement*'s, in the
  same delta and the same pass, did not. Identical to the fault 8.6 fixed for
  `default-frontend`, one capability over. Fixed.
- [x] 12.4 **NIT — fenced code blocks** are now skipped by the parser, so a `##` or
  `### Requirement:` inside an example cannot move a boundary or invent a requirement. Latent
  today; no active delta contains one.
