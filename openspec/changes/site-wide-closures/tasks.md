## 1. Domain: the closure layer and precedence

- [x] 1.1 Add a `SiteClosure` value type (id, date, label) with `Create` validating a required, trimmed label against the name-column length and returning the stable code `closure-label-invalid`; verify with unit tests covering absent, blank, whitespace-only and over-long labels
- [x] 1.2 Add the failure codes `closure-label-invalid`, `duplicate-closure-date` and `closure-not-found` to `FailureCodes`; verify by a test asserting each constant's literal value, since the codes are a published contract
- [x] 1.3 Extend `AvailabilityConfiguration` with an applicable-closure layer via a new optional parameter on `Create` and a read-only member, leaving the existing signature and every existing caller untouched; verify the existing availability suite still passes unchanged
- [x] 1.4 Make `EffectiveWindows` consult closures first — a closure yields no windows for its date, whatever the exception or weekly pattern says; verify with unit tests for closure-over-weekly, closure-over-override-exception, and closure-over-closure-exception
- [x] 1.5 Add the resource's opt-out closure ids to `Resource` as a new optional parameter on `Create` (defaulting to none) and a read-only member; verify a resource created without them carries none and existing construction sites still compile
- [x] 1.6 Verify the full precedence ladder end to end in `UBookIt.Core` — closure, then the resource's own exception, then the weekly pattern — including that an opted-out closure produces a result identical to a site holding no closure for that date
- [x] 1.7 Define the closure read and management ports (`ISiteClosureStore` and its management counterpart) as **new** interfaces; verify by a test asserting no member was added to `IResourceStore`, `IBookingStore` or any other published port

## 2. Persistence: tables, migration, hydration

- [x] 2.1 Add `SiteClosureRow` and `ResourceClosureOptOutRow` entities with a unique index on the closure date, a unique (resource, closure) pair, and cascade delete from both parents; verify the model builds and the context snapshot reflects both tables
- [x] 2.2 Generate one additive EF Core migration creating both tables; verify against a database from the previous version that both tables exist and no existing table, column or index was altered or dropped
- [x] 2.3 Implement the closure stores (list with an upcoming/all filter, create, update, delete), with deterministic ordering; verify with integration tests on real SQL Server, including that a duplicate date is refused as `duplicate-closure-date` under concurrent writes rather than throwing
- [x] 2.4 Hydrate applicable closures in `ResourceRowMapper.ToDomain` and the resource stores — the full closure list minus that resource's opt-outs — reading closures **once per store call**; verify a type-filtered listing issues one closure query for the whole batch, not one per resource
- [x] 2.5 Persist opt-out rows through `ToRow`/`ApplyScalars` alongside open hours and exceptions, replaced wholesale in the same transaction; verify with integration tests that concurrent updates leave exactly one writer's complete set and that an update replaces rather than merges
- [x] 2.6 Verify the write-back trap is shut: load a resource inheriting a closure, save it unchanged, delete the closure, and assert the resource has no exception for that date and the date resolves from its weekly pattern
- [x] 2.7 Write the seam test through the production entry point — a real resource read followed by a real availability query on a closure date — rather than a test either side of the join; verify it fails if hydration is removed
- [x] 2.8 Register the new stores in the persistence composer; verify by resolving them from the container in an integration test

## 3. Availability and placement wiring

- [x] 3.1 Verify slot projection, bookable-start projection and the pure `ProjectBookableStarts` overload all omit a closure date, with no change to their signatures; assert the public surface is unchanged by a test over the frozen API
- [x] 3.2 Verify service availability over a candidate pool omits a closure date, and still offers it when a candidate that can fill every role has opted out
- [x] 3.3 Verify placement on a closure date fails with `outside-open-hours`, that the failure names neither the closure nor its label, and that placement on an opted-out closure date succeeds

## 4. Management API

