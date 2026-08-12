## 1. Core duration model

- [x] 1.1 Add `ServiceDuration` value object in `UBookIt.Core/Services` — private constructor, `Fixed(TimeSpan)` and `Variable(TimeSpan? min, TimeSpan? max)` factories returning `DomainResult<ServiceDuration>`, exposing the kind and bounds
- [x] 1.2 Validate in the factories: non-positive length or bound, sub-minute length or bound, and minimum greater than maximum — all as `service-duration-invalid` with a field identifying the offending input (design D10)
- [x] 1.3 Implement `TryResolveAgainst(BookingConstraints)` returning the effective range or "cannot fulfil": intersect with the resource range, then floor the maximum and raise the minimum to granularity multiples; report cannot-fulfil when the intersection is empty or holds no granularity multiple (design D3, D4)
- [x] 1.4 Change `Service.Duration` to the non-nullable `ServiceDuration` and update `Service.Create` to accept and surface it; drop the old `TimeSpan?` duration validation now living on the value object
- [x] 1.5 Unit-test `ServiceDuration` construction and validation, covering every failure in 1.2 and both kinds
- [x] 1.6 Unit-test `TryResolveAgainst` against the spec scenarios: narrowing, resource-maximum ceiling, unbounded deferral, non-aligned bounds rounding to 45 minutes, and empty intersection

## 2. Core availability projection

- [x] 2.1 Add a `BookableStart` result type carrying the start instant and its minimum and maximum bookable lengths
- [x] 2.2 Refactor `SlotProjector` so fixed-duration slots and bookable starts derive from one traversal of free time rather than two independent walks (design D8)
- [x] 2.3 Compute each start's maximum as the run to the end of its containing free interval, clamped to the resource maximum and floored to a granularity multiple; omit starts whose maximum falls below the resource minimum
- [x] 2.4 Add `GetBookableStartsAsync(resourceId, fromDate, toDate, ct)` to `IAvailabilityQueryService` and `AvailabilityService`, reusing the existing `ResolveAsync` so the bounded-range, inverted-range, unknown-resource and time-zone failures behave identically
- [x] 2.5 Unit-test the projection against the spec scenarios: shortening maxima across a free interval, resource-maximum capping, a start too close to the interval end, granularity flooring, and lead-time exclusion
- [x] 2.6 Unit-test the equivalence property — for a spread of durations, slot projection returns exactly the bookable starts whose range admits that duration

## 3. Persistence

- [x] 3.1 Replace `ServiceRow.DurationMinutes` with a duration kind column plus nullable minimum and maximum minute columns
- [x] 3.2 Amend `20260807125020_AddServices` and its designer file in place to create the new columns, and update the model snapshot (design D6)
- [x] 3.3 Update `ServiceRowMapper` to map both kinds in each direction, keeping rehydration through the Core factory so corrupt rows surface as exceptions
- [x] 3.4 Drop and recreate the development database (or the uBookIt tables plus their `__uBookItEFMigrationsHistory` rows) so the amended migration applies cleanly
- [x] 3.5 Extend the services integration tests to round-trip a fixed duration, a bounded variable duration, and an unbounded variable duration

## 4. Management API

- [x] 4.1 Replace `DurationMinutes` on `ServiceRequestModel`/`ServiceResponseModel` with the nested duration object carrying an explicit kind (design D12)
- [x] 4.2 Update `ServiceModelMapper` in both directions; an unknown or absent kind maps to a `service-duration-invalid` failure rather than a silent default
- [x] 4.3 Verify `ServicesController` surfaces the field on duration failures so the editor can associate messages with the right input
- [x] 4.4 Test the three round-trips (fixed, bounded variable, unbounded variable) and the rejection of an inverted-bounds payload through the HTTP edge

## 5. Backoffice editor

- [x] 5.1 Update the duration group in `services-editor.element.ts` — keep two radios, relabel them fixed and variable, and add optional minimum and maximum bound inputs shown for the variable choice
- [x] 5.2 Update load and save to read and send the nested duration object, replacing the `durationMinutes === null` mode inference
- [x] 5.3 Update the client-side guard and hint text: state that a variable duration lets the visitor choose the length, and that an empty bound defers to each resource's own limit
- [x] 5.4 Associate duration failures with the specific bound input using the field from the failure, preserving the existing `aria-describedby`/`aria-invalid` pattern
- [x] 5.5 Add or update localisation terms for the relabelled radios, the bound inputs, and the hints
- [x] 5.6 Build the client (`npm run build`) with no TypeScript errors and no new warnings

## 6. Delivery API

