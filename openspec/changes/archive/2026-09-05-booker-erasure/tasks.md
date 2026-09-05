## 1. Domain: the erased state

- [x] 1.1 Add `BookerContact(string Name, string Email, string? Phone)` to `UBookIt.Core/Bookings/Booker.cs`.
- [x] 1.2 Reshape `Booker` to a private constructor plus `MemberKey`, `Contact` (nullable), `ErasedUtc` (nullable); keep `Create(memberKey, name, email, phone)` validating exactly as now and returning a booker whose `Contact` is present and whose `ErasedUtc` is null.
- [x] 1.3 Add `Booker.Erased(DateTimeOffset erasedUtc)` producing a booker with no member key, no contact and that instant. Confirm no factory can produce contact-null-and-instant-null.
- [x] 1.4 Add `Booking.EraseBooker(DateTimeOffset erasedUtc)` — `Booker` becomes `{ get; private set; }`, mirroring `Status`. Erasing an already-erased booking is a no-op that keeps the **first** instant. Nothing else on the aggregate changes.
- [x] 1.5 Fix the compile errors this produces across the solution. **Do not paper over them** — each one is a site that read `Booker.Name`/`.Email` unconditionally, and the point of the nullable `Contact` is that the compiler enumerates them. Record the list; it is the evidence for task 7.2.
- [x] 1.6 Confirm `Booking.Rehydrate`'s signature is unchanged and that it accepts an erased booker without revalidation.

## 2. Domain: the verb

- [x] 2.1 Add `EraseBookerAsync(Guid bookingId, CancellationToken)` to `IBookingService`, documented alongside `CancelAsync`.
- [x] 2.2 Implement it in `BookingService`: read via `IBookingStore.GetBookingAsync`, call `EraseBooker` with `timeProvider.GetUtcNow()`, persist via `UpdateAsync`. Unknown id fails with a stable failure code.
- [x] 2.3 Add or reuse a failure code for "no such booking" consistent with how `CancelAsync` reports it.

## 3. Persistence

- [x] 3.1 `BookingRow`: make `BookerName` and `BookerEmail` nullable, add `BookerErasedUtc` (nullable). `MemberKey` and `BookerPhone` are already nullable.
- [x] 3.2 `UBookItDbContext`: relax the two `IsRequired` mappings, map the new column, keep the existing max lengths.
- [x] 3.3 **Fix `SqlBookingStore.UpdateAsync`, which today writes only `row.Status`.** It must write the booker columns too. Without this the whole change is a silent no-op that passes any test asserting on the returned aggregate.
- [x] 3.4 Update `SqlBookingStore`'s row→domain mapping to produce `Booker.Erased(...)` when the erasure column is set, and `Booker.Create(...)` otherwise.
- [x] 3.5 Update `SqlBookingManagementStore`'s projection to carry the erasure instead of the contact details when erased.
- [x] 3.6 Add one additive migration widening the two columns and adding the erased column. No back-fill. Check the generated SQL is `ALTER COLUMN ... NULL` and `ADD`, with no data statement.
- [x] 3.7 Regenerate the model snapshot and confirm the diff contains only these three column changes.

## 4. Read port

- [x] 4.1 Change `BookingSummary` so the booker crosses as contact-or-erasure rather than two non-null strings. Keep it a summary — no new fields beyond what a row displays.
- [x] 4.2 Update the port's XML documentation: the booker crosses in exactly the two domain states, and the port stays read-only.

## 5. HTTP contract

