# Tasks — booking on a booker's behalf

Specs: `specs/{bookings,service-booking,booking-management,booking-emails,permissions,sensitive-data}/spec.md`.
Design decisions referenced below as D1–D7.

## 1. Baseline

- [x] 1.1 Confirm no TestSite is running and no orphan holds port 44348, then build Release `--no-incremental` and record the warning count — the baseline is **zero** beyond the accepted NU1903 transitives; a lock failure here is an earlier session's orphan, not a code fault
- [x] 1.2 Run the full suite from a clean build as two steps (`dotnet build`, then `dotnet test --no-build`) and record the four counts, so every later claim about "the suite is green" has a baseline to be measured against
- [x] 1.3 Branch `change/booking-on-behalf` from `main` and push with `-u`

### The guarantee diff of every replaced requirement

Seven requirements are replaced wholesale. Each was extracted **verbatim by script** from
`openspec/specs/`, edited surgically, then `diff`ed back so every removed line is accounted for —
prose diffed for guarantees, not read for sense. Re-verify each before trusting the table; a
measurement nobody re-ran is a claim.

- [x] 1.4 `bookings` — **Availability and placement service ports**: 1 line changed (the service's verb enumeration gains operator placement). 12 → 13 scenarios; every SHALL and every BREAKING note carried forward. Verify no store-port addition crept in — this widening adds none, which is why it carries no BREAKING note of its own
- [x] 1.5 `bookings` — **Booking a single resource requires that resource to permit it**: 4 lines changed, all narrowing "the single-resource entry point" to "the visitor's". 6 → 9 scenarios, 7 → 12 SHALLs. Verify the three original guarantees survive verbatim: the refusal is evaluated in Core before claim composition, service-derived claims are exempt, and the distinction stays structural
- [x] 1.6 `bookings` — **Booking status machine**: 6 lines changed. 6 → 9 scenarios. **This is the one the sibling sweep found**, not the delta review — verify the four transitions and the `Declined`/`Requested` provenance sentence are intact, since only the `AutoConfirm` derivation was meant to move
- [x] 1.7 `booking-emails` — **Which events produce messages, and for whom**: 3 lines changed. 8 → 12 scenarios. Verify the auto-confirm one-message rule, the erased-booker silence, and the unconditional gating sentence all survive — none of them was meant to change
- [x] 1.8 `permissions` — **Access within the section is decided by four verbs**: 1 line changed (Manage's bullet). 8 → 11 scenarios. Verify "exactly four verbs", Manage-implies-Read, and the Settings-implies-nothing sentence are untouched
- [x] 1.9 `permissions` — **Sensitive-data gates are joined by verbs, never replaced**: 2 lines changed. 2 → 4 scenarios. Verify "a verb SHALL never disclose what that group withholds" survives verbatim — it is the sentence the whole requirement exists for
- [x] 1.10 `sensitive-data` — **Withheld data SHALL NOT be reachable by asking about it**: 1 line changed (the exactness obligation is scoped to an endpoint that *matches on* a detail). 4 → 7 scenarios. Verify the gate sentence, the no-partial-form rule, the reveal-nothing rule and the historical note are all intact — this requirement is a security constraint and the narrowing must not have reached them

## 2. Core — the terms (D1)

- [x] 2.1 Add `ApprovalApplies` to `PlacementTerms`, `true` on `Visitor` and `false` on `Operator`, with the rationale in XML docs; verify existing `PlacementTerms` tests still pass unchanged
- [x] 2.2 Add a unit test asserting `PlacementTerms` carries **no** member concerning direct bookability (`bookings`, *…the waiver is not a member of the operator's terms*), so D1's second half is guarded rather than intended
- [x] 2.3 Thread `PlacementTerms` through the private `PlaceAsync` to the status decision, so `Booking.Create` reads `terms.ApprovalApplies && !settings.AutoConfirm`; verify by a test that a visitor placement under `AutoConfirm` off is still `Requested`

## 3. Core — operator placement of a resource (D2, D3, D4)

- [x] 3.1 Add `IBookingService.PlaceOnBehalfAsync(BookingRequest, CancellationToken)` as a **new member**, never by widening `PlaceAsync`; verify the existing `PlaceAsync` signatures are byte-identical to `main` (`git diff` on the interface shows additions only)
- [x] 3.2 Implement it as a sibling of the direct overload — composing its own `MultiClaimBookingRequest` and calling the private pipeline with `PlacementTerms.Operator`, **not** calling `PlaceAsync(BookingRequest)`; verify by a test that a resource withholding `DirectlyBookable` is placeable on behalf and still refused to a visitor
- [x] 3.3 Cover the pipeline scenarios from `bookings`, *Placing a booking on a booker's behalf*: lead time waived, past refused with `lead-time`, horizon waived, and open hours / conflict / duration bounds / granularity each still binding
- [x] 3.4 Cover the status scenarios from `bookings`, *Booking status machine*: an operator's placement is `Confirmed` under `AutoConfirm` both off and on, and a visitor's is unchanged under both
- [x] 3.5 Verify the booker is validated exactly as a visitor's (missing or malformed email refused) and that no member key is set

## 4. Core — operator placement of a service (D2)

- [x] 4.1 Add `IServiceBookingService.PlaceOnBehalfAsync` as a new member, delegating to the booking service's operator placement when a resource is named rather than a service
- [x] 4.2 Apply the service's length rules exactly as `PlaceAsync` does — the intersection of the duration specification with each candidate's range, same stable codes, no substitution of a permitted length; verify with the 45–120 service refused at 30 minutes and at a candidate's 90-minute ceiling
- [x] 4.3 **Verify operator terms are not re-imposed by resolution**: a service whose resources require 24 hours' notice is placeable one hour out. This is the rule that lives one layer up and is the exact defect class `move-booking` shipped — test it through the service entry point, not the booking service's
- [x] 4.4 Verify a visitor's service placement is unchanged: same rules, codes and assignment behaviour, lead time and horizon included

## 5. Emails (D5)

- [x] 5.1 Send `BookerPlaced` and suppress the internal recipients for an operator placement, decided at the placement path rather than by inspecting the booking; verify with a recording mail sender **through the production entry point** that the booker got exactly one message and internal recipients got none
- [x] 5.2 Verify the observer still fires for an operator placement — only the package's internal recipient list is skipped, never the port a host subscribes to. This is the seam D5 names; a guard over each half will stay green through the regression
- [x] 5.3 Verify a visitor's placement still writes to both directions, and that an operator's placement on a site without booker emails enabled is silent and still succeeds

## 6. Management endpoint (D6)

- [x] 6.1 Add the action to `BookingsController`, carrying the `Manage` verb policy **and** `[Authorize(Policy = …SensitiveDataAccessPolicy)]` on the action itself; verify a handler-level check would not satisfy it by confirming the attribute is present on the method
- [x] 6.2 Request model binding exactly one of `serviceId` / `resourceId`; verify both-or-neither is refused before the domain is reached
- [x] 6.3 Reuse the move endpoint's existing zoneless start parsing rather than writing a second one; verify a start carrying `Z` or an offset fails with `interval-invalid` against the start field
- [x] 6.4 Response model carrying id, reference, status and interval and **no booker member**; verify by reflecting over the model, so the guarantee is structural rather than dependent on the caller's access
- [x] 6.5 Verify refusals carry the domain's stable code, and that a malformed booker address is distinguishable from a pipeline refusal
- [x] 6.6 Verify the authorization matrix live: Manage-without-sensitive-data refused, sensitive-data-with-Read-only refused, unauthenticated 401

## 7. The three guards that must be told by hand, and do not fail helpfully

- [x] 7.1 `PermissionsTests` classification map — add the new endpoint; verify the test fails first with the entry absent
- [x] 7.2 `SensitiveDataRedactionTests`: add the action to `recordedActions` as a **write**, add the booker parameters to `recordedContactParameters`, and update the recorded redaction snapshot string. Add a note beside the new entries stating that this endpoint **stores** rather than matches, so the "matches the whole value exactly" obligation is vacuous for it — per D6's second risk
- [x] 7.3 `BackofficeDocumentationTests` capability-summary route map — add the route; verify the test fails first with it absent
- [x] 7.4 Confirm no field was renamed to evade the name-based contact scan: the request model binds a parameter the scan recognises, and it is gated

## 8. Backoffice client (D7)

- [x] 8.1 Rebuild the client **before** starting any site, and rebuild `UBookIt.Backoffice` `--no-incremental` after; a stale hashed bundle name fails `dotnet build` with "No file exists for the asset"
- [x] 8.2 Add the placement modal element and token, following `move-booking-modal.element.ts`: native inputs with real `<label for>`, never `uui-*` for a labelled control
- [x] 8.3 Manage focus explicitly — first field on open, the offending field or error summary after a refusal, the opening control on dismissal — because Umbraco's modal container does not move focus into the content it hosts
- [x] 8.4 Offer the control from the view rather than a row, hidden unless the user's verbs and sensitive-data access permit it; verify the endpoint still refuses independently
- [x] 8.5 "What to book" as one `<select>` with services and resources in two `<optgroup>`s, resources including those withheld from visitors
- [x] 8.6 Show the reference on success; show a refusal in place **keeping the booker's details the operator typed**
- [x] 8.7 State that the package writes to the person who booked only where booking emails are configured
- [x] 8.8 Where the placed booking falls outside the list's window, say so rather than leaving the table unchanged; verify by placing next month's booking on a screen showing this week
- [x] 8.9 Client unit tests for the row/modal logic, and verify the client test count rises

## 9. Documentation

- [x] 9.1 `docs/notifications.md`: the operator-placement case — the booker is written to, the internal recipients are not
- [x] 9.2 **Discharge the deferred obligation** this change's touch of `notifications` brings due: "Confirming or declining sends this list nothing" appears **twice** in that document, so its `DocumentationAssert.Says` pin at `NotificationDocumentationTests.cs:233` pins nothing. Introduce `SaysOnce` and paraphrase one occurrence; verify by deleting the table row and confirming the guard now fails
- [x] 9.3 `docs/configuration.md`: the two port additions, beside `move`'s three
- [x] 9.4 Document that there is no availability picker in this view, as the move requirement's counterpart does

## 10. Verification

- [x] 10.1 `openspec validate --all --strict` passes
- [x] 10.2 Release build `--no-incremental` with zero warnings beyond the accepted transitives
- [x] 10.3 Full suite green from a clean build, run as two steps; record all four counts and the delta from 1.2
- [x] 10.4 **Live verification in the running TestSite**: place a booking on a booker's behalf for a service, and confirm in the browser that it appears, carries a reference, is `Confirmed` on a site with `AutoConfirm` off, and that the service's length rules refuse an out-of-range length. The live probe is what caught `move-booking`'s MAJOR that 1644 unit tests did not
- [x] 10.5 Live-verify the keyboard path through the modal: focus lands inside on open, an error is announced in association with its control, focus returns to the opening control on dismissal
- [x] 10.6 Stop the TestSite and confirm no orphan holds port 44348

## 11. Handover

- [ ] 11.1 Write the QA handover into this file: what was built, what is claimed, the build and test state, and **the explicit instruction to verify rather than trust** — twice the reviewer has found a claim in the handover itself to be false
- [ ] 11.2 Name for the reviewer the four places a defect is most likely: the seam at 5.2, the layer-above rule at 4.3, the structural waiver at 3.2, and the guard notes at 7.2
- [x] 11.3 Record the deferred obligation this change creates — narrowing `sensitive-data`'s input rule to distinguish a query term from a stored value — in the deferred-obligations memory, with the reason it was not attempted here
