# resource-management Specification

## Purpose

Defines the backoffice management surface for bookable resources: authorized versioned Management API endpoints with purpose-built DTOs, server-side validation with stable failure codes, delete protection for claimed resources, containment of raw booking storage away from the HTTP layer, and a native Umbraco backoffice section with an accessible collection view and workspace editor.

## Requirements

### Requirement: Management endpoints require backoffice authorization
Every uBookIt management endpoint SHALL require an authenticated backoffice user via an Umbraco backoffice authorization policy applied to the shared controller base. Unauthenticated requests SHALL receive 401; the endpoints SHALL NOT be reachable anonymously under any configuration shipped by the package.

#### Scenario: Anonymous request is rejected
- **WHEN** any management endpoint is called without backoffice authentication
- **THEN** the response is 401 and no handler logic executes

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

### Requirement: Server-side validation with stable failure codes
All management input SHALL be validated server-side through the Core domain factories. Validation failures SHALL produce a 400 problem-details response carrying every failed rule as `{code, message, field}` using the domain's stable failure codes verbatim. Unknown resource ids SHALL produce 404 with code `resource-not-found`. Model validation SHALL NOT be the only line of defence: a request bypassing client-side checks receives identical server-side rejection.

#### Scenario: Invalid availability is rejected with codes
- **WHEN** a resource is submitted with overlapping Monday windows and a blank display name
- **THEN** the response is 400 and its errors include codes `windows-overlap` and `display-name-required`

#### Scenario: Duplicate exception dates are rejected at the API
- **WHEN** a resource is submitted with two exceptions for the same date
- **THEN** the response is 400 with code `duplicate-exception-date`

### Requirement: Delete refuses resources with booking claims
Deleting a resource that has any booking claims SHALL fail with a 409 problem-details response carrying stable code `resource-in-use`, and the resource SHALL remain intact. Deleting an unclaimed resource SHALL succeed and remove it with its availability configuration.

#### Scenario: Claimed resource survives delete
- **WHEN** delete is called for a resource that has a booking claim
- **THEN** the response is 409 with code `resource-in-use` and the resource still exists

#### Scenario: Unclaimed resource is deleted
- **WHEN** delete is called for a resource with no claims
- **THEN** the response indicates success and a subsequent get returns 404

### Requirement: HTTP callers cannot reach raw booking storage
Management controllers SHALL depend only on the resource management port and validated Core services. `IBookingStore` and `Booking.Rehydrate` SHALL NOT be referenced by any controller or API-layer type. (Discharges the containment obligation recorded at the persistence archive.)

#### Scenario: API layer has no raw store references
- **WHEN** the management API layer's dependencies are inspected
- **THEN** no controller or API model references `IBookingStore` or `Booking.Rehydrate`

### Requirement: Resource type usage endpoint
The Management API SHALL expose a versioned endpoint in the `ubookitbackoffice` swagger group returning the distinct resource type keys currently in use, each with the number of resources having that type. It SHALL require backoffice authorization like every other management endpoint, SHALL be a read-only projection over existing resource storage requiring no schema change, and SHALL return an empty collection rather than an error when no resources exist. Results SHALL be ordered deterministically so that repeated calls present the same order.

#### Scenario: Types in use are reported with counts
- **WHEN** three resources of type `room` and one of type `masseur` exist and a backoffice user calls the endpoint
- **THEN** the response contains `room` with a count of 3 and `masseur` with a count of 1, and no other entries

#### Scenario: No resources yet
- **WHEN** the endpoint is called on a site with no resources
- **THEN** the response is a successful, empty collection

#### Scenario: Authorization is required
- **WHEN** the endpoint is called without backoffice authentication
- **THEN** the request receives 401 and does not reach handler logic

### Requirement: Problem-details responses carry a type member
Every problem-details response from the Management API SHALL populate the RFC 7807 `type` member alongside `title`, `status`, and the `errors` extension. The backoffice's default error interceptor validates an error body before use and discards any body without a `type`, replacing it with a generic server-error problem that carries no `errors` — so omitting the member makes every field-level failure unreadable to the editor while the server response itself remains correct. The member SHALL distinguish validation, not-found, and conflict outcomes.

#### Scenario: A validation failure reaches the editor with its code intact
- **WHEN** a management endpoint rejects a request with a domain validation failure
- **THEN** the response carries a `type` member and the editor displays the domain failure's own message, not a generic server error

#### Scenario: Every failure status is typed
- **WHEN** a management endpoint returns a validation, not-found, or conflict problem
- **THEN** each response carries a non-empty `type` member distinguishing which of the three occurred

