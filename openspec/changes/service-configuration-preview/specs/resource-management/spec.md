## REMOVED Requirements

### Requirement: Role match preview endpoint
**Reason**: Superseded by the service configuration preview endpoint, which answers a strictly larger question. The removed endpoint applied the capability rule itself, which is the second implementation of eligibility this change exists to eliminate; keeping both would reinstate the drift risk the replacement removes. It had exactly one consumer, and nothing is published.

**Migration**: Callers move to the service configuration preview endpoint, which takes a role **and** a duration specification and returns the full resolution chain. A caller wanting only the previous behaviour supplies an unbounded variable duration, whose final stage then differs from the capability stage only for resources whose own constraints are internally incoherent.

## ADDED Requirements

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
