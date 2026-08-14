## MODIFIED Requirements

### Requirement: Resource CRUD endpoints
The Management API SHALL expose versioned endpoints in the `ubookitbackoffice` swagger group: paged list (`GET`, with skip/take and total count), get by id, create, full update, and delete. Request and response bodies SHALL be purpose-built DTO models — domain types SHALL NOT appear in the HTTP contract. The full availability model (opening hours, exceptions, constraints) and the resource's capability set SHALL be readable and writable through these endpoints. An omitted capability collection SHALL be treated as empty, and a full update SHALL replace the capability set rather than merging into it, consistent with the full-update semantics of the rest of the model.

#### Scenario: Round-trip through the API
- **WHEN** a resource is created with opening hours, an exception, custom constraints, and two capabilities, then fetched by id
- **THEN** the response contains the same details, opening hours, exception, constraint values, and both capabilities

#### Scenario: Paged list
- **WHEN** 25 resources exist and the list endpoint is called with skip 20, take 10
- **THEN** the response contains 5 items and reports a total of 25

#### Scenario: Update replaces the capability set
- **WHEN** a resource carrying `cert-x` and `massage` is updated with a body listing only `massage`
- **THEN** the stored resource carries only `massage`

#### Scenario: Omitted capabilities mean none
- **WHEN** a resource is created with no capability collection in the body
- **THEN** creation succeeds and the resource carries no capabilities

#### Scenario: Malformed capability key is rejected
- **WHEN** a resource is created with the capability key `"Cert X"`
- **THEN** the request fails validation with the `capability-key-invalid` code

### Requirement: Workspace editor for a resource
Editing SHALL happen in a separate editor view (not inline in the table) with grouped sections for Details, Opening hours (add/remove window rows per weekday), Exceptions (date with closure or override windows), Constraints, and Capabilities. The Capabilities section SHALL let a user add and remove capability keys, offering those already in use while permitting a new one. Saving SHALL submit the full resource, surface per-field validation failures using the returned codes, and return the user to an accurate collection view.

#### Scenario: Editing availability end-to-end
- **WHEN** a user opens a resource, adds a Tuesday 09:00–17:00 window and a closure exception, and saves
- **THEN** the save succeeds and reopening the resource shows both changes

#### Scenario: Validation failure is surfaced per field
- **WHEN** a user saves a resource with an overlapping window
- **THEN** the editor shows the failure associated with the offending group and no data is lost from the form

#### Scenario: Editing capabilities end-to-end
- **WHEN** a user opens a resource, adds the capability `projector`, and saves
- **THEN** the save succeeds and reopening the resource shows `projector`

#### Scenario: Removing a capability
- **WHEN** a user removes a capability from a resource and saves
- **THEN** the resource no longer carries it, and services requiring it no longer match that resource

#### Scenario: Capability validation failure is surfaced on its own control
- **WHEN** a user saves a resource with a malformed capability key
- **THEN** the editor surfaces the `capability-key-invalid` failure associated with the Capabilities section and no data is lost from the form

## ADDED Requirements

### Requirement: Capability usage endpoint
The Management API SHALL expose a versioned endpoint in the `ubookitbackoffice` swagger group returning the distinct capability keys currently carried by resources, each with the number of resources carrying it. It SHALL require backoffice authorization like every other management endpoint, SHALL be a read-only projection over existing storage, and SHALL return an empty collection rather than an error when no resource carries any capability. Results SHALL be ordered deterministically so that repeated calls present the same order.

The vocabulary SHALL be descriptive, not prescriptive: this endpoint reports what is in use and SHALL NOT constrain what may be entered.

#### Scenario: Capabilities in use are reported with counts
- **WHEN** three resources carry `cert-x` and one carries `massage`, and a backoffice user calls the endpoint
- **THEN** the response contains `cert-x` with a count of 3 and `massage` with a count of 1, and no other entries

#### Scenario: A capability carried by no resource is absent
- **WHEN** a service role requires a capability that no resource carries
- **THEN** that key does not appear in the response

#### Scenario: No capabilities yet
- **WHEN** the endpoint is called on a site where no resource carries a capability
- **THEN** the response is a successful, empty collection

#### Scenario: Authorization is required
- **WHEN** the endpoint is called without backoffice authentication
- **THEN** the request receives 401 and does not reach handler logic

### Requirement: Role match preview endpoint
The Management API SHALL expose a versioned endpoint in the `ubookitbackoffice` swagger group that, given a resource type key and a set of required capability keys, returns the resources matching them. It SHALL require backoffice authorization, SHALL be read-only, and SHALL accept a requirement that no saved service holds — its purpose is to report on a requirement being edited, before it is saved.

The endpoint SHALL determine matching using the same capability subset test that candidate resolution uses, never a separate implementation, so that the backoffice cannot report a different answer from the one the booking path will act on. It SHALL apply the type and capability terms only; it SHALL NOT apply duration-based candidate exclusion, and its contract SHALL be understood as answering "which resources hold these capabilities", not "which resources can fulfil this service".

An empty required-capability set SHALL match every resource of the given type. An unknown or not-yet-used type key SHALL return an empty collection, not an error.

#### Scenario: Matching resources are returned
- **WHEN** the endpoint is called for type `room` requiring `projector`, and three of four rooms carry `projector`
- **THEN** the response contains exactly those three resources

#### Scenario: An unsaved requirement can be previewed
- **WHEN** the endpoint is called with a type and capability combination that no saved service uses
- **THEN** the request succeeds and reports the matching resources

#### Scenario: No required capabilities matches the whole type
- **WHEN** the endpoint is called for type `room` with no required capabilities
- **THEN** every resource of type `room` is returned

#### Scenario: An unused type key yields an empty result
- **WHEN** the endpoint is called for a well-formed type key no resource uses
- **THEN** the response is a successful, empty collection

#### Scenario: Preview agrees with candidate resolution
- **WHEN** the endpoint is called for the type and capabilities of a saved service whose candidates are also resolved through Core
- **THEN** the resources reported by the preview include every candidate Core resolves, differing only by resources Core excludes for reasons other than capability matching

#### Scenario: Authorization is required
- **WHEN** the endpoint is called without backoffice authentication
- **THEN** the request receives 401 and does not reach handler logic
