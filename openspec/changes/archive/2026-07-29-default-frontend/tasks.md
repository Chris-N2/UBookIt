## 1. View models + message mapping (host-independent, unit-testable)

- [x] 1.1 Define front-end view models: a `BookingFormModel` (resourceId, selected date, available times as {instant, local display label}, the currently selected instant, booker name/email/phone, and any error messages), and a `BookingConfirmationModel` (booking id, resource display name, local time range, booker).
- [x] 1.2 Add a `BookingMessages` map turning stable domain failure codes (`conflict`, `outside-open-hours`, `lead-time`, `horizon`, `granularity`, `duration-too-short`, `duration-too-long`, `email-invalid`, `name-required`, `interval-invalid`, `resource-not-found`) into user-facing sentences; `conflict` → a "no longer available" message.
- [x] 1.3 Add the view-model assembly helper: given a resource, a date, the Core slots, and the site zone, build a `BookingFormModel` with times shown as site-zone wall-clock and each option's value carrying the exact UTC instant.

## 2. ViewComponent (render)

- [x] 2.1 `BookingViewComponent` (`InvokeAsync(Guid resourceId)`), injecting `IResourceStore` + `IAvailabilityQueryService` + `SiteBookingSettings`: resolve the resource (missing → an accessible "resource unavailable" message), read the selected date from the request (default: first open date within horizon, else today), fetch that date's slots in-process, and render.
- [x] 2.2 On render, repopulate from any `TempData` failed-submission (selected time, entered name/email/phone) and surface its error messages.

## 3. Razor views (semantic, accessible, no JS)

- [x] 3.1 `Views/Shared/Components/Booking/Default.cshtml`: a date `<input type="date">` + "show times" GET submit; when a date is selected, a `fieldset`/`legend` radio group of available times (or an explicit "no times available" message); labelled name/email/phone inputs with required indicated in text and hints via `aria-describedby`; the booking submit rendered via `Html.BeginUmbracoForm<BookingSurfaceController>` (emits the anti-forgery token).
- [x] 3.2 An error-summary region at the top of the form: lists each message in text, is associated with the offending fields, and is focusable/announced (e.g. `role="alert"` / heading + list). Rendered only when there are errors.
- [x] 3.3 Confirmation view: booking reference (id), resource, local time range, and booker; semantic headings; no author-CSS dependency for usability.
- [x] 3.4 Verify the whole flow reads and operates correctly with NO author stylesheet (logical source order, every control labelled and reachable).

## 4. SurfaceController (submit, anti-forgery, PRG)

- [x] 4.1 `BookingSurfaceController : SurfaceController` with a `[HttpPost] [ValidateAntiForgeryToken] Submit(...)` action binding resourceId, the selected instant, duration, and booker fields.
- [x] 4.2 Build a `BookingRequest` (booker from the body, member key null — consistent with the delivery API) and place via `IBookingService` in-process.
- [x] 4.3 Success → stash the booking id in `TempData` and PRG-redirect to the confirmation. Failure → map failures via `BookingMessages`, stash submitted values + messages in `TempData`, and `RedirectToCurrentUmbracoPage()` so the ViewComponent redraws accessibly with input preserved.
- [x] 4.4 A `conflict` (slot taken between render and submit) surfaces the "no longer available" message with refreshed availability, not a raw error.

## 5. TestSite wiring (not shipped)

- [x] 5.1 Add a template/content node in `UBookIt.TestSite` that invokes `@await Component.InvokeAsync("Booking", new { resourceId })` for a seeded resource, so the flow is reachable for verification. *(template `Views/UbookitBookingTest.cshtml`; doctype + published "Booking Test Page" created in the backoffice, flow reachable at the site root)*

## 6. Tests

- [x] 6.1 Unit-test `BookingMessages`: every mapped code yields a non-empty user-facing message; `conflict` yields the "no longer available" wording; an unknown code has a safe fallback.
- [x] 6.2 Unit-test the view-model assembly: times render as site-zone wall-clock; each option's value is the exact UTC instant; an empty-slot day yields the "no times" state; the horizon/lead defaults pick a sensible initial date.
- [x] 6.3 Unit-test repopulation: a failed-submission model round-trips selected time + entered fields into the `BookingFormModel` and carries its error messages.

## 7. Verification & housekeeping

- [x] 7.1 Build with `--no-incremental`; only the accepted NU1903 transitive advisories may warn (fix any compiler/analyzer/Razor warning).
- [x] 7.2 Run the full test suite green (unit + integration).
- [x] 7.3 Live-verify against the running TestSite: complete a booking with JavaScript disabled (choose date → time → details → submit → confirmation); a tokenless POST is rejected; refreshing the confirmation creates no second booking; a missing email redraws with an accessible error summary and preserved input; a `conflict` shows the "no longer available" message. *(all live: no-JS booking -> confirmation (ref 13ce1304); tokenless POST -> 400; refresh returns the form (no re-book); missing email -> role=alert summary + preserved name; conflict -> "no longer available")*
- [x] 7.4 Accessibility pass: inspect rendered markup (labels, fieldset/legend radio group, error-summary association, required-in-text) and do a keyboard-only walkthrough; record a screen-reader pass as a pre-release obligation if not completed now. *(markup verified: label/for, fieldset+legend radio group, role=alert tabindex=-1 summary, required-in-text, aria-describedby hint, logical source order, no CSS dependency; keyboard-operable native controls. Human screen-reader pass recorded as a pre-release obligation)*
