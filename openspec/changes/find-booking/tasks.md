# Tasks — find a booking

Specs: `specs/booking-management/spec.md`. Design decisions referenced as D1–D6.

## 1. Baseline

- [x] 1.1 Confirm no TestSite holds port 44348, then build Release `--no-incremental` and record the warning count — the baseline is **zero**
- [x] 1.2 Full suite from a clean build as two steps (`dotnet build`, then `dotnet test --no-build`) and record all four counts — the baseline is 1706 / 149 / 1086 / 235
- [x] 1.3 Branch `change/find-booking` from `main` and push with `-u`

### The guarantee diff of every replaced requirement

Two requirements are replaced wholesale. Each was extracted **verbatim by script**, edited
surgically, then `diff`ed back. Re-verify before trusting; a measurement nobody re-ran is a claim.

- [x] 1.4 `booking-management` — **The list is windowed, and the window is bounded**: 2 lines removed, both the sentence being corrected ("…from a booker's name or reference is a different query… and is not provided by it") — the name clause is kept, the reference clause replaced by a paragraph mirroring the email one. 6 → 7 scenarios. Verify the window's non-optionality, its bound, the whole-day count, the backwards-window refusal and all six original scenarios survive verbatim
- [x] 1.5 `booking-management` — **A subject's bookings can be found by their email address**: **zero lines removed**, purely additive (purpose widened to operator lookup; the view SHALL reach it). 9 → 10 scenarios. Verify the gate, exactness, unwindowed, paged, indexed and erased-not-found guarantees are byte-identical

## 2. Core — the read (D1)

- [x] 2.1 Add `IBookingManagementStore.FindByReferenceAsync(BookingReference, CancellationToken)` returning `BookingSummary?`, with the port-break remark; verify the existing members are byte-identical to `main`
- [x] 2.2 Add `FailureCodes.ReferenceInvalid = "reference-invalid"` with remarks on why it is distinct from `booking-not-found`
- [x] 2.3 Update every `IBookingManagementStore` double in the test suite; verify by building

## 3. Persistence — the seek

- [x] 3.1 Implement `FindByReferenceAsync` in `SqlBookingManagementStore` as an equality on the indexed `Reference` column, composing the row through the **same** summary projection the list uses; verify by an integration test that a found row equals the list's row for the same booking
- [x] 3.2 Integration test: the emitted SQL is an equality on `Reference` and never a `LIKE` — the same shape `FindByBookerStoreTests` uses for the address
- [x] 3.3 Integration tests for the spec scenarios: lower case and with/without separator find the same booking; a cancelled booking is found; an erased booking is found and shows erased; a fragment finds nothing

## 4. Management endpoint (D2)

- [x] 4.1 `GET bookings/by-reference/{reference}` on `BookingsController`, gated on `Constants.VerbPolicies.BookingsRead`, parsing via `BookingReference.TryParse`; 400 `reference-invalid` / 404 `booking-not-found` / 200 with the list's `BookingModel` through the list's mapper
- [x] 4.2 Endpoint tests, driven through the controller with a real store: found row withheld for a caller without sensitive-data access and shown for one with it — the *same* withholding path the list uses, not a second one; malformed vs missing distinguishable by code
- [x] 4.3 Verify by reflection that the action carries the Read policy and **not** the sensitive-data policy, with the reason in the test

## 5. The three guards that must be told by hand

- [x] 5.1 `PermissionsTests` classification map — `BookingsController.FindBookingByReference = UBookItBookingsRead`; verify it fails first with the entry absent
- [x] 5.2 `SensitiveDataRedactionTests` — record the action as a **read** in `recordedActions`. **Do NOT add `reference` to `recordedContactParameters`**: it is not a contact detail, and recording it as one would teach the next reader that it needs the sensitive-data gate. Say so in a note beside the entry
- [x] 5.3 `BackofficeDocumentationTests` route map — `["GET bookings/by-reference/{reference}"] = "find a booking by its reference"`; and edit the `booking-management` Purpose paragraph directly so the verb list names it; verify the guard fails first
- [x] 5.4 `booker-erasure/spec.md`'s HTML comment cites the "not provided" sentence as the reason a scenario was reworded; edit the comment directly (it is not a requirement) so it no longer describes a lookup the package now has

