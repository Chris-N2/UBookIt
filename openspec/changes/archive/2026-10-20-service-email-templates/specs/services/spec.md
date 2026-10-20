## ADDED Requirements

### Requirement: A service may name an email template set

A `Service` SHALL carry an optional **email template set**: a short name that the
`email-templates` capability uses to find content written for this service before content
written for every service. Several services MAY name the same set.

The name SHALL be normalised the same way wherever it is set:
- leading and trailing whitespace SHALL be removed;
- a value that is empty or only whitespace SHALL mean **no set**, and SHALL be held as absent
  rather than as an empty string;
- the value SHALL be converted to **lower case** using culture-invariant rules.

**Lower case is enforced because the name becomes a folder name.** Folder names are
case-sensitive on some hosts and not on others, so a name whose case could differ from its folder
would work where it was written and fall back silently where it was deployed.

After normalisation the name SHALL consist only of the characters `a`–`z`, `0`–`9` and `-`. It
SHALL begin and end with a letter or digit, SHALL NOT contain two hyphens in a row, and SHALL be
at most **64 characters**. Any other value SHALL be rejected with the stable code
`service-template-set-invalid`, carrying the field `templateSet`, as a validation failure and
never as a database error. **The name is used as a path segment, so no accepted value SHALL be
able to name a location outside the folder the sets live in.**

Adding the template set SHALL NOT change how an existing caller constructs a service. Code that
created a service before this requirement existed SHALL compile and behave unchanged, and SHALL
produce a service with no template set.

The Management API SHALL carry the template set on the service request and response as
`templateSet`. In the response it SHALL always be present, carrying `null` where the service has
none. A service's template set SHALL round-trip through create, get and update, in its normalised
form. A request failing both this rule and another service rule SHALL report both failures in the
same response.

#### Scenario: A template set round-trips through the API
- **WHEN** a service is created with template set `remote`, then fetched by id
- **THEN** the response carries `templateSet` as `remote`

#### Scenario: Upper case is lowered
- **WHEN** a service is saved with template set `Remote-Consult`
- **THEN** the service's template set is `remote-consult`, and the response carries that value

#### Scenario: Surrounding whitespace is trimmed
- **WHEN** a service is saved with template set `  remote  `
- **THEN** the service's template set is `remote`

#### Scenario: Whitespace alone means no set
- **WHEN** a service is saved with a template set consisting only of whitespace
- **THEN** the service has no template set, and the response carries `templateSet` as `null`

#### Scenario: A service without a set reports null, not an omission
- **WHEN** a service saved without a template set is fetched by id
- **THEN** the response carries `templateSet` with the value `null`

#### Scenario: A name that could leave the folder is rejected
- **WHEN** a service is submitted with template set `../partials`, `a/b`, `a\b` or `.`
- **THEN** the response is 400 and its errors carry `service-template-set-invalid` for the field `templateSet`, and nothing is stored

#### Scenario: Characters outside the rule are rejected
- **WHEN** a service is submitted with template set `in person`, `-remote`, `remote-`, `re--mote` or `café`
- **THEN** the response is 400 and its errors carry `service-template-set-invalid` for the field `templateSet`, and nothing is stored

#### Scenario: Length is bounded
- **WHEN** a service is submitted with a template set of 65 valid characters, and another with exactly 64
- **THEN** the first is rejected with `service-template-set-invalid` and the second is saved

#### Scenario: Two failures are reported together
- **WHEN** a service is submitted with an empty name and template set `../x`
- **THEN** the 400 response carries both `service-name-required` and `service-template-set-invalid`

#### Scenario: Removing the set
- **WHEN** a service that has a template set is updated with `templateSet` null or blank
- **THEN** the service has no template set afterwards

#### Scenario: Two services share a set
- **WHEN** two services are saved with the same template set
- **THEN** both are saved, and both carry it

#### Scenario: An existing way of creating a service is unchanged
- **WHEN** a service is created through the factory exactly as code written before this requirement did
- **THEN** it compiles, succeeds or fails exactly as before, and the resulting service has no template set

## MODIFIED Requirements

### Requirement: Workspace editor for a service
Editing a service SHALL happen in a workspace editor with grouped sections for Details (name, instructions for the booker, and email template set), Service Requirements, and Duration. Saving SHALL submit the full service via the management API's create or update endpoint, return the user to an accurate collection view on success, and on failure SHALL surface the returned failure codes without losing any form state.

The instructions SHALL be edited in a multi-line text field labelled **Instructions for the booker**. Its programmatically associated hint SHALL say that the text is sent to the booker once their booking is confirmed, and is not shown on the public site. The field SHALL be optional. A failure carrying `service-booker-instructions-too-long` SHALL be shown against this field.

The template set SHALL be edited in a single-line text field labelled **Email template set**. Its programmatically associated hint SHALL say that the set selects email content for this service from a folder of the same name, that the name is saved in lower case, and which characters it may contain. The field SHALL be optional. A failure carrying `service-template-set-invalid` SHALL be shown against this field. After a save, the field SHALL show the name as stored, so an editor who typed upper case sees the folder name that will be used.

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

#### Scenario: A template set is edited with the service
- **WHEN** a user enters `Remote` as the template set for a service and saves
- **THEN** the save succeeds and the field shows `remote`, both straight after the save and on reopening the service

#### Scenario: An invalid template set is reported against its field
- **WHEN** a save fails with `service-template-set-invalid`
- **THEN** the failure is shown against the template set field, which is described by it, and every value the user had entered is still present
