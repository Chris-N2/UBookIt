## MODIFIED Requirements

### Requirement: The closures view

The package SHALL present closures in a backoffice view of their own within the uBookIt section,
listing each closure's date and label with actions to create, edit and delete. **Where the site has
registered a public holiday source and the user holds the verb that reaches it, the view SHALL also
carry the import control**; where either is missing, it SHALL carry no such control — the
enumeration of the view's actions is therefore conditional on the site, not fixed.

**The view SHALL show upcoming closures by default and SHALL offer past ones on request.** A past
closure is the record of why a date was shut and SHALL NOT be deleted automatically; the default
filter keeps a list that grows every year usable without destroying anything.

The view's markup SHALL meet the project's accessibility bar on the same terms as the rest of the
section: every input labelled, failures programmatically associated with the control they concern
and announced on failed save, full keyboard operability with visible focus, and semantic headings
and controls.

#### Scenario: Upcoming closures are shown by default
- **WHEN** the closures view is opened on a site holding both past and future closures
- **THEN** the future closures are listed, and the past ones are not, until they are asked for

#### Scenario: Past closures remain available
- **WHEN** past closures are requested
- **THEN** they are listed, and nothing has been deleted

#### Scenario: Keyboard-only management
- **WHEN** a user operates the closures view using only a keyboard
- **THEN** creating, editing, deleting and revealing past closures are all reachable and operable with visible focus, as are the import control and every row's selection where the import is present

#### Scenario: A failed save is announced
- **WHEN** a closure save fails validation
- **THEN** the failure is announced to assistive technology and associated with the control it concerns, and no entered data is lost

#### Scenario: The import control is part of the view's accessibility bar
- **WHEN** an import preview is displayed and operated using only a keyboard
- **THEN** the window inputs, the fetch, every selectable row and the confirm are labelled, reachable and operable with visible focus, on the same terms as the rest of the view

### Requirement: Closures are read and written by different verbs

**Writing the closure list SHALL require `UBookIt.Settings`.** Closing the whole organisation is a
site-level act, and the grant that means "may add a meeting room" SHALL NOT carry it.

**Reading the closure list SHALL require `UBookIt.Configure` or `UBookIt.Settings`.** An operator
editing a resource must be able to see what that resource is inheriting in order to exempt it, and
cannot do so without reading the list.

**Setting a resource's opt-outs SHALL require `UBookIt.Configure`** — the verb that governs every
other resource write, which is what exempting one resource is. It is deliberately NOT reachable
under `UBookIt.Settings`: that grant decides the site's policy, and applying an exemption to one
resource is a resource decision. A holder of `UBookIt.Settings` alone is therefore refused a
resource write, exactly as they are refused every other one.

**Previewing and importing public holidays SHALL require `UBookIt.Settings`**, on the same terms as
writing the list directly: an import creates closures, and the preview is the half of that act which
makes the site's own code reach outward on an operator's behalf.

Each of these SHALL be enforced by the server, whatever the client rendered.

**Where a user may read but not write, the package SHALL say so** rather than presenting controls
whose use would be refused. `UBookIt.Settings` is never seeded, so on an upgraded site nobody holds
it until it is granted — a closures view that merely showed inert buttons would read as a feature
that failed to ship.

**That rule is about the verb, and SHALL NOT be read as a rule about a capability the site does not
have.** A user who may read but not write is told that changing closures needs the settings grant,
and that one statement covers importing too, because importing is a way of changing them. It says
nothing about whether the site registered a holiday source, which is not a permission and is not
this user's to acquire. Where no source is registered the import is absent and unexplained for
everyone, whatever they hold — the two rules answer different questions and neither weakens the
other.

#### Scenario: Configure alone reads but cannot write
- **WHEN** a user whose groups hold only `UBookIt.Configure` reads the closure list and then attempts to create a closure
- **THEN** the list is served and the creation is refused

#### Scenario: Configure alone may still opt a resource out
- **WHEN** a user whose groups hold only `UBookIt.Configure` saves a resource with an opt-out
- **THEN** the write is served

#### Scenario: Settings alone cannot opt a resource out
- **WHEN** a user whose groups hold only `UBookIt.Settings` saves a resource carrying an opt-out
- **THEN** the write is refused, because saving a resource is a `Configure` act

#### Scenario: Settings reaches the writes
- **WHEN** a user whose groups hold `UBookIt.Settings` creates, edits and deletes a closure
- **THEN** each write is served

#### Scenario: Neither verb reaches nothing
- **WHEN** a user holding the section but neither `UBookIt.Configure` nor `UBookIt.Settings` requests the closure list
- **THEN** the request is refused

#### Scenario: The read-only state is explained
- **WHEN** a user who may read but not write opens the closures view
- **THEN** the view states that changing closures requires the settings grant, and where it is granted, rather than rendering controls that would be refused

#### Scenario: An administrator is not exempt
- **WHEN** an Umbraco administrator none of whose groups hold `UBookIt.Settings` attempts to create a closure
- **THEN** the request is refused

#### Scenario: Settings reaches the import
- **WHEN** a user whose groups hold `UBookIt.Settings` previews holidays and imports the rows they chose
- **THEN** both are served

#### Scenario: Configure alone reaches neither half of an import
- **WHEN** a user whose groups hold only `UBookIt.Configure` requests a holiday preview or an import
- **THEN** both are refused, and reading the closure list remains available to them

#### Scenario: The verb explanation covers the import without naming the source
- **WHEN** a user who may read but not write opens the closures view on a site that has registered a source
- **THEN** the view states that changing closures requires the settings grant, and presents no import control and no explanation of one

### Requirement: Closures are managed through versioned management endpoints

The Management API SHALL expose versioned endpoints for closures in the `ubookitbackoffice` swagger
group: list, create, update, delete, **a preview of what a registered holiday source offers for a
window, an import creating closures from chosen rows, and a probe reporting whether a source is
registered at all**. Request and response bodies SHALL be purpose-built DTO
models — domain types SHALL NOT appear in the HTTP contract, on the same terms as every other
management endpoint.

The list SHALL be filterable to upcoming closures or all closures, so that the view's default is
served by the server rather than by fetching everything and hiding some of it in the client.

Validation failures SHALL carry the stable codes this capability defines and SHALL be rendered as
problem details carrying a type member, on the same terms as the rest of the management surface. A
closure id that does not exist SHALL yield a 404 problem-details response, **and so SHALL a preview
or import on a site that registered no source** — every 404 this capability returns is problem
details, so a client never has to tell one shape of absence from another.

#### Scenario: Closures round-trip through the API
- **WHEN** a closure is created and the list is then read
- **THEN** the closure appears with the date and label it was created with, and a stable id

#### Scenario: The list serves the upcoming filter
- **WHEN** the list is requested filtered to upcoming closures on a site holding past and future ones
- **THEN** only the future closures are returned, and the filtering is performed by the server

#### Scenario: A validation failure is problem details
- **WHEN** a closure is submitted with a blank label
- **THEN** the response is a problem-details body carrying a type member and the `closure-label-invalid` code

#### Scenario: An unknown closure id is a 404
- **WHEN** an update or delete names a closure id that does not exist
- **THEN** the response is a 404 problem-details body

#### Scenario: The import endpoints are absent without a source
- **WHEN** a preview or an import is requested on a site that has registered no holiday source
- **THEN** each responds 404 with a problem-details body, and no closure is created

#### Scenario: Absence is reported behind the verb, not in front of it
- **WHEN** any of the three holiday endpoints is called without backoffice authentication
- **THEN** the response is 401, whether or not a source is registered, because authentication precedes the question