- [x] 4.1 Add closure DTO models and a closures controller in the `ubookitbackoffice` swagger group (list with upcoming/all filter, create, update, delete), with problem-details responses carrying a type member; verify with controller tests covering each verb and a 404 for an unknown id
- [x] 4.2 Enforce the verb split in authorization policy — writes require `UBookIt.Settings`, reads require `UBookIt.Configure` or `UBookIt.Settings`; verify with tests for each verb combination, including that an Umbraco administrator without `UBookIt.Settings` is refused a write
- [x] 4.3 Add opt-out closure ids to `ResourceRequestModel` with full-replacement semantics and `closure-not-found` validation; verify round-trip, replacement, omission-means-none, and the unknown-id rejection
- [x] 4.4 Add the projected closure list (id, date, label, excluded) to `ResourceResponseModel`; verify a resource's response reports every closure with the correct excluded flag
- [x] 4.5 Compute the superseded-exception marking server-side, firing only where the outcome differs; verify an override exception on a non-excluded closure date is marked, a closure exception on the same date is not, and opting out clears the marking
- [x] 4.6 Regenerate the OpenAPI client and verify the generated TypeScript compiles and carries the new endpoints and members

## 5. Backoffice client

- [x] 5.1 Add the `Closures` section view and manifest entry gated `oneOf: [UBookIt.Configure, UBookIt.Settings]`; verify the view appears for a Configure-only user and is absent for a user holding neither
- [x] 5.2 Implement the closures list with create, edit and delete, defaulting to upcoming with past available on request, served by the server-side filter; verify with client tests over the list's filter state
- [x] 5.3 State the read-only condition for a user without `UBookIt.Settings` — what the grant is and where it is given — instead of rendering controls that would be refused; verify with a client test for the read-only rendering
- [x] 5.4 State unconditionally that closures never cancel bookings already placed, computing nothing; verify by a test asserting the statement is produced without any booking request being made
- [x] 5.5 Add the Global closures group to the resource editor — each closure's date and label with an opt-out control, identifiable as originating outside the resource; verify opting out round-trips through a save and reopen
- [x] 5.6 Render the superseded-exception statement from the server's marking, programmatically associated with the exception it concerns; verify with a client test that the marking is read from the response and not derived from the closure list
- [x] 5.7 Add the new localization terms to `en-us.ts`; verify with the cross-language guard that every term the C# side and the elements reference has an entry, so no raw key can render
- [x] 5.8 Verify the accessibility baseline on both surfaces: every input labelled, failures announced and associated, full keyboard operability with visible focus on the closures view and the Global closures group

## 6. Live verification

- [x] 6.1 Build the client, then the solution, and verify zero warnings in Release
- [x] 6.2 On the running TestSite, create a closure and verify a resource's availability loses that date in the front-end booking flow, with nothing naming the closure or its reason
- [x] 6.3 On the running TestSite, opt one resource out and verify the date returns for that resource only, and that a resource whose own override exception was superseded now offers those windows
- [x] 6.4 Verify the delivery API exposes no closure, label or opt-out on the public resource read model, and that a closed date is indistinguishable from any other unavailable date
- [x] 6.5 Verify the verb split live: a Configure-only backoffice user sees the list and can opt a resource out but cannot create a closure

## 7. Documentation

- [x] 7.1 Document site closures in the README feature list and the setup guide, including the precedence ladder and the opt-out; verify the README's feature claims match what ships, since a denial pinned by a guard has shipped here before
- [x] 7.2 Document that a host implementing its own `IResourceStore` owns applying closures, alongside the other published-port obligations; verify the statement names the interface and the consequence
- [x] 7.3 Record in the front-end/theming docs that a closed date is indistinguishable from any other unavailable date, so a theme author does not expect a reason to render

## 8. Wholesale replacements to re-diff

Each of these requirements is replaced **in full** by this change's deltas, so anything the current
version guarantees and the new one forgets to restate is deleted with nothing in the diff that looks
like a deletion. `design.md` (D9) records the diff made at propose time; each task below is to check
that record against `openspec/specs/` as it stands now, not to trust it.

- [x] 8.1 Re-diff **Date exceptions** (availability): every SHALL and both original scenarios carried forward, closure precedence and retention added; verify by listing each original guarantee as carried, dropped or superseded
- [x] 8.2 Re-diff **Free-time computation** (availability): subtraction of blocking claims, ordered disjoint output, non-blocking statuses, window coalescing, all three original scenarios; verify the same way
- [x] 8.3 Re-diff **Placement validation pipeline** (bookings): the ordered code list, start-alignment rule, both representability paragraphs, the service-pool rule, all nine original scenarios; verify the same way
- [x] 8.4 Re-diff **Access within the section is decided by four verbs** (permissions): four bullets, every italic rationale, Manage-implies-Read, Settings-implies-nothing, the union rule, the no-permission-store rule, all twelve original scenarios; verify the same way
- [x] 8.5 Re-diff **Resource CRUD endpoints** (resource-management): every existing sentence and all five original scenarios; verify the same way
- [x] 8.6 Re-diff **Workspace editor for a resource** (resource-management): every existing sentence and all five original scenarios; verify the same way

