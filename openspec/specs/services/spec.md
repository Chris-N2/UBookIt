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

### Requirement: Backoffice collection view for services
The package SHALL register a services collection view within the existing uBookIt backoffice section, alongside the resources view. It SHALL list services in a semantic `uui`-based table showing the service name, a summary of what the service requires, and its duration (rendered as the resource-minimum fallback when the service has no fixed duration), with paging over the management API's paged list and affordances to create, edit, and delete. Editing SHALL open a separate editor view, never inline in the table. All user-facing strings SHALL come from Umbraco's localization mechanism. No third-party widget framework SHALL be used.

#### Scenario: Section lists services
- **WHEN** a backoffice user with access opens the services view of the uBookIt section
- **THEN** existing services are listed with name, requirement summary, and duration, and a create action is available

#### Scenario: Both views are reachable
- **WHEN** a backoffice user opens the uBookIt section
- **THEN** both a Resources view and a Services view are available, and selecting Services does not disturb the resources view's behaviour

#### Scenario: Paging beyond one page
- **WHEN** more services exist than fit one page and the user advances a page
- **THEN** the next page of services is shown with an accurate "showing X–Y of Z" indication

### Requirement: Workspace editor for a service
Editing a service SHALL happen in a workspace editor with grouped sections for Details (name), Service Requirements, and Duration. Saving SHALL submit the full service via the management API's create or update endpoint, return the user to an accurate collection view on success, and on failure SHALL surface the returned failure codes without losing any form state.

#### Scenario: Creating a service end-to-end
- **WHEN** a user creates a service named "Deep tissue massage" requiring resource type `masseur` with a fixed duration of 60 minutes and saves
- **THEN** the save succeeds, the collection view lists the new service, and reopening it shows all three values as entered

#### Scenario: Editing an existing service
- **WHEN** a user opens an existing service, changes its name and duration, and saves
- **THEN** the save succeeds and reopening the service shows both changes

#### Scenario: Validation failure preserves the form
- **WHEN** a save fails validation (for example an empty name yielding `service-name-required`)
- **THEN** the editor shows the failure and every value the user had entered is still present in the form

### Requirement: Service duration is an explicit choice
The editor SHALL NOT represent "no fixed duration" as an empty input. It SHALL offer an explicit choice between deferring to each resource's minimum duration and specifying a fixed duration in minutes, and SHALL state the deferral behaviour in the UI. Choosing deferral SHALL send `null` for the duration; choosing a fixed duration SHALL send that value in minutes.

#### Scenario: Deferring to the resource minimum
- **WHEN** a user chooses to use each resource's minimum duration and saves
- **THEN** the request carries a null duration and the stored service reports no fixed duration

#### Scenario: Setting a fixed duration
- **WHEN** a user chooses a fixed duration and enters 90 minutes and saves
- **THEN** the stored service reports a 90-minute duration, and reopening the editor shows the fixed-duration choice selected with 90 in the field

### Requirement: Service requirements are edited as a list of one
The editor SHALL present the service's required resource composition in a "Service Requirements" group rendered as a list containing exactly one entry, with no affordance to add or remove entries. The role's count SHALL NOT be surfaced in the UI and SHALL always be sent as 1. The presentation SHALL be structured such that supporting multiple requirements, or a count greater than one, is an addition to this UI rather than a restructuring of it.

#### Scenario: One requirement is editable
- **WHEN** a user opens a service in the editor
- **THEN** exactly one requirement row is shown, no add or remove control is present, and no count field is displayed

#### Scenario: Count is sent as one
- **WHEN** a user saves any service from the editor
- **THEN** the request body carries exactly one role with a count of 1

### Requirement: Resource type is chosen from types already in use
The requirement row SHALL let the user choose a resource type from the types currently in use — sourced from the resource type usage endpoint — while still permitting a type key that no resource currently uses, since a service may legitimately be defined before its resources exist. Entering a type with no matching resources SHALL produce a non-blocking informational hint and SHALL NOT prevent saving. An invalid type key SHALL still be rejected by the server's existing `type-key-invalid` validation and surfaced like any other failure.

#### Scenario: Choosing an existing type
- **WHEN** resources of types `room` and `masseur` exist and the user opens the requirement's type control
- **THEN** both `room` and `masseur` are offered as choices

#### Scenario: Naming a type that does not exist yet
- **WHEN** a user enters the type `physiotherapist` while no resource has that type
- **THEN** an informational hint states that no resources currently have this type, and saving still succeeds

#### Scenario: Invalid type key is rejected by the server
- **WHEN** a user enters a type key the domain rejects and saves
- **THEN** the editor surfaces the server's `type-key-invalid` failure and no service is created

### Requirement: Services editor accessibility baseline
The services collection view and editor SHALL meet the same accessibility bar as the resources section: every input programmatically labelled, a failed save producing an error summary that is announced to assistive technology and receives focus, field- or group-level errors programmatically associated with their controls, full keyboard operability with visible focus, semantic headings and controls, and `uui` components preferred over hand-rolled ones.

#### Scenario: Keyboard-only management
- **WHEN** a user operates the services collection view and editor using only a keyboard
- **THEN** every action — page, create, edit each field, choose a duration mode, choose a resource type, save, cancel, delete and confirm — is reachable and operable with visible focus

#### Scenario: Failed save is announced
- **WHEN** a save fails
- **THEN** the error summary is exposed to assistive technology and receives focus, and each error is associated with the group it belongs to

### Requirement: Service failures are rendered from server-supplied messages
The services UI SHALL render failure messages supplied by the API rather than mapping known failure codes to hard-coded client strings, so that failure codes added later — such as a future guard against deleting a service in use — surface meaningfully without a client change. A generic fallback message SHALL be used only when a response carries no usable message.

#### Scenario: An unrecognized failure code still informs the user
- **WHEN** a delete or save fails with a failure code the client has no specific handling for
- **THEN** the message supplied by the server is displayed to the user