- [x] 5.1 Reshape `BookerModel` to carry the stated condition plus a nested contact object and a nullable erasure instant; the condition is a **string**, not an enum.
- [x] 5.2 Make `BookingModel.Booker` non-nullable and document the three conditions and the BREAKING (unpublished) replacement of the null.
- [x] 5.3 Extend `BookerVisibility`'s documentation — it still answers only "may this caller see present details", and erasure is decided before it applies.
- [x] 5.4 Update `BookingModelMapper.ToModel` to the single precedence expression: erased → `Erased`, else visibility `Shown` → `Shown`, else `Withheld`. Keep the visibility parameter **required**.
- [x] 5.5 Add the erase endpoint to `BookingsController` as `POST bookings/{id:guid}/erase-booker`, returning the booking id and the erasure instant, echoing nothing that was erased.
- [x] 5.6 Gate the erase endpoint on sensitive-data access **before** the handler body runs, not as an `if` inside it. Prefer a filter/attribute over an inline check; if the codebase has no precedent for one, write the check as the first statement and leave a comment saying why it is not a policy.
- [x] 5.7 Confirm the list endpoint offers no filter, search, sort or count over contact details — unchanged by this work, and re-checked because it is adjacent.

## 6. Backoffice client

- [x] 6.1 Regenerate or hand-update the generated API client types for the new booker shape.
- [x] 6.2 Render the three conditions in the bookings table: contact details, "hidden", and "erased" — three distinct cells, no blanks.
- [x] 6.3 Show the Sensitive-data-group explanation only when at least one row is genuinely **withheld**; a page whose absences are all erasures must not show it.
- [x] 6.4 Add the new strings to `en-US` localization.
- [x] 6.5 Do not add an erase button in this change — the endpoint is the deliverable, and a destructive irreversible action needs a confirmation design that is not in this change's scope. Note it as follow-up.

## 7. Verification

- [x] 7.1 Update the `SensitiveDataRedactionTests` membership snapshot for the new `BookerModel`/`BookingModel` members, **after** the shape is final, and read its failure message before changing it.
- [x] 7.2 **Re-enumerate the delivery API by inspection**, not by trusting the proposal: list every endpoint in `UBookIt.Web/Controllers` and confirm none reads a booking back, so no anonymous surface can disclose an erased or unerased booker.
      **Done after the change, against the code.** Ten endpoints: `GET resources/{id}/free-time`, `GET resources/{id}/slots`, `GET resources/{id}/bookable-starts`, `GET resources`, `GET resources/{id}`, `GET services`, `GET services/{id}`, `GET services/{id}/bookable-starts`, `POST bookings`, `POST services/{id}/bookings`.
      **No GET returns a booking.** The only booker-carrying responses are the two placements, echoing the request body in the same request, so no anonymous surface can read a stored booker — erased or not. `UBookIt.Web` gained no endpoint in this change; its only edits were the four compile sites and `PlacedBooking`.
- [x] 7.3 Unit tests: erase changes only the booker; an erased booking still blocks; the clock supplies the instant; a second erasure keeps the first instant; the neither-state is unconstructible; placement never produces an erased booker.
- [x] 7.4 **Round-trip test through storage** for the erasure — erase, re-read in a **fresh context**, assert the columns are null and the instant set. Asserting on the returned aggregate proves nothing here.
- [x] 7.5 **Mutation-check task 3.3**: revert `UpdateAsync` to writing only the status and confirm 7.4 fails. If it passes, 7.4 is measuring the in-memory aggregate and must be rewritten.
- [x] 7.6 Mutation-check the precedence in 5.4: make withheld win over erased and confirm a test fails; make erased render as withheld for a non-sensitive-data caller and confirm a test fails.
- [x] 7.7 Endpoint tests: erase succeeds for a permitted user; is refused for section-access-only; is 401 anonymous; is idempotent; 404s on an unknown id; response carries no erased values.
- [x] 7.8 List-endpoint tests: erased reported as erased to **both** kinds of caller; withheld still reported as withheld; the booker member is never absent; reference survives both conditions.
- [x] 7.9 Integration test against real SQL Server covering the migration applying to a database holding pre-existing bookings, which keep their details.
- [x] 7.10 Client tests for the three cells and for the explanation's suppression on an all-erased page.

## 8. Modified requirements — the guarantee diff

