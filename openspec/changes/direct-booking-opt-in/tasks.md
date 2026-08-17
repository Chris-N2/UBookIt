## 1. Core — the permission

- [x] 1.1 Add to `Resource` whether it may be booked on its own, defaulting to withheld. Not part of eligibility, not part of validation — `Resource.Create` must accept either answer and reject neither.
- [x] 1.2 Confirm the resolution chain is untouched: type → capabilities → duration, with no fourth term. A test that a withholding resource still appears in a candidate pool, since this is the guarantee the whole change rests on.
- [x] 1.3 Rehydration carries it, like every other stored scalar. `Booking.Rehydrate`'s neighbourhood is the model for what "round-trips" has to mean here.

## 2. Core — the guard

- [x] 2.1 Refuse in `IBookingService.PlaceAsync(BookingRequest)`, **before** it composes the `MultiClaimBookingRequest` and delegates. New stable code `resource-not-directly-bookable`.
- [x] 2.2 **The mutation that matters, and the one an obvious test misses**: move the guard down into the multi-claim overload and confirm service placement breaks. A test asserting only "a withholding resource cannot be booked directly" passes that implementation while every service booking is broken. The covering test MUST book **the same withholding resource both ways** and assert opposite outcomes.
- [x] 2.3 A single-role service of count 1 over a withholding resource places successfully. This is the case most likely to be "fixed" into a refusal by someone who reads the rule as being about claim-set size rather than about which entry point was used.
- [x] 2.4 The refusal precedes the rule pipeline: a withholding resource asked for a time outside its open hours reports `resource-not-directly-bookable`, not `outside-open-hours`.
- [x] 2.5 Nothing is persisted on refusal, and the code is never `conflict` — it cannot come good on a retry.
- [x] 2.6 Confirm the ⑨-2 all-fail classification is untouched: service placement never reaches this code, so it must not appear in the deterministic-refusal whitelist. Assert the whitelist is unchanged.

## 3. Persistence

- [x] 3.1 One additive, non-nullable column with a default. Existing rows take the default, which is the behaviour change the proposal states.
- [x] 3.2 The migration is additive; no destructive schema change and no upgrade path needed, since nothing is published.
- [x] 3.3 Round-trip through the store, and confirm the value survives a full update rather than being silently preserved from the prior row — full-replacement semantics, as the capability set has.

## 4. Delivery API

- [x] 4.1 Publish it on the resource read model, on both `GET /resources` and `GET /resources/{id}`, as a value and never by omission.
- [x] 4.2 `POST /bookings` maps the new code to 400 through the existing catch-all rule. Assert the status rather than assuming it — the mapping requirement is not being modified, so this is the only thing holding it.
- [x] 4.3 Availability reads answer identically for a withholding resource: free-time, slots and bookable-starts. Assert this directly; it is the counter-intuitive half and the one most likely to be "tidied" later.
- [x] 4.4 `POST /services/{id}/bookings` over a withholding resource succeeds, end to end over HTTP rather than only in Core.
- [x] 4.5 Confirm no other delivery contract moved — the OpenAPI diff should be one added member and nothing else.

## 5. Management API and backoffice

- [x] 5.1 Carry it on create, read and update. An omitted value withholds, consistent with the domain default and with the capability collection's treatment.
- [x] 5.2 A full update can withdraw it — full-replacement semantics, not merge.
- [x] 5.3 The resource editor gains a control that states what withholding *means*: still bookable as part of a service, not bookable alone. A label reading only "bookable" is wrong and will be read as "can be booked at all".
- [x] 5.4 The resources list shows it, because with the default withheld the editor's question is "why can nothing book this room?" and the list is where they will look.
- [x] 5.5 Neither answer blocks a save, in either direction.
- [x] 5.6 Accessibility: the control is labelled in the same shadow root, its explanatory text is associated rather than merely adjacent, and the list column is not conveyed by colour or icon alone. **Read the rendered shadow DOM, not a screenshot**, and check the value both ways.
- [x] 5.7 Regenerate the client against the running TestSite and check the diff for unrelated drift.

## 6. Default front end

- [x] 6.1 Render the "not offered on its own" statement for a withholding resource, and offer no submission.
- [x] 6.2 A resource that permits direct booking but has no bookable times still reports no available times. **Test both, or the two collapse into one message** — this is the ⑨-1a failure mode arriving through a new door.
- [x] 6.3 The permitted flow is unchanged end to end: length choice, anti-forgery, the 303 redirect, the confirmation, and failure handling with input preservation.
- [x] 6.4 The statement meets the flow's existing WCAG 2.2 AA baseline and is announced on the same terms as its other outcome messages.

