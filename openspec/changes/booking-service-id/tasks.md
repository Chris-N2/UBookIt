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
  <br>**This was ticked before the test existed.** See 8.2: nothing invoked
  `PlaceForServiceAsync` and `PlaceAsync(MultiClaimBookingRequest)` for one request and
  compared them; the guarantee was safe by construction and unasserted, which is how a later
  change comes to believe it is protected.
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

## 8. QA round 1 — REJECT, two MAJORs, both "the claim is right, the artifact is missing"

Nothing was behaviourally wrong. Both MAJORs were scenarios and a ticked task with no test
behind them — a guarantee that holds by construction today and would break silently the day
construction changes.

- [x] 8.1 **Cancelling a service booking was tested nowhere.** The narrowed
  "indistinguishable in shape" clause names cancellation explicitly, and no test in any
  project cancelled an attributed booking. Two failures were possible and neither would have
  surfaced: cancellation refusing a booking it did not recognise as ordinary, or a status
  update rewriting the row and dropping the columns — turning a cancelled service booking
  into a cancelled walk-in, in exactly the reporting this change exists for.
  <br>**And the obvious place to assert it was the wrong one.** The unit-level test re-reads
  the same instance from the in-memory store, so its attribution assertion cannot fail for a
  persistence reason. It covers the domain half and now *says so*; the persistence half is
  asserted against real SQL Server. Mutation-checked there: adding `row.ServiceId = null` to
  `UpdateAsync` fails the integration test and nothing else.
- [x] 8.2 **Task 5.6 was ticked with no artifact.** Two tests now do what it claimed. The
  success case compares a request placed through both entry points — same interval, status
  and claims; service present on one and absent on the other. The refusal case matters more
  and was the one worth thinking about: a service path that *skipped* a rule shows up as a
  success where the general path fails, which an equivalence test over two successes cannot
  see. Mutation-checked: skipping the per-resource rules when a service is present fails the
  refusal test (and, as it happens, much of the suite — but the refusal test is the one that
  fails *for the right reason*).
- [x] 8.3 **MINOR — a failed service placement recording nothing** had no assertion pairing
  the failure with the attribution. Added to the existing all-candidates-busy test: the
  bookings that do exist there were placed directly, so a stray attribution could only land
  on one of them, and it would be caught. "No booking, therefore no attribution" is an
  inference, and the failure it would miss is a service booking counted that never happened.
- [x] 8.4 **MINOR — D1's rationale overclaimed, and QA was right to call it.** The design
  said the recorded value "cannot be set to something the placement did not actually do" and
  called the wrong value "unconstructible". Both false: `PlaceForServiceAsync` is public and
  `ServiceAttribution` has a public constructor, so a forged attribution is no less reachable
  than through the request field this design rejected. What D1 buys is narrower and real —
  the **general** contract cannot name a service, so the ordinary path cannot acquire one by
  accident or by a caller filling in a field because it was there. That narrower claim is
  what the spec's SHALL already says, so this was a prose defect rather than a conformance
  failure. Corrected, with the overclaim recorded rather than quietly deleted: it is exactly
  the kind of sentence a later change leans on while removing what actually protected it.
- [x] 8.5 **NIT — `Booking.Create`'s parameter is now required.** It is internal with one
  call site, so the default bought nothing and left a silent-omission case inside Core.
  `Rehydrate` keeps its default: it is the persistence boundary with many callers that have
  no service to give, and its one production caller is mutation-covered.
- [x] 8.6 **NIT — the NULL-semantics rule lived in two stores.** The rule that decides what
  `NULL` *means* was written twice, and two copies of it can drift into the same row saying
  one thing as an aggregate and another as a list row. One `BookingAttributionMapper` now,
  writing both columns from a single source so they cannot disagree.
- [x] 8.7 **QA corrected me on a guard I had called weak.** I flagged
  `A_booking_outlives_the_service_it_names` as probably only catching a foreign key that
  reached the database via a migration. It catches both: an FK in the migration fails it and
  the management-list test; an FK declared in `UBookItDbContext` alone fails 20 integration
  tests, because the fixture migrates and a model/schema mismatch breaks the context loudly.
  Recorded because I had said otherwise in writing.
- [x] 8.8 Clean-build gates, then QA round 2.

## 9. QA round 2 — APPROVE, four NITs

No must-fix. Two taken, two recorded as accepted with the reason.

- [x] 9.1 **`PlaceForServiceAsync` did not guard a null attribution**, while
  `CheckPlacementRules` guards its reference parameter. A null-oblivious caller would have
  placed an *unattributed* booking that succeeded and looked ordinary — the quiet failure
  this change exists to prevent, arriving through the very method that exists to prevent it.
  Now throws. Mutation-checked: removing the guard fails the covering test.
- [x] 9.2 **`ToColumns` was called once per column**, which is harmless — it is pure over
  the same value, so the columns could not disagree — but it read as two independent
  derivations of a pair that must agree, directly undercutting the comment saying otherwise.
  Destructured once.
