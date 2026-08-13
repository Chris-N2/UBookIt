## 1. Core ports and failure codes

- [x] 1.1 Add `service-unavailable` and `resource-not-eligible` to `FailureCodes`, with XML docs stating when each is returned (D6, D9)
- [x] 1.2 Add `IResourceStore.ListByTypeAsync(string type, CancellationToken)` returning `IReadOnlyList<Resource>`, documenting why it takes no paging parameters (D1, D2)
- [x] 1.3 Add a multi-resource claims read to `IBookingStore`, documenting that it must match per-resource reads for the same ids and range (D5)
- [x] 1.4 Add an `IAvailabilityQueryService` bookable-starts overload taking an already-loaded `Resource`; refactor the id-based method to load then delegate so the two cannot diverge (D5)
- [x] 1.5 Update the in-memory test doubles for both store ports to implement the new methods consistently with their single-resource counterparts

## 2. Core service resolution and availability

- [x] 2.1 Add the eligibility resolver: load the service, take its single role's type key, list resources of that type, filter to those where `ServiceDuration.TryResolveAgainst` succeeds, keeping each resource's resolved `DurationRange`; unknown service id yields `service-not-found`
- [x] 2.2 Add the run type — `{Min, Max, Step}` with the invariant that both bounds are multiples of `Step` — plus the service bookable-start type holding a start and its runs (D3)
- [x] 2.3 Implement the service availability query: batched claims read, per-candidate bookable starts via the pre-loaded overload, post-filter each `BookableStart` by the candidate's resolved range to produce one run, group by start, collapse identical runs, order deterministically (D3, D4, D5)
- [x] 2.4 Apply the shared range/zone guards so `date-range-invalid`, `date-range-too-large`, and time-zone failures behave identically to the per-resource queries
- [x] 2.5 Verify by inspection that nothing under `Availability/` was modified (D4); if a change seemed necessary, stop and re-read the post-filter derivation

## 3. Core service placement

- [x] 3.1 Add the service placement request/result types, including the optional preferred resource id and a result naming the resolved resource (D7)
- [x] 3.2 Implement the pool-wide duration pre-check: reject below every candidate's resolved minimum with `duration-too-short` and above every resolved maximum with `duration-too-long`, from constraints alone before any free-time work (D8)
- [x] 3.3 Implement preferred-resource handling: in-pool preference ordered first, out-of-pool preference rejected with `resource-not-eligible` (D9)
- [x] 3.4 Implement the candidate loop over the existing atomic `PlaceAsync`, ascending resource id, first success wins, one attempt in flight at a time
- [x] 3.5 Implement the all-fail outcome selection: any candidate `conflict` yields `conflict`, otherwise `service-unavailable`; never echo the last candidate's failures (D6)
- [x] 3.6 Register the new Core service in composition, depending only on read ports plus `IServiceStore`

## 4. Persistence

- [x] 4.1 Implement `ListByTypeAsync` in `SqlResourceStore` as a single type-filtered query eagerly loading open hours and exceptions, with no paging
- [x] 4.2 Implement the batched claims read in `SqlBookingStore` as one query over the existing interval index, not a loop over the single-resource read
- [x] 4.3 Confirm no entity, index, or migration change was introduced, and that the migrations folder is unchanged

## 5. Delivery API

- [x] 5.1 Add delivery DTOs: service read model with an explicit duration kind, paged services model, run model, service bookable-starts response, service placement request
- [x] 5.2 Add `ServicesController` with `GET services` and `GET services/{id}` over `IServiceStore`, returning `service-not-found` for unknown ids
- [x] 5.3 Add `GET services/{id}/bookable-starts` delegating to the Core service availability query, carrying the site zone id once at the top level and naming no resource (D11)
- [x] 5.4 Add `POST services/{id}/bookings` with its own request model, required `durationMinutes`, and the existing `PlacementResponseModel` reused so the resolved resource id is echoed (D7, D8)
- [x] 5.5 Add `service-not-found` to the 404 arm of `ApiResults`, leaving `service-unavailable` and `resource-not-eligible` on the default 400 (D10)
- [x] 5.6 Confirm the new endpoints are anonymous, carry no anti-forgery requirement, and appear in the delivery OpenAPI document, not the backoffice one

## 6. Tests

- [x] 6.1 Unit tests for eligibility: pool is every resource of the type; a resource whose range or granularity admits no permitted length is excluded silently; unknown service yields `service-not-found`
- [x] 6.2 Unit tests for per-candidate narrowing, including that a resource maximum is never widened and that two candidates of one type can offer different lengths
- [x] 6.3 Unit tests for union availability derived from the spec scenarios, not from the implementation: homogeneous pool yields one run; differing granularities yield separate runs and never imply an off-grid length; a gap between candidates is preserved; free time truncates a run; a start no candidate can fulfil is omitted
- [x] 6.4 Unit test that a single unnarrowed candidate reproduces exactly the per-resource bookable-start result
- [x] 6.5 Unit tests for placement: first available candidate wins; ordering is deterministic across repeated runs; preferred resource tried first; preferred-but-busy falls through; out-of-pool preference rejected
- [x] 6.6 Unit tests for the two all-fail outcomes, including the mixed case favouring `conflict` and the deterministic case yielding `service-unavailable`
- [x] 6.7 Unit tests for length handling: omitted length rejected; fixed-service mismatch rejected with no booking placed at any length; too-short and too-long against the pool
- [x] 6.8 Unit tests for the everything-optional invariant: direct placement unchanged; a service booking blocks a later direct booking; a direct booking removes a candidate from availability
- [x] 6.9 Integration tests on real SQL Server for the batched claims read and the type-filtered listing, asserting equivalence with per-resource reads
- [x] 6.10 Integration tests driving all four endpoints, including the 404 for an unknown service and the 400 carrying `service-unavailable` — at the controller boundary, matching the existing `DeliveryApiTests` convention (there is no host test harness in this repo); real HTTP is covered by the live verification in 7.3/7.4
- [x] 6.11 Integration test for a raced candidate loop: concurrent service placements against a pool of two resolve to distinct resources, and a third fails with `conflict`

## 7. Verification

- [x] 7.1 Build the full solution with the TestSite stopped; confirm the only warning is the pre-existing NU1903 baseline
- [x] 7.2 Run the whole test suite and record unit and integration counts
- [x] 7.3 Seed a heterogeneous pool (two resources of one type with different granularity and minimum) in the running TestSite and verify over HTTP that a start carries two distinct runs and that no advertised length is rejected by placement
- [x] 7.4 Verify live that a start and length taken verbatim from the availability response never yields `service-unavailable` — the canary invariant from D6
- [x] 7.5 Confirm the delivery OpenAPI document renders the new operations and models as expected
- [ ] 7.6 Hand to `qa-review` in a fresh session or subagent; do not self-review
