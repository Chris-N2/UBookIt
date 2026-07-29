## 1. View models + message mapping (host-independent, unit-testable)

- [ ] 1.1 Define front-end view models: a `BookingFormModel` (resourceId, selected date, available times as {instant, local display label}, the currently selected instant, booker name/email/phone, and any error messages), and a `BookingConfirmationModel` (booking id, resource display name, local time range, booker).
- [ ] 1.2 Add a `BookingMessages` map turning stable domain failure codes (`conflict`, `outside-open-hours`, `lead-time`, `horizon`, `granularity`, `duration-too-short`, `duration-too-long`, `email-invalid`, `name-required`, `interval-invalid`, `resource-not-found`) into user-facing sentences; `conflict` → a "no longer available" message.
- [ ] 1.3 Add the view-model assembly helper: given a resource, a date, the Core slots, and the site zone, build a `BookingFormModel` with times shown as site-zone wall-clock and each option's value carrying the exact UTC instant.

## 2. ViewComponent (render)

- [ ] 2.1 `BookingViewComponent` (`InvokeAsync(Guid resourceId)`), injecting `IResourceStore` + `IAvailabilityQueryService` + `SiteBookingSettings`: resolve the resource (missing → an accessible "resource unavailable" message), read the selected date from the request (default: first open date within horizon, else today), fetch that date's slots in-process, and render.
- [ ] 2.2 On render, repopulate from any `TempData` failed-submission (selected time, entered name/email/phone) and surface its error messages.

## 3. Razor views (semantic, accessible, no JS)

- [ ] 3.1 `Views/Shared/Components/Booking/Default.cshtml`: a date `<input type="date">` + "show times" GET submit; when a date is selected, a `fieldset`/`legend` radio group of available times (or an explicit "no times available" message); labelled name/email/phone inputs with required indicated in text and hints via `aria-describedby`; the booking submit rendered via `Html.BeginUmbracoForm<BookingSurfaceController>` (emits the anti-forgery token).
- [ ] 3.2 An error-summary region at the top of the form: lists each message in text, is associated with the offending fields, and is focusable/announced (e.g. `role="alert"` / heading + list). Rendered only when there are errors.
- [ ] 3.3 Confirmation view: booking reference (id), resource, local time range, and booker; semantic headings; no author-CSS dependency for usability.
- [ ] 3.4 Verify the whole flow reads and operates correctly with NO author stylesheet (logical source order, every control labelled and reachable).

## 4. SurfaceController (submit, anti-forgery, PRG)

- [ ] 4.1 `BookingSurfaceController : SurfaceController` with a `[HttpPost] [ValidateAntiForgeryToken] Submit(...)` action binding resourceId, the selected instant, duration, and booker fields.
- [ ] 4.2 Build a `BookingRequest` (booker from the body, member key null — consistent with the delivery API) and place via `IBookingService` in-process.
- [ ] 4.3 Success → stash the booking id in `TempData` and PRG-redirect to the confirmation. Failure → map failures via `BookingMessages`, stash submitted values + messages in `TempData`, and `RedirectToCurrentUmbracoPage()` so the ViewComponent redraws accessibly with input preserved.
- [ ] 4.4 A `conflict` (slot taken between render and submit) surfaces the "no longer available" message with refreshed availability, not a raw error.

## 5. TestSite wiring (not shipped)

- [ ] 5.1 Add a template/content node in `UBookIt.TestSite` that invokes `@await Component.InvokeAsync("Booking", new { resourceId })` for a seeded resource, so the flow is reachable for verification.

## 6. Tests

- [ ] 6.1 Unit-test `BookingMessages`: every mapped code yields a non-empty user-facing message; `conflict` yields the "no longer available" wording; an unknown code has a safe fallback.
- [ ] 6.2 Unit-test the view-model assembly: times render as site-zone wall-clock; each option's value is the exact UTC instant; an empty-slot day yields the "no times" state; the horizon/lead defaults pick a sensible initial date.
- [ ] 6.3 Unit-test repopulation: a failed-submission model round-trips selected time + entered fields into the `BookingFormModel` and carries its error messages.

## 7. Verification & housekeeping

- [ ] 7.1 Build with `--no-incremental`; only the accepted NU1903 transitive advisories may warn (fix any compiler/analyzer/Razor warning).
- [ ] 7.2 Run the full test suite green (unit + integration).
- [ ] 7.3 Live-verify against the running TestSite: complete a booking with JavaScript disabled (choose date → time → details → submit → confirmation); a tokenless POST is rejected; refreshing the confirmation creates no second booking; a missing email redraws with an accessible error summary and preserved input; a `conflict` shows the "no longer available" message.
- [ ] 7.4 Accessibility pass: inspect rendered markup (labels, fieldset/legend radio group, error-summary association, required-in-text) and do a keyboard-only walkthrough; record a screen-reader pass as a pre-release obligation if not completed now.