## 9. Spec sync

- [x] 9.1 Re-run the sibling-falsification sweep over every spec not in this change's deltas, looking for sentences about availability, exceptions or verbs that closures make untrue; verify by listing what was checked and what was found
- [x] 9.2 Run `openspec validate --strict` and verify the change passes
- [x] 9.3 Run the full unit suite and verify `ChangeDeltaIntegrityTests` passes, since it is the guard that refuses an unlisted wholesale replacement

## Results

### Guarantee diffs (group 8)

Re-diffed mechanically against `openspec/specs/` as it stands, with a script that was first
**proved able to find a deletion**: removing one scenario and one untouched SHALL from a copy
of the availability delta made it report both. (A diff that reports nothing is as suspect as
one that reports everything.)

**No scenario was dropped from any of the six replaced requirements** — every one carries
more scenarios than the version it replaces:

| Requirement | Scenarios before → after | SHALLs checked |
|---|---|---|
| availability / Date exceptions | 2 → 5 | 3 |
| availability / Free-time computation | 3 → 5 | 4 |
| bookings / Placement validation pipeline | 8 → 10 | 10 |
| permissions / Access within the section is decided by four verbs | 12 → 17 | 4 |
| resource-management / Resource CRUD endpoints | 5 → 11 | 4 |
| resource-management / Workspace editor for a resource | 5 → 9 | 3 |

Five SHALL sentences were reported as "not found verbatim". Each was checked by hand and is a
sentence this change **deliberately widened**, with the remainder intact:

1. *Free-time computation* — "open hours (with exceptions applied)" → "(with the resource's own
   exceptions and any applicable site closures applied)".
2. *Placement validation pipeline* — the same widening inside the `outside-open-hours` gloss.
3. *Four verbs* (twice) — the Configure and Settings bullets gained closures; the sentences
   themselves (`define exactly four permission verbs…`, `Manage SHALL imply Read`,
   `SHALL imply nothing and SHALL be implied by nothing`, the union rule, the no-permission-store
   rule) are all present verbatim, confirmed by literal match.
4. *Workspace editor* — the grouped-sections list gained **Global closures**.

### Sibling-falsification sweep (9.1)

Checked every spec **not** in this change's deltas for sentences about open hours, exceptions,
availability or verbs. **Nothing was falsified**; the four worth recording as considered:

- `delivery-api` "SHALL NOT expose management or internal configuration (raw weekly open-hours
  pattern, date exceptions)" — **still true**, and closures are not exposed. The parenthetical is
  an example list rather than an enumeration, and the positive guarantee for closures is stated
  with its own scenarios in `site-closures` ("Closures are not disclosed publicly"), so the
  requirement was left unmodified deliberately rather than by oversight.
- `delivery-api` failure-code mapping — closures reuse `outside-open-hours` → 400, already listed.
  Confirmed live.
- `booking-management` move-refusal wording ("outside the resource's open hours") — true of a
  closure date, which is outside the resource's effective open hours.
