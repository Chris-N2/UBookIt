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

- [x] 6.1 Rebuild the client before starting the TestSite; start it; regenerate the API client for the new endpoint; stop the site before rebuilding
- [x] 6.2 `find-fields.ts`: pure functions — `classify(text)` → reference | email | neither; `looksLikeReference` as a port of `TryParse`'s rule; `refusalTerm` map for `reference-invalid`, `booking-not-found` and the find-by-email codes; the status-line term for each mode. Tests for each
- [x] 6.3 **Assert the shape rule agrees with the parser** (spec scenario): the client test reads a fixture of accept/reject vectors that the C# suite writes from `BookingReference.TryParse`, or — if that plumbing is disproportionate — the client test quotes the alphabet and a server-side guard asserts the two constants are identical. Either way, state which in the handover
- [x] 6.4 The Find control in `bookings-list.element.ts`: one labelled input, label narrowed for users without sensitive-data access (read from the same current-user context ㊳ uses, **outside** `#bookerCell`); one Find button; an email typed by a non-holder refused in place with the sentence naming the group
- [x] 6.5 The mode: `window` | `reference` | `email`; lookup modes hide the window and filter controls, render a `role="status"` line with what is shown and a **Back to dates** control, and render rows through the existing `#renderRow`
- [x] 6.6 A miss renders the status line only — no table (D6)
- [x] 6.7 Make `#settleAfterRowAction` mode-aware: in a lookup mode it re-runs the lookup, not the window. Client tests for the decision per mode
- [x] 6.8 Back to dates restores the window view and places focus on the From input; verify the focus target exists by id
- [x] 6.9 Client unit tests; verify the count rises from 235

## 7. Documentation

- [x] 7.1 `docs/backoffice.md`: the Find control — what it accepts, who sees the email route, what happens on a miss, Back to dates
- [x] 7.2 `docs/configuration.md`: the port addition beside the previous ones
- [x] 7.3 Sweep every `.cs` file this change touches for a doc block attached to the wrong member — `/// </remarks>` or `/// </summary>` followed by `/// <summary>`, across blank lines and attributes. ㊳ shipped two and a BREAKING note went missing

## 8. Verification

- [x] 8.1 `openspec validate --all --strict` passes
- [x] 8.2 Release build `--no-incremental`, zero warnings
- [x] 8.3 Full suite green from a clean build, two steps; record all four counts and the deltas from 1.2
- [ ] 8.4 **Live**: find `BJQ4-ZP5C` (a ㊳ residue booking on 18 Sep) from a window showing another week — it appears alone under the status line; find it in lower case with the separator; find `behalf.probe@example.com` — its bookings, all dates; type `not-a-thing` — refused in place; type an email as a user without the group — told why. Then **move the found booking to a date outside the original window and confirm it is still shown** (the D5 seam)
  - Verified live 2026-09-18: miss renders as the sentence "No booking has the reference ZZZZ-2222."
    with **Back to dates** and no table; `not-a-thing` refused in place with the window untouched;
    `behalf.probe@example.com` returned `BJQ4-ZP5C` dated **Oct 15 2026** under a window showing
    14–20 Sep, which is the D5 seam proved from the email side as well as the reference side.
  - **STILL OWED**: "type an email as a user without the group — told why" needs a second sign-in
    (Perm Tester, who lacks Sensitive data). It is the only live check outstanding.
- [x] 8.5 Live keyboard path: Find input labelled and reachable; an error associated with it; Back to dates puts focus on the From input — measured with `document.activeElement`, not eyeballed
- [x] 8.6 Stop the TestSite and confirm port 44348 is free

## 9. Handover

- [x] 9.1 Write the QA handover into this file: what was built, what is claimed, build and test state, and the instruction to **verify rather than trust** — three claims in ㊳'s handover were false and QA found all three
- [x] 9.2 Name for the reviewer where a defect is most likely: the mode-aware refetch (6.7), the shape-rule/parser agreement (6.3), the "same withholding path" claim (4.2), and the contact-parameter guard note (5.2)

## 10. QA handover

**Verify rather than trust.** Three claims in ㊳'s handover were false and QA found all three;
every number and every claim below is a claim until you re-run it.

**STOPPED BEFORE QA, DELIBERATELY.** Chris asked for the session to pause at the QA point
(token budget, late evening). This handover is written; no reviewer has been spawned. The two
live tasks — 8.4 and 8.5 — are **NOT done**, because they need Chris logged in to the backoffice.
They are unticked above, and nothing below claims them.

