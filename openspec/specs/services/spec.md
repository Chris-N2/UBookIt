# services Specification

## Purpose

Defines services as a first-class bookable concept: a domain `Service` with a display name, a duration specification that is either fixed or variable within optional bounds, and one or more required roles, each naming a resource type, the capabilities a resource must carry to fill it, and how many distinct resources it requires at once; Core-factory validation with stable machine-readable failure codes; the duration semantics that govern appointment length when a service is booked; and an authorized versioned Management API surface with purpose-built DTOs that keeps raw booking storage out of the HTTP layer. Defining services is purely additive — the existing direct-resource booking path is unaffected.
## Requirements
### Requirement: Service definition
A `Service` SHALL have a `Guid` id, a non-empty display name, a duration
specification, and at least one required role. The duration specification SHALL be a
value object with exactly two kinds — **fixed** (a single length) and **variable** (an
optional minimum and an optional maximum bound) — constructible only through
validating factories, so a service can never hold a combination of duration fields
that has no meaning. A `ServiceRole` SHALL name a resource **type** key (normalized
lower-case kebab-case, as for resources), a set of required capability keys, a count,
which SHALL be at least 1 and at most a bounded maximum, and whether a visitor may
choose which resource fills it. The required-capability set SHALL default to empty,
and an empty set SHALL mean the role constrains only by type. Domain purity holds:
`Service`, `ServiceRole`, the duration value object, and the capability value object
reference no Umbraco, EF, or third-party types.

A role's **count** SHALL mean that many **distinct** resources are required
simultaneously. A count of *N* SHALL be equivalent to *N* interchangeable slots drawn
from that role's eligibility pool, and no resource SHALL fill more than one of them.

A role SHALL carry whether it is **visitor-selectable**, defaulting to not. At most
one role of a service SHALL be visitor-selectable. The restriction exists because a
pinned resource names the **booking** rather than a role — a resource eligible for
several slots may be pinned into any of them — so a booking request carries at most
one pinned resource, and two selectable roles could not both be honoured by it.

Visitor-selectability SHALL govern only what is **offered** as a choice: which pool a
front end presents, and which role a public read advertises as choosable. It SHALL
NOT be a condition of placement. A pinned resource eligible for a role that is not
visitor-selectable SHALL still be honoured, because such a booking is deliverable and
nothing about it is wrong. Neither SHALL it be treated as concealment: every role's
candidate pool is already computable from public reads, so the flag decides what is
presented, never what is knowable.

Two roles MAY name the same resource type, provided their required capabilities
differ. Their eligibility pools then overlap without being equal, which is precisely
the case assignment exists to resolve.

Two roles naming the same resource type **and** requiring the same capabilities SHALL
be rejected. They are two spellings of one requirement, and a count expresses it
exactly once; allowing both would leave two representations that compare unequal and
publish differently while meaning the same thing, and silently merging them would
rewrite what the editor entered. Visitor-selectability SHALL NOT distinguish two
otherwise-identical roles for this purpose: it says how a role is offered, not what it
requires.

Roles SHALL be an unordered set as far as behaviour is concerned: no requirement
depends on their order, and reordering a service's roles SHALL change nothing
observable. The aggregate SHALL nonetheless hold them in a canonical order that is
**total** — resource type, then required capabilities, then count — so that a service
round-trips to an equal value and publishes its roles deterministically. Ordering by
type alone is no longer total now that two roles may share one, and an unstable sort
would let two same-type roles exchange places between saves.

#### Scenario: Valid service with a fixed duration
- **WHEN** a service is created with name "Massage", a fixed 60-minute duration, and one role requiring resource type `person`
- **THEN** the service is valid, has a non-empty id, reports a fixed duration of 60 minutes, and has one role for type `person` with count 1

#### Scenario: Valid service with bounded variable duration
- **WHEN** a service is created with a name, one role, and a variable duration bounded between 45 and 120 minutes
- **THEN** the service is valid and reports a variable duration with a 45-minute minimum and a 120-minute maximum

#### Scenario: Variable duration with no bounds is the unconfigured default
- **WHEN** a service is created with a name and a single role and no duration configuration
- **THEN** the service is valid and reports a variable duration with no minimum and no maximum, deferring entirely to the fulfilling resource's range

#### Scenario: Name is required
- **WHEN** a service is created with an empty or whitespace name
- **THEN** creation is rejected with a validation failure identifying the name

#### Scenario: Required capabilities default to empty
- **WHEN** a service is created with a role naming only a resource type
- **THEN** the service is valid and its role reports an empty required-capability set

#### Scenario: A role carries the capabilities it requires
- **WHEN** a service is created with a role requiring `cert-x` and `welsh`
- **THEN** the role reports both required capabilities and no others

#### Scenario: A service may require several roles of different types
- **WHEN** a service is created with a role for type `room` and a role for type `therapist`
- **THEN** the service is valid and reports both roles, each with count 1

