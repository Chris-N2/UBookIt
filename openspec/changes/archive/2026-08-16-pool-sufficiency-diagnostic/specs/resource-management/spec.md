## ADDED Requirements

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

## MODIFIED Requirements

### Requirement: Service configuration preview endpoint
The Management API SHALL expose a versioned endpoint in the `ubookitbackoffice` swagger group that, given **a list of roles** (each a resource type key, a set of required capability keys, and a count) and a duration specification, returns **one resolution chain per role**: the resources of that role's type, those of them satisfying its capabilities, those of them whose constraints admit a permitted length, and the resources excluded at the duration stage with the bound that excluded them. Each chain SHALL identify the role it describes, and the chains SHALL be returned in the order the roles were supplied.

**BREAKING (unpublished):** the request carries a list of roles and the response a chain per role, replacing the single-role request and single chain.

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
