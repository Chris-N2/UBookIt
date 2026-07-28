## 1. Core: range cap + read-port list (additive)

- [ ] 1.1 Add `MaxQueryRangeDays` (int, default 31) to `SiteBookingSettings`, with XML docs stating it is an admin guardrail bounding the day-by-day availability computation.
- [ ] 1.2 Add `FailureCodes.DateRangeTooLarge = "date-range-too-large"` (new stable code; do not alter existing values).
- [ ] 1.3 In `AvailabilityService.ResolveAsync`, enforce the inclusive span (`(toDate - fromDate).Days + 1 <= MaxQueryRangeDays`) and return `date-range-too-large` **before** zone resolution and resource load, so both free-time and slot paths are covered and an over-wide range costs nothing.
- [ ] 1.4 Add `Task<ResourcePage> ListAsync(int skip, int take, CancellationToken)` to the read port `IResourceStore` (reuse the existing `ResourcePage` record).
- [ ] 1.5 Bind the setting: extend `UBookItPersistenceComposer.ResolveSettings` to read `UBookIt:MaxQueryRangeDays`, falling back to the default 31 on missing/malformed/non-positive values (mirror the `TimeZoneId` safe-default pattern).

## 2. Persistence: read-only list implementation

- [ ] 2.1 Implement `IResourceStore.ListAsync` on `SqlResourceStore` as a read-only paged query (ordered deterministically, e.g. by display name then id), returning items + unpaged total. No schema change, no migration.

## 3. Web: project wiring + shared edge

- [ ] 3.1 Add the delivery API constants (public `MapToApi` name + route base `umbraco/ubookit/api/v{version:apiVersion}`) and reference required packages via central package management (no new licensed third-party deps).
- [ ] 3.2 Create the anonymous base controller carrying `[ApiController]`, the public versioned route, and `[MapToApi(...)]` — **no** `[Authorize]`, **no** `[BackOfficeRoute]`.
- [ ] 3.3 Add a composer registering the delivery Swagger/OpenAPI document (separate from the backoffice document) and the operation-id/security wiring, mirroring the backoffice composer but with anonymous (public) semantics.
- [ ] 3.4 Add a Web-local `ToProblemResult` mapper: `conflict`→409, `resource-not-found`/`booking-not-found`→404, all other domain codes (incl. `date-range-too-large`)→400; echo every failure as `{ code, message, field }` in `errors[]`, codes verbatim.

## 4. Web: view models + mappers

- [ ] 4.1 Define read-side view models: `ResourceReadModel` (id, type, displayName, description, constraints, zoneId), `PagedResourcesModel`, `IntervalModel` (startUtc, endUtc), `FreeTimeResponseModel` (zoneId + intervals), `SlotModel` (startUtc, durationMinutes) and `SlotsResponseModel` (zoneId + slots).
- [ ] 4.2 Define write-side models: `PlacementRequestModel` (resourceId, start, durationMinutes, booker: name/email/optional phone — **no member key**) and `PlacementResponseModel` (bookingId, status, resourceId, interval, echoed booker).
- [ ] 4.3 Add static mappers Core→view-model (Resource, UtcInterval, Slot, Booking) and request-model→`BookingRequest` (build `Booker` from body only, member key always null; duration minutes→`TimeSpan`).

## 5. Web: endpoints (resource read first, per scope order)

- [ ] 5.1 `ResourcesController` (depends only on `IResourceStore`): `GET /resources` (paged) and `GET /resources/{id:guid}` → read model; unknown id → 404 problem details.
- [ ] 5.2 `AvailabilityController` (depends on `IAvailabilityQueryService`): `GET` free-time and `GET` slots for `resourceId` + `[from,to]` (+ duration for slots); map `date-range-too-large`/`date-range-invalid`/`resource-not-found` via the problem-details mapper; serialize instants ISO-8601 UTC, duration as minutes, zoneId once.
- [ ] 5.3 `BookingsController` (depends on `IBookingService`): `POST /bookings` anonymous, body-only booker; run the placement pipeline; success → `PlacementResponseModel` with the booking id; map every pipeline failure code to problem details.
- [ ] 5.4 Model-level input validation (required fields, well-formed values) returning 400 in the same `errors[]` envelope before the domain is called; route `{id:guid}` constraints for malformed ids.

## 6. Tests

- [ ] 6.1 Core unit tests for the range cap: within-limit computes; over-wide fails `date-range-too-large`; boundary (exactly `MaxQueryRangeDays`) accepted; cap evaluated before resource lookup (unknown resource + over-wide range → `date-range-too-large`); slot path honours the cap.
- [ ] 6.2 Core/Persistence test for `IResourceStore.ListAsync` (paging + total) against the in-memory store and, where the suite runs it, LocalDB.
- [ ] 6.3 Web integration tests (anonymous): resource list/get (+404), free-time and slot reads (shape: UTC instants, zoneId, duration minutes), over-wide range → 400, placement success returns booking id + Confirmed, placement conflict → 409, multi-rule failure echoes each code, missing/malformed body → 400.
- [ ] 6.4 Contract tests for the auth stance: anonymous read/placement succeed without any token; the placement request model exposes no member-key field; a request carrying an ambient auth cookie still books as the body's booker (no member key inferred).
- [ ] 6.5 Structural test: a placement response body exposes no internal aggregate structure (no resource-claims collection) — view-model, not domain aggregate.

## 7. Verification & housekeeping

- [ ] 7.1 Build with `--no-incremental`; confirm the only warnings are the accepted NU1903 Umbraco-transitive advisories (fix any compiler/analyzer/TS warning).
- [ ] 7.2 Confirm the delivery OpenAPI document generates as a separate document from the backoffice one and lists the delivery operations.
- [ ] 7.3 Run the full test suite (LocalDB by default) green.
- [ ] 7.4 Verify the live endpoints against the running TestSite (anonymous availability read + a placement) via the Umbraco MCP / direct HTTP; Chris handles any login step.