#### Scenario: A service may require two roles of one type with different capabilities
- **WHEN** a service is created with two roles naming `therapist`, one requiring `cert-x` and the other requiring nothing
- **THEN** the service is valid and reports both roles

#### Scenario: A role may require several resources
- **WHEN** a service is created with one role naming `room` and a count of 2
- **THEN** the service is valid and reports that role with a count of 2

#### Scenario: Role order is not observable
- **WHEN** two services are created with the same two roles supplied in opposite orders
- **THEN** they resolve to the same candidates and offer the same availability

#### Scenario: Two same-type roles round-trip in a stable order
- **WHEN** a service with two roles of one resource type differing in capabilities is saved and re-read repeatedly
- **THEN** its roles are returned in the same order every time, and the re-read service compares equal to the saved one

#### Scenario: A role is not visitor-selectable unless said to be
- **WHEN** a service is created with a role naming only a resource type and a count
- **THEN** that role reports that a visitor may not choose which resource fills it

#### Scenario: One role may be marked visitor-selectable
- **WHEN** a service is created with a `room` role and a visitor-selectable `therapist` role
- **THEN** the service is valid, the `therapist` role reports itself visitor-selectable, and the `room` role does not

#### Scenario: A pin is honoured for a role that is not visitor-selectable
- **WHEN** a booking for a service whose roles are all non-selectable is placed pinning an eligible resource
- **THEN** the booking is placed claiming that resource, exactly as it would be for a selectable role

#### Scenario: Visitor-selectability does not make two identical roles distinct
- **WHEN** a service is created with two roles naming `therapist` and requiring the same capabilities, one visitor-selectable and one not
- **THEN** creation is rejected as a duplicated role, as it would be were neither selectable

### Requirement: Service validation with stable failure codes
Service creation and update SHALL be validated through a Core factory, producing a
structured result carrying one stable machine-readable code per failed rule (never
exceptions for expected rejections). A blank name, a role whose resource-type key is
not normalized, a role whose required-capability key is not normalized, a role count
below 1 or above the permitted maximum, two roles naming the same resource type
with the same required capabilities, and more than one visitor-selectable role SHALL
each be rejected with a stable code. A role's malformed capability key SHALL be
rejected with `capability-key-invalid`, distinct from the `type-key-invalid` used for
its resource type, so a consumer can associate each message with the correct control.
Invalid duration configuration SHALL be rejected with the stable
`service-duration-invalid` code, carrying a field identifying which duration input is
at fault so a consumer can associate the message with the correct control. Invalid
duration configuration comprises: a non-positive fixed length or bound, a fixed length
or bound that is not a whole number of minutes, and a variable minimum that exceeds
its maximum. Codes are contract — consumers map them to messages.

A duplicated role — same resource type **and** same required capabilities — SHALL be
rejected with its own stable code, distinct from the code used for a malformed type
key, because the two faults are corrected differently: one is a typo in a key, the
other a requirement stated twice where a count was meant. The failure SHALL identify
the duplicated type and SHALL name the count as the correction.

A service naming more than one visitor-selectable role SHALL be rejected with its own
stable code, distinct from every other role failure. The fault is a property of the
service rather than of any one role — either role would be legal alone — so the
failure SHALL identify the roles in conflict rather than blaming one of them
arbitrarily. It SHALL NOT be resolved by silently clearing the flag on all but one:
which one the editor meant is not derivable, and choosing for them discards a decision
they made.

An out-of-range count SHALL be rejected with its own stable code carrying the
offending role, rather than being accepted and left for the assignment to refuse
slowly. The upper bound is a sanity limit on work, not a domain claim: each unit is a
slot the assignment must fill.

A count exceeding the number of eligible resources SHALL **NOT** be rejected. That is
a property of the pool rather than of the service — resources may be added later —
and refusing the save would block a configuration that is not wrong.

A visitor-selectable role whose pool is empty SHALL **NOT** be rejected, for the same
reason: which resources exist is not a property of the service, and a service may
legitimately be configured before its resources are.

#### Scenario: Non-normalized role type key is rejected
- **WHEN** a service role is defined with resource type `"Meeting Room"` (upper-case and a space)
- **THEN** creation is rejected with a validation failure identifying the role's type key

#### Scenario: Non-normalized required capability key is rejected
- **WHEN** a service role is defined requiring the capability `"Cert X"` (upper-case and a space)
- **THEN** creation is rejected with the `capability-key-invalid` code

#### Scenario: Type and capability failures are distinguishable
- **WHEN** a service role is defined with both a malformed resource type key and a malformed required-capability key
- **THEN** the result carries `type-key-invalid` and `capability-key-invalid` as separate failures

#### Scenario: Non-positive fixed duration is rejected
- **WHEN** a service is created with a fixed duration of zero or negative
- **THEN** creation is rejected with the `service-duration-invalid` code

