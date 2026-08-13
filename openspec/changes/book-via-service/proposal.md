## Why

Services can be defined and managed (change ⑥) and can express how long they
take (change ⑦-1), but nothing can be **booked** through one. A visitor still
has to know which resource they want; the whole point of a service — "book a
consultation, I don't care who with" — is unreachable. This change makes a
service bookable end to end over the delivery API by resolving its single role
to an eligible resource at query and placement time.

It lands now because ⑦-1 deliberately built the duration machinery first
(`ServiceDuration.TryResolveAgainst`) on the simple path, leaving a first caller
for exactly this slice. Building it any later means retrofitting a resolved
length through a finished booking flow.

## What Changes

- **A new Core service-booking capability**: resolve a service's single role to
  the set of eligible resources (by resource type key — capabilities are ⑧),
  compute availability as a union across them, and place a booking by looping
  candidates over the existing atomic `IBookingStore.PlaceAsync`.
- **Union availability is set-valued, and the contract says so.** A start backed
  by several resources does not offer a contiguous band of lengths: resources
  differ in granularity and minimum, so the union is gappy and off-grid.
  `BookableStart`'s "every granularity multiple between Min and Max" promise
  cannot be reused. Service-level starts instead carry a list of **arithmetic
  runs** — `{min, max, stepMinutes}`, one per contributing resource — which
  degenerates to a single run (today's shape) on a homogeneous site.
- **Two distinct all-candidates-failed outcomes.** A deterministic all-fail
  (length outside every candidate's resolved range, start off every grid,
  outside every candidate's open hours) reports a new stable
  `service-unavailable` code: retrying is pointless. An all-fail where any
  candidate failed on `conflict` reports `conflict`: the slot was real and
  raced, so retrying may succeed. Returning the last candidate's failures would
  be arbitrary and misleading. The deterministic code doubles as a **canary** —
  it should be unreachable from availability-driven clients, so if it fires,
  availability and placement have drifted apart.
- **New delivery endpoints**: `GET services`, `GET services/{id}`,
  `GET services/{id}/bookable-starts`, `POST services/{id}/bookings`.
- **Service placement is its own endpoint with its own request model**
  (`{start, durationMinutes, preferredResourceId?, booker}`), not nullable
  service fields XOR'd onto the existing `PlacementRequestModel`. The optional
  preferred-resource id is present from day one even though v1 behaviour is
  any-eligible-only: it is cheap now and contract-breaking to retrofit.
- **`durationMinutes` is always required and never substituted**, including for
  a fixed-duration service. A submitted length outside the resolved range is
  rejected. This applies design D13 from ⑦-1 directly, where a write path
  silently substituting a length let a visitor be confirmed for 30 minutes
  having asked for 120.
- **Read port gains a type-filtered, unpaged resource listing.** A candidate
  pool must be complete; paginating it would silently change the answer.
  `IResourceManagementStore.ListTypesAsync` is untouched and stays
  management-only — it carries type keys and counts, which is not the data
  eligibility needs.
- **Store ports gain a batched claims read and Core an availability overload
  taking an already-loaded resource**, so a union query over N candidates does
  not cost `1 + 2N` round trips.
- **`service-not-found` maps to 404** in the delivery problem-details mapper;
  it currently falls through to 400.
- No breaking change to any published contract; no schema change and no
  migration. Direct-resource booking (change ⑤) is untouched and must not
  regress.

## Capabilities

### New Capabilities
- `service-booking`: resolving a service's role to eligible resources, union
  availability across a heterogeneous candidate pool, candidate-loop placement,
  and the failure semantics when no candidate can fulfil a request.

### Modified Capabilities
- `delivery-api`: adds the four service endpoints, the service read model, the
  service placement request model, and the `service-not-found` → 404 mapping;
  states that a deterministic all-fail takes the default 400.
- `bookings`: the store-port requirement gains a batched claims read and a
  type-filtered read-port listing, and records that a new Core service may
  depend on `IServiceStore` alongside the two existing ports.
- `persistence`: SQL Server implementations of the two new port methods.
  Explicitly no schema change and no new migration.

## Non-goals

- **No capability-based eligibility.** Eligibility is by resource type key only;
  required capabilities are change ⑧.
- **No multi-role services.** v1 remains exactly one role of count 1;
  composition, intersection availability, and multi-claim atomic placement are
  change ⑨.
- **No front-end.** This is delivery-API only. The service-oriented default
  front-end (pick service → who → time → book) is change ⑩. ⑤'s existing
  resource-based form is not modified.
- **No `services/{id}/slots?durationMinutes=` endpoint.** The bookable-starts
  response is a strict superset; filtering it for one length is a client-side
  concern. Add the projection when ⑩ demonstrates it needs one, rather than
  shipping a second public contract with no consumer.
- **No cap on candidate-pool size.** The pool is unbounded, with the scale
  characteristics recorded in design. A cap that silently truncates would be
  wrong, and a cap that fails loudly has no motivating case yet.
- **No choose-your-resource UI or per-service "offer preference" switch.** The
  request model accepts a preferred resource id; honouring it as anything other
  than a candidate-ordering hint, and exposing the choice to a booker, are later
  slices.
- **No fairness or round-robin candidate ordering.** Ordering stays
  deterministic by id; correctness holds either way.

## Impact

- **`UBookIt.Core`**: new `Services` booking/availability service and its
  request/result types; `IResourceStore` and `IBookingStore` gain one method
  each; `IAvailabilityQueryService` gains a pre-loaded-resource overload;
  `FailureCodes` gains `service-unavailable`. `ServiceDuration.TryResolveAgainst`
  gets its first runtime caller, unchanged. Nothing under `Availability/`
  changes behaviour.
- **`UBookIt.Persistence`**: `SqlResourceStore` and `SqlBookingStore` implement
  the new port methods. No entity, index, or migration change.
- **`UBookIt.Web`**: new `ServicesController`; new delivery DTOs; one added arm
  in `ApiResults`. Existing controllers, models, and the Razor front-end are
  untouched.
- **`UBookIt.Backoffice`**: unaffected.
- **`UBookIt.Tests`**: unit coverage for eligibility, run-encoded union
  availability across heterogeneous resources, and the two all-fail outcomes;
  integration coverage for the new endpoints and for a raced candidate loop.
