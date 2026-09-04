## 0. Wholesale replacements — diff the guarantees, not the prose

Each entry under `## MODIFIED Requirements` **replaces its requirement whole**, deleting
anything it forgets to restate, with nothing in the diff that looks like a deletion. Both are
listed here so the re-diff has somewhere a reader will look.

- [x] 0.1 "Bookings are readable over an authorized management endpoint" (booking-management) — 9 SHALL-level guarantees and 5 scenarios enumerated against `openspec/specs/`; all carried forward. One reworded rather than copied: the response previously promised "exactly what the read port supplies", which withholding makes false as written, so it now reads *what the port supplies, less what the caller may not see, and never more*. Nothing dropped.
- [x] 0.2 "Bookings have a backoffice collection view" (booking-management) — 8 guarantees and 3 scenarios enumerated; all carried forward, with the booker column's wording widened to cover a withheld value. Nothing dropped.
- [x] 0.3 Identification by reference added as a NEW requirement rather than by replacing "The bookings view can cancel a booking" — that requirement has 7 scenarios and says nothing about the booker, so replacing it wholesale would risk them to say something it does not cover.

## 1. Contract and mapping

- [x] 1.1 Add `BookerModel` (`Name`, `Email`) to `src/UBookIt.Backoffice/Models/BookingModels.cs`, documenting that a null `Booker` on a row means the details were withheld from the caller and never that the booking has none.
- [x] 1.2 Replace `BookingModel.BookerName` and `BookingModel.BookerEmail` with `BookerModel? Booker`. Mark the breaking change in the XML doc as the `Service` member's precedent is marked.
- [x] 1.3 Add `BookerVisibility` (`Shown` / `Withheld`) to `src/UBookIt.Backoffice/Models/`, so a call site cannot read the decision backwards.
- [x] 1.4 Change `BookingModelMapper.ToModel` to take `BookerVisibility` as a required second argument and to emit `Booker = null` for `Withheld`. Update the mapper's class comment: withholding is not "computing a field", it is declining to carry one, and the no-derivation rule still holds.

## 2. Endpoint

- [x] 2.1 Inject `IBackOfficeSecurityAccessor` into `BookingsController` and resolve the current user's sensitive-data access via `IUser.HasAccessToSensitiveData()`.
- [x] 2.2 Withhold when the current user cannot be resolved (design D4), with a comment saying why the unreachable branch is written rather than assumed away.
- [x] 2.3 Pass the resulting `BookerVisibility` through to `ToModel` in `ListBookings`. Confirm no other action composes a `BookingModel`.

## 3. Backoffice client

- [x] 3.1 Regenerate `src/UBookIt.Backoffice/Client/src/api/` from the changed contract and confirm `bookerName`/`bookerEmail` are gone from `types.gen.ts`.
- [x] 3.2 Add `ubookitBookings_bookerHidden` and `ubookitBookings_bookerHiddenNote` to `src/UBookIt.Backoffice/Client/src/localization/en-us.ts` using the approved copy from design D8.
- [x] 3.3 Render the booker cell in `bookings-list.element.ts` from `booking.booker`: name over email when present, the `bookerHidden` term when null. Do not query the current-user context (design D5).
- [x] 3.4 Show `bookerHiddenNote` once above the table when any row on the page has a null booker, exposed to assistive technology on the same terms as the view's existing failure message, and not shown when no row is withheld.
- [x] 3.5 Change the per-row cancel control's accessible name and the cancellation confirmation content to identify the booking by its **reference**, unconditionally — including the localization entry that currently interpolates the booker's name.
- [x] 3.6 Update `booking-rows.ts` and its tests for the new row shape.

## 4. Guards and tests