#### Scenario: Inverted variable bounds are rejected
- **WHEN** a service is created with a variable duration whose minimum is 120 minutes and whose maximum is 45 minutes
- **THEN** creation is rejected with the `service-duration-invalid` code and a field identifying the offending bound

#### Scenario: Sub-minute bounds are rejected
- **WHEN** a service is created with a variable duration bound that is not a whole number of minutes
- **THEN** creation is rejected with the `service-duration-invalid` code

#### Scenario: Two identical roles are rejected in favour of a count
- **WHEN** a service is created with two roles both naming `therapist` and both requiring nothing
- **THEN** creation is rejected with the duplicate-role code, distinct from `type-key-invalid`, the failure identifies `therapist`, and its message names the count as the correction

#### Scenario: Differing capabilities make two roles of one type distinct
- **WHEN** two roles name the same resource type and differ in their required capabilities
- **THEN** creation succeeds — the pools overlap without being equal, which assignment resolves

#### Scenario: A count below one is rejected
- **WHEN** a service is created with a role whose count is zero or negative
- **THEN** creation is rejected with the count code, identifying the offending role

#### Scenario: A count above the permitted maximum is rejected
- **WHEN** a service is created with a role whose count exceeds the permitted maximum
- **THEN** creation is rejected with the count code, identifying the offending role

#### Scenario: A count larger than the pool is accepted
- **WHEN** a service is created with a role of count 3 while only one resource of that type exists
- **THEN** creation succeeds, because resources may be added later and nothing about the service is wrong

#### Scenario: Two visitor-selectable roles are rejected
- **WHEN** a service is submitted with a visitor-selectable `room` role and a visitor-selectable `therapist` role
- **THEN** creation is rejected with a stable code distinct from the duplicate-role and count codes, and the failure identifies both roles

#### Scenario: One visitor-selectable role among several is accepted
- **WHEN** a service is submitted with three roles of which exactly one is visitor-selectable
- **THEN** creation succeeds

#### Scenario: A visitor-selectable role with no eligible resources still saves
- **WHEN** a service is submitted with a visitor-selectable role naming a resource type no resource currently has
- **THEN** creation succeeds, and the empty pool is reported by the resolution summary rather than by rejecting the save

### Requirement: Service duration semantics
A service's duration specification SHALL narrow, and SHALL NEVER widen, the range the fulfilling resource already permits. The effective bookable range for a service on a given resource SHALL be the intersection of the service's range with the resource's `[MinDuration, MaxDuration]`, where a fixed duration is the degenerate range whose minimum and maximum are equal and an absent bound contributes no narrowing. A resource's maximum duration SHALL therefore be a hard ceiling regardless of service configuration.

Service bounds SHALL NOT be required to align to any resource's granularity; they are bounds, not lengths. The lengths actually bookable SHALL be the granularity multiples lying within the effective range.

When the intersection is empty, or when no granularity multiple lies within it, that resource SHALL be treated as unable to fulfil that service. This SHALL NOT be a validation failure at service-definition time, because a service spanning many resources cannot know each resource's limits.

This resolution governs booking behaviour: it is what decides a service's candidate pool and the lengths each candidate offers (see `service-booking`).

#### Scenario: Service bounds narrow the resource range
- **WHEN** a service with a variable duration of 45–180 minutes is resolved against a resource permitting 30–120 minutes
- **THEN** the effective range is 45–120 minutes

#### Scenario: A resource maximum cannot be exceeded
- **WHEN** a service with a fixed 90-minute duration is resolved against a resource whose maximum duration is 60 minutes
- **THEN** the intersection is empty and the resource is reported as unable to fulfil the service

#### Scenario: Unbounded variable duration defers entirely to the resource
- **WHEN** a service with a variable duration and no bounds is resolved against a resource permitting 30 minutes to 8 hours
- **THEN** the effective range is 30 minutes to 8 hours

#### Scenario: Bounds need not align to granularity
- **WHEN** a service with a variable minimum of 40 minutes is resolved against a resource with 15-minute granularity permitting 30–120 minutes
- **THEN** the effective range starts at 40 minutes and the shortest bookable length is 45 minutes

#### Scenario: Empty intersection is not a definition-time failure
- **WHEN** a service is defined with a fixed duration that no currently defined resource could satisfy
- **THEN** the service is created successfully and the mismatch surfaces only when resolving against a resource

#### Scenario: The resolution drives the candidate pool
- **WHEN** a service's availability or placement is requested
- **THEN** a resource whose effective range is empty is excluded from the candidate pool, and each remaining candidate offers exactly the granularity multiples within its own effective range

### Requirement: Service management endpoints require backoffice authorization
Every service management endpoint SHALL require an authenticated backoffice user via the shared Umbraco backoffice authorization policy. Unauthenticated requests SHALL receive 401 and SHALL NOT reach handler logic; the endpoints SHALL NOT be reachable anonymously under any shipped configuration.

