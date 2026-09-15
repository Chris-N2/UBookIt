# resource-management Specification

## Purpose

Defines the backoffice management surface for bookable resources: authorized versioned Management API endpoints with purpose-built DTOs, server-side validation with stable failure codes, delete protection for claimed resources, containment of raw booking storage away from the HTTP layer, and a native Umbraco backoffice section with an accessible collection view and workspace editor.

## Requirements

### Requirement: Management endpoints require backoffice authorization
Every uBookIt management endpoint SHALL require an authenticated backoffice user via an
Umbraco backoffice authorization policy applied to the shared controller base.
Unauthenticated requests SHALL receive 401; the endpoints SHALL NOT be reachable
anonymously under any configuration shipped by the package.

**The policy SHALL grant access on the basis of the package's own backoffice section**, not
of an unrelated one. Authorizing uBookIt's endpoints against another section is wrong in
both directions at once: a user granted uBookIt but not that section is refused an API for
a section they can see, and a user granted that section but not uBookIt can call every
uBookIt endpoint for a section they cannot. Neither is a configuration a site chose.

This matters more than tidiness because these endpoints return **personal data** — a
booking carries the booker's name and email — and an endpoint that inherits its
authorization from whichever policy was nearest to hand is how such data becomes reachable
by people the site never granted it to.

**The section is the outer gate, not the whole answer.** The `permissions` capability
refines access within it by verb, so section access alone no longer reaches every
endpoint — but no endpoint SHALL require access to any *other* section, and the section
requirement SHALL never be removable by any verb.

#### Scenario: Anonymous request is rejected
- **WHEN** any management endpoint is called without backoffice authentication
- **THEN** the response is 401 and no handler logic executes

#### Scenario: Access follows the package's own section
- **WHEN** an authenticated backoffice user without access to the package's section calls a management endpoint
- **THEN** the request is refused, whatever other sections they hold

#### Scenario: No other section is required
- **WHEN** an authenticated backoffice user with access to the package's section and the endpoint's verb calls a management endpoint
- **THEN** the request is authorized, without requiring access to any other section

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
Management controllers SHALL depend only on the management **ports** and validated Core
services. `IBookingStore` and `Booking.Rehydrate` SHALL NOT be referenced by any controller
or API-layer type. (Discharges the containment obligation recorded at the persistence
archive.)

The plural is deliberate and is the only thing that changed here: the package now has more
than one management port, and a controller reading bookings for the backoffice depends on
the booking management port exactly as the resource controllers depend on the resource one.
**The guarantee is unchanged** — a management port and a validated Core service are the
only routes to storage an API-layer type may take, and raw booking storage is reachable
through neither.

#### Scenario: API layer has no raw store references
- **WHEN** the management API layer's dependencies are inspected
- **THEN** no controller or API model references `IBookingStore` or `Booking.Rehydrate`

#### Scenario: A management port is the only storage route
- **WHEN** a management controller reads or writes stored data
- **THEN** it does so through a management port or a validated Core service, and never through a raw store

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
The Management API SHALL expose a versioned endpoint in the `ubookitbackoffice` swagger group that, given **a list of roles** (each a resource type key, a set of required capability keys, and a count) and a duration specification, returns **one resolution chain per role**: the resources of that role's type, those of them satisfying its capabilities, those of them whose constraints admit a permitted length, and the resources excluded at the duration stage with the bound that excluded them. Each chain SHALL identify the role it describes, and the chains SHALL be returned in the order the roles were supplied.

**BREAKING, made before the first release reached a feed:** the request carries a list of roles and the response a chain per role, replacing the single-role request and single chain.

A role's **count** SHALL be accepted on the request and SHALL default to 1 when
omitted. It is not an input to any chain — a role of count *N* draws on exactly
the pool a role of count 1 does, and every chain SHALL be exactly what it would
have been without it. It is carried because the sufficiency finding beside the
chains is a question about an assignment, which cannot be asked without knowing
how many distinct resources each role needs.

