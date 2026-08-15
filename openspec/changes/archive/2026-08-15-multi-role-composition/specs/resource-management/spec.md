## MODIFIED Requirements

### Requirement: Service configuration preview endpoint
The Management API SHALL expose a versioned endpoint in the `ubookitbackoffice` swagger group that, given **a list of roles** (each a resource type key and a set of required capability keys) and a duration specification, returns **one resolution chain per role**: the resources of that role's type, those of them satisfying its capabilities, those of them whose constraints admit a permitted length, and the resources excluded at the duration stage with the bound that excluded them. Each chain SHALL identify the role it describes, and the chains SHALL be returned in the order the roles were supplied.

**BREAKING (unpublished):** the request carries a list of roles and the response a chain per role, replacing the single-role request and single chain.

It SHALL require backoffice authorization, SHALL be read-only, and SHALL accept a configuration that no saved service holds — its purpose is to report on a service being edited, before it is saved and while it may still be incomplete. It SHALL NOT require the configuration to be a valid service; in particular it SHALL NOT require a service name.

The endpoint SHALL NOT reject a configuration whose roles duplicate a resource type. Saving such a service is rejected, but previewing one is how an editor sees what each role resolves to while correcting it, and refusing to answer would withhold the information needed to fix the fault. Each role's chain SHALL be reported independently, exactly as supplied.

The endpoint SHALL obtain its answer from Core's resolution rather than computing eligibility itself, so that the backoffice cannot report a different answer from the one the booking path will act on. It SHALL be a `POST`: a duration specification is a structured value carrying a kind and whichever bounds apply, and flattening it into query parameters would reproduce the ambiguity the duration value object exists to prevent.

An empty required-capability set SHALL match every resource of the given type. An unknown or not-yet-used type key SHALL yield a chain whose first stage is empty, not an error. A malformed type key or capability key SHALL be a validation failure carrying the same stable code as elsewhere, identifying which role it came from. An empty role list SHALL be a validation failure.

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

#### Scenario: Authorization is required
- **WHEN** the endpoint is called without backoffice authentication
- **THEN** the request receives 401 and does not reach handler logic
