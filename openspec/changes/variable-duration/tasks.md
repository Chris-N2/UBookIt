## 1. Core duration model

- [x] 1.1 Add `ServiceDuration` value object in `UBookIt.Core/Services` — private constructor, `Fixed(TimeSpan)` and `Variable(TimeSpan? min, TimeSpan? max)` factories returning `DomainResult<ServiceDuration>`, exposing the kind and bounds
- [x] 1.2 Validate in the factories: non-positive length or bound, sub-minute length or bound, and minimum greater than maximum — all as `service-duration-invalid` with a field identifying the offending input (design D10)
- [x] 1.3 Implement `ResolveAgainst(BookingConstraints)` returning the effective range or "cannot fulfil": intersect with the resource range, then floor the maximum and raise the minimum to granularity multiples; report cannot-fulfil when the intersection is empty or holds no granularity multiple (design D3, D4)
- [x] 1.4 Change `Service.Duration` to the non-nullable `ServiceDuration` and update `Service.Create` to accept and surface it; drop the old `TimeSpan?` duration validation now living on the value object
- [x] 1.5 Unit-test `ServiceDuration` construction and validation, covering every failure in 1.2 and both kinds
- [x] 1.6 Unit-test `ResolveAgainst` against the spec scenarios: narrowing, resource-maximum ceiling, unbounded deferral, non-aligned bounds rounding to 45 minutes, and empty intersection

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
- [ ] 6.4 Verify the endpoint appears in the `UBookIt.Delivery` OpenAPI group and that the existing `/slots` contract is unchanged

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
- [ ] 8.3 Start the TestSite and exercise the services editor live — create a fixed service, a bounded variable service, and an unbounded one; reopen each and confirm the round-trip; confirm a bound error is announced against the right input by reading the rendered shadow DOM rather than a screenshot
- [ ] 8.4 Exercise the booking form live with JavaScript disabled: default length books as before, a longer length filters the times, an unavailable length reports the longest available, and the booking is placed at the chosen length
- [ ] 8.5 Call the `bookable-starts` endpoint against the running site and confirm filtering its response for a duration matches the `/slots` response for that duration
- [x] 8.6 Confirm no DevExpress reference and no new third-party dependency entered the repository
- [x] 8.7 Stop the TestSite and confirm no orphaned process holds port 44348

## Status at end of the implementation session (2026-08-12)

42 of 46 tasks complete. Everything builds clean (`--no-incremental`, exit 0,
only the 76 accepted NU1903 transitive advisories) and all 262 tests pass
(233 unit + 29 integration on SQL Server).

**Four tasks remain, all live verification against a running TestSite**, deferred
because the site was needed elsewhere:

- [ ] 6.4 Verify the endpoint appears in the `UBookIt.Delivery` OpenAPI group and that the existing `/slots` contract is unchanged
- [ ] 8.3 Exercise the services editor live (three duration kinds, round-trip, bound error announced against the right input via the rendered shadow DOM)
- [ ] 8.4 Exercise the booking form live with JavaScript disabled
- [ ] 8.5 Call `bookable-starts` against the running site and confirm it agrees with `/slots`

Qualifications on what *was* checked, so nothing reads as more verified than it is:

- **6.3 was confirmed structurally, not live.** The new endpoint returns failures
  through the same `ApiResults.ToProblemResult()` helper as the existing delivery
  endpoints, and that helper sets `Type` (`ApiResults.cs:36`). No HTTP response
  from the new endpoint has been inspected — that is 8.5's job.
- **3.4 was done against the TestSite database only.** The `uBookItService` and
  `uBookItServiceRole` tables were dropped and the `20260807125020_AddServices`
  row removed from `__uBookItEFMigrationsHistory`; both tables were empty first.
  **The amended migration has not yet been observed re-applying at startup** —
  the site was stopped before it next booted. First startup should recreate both
  tables with the new duration columns; if it does not, that is the thing to look
  at before anything else.
- The backoffice client was regenerated from the running site's swagger and
  builds clean, but **no rendered backoffice UI has been looked at**.

QA review has NOT been run for this change.
