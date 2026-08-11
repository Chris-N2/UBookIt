## ADDED Requirements

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

## MODIFIED Requirements

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