Every requirement this change replaces wholesale, and what happened to each guarantee it made.
A `## MODIFIED Requirements` entry replaces body *and* scenarios, so anything not restated is
deleted with nothing in the diff that looks like a deletion.

- [x] 8.1 **`bookings` → "Booker identity".** Carried forward: the member key is optional and
      opaque; contact details are a non-empty name, a well-formed email, an optional phone; they
      are required regardless of the member key; both original scenarios verbatim. Added: the
      two-state rule, that placement only ever produces the details-carrying state, and that the
      neither-state is unconstructible. **Nothing dropped** — the validation requirement is
      unchanged for every booking the domain creates. **Also declares the published-API break**
      (`Booker.Name`/`.Email`/`.Phone` removed), which the first draft of this change omitted:
      the wire break was carefully qualified as unpublished and the same question was never
      asked about Core, where the answer is the opposite.
- [x] 8.9 **`bookings` → "Availability and placement service ports".**
      Added at QA round 3. The requirement enumerates what `UBookIt.Core`'s booking service is
      — "placement running the validation pipeline, and cancellation applying the status
      machine" — and this change adds a third verb to that service while leaving the sentence
      at two. Carried forward: every word of the dependency constraints, the generation-port
      reasoning, the service-store and management-store rules, the unpaged type listing, the
      batched claims read, the pure projection, the only-one-member rule, the earlier
      widening note, and all eight scenarios verbatim. Changed: the verb list, plus a note
      recording that this is the third widening and why it changes nothing the requirement
      protects. **Nothing dropped.**
      *THIRD occurrence of one lesson on one change. 9.3 swept outward at three sibling
      capabilities; round 1 found the miss in `booking-management`, round 2 in
      `sensitive-data`, round 3 here in `bookings`. Sweeping "the capability you are editing"
      is not one check — it is one per capability the change touches, and this change touches
      four.*
- [x] 8.2 **`booking-management` → "Bookings can be enumerated for management".** Carried
      forward: everything a row shows without a further read; resource names as a guarantee; the
      service name snapshot and its reason; the reference and the telephone case; read-only.
      Changed: the booker crosses as contact-details-or-erasure rather than as two non-null
      strings. The read-only sentence is strengthened to name the erase endpoint alongside
      cancel.
      **CORRECTED AFTER QA — this entry previously read "Nothing dropped" and was wrong.** The
      requirement carries **six** scenarios, not the two the first draft restated: *The service
      comes back with the list*, *A directly placed booking reports no service*, *An operator
      can match what a caller reads out* and *The front-end reads are unaffected* were deleted
      silently, along with the italic note recording why the last had once been narrowed. All
      four are restored verbatim, and the note with them. The cause is worth recording because
      it is not the one CLAUDE.md warns about: the guarantees were not weighed and rejected,
      they were never *read* — the requirement was skimmed to the end of a `sed` window and the
      window stopped twenty lines short of the requirement. **Diffing guarantees requires first
      establishing where the requirement ends.**
- [x] 8.3 **`booking-management` → "Bookings are readable over an authorized management endpoint".**
      <!-- Kept on one line deliberately: the integrity guard matches the requirement name in
           tasks.md, and a name wrapped across two lines is a name it cannot see. -->
      Carried forward: versioned, same swagger group, section authorization, never
      anonymous, purpose-built models, no domain types, port-supplies-it-or-it-is-not-there,
      details clumped in one member, decided server-side before composition, canonical
      reference, reference never withheld, service as one nullable object. Every original
      scenario is restated. **Superseded, not dropped:** "a single nullable object whose null
      means withheld" becomes a non-nullable member stating one of three conditions — the
      guarantee it protected (a client can tell "not given" from "nothing here", and cannot
      observe a half-populated pair) is restated in the new shape and strengthened, since the
      null it relied on had acquired a second cause.