- `site-settings` time-zone consequence ("open hours and exceptions are stored as day-and-time
  without a zone") — a closure is a bare date, so it is reinterpreted on the same terms; the
  statement remains true and complete about what it names.

### Live verification (group 6)

Against the running TestSite, over the real HTTP surfaces:

- **6.1** Release build clean, **0 warnings**. (Trap worth knowing: the vite bundle's hashed
  filenames change on every client build, so a stale `obj/<config>` static-web-asset manifest
  fails the build with "No file exists for the asset". Deleting `wwwroot/App_Plugins/UBookItBackoffice`
  and the config's `obj` folder clears it — hit in both Debug and Release.)
- **6.2** On the **shipped Razor booking page**, not just the API: 29 dates offered; closing one
  in the middle left 28, with the closed date gone, **every other date still offered**, and the
  date restored when the closure was deleted. The rendered page names no closure, label or
  reason. (The check first flagged the word "closure" on the page — it was the fixture resource's
  own display name, "Closure Live Room", in the heading. Explained rather than waved away.)
- **6.3** Opting one resource out returned 8 slots on the closed date while a second resource
  **created after the closure** still had none. A resource whose own 10:00–14:00 override had been
  superseded then offered exactly 10:00, 11:00, 12:00 and 13:00 — the ladder, end to end.
- **6.4** The public resource read model carries no closure, label or opt-out. Placement on a
  closed date was refused **400 `outside-open-hours`**, with nothing in the body naming the
  closure or its label; a control placement on an open date succeeded.
- **6.5** Proved against a **real user holding `UBookIt.Configure` and nothing else** (a user
  group and API user created for the purpose): `GET closures` → 200, `POST closures` → **403**,
  `GET resources` → 200, `GET settings` → 403. The credentials were revoked and the user disabled
  afterwards; every closure created by the checks was deleted, so the TestSite is back to
  baseline.

### The backoffice UI, live — DONE, and it found three things

Driven in the real backoffice once the browser was connected. The Closures tab appears between
Bookings and Settings; a closure was created **entirely by keyboard** (Tab through date and name,
Enter on Save) with visible focus throughout, and the table, the Global closures group and the
superseded statement all render.

**Three defects that no source-level test could see:**

1. **The date and label were stated three times on each opt-out row.** `uui-toggle` renders its
   `label` as visible text *as well as* using it for the accessible name — measured in the running
   backoffice — so the two spans beside it repeated what the control already said. The spans are
   gone; the toggle's own label carries it once.
2. **The row buttons used a colon** (`Edit: 2026-12-25 Christmas Day`) where the rest of the
   backoffice writes `Edit Closure Live Room`. Now consistent. (The visible-name style itself is
   the house pattern — the Resources list does the same — so it was left alone.)
3. **A save reported `superseded: false` however the site was configured.** The create and update
   responses were mapped from the aggregate the REQUEST produced, and a request carries opt-outs
   but never closures — so the layer that decides the marker was absent from it, while a GET of
   the same resource said `true`.

   **What this did NOT do — corrected after QA round 1.** The first version of this note said
   "the editor lost the statement the moment somebody saved it and got it back on reload".
   **That cannot happen**: `resources-view.element.ts:33` binds `@ubookit-saved` to the handler
   that clears `_editing`, so the editor unmounts on save and the shipped backoffice never
   renders a save response at all. The defect was real, but it was a **contract** defect — the
   management API is a published surface and its save response disagreed with its own next read
   — not a user-visible one. The record justified an extra database read with a symptom that does
   not exist, which is the third handover claim on this project a reviewer has found false.

   Fixed by re-reading through the read port after a write, so a save answers exactly what the
   next read would. **Composing the closure layer in the controller instead was rejected**: it
   would be a second implementation of "which closures apply", which the hydration seam exists to
   keep singular. The null branch of that re-read returns not-found rather than falling back to
   the write aggregate — the fallback quietly reinstated the very defect (QA proved the branch
   wrong by mutation, not merely untested).

   **The test double hid this.** `InMemoryResourceStore` returned exactly what had been written,
   so the controller looked consistent with it and every test passed. The double now hydrates
   closures on every read path, and three tests assert the save response, the paged list and the
   next read all agree.

One result worth recording because it looks like a defect and is not: after opting out through the
UI, the delivery API offered **no slots on 25 December**. That is the 90-day booking horizon, which
ends 2026-12-22 — confirmed by walking the boundary on the same resource (20th, 21st, 22nd → 8
slots each; 23rd, 25th → none).

### Superseded — the live statement

> *A global closure covers this date, so this exception has no effect at the moment. Tick "Open
> anyway" for that date in Global closures to use it.*

Rendered italic, inside the exception's own fieldset and referenced from it, and it disappears once
the closure is overridden.

### Superseded (historic note)

**5.8's live half is NOT done.** The source-level half is (`ClosureAccessibilityTests`: every
control names the closure it acts on, native inputs are labelled, the failed save is announced
and focused, the superseded statement is referenced from its exception). What has **not** been
verified is the rendered backoffice: the Closures view and the Global closures group in a real
browser, keyboard operability and focus visibility through `uui-*` shadow DOM, and that the
section tab appears where expected.

The Chrome extension is not connected in this session, so it could not be driven from here. This
package's own history says backoffice editor faults show up only in the real shadow DOM, so this
is a genuine gap rather than a formality — it needs doing before QA, by hand or with the
extension connected.

## QA round 1 — REJECT, and what it found

Six MAJOR findings, all upheld. Every claim I made was independently verified by the reviewer
(suite counts, Release warnings, `--strict`, and the guarantee diff, which they re-derived with
their own differ rather than re-running mine). What the review found was not wrong claims about
the code, but **missing guards, and two documents disagreeing with it**:

1. **`specs/site-closures/spec.md` contradicted the permissions delta** on who may set a
   resource's opt-out — the security sentence said Configure OR Settings, while the code, the
   permissions delta, the README and the backoffice doc all say Configure alone. The spec was
   wrong; it now separates reading the list (either verb) from setting an opt-out (Configure,
   because it is a resource write), with a scenario for the Settings-only refusal.
2. **No test evaluated the `ClosuresRead` policy.** The one that claimed to compared `[Authorize]`
   attribute *strings*. Three tests now run the real policy engine through the harness in
   `PermissionsTests`; the misleading test is renamed to what it actually asserts.
   **Mutation-checked**: registering `ClosuresRead` with `Settings` alone turns the new test red.
3. **Task 2.4's N+1 verification did not exist.** Now counted rather than asserted:
   `Listing_by_type_reads_closures_once_for_the_whole_batch` records every command a real context
   sends and requires exactly one closure read for a five-resource pool. **Mutation-checked**:
   moving the read inside the projection turns it red. A second test proves no closure query joins
   the exception or open-hours tables, so precedence stays in the domain.
4. **The delivery-API omission guard tested a sample the change had outgrown** — it matched
   `open|hour|exception|window` and never `closure`. Extended, plus a second guard over the
   serialized body, because a label can escape through a member whose name says nothing.
5. **The double's comment said "every read" and hydrated one.** `ListAsync` and `ListByTypeAsync`
   now hydrate too, and a new test covers the paged list, which was the uncovered surface.
6. **The item-3 narrative above was false** — corrected in place, above, rather than quietly
   edited away.

Also fixed from the MINORs: the null fallback that reinstated the defect; `showsEditingControls`
exported, tested and called by nothing (now the decision every write branch routes through); the
default filter using UTC rather than the site's time zone (with a fixed-clock test at an instant
where the two dates genuinely differ, and a fallback test for an unreadable zone id); and
`notPermitted` serving two different conditions with one message.

