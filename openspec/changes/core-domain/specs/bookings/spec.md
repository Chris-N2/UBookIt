# bookings

## ADDED Requirements

### Requirement: Booking shape
A booking SHALL have a `Guid` id, exactly one continuous interval `[start, end)` held as UTC instants plus the IANA zone id it was placed against, a creation timestamp (UTC), a booker, a status, and a collection of 1..N resource claims. Each `ResourceClaim` SHALL bind exactly one resource to the booking's interval. v1 behaviour SHALL enforce exactly one claim per booking; the model SHALL NOT structurally prevent multiple claims.

#### Scenario: Valid single-claim booking
- **WHEN** a booking is placed for one room resource for a valid interval
- **THEN** the booking has exactly one resource claim, referencing that resource, covering the booking interval

#### Scenario: Claims collection is plural by design
- **WHEN** the domain model's public surface is inspected
- **THEN** a booking exposes a collection of resource claims (not a single resource reference)

### Requirement: Booking status machine
Booking status SHALL be one of `Requested`, `Confirmed`, `Declined`, `Cancelled`. Permitted transitions SHALL be exactly: `Requested → Confirmed`, `Requested → Declined`, `Requested → Cancelled`, and `Confirmed → Cancelled`. Any other transition SHALL be rejected with a stable failure code `invalid-status-transition`. In v1, successful placement SHALL yield a `Confirmed` booking (auto-confirm); no v1 pathway SHALL produce `Requested` or `Declined`, but both statuses and their transitions SHALL exist so approval behaviour can be added without breaking changes.

#### Scenario: v1 placement auto-confirms
- **WHEN** a booking is successfully placed
- **THEN** its status is `Confirmed`

#### Scenario: Cancelling a confirmed booking
- **WHEN** a `Confirmed` booking is cancelled
- **THEN** its status becomes `Cancelled`

#### Scenario: Cancelling twice is rejected
- **WHEN** a `Cancelled` booking is cancelled again
- **THEN** the operation fails with code `invalid-status-transition` and the status remains `Cancelled`

### Requirement: Booker identity
A booking's booker SHALL carry an optional opaque member key (`Guid?`, never an Umbraco type) and contact details: a non-empty name, a well-formed email address, and an optional phone number. Contact details SHALL be required regardless of whether a member key is present.

#### Scenario: External booker without member record
- **WHEN** a booking is placed with no member key but with name and valid email
- **THEN** the booking is accepted

#### Scenario: Missing email is rejected
- **WHEN** a booking is placed with a name but no email address
- **THEN** placement fails with a validation failure identifying the email field

### Requirement: Placement validation pipeline
Booking placement SHALL validate a request against an ordered rule pipeline, producing a structured result: success, or failure carrying one stable machine-readable code per failed rule. The rules and codes SHALL be, in order: `interval-invalid` (end not after start, or malformed), `granularity` (start or duration not aligned), `duration-too-short`, `duration-too-long`, `lead-time` (starts sooner than minimum lead), `horizon` (starts beyond booking horizon), `outside-open-hours` (interval not fully inside open hours with exceptions applied), `conflict` (overlaps a blocking claim). Start alignment is defined relative to the start of the coalesced open window containing the interval; when the interval lies outside open hours, start alignment cannot be evaluated and only `outside-open-hours` is reported. Failures SHALL NOT be signalled by exceptions.

#### Scenario: Inverted interval
- **WHEN** placement is requested with an end instant not after its start
- **THEN** the result is failure with code `interval-invalid`

#### Scenario: Request outside open hours
- **WHEN** placement is requested for 07:00–08:00 on a resource open from 08:00
- **THEN** the result is failure with code `outside-open-hours`

#### Scenario: Valid request succeeds with structured result
- **WHEN** placement is requested for an aligned, correctly sized interval in free open time
- **THEN** the result is success and carries the created `Confirmed` booking

### Requirement: Conflict semantics
Two claims SHALL conflict iff they reference the same resource and their intervals overlap, where intervals are half-open `[start, end)` — a booking ending at instant T does not conflict with one starting at T. Claims SHALL block (participate in conflicts and reduce free time) iff their booking's status is `Requested` or `Confirmed`; `Cancelled` and `Declined` bookings SHALL NOT block.

#### Scenario: Back-to-back bookings do not conflict
- **WHEN** a resource has a confirmed booking 09:00–10:00 and placement is requested for 10:00–11:00
- **THEN** no conflict is reported

#### Scenario: One-minute overlap conflicts
- **WHEN** a resource has a confirmed booking 09:00–10:00 and placement is requested for 09:59–11:00
- **THEN** the result is failure with code `conflict`

#### Scenario: Cancelled booking does not block
- **WHEN** a resource's only booking 09:00–10:00 is `Cancelled` and placement is requested for 09:00–10:00
- **THEN** no conflict is reported and placement can succeed

### Requirement: Atomic placement contract
The booking store port (`IBookingStore`) SHALL define placement as atomic with respect to conflict detection: between the conflict check and the persistence of a new booking's claims, no other placement for an overlapping interval on the same resource may succeed. Under concurrent placement of conflicting requests, exactly one SHALL succeed and the others SHALL fail with code `conflict`. (Core defines this contract and honours it in its in-memory test double; the SQL Server implementation and its concurrency proof are change ②.)

#### Scenario: Racing conflicting placements
- **WHEN** two placements for overlapping intervals on the same resource are executed concurrently against a conforming store
- **THEN** exactly one succeeds and the other fails with code `conflict`

### Requirement: Availability and placement service ports
`UBookIt.Core` SHALL expose an availability query service (free-time and slot projection for a resource and date range, per `availability`) and a booking service (placement running the validation pipeline, and cancellation applying the status machine). Both SHALL depend only on the two store ports (`IResourceStore`, `IBookingStore`) so implementations can be swapped without changing Core.

#### Scenario: Services are testable with in-memory stores
- **WHEN** the availability and booking services are constructed with in-memory store implementations
- **THEN** all placement, cancellation, free-time, and slot-projection behaviour in these specs is exercisable without a database