- [x] 8.7 **`booking-management` → "Bookings have a backoffice collection view".**
      Added after QA, and only because QA's sweep found what mine did not: this requirement was
      left untouched while the contract underneath it changed, so two of its clauses had gone
      stale. Carried forward: the view is registered beside the others under the same section
      condition; a semantic table from the UI library; the reference first and why; the unpaged
      total and paging; a withheld booker stated rather than blank; the group explanation and
      the administrator surprise; visibility read from the response and never asked elsewhere;
      no computing what the endpoint did not return; formatting is not computing; no third-party
      widget framework; every string localized with `en-US`. All seven scenarios restated.
      **Two clauses restated rather than dropped:** "the booker where the endpoint supplied one"
      described a member that could be absent and now always is present, and the
      no-explanation scenario's *"every row's booker was supplied"* became vacuously true of
      every page — including an all-erased one, which is the case it most needed to select.
      Both are the same guarantees against the new shape. **Nothing dropped.**
      *The lesson is the recorded one: the capability you are already modifying is the one you
      are least likely to sweep. My 9.3 sweep looked outward at three sibling capabilities and
      never at the untouched requirements of the capability I had my hands in.*
- [x] 8.4 **`persistence` → "Schema shape and naming".** Carried forward: the `uBookIt` prefix,
      the full table list, capability uniqueness and cascade, the canonical uniquely-indexed
      reference and its reasoning, the no-foreign-key service columns, the placement-time
      service name snapshot. All nine original scenarios restated verbatim. Changed: the booker
      columns are nullable and an erasure instant is added. **Nothing dropped.**
- [x] 8.5 **`persistence` → "Store implementations honour Core semantics".** Carried forward:
      SQL Server implementations of both stores; `GetClaimsAsync` overlap semantics and its
      index; the batched multi-resource read; the type-filtered listing. All five original
      scenarios restated.
      **REWRITTEN AT QA ROUND 4, and this entry was stale until round 5 caught it.** It
      previously recorded the round-1 decision — "`UpdateAsync` persists **every**
      post-placement mutation rather than 'status changes' — a widening" — which round 4
      reversed after that widening produced three consecutive defects. The requirement now says
      the opposite: `UpdateAsync` writes the **status and not the booker**, a store exposes a
      **separate** erasure taking an id and an instant, the two touch **disjoint columns**, the
      erasure is absorbing **inside the write statement**, and no implementation may return an
      erased booker to carrying details.
      *A stale §8 entry is worse than none: this section is the artifact the project uses to
      prove guarantees survived a wholesale replacement, so one describing a superseded decision
      certifies a diff nobody re-did. It also left a scenario standing — "no separate
      erasure-only update method exists alongside `UpdateAsync`" — that FORBADE the design the
      same requirement mandates, which change ③ would have read as a prohibition on the very
      verb it must call. Replaced with two scenarios stating what the split actually
      guarantees.*
      **Deliberately rescoped, and still worth looking at twice:** the scenario asserting "the
      migrations folder contains no new migration" was written about that change's two reads
      and, left standing, reads as a prohibition on the package ever adding a migration. It is
      restated as a claim about those two reads, which is what it always meant. This change does
      add a migration.
- [x] 8.8 **`sensitive-data` → "Personal data is shown only to a backoffice user Umbraco permits to see it".**
      Added at QA round 2, which found the scenario *A permitted user sees contact details*
      falsified by this change: it said "**each** booking's booker name and email are present",
      and a permitted user reading an erased booking is correctly given neither. Carried
      forward: details shown only with sensitive-data access, decided by asking Umbraco; no
      group, flag or setting of our own; the decision made server-side before composition;
      section access still required as a separate gate. All four original scenarios restated,
      the first narrowed to bookings that have not been erased, the second widened to cover
      both kinds of absence, and a new scenario added for the erased case. **Nothing dropped.**
      *Second time this lesson has landed on this change: 9.3 swept three sibling capabilities
      and neither of the two I was editing. Round 1 found the first; this is the other.*
