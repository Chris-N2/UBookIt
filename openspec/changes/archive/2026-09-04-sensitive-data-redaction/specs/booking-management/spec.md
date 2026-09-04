## MODIFIED Requirements

### Requirement: Bookings are readable over an authorized management endpoint
The package SHALL expose a **versioned** backoffice endpoint, **in the same swagger group as
its other management endpoints**, returning the **windowed, paged, filtered** list of bookings
the management read port provides.

The endpoint SHALL require backoffice authorization on the same terms as every other
uBookIt management endpoint. **It SHALL NOT be reachable anonymously under any configuration
the package ships.**

**Request and response bodies SHALL be purpose-built models.** **Domain types SHALL NOT appear
in the HTTP contract**, on the same terms as the resource and service endpoints.

The response SHALL carry, per booking: the booking's identity, **its quotable reference**, its
interval and the time zone it was made in, its status, when it was created, **the booker's
contact details where the caller may see them**, each claimed resource's id and name, and
**the service it was placed for, or null**. It SHALL report the unpaged total alongside the
page, so a caller can render a pager.

**The response SHALL carry what the read port supplies, less what the caller may not see, and
never more.** The subtraction is the whole of the difference: no field is added here that the
port cannot fill, and no field the port fills reaches a caller who is not permitted it. A
field the port does not carry is one the screen must obtain another way, which is how a
second read path into bookings begins.

**Booker contact details SHALL cross the boundary as a single nullable object**, on the same
terms and for the same reason as the service: a null member says "these were withheld from
you", where two parallel nullable strings could be observed half-populated and leave a client
with no correct reading. **A null booker SHALL NOT be read as a booking without one** — the
domain requires a name and an email of every booker, so the state is not expressible.

**Whether the caller may see them SHALL be decided from Umbraco's sensitive-data access**, per
the `sensitive-data` capability, and SHALL be settled before the response is composed. The
endpoint SHALL NOT return the details and rely on a client to suppress them.

**The reference SHALL cross the boundary in canonical form.** How it is grouped for reading is
the client's decision, and a client that searches or compares needs the value exactly as it is
stored — the same reasoning that keeps widgets out of every other contract this package
publishes. **The reference is not personal data and SHALL NOT be withheld**: it identifies a
booking, not a person, and withholding it would leave a row with nothing to call it by.

**The service SHALL cross the boundary as a single nullable object carrying both id and
name**, rather than as two parallel nullable fields. "A booking has a service, or it does
not" is then expressed in the shape, rather than as a rule that two fields must be null
together — which a client can observe violated and has no way to interpret.

#### Scenario: A page of bookings is returned with its total
- **WHEN** an authorized caller with sensitive-data access requests bookings for a window
- **THEN** the matching page is returned with the unpaged total, each booking carrying its reference, interval, time zone, status, creation time, its booker's name and email, its resources with their names, and its service where it has one

#### Scenario: A caller without sensitive-data access receives the same rows without contact details
- **WHEN** an authorized caller lacking sensitive-data access requests bookings for a window
- **THEN** the same bookings are returned with the same total, each carrying its reference, interval, time zone, status, creation time, resources and service, and a null booker member

#### Scenario: A withheld booker leaves nothing behind in the payload
- **WHEN** a booking's booker is withheld
- **THEN** neither the name nor the email appears anywhere in the response, including as an empty or masked value

#### Scenario: A directly placed booking carries a null service
- **WHEN** a returned booking was placed directly
- **THEN** its service member is null, rather than an object with empty or placeholder values

#### Scenario: The endpoint is not anonymous
- **WHEN** the endpoint is called without backoffice authentication
- **THEN** the response is 401 and no handler logic executes

#### Scenario: No domain type crosses the HTTP boundary
- **WHEN** the endpoint's request and response models are inspected
- **THEN** they are purpose-built models, and no domain or store type appears in the contract

#### Scenario: The reference crosses the boundary unformatted
- **WHEN** a booking's reference is returned by the endpoint
- **THEN** it is the canonical stored value, leaving the client to group it for display

#### Scenario: The reference survives withholding
- **WHEN** a booking's booker is withheld from the caller
- **THEN** its reference is still returned, in canonical form

### Requirement: Bookings have a backoffice collection view
The package SHALL register a **Bookings** view in the uBookIt backoffice section, beside the
resource and service views and under the same section condition, listing the bookings the
management endpoint returns.