**Deliberately not changed**, recorded rather than left silent:

- **The resource editor's Global closures group is unfiltered.** The reviewer is right that it
  grows without bound. Filtering it to upcoming would change what the `resource-management` delta
  promises ("the site closures that apply to the resource") and would hide an exemption on a past
  date from the only screen that can remove it. It belongs with pruning, which this change
  deliberately does not do.
- **`persistence`'s *Schema shape and naming* still says "SHALL comprise"** over an enumeration
  that omits seven tables. Pre-existing; D9 is about where new tables are declared, not about that
  word, and correcting it here would mean a wholesale replacement of an untouched requirement for
  a single misleading verb.

## QA round 2 — REJECT, and two of the three were round 1's fixes

The shape I was warned to expect. Both of the guards written in round 1 were **proved defective by
mutation**, not merely doubted:

1. **`specs/permissions/spec.md` contradicted itself inside one requirement.** Round 1 fixed who
   may *set an opt-out*; it left the **read** wrong in the place that matters most — the bullet
   list, which is the normative enumeration of what each verb governs. The `Settings` bullet did
   not mention reading the closure list, and the rationale assigned reading to `Configure`, while
   four paragraphs later the change's own added sentence said the read sits with either verb.
   Now: the bullet carries the read, the rationale splits three acts instead of two (change =
   Settings, exempt = Configure, **read = both**), a `Settings alone reads the closure list`
   scenario exists, and the README and `docs/backoffice.md` rows carry the same correction.
2. **`No_query_joins_closures_to_decide_precedence` was vacuous.** `Assert.All` over an empty
   sequence passes; QA replaced the closure store's body with `return []` and the test stayed
   green. `Assert.NotEmpty(closureCommands)` added — **verified by reproducing that exact
   mutation**, which now fails.