#### Scenario: Anonymous request is rejected
- **WHEN** any service management endpoint is called without backoffice authentication
- **THEN** the response is 401 and no handler logic executes

### Requirement: Service CRUD endpoints
The Management API SHALL expose versioned endpoints in the `ubookitbackoffice` swagger group for services: paged list (`GET`, with skip/take and total count), get by id, create, full update, and delete. Request and response bodies SHALL be purpose-built DTO models — domain types SHALL NOT appear in the HTTP contract. The duration SHALL be carried as a single nested object naming its kind explicitly (`fixed` or `variable`) together with its length or bounds in whole minutes, so the wire contract admits no ambiguous combination; an absent bound SHALL be expressed as null. Each role SHALL carry whether it is visitor-selectable, always present rather than omitted when false, so a client never has to treat its absence as a default. Validation failures SHALL produce a 400 problem-details response carrying every failed rule as `{code, message, field}` with the domain's stable codes verbatim; an unknown service id SHALL produce 404 with a stable `service-not-found` code.

#### Scenario: Fixed duration round-trips through the API
- **WHEN** a service is created with a name, a fixed 60-minute duration, and one role, then fetched by id
- **THEN** the response contains the same name, a duration of kind `fixed` with 60 minutes, and the same role

#### Scenario: Variable duration round-trips through the API
- **WHEN** a service is created with a variable duration bounded between 45 and 120 minutes, then fetched by id
- **THEN** the response contains a duration of kind `variable` with a 45-minute minimum and a 120-minute maximum

#### Scenario: Unbounded variable duration round-trips through the API
- **WHEN** a service is created with a variable duration and both bounds null, then fetched by id
- **THEN** the response contains a duration of kind `variable` with both bounds null

#### Scenario: Visitor-selectability round-trips through the API
- **WHEN** a service is created with one of its roles visitor-selectable, then fetched by id
- **THEN** that role reports itself visitor-selectable and the others report that they are not

#### Scenario: Paged list
- **WHEN** 25 services exist and the list endpoint is called with skip 20, take 10
- **THEN** the response contains 5 items and reports a total of 25

#### Scenario: Unknown id
- **WHEN** get or update is called for a service id that does not exist
- **THEN** the response is 404 with code `service-not-found`

#### Scenario: Invalid service is rejected with codes
- **WHEN** a service is submitted with a blank name and a non-normalized role type key
- **THEN** the response is 400 and its errors carry the stable name and role-type-key codes

#### Scenario: Two visitor-selectable roles are rejected with the domain's code
- **WHEN** a service with two visitor-selectable roles is submitted
- **THEN** the response is 400 and its errors carry the domain's stable code for that rule verbatim

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
The package SHALL register a services collection view within the existing uBookIt backoffice section, alongside the resources view. It SHALL list services in a semantic `uui`-based table showing the service name, a summary of what the service requires, and a summary of its duration, with paging over the management API's paged list and affordances to create, edit, and delete. The duration summary SHALL distinguish the two kinds and SHALL state whichever bounds a variable duration carries, rather than reporting only that it is variable. Editing SHALL open a separate editor view, never inline in the table. All user-facing strings SHALL come from Umbraco's localization mechanism. No third-party widget framework SHALL be used.

The requirement summary SHALL distinguish two roles that name the **same** resource
type. Such roles are legal exactly when their required capabilities differ, so a
summary reporting only type and count renders a configuration the domain accepts
identically to one it rejects — and the rejection's own message tells the editor to
use a count instead, which a reader of that summary would have no way to understand.
Where two roles share a resource type the summary SHALL therefore state what
distinguishes them.

Where no two roles share a resource type the summary SHALL NOT be required to state
required capabilities, so the common case stays short. The summary is a table cell,
and making every service noisier to disambiguate the few would trade the many
against the few.

#### Scenario: Section lists services
- **WHEN** a backoffice user with access opens the services view of the uBookIt section
- **THEN** existing services are listed with name, requirement summary, and duration summary, and a create action is available

#### Scenario: A fixed duration is summarised as its length
- **WHEN** a service with a fixed 60-minute duration is listed
- **THEN** its duration summary states 60 minutes

#### Scenario: A bounded variable duration states both bounds
- **WHEN** a service with a variable duration bounded between 45 and 120 minutes is listed
- **THEN** its duration summary states that it is variable and reports both bounds

#### Scenario: An unbounded variable duration is summarised as variable
- **WHEN** a service with a variable duration and no bounds is listed
- **THEN** its duration summary states that it is variable, with no bounds reported

#### Scenario: Both views are reachable
- **WHEN** a backoffice user opens the uBookIt section
- **THEN** both a Resources view and a Services view are available, and selecting Services does not disturb the resources view's behaviour

#### Scenario: Paging beyond one page
- **WHEN** more services exist than fit one page and the user advances a page
- **THEN** the next page of services is shown with an accurate "showing X–Y of Z" indication

