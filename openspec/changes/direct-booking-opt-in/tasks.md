## 1. Core — the permission

- [ ] 1.1 Add to `Resource` whether it may be booked on its own, defaulting to withheld. Not part of eligibility, not part of validation — `Resource.Create` must accept either answer and reject neither.
- [ ] 1.2 Confirm the resolution chain is untouched: type → capabilities → duration, with no fourth term. A test that a withholding resource still appears in a candidate pool, since this is the guarantee the whole change rests on.
- [ ] 1.3 Rehydration carries it, like every other stored scalar. `Booking.Rehydrate`'s neighbourhood is the model for what "round-trips" has to mean here.

## 2. Core — the guard

- [ ] 2.1 Refuse in `IBookingService.PlaceAsync(BookingRequest)`, **before** it composes the `MultiClaimBookingRequest` and delegates. New stable code `resource-not-directly-bookable`.
- [ ] 2.2 **The mutation that matters, and the one an obvious test misses**: move the guard down into the multi-claim overload and confirm service placement breaks. A test asserting only "a withholding resource cannot be booked directly" passes that implementation while every service booking is broken. The covering test MUST book **the same withholding resource both ways** and assert opposite outcomes.
- [ ] 2.3 A single-role service of count 1 over a withholding resource places successfully. This is the case most likely to be "fixed" into a refusal by someone who reads the rule as being about claim-set size rather than about which entry point was used.
- [ ] 2.4 The refusal precedes the rule pipeline: a withholding resource asked for a time outside its open hours reports `resource-not-directly-bookable`, not `outside-open-hours`.
- [ ] 2.5 Nothing is persisted on refusal, and the code is never `conflict` — it cannot come good on a retry.
- [ ] 2.6 Confirm the ⑨-2 all-fail classification is untouched: service placement never reaches this code, so it must not appear in the deterministic-refusal whitelist. Assert the whitelist is unchanged.

## 3. Persistence

- [ ] 3.1 One additive, non-nullable column with a default. Existing rows take the default, which is the behaviour change the proposal states.
- [ ] 3.2 The migration is additive; no destructive schema change and no upgrade path needed, since nothing is published.
- [ ] 3.3 Round-trip through the store, and confirm the value survives a full update rather than being silently preserved from the prior row — full-replacement semantics, as the capability set has.

## 4. Delivery API

- [ ] 4.1 Publish it on the resource read model, on both `GET /resources` and `GET /resources/{id}`, as a value and never by omission.
- [ ] 4.2 `POST /bookings` maps the new code to 400 through the existing catch-all rule. Assert the status rather than assuming it — the mapping requirement is not being modified, so this is the only thing holding it.
- [ ] 4.3 Availability reads answer identically for a withholding resource: free-time, slots and bookable-starts. Assert this directly; it is the counter-intuitive half and the one most likely to be "tidied" later.
- [ ] 4.4 `POST /services/{id}/bookings` over a withholding resource succeeds, end to end over HTTP rather than only in Core.
- [ ] 4.5 Confirm no other delivery contract moved — the OpenAPI diff should be one added member and nothing else.

## 5. Management API and backoffice

- [ ] 5.1 Carry it on create, read and update. An omitted value withholds, consistent with the domain default and with the capability collection's treatment.
- [ ] 5.2 A full update can withdraw it — full-replacement semantics, not merge.
- [ ] 5.3 The resource editor gains a control that states what withholding *means*: still bookable as part of a service, not bookable alone. A label reading only "bookable" is wrong and will be read as "can be booked at all".
- [ ] 5.4 The resources list shows it, because with the default withheld the editor's question is "why can nothing book this room?" and the list is where they will look.
- [ ] 5.5 Neither answer blocks a save, in either direction.
- [ ] 5.6 Accessibility: the control is labelled in the same shadow root, its explanatory text is associated rather than merely adjacent, and the list column is not conveyed by colour or icon alone. **Read the rendered shadow DOM, not a screenshot**, and check the value both ways.
- [ ] 5.7 Regenerate the client against the running TestSite and check the diff for unrelated drift.

## 6. Default front end

- [ ] 6.1 Render the "not offered on its own" statement for a withholding resource, and offer no submission.
- [ ] 6.2 A resource that permits direct booking but has no bookable times still reports no available times. **Test both, or the two collapse into one message** — this is the ⑨-1a failure mode arriving through a new door.
- [ ] 6.3 The permitted flow is unchanged end to end: length choice, anti-forgery, the 303 redirect, the confirmation, and failure handling with input preservation.
- [ ] 6.4 The statement meets the flow's existing WCAG 2.2 AA baseline and is announced on the same terms as its other outcome messages.

## 7. Verification

- [ ] 7.1 Full solution build with `--no-incremental`, **TestSite stopped first**. Only the known NU1903 advisories.
- [ ] 7.2 Unit, integration and client suites green, including every ⑤–⑨ scenario unchanged. The ⑨ suites are the guard for task 2.2 having been done right.
- [ ] 7.3 Live: a withholding resource refuses direct placement over HTTP and succeeds through a service that resolves to it.
- [ ] 7.4 Live: the no-JS flow renders the statement for a withholding resource and the ordinary flow for a permitting one.
- [ ] 7.5 Live: the backoffice sets it both ways and the list reflects it.
- [ ] 7.6 Stop the TestSite and check port 44348 for orphans. Umbraco 17 intermittently renders the backoffice shell without registering package extensions; a fresh navigation or a fresh login clears it, and importing the bundle to check `customElements.get(...)` is what distinguishes that flake from a real fault.

## 8. Spec hygiene

- [ ] 8.1 Guarantee diff for the one MODIFIED requirement, `Resource read model`. Only one sentence is extended in place; confirm nothing else moved. **A diff reporting nothing is as suspect as one reporting everything** — prove the tool can find a deletion before believing it.
- [ ] 8.2 The outward grep for sibling specs this change falsifies, **before and after** the sync. Grep the vocabulary of the *mechanisms*, not only of the change: "bookable", "book a resource", "single resource", "directly", "read model carries", "no times available", "unavailable". Every change but one has found something here.
- [ ] 8.3 Confirm the "everything optional" invariant is recorded as **narrowed, not inverted** — services remain opt-in sugar and the direct path survives; what changed is that it became editor-controlled. Update `openspec/specs` prose only if some requirement actually states the old form.

## 9. Handover

- [ ] 9.1 Record what the pin change inherits: it is the same theme from the other actor's side, and `preferredResourceId`'s silent fall-through is specified behaviour that has to be modified rather than fixed as a bug.
- [ ] 9.2 Record what ⑩ inherits: one booking path per resource decided by the editor, and a read model that says which, so the front end can filter without probing.
- [ ] 9.3 Record whether the default-withheld choice caused friction in practice, since it is the decision most likely to be revisited and the one with a real cost.
