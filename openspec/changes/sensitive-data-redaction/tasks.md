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

## 5. Documentation

- [x] 5.1 Add a Sensitive data subsection to `docs/backoffice.md` under "Who can use it": what requires membership, how to grant it, and that Umbraco's installer places only the original super user in the group so a later administrator is not in it.
- [x] 5.2 Add a sentence to `docs/notifications.md` stating that the notification payload is deliberately unfiltered and why, so its existing description of what a booking carries is not read as an oversight.
- [x] 5.3 Extend `BackofficeDocumentationTests` to assert both documented facts, so the docs requirement is checked rather than asserted.

## 6. Verification

- [x] 6.1 `dotnet build` clean against a **zero** warning baseline, and the full .NET and client suites green.
- [x] 6.2 `openspec validate --strict` for the change.
- [ ] 6.3 Live check — **BLOCKED, needs Chris.** Confirm in a browser that the list shows hidden cells plus the note for a user without sensitive-data access, and details for one with it.

  **The method in this task as written does not work, and the replacement is better.** Creating a second backoffice user requires completing an Umbraco invite, which goes by email — there is no SMTP on the dev site, so the second account can never be activated to log in as.

  Do this instead: **remove the super user from the Sensitive data group**, reload Bookings, observe the hidden cells and the note; then add them back and observe the details return. That exercises the identical code path — `HasAccessToSensitiveData()` is group membership and nothing else — needs no new user, no email and no second login, and it is reversible in two clicks.

  What is still true from the original wording: **the super user's default view is not evidence.** They are in the group by install, so seeing names proves only the Shown path. The withheld path is the one that has never been seen in a browser.
- [x] 6.4 Sweep `openspec/specs/` and `docs/` for any sentence this change falsifies, and record the result in this file whether or not anything is found.

  **Result: two falsified sentences found, both fixed; one assessed and deliberately left.**

  - `docs/backoffice.md` — "Anyone with this section can read that", of booker names and email addresses. Directly falsified: the section grant no longer discloses them. Rewritten to separate the two gates.
  - `README.md` — the same claim in shorter form, in the install instructions. It would have been the first thing a new user read about this, and it was the one the docs sweep nearly missed, because the phrasing differs. Rewritten.
  - `openspec/specs/resource-management/spec.md` — "these endpoints return **personal data** — a booking carries the booker's name and email". **Not falsified, and left alone.** Its subject is why uBookIt's endpoints authorize on uBookIt's own section, and that reasoning is untouched: a booking does carry contact details, and the endpoints can still return them. Sensitive-data access is a second, inner gate rather than a replacement, so the requirement makes no claim this change contradicts. Narrowing it would have put six scenarios about resource authorization at risk to restate something they do not cover.