#### Scenario: Two roles of one type are distinguishable in the summary
- **WHEN** a service with two `therapist` roles, one requiring `cert-x` and one requiring nothing, is listed
- **THEN** its requirement summary distinguishes the two entries by what each requires, rather than rendering them as two identical entries

#### Scenario: Distinct-type services keep a short summary
- **WHEN** a service requiring a `room` and a `therapist`, neither sharing a type, is listed
- **THEN** its requirement summary names the two types and their counts, and is not required to state required capabilities

#### Scenario: A counted role states its count
- **WHEN** a service with one role of count 2 is listed
- **THEN** its requirement summary states that two of that resource type are required

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
The editor SHALL NOT represent any duration mode as an empty input. It SHALL offer an explicit choice between a **fixed** duration in minutes and a **variable** duration, and SHALL state in the UI what the variable option means — that the visitor chooses the length, within the bounds given and within what each resource permits. The variable option SHALL offer optional minimum and maximum bound inputs, and SHALL state that leaving a bound empty defers that bound to the resource. Choosing fixed SHALL send a fixed duration carrying that value in minutes; choosing variable SHALL send a variable duration carrying whichever bounds were supplied.

Validation messages for duration inputs SHALL be associated with the specific input at fault using the field carried on the failure, so a screen-reader user is told which control to correct.

#### Scenario: Setting a fixed duration
- **WHEN** a user chooses a fixed duration, enters 90 minutes, and saves
- **THEN** the stored service reports a fixed duration of 90 minutes, and reopening the editor shows the fixed choice selected with 90 in the field

#### Scenario: Setting bounded variable duration
- **WHEN** a user chooses a variable duration, enters a 45-minute minimum and a 120-minute maximum, and saves
- **THEN** the stored service reports a variable duration with those bounds, and reopening the editor shows the variable choice selected with both bounds populated

#### Scenario: Leaving both bounds empty defers to the resource
- **WHEN** a user chooses a variable duration, leaves both bound inputs empty, and saves
- **THEN** the stored service reports a variable duration with no bounds and the editor states that each resource's own limits apply

#### Scenario: A bound error identifies its own input
- **WHEN** a user submits a variable duration whose minimum exceeds its maximum
- **THEN** the error message is rendered and programmatically associated with the offending bound input rather than with the duration group as a whole

### Requirement: Service requirements are edited as a list of roles
The editor SHALL present the service's required resource composition in a "Service
Requirements" group rendered as a list of one or more entries, with affordances to
add and remove entries. A service SHALL always retain at least one role; removing the
last entry SHALL NOT be offered. Each entry SHALL carry its own resource type
control, its own required-capability control, its own **count** control, and its own
**visitor-selectable** control.

The count control SHALL default to 1, SHALL be labelled and associated with its entry
as the other controls are, and SHALL express how many distinct resources of that
requirement a booking needs at once. A count the server rejects SHALL have its failure
rendered against that entry.

The visitor-selectable control SHALL default to off, SHALL be labelled and associated
with its entry as the other controls are, and SHALL state what turning it on does:
that a visitor booking this service may choose which resource fills this requirement.
Because turning it on publishes that role's resources as a list of choices, the label
SHALL make that consequence legible to the editor rather than leaving it to be
discovered on the site.

Where a role's count exceeds 1, the control SHALL state that a visitor chooses **one**
of that many resources and the rest are assigned. An editor who believes they are
offering a choice of all of them would be configuring something the product does not
do.

The editor SHALL NOT prevent a user selecting a resource type another role already
uses. The duplicate-role rule SHALL be enforced by the server and its failure rendered
against the offending entry, so that changing the rule remains a server change rather
than a change in two places.

The editor SHALL NOT prevent a user turning the visitor-selectable control on for a
second entry, for the same reason. The at-most-one rule SHALL be enforced by the
server and its failure rendered against the entries in conflict, so no data is lost
from the form and the rule lives in one place.

#### Scenario: Roles can be added and removed
- **WHEN** a user opens a service in the editor and adds a second requirement
- **THEN** two requirement rows are shown, each with its own type, capability, count and visitor-selectable controls, and each can be removed except when only one remains

#### Scenario: The last role cannot be removed
- **WHEN** a service has exactly one requirement row
- **THEN** no remove control is offered for it

#### Scenario: Count defaults to one
- **WHEN** a user adds a requirement row and saves without touching its count
- **THEN** that role is sent with a count of 1

#### Scenario: A count is editable and round-trips
- **WHEN** a user sets a requirement's count to 2, saves, and reopens the service
- **THEN** the row shows a count of 2

#### Scenario: Visitor-selectable defaults to off
- **WHEN** a user adds a requirement row and saves without touching its visitor-selectable control
- **THEN** that role is sent as not visitor-selectable

#### Scenario: Visitor-selectable is editable and round-trips
- **WHEN** a user turns a requirement's visitor-selectable control on, saves, and reopens the service
- **THEN** the row shows that control on