3. **`A_serialized_resource_read_model_carries_no_trace_of_a_closure` could not see what its own
   comment named.** It hand-built the model, so it inspected the type's defaults rather than the
   endpoint's output. QA added an `UnavailableReason` member, had the mapper fill it with "Site
   closure: Christmas Day", and watched all 38 delivery tests pass while the public API leaked the
   label. Rewritten to serialize a **real controller response** — both the single read and the
   paged list — for a resource genuinely subject to a distinctively-labelled closure. **Verified
   by reproducing QA's mutation**: the rewritten guard fails on it.

MINORs: `CreateResource` now declares the 404 its re-read can produce (client regenerated, which
also picked up `superseded?: boolean | null`); the closure list is read once per write rather than
twice; the duplicated third assertion — identical to the first, under a remark about writing — is
gone, with a note pointing at the two tests that actually carry the non-implication; the zone
try/catch records why it is a third copy with a third fallback; and the comment arithmetic said
22:00 where New York is 23:00 under EDT.

QA withdrew both round-1 deferrals, accepting the reasons — and judged the Global closures one
better than its own finding, because filtering would hide a past-dated exemption from the only
screen that can remove it.

### What was NOT seen rendered

The two new refusal messages (`notPermittedRead` / `notPermittedWrite`) have **not** been observed
in the real backoffice. After the rebuild, Umbraco stopped registering package extensions in this
browser session across two tabs — the bundle's entry filename is stable while its content changes
each build, so a cached entry points at chunk hashes that no longer exist. **The code is sound**:
importing the bundle directly loads all 15 manifests, the Closures view module evaluates without
throwing, and the element defines — which is the technique this project already records for
telling that flake from a module-evaluation break. Both strings are covered by
`ElementLocalizationKeyTests`, which is mutation-proved to fail on a missing term, so a raw key
cannot render; what is unverified is only how they look on screen.

## QA round 3 — REJECT, one MAJOR, and it was the same fault twice

**The serialization guard had no precondition that its subject carried a closure.** QA left a
label leaking through a member called `Note` *and* dropped the `closures:` argument from the
fixture: the test passed while the anonymous delivery API handed every caller the label, because
`Zzyzx` was never in the body to be found. `Assert.DoesNotContain` over an absent subject is the
string-matching twin of `Assert.All` over an empty sequence — **the exact fault round 2 found in
the SQL guard, whose fix I applied twelve lines away in the same commit and did not carry across.**
The class, not the instance: this repository's own recorded lesson, and I had it in front of me.

Fixed with the precondition **and** the stronger check QA suggested: the guard now asserts the
resource carries exactly one closure, and that the closure is **in force on the read path it
inspects** — the delivery availability for that date is empty through the real
`AvailabilityController`. **Verified by reproducing QA's mutation**: dropping `closures:` now fails
on the precondition rather than passing vacuously.

Two MINORs, both one-liners in tests: `isSuperseded` now pins `null`, which joined the contract in
round 2 when the member became nullable and nothing followed it (a later "simplification" to
`superseded !== false` would have passed everything while telling an operator that an exception in
force has no effect); and the SQL-shape guard now asserts the read it inspects actually hydrated,
rather than only that a closure query was sent.

QA answered the three questions I put to it, by mutation rather than by reading:

- The rationale and the bullet list now agree, clause by clause — **finding 1 closed**.
- `NotEmpty` is the right precondition for the shape guard; a count would over-specify, because
  the number of closure reads is its sibling's subject, not this one's.
- The distinctive label is load-bearing: leaking **only** the label through a member named `Note`,
  with no forbidden substring in the value, is caught by `Zzyzx`/`stocktake` and by nothing else in
  the list.

**Spec criteria verified: 51 of 51**, with the live gap below standing.

### The live gap, as QA framed it

QA advised **against** restarting the TestSite for a further round: `notPermittedRead` is
unreachable behind the manifest's `oneOf` gate, `notPermittedWrite` is a plain paragraph of a term
whose resolution is mutation-proved, and nothing since has touched rendered markup structure. The
two refusal messages are folded into the existing 5.8 live-verification gap, to be discharged with
it before archive. The extension-registration flake is a known caching shape — stable entry
filename, per-build chunk hashes — and is not evidence about this change.