- [x] 4.1 Extend `BookingsEndpointTests` with both callers: one with sensitive-data access receiving name and email, one without receiving a null booker, the same rows and the same total.
- [x] 4.2 Assert the withheld response contains neither the name nor the email **anywhere in the serialized payload**, rather than only that the member is null — the guarantee is about disclosure, not about one property.
- [x] 4.3 Assert an unresolvable current user yields a withheld response.
- [x] 4.4 Add a mapper test covering both `BookerVisibility` values, and confirm by mutation that inverting the mapper's branch fails it.
- [x] 4.5 Add the membership-snapshot guard over `BookingModel` and `BookerModel` public properties (design D6), with a failure message that asks whether the new member is personal data and who may see it.
- [x] 4.6 Add a guard asserting no management endpoint parameter accepts a booker name or email as a filter, search or sort key.
- [x] 4.7 Extend `BookingsViewReferenceTests` (or add alongside) for the hidden cell, the once-per-page note, its absence when nothing is withheld, and reference-based identification of the cancel control and confirmation.

## 4a. Repo guard relaxed by this change (declared, not incidental)

- [x] 4a.1 `tests/UBookIt.Tests/ChangeDeltaIntegrityTests.cs` — `Every_modified_requirement_names_one_that_exists` asserted an upstream spec exists for **every** active delta, which a change introducing a **new** capability cannot satisfy: that file does not exist until sync. Scoped to deltas whose `Modified` list is non-empty, which is what the guard's own rationale describes. A delta that modifies a requirement and has no upstream spec still fails.
- [x] 4a.2 It had never fired because it walks active changes only, and by archive time a capability is upstream — `sensitive-data` is the first new capability proposed since the guard was written. Recorded here because relaxing a guard that exists to catch silently-mis-synced deltas is exactly the kind of edit that should be declared rather than found in a diff.

## 5. Documentation

- [x] 5.1 Add a Sensitive data subsection to `docs/backoffice.md` under "Who can use it": what requires membership, how to grant it, and that Umbraco's installer places only the original super user in the group so a later administrator is not in it.
- [x] 5.2 Add a sentence to `docs/notifications.md` stating that the notification payload is deliberately unfiltered and why, so its existing description of what a booking carries is not read as an oversight.
- [x] 5.3 Extend `BackofficeDocumentationTests` to assert both documented facts, so the docs requirement is checked rather than asserted.

## 6. Verification

- [x] 6.1 `dotnet build` clean against a **zero** warning baseline, and the full .NET and client suites green.
- [x] 6.2 `openspec validate --strict` for the change.
- [x] 6.3 Live check — **DONE in Chrome against the running TestSite, 2026-09-04.** Chris logged in; the group was toggled on `uBookIt Admin` and restored.

  Method used was the replacement described below, not the original: creating a second user needs an Umbraco invite by email and there is no SMTP on the dev site, so that account could never be activated to log in as. Instead the super user's own Sensitive data membership was removed and re-added.

  **With the group (baseline):** row `QSXG-Q8QQ` showed `Alan Turing` / `alan@example.com`. Intercepted response carried `booker: {name, email}` and the item keys were exactly the ten recorded in the membership guard — an independent confirmation of that guard against the live wire format.

  **Without the group:** the cell read *"Contact details hidden"* in italics; the note rendered once above the table naming the Sensitive data group; the reference stayed in the first column and the cancel control read "Cancel booking QSXG-Q8QQ". **The intercepted response carried `booker: null`, and the name and email appeared nowhere in the 471-byte payload** — the disclosure guarantee, verified end to end rather than inferred. The row and the total were unchanged (`Showing 1–1 of 1`), so withholding removed details from a row rather than a row from the results.

  **Re-added:** details returned, note disappeared. Account left exactly as found (Administrators + Sensitive data).

  **Incidental finding worth keeping:** this site's `claude-mcp-ubookit` user is in Administrators and *not* in Sensitive data — a live instance of the documented gotcha, sitting in the wild on the first site anyone looked at.