- [x] 6.1 Add `GET resources/{resourceId:guid}/bookable-starts` to `AvailabilityController` with `from`/`to` query parameters (design D5)
- [x] 6.2 Add the response models — top-level resource id and zone id, plus entries carrying the start instant and minimum/maximum minutes — and map through `DeliveryModelMapper`
- [x] 6.3 Confirm failures render as problem details with `type` set, consistent with the existing endpoints
- [x] 6.4 Verify the endpoint appears in the `UBookIt.Delivery` OpenAPI group and that the existing `/slots` contract is unchanged

## 7. Default front-end

- [x] 7.1 Add a length `<select>` to the date-selection GET form, offering the granularity multiples the resource permits and defaulting to its minimum duration (design D7, D11)
- [x] 7.2 Read the chosen length from the query in `BookingViewComponent`, clamping or falling back to the resource minimum when absent or unpermitted
- [x] 7.3 Have `BookingViewComponent` obtain bookable starts from Core in-process and filter to the chosen length, rather than querying slots per length
- [x] 7.4 Carry the chosen length on `BookingFormModel` and through the POST so `BookingSurfaceController` places the booking at that length
- [x] 7.5 Compute the longest-available length for the selected date in `BookingFormBuilder` and render the explanatory empty state; keep the existing message for a date with no availability at all (design D9)
- [x] 7.6 Preserve the chosen length across a failed submission alongside the other preserved inputs
- [x] 7.7 Label the length control and keep the fieldset legend accurate now that the duration is chosen rather than fixed
- [x] 7.8 Unit-test `BookingFormBuilder` for the option list, the default, the longest-available figure, and preservation after failure

## 8. Verification

- [x] 8.1 Build the full solution with `--no-incremental`; no new warnings beyond the accepted NU1903 transitive advisories
- [x] 8.2 Run the full test suite, including the SQL Server integration tests
- [x] 8.3 Start the TestSite and exercise the services editor live — create a fixed service, a bounded variable service, and an unbounded one; reopen each and confirm the round-trip; confirm a bound error is announced against the right input by reading the rendered shadow DOM rather than a screenshot
- [x] 8.4 Exercise the booking form live with JavaScript disabled: default length books as before, a longer length filters the times, an unavailable length reports the longest available, and the booking is placed at the chosen length
- [x] 8.5 Call the `bookable-starts` endpoint against the running site and confirm filtering its response for a duration matches the `/slots` response for that duration
- [x] 8.6 Confirm no DevExpress reference and no new third-party dependency entered the repository
- [x] 8.7 Stop the TestSite and confirm no orphaned process holds port 44348

## Live verification (2026-08-12, second session)

All 46 tasks complete. Everything below was exercised against the running
TestSite, not reasoned about.

- **The amended migration re-applied cleanly.** `uBookItService` was rebuilt with
  `DurationKind` / `MinDurationMinutes` / `MaxDurationMinutes`, `DurationMinutes`
  is gone, and `20260807125020_AddServices` is back in
  `__uBookItEFMigrationsHistory`. This was the one genuinely unproven step.
- **6.3** upgraded from structural to live: `bookable-starts` returns
  `application/problem+json; charset=utf-8` with `type` set on all three failure
  modes — `date-range-invalid` (400), `resource-not-found` (404),
  `date-range-too-large` (400).
- **6.4**: the endpoint appears in the `ubookitdelivery` OpenAPI document;
  `/slots` still requires `durationMinutes`, so its contract is unchanged.
- **8.5**: over a 2-day range on a 09:00–17:00 / 30-min-granularity resource,
  filtering the `bookable-starts` response matched `/slots` exactly at 30, 60,
  90, 120 and 240 minutes. Maxima shorten correctly toward the end of the day
  (16:30 → 30 minutes) and clamp at the resource maximum.
- **8.3**: all three duration kinds created through the editor, stored correctly
  (Fixed stores 60/60 — the degenerate range), and each reopened with its kind,
  bounds and enable/disable state intact. Inverted bounds (120/45) were rejected
  with the message associated with the **Shortest** input specifically:
  `aria-invalid="true"` and an `aria-describedby` that resolves, while the other
  two inputs have the attribute **absent rather than empty**, and no duplicate
  group-level error. Verified by reading the shadow DOM, not a screenshot.
- **8.4**: booking form driven end to end. The page contains **zero `<script>`
  elements**, so the flow is inherently JS-free. Default length (no `ubMins`)
  offers 30 minutes as before; a 2-hour choice filtered the starts to 09:00–15:00
  and **placed a booking of exactly 120 minutes** (confirmed in
  `uBookItBooking`); an unavailable 4-hour choice reported the longest available
  as 3 hours, correct given the 12:00–14:00 booking splitting the day.

**One defect found and fixed during this pass.** The explanatory empty state read
"No 4 hours times are available…" — the duration label is a noun phrase, so using
it adjectivally is ungrammatical for every value. Reworded to "No times are
available for 4 hours on …". Logic was correct throughout; this was copy only.

QA review has still NOT been run for this change.