### State as handed over

| | |
|---|---|
| Branch | `change/find-booking`, merge-base `4106a73` (the proposal commit on `main`) |
| Release build | `--no-incremental` **0 warnings / 0 errors** — but see the note below |
| Unit | 1716 (baseline 1706; +9 endpoint tests, +1 alphabet guard) |
| Integration | 158 (baseline 149; +9) · Rendering 1086 (unchanged) · Client 277 (baseline 235; +42) |
| `openspec validate --all --strict` | 22/22 |
| TestSite | stopped; port 44348 confirmed free after the client regeneration |

**A `--no-incremental` build failed ONCE with "1 Error(s)" and I did not capture the error line**
(my filter matched only `error CS`). The immediate incremental rebuild and a second
`--no-incremental` rebuild were both clean. I believe it was the client bundle being emptied
mid-build or a lingering site process, but that is a belief: run the clean build yourself and
treat a second occurrence as real.

### The four places a defect is most likely

1. **The mode-aware reload (D5, §6.7).** `#load` now dispatches through `#fetch()` on `_mode`;
   `#settleAfterRowAction` is unchanged and simply calls `#load`, so "the lookup re-runs, not the
   window" is a property of that dispatch, not a separate branch. The client test covers
   `reloadsLookup` as a pure decision; **no test drives the element through a row action in a
   lookup mode** (no DOM environment), and the live check that would (8.4's move-then-still-shown)
   was not run. This is the seam.
2. **The shape rule's agreement with the parser (§6.3).** Done as the design's *fallback*: the
   TypeScript quotes the alphabet, `BookingReferenceAlphabetTests` asserts the quoted constant and
   length equal `BookingReference.Alphabet`/`.Length`, and the client test runs the same
   accept/reject vectors the C# suite uses. The RULE is ported by hand; only the alphabet is held
   equal mechanically. A divergence in the rule (e.g. what counts as a separator) would not be
   caught by the guard.
3. **"Same withholding path" (§4.2).** Proven behaviourally with a real Umbraco user in and out of
   the group, and by the row-composition file guard (`BookingsController.cs` is now a recorded
   referencer of `BookingModel`). Attack the recording: it was forced by a `ProducesResponseType`
   attribute, and the guard's own comment says it strips comments precisely so prose cannot
   satisfy it — check that an attribute cannot either, i.e. that the mapper call is what actually
   composes the row.
4. **The contact-parameter guard note (§5.2).** `reference` is deliberately NOT in
   `recordedContactParameters`. The scan is name-based and does not match it; the note says that
   is by design. Confirm the reasoning survives a hostile read: a reference is quotable, not a
   contact detail, and the response withholds by the list's rule.

### Claims worth attacking

- **The window requirement's narrowing** — the reference clause replaced by a paragraph
  mirroring the email one. 2 lines removed, 6 → 7 scenarios. Re-do the diff.
- **`find-by-booker`'s purpose widened with zero guarantees changed** — 0 lines removed,
  9 → 10 scenarios. Re-do the diff; the claim is that a widened *purpose* cannot weaken a read.
- **404 → miss, everything else → error** in `#fetch`'s reference branch. A 401/403 from the
  by-reference endpoint would surface as `_findError` via the generic term and drop the view
  back to window mode. Is that the right behaviour for a session that has expired mid-lookup?
- **The Find control's `aria-describedby` is `nothing` until an error exists.** Correct per the
  other dialogs' pattern, but check the attribute is actually removed rather than rendered as
  the string "nothing".
- **A row action in email mode after paging** — `_skip` is preserved through `#load`, and
  `skipAfterEmptyPage` steps back if the page empties. Untested for the email read specifically.

### Not done, and why

- **8.4, 8.5 live verification** — needs Chris logged in. Everything the live probe would
  exercise is described in the task; none of it has been seen in a browser.
- **The `email-invalid` refusal term** is mapped, but the find-by-email endpoint refuses a
  malformed address before any query, and the client's `looksLikeEmail` is looser than the
  server's — so it is reachable and untested live.

### Recorded for the next session

`git push` from the **PowerShell** tool hangs on a GitHub account-chooser prompt (Chris has two
accounts); the **Bash** tool pushes straight through. Every push this session that worked was
from Bash.