- [x] 6.4 Sweep `openspec/specs/` and `docs/` for any sentence this change falsifies, and record the result in this file whether or not anything is found.

  **Result: two falsified sentences found, both fixed; one assessed and deliberately left.**

  - `docs/backoffice.md` — "Anyone with this section can read that", of booker names and email addresses. Directly falsified: the section grant no longer discloses them. Rewritten to separate the two gates.
  - `README.md` — the same claim in shorter form, in the install instructions. It would have been the first thing a new user read about this, and it was the one the docs sweep nearly missed, because the phrasing differs. Rewritten.
  - `openspec/specs/resource-management/spec.md` — "these endpoints return **personal data** — a booking carries the booker's name and email". **Not falsified, and left alone.** Its subject is why uBookIt's endpoints authorize on uBookIt's own section, and that reasoning is untouched: a booking does carry contact details, and the endpoints can still return them. Sensitive-data access is a second, inner gate rather than a replacement, so the requirement makes no claim this change contradicts. Narrowing it would have put six scenarios about resource authorization at risk to restate something they do not cover.

## 7. QA round 1 — REJECT, remediated

- [x] 7.1 **MAJOR** — the two new localization strings had no test. `#term(key: string)` is untyped so tsc cannot catch a missing key, and a missing key renders as **nothing**: a blank Booker cell, the exact state the requirement forbids. An identical guard for the cancel flow's keys already sat twenty lines above the new tests, added by change ⑲ for this reason, and was not extended. Added three tests in that pattern: the keys exist and are non-empty; the note names "Sensitive data" and "administrator"; the cell states an absence rather than masking a value. Mutation-checked twice — deleting the key fails 2, and a note that exists but names nothing fails 1, so the guard checks what it *says* and not merely that it is there.
- [x] 7.2 **MINOR** — the guard relaxation above was undeclared. Now declared in tasks §4a and in the proposal's Impact.
- [x] 7.3 **MINOR** — `sensitive-data`'s "no route skips the decision" was guarded for the mapper but not against a *second* composition site: `BookingModel` is a public POCO with a settable `Booker`. Task 2.3 checked that by hand once, and this change's own argument is that a one-time check is not a guard. Added `The_mapper_is_the_only_place_a_booking_row_is_composed`, a source-level scan (the property is an absence, so no compiled artefact carries it).
- [x] 7.4 **MINOR** — `docs/backoffice.md`'s "What is in the section" table still said Bookings shows "who", unqualified, in the summary a reader scans first. Qualified.
- [x] 7.5 **NIT** — `The_personal_data_in_booking_records_is_disclosed` had been weakened to a phrase matching four places in the document, so it no longer pinned the "Grant it deliberately" callout. Added an assertion that does.
- [x] 7.6 **NIT** — dropped "query" and "term" from the forbidden-parameter list; both match ordinary unrelated parameter names, and a guard that fires for the wrong reason gets relaxed. "search" already covers the endpoint it is really about.

## 8. QA round 2 — REJECT, remediated

Two MAJORs, both **guards that did not observe what they claimed** — the failure mode this
repository has now hit across two changes. One was round 1's own remediation.

- [x] 8.1 **MAJOR** — `The_mapper_is_the_only_place_a_booking_row_is_composed` (added in round 1) was a **no-op**. It scanned for the literal `new BookingModel`, which appears nowhere in `src/`: the mapper composes target-typed, `=> new()`. Every file hit the `continue`, the offender list was unconditionally empty, and the whitelist was never evaluated. A composition site in the mapper's own idiom passed it 8/8. Replaced with a scan that matches **the type** — which target-typed construction cannot avoid naming, since the enclosing member must declare the return type — over **comment-stripped** source, with a positive control asserting the mapper is still visible.
- [x] 8.2 The first replacement was **also blind**, and only the mutation showed it: a file-set snapshot over raw text recorded `BookingsController.cs` as a legitimate referencer, because it names the type twice in `///` prose — so a composition site added to that file, which is exactly where one would go, changed the set not at all. Stripping comment lines first is the whole difference. Re-ran the mutation: now caught.
- [x] 8.3 **MAJOR** — `Everything_that_is_not_personal_data_survives_withholding` could not observe what it was written to prove. Its fixture passed `[]` resources and `null` service, so `Assert.Equal(shown.Resources.Count, withheld.Resources.Count)` compared 0 to 0 and the service was never compared. A mapper stripping both when withholding passed all 1820 tests. Fixture now carries two resources and a named service; both compared **by content**; the fixture's own non-emptiness asserted as an anti-vacuity control. Resources and service mutation-checked **independently**.
- [x] 8.4 **MINOR** — the booker-filter guard read parameter *names* on *one* controller. A `[FromQuery]` DTO whose properties are `BookerName`/`BookerEmail` binds as `?filter.BookerEmail=` and was invisible. Now enumerates every controller in the assembly, GET actions only (the oracle needs a read; a write carrying booker details is placement), and inspects complex parameters' property names as well.
- [x] 8.5 **MINOR** — round 1's NIT was remediated by deletion and cost coverage: with `query`/`term` dropped, `[FromQuery] string? term` passed. Restored as **exact-name** matches alongside substring matching for `booker`/`email`, which is what the false-positive objection actually called for.
- [x] 8.6 All five of QA's mutations re-run against the reworked guards: resources stripped, service stripped, target-typed composition site, filter DTO, bare `term`. Each compiled and each failed with its intended message.