#### Scenario: A second visitor-selectable role is reported by the server against its rows
- **WHEN** a user turns the visitor-selectable control on for a second requirement and saves
- **THEN** the editor renders the server's failure against the requirement rows in conflict, and no data is lost from the form

#### Scenario: A selectable role of count greater than one says what a visitor chooses
- **WHEN** a requirement row has a count of 2 and its visitor-selectable control is on
- **THEN** the row states that a visitor chooses one of the two resources and the remainder are assigned

#### Scenario: A duplicate role is reported by the server against its row
- **WHEN** a user selects a resource type and capability set already used by another requirement and saves
- **THEN** the editor renders the server's duplicate-role failure against that requirement row, and no data is lost from the form

#### Scenario: An out-of-range count is reported against its row
- **WHEN** a user enters a count the server rejects and saves
- **THEN** the editor renders that failure against the offending requirement row, and no data is lost from the form

### Requirement: Resource type is chosen from types already in use
The requirement row SHALL let the user choose a resource type from the types currently in use — sourced from the resource type usage endpoint — while still permitting a type key that no resource currently uses, since a service may legitimately be defined before its resources exist. Choosing a type with no matching resources SHALL NOT prevent saving; the fact that nothing has that type SHALL be reported through the resolution summary's first stage rather than as a separate type-specific hint, so that one report answers the question. An invalid type key SHALL still be rejected by the server's existing `type-key-invalid` validation and surfaced like any other failure.

#### Scenario: Choosing an existing type
- **WHEN** resources of types `room` and `masseur` exist and the user opens the requirement's type control
- **THEN** both `room` and `masseur` are offered as choices

#### Scenario: Naming a type that does not exist yet
- **WHEN** a user enters the type `physiotherapist` while no resource has that type
- **THEN** the summary reports the type stage as empty, saving still succeeds, and no blocking error is shown

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

### Requirement: Required capabilities are edited on the requirement row
The requirement row SHALL let a user add and remove required capability keys, offering the capability keys already in use — sourced from the capability usage endpoint — while still permitting a key no resource currently carries, for the same reason a not-yet-used resource type is permitted. Removing every capability SHALL be permitted and SHALL restore type-only matching, which the summary's capability stage then reports as equal to its type stage. A malformed key SHALL be surfaced from the server's `capability-key-invalid` failure, associated with the capability control rather than the type control.

#### Scenario: Adding a required capability
- **WHEN** a user adds the capability `cert-x` to the requirement and saves
- **THEN** the save succeeds and reopening the service shows `cert-x` as required

#### Scenario: Capabilities in use are offered
- **WHEN** resources carry the capabilities `cert-x` and `massage` and the user opens the capability control
- **THEN** both are offered as choices

#### Scenario: A capability nothing carries is still permitted
- **WHEN** a user requires a capability no resource currently carries and saves
- **THEN** the save succeeds and the summary reports the capability stage as empty while the type stage is populated

#### Scenario: Removing all capabilities restores type-only matching
- **WHEN** a user removes every required capability from a service and saves
- **THEN** the service's role requires no capabilities and matches every resource of its type

#### Scenario: Malformed capability key is surfaced on its own control
- **WHEN** the server rejects a capability key with `capability-key-invalid`
- **THEN** the editor associates the message with the capability control and no data is lost from the form

### Requirement: The editor reports the resolution chain for each role
The editor SHALL report, as a service is edited, the resolution chain for **each role** of the configuration on screen: how many resources have that role's resource type, how many of those carry all its required capabilities, and how many of those can provide the service's duration. Each chain SHALL identify the role it describes. The report SHALL refresh when any role's type or capabilities, or the duration, change, and SHALL be informational: it SHALL NOT block saving.

The report SHALL be presented at **form level**, not inside the Service Requirements group. Its inputs span both the requirements and the duration, and an editor who narrows a duration must not have to return to another group to discover that doing so emptied a pool.

Reporting a single surviving count per role SHALL NOT be sufficient. Each chain SHALL make each stage's count derivable, so that an empty or narrowed pool is attributable to the filter responsible: a mistyped resource type, an over-narrow capability set, and a duration no resource can provide are three different faults corrected in three different places. In particular, a resource type matching nothing SHALL NOT be reported as a capability problem.

The report SHALL NOT refer to required capabilities for a role that names none. Such a stage filtered nothing by construction — an empty requirement matches every resource of its type — so its count remains derivable from the stage before it, and reporting it would describe the configuration in terms the editor never entered.

Each role's chain SHALL be reported independently, and a report combining several roles into one count SHALL NOT be produced. Two roles may now name one resource type, so their pools can **overlap**: a resource may be counted in more than one chain, and a combined number would double-count it as well as describing no filter that resolution applies. Each chain remains true of the role it describes — it states what that role resolves to, not what remains once another role has taken someone — and the chains deliberately say nothing about whether the roles can be filled *simultaneously*, which is a question about assignment rather than about filters.