## 7. Verification

- [x] 7.1 Full solution build with `--no-incremental`, **TestSite stopped first**. Only the known NU1903 advisories.
- [x] 7.2 Unit, integration and client suites green, including every ⑤–⑨ scenario unchanged. The ⑨ suites are the guard for task 2.2 having been done right.
- [x] 7.3 Live: a withholding resource refuses direct placement over HTTP and succeeds through a service that resolves to it.
- [x] 7.4 Live: the no-JS flow renders the statement for a withholding resource and the ordinary flow for a permitting one.
- [x] 7.5 Live: the backoffice sets it both ways and the list reflects it.
- [x] 7.6 Stop the TestSite and check port 44348 for orphans. Umbraco 17 intermittently renders the backoffice shell without registering package extensions; a fresh navigation or a fresh login clears it, and importing the bundle to check `customElements.get(...)` is what distinguishes that flake from a real fault.

## 8. Spec hygiene

- [x] 8.1 Guarantee diff for the one MODIFIED requirement, `Resource read model`. Only one sentence is extended in place; confirm nothing else moved. **A diff reporting nothing is as suspect as one reporting everything** — prove the tool can find a deletion before believing it.
- [x] 8.2 The outward grep for sibling specs this change falsifies, **before and after** the sync. Grep the vocabulary of the *mechanisms*, not only of the change: "bookable", "book a resource", "single resource", "directly", "read model carries", "no times available", "unavailable". Every change but one has found something here.
- [x] 8.3 Confirm the "everything optional" invariant is recorded as **narrowed, not inverted** — services remain opt-in sugar and the direct path survives; what changed is that it became editor-controlled. Update `openspec/specs` prose only if some requirement actually states the old form.

### Notes from the apply

- **The seam held exactly as designed.** `PlaceAsync(BookingRequest)` loads the
  resource, refuses if it exists and withholds, and otherwise delegates unchanged.
  A missing resource falls through on purpose so the pipeline still reports
  `resource-not-found` rather than this rule inventing a second opinion about a
  resource it could not read. Cost: one extra read on the direct path, taken
  knowingly rather than threading a loaded aggregate through a signature every
  service placement would have to carry.
- **Task 2.2's mutation, run:** moving the guard into the multi-claim overload
  turns 7 tests red, including
  `Spec_scenario_the_same_resource_is_bookable_as_part_of_a_service` and
  `Spec_scenario_a_single_role_service_of_count_one_is_still_a_service` — the two
  the task exists for. Without them the suite would have gone green with every
  service booking in the product broken.
- **The fixture problem was real and is the reason `DirectBookingTests` exists.**
  Defaulting to withheld broke 79 unit tests and 3 integration tests, all of them
  fixtures using the direct path to occupy a calendar. Diagnosed rather than
  assumed: the failing line was `Assert.True(PlaceAsync(...).Succeeded)` in
  *setup*, and a pure service-placement test in the same class passed throughout.
  The helpers now grant the permission, which is honest for fixtures standing in
  for ordinary bookable resources — **and is exactly why every fixture in
  `DirectBookingTests` states it explicitly**, since a granting helper would hide
  a misplaced guard.
- **Two positional `Resource.Create` call sites shifted** when the parameter was
  added — the row mapper and the backoffice mapper — and the compiler caught both
  only because the types differed. Both now pass arguments by name.
- **A landmine corrected rather than fed.** `MultiClaimConcurrencyTests` pins the
  exact migration list and says a new entry means the claim model changed shape.
  That inference is false for this migration, so the comment now says the list is
  a prompt to check, not a proof, and records that this one touches only the
  resources table.
- **Backticks inside a Lit template comment end the template.** Cost one build
  error; noted because the comment in question was explaining an accessibility
  decision and read perfectly well in the editor.
- **8.2 found two, and they were handled differently.** (a) `resources`'
  "Resource definition" enumerates the aggregate's fields, and ⑧ set the precedent
  by extending that sentence when it added capabilities — so this change extends
  it too, as a MODIFIED requirement, guarantee-diffed with nothing dropped.
  (b) `delivery-api`'s scenario "Direct placement is unchanged" says its request
  and response are "exactly as before, carrying a single resource id". **Judged
  not falsified and left alone**: it dates from ⑨-1 and means that service
  placement's collection response did not change the direct endpoint's shape,
  which is still true — a successful direct placement is byte-identical. A new
  *failure* is possible, which this change's own ADDED requirement states. Flagged
  here rather than left silent, because it is the kind of call a reviewer should
  get to overrule.