## 9. QA round 3 — REJECT, remediated

Two MAJORs. One a hole in round 2's remediation; one in the client rendering, which nothing had
ever attacked.

- [x] 9.1 **MAJOR** — the composition guard recorded **files**; the requirement is about **routes**. Adding `ToModel(BookingSummary) => ToModel(summary, Shown)` to the mapper — inside the one file the guard whitelists — passed all 1820 tests. Three generations of that scan each corrected the mechanism the last got wrong (wrong literal, wrong idiom, wrong file set) while never expressing the guarantee. Added `Every_route_that_composes_a_booking_row_requires_the_decision`: **reflection** over every member of `BookingModelMapper` returning a `BookingModel`, asserting each takes a `BookerVisibility` **and that it has no default value** — a defaulted parameter satisfies "takes the decision" while letting every caller omit it. The file scan is kept for the other half (a *new* file). Both mutations caught, each with its own message.
- [x] 9.2 **MAJOR** — the element's two withholding decisions could be inverted with all 127 client tests and all 8 source guards green. Swapping the booker cell's arms rendered an **empty cell** for a withheld row and "Contact details hidden" over a supplied name; inverting the note showed the explanation exactly when nothing was withheld. Every guard on that file checked token **presence**, which a swapped ternary does not change — round 1's MAJOR one layer out. Extracted `bookerCell` and `bookerNote` into `booking-rows.ts`, beside `serviceLabel`, where the module's own header says a cell's derivation belongs.
- [x] 9.3 The fix makes both inversions **inexpressible rather than merely detected**. `bookerCell` returns a discriminated union, so swapping the template's arms is a **type error** (verified: three TS errors). `bookerNote` returns a list of zero or one rendered with `.map()`, so the template has no condition left to invert. Content is asserted against both functions in the client suite.
- [x] 9.4 **MINOR** — the filter guard's anti-vacuity control did not control for the case that mattered: blinding the scan to `BookingsController` specifically still left plenty of identifiers from Resources and Services, and both checks passed. Now asserts the bookings controller is among those scanned and that a parameter only it contributes was seen.
- [x] 9.5 **MINOR** — the guard was GET-only, and a POST named `bookings/search` taking a booker's email is a read whatever its verb. The `booker`/`email` rules now apply to **every** HTTP method; the generic search words stay on GETs, where `name` would otherwise fire on every legitimate create body.
- [x] 9.6 **MINOR** — nested filter DTOs (`?filter.Contact.Email=`) were invisible to a one-level scan. Now a depth-bounded, cycle-safe walk. Decided rather than left open, per QA's request.
- [x] 9.7 Three source guards in `BookingsViewReferenceTests` failed after the refactor because they grepped for tokens the element no longer contains. Re-pointed at the new seam and **re-anchored** the `role="status"` check on the note's class, since the term is now read before the element is opened. Both mutation-checked; the realistic regression (inline the logic, delete the derivation) is caught.
- [x] 9.8 Nine mutations run in total this round. Two proved nothing on the first attempt and were re-run: one failed to build (the static-asset cache), one failed to compile (`TS6133`, an unused private method). **A mutation whose run did not compile has proved nothing** — recorded because it nearly passed as evidence twice.