Because the report evaluates every filter candidate resolution applies, it MAY state that resources can provide the service. It SHALL NOT state or imply that the service is *available* — the chains say nothing about opening hours, lead time, booking horizon, existing bookings, or whether the roles' start grids ever coincide, and wording that suggests a bookable slot would over-claim.

Where a stage's count is not known — because the configuration is too incomplete to resolve, or the request failed — the report SHALL say nothing for that stage rather than reporting zero. Zero is the answer that tells an editor their configuration is wrong, so reporting it because a request failed sends them to correct something that is correct.

The report's wording SHALL be derived from state captured with the response it describes, never from the live form, so that it cannot momentarily assert a sentence that is false for the configuration it is reporting on.

#### Scenario: A chain is reported for each role
- **WHEN** a service is configured with a `room` role and a `therapist` role
- **THEN** the report shows one chain per role, each labelled with its resource type

#### Scenario: The chain is reported
- **WHEN** ten `room` resources exist, three carry `projector`, one of those can provide a fixed four-hour duration, and a role is configured that way
- **THEN** that role's chain shows ten, three, and one for the three stages

#### Scenario: The report refreshes as a requirement changes
- **WHEN** a user adds a further required capability to one role that only one resource carries
- **THEN** that role's chain updates without the user saving, and the other role's chain is unchanged

#### Scenario: The report refreshes when the duration changes
- **WHEN** a user changes the duration to a length no resource can provide
- **THEN** every role's chain updates to show its duration stage at zero, without the user saving or leaving the Duration group

#### Scenario: A mistyped resource type is not reported as a capability problem
- **WHEN** a role names a resource type no resource uses and also requires capabilities
- **THEN** that role's chain attributes the empty pool to the resource type, not to the capabilities

#### Scenario: An over-narrow capability set is attributed to the capabilities
- **WHEN** resources of a role's type exist but none carries a required capability
- **THEN** that role's chain shows the type stage populated and the capability stage at zero

#### Scenario: A duration no resource can provide is attributed to the duration
- **WHEN** resources carry a role's required capabilities but none can provide the configured length
- **THEN** that role's chain shows the capability stage populated and the duration stage at zero, and identifies the resources excluded and the bound that excluded them

#### Scenario: The report describes the type when no capabilities are required
- **WHEN** a role names a resource type, requires no capabilities, and some resources of that type cannot provide the duration
- **THEN** that role's chain describes how many resources have that **type** and how many of those can provide the service, and says nothing about required capabilities that were never named

#### Scenario: The report does not claim availability
- **WHEN** the report is displayed for any configuration
- **THEN** its wording describes what resources can provide, and does not state that the service is available, free, or bookable at any particular time

#### Scenario: A stage that is not known says nothing
- **WHEN** a role's resource type is empty, or the chains could not be retrieved
- **THEN** the report says nothing for that stage rather than reporting zero

#### Scenario: An unsaved service is reported
- **WHEN** a user edits the roles or duration of a service that has never been saved and has no name
- **THEN** the report still reports a chain per role for the configuration as currently entered

#### Scenario: The report never asserts a stale phrasing
- **WHEN** a user changes the configuration and the previous chains have not yet been replaced
- **THEN** the report continues to describe the configuration it was computed for, or says nothing, and never describes the new configuration using the old counts

### Requirement: The editor reports roles whose start times can never coincide
The services editor SHALL report, as a service is edited, when its roles can
never share a bookable start — the case where every role resolves healthily and
the service is nonetheless unbookable forever.

The report SHALL be presented **separately from the resolution chains**, not as
part of any role's chain. The chains describe which resources can provide the
service; this describes whether their start times can ever meet, which is a
different claim about different data.

The report SHALL name what an editor acts on: the two roles, the two resources,
and their opening times and granularities. The fix is to edit a **resource** —
its opening time or its granularity — or to add one that aligns, so a report
that named only the service would send an editor to the wrong screen.

The report SHALL appear only for a permanent misalignment. It SHALL NOT appear
because a service is fully booked, because a range contains no free time, or for
any condition that a different day or a cancelled booking would resolve.

The report SHALL NOT block saving, and SHALL NOT be presented as a validation
failure. A misaligned service is legitimate: the resources it needs may be added
or adjusted later, and nothing about the service itself is wrong.

The report SHALL NOT state or imply that a service **is** bookable or available
when it is absent. Its absence means only that no permanent misalignment was
found.

Where the finding is not known — the configuration is too incomplete, or the
request failed — the editor SHALL say nothing rather than implying either
answer, consistent with how the resolution chains treat a stage they cannot
report.

#### Scenario: A permanent misalignment is reported
- **WHEN** a service is configured with a `room` role whose resource opens at 09:00 in 30-minute steps and a `therapist` role whose resource opens at 09:15 in 20-minute steps
- **THEN** the editor reports that those two roles can never share a start, naming both resources with their opening times and step sizes

