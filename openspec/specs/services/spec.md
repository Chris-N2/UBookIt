# services Specification

## Purpose

Defines services as a first-class bookable concept: a domain `Service` with a display name, an optional fixed duration, and a single required resource-type role in v1; Core-factory validation with stable machine-readable failure codes; the duration semantics that govern appointment length when booking-via-service ships; and an authorized versioned Management API surface with purpose-built DTOs that keeps raw booking storage out of the HTTP layer. Defining services is purely additive — the existing direct-resource booking path is unaffected.

## Requirements

### Requirement: Service definition
A `Service` SHALL have a `Guid` id, a non-empty display name, an optional fixed duration, and exactly one required role in v1. A `ServiceRole` SHALL name a resource **type** key (normalized lower-case kebab-case, as for resources) and a count, which SHALL be 1 in v1. The model SHALL NOT structurally prevent multiple roles or a role carrying required capabilities — those are additive later — but v1 behaviour SHALL enforce a single role of count 1 and no capability requirements. Domain purity holds: `Service`/`ServiceRole` reference no Umbraco, EF, or third-party types.

#### Scenario: Valid service
- **WHEN** a service is created with name "Massage", a 60-minute duration, and one role requiring resource type `person`
- **THEN** the service is valid, has a non-empty id, a duration of 60 minutes, and one role for type `person` with count 1

#### Scenario: Duration is optional
- **WHEN** a service is created with a name and a single role but no duration
- **THEN** the service is valid and reports no fixed duration

#### Scenario: Name is required
- **WHEN** a service is created with an empty or whitespace name
- **THEN** creation is rejected with a validation failure identifying the name

### Requirement: Service validation with stable failure codes
Service creation and update SHALL be validated through a Core factory, producing a structured result carrying one stable machine-readable code per failed rule (never exceptions for expected rejections). A blank name, a role whose resource-type key is not normalized, a non-positive duration when one is supplied, and a role count other than 1 SHALL each be rejected with a stable code. Codes are contract — consumers map them to messages.

#### Scenario: Non-normalized role type key is rejected
- **WHEN** a service role is defined with resource type `"Meeting Room"` (upper-case and a space)
- **THEN** creation is rejected with a validation failure identifying the role's type key

#### Scenario: Non-positive duration is rejected
- **WHEN** a service is created with a supplied duration of zero or negative
- **THEN** creation is rejected with a stable duration validation code

### Requirement: Service duration semantics
A service's optional duration SHALL define the appointment length when set. When a service has no duration, the booking length SHALL fall back to the fulfilling resource's minimum duration (the current single-resource behaviour). This requirement fixes the contract; it takes effect when booking-via-service ships. Defining a service with or without a duration SHALL be permitted regardless.

#### Scenario: Duration recorded when set
- **WHEN** a service is defined with a 45-minute duration
- **THEN** the stored service reports a fixed duration of 45 minutes

#### Scenario: No duration recorded when unset
- **WHEN** a service is defined without a duration
- **THEN** the stored service reports no fixed duration, deferring to the resource's minimum at booking time

### Requirement: Service management endpoints require backoffice authorization
Every service management endpoint SHALL require an authenticated backoffice user via the shared Umbraco backoffice authorization policy. Unauthenticated requests SHALL receive 401 and SHALL NOT reach handler logic; the endpoints SHALL NOT be reachable anonymously under any shipped configuration.

#### Scenario: Anonymous request is rejected
- **WHEN** any service management endpoint is called without backoffice authentication
- **THEN** the response is 401 and no handler logic executes

### Requirement: Service CRUD endpoints
The Management API SHALL expose versioned endpoints in the `ubookitbackoffice` swagger group for services: paged list (`GET`, with skip/take and total count), get by id, create, full update, and delete. Request and response bodies SHALL be purpose-built DTO models — domain types SHALL NOT appear in the HTTP contract. Validation failures SHALL produce a 400 problem-details response carrying every failed rule as `{code, message, field}` with the domain's stable codes verbatim; an unknown service id SHALL produce 404 with a stable `service-not-found` code.

#### Scenario: Round-trip through the API
- **WHEN** a service is created with a name, a duration, and one role, then fetched by id
- **THEN** the response contains the same name, duration, and role

#### Scenario: Paged list
- **WHEN** 25 services exist and the list endpoint is called with skip 20, take 10
- **THEN** the response contains 5 items and reports a total of 25

#### Scenario: Unknown id
- **WHEN** get or update is called for a service id that does not exist
- **THEN** the response is 404 with code `service-not-found`

#### Scenario: Invalid service is rejected with codes
- **WHEN** a service is submitted with a blank name and a non-normalized role type key
- **THEN** the response is 400 and its errors carry the stable name and role-type-key codes

### Requirement: HTTP callers cannot reach raw booking storage
Service management controllers SHALL depend only on the service management/read ports and validated Core factories. `IBookingStore` and `Booking.Rehydrate` SHALL NOT be referenced by any service controller or API-layer type, preserving the containment established for resource management.

#### Scenario: API layer has no raw store references
- **WHEN** the service management API layer's dependencies are inspected
- **THEN** no service controller or API model references `IBookingStore` or `Booking.Rehydrate`

### Requirement: Defining services does not affect direct-resource booking
Introducing services SHALL NOT change the existing single-resource booking path in any way. A site that defines no services SHALL behave exactly as before this change; availability, placement, and the default front-end for direct-resource booking SHALL be unaffected.

#### Scenario: No services defined
- **WHEN** no service has been created
- **THEN** direct-resource availability queries and booking placement behave exactly as before this change