### Requirement: Backoffice section with collection view
The package SHALL register a uBookIt backoffice section containing a resource collection view: a semantic table (uui-based) listing resources with display name, type, and an availability summary, with paging and affordances to create, edit, and delete. Deleting SHALL require confirmation through an accessible in-page modal provided by the backoffice, not a native browser dialog, so that the confirmation is keyboard-operable, exposed to assistive technology, and does not block the page. Dismissing or cancelling the confirmation SHALL leave the resource untouched. The section SHALL use Umbraco's localization mechanism with `en-US` provided. No third-party widget framework SHALL be used.

#### Scenario: Section lists resources
- **WHEN** a backoffice user with access opens the uBookIt section
- **THEN** existing resources are listed in a table with name, type, and availability summary, and a create action is available

#### Scenario: Delete asks for confirmation in-page
- **WHEN** a user activates delete on a resource
- **THEN** an in-page confirmation modal naming the resource is shown, and no native browser dialog appears

#### Scenario: Cancelling confirmation deletes nothing
- **WHEN** a user activates delete and then cancels or dismisses the confirmation
- **THEN** no delete request is issued and the resource remains listed

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

### Requirement: Editor accessibility baseline
The section's markup SHALL meet the project's accessibility bar from the start: every input labelled, error messages programmatically associated with their fields and announced on failed save, full keyboard operability with visible focus, semantic buttons/links/headings, and uui components preferred over hand-rolled controls. Visual styling refinement is out of scope; accessibility is not.

#### Scenario: Keyboard-only editing
- **WHEN** a user operates the collection view and editor using only a keyboard
- **THEN** every action (navigate, create, edit fields, add/remove windows, save, delete) is reachable and operable with visible focus

#### Scenario: Failed save is announced
- **WHEN** a save fails validation
- **THEN** the error summary is exposed to assistive technology (announced), and each field-level error is associated with its input

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

### Requirement: Service configuration preview endpoint
The Management API SHALL expose a versioned endpoint in the `ubookitbackoffice` swagger group that, given a role (a resource type key and a set of required capability keys) and a duration specification, returns the resolution chain for that configuration: the resources of the type, those of them satisfying the capabilities, those of them whose constraints admit a permitted length, and the resources excluded at the duration stage with the bound that excluded them.

It SHALL require backoffice authorization, SHALL be read-only, and SHALL accept a configuration that no saved service holds — its purpose is to report on a service being edited, before it is saved and while it may still be incomplete. It SHALL NOT require the configuration to be a valid service; in particular it SHALL NOT require a service name.

The endpoint SHALL obtain its answer from Core's resolution rather than computing eligibility itself, so that the backoffice cannot report a different answer from the one the booking path will act on. It SHALL be a `POST`: a duration specification is a structured value carrying a kind and whichever bounds apply, and flattening it into query parameters would reproduce the ambiguity the duration value object exists to prevent.

An empty required-capability set SHALL match every resource of the given type. An unknown or not-yet-used type key SHALL yield a chain whose first stage is empty, not an error. A malformed type key or capability key SHALL be a validation failure carrying the same stable code as elsewhere.

#### Scenario: The chain is returned for a configuration
- **WHEN** the endpoint is called for type `room` requiring `projector` with a fixed four-hour duration, and ten rooms exist of which three carry `projector` and one of those can provide four hours
- **THEN** the response reports ten, three, and one for the three stages, and identifies the two resources excluded by duration

#### Scenario: An unsaved and incomplete configuration can be previewed
- **WHEN** the endpoint is called for a configuration no saved service uses and with no service name supplied
- **THEN** the request succeeds and reports the chain

#### Scenario: No required capabilities matches the whole type
- **WHEN** the endpoint is called for type `room` with no required capabilities
- **THEN** the capability stage reports every resource of type `room`

#### Scenario: An unused type key yields an empty first stage
- **WHEN** the endpoint is called for a well-formed type key no resource uses
- **THEN** the response is successful and its first stage is empty, distinguishing this from a capability or duration exclusion

#### Scenario: Preview agrees with candidate resolution
- **WHEN** the endpoint is called for the role and duration of a saved service whose candidates are also resolved through Core
- **THEN** the chain's final stage contains exactly the candidates Core resolves

#### Scenario: A malformed key is rejected rather than ignored
- **WHEN** the endpoint is called with a capability key that is not a normalized key
- **THEN** the request fails with `capability-key-invalid` rather than reporting a chain for a silently narrowed configuration

#### Scenario: Authorization is required
- **WHEN** the endpoint is called without backoffice authentication
- **THEN** the request receives 401 and does not reach handler logic