It SHALL require backoffice authorization, SHALL be read-only, and SHALL accept a configuration that no saved service holds — its purpose is to report on a service being edited, before it is saved and while it may still be incomplete. It SHALL NOT require the configuration to be a valid service; in particular it SHALL NOT require a service name.

The endpoint SHALL NOT reject a configuration whose roles duplicate a resource type. Two roles of one type are now valid where their required capabilities differ, and where they do not the save is rejected — but previewing either is how an editor sees what each role resolves to while building or correcting it, and refusing to answer would withhold the information needed to fix the fault. Each role's chain SHALL be reported independently, exactly as supplied.

The endpoint SHALL obtain its answer from Core's resolution rather than computing eligibility itself, so that the backoffice cannot report a different answer from the one the booking path will act on. It SHALL be a `POST`: a duration specification is a structured value carrying a kind and whichever bounds apply, and flattening it into query parameters would reproduce the ambiguity the duration value object exists to prevent.

An empty required-capability set SHALL match every resource of the given type. An unknown or not-yet-used type key SHALL yield a chain whose first stage is empty, not an error. A malformed type key or capability key SHALL be a validation failure carrying the same stable code as elsewhere, identifying which role it came from. An empty role list SHALL be a validation failure. An out-of-range count SHALL be a validation failure carrying the same stable code the save reports, identifying the role it came from — it is now an input to part of the answer, and reporting on a configuration whose count was silently narrowed would describe something other than what is on screen, exactly as a silently narrowed capability set would.

#### Scenario: A chain is returned for each role
- **WHEN** the endpoint is called with a `room` role and a `therapist` role
- **THEN** the response carries two chains in that order, each identifying its role

#### Scenario: The chain is returned for a configuration
- **WHEN** the endpoint is called for a single role of type `room` requiring `projector` with a fixed four-hour duration, and ten rooms exist of which three carry `projector` and one of those can provide four hours
- **THEN** that role's chain reports ten, three, and one for the three stages, and identifies the two resources excluded by duration

#### Scenario: An unsaved and incomplete configuration can be previewed
- **WHEN** the endpoint is called for a configuration no saved service uses and with no service name supplied
- **THEN** the request succeeds and reports the chains

#### Scenario: A configuration that could not be saved can still be previewed
- **WHEN** the endpoint is called with two roles naming the same resource type
- **THEN** the request succeeds and reports a chain for each, even though saving such a service would be rejected

#### Scenario: No required capabilities matches the whole type
- **WHEN** the endpoint is called for a role of type `room` with no required capabilities
- **THEN** that role's capability stage reports every resource of type `room`

#### Scenario: An unused type key yields an empty first stage
- **WHEN** the endpoint is called for a well-formed type key no resource uses
- **THEN** the response is successful and that role's first stage is empty, distinguishing this from a capability or duration exclusion

#### Scenario: Preview agrees with candidate resolution
- **WHEN** the endpoint is called for the roles and duration of a saved service whose candidates are also resolved through Core
- **THEN** each chain's final stage contains exactly the candidates Core resolves for that role

#### Scenario: A malformed key is rejected rather than ignored
- **WHEN** the endpoint is called with a capability key that is not a normalized key
- **THEN** the request fails with `capability-key-invalid`, identifying the role it came from, rather than reporting chains for a silently narrowed configuration

#### Scenario: An empty role list is rejected
- **WHEN** the endpoint is called with no roles
- **THEN** the request fails validation rather than returning an empty chain list

#### Scenario: An omitted count is one
- **WHEN** the endpoint is called for a role carrying no count
- **THEN** the role is reported as needing one resource, exactly as every request written before counts were carried meant

#### Scenario: A count does not change any chain
- **WHEN** the endpoint is called twice for one configuration, differing only in a role's count
- **THEN** every chain is identical in both responses, because count is not a filter resolution applies