The list SHALL be a semantic table built from the backoffice UI library, showing per booking:
**its quotable reference**, when it runs, **the booker where the endpoint supplied one**, the
resources it claims by name, the service it was placed for, and its status. **The reference
SHALL come first**, because it is the column an operator scans while somebody reads it out —
which is the case the whole identifier exists for. It SHALL show the unpaged total and page
through results, in the same idiom the section's existing lists use.

**A withheld booker SHALL be rendered as a stated absence, not as an empty cell.** The view
SHALL show a localized indication that contact details are hidden, for the same reason a
booking with no service says so rather than leaving its cell blank: a blank cell reads as
missing data, and here it would read as a defect in the package.

**Where any row's booker is withheld, the view SHALL explain why**, once, where an operator
reading the list will see it — naming Umbraco's Sensitive data group as what grants access.
An operator who has just been made an administrator and finds every contact detail hidden has
no way to reach that explanation from the screen otherwise, and the most likely next action is
a defect report. The explanation SHALL be exposed to assistive technology on the same terms as
the view's other messages, rather than only rendered.

**The view SHALL NOT compute anything the endpoint does not return.** A value the endpoint
cannot supply is a finding about the endpoint, not a calculation to add to a screen — the
same rule the endpoint already carries about fields the read port cannot supply, pointed one
layer further out. **It SHALL NOT ask any other source whether contact details may be shown**:
the response already says so by what it carries, and a second source could disagree with the
first while looking authoritative.

**Formatting a value the endpoint did return is not computing one.** The reference crosses
the boundary canonical and is grouped for reading on the way to the screen, exactly as an
instant crosses as UTC and is rendered in the booking's zone. The rule above is about the
view inventing data; presentation of data it was given is the view's own business, and
stating that here keeps a reader of this requirement alone from seeing a prohibition being
broken.

No third-party widget framework SHALL be used, and every string the view displays SHALL come
from the package's localization with `en-US` provided.

#### Scenario: The section lists bookings
- **WHEN** a backoffice user with access to the uBookIt section and to sensitive data opens the Bookings view
- **THEN** bookings in the default window are listed with their reference, time, booker, resources, service and status, and the unpaged total is shown

#### Scenario: A withheld booker says so
- **WHEN** a listed booking's booker was withheld by the endpoint
- **THEN** the cell states that contact details are hidden, rather than being blank, and the row is still identifiable by its reference

#### Scenario: The operator is told what grants access
- **WHEN** any row on the page has a withheld booker
- **THEN** the view explains once that contact details require membership of Umbraco's Sensitive data group, exposed to assistive technology rather than only rendered

#### Scenario: A page with nothing withheld carries no explanation
- **WHEN** every row's booker was supplied
- **THEN** the explanation is not shown

#### Scenario: Visibility is read from the response, not asked elsewhere
- **WHEN** the view's decision to show or hide contact details is inspected
- **THEN** it follows from what the endpoint returned, with no separate query about the current user's permissions

#### Scenario: Paging reaches the rest
- **WHEN** more bookings match than fit on one page
- **THEN** the remaining bookings are reachable, and the total reported is the number matching the query rather than the number on the page

#### Scenario: A booking placed directly says so
- **WHEN** a listed booking has no service
- **THEN** the view states that it was booked directly, rather than leaving the cell blank as though the value were missing

## ADDED Requirements

### Requirement: A booking is identified to an operator by its reference
Wherever the bookings view names a particular booking — the accessible name of a per-row
control, and the confirmation shown before an action is carried out — it SHALL identify that
booking by its **quotable reference**.

**It SHALL do so for every operator, not only where the booker is withheld.** A screen that
names the booker when it can and the reference when it cannot has two behaviours to reason
about and two to test, and the branch exists only to preserve a habit. The reference is also
the better identifier for the purpose: it is unique, where two bookings may share a booker's
name, and it is what the person on the telephone is holding.

This restates nothing about *when* an action is offered, refused or confirmed — those are the
cancellation requirement's, unchanged. It constrains only what the operator is shown the
booking as.

#### Scenario: A per-row control is distinguishable
- **WHEN** a screen reader user reaches a per-row control in the bookings list
- **THEN** its accessible name identifies the booking by its reference, distinguishing it from the same control on every other row

#### Scenario: A confirmation names the booking by reference
- **WHEN** an operator is asked to confirm an action on a booking
- **THEN** the booking is identified by its reference

#### Scenario: The identification does not depend on visibility
- **WHEN** an operator with sensitive-data access uses the view
- **THEN** bookings are identified by reference exactly as they are for an operator without it
