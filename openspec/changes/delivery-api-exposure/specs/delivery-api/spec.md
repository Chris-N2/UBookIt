# Delta for delivery-api

Guarantee-diff note for the MODIFIED requirement: every auth-stance SHALL (no backoffice
policy, no anti-forgery, body-only identity) and all three scenarios are carried forward;
the only change is that reachability is scoped to a direction the site has enabled — the
two read scenarios gain that condition in their WHEN, and nothing else moves.

## ADDED Requirements

### Requirement: The delivery API is off until a site turns it on

The delivery API SHALL be exposed in two independently switchable directions — **reads**
(resource and service discovery, availability, slots, bookable-starts, and the retention
read) and **placement** (anonymous booking placement, direct and via service) — and both
SHALL be **off by default**: a fresh install, and an upgrade that changes no
configuration, SHALL serve no delivery endpoint at all.

**This deliberately reverses the original always-on registration**, and the reversal is a
breaking change for existing consumers of the API, accepted and documented as such: the
package's rule is that nothing is exposed or sent until a site asks, an anonymous public
API is the package's largest unasked-for surface, and no request-time validation can
substitute for absence — an anonymous API has no way to know who is calling.

Each direction SHALL be enabled by its own explicit configuration setting, read at
startup. Neither direction SHALL imply the other, and enabling either SHALL NOT change
what the shipped Razor front end does — it renders in-process from the Core ports, as
the `default-frontend` capability requires, and functions identically with the API off.

Every delivery endpoint SHALL belong to exactly one direction, and the classification
SHALL be total: an endpoint that has not been classified SHALL be a test failure, never
an exposed default.

#### Scenario: An untouched install serves nothing

- **WHEN** no delivery API configuration is present and any delivery endpoint is
  requested
- **THEN** the response is the host's ordinary not-found response

#### Scenario: Reads can be enabled without placement

- **WHEN** a site enables the read direction only
- **THEN** availability and discovery requests are served, and a placement request
  receives the host's ordinary not-found response

#### Scenario: Placement can be enabled without reads

- **WHEN** a site enables the placement direction only
- **THEN** a valid placement request is served, and an availability request receives the
  host's ordinary not-found response

#### Scenario: Both directions enabled is the full API

- **WHEN** a site enables both directions
- **THEN** every delivery endpoint behaves exactly as this capability's other
  requirements describe

#### Scenario: Every endpoint is classified

- **WHEN** the delivery API's actions are enumerated
- **THEN** each carries exactly one direction, and an action carrying none or both is
  reported as a failure naming it

### Requirement: A disabled direction is absent, not refused

A request to an endpoint of a disabled direction SHALL receive the host's ordinary
not-found response — the same status, shape and headers as a route that never existed —
and SHALL NOT receive any status, header, body member or timing signal that
distinguishes "disabled" from "never existed". The operations of a disabled direction
SHALL NOT appear in the delivery API's OpenAPI document.

**Because absence is the guarantee, refusal is information.** A 403 or a
problem-details body saying "disabled" tells an unauthenticated stranger that the
package is installed and the endpoint exists to be turned on; a plain 404 says nothing.
The OpenAPI document follows for the same reason: it is the API's public inventory, and
an inventory listing endpoints a site has switched off would advertise the surface the
switch exists to remove.

#### Scenario: Disabled and non-existent are indistinguishable

- **WHEN** a disabled direction's endpoint is requested and a genuinely non-existent
  route under the same path prefix is requested
- **THEN** the two responses are indistinguishable in status and shape

#### Scenario: The OpenAPI document lists only what is on

- **WHEN** the delivery OpenAPI document is generated on a site with only the read
  direction enabled
- **THEN** it contains the read operations and no placement operation

#### Scenario: Everything off is an empty inventory

- **WHEN** the delivery OpenAPI document is generated on a site with no delivery
  configuration
- **THEN** it contains no operations

## MODIFIED Requirements

### Requirement: Anonymous access and auth stance
Delivery API endpoints, **where their direction is enabled**, SHALL be reachable anonymously — they SHALL NOT be protected by a backoffice authorization policy. They SHALL NOT require, issue, or validate a cookie-based anti-forgery token. Write endpoints SHALL NOT derive booker or member identity from an ambient authentication cookie; all booker identity SHALL come from the request body. This keeps every *alternative* UI (a separate-repo DevExpress UI, a SPA, a mobile client) a symmetric consumer of the same contract. Whether a direction is exposed at all is the exposure requirement's concern; this requirement governs how an exposed endpoint behaves, and **anonymity is a property of the enabled API, never a promise that the API is enabled**.

#### Scenario: Anonymous read succeeds
- **WHEN** an unauthenticated caller requests availability for a resource on a site that has enabled the read direction
- **THEN** the request is served (no authentication challenge)

#### Scenario: Anonymous placement succeeds
- **WHEN** an unauthenticated caller posts a valid booking with body-only contact details and no anti-forgery token, on a site that has enabled the placement direction
- **THEN** the booking is placed and the request is not rejected for missing authentication or a missing token

#### Scenario: Ambient identity is not trusted for writes
- **WHEN** a placement request arrives carrying an ambient authentication cookie, on a site that has enabled the placement direction
- **THEN** the placed booking's booker reflects only the request body, and no member key is inferred from the ambient context
