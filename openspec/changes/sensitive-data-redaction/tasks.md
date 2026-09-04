## 1. Contract and mapping

- [ ] 1.1 Add `BookerModel` (`Name`, `Email`) to `src/UBookIt.Backoffice/Models/BookingModels.cs`, documenting that a null `Booker` on a row means the details were withheld from the caller and never that the booking has none.
- [ ] 1.2 Replace `BookingModel.BookerName` and `BookingModel.BookerEmail` with `BookerModel? Booker`. Mark the breaking change in the XML doc as the `Service` member's precedent is marked.
- [ ] 1.3 Add `BookerVisibility` (`Shown` / `Withheld`) to `src/UBookIt.Backoffice/Models/`, so a call site cannot read the decision backwards.
- [ ] 1.4 Change `BookingModelMapper.ToModel` to take `BookerVisibility` as a required second argument and to emit `Booker = null` for `Withheld`. Update the mapper's class comment: withholding is not "computing a field", it is declining to carry one, and the no-derivation rule still holds.

## 2. Endpoint

- [ ] 2.1 Inject `IBackOfficeSecurityAccessor` into `BookingsController` and resolve the current user's sensitive-data access via `IUser.HasAccessToSensitiveData()`.
- [ ] 2.2 Withhold when the current user cannot be resolved (design D4), with a comment saying why the unreachable branch is written rather than assumed away.
- [ ] 2.3 Pass the resulting `BookerVisibility` through to `ToModel` in `ListBookings`. Confirm no other action composes a `BookingModel`.

## 3. Backoffice client

- [ ] 3.1 Regenerate `src/UBookIt.Backoffice/Client/src/api/` from the changed contract and confirm `bookerName`/`bookerEmail` are gone from `types.gen.ts`.
- [ ] 3.2 Add `ubookitBookings_bookerHidden` and `ubookitBookings_bookerHiddenNote` to `src/UBookIt.Backoffice/Client/src/localization/en-us.ts` using the approved copy from design D8.
- [ ] 3.3 Render the booker cell in `bookings-list.element.ts` from `booking.booker`: name over email when present, the `bookerHidden` term when null. Do not query the current-user context (design D5).
- [ ] 3.4 Show `bookerHiddenNote` once above the table when any row on the page has a null booker, exposed to assistive technology on the same terms as the view's existing failure message, and not shown when no row is withheld.
- [ ] 3.5 Change the per-row cancel control's accessible name and the cancellation confirmation content to identify the booking by its **reference**, unconditionally — including the localization entry that currently interpolates the booker's name.
- [ ] 3.6 Update `booking-rows.ts` and its tests for the new row shape.

## 4. Guards and tests

- [ ] 4.1 Extend `BookingsEndpointTests` with both callers: one with sensitive-data access receiving name and email, one without receiving a null booker, the same rows and the same total.
- [ ] 4.2 Assert the withheld response contains neither the name nor the email **anywhere in the serialized payload**, rather than only that the member is null — the guarantee is about disclosure, not about one property.
- [ ] 4.3 Assert an unresolvable current user yields a withheld response.
- [ ] 4.4 Add a mapper test covering both `BookerVisibility` values, and confirm by mutation that inverting the mapper's branch fails it.
- [ ] 4.5 Add the membership-snapshot guard over `BookingModel` and `BookerModel` public properties (design D6), with a failure message that asks whether the new member is personal data and who may see it.
- [ ] 4.6 Add a guard asserting no management endpoint parameter accepts a booker name or email as a filter, search or sort key.
- [ ] 4.7 Extend `BookingsViewReferenceTests` (or add alongside) for the hidden cell, the once-per-page note, its absence when nothing is withheld, and reference-based identification of the cancel control and confirmation.

## 5. Documentation

- [ ] 5.1 Add a Sensitive data subsection to `docs/backoffice.md` under "Who can use it": what requires membership, how to grant it, and that Umbraco's installer places only the original super user in the group so a later administrator is not in it.
- [ ] 5.2 Add a sentence to `docs/notifications.md` stating that the notification payload is deliberately unfiltered and why, so its existing description of what a booking carries is not read as an oversight.
- [ ] 5.3 Extend `BackofficeDocumentationTests` to assert both documented facts, so the docs requirement is checked rather than asserted.

## 6. Verification

- [ ] 6.1 `dotnet build` clean against a **zero** warning baseline, and the full .NET and client suites green.
- [ ] 6.2 `openspec validate --strict` for the change.
- [ ] 6.3 Live check in `UBookIt.TestSite`: create a second backoffice user with the uBookIt section but not Sensitive data, confirm the list shows hidden cells plus the note, add them to the group, confirm details appear. The super user's own view is not evidence — they are in the group by install.
- [ ] 6.4 Sweep `openspec/specs/` and `docs/` for any sentence this change falsifies, and record the result in this file whether or not anything is found.