- [x] 9.3 **NIT accepted, not fixed: the failed-placement attribution assertion is close to
  unfalsifiable.** QA is right that no code path can attach an attribution to a pre-existing
  direct booking, and that the real guard for that scenario is the claim count above it.
  Kept as documentation of what the scenario means, and recorded here so nobody later reads
  it as load-bearing. It is not what would catch a regression.
- [x] 9.4 **NIT accepted, not fixed: the two-entry-point pair does not compare the paths
  through a `conflict` or the atomic contract.** Both share one private pipeline and the
  concurrency suites cover that ground; adding a third comparison would assert the same
  delegation a third time. Recorded as a completeness note rather than closed.

## 10. The sync-time sweep found what three earlier passes did not

**A statement of a limit outlived the limit, and it was published on the read port itself.**
`booking-management`'s filter requirement explained the absent service filter by asserting
that a booking does not record the service that produced it — that the service chooses
resources and is then discarded. True when written. False the moment this change landed, in
the same capability the change was already modifying two other requirements in. Its scenario
went further and required the *package* to publish that explanation, and
`IBookingManagementStore` duly did: a reader inspecting the port was told bookings forget
something the package records.

**My own sweep (task 6.6) missed it, and so did both QA rounds.** 6.6 swept
`service-booking`, `delivery-api` and `bookings` — and stopped short of the capability whose
deltas were already open in front of me. The lesson is narrow and worth keeping: *the
capability you are already modifying is the one you are least likely to sweep*, because
editing two of its requirements feels like having read it.

- [x] 10.1 The requirement is **MODIFIED, not silently corrected in the main spec** — a
  falsified sentence found at sync time still needs a delta, or the change's own record does
  not contain the thing it changed. The conclusion is unchanged (no service filter here) and
  only the reason moves: from a limit of the data to a scope decision, because a filter
  belongs with the screen that would drive it. Every other SHALL and all six scenarios
  carried forward verbatim.
- [x] 10.2 The scenario now forbids the false claim as well as requiring the true one — it
  is not enough to state a reason if the old one can sit beside it.
- [x] 10.3 `IBookingManagementStore`'s comment corrected, with the correction visible rather
  than quietly rewritten.
- [x] 10.4 **A test now pins the prose**, so the next contradiction is a failing test rather
  than a third pair of eyes. It is a source grep, which the previous change deleted one of —
  and the distinction is the point: that one asserted *behaviour* through source text and
  broke when a comment mentioned a status name; this asserts *prose*, which is exactly what
  the requirement is about. Mutation-checked. It also caught my own first draft of the
  correction, which restated the false sentence verbatim while explaining that it was false.
- [x] 10.5 **A BOM I introduced was stripped.** Re-syncing one requirement through a script
  rewrote `booking-management/spec.md` with a UTF-8 BOM that no other spec carries, showing
  up as a phantom change to line 1. Caught by reading the diff rather than the summary.
- [x] 10.6 Clean-build gates, then QA round 3 on the new delta.

## 11. QA round 3 — REJECT, one MAJOR and two MINORs

- [x] 11.1 **A stray `</para>` left the public read port's XML doc malformed.** Mine: I split
  one paragraph into two and left the original's closing tag. `CS1570` — the only one in the
  repo — and **invisible to every gate**, because no project sets `GenerateDocumentationFile`.
  So the summary of the very type whose published prose this round exists to correct was
  itself invalid XML, fed broken to IDE quick-info and any doc generator. Fixed, and verified
  by building all four shipped projects with documentation generation on.
  <br>**The gate gap is real and is recorded, not fixed here.** Turning documentation
  generation on repo-wide surfaces five pre-existing `CS1573` warnings on
  `BookingQuery.Create` (partial `<param>` tags, from the previous change) and would need a
  decision about `CS1591` across the whole public surface. That is a change of its own for a
  package whose public API is a compatibility promise; doing it inside this one would be
  scope creep on top of a defect fix.
- [x] 11.2 **MINOR — the prose guard forbade one literal string, not the claim.** The
  requirement forbids *stating* that a booking does not record its service — a meaning. A
  reworded restatement passed, and so would the same words rewrapped, because the sentence is
  broken across ~90-character lines and the test read raw text. It now unwraps the port's own
  comment first, is scoped to that comment rather than the whole thousand-line file, and
  checks several phrasings. Mutation-checked three ways: the original claim reinserted
  **across a line break**, a **reworded** version of it, and the true statement removed — all
  three caught, and the first two are exactly what the first draft would have missed.
- [x] 11.3 **MINOR — `Contains("scope decision")` pinned a form of words.** It failed on a
  correct rewording and passed on the phrase appearing anywhere in the file. Replaced with
  assertions on the substance: that no filter by service is offered, and that a booking
  records the service it was placed for.
- [ ] 11.4 Clean-build gates, then QA round 4 to confirm — three rounds have each found
  something real, so the confirming round is cheap next to merging a defect.
