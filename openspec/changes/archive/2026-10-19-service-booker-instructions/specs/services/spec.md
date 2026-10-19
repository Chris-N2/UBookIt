## ADDED Requirements

### Requirement: A service may carry instructions for the booker

A `Service` SHALL carry optional **instructions for the booker**: plain text a site writes once
for the service, which the `booking-emails` capability passes to a confirmed booker. Joining
details for an online session and directions for an in-person one are the cases it exists for.

The instructions SHALL be normalised the same way wherever they are set:
- leading and trailing whitespace SHALL be removed;
- a value that is empty or only whitespace SHALL mean **no instructions**, and SHALL be held as
  absent rather than as an empty string;
- line breaks within the text SHALL be preserved.

Instructions longer than **2000 characters** after normalisation SHALL be rejected with the stable
code `service-booker-instructions-too-long`, carrying the field `bookerInstructions`. They SHALL be
rejected as a validation failure, never as a database error.

The instructions SHALL be plain text. The package SHALL NOT interpret markup in them. Where a
message is HTML, the text SHALL be encoded like any other value.

Adding the instructions SHALL NOT change how an existing caller constructs a service. Code that
created a service before this requirement existed SHALL compile and behave unchanged, and SHALL
produce a service with no instructions.

The Management API SHALL carry the instructions on the service request and response as
`bookerInstructions`. In the response it SHALL always be present, carrying `null` where the service
has none, so a client never has to treat an absent member as a default. A service's instructions
SHALL round-trip through create, get and update.

#### Scenario: Instructions round-trip through the API
- **WHEN** a service is created with instructions "Join on Teams: https://example.test/meet" over two lines, then fetched by id
- **THEN** the response carries the same text with its line break preserved

#### Scenario: Surrounding whitespace is trimmed
- **WHEN** a service is saved with instructions that begin and end with spaces and blank lines
- **THEN** the stored instructions are the text without them

#### Scenario: Whitespace alone means no instructions
- **WHEN** a service is saved with instructions consisting only of whitespace
- **THEN** the service has no instructions, and the response carries `bookerInstructions` as `null`

#### Scenario: A service without instructions reports null, not an omission
- **WHEN** a service saved without instructions is fetched by id
- **THEN** the response carries `bookerInstructions` with the value `null`

#### Scenario: Over-long instructions are rejected as validation
- **WHEN** a service is submitted with instructions of 2001 characters after trimming
- **THEN** the response is 400 and its errors carry `service-booker-instructions-too-long` for the field `bookerInstructions`, and nothing is stored

#### Scenario: Instructions of exactly the limit are accepted
- **WHEN** a service is submitted with instructions of exactly 2000 characters after trimming
- **THEN** the service is saved with those instructions

#### Scenario: Removing the instructions
- **WHEN** a service that has instructions is updated with `bookerInstructions` null or blank
- **THEN** the service has no instructions afterwards

#### Scenario: An existing way of creating a service is unchanged
- **WHEN** a service is created through the factory exactly as code written before this requirement did
- **THEN** it compiles, succeeds or fails exactly as before, and the resulting service has no instructions

## MODIFIED Requirements

### Requirement: Workspace editor for a service
Editing a service SHALL happen in a workspace editor with grouped sections for Details (name, and instructions for the booker), Service Requirements, and Duration. Saving SHALL submit the full service via the management API's create or update endpoint, return the user to an accurate collection view on success, and on failure SHALL surface the returned failure codes without losing any form state.

The instructions SHALL be edited in a multi-line text field labelled **Instructions for the booker**. Its programmatically associated hint SHALL say that the text is sent to the booker once their booking is confirmed, and is not shown on the public site. The field SHALL be optional. A failure carrying `service-booker-instructions-too-long` SHALL be shown against this field.

#### Scenario: Creating a service end-to-end
- **WHEN** a user creates a service named "Deep tissue massage" requiring resource type `masseur` with a fixed duration of 60 minutes and saves
- **THEN** the save succeeds, the collection view lists the new service, and reopening it shows all three values as entered

#### Scenario: Editing an existing service
- **WHEN** a user opens an existing service, changes its name and duration, and saves
- **THEN** the save succeeds and reopening the service shows both changes

#### Scenario: Validation failure preserves the form
- **WHEN** a save fails validation (for example an empty name yielding `service-name-required`)
- **THEN** the editor shows the failure and every value the user had entered is still present in the form

#### Scenario: Instructions are edited with the service
- **WHEN** a user enters instructions over two lines for a service and saves
- **THEN** the save succeeds and reopening the service shows the instructions with their line break

#### Scenario: Over-long instructions are reported against their field
- **WHEN** a save fails with `service-booker-instructions-too-long`
- **THEN** the failure is shown against the instructions field, which is described by it, and every value the user had entered is still present
