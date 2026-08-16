## ADDED Requirements

### Requirement: The editor reports roles whose pools cannot be filled together
The services editor SHALL report, as a service is edited, when the configuration's
roles cannot all be filled at once by the resources that exist — read from the
configuration preview's sufficiency member, never computed in the client, so the
editor cannot state something Core did not conclude.

It SHALL be presented at **form level**, beside the resolution chains and the
start-grid report, and SHALL NOT be attached to a requirement row. The finding
belongs to a set of roles rather than to one of them, and rendering it against a row
would tell an editor to correct a row that may be perfectly well formed — the fault
is as often a missing resource as a wrong count.

The report SHALL be **informational** and SHALL NOT block saving, because a
configuration whose pool is too small today is corrected as often by adding a
resource as by editing the service, and refusing the save would force the editor to
do those in one order.

The wording SHALL state what is required against what is eligible, and SHALL name the
roles involved. It SHALL name them by the **requirement row** each belongs to, as
the resolution chains beside it do: the editor has rows, the fix for a role is on
its row, and two roles of one resource type requiring the same capabilities are
distinguishable by nothing else. A row SHALL be identified by the position the
response reports for it, never by its position within the finding — the finding is
a subset of the configuration, so its second entry is not the second row. It SHALL NOT state or imply that the service *is* available, free, or
bookable, and SHALL NOT state that a sufficient pool means the service can be booked
— eligibility is not availability, and this check evaluates neither opening hours,
lead time, horizon, nor the booking calendar.

The report SHALL say nothing when the roles can be filled together, and SHALL say
nothing when the configuration is too incomplete to resolve or the request failed,
rather than reporting a shortfall of zero. Reporting zero is the answer that tells an
editor their configuration is wrong.

The report's wording SHALL be derived from state captured with the response it
describes, never from the live form, so that it cannot momentarily assert a sentence
that is false for the configuration it is reporting on.

#### Scenario: An insufficient pool is reported
- **WHEN** an editor sets a requirement's count to 2 while only one resource is eligible for it
- **THEN** the editor states that the role requires 2 distinct resources and 1 is eligible, at form level, and saving remains available

#### Scenario: A sufficient pool is reported as nothing
- **WHEN** the configuration's roles can all be filled at once
- **THEN** the editor shows no sufficiency statement, and no wording suggesting the service is available or bookable

#### Scenario: The report does not block saving
- **WHEN** a service whose pool is insufficient is saved
- **THEN** the save succeeds, because the pool is a property of the resources rather than of the service

#### Scenario: The report is not attached to a requirement row
- **WHEN** two roles together cannot be filled
- **THEN** the statement appears once at form level naming both roles, and neither requirement row is marked as being in error

#### Scenario: Two roles identical in type and capabilities are still distinguishable
- **WHEN** two requirement rows name the same resource type and require the same capabilities, and cannot be filled together
- **THEN** the statement names each by its own requirement row rather than producing two identical entries

#### Scenario: A row above that is not yet filled in does not shift the numbering
- **WHEN** a requirement row above has no resource type entered yet, so the configuration reported on omits it
- **THEN** every reported role is still named by the requirement row it belongs to, not by its position among the roles reported

#### Scenario: An unresolvable configuration reports nothing rather than zero
- **WHEN** the configuration is too incomplete to resolve, or the preview request fails
- **THEN** the editor shows no sufficiency statement, rather than one reporting that zero resources are eligible

#### Scenario: Accessibility of the report
- **WHEN** the sufficiency statement is rendered
- **THEN** it is text within the form's own structure, every id it references resolves in the same shadow root, and it is not conveyed by colour alone

## MODIFIED Requirements

### Requirement: Backoffice collection view for services
The package SHALL register a services collection view within the existing uBookIt backoffice section, alongside the resources view. It SHALL list services in a semantic `uui`-based table showing the service name, a summary of what the service requires, and a summary of its duration, with paging over the management API's paged list and affordances to create, edit, and delete. The duration summary SHALL distinguish the two kinds and SHALL state whichever bounds a variable duration carries, rather than reporting only that it is variable. Editing SHALL open a separate editor view, never inline in the table. All user-facing strings SHALL come from Umbraco's localization mechanism. No third-party widget framework SHALL be used.

The requirement summary SHALL distinguish two roles that name the **same** resource
type. Such roles are legal exactly when their required capabilities differ, so a
summary reporting only type and count renders a configuration the domain accepts
identically to one it rejects — and the rejection's own message tells the editor to
use a count instead, which a reader of that summary would have no way to understand.
Where two roles share a resource type the summary SHALL therefore state what
distinguishes them.

Where no two roles share a resource type the summary SHALL NOT be required to state
required capabilities, so the common case stays short. The summary is a table cell,
and making every service noisier to disambiguate the few would trade the many
against the few.

#### Scenario: Section lists services
- **WHEN** a backoffice user with access opens the services view of the uBookIt section
- **THEN** existing services are listed with name, requirement summary, and duration summary, and a create action is available

#### Scenario: A fixed duration is summarised as its length
- **WHEN** a service with a fixed 60-minute duration is listed
- **THEN** its duration summary states 60 minutes

#### Scenario: A bounded variable duration states both bounds
- **WHEN** a service with a variable duration bounded between 45 and 120 minutes is listed
- **THEN** its duration summary states that it is variable and reports both bounds

#### Scenario: An unbounded variable duration is summarised as variable
- **WHEN** a service with a variable duration and no bounds is listed
- **THEN** its duration summary states that it is variable, with no bounds reported

#### Scenario: Both views are reachable
- **WHEN** a backoffice user opens the uBookIt section
- **THEN** both a Resources view and a Services view are available, and selecting Services does not disturb the resources view's behaviour

#### Scenario: Paging beyond one page
- **WHEN** more services exist than fit one page and the user advances a page
- **THEN** the next page of services is shown with an accurate "showing X–Y of Z" indication

#### Scenario: Two roles of one type are distinguishable in the summary
- **WHEN** a service with two `therapist` roles, one requiring `cert-x` and one requiring nothing, is listed
- **THEN** its requirement summary distinguishes the two entries by what each requires, rather than rendering them as two identical entries

#### Scenario: Distinct-type services keep a short summary
- **WHEN** a service requiring a `room` and a `therapist`, neither sharing a type, is listed
- **THEN** its requirement summary names the two types and their counts, and is not required to state required capabilities

#### Scenario: A counted role states its count
- **WHEN** a service with one role of count 2 is listed
- **THEN** its requirement summary states that two of that resource type are required