## 6. Backoffice client (D3–D6)

- [ ] 6.1 Rebuild the client before starting the TestSite; start it; regenerate the API client for the new endpoint; stop the site before rebuilding
- [ ] 6.2 `find-fields.ts`: pure functions — `classify(text)` → reference | email | neither; `looksLikeReference` as a port of `TryParse`'s rule; `refusalTerm` map for `reference-invalid`, `booking-not-found` and the find-by-email codes; the status-line term for each mode. Tests for each
- [ ] 6.3 **Assert the shape rule agrees with the parser** (spec scenario): the client test reads a fixture of accept/reject vectors that the C# suite writes from `BookingReference.TryParse`, or — if that plumbing is disproportionate — the client test quotes the alphabet and a server-side guard asserts the two constants are identical. Either way, state which in the handover
- [ ] 6.4 The Find control in `bookings-list.element.ts`: one labelled input, label narrowed for users without sensitive-data access (read from the same current-user context ㊳ uses, **outside** `#bookerCell`); one Find button; an email typed by a non-holder refused in place with the sentence naming the group
- [ ] 6.5 The mode: `window` | `reference` | `email`; lookup modes hide the window and filter controls, render a `role="status"` line with what is shown and a **Back to dates** control, and render rows through the existing `#renderRow`
- [ ] 6.6 A miss renders the status line only — no table (D6)
- [ ] 6.7 Make `#settleAfterRowAction` mode-aware: in a lookup mode it re-runs the lookup, not the window. Client tests for the decision per mode
- [ ] 6.8 Back to dates restores the window view and places focus on the From input; verify the focus target exists by id
- [ ] 6.9 Client unit tests; verify the count rises from 235

## 7. Documentation

- [ ] 7.1 `docs/backoffice.md`: the Find control — what it accepts, who sees the email route, what happens on a miss, Back to dates
- [ ] 7.2 `docs/configuration.md`: the port addition beside the previous ones
- [ ] 7.3 Sweep every `.cs` file this change touches for a doc block attached to the wrong member — `/// </remarks>` or `/// </summary>` followed by `/// <summary>`, across blank lines and attributes. ㊳ shipped two and a BREAKING note went missing

## 8. Verification

- [ ] 8.1 `openspec validate --all --strict` passes
- [ ] 8.2 Release build `--no-incremental`, zero warnings
- [ ] 8.3 Full suite green from a clean build, two steps; record all four counts and the deltas from 1.2
- [ ] 8.4 **Live**: find `BJQ4-ZP5C` (a ㊳ residue booking on 18 Sep) from a window showing another week — it appears alone under the status line; find it in lower case with the separator; find `behalf.probe@example.com` — its bookings, all dates; type `not-a-thing` — refused in place; type an email as a user without the group — told why. Then **move the found booking to a date outside the original window and confirm it is still shown** (the D5 seam)
- [ ] 8.5 Live keyboard path: Find input labelled and reachable; an error associated with it; Back to dates puts focus on the From input — measured with `document.activeElement`, not eyeballed
- [ ] 8.6 Stop the TestSite and confirm port 44348 is free

## 9. Handover

- [ ] 9.1 Write the QA handover into this file: what was built, what is claimed, build and test state, and the instruction to **verify rather than trust** — three claims in ㊳'s handover were false and QA found all three
- [ ] 9.2 Name for the reviewer where a defect is most likely: the mode-aware refetch (6.7), the shape-rule/parser agreement (6.3), the "same withholding path" claim (4.2), and the contact-parameter guard note (5.2)
