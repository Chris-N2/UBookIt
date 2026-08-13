## 1. Capture the current behaviour

- [x] 1.1 With the TestSite running the pre-fix build, re-issue all nine failing requests and record the status and body of each in the change folder as a before/after baseline; confirm all nine are 500 today
- [x] 1.2 Record the three requests that are already correct — availability at the first representable date (200), a large positive `durationMinutes` (400), a negative `durationMinutes` on the direct route (400 `interval-invalid`) — so the fix can be shown not to have changed them

## 2. Availability: reject an unwalkable range

- [x] 2.1 Extend `AvailabilityService.ValidateQueryPreconditions` to fail with `date-range-invalid` when `to` is the last representable date, documenting why the span check does not already cover it (D1)
- [x] 2.2 Confirm the guard is reached by every availability query — free-time, slots, bookable-starts — and by the service bookable-starts query, which shares it
- [x] 2.3 Remove `ServiceBookingService`'s independent exposure: its claims window derives from `toDate.AddDays(1)`, which the shared guard now prevents reaching, but assert that ordering explicitly rather than relying on it
- [x] 2.4 Leave `FreeTimeCalculator.OpenIntervals` unchanged if the guard fully protects it; if any caller can still reach it with an unwalkable range, bound the loop there too and say so in the design

## 3. Placement: guard the interval arithmetic

- [x] 3.1 Add a bidirectional representable-interval check to `BookingService.PlaceAsync` before `BookingInterval.Create`, failing with `interval-invalid` for both overflow (far-future start) and underflow (large negative duration) (D2)
- [x] 3.2 Confirm the check sits ahead of every other pipeline rule, consistent with `interval-invalid` being first in the documented order
- [x] 3.3 Verify the service placement route inherits the fix through `BookingService`, and that its own pool-wide duration pre-check still short-circuits the negative-duration case first — the two must not disagree about the code returned

## 4. Placement: guard the open-hours window

- [x] 4.1 Add a check that the ±1-day window around the start's **local** date can be formed, failing with `interval-invalid` when the start falls on the first or last representable date (D3)
- [x] 4.2 Use the local date, not the instant — conversion to the site zone can move the date across the boundary
- [x] 4.3 Confirm placement at ordinary far-future dates still reaches `horizon` rather than the new check

## 5. Sweep for further instances

- [x] 5.1 Search Core for the pattern rather than the known cases: every `AddDays`, `AddMinutes`, `AddHours`, and every `+`/`-` on `DateOnly`, `DateTime`, `DateTimeOffset`, or `TimeSpan` reachable from delivery input
- [x] 5.2 For each hit, determine whether an expressible input can drive it to a boundary; fix what can, and record what cannot along with why
- [x] 5.3 Probe the boundary inputs not yet tried — `DateTimeOffset.MinValue` start on both routes, `to` one day before the limit, `from` at the limit with `to` earlier, a duration of `int.MaxValue` from a far-future start — and cover anything new that surfaces

## 6. Tests

- [x] 6.1 Unit tests for the availability guard at `DateOnly.MaxValue`, asserting `date-range-invalid`, across all four query entry points
- [x] 6.2 Unit tests proving the neighbours still work: `DateOnly.MinValue`, and the day before `DateOnly.MaxValue`
- [x] 6.3 Unit tests for placement overflow and underflow, both asserting `interval-invalid` and that no booking is persisted
- [x] 6.4 Unit tests for the calendar-boundary start on both the direct and service routes
- [x] 6.5 Unit test that ordinary far-future placement still fails on `horizon`, so the guards have not swallowed a real rule
- [x] 6.6 **Mutation-check every test in this group**: revert each guard in turn, confirm the corresponding test fails, restore. A guard test that passes without the guard is testing nothing — record that this was done
- [x] 6.7 Confirm the existing 311 unit and 35 integration tests pass unedited; any test needing a change is a signal the fix altered real behaviour, not just the boundary

## 7. Verification

- [x] 7.1 Full `--no-incremental` solution build with the TestSite stopped; only NU1903 warnings
- [x] 7.2 Whole suite green; record the new counts
- [x] 7.3 Re-issue all nine requests from 1.1 against the running site; every one now 400 with the documented code, none 500
- [x] 7.4 Re-issue the controls from 1.2 and account for every difference — the three lower-bound cases changed deliberately (D6); the rest are unchanged
- [x] 7.5 Spot-check that a normal booking and a normal availability query still behave identically, over HTTP
- [ ] 7.6 Hand to `qa-review` in a fresh session or subagent; do not self-review
