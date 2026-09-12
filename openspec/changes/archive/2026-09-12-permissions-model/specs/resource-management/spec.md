# Delta for resource-management

Guarantee-diff note: every SHALL and the first two scenarios are carried verbatim. The
third scenario — "Access to the package's section is sufficient" — is the sentence the
`permissions` capability falsifies, and it is superseded rather than dropped: sufficiency
narrows to the section *policy's* question (no OTHER section is required), while access to
an endpoint now also requires the relevant verb. Nothing else moves.

## MODIFIED Requirements

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
