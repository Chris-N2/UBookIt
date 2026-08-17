## ADDED Requirements

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
