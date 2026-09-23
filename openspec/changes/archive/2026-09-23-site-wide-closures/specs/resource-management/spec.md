## MODIFIED Requirements

### Requirement: Resource CRUD endpoints
The Management API SHALL expose versioned endpoints in the `ubookitbackoffice` swagger group: paged list (`GET`, with skip/take and total count), get by id, create, full update, and delete. Request and response bodies SHALL be purpose-built DTO models — domain types SHALL NOT appear in the HTTP contract. The full availability model (opening hours, exceptions, constraints) and the resource's capability set SHALL be readable and writable through these endpoints. An omitted capability collection SHALL be treated as empty, and a full update SHALL replace the capability set rather than merging into it, consistent with the full-update semantics of the rest of the model.

**The request model SHALL additionally carry the resource's site-closure opt-outs, as closure ids.** An omitted collection SHALL be treated as none, and a full update SHALL replace the opt-out set rather than merging into it, on the same full-replacement terms as the capability set. An opt-out naming a closure that does not exist SHALL be refused with the stable code `closure-not-found`.

**The response model SHALL additionally carry the site closures that apply to the resource**, each with its id, date, label, and whether this resource is excluded from it. **Where the resource carries an override exception on a closure date it is not excluded from, the response SHALL mark that exception as superseded.** The marking SHALL be made only where the outcome differs — an exception that is itself a closure on a closure date closes the date either way and SHALL NOT be marked — so that the precedence rule is evaluated once, by the server, and not re-derived by each client.

The projected closures and the superseded marking SHALL be read-only on the response: the only closure state a resource writes is its opt-out set.

#### Scenario: Round-trip through the API
- **WHEN** a resource is created with opening hours, an exception, custom constraints, and two capabilities, then fetched by id
- **THEN** the response contains the same details, opening hours, exception, constraint values, and both capabilities

#### Scenario: Paged list
- **WHEN** 25 resources exist and the list endpoint is called with skip 20, take 10
- **THEN** the response contains 5 items and reports a total of 25

#### Scenario: Update replaces the capability set
- **WHEN** a resource carrying `cert-x` and `massage` is updated with a body listing only `massage`
- **THEN** the stored resource carries only `massage`

#### Scenario: Omitted capabilities mean none
- **WHEN** a resource is created with no capability collection in the body
- **THEN** creation succeeds and the resource carries no capabilities

#### Scenario: Malformed capability key is rejected
- **WHEN** a resource is created with the capability key `"Cert X"`
- **THEN** the request fails validation with the `capability-key-invalid` code

#### Scenario: Opt-outs round-trip
- **WHEN** a resource is updated with an opt-out naming an existing closure, then fetched by id
- **THEN** the response reports that closure as one this resource is excluded from

#### Scenario: Update replaces the opt-out set
- **WHEN** a resource holding two opt-outs is updated with a body naming only one
- **THEN** the stored resource holds only that one

#### Scenario: Omitted opt-outs mean none
- **WHEN** a resource is updated with no opt-out collection in the body
- **THEN** the update succeeds and the resource is excluded from no closure

#### Scenario: An opt-out naming no closure is rejected
- **WHEN** a resource is updated with an opt-out naming an id no closure carries
- **THEN** the request fails validation with the `closure-not-found` code and the resource is unchanged

#### Scenario: The response marks a superseded exception
- **WHEN** a resource carrying an override exception on a closure date it is not excluded from is fetched
- **THEN** the response marks that exception as superseded

#### Scenario: A closure exception on a closure date is not marked superseded
- **WHEN** a resource whose own exception for a closure date is itself a closure is fetched
- **THEN** the response does not mark that exception as superseded

### Requirement: Workspace editor for a resource
Editing SHALL happen in a separate editor view (not inline in the table) with grouped sections for Details, Opening hours (add/remove window rows per weekday), Exceptions (date with closure or override windows), **Global closures**, Constraints, and Capabilities. The Capabilities section SHALL let a user add and remove capability keys, offering those already in use while permitting a new one. Saving SHALL submit the full resource, surface per-field validation failures using the returned codes, and return the user to an accurate collection view.

**The Global closures section SHALL list every site closure that applies to the resource**, showing each closure's date and its label, with a control that excludes this resource from that closure. It SHALL make clear that these dates originate outside the resource, so that an operator can tell an inherited closure from one the resource carries itself.

**Where the response marks an exception as superseded, the editor SHALL say so**, programmatically associated with the exception it concerns, and SHALL present the state the server reported rather than deriving it from the closure list.

#### Scenario: Editing availability end-to-end
- **WHEN** a user opens a resource, adds a Tuesday 09:00–17:00 window and a closure exception, and saves
- **THEN** the save succeeds and reopening the resource shows both changes

#### Scenario: Validation failure is surfaced per field
- **WHEN** a user saves a resource with an overlapping window
- **THEN** the editor shows the failure associated with the offending group and no data is lost from the form

#### Scenario: Editing capabilities end-to-end
- **WHEN** a user opens a resource, adds the capability `projector`, and saves
- **THEN** the save succeeds and reopening the resource shows `projector`

#### Scenario: Removing a capability
- **WHEN** a user removes a capability from a resource and saves
- **THEN** the resource no longer carries it, and services requiring it no longer match that resource

#### Scenario: Capability validation failure is surfaced on its own control
- **WHEN** a user saves a resource with a malformed capability key
- **THEN** the editor surfaces the `capability-key-invalid` failure associated with the Capabilities section and no data is lost from the form

#### Scenario: Inherited closures are shown with their labels
- **WHEN** a user opens a resource on a site holding closures
- **THEN** the Global closures section lists each closure's date and label, identifiably as dates originating outside this resource

#### Scenario: Opting out end-to-end
- **WHEN** a user excludes the resource from one closure and saves
- **THEN** the save succeeds, reopening shows the exclusion, and that date offers availability again

#### Scenario: A superseded exception is stated in the editor
- **WHEN** a resource carrying an override exception on a non-excluded closure date is opened
- **THEN** the editor states that the exception is currently superseded, associated with that exception

#### Scenario: The editor does not compute precedence itself
- **WHEN** the editor's handling of the superseded state is inspected
- **THEN** it renders what the response reported, rather than comparing the closure list against the exception set