- [x] 8.6 **`sensitive-data` → "A withheld value is absent, not blanked".** Carried forward:
      omit rather than blank; details withheld together as a single member; no blanking even
      where a field is not nullable; and the principle that a member whose absence is already
      meaningful must not be overloaded. Scenarios 1 and 2 restated. **Superseded, not dropped:**
      "null cannot be misread as no booker" becomes "the response states whether details were
      withheld or erased" — the same guarantee, now discharged by the shape rather than by
      documentation, plus two new scenarios covering the erased case.

## 9. Documentation and close-out

- [x] 9.0 **Purpose prose, edited on this branch rather than deferred to sync.**
      `openspec/specs/booking-management/spec.md`'s **Purpose** said the honest v1 verbs are
      "*see* and *cancel*" and named "editing a booker" among things that live elsewhere, while
      this change adds a third endpoint to that capability. Deltas modify **requirements**;
      OpenSpec has no form for Purpose prose, and the repo's own precedent is to hand-edit it —
      three previous changes did, at sync. **Recording it as an instruction for the syncer was
      not enough**: nothing would have failed if nobody read the checklist, which is the same
      "stated but unenforced" fault this change has been rejected for elsewhere. So the edit is
      made here, and guarded by
      `BackofficeDocumentationTests.The_capabilitys_own_summary_names_every_verb_the_capability_has`,
      which enumerates the controller's routes and requires the Purpose to describe each one —
      so a fourth endpoint fails until somebody says what it is.
      *The spec text says erasing a booker is not the same as editing one, and says why; the
      earlier wording of this task said the opposite. The spec is right — nothing here permits
      changing a booker's details, only removing them.*
- [x] 9.1 `docs/backoffice.md`: what erasure does, that it is irreversible, that it needs the Sensitive data group, that it erases **one booking** and not a person's other bookings, and that erasing a future booking leaves the site unable to contact somebody who will arrive.
- [x] 9.2 State the single-durable-home constraint where a future change will meet it, so that emails and any audit trail have to confront it.
- [x] 9.3 Sibling-spec sweep (**done**: `default-frontend` and `delivery-api` mentions are all placement-time, where a booking always carries details; `resource-management`'s is authorization rationale, still true. Nothing falsified beyond the two already fixed in `persistence`.): grep the other specs in `openspec/specs/` for sentences this change falsifies — in particular anything asserting a booking always has readable contact details, or that `uBookItBooking`'s booker columns are non-null. Two were already found (`persistence`'s "`UpdateAsync` SHALL persist status changes" and its no-new-migration scenario); assume there are more and look rather than assuming there are not.
- [x] 9.4 Clean build from a clean tree; zero warnings; full .NET and client suites green.
- [ ] 9.6 **Release decision, deliberately left open and recorded so it is not lost.**
      `Directory.Build.props` is still `0.1.0` and this change declares two breaks to published
      API — `Booker.Name`/`.Email`/`.Phone` removed, and `IBookingStore` gaining a member. The
      proposal states that whether `0.1.0` consumers exist is a release decision rather than
      this change's; the version bump therefore belongs to whoever cuts `0.3.0`, and this entry
      exists so that person meets the question rather than discovering it. **Left unticked on
      purpose:** it is not work this change performs.
- [x] 9.5 Run `openspec validate booker-erasure --strict`. **Validated on BOTH the pinned CLI
      (1.6.0) and the current one (1.12.0).** 1.12.0 initially refused two deltas: it treats a
      scenario RENAMED inside a `MODIFIED` block as a dropped scenario, and refuses to archive
      rather than lose it. It was right to. Two scenarios had been renamed while their content
      was rescoped — `persistence`'s "No migration is added" and `sensitive-data`'s "Null cannot
      be misread as 'no booker'" — and although §8.5 and §8.6 record why the content changed,
      the tool cannot read prose and a reader diffing names would have seen two deletions.
      Both original names are restored; the rescoped content stays. **The name is the
      scenario's identity and the body is what a MODIFIED entry is for; renaming both at once
      makes a change indistinguishable from a removal.**