- **8.3: nothing to change in the specs.** The "everything optional" invariant is
  stated in neither `openspec/specs/` nor `CLAUDE.md` — it lives only in the
  services-model memory, which the handover updates to record it as *narrowed*
  (the direct path survives and became editor-controlled) rather than inverted.
- **Live (7.3, 7.4), over HTTP and anonymously.** A withholding resource is
  refused with `resource-not-directly-bookable` and a message naming it; its read
  model publishes the permission; its bookable-starts still answer (35 starts);
  the **same** resource books through a service with 200; a permitting resource is
  unaffected; and a full update withdraws the permission while preserving opening
  hours. The no-JS flow renders three distinct outcomes — "Not available on its
  own" with no form, the ordinary form, and "No times are available on Sunday 23
  August 2026" — so task 6.2's two messages provably do not collapse.
- **The TestSite dev harness gained `?resourceId=`**, because it always rendered
  the first resource and this change has two outcomes to look at. Test
  infrastructure only.
- **Incidental confirmation of the migration:** all 23 pre-existing resources read
  back as withholding, which is the column default reaching the domain.
- **The `uui-toggle` question, answered with evidence rather than asserted.** The
  control is a native checkbox because the hint must be *associated*, and the
  claim that a uui component would not carry it was tested in the live backoffice:
  `aria-describedby` set on a `uui-toggle` stays on the **host**, and its inner
  `<input>` — the element a screen reader focuses — receives nothing. So the hint
  would have been silent. Reusable finding, of the same family as the existing
  "a `uui-label` with `for` cannot pierce the shadow root" note.
- **Live editor pass (5.6, 7.5).** Native checkbox, label resolving in the same
  shadow root, `aria-describedby` resolving to the hint, no dangling ids anywhere
  in the editor, and the checkbox reflecting the stored value. Set both ways
  through the real control and saved: the API read back `false` then `true`, no
  error summary either time, and a **freshly mounted** list — not a stale
  in-memory one — showed "On its own" against "Service only".
- **The registration flake hit again** and was ruled out the established way
  before working around it: every chunk imports, the manifest exports its 5
  entries, and both custom elements define on import.

## 9. Handover

- [x] 9.1 Record what the pin change inherits: it is the same theme from the other actor's side, and `preferredResourceId`'s silent fall-through is specified behaviour that has to be modified rather than fixed as a bug.
- [x] 9.2 Record what ⑩ inherits: one booking path per resource decided by the editor, and a read model that says which, so the front end can filter without probing.
- [x] 9.3 Record whether the default-withheld choice caused friction in practice, since it is the decision most likely to be revisited and the one with a real cost.

### What follows this change

- **The pin, next, and it is the same theme from the other actor's side.** This
  change gave the *editor* control over which resources may be booked alone; the
  pin gives the *booker* control over which resource they get.
  `preferredResourceId` currently falls through silently to a different resource,
  and that is **specified** behaviour — `service-booking`, scenario "a preferred
  resource id that is eligible but already booked … falls through" — so it must be
  modified deliberately, not fixed as though it were a bug. It is reachable in the
  obvious front-end flow, because service bookable-starts do not say *who* is
  free, and a race falls through rather than reporting a conflict.
- **What ⑩ inherits.** One booking path per resource, decided by the editor, and a
  read model that says which — so the service front end can filter a
  direct-booking UI without probing and being refused. Availability reads stay
  open for withholding resources, which is exactly what ⑩'s "who / any" picker
  needs in order to offer a person who is not standalone-bookable.
- **The default-withheld decision, and what would revisit it.** It cost nothing
  here: nothing is published, and all 23 existing fixtures simply became
  service-only. The friction it buys is the one D2 accepted — a newly created
  resource cannot be booked until someone says so — and the mitigation is the list
  column, which answers "why can nothing book this room?" on the screen where
  rooms are managed. **Trigger to revisit:** a real editor reporting that the
  first-run experience is confusing, not a hypothetical one. If it is ever
  reversed, the safe default is the thing being given up, and the massage case is
  the argument to re-read first.
- **A residue worth knowing.** The TestSite's booking harness now takes
  `?resourceId=`; without it the page renders the first resource by list order,
  which is a withholding one and therefore only ever shows half the behaviour.
