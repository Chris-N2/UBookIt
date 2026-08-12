## Why

A resource already carries a duration *range* (`BookingConstraints.MinDuration`/`MaxDuration`, default 30 min–8 h) and the placement pipeline already validates any requested length against it — so resources have always been variable-length. Nothing exposes that. `Service.Duration : TimeSpan?` can express only "fixed length" or "the resource's minimum", and no UI offers a length picker, so every booking is pinned to the bottom of the resource's range.

That excludes the case this package most obviously needs to serve: *"book this room for as long as you like, but at least the minimum"* — rooms, equipment, and machinery hire, as opposed to fixed-length appointments. This change lands the duration model and the length picker **before** booking-via-service is built, so that slice consumes a complete duration model instead of retrofitting a user-chosen length through a finished booking flow.

## What Changes

- **BREAKING (Core public API):** `Service.Duration` changes type from `TimeSpan?` to a new `ServiceDuration` value object with two kinds — `Fixed(d)` and `Variable(min?, max?)` — constructed only through validating factories. Three nullable fields were rejected: they admit 8 combinations of which 6 are invalid and force Core, the EF mapper, the management DTO, the Lit editor and Razor each to re-derive the same rule.
- **BREAKING (behaviour):** the late-bound "use each resource's minimum duration" mode is **removed**. The editor's existing `inherit` mode is redefined as `Variable(null, null)` — the resource's own full range. No service can currently be booked, so no booking behaviour regresses.
- **BREAKING (Management API):** `ServiceRequestModel`/`ServiceResponseModel` replace `DurationMinutes : int?` with an explicit duration kind plus optional bounds.
- **BREAKING (front-end helper):** `BookingFormBuilder.ResolveDuration` is renamed `ResolveDisplayDuration`. It is public on a public static class, and the rename is deliberate rather than cosmetic — the neutral name invited use on the write path, where substituting a length is wrong (design D13).
- **BREAKING (schema), explicitly approved:** change ⑥'s `20260807125020_AddServices` migration is amended **in place** rather than superseded by an additive migration. This is the destructive-schema exception that `persistence` requires explicit approval for; approval is recorded here (see design D6).
- **Narrow-only resolution.** A service's bounds narrow, never widen, the resource's range: `[max(svc.Min ?? res.Min, res.Min), min(svc.Max ?? res.Max, res.Max)]`. A resource's maximum is a hard ceiling regardless of service configuration. An empty intersection is not an error — it means that resource cannot fulfil that service.
- **New Core availability query (start-first).** Returns aligned start times each carrying the **maximum bookable length** at that start, rather than only answering "which starts fit exactly N minutes". It is a strict superset — the existing fixed-length projection is derivable from it — so one query serves every length and the maximum becomes discoverable instead of guessed.
- **New delivery endpoint** exposing that query. The existing `GET resources/{id}/slots?durationMinutes=` is unchanged.
- **Length picker in the default front-end.** A length control joins the existing step-1 GET form beside the date; step 2 then lists only the starts that fit. It defaults to the resource's minimum, so ignoring the control reproduces today's behaviour exactly.
- **Informative empty state.** When no start admits the requested length, the form states the longest length actually available that day instead of rendering a blank list.
- **Backoffice editor** duration group keeps two radios (fixed / variable); the variable option gains optional minimum and maximum inputs.

## Capabilities

### New Capabilities

None. This change modifies existing capabilities only.

### Modified Capabilities

- `services`: "Service duration semantics" is rewritten around the two-kind `ServiceDuration` model and narrow-only resolution; "Service duration is an explicit choice" and the CRUD contract are updated for the new DTO shape.
- `availability`: adds the start-first query (aligned starts with their maximum bookable length) alongside the existing fixed-duration slot projection, and fixes the relationship between the two so they cannot drift.
- `delivery-api`: adds the public endpoint for the start-first query.
- `default-frontend`: the booking length becomes a user choice rather than fixed at the resource minimum, with an accessible empty state when no start admits the chosen length.

## Non-goals

- **Booking via a service.** Eligibility-by-type resolution, union availability across eligible resources, the candidate-loop placement, and service delivery endpoints are all change ⑦-2. Nothing here makes a service bookable.
- **Capabilities / capability-constrained eligibility** (⑧) and **multi-role composition** (⑨).
- **Per-resource service overrides.** Dropping the late-bound "resource minimum" kind loses the ability to express "each resource's own minimum" (two barbers with different minimum appointment lengths). That is per-resource service configuration and belongs with ⑧, not with a duration kind carried forever. Accepted knowingly.
- **A preferred-resource parameter** on booking requests — decided, but it belongs to ⑦-2's contract.
- **JS progressive enhancement.** The length picker ships no-JS-first, consistent with the shipped front-end. A JS layer remains deferred from ⑤.
- **Resource-discovery UI** — still deferred from ⑤.

## Impact

**Core** — `Service`, new `ServiceDuration` value object, new failure codes for invalid duration bounds, the narrow-only intersection helper, `IAvailabilityQueryService` gains the start-first query, `SlotProjector` refactored so the fixed-length and start-first projections share one computation.

**Persistence** — `ServiceRow` duration columns, `ServiceRowMapper`, and the in-place amendment of `20260807125020_AddServices` plus its designer file. Requires recreating any existing development database; the TestSite's services have already been cleared.

**Backoffice** — `ServiceRequestModel`/`ServiceResponseModel`, `ServiceModelMapper`, `ServicesController` validation surface, and `services-editor.element.ts` (duration fieldset, client-side guard, localisation terms).

**Web** — new delivery controller action and response models; `BookingViewComponent`, `BookingFormBuilder`, `BookingFormModel`, `Default.cshtml`, and `BookingSurfaceController` for the chosen length.

**Tests** — unit coverage for `ServiceDuration` validation and narrow-only intersection, the start-first projection (including its equivalence with the fixed-length projection), and integration coverage for the amended service schema round-trip.

**No new dependencies.** No DevExpress, no third-party widget framework, no change to the Umbraco 17.5.x pin.
