## 1. Read port

- [x] 1.1 Add a lookup to `IBookingManagementStore` taking an email address plus paging, returning `BookingPage`. Document it as unwindowed and exact-match, and say why it is separate from `ListAsync` rather than a filter on it.
- [x] 1.2 Introduce whatever query type it needs by the same discipline as `BookingQuery` — if there is an invalid state, make it unrepresentable in the constructor rather than validated in a service.
- [x] 1.3 **BREAKING — published port.** Declare the `IBookingManagementStore` addition in the proposal and the delta, as `booker-erasure` did for `IBookingStore`.

## 2. Persistence

- [x] 2.1 Add a non-unique index on `BookerEmail` in `UBookItDbContext`.
- [x] 2.2 One additive migration. Check the generated SQL is `CREATE INDEX` only — no column change, no data statement.
- [x] 2.3 Regenerate the model snapshot; confirm the diff is the index and nothing else.
- [x] 2.4 Implement the lookup in `SqlBookingManagementStore`, reusing the existing projection so the row shape cannot drift from the list's.
- [x] 2.5 Confirm the query actually uses the index rather than scanning — read the plan or the generated SQL, do not assume it from the index existing.
- [x] 2.6 **Re-check the availability path's plan.** Done — the claims-read suite (5 tests, including the half-open overlap and the batched multi-resource read) passes unchanged with the new index present. The index is on `BookerEmail`, which no availability predicate references. A new index changes the optimiser's options; `persistence` has a standing QA gate that the date-range lookup must not table-scan.

## 3. Endpoint

- [x] 3.1 Add the search to `BookingsController`, carrying `[Authorize(Policy = Constants.SensitiveDataAccessPolicy)]` — the same policy the erase endpoint uses, which already composes the section requirement.
- [x] 3.2 Reuse `PagedBookingsModel` and the three-state booker; add no new row shape.
- [x] 3.3 **POST with a body.** An address in a query string is written to the web server's log, every proxy's log and the operator's browser history — none of which this endpoint's gate controls, and a gate that holds for the response while the request scatters the value is not much of a gate. The cost is that a read is not cacheable or bookmarkable, neither of which is wanted for a search for a person. Recorded on the request model, in the endpoint's docs, and pinned by a test. Original note: An address in a URL lands in server logs, proxy logs and browser history, which is a disclosure the endpoint's own gate does not control — that argues for POST despite it being a read.
- [x] 3.4 Validate the input as an address before querying, reusing the domain's rule rather than a second one.
- [x] 3.5 Confirm the list endpoint gains no contact-detail parameter and its window stays required.

## 4. Verification

- [x] 4.1 Store tests: two bookings for one address both returned; a different address returns empty; matching is exact; results page; unwindowed — a booking far outside any acceptable list window is still found.
- [x] 4.2 An **erased** booking is not returned by a search for the address it used to hold.
- [x] 4.3 Endpoint tests. **The 401 and the "refusal is identical" scenarios are established STRUCTURALLY, not measured** — the policy denies before the action, so no query runs and no branch differs on whether a match exists. That is an argument, and it is recorded as one rather than ticked as though a test observed it; what IS tested is the mechanism, in both directions. (The gate is a POLICY, so it is asserted against the attribute and the composed policy rather than by an in-process call that never meets it — see `EraseBookerEndpointTests` and `UBookItSensitiveDataAccessTests` for the composition and the handler): succeeds with the group; refused with section access alone; 401 anonymous; refusal is identical whether or not a match exists.
- [x] 4.4 **Mutation-check the gate** — done, two ways: removing `[Authorize]` from the search fails the endpoint test AND the capability tripwire; adding an ungated `bookerEmail` parameter to the ordinary list fails the tripwire by name. Remove `HasAccessToSensitiveData` from the handler and confirm the handler tests fail (they already exist).
- [x] 4.5 **Mutation-check exactness**: make the store match with `Contains` or `StartsWith` and confirm a test fails. This is the guarantee most likely to be widened later by somebody being helpful.
- [x] 4.6 Guard that the endpoint's parameters offer no ordering by, or count of, a contact detail.
- [x] 4.7 Integration test against real SQL Server for the index migration applying to a database holding existing bookings.
- [x] 4.8 **Every test written to close a review finding gets mutation-checked before it is believed.** Five unfalsifiable tests were written on `booker-erasure`, two of them while fixing the other three.

## 5. Modified requirements — the guarantee diff