#### Scenario: The report is separate from the resolution chains
- **WHEN** a misalignment is reported
- **THEN** it is rendered outside the per-role chains, and no chain line mentions opening hours or start times

#### Scenario: An alignable configuration says nothing about alignment
- **WHEN** a service's roles can share a start
- **THEN** the editor shows no alignment report, and does not state that the service is bookable or available

#### Scenario: Saving is unaffected
- **WHEN** a user saves a service whose roles are permanently misaligned
- **THEN** the save succeeds, and the report is informational rather than a validation failure

#### Scenario: The report refreshes with the configuration
- **WHEN** a user changes a role's resource type so that the roles can now align
- **THEN** the report disappears without the user saving

#### Scenario: A busy week is not reported as a misalignment
- **WHEN** a service's roles can share a start but every such start is already booked
- **THEN** the editor shows no alignment report, because nothing about the configuration is permanently wrong

#### Scenario: An unknown finding says nothing
- **WHEN** the configuration is too incomplete to resolve, or the request for it fails
- **THEN** the editor says nothing about alignment rather than implying that the roles do or do not align

### Requirement: The editor reports roles whose pools cannot be filled together
The services editor SHALL report, as a service is edited, when the configuration's
roles cannot all be filled at once by the resources that exist — read from the
configuration preview's sufficiency member, never computed in the client, so the
editor cannot state something Core did not conclude.

It SHALL be presented at **form level**, beside the resolution chains and the
start-grid report, and SHALL NOT be attached to a requirement row. The finding
belongs to a set of roles rather than to one of them, and rendering it against a row
would tell an editor to correct a row that may be perfectly well formed — the fault
is as often a missing resource as a wrong count.

The report SHALL be **informational** and SHALL NOT block saving, because a
configuration whose pool is too small today is corrected as often by adding a
resource as by editing the service, and refusing the save would force the editor to
do those in one order.

The wording SHALL state what is required against what is eligible, and SHALL name
the roles involved.

It SHALL name them by the **requirement row** each belongs to: the editor has rows,
the fix for a role is on its row, and two roles of one resource type requiring the
same capabilities are distinguishable by nothing else. It SHALL do so for every
role it names, unconditionally — unlike the resolution chains, which state a row
only where two roles share a resource type. The finding is a **subset** of the
configuration, so asking whether the roles *it names* share a type asks the wrong
question: a service of two same-type rows may produce a finding naming one of them,
which would then read as unambiguous while leaving the editor unable to tell which
row is short.

A row SHALL be identified by the position the response reports for it, never by its
position within the finding — the finding being a subset, its second entry is not
the second row. It SHALL NOT state or imply that the service *is* available, free, or
bookable, and SHALL NOT state that a sufficient pool means the service can be booked
— eligibility is not availability, and this check evaluates neither opening hours,
lead time, horizon, nor the booking calendar.

The report SHALL say nothing when the roles can be filled together, and SHALL say
nothing when the configuration is too incomplete to resolve or the request failed,
rather than reporting a shortfall of zero. Reporting zero is the answer that tells an
editor their configuration is wrong.

The report's wording SHALL be derived from state captured with the response it
describes, never from the live form, so that it cannot momentarily assert a sentence
that is false for the configuration it is reporting on.

#### Scenario: An insufficient pool is reported
- **WHEN** an editor sets a requirement's count to 2 while only one resource is eligible for it
- **THEN** the editor states that the role requires 2 distinct resources and 1 is eligible, at form level, and saving remains available

#### Scenario: A sufficient pool is reported as nothing
- **WHEN** the configuration's roles can all be filled at once
- **THEN** the editor shows no sufficiency statement, and no wording suggesting the service is available or bookable

#### Scenario: The report does not block saving
- **WHEN** a service whose pool is insufficient is saved
- **THEN** the save succeeds, because the pool is a property of the resources rather than of the service

#### Scenario: The report is not attached to a requirement row
- **WHEN** two roles together cannot be filled
- **THEN** the statement appears once at form level naming both roles, and neither requirement row is marked as being in error

#### Scenario: Two roles identical in type and capabilities are still distinguishable
- **WHEN** two requirement rows name the same resource type and require the same capabilities, and cannot be filled together
- **THEN** the statement names each by its own requirement row rather than producing two identical entries

#### Scenario: A row above that is not yet filled in does not shift the numbering
- **WHEN** a requirement row above has no resource type entered yet, so the configuration reported on omits it
- **THEN** every reported role is still named by the requirement row it belongs to, not by its position among the roles reported

#### Scenario: An unresolvable configuration reports nothing rather than zero
- **WHEN** the configuration is too incomplete to resolve, or the preview request fails
- **THEN** the editor shows no sufficiency statement, rather than one reporting that zero resources are eligible

#### Scenario: Accessibility of the report
- **WHEN** the sufficiency statement is rendered
- **THEN** it is text within the form's own structure, every id it references resolves in the same shadow root, and it is not conveyed by colour alone