#### Scenario: An out-of-range count is rejected rather than narrowed
- **WHEN** the endpoint is called with a role whose count is zero or above the permitted maximum
- **THEN** the request fails with the same count code the save reports, identifying the role it came from

#### Scenario: Authorization is required
- **WHEN** the endpoint is called without backoffice authentication
- **THEN** the request receives 401 and does not reach handler logic

### Requirement: The configuration preview reports a permanent start-grid misalignment
The service configuration preview endpoint SHALL carry, alongside its per-role
resolution chains, whether the configuration's roles can share a bookable start
at all — obtained from Core's alignment check rather than computed by the
backoffice, so the report cannot disagree with the rules the booking path acts
on.

The finding SHALL be a **member of its own** on the response, never folded into a
role's chain. The chains report type, capabilities and duration and say nothing
about opening hours; a stage that mentioned opening times would be the chain
claiming something about availability, which it is forbidden to do.

The finding SHALL be present only when the roles are permanently misaligned.
Alignment SHALL NOT be reported positively, because it would be read as a promise
that the service is bookable, which the endpoint cannot make.

When present, the finding SHALL identify the two roles by their resource types,
the two resources by id and display name, and the window start and granularity of
each — the numbers an editor changes.

The endpoint SHALL remain read-only, SHALL still accept a configuration no saved
service holds, and SHALL NOT reject a configuration because its roles are
misaligned. Its existing responses, codes and authorization are unchanged.

#### Scenario: A misaligned configuration reports the finding
- **WHEN** the endpoint is called for a `room` role whose only resource opens at 09:00 on a 30-minute granularity and a `therapist` role whose only resource opens at 09:15 on a 20-minute granularity
- **THEN** the response carries both role chains as before, plus a misalignment finding naming both roles, both resources, and their window starts and granularities

#### Scenario: An alignable configuration carries no finding
- **WHEN** the endpoint is called for a configuration whose roles can share a start
- **THEN** the response carries the chains and no misalignment finding

#### Scenario: The finding is separate from the chains
- **WHEN** a misalignment finding is returned
- **THEN** no role's chain stage mentions opening hours or start times, and the chains are exactly what they would have been without the finding

#### Scenario: A single-role configuration never reports a misalignment
- **WHEN** the endpoint is called for a configuration with one role
- **THEN** no misalignment finding is returned, because there is no second grid to miss

#### Scenario: A misaligned configuration is still previewed, not rejected
- **WHEN** the endpoint is called for a permanently misaligned configuration
- **THEN** the request succeeds with its chains, exactly as for any other configuration

### Requirement: The configuration preview reports a structurally insufficient pool
The service configuration preview endpoint SHALL carry, alongside its per-role
resolution chains and its start-grid misalignment report, whether the configuration's
roles can be filled **at once** by the resources that exist — obtained from Core's
sufficiency check rather than computed by the backoffice, so the report cannot
disagree with the rules the booking path acts on.

It SHALL be carried as its own member of the response rather than folded into a
chain. The finding is a property of a **set of roles** and belongs to no single one
of them: each role's chain is independently true of that role, and a chain reporting
"2 eligible" is not wrong merely because another role competes for the same two.

The member SHALL be absent, or explicitly empty, when the roles can be filled
together — never a positive statement of sufficiency, and never a count that could be
read as one. The check is one-directional, and the endpoint SHALL NOT add a claim
Core declines to make.

When present it SHALL identify the roles that cannot be filled together, how many
distinct resources they require between them, and how many resources are eligible for
any of them, so the reader can tell a count that is too high from a pool that is too
small without a second request.

Each role the finding names SHALL carry its **position in the request's role
list**. Nothing else identifies it: two roles of one resource type requiring the
same capabilities are equal in every other field, and a consumer rendering them
would otherwise emit two identical entries for two different rows. The position is
what lets a consumer with rows on screen point at one, and a consumer without them
may ignore it.

