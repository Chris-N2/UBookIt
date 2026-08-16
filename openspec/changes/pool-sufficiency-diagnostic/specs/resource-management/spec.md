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

#### Scenario: The chains are not altered by the finding
- **WHEN** a configuration is previewed whose two roles each report two eligible resources but which share the same two
- **THEN** each chain still reports two, and the sufficiency member carries the joint claim

#### Scenario: An insufficient configuration is still previewed
- **WHEN** a configuration whose pools are insufficient is previewed
- **THEN** the endpoint returns 200 with the chains and the finding, rather than rejecting the request

#### Scenario: Authorization is unchanged
- **WHEN** the endpoint is called without backoffice authentication
- **THEN** the request receives 401 and does not reach handler logic