A `## MODIFIED Requirements` entry replaces body *and* scenarios; anything not restated is deleted
with nothing in the diff resembling a deletion. **Establish where each requirement ENDS before
diffing it** — reading to the end of a too-short window is precisely how `booker-erasure` dropped
four scenarios while asserting it had dropped none.

- [x] 5.1 **`sensitive-data` → "Withheld data SHALL NOT be reachable by asking about it".** THE REOPENING, signed off by Chris on 2026-09-05. Carried forward: the reason in full (a facility answering questions about values the caller was not given; confirming an address by observing whether a row comes back; enumerating candidates the same way); the "stated rather than left as an accident" clause and its tripwire purpose; and the scenario *A withheld booking still appears in results* verbatim. **Superseded, not dropped:** the blanket "no filter, search, sort or count" becomes a prohibition on answering to a caller who **may not read** the values, plus three new obligations — sensitive-data access as the endpoint's own authorization, exact match only, and no information leaking through a count, error or timing difference. The scenario *No filter over contact details is offered* keeps its **name** while its content narrows, deliberately: renaming a scenario inside a MODIFIED block reads as a deletion to the tooling and to a reader.
- [x] 5.2 **`booking-management` → "The list is windowed, and the window is bounded".** Carried forward: every clause about the window being required and bounded, the distinct failure codes, and all its scenarios. Changed: the sentence saying locating a booking from an email "is not provided here" now says it is provided by a separate read, and states that this requirement's window is not to be relaxed to accommodate it. **Nothing dropped.**
- [x] 5.3 **`persistence` → "Schema shape and naming".** Carried forward: every table, every column, the capability uniqueness rules, the canonical uniquely-indexed reference, the no-FK service columns, the booker-column nullability and erasure semantics, and every scenario. Added: the non-unique email index and why it is not unique. **Nothing dropped.**
- [x] 5.5 **`booking-management` → "Results are paged in a stable order".** Found by the sweep at 6.3, not by review — its opening sentence scoped the total to "the window and filters", and the by-address read has no window. Carried forward: every clause about the total, the start-then-identity ordering, why the tiebreak is required, that a total order is not a specified id sequence, and the bounded page size; all scenarios. Changed: the requirement reaches every paged read this capability offers, and the page bound is said to be the same across reads as well as across capabilities. **Nothing dropped** — the substance already held for the new read; the wording would have gone stale.
- [x] 5.6 **`booker-erasure` → "What erasure does not reach is documented".** Found by QA, not by my sweep — which is the point. Its first boundary said "The package does not claim to find them all", true when erasure shipped with no way to find anything and made false by this change. Carried forward: both boundaries, the future-booking permission and its reasoning, the closing sentence, and both scenarios. Changed: the first boundary now points at the search and states what the search does NOT reach (one address, so two addresses means one set found). **Nothing dropped** — the limit moved rather than disappearing.
      *§6.3 swept the three capabilities this change MODIFIES. It did not sweep `booker-erasure`, whose documented boundary this change exists to remove — the one capability most certain to be affected. "One per capability the change touches" was the wrong rule; it is one per capability the change makes a difference to, and those are not the same set.*
- [x] 5.4 Diff each one against `openspec/specs/` **as it stands on `main`**, not against memory of it.

## 6. Documentation and close-out

- [x] 6.1 `docs/backoffice.md`: the erasure section states that a person's bookings can now be found by address, that matching is exact, that it needs the same group, and **that it finds bookings made with *that* address** — somebody who booked under two addresses has one found.
- [x] 6.2 State that an erased booking cannot be found by the address it used to hold, so a completed erasure is not verifiable by searching for the person again.
- [x] 6.3 **Sibling-spec sweep, one per capability this change touches** — and it found one: `booking-management`'s *Results are paged in a stable order* scoped its total to "the window and filters", which the unwindowed read would have made stale. Widened deliberately (task 5.5). `sensitive-data`'s and `persistence`'s untouched requirements hold; `default-frontend` and `delivery-api` mentions are all placement-time. Original instruction: — `sensitive-data`, `booking-management`, `persistence` — *including the untouched requirements of each.* On `booker-erasure` this same lesson landed three times, once per capability, because the sweep looked outward at siblings and never at the capability being edited.
- [x] 6.4 Re-enumerate the delivery API by inspection and confirm it gained nothing.
- [x] 6.5 Clean build from a clean tree; zero warnings; .NET and client suites green.
- [x] 6.6 `openspec validate find-by-booker --strict` on **both** the pinned CLI and `@1.12.0` — 1.12.0 refuses a scenario renamed inside a MODIFIED block, and it refused this change once already.