The endpoint SHALL NOT reject a configuration whose pools are insufficient, exactly
as it does not reject one whose roles duplicate a resource type: previewing such a
configuration is how an editor sees what is wrong while correcting it, and refusing
to answer would withhold the information needed to fix the fault.

This SHALL NOT change any other part of the response. The chains, the misalignment
report, the endpoint's authorization, and its behaviour for configurations that are
too incomplete to resolve are unaffected.

#### Scenario: An insufficient configuration is reported
- **WHEN** a configuration is previewed whose single role has a count of 2 and only one eligible resource
- **THEN** the response carries the sufficiency member identifying that role, 2 required and 1 eligible, and the resolution chains are returned unchanged beside it

#### Scenario: A sufficient configuration reports nothing
- **WHEN** a configuration is previewed whose roles can all be filled at once
- **THEN** the sufficiency member is absent or empty, and carries no positive statement that the service is fulfillable, available, or bookable

#### Scenario: Two roles competing for one resource are reported together
- **WHEN** a configuration is previewed with two roles of one resource type that both resolve to the same single resource
- **THEN** the sufficiency member names both roles as one finding, rather than reporting each role separately

#### Scenario: Each named role carries its position in the request
- **WHEN** a configuration is previewed whose second and third roles cannot be filled together
- **THEN** the finding identifies them by their positions in the request, so a consumer can tell them apart even when they are identical in resource type and required capabilities

#### Scenario: The chains are not altered by the finding
- **WHEN** a configuration is previewed whose two roles each report two eligible resources but which share the same two
- **THEN** each chain still reports two, and the sufficiency member carries the joint claim

#### Scenario: An insufficient configuration is still previewed
- **WHEN** a configuration whose pools are insufficient is previewed
- **THEN** the endpoint returns 200 with the chains and the finding, rather than rejecting the request

#### Scenario: Authorization is unchanged
- **WHEN** the endpoint is called without backoffice authentication
- **THEN** the request receives 401 and does not reach handler logic

### Requirement: Direct bookability is managed on the resource
The management API SHALL carry whether a resource may be booked on its own, on
read and on write, through the existing resource endpoints. A create or update that
omits it SHALL be treated as withholding the permission, consistent with the domain
default and with how an omitted capability collection is treated.

The backoffice resource editor SHALL let a user grant or withhold it, and SHALL
state what withholding means, because the consequence is not inferable from the
control: the resource remains fully bookable as part of a service and stops being
bookable by itself. A control labelled only "bookable" would be read as "this
resource can be booked at all", which is false.

The resources collection view SHALL show it, so that a resource nobody can book
directly is visibly so from the list rather than only after opening it. With the
permission withheld by default, the question an editor asks is "why can nothing
book this room?", and that question SHALL be answerable on the screen where rooms
are managed.

Granting or withholding it SHALL NOT be a validation failure and SHALL NOT block a
save, in either direction. It SHALL NOT affect the delete rule, the type usage
endpoint, the capability usage endpoint, or the service configuration preview.

#### Scenario: The permission round-trips through the API
- **WHEN** a resource is created granting direct booking, then read back
- **THEN** the response states that it may be booked on its own

#### Scenario: An omitted permission withholds it
- **WHEN** a resource is created by a request that does not mention direct booking
- **THEN** the stored resource withholds it

#### Scenario: A full update can withdraw it
- **WHEN** a resource that permits direct booking is updated by a request that withholds it
- **THEN** the stored resource withholds it, consistent with the full-replacement semantics of the rest of the model

#### Scenario: The editor explains what withholding means
- **WHEN** a backoffice user views the control
- **THEN** it states that the resource remains bookable as part of a service, rather than implying the resource cannot be booked at all

#### Scenario: The list distinguishes the two
- **WHEN** a backoffice user opens the resources collection view
- **THEN** each resource shows whether it may be booked on its own

#### Scenario: Neither answer blocks a save
- **WHEN** a resource is saved granting the permission, and another withholding it
- **THEN** both saves succeed
