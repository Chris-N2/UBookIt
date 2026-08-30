## ADDED Requirements

### Requirement: Bookings have a backoffice collection view
The package SHALL register a **Bookings** view in the uBookIt backoffice section, beside the
resource and service views and under the same section condition, listing the bookings the
management endpoint returns.

The list SHALL be a semantic table built from the backoffice UI library, showing per booking:
when it runs, the booker's name and email, the resources it claims by name, the service it
was placed for, and its status. It SHALL show the unpaged total and page through results, in
the same idiom the section's existing lists use.

**The view SHALL NOT compute anything the endpoint does not return.** A value the endpoint
cannot supply is a finding about the endpoint, not a calculation to add to a screen — the
same rule the endpoint already carries about fields the read port cannot supply, pointed one
layer further out.

No third-party widget framework SHALL be used, and every string the view displays SHALL come
from the package's localization with `en-US` provided.

#### Scenario: The section lists bookings
- **WHEN** a backoffice user with access to the uBookIt section opens the Bookings view
- **THEN** bookings in the default window are listed with their time, booker, resources, service and status, and the unpaged total is shown

#### Scenario: Paging reaches the rest
- **WHEN** more bookings match than fit on one page
- **THEN** the remaining bookings are reachable, and the total reported is the number matching the query rather than the number on the page

#### Scenario: A booking placed directly says so
- **WHEN** a listed booking has no service
- **THEN** the view states that it was booked directly, rather than leaving the cell blank as though the value were missing

### Requirement: The view opens on a usable window and lets an operator change it
The endpoint requires a date window, so the view SHALL open on one rather than on an empty
result or an error. The default SHALL be **the current week**.

The window SHALL be presented as two dates the operator can see and change, because the
site's guardrail refuses an over-wide window and reports the refusal **in terms of the dates
that were sent** — a failure that cannot be acted on if the dates are not visible.

**The view SHALL send dates, and SHALL NOT convert them to instants or apply any time-zone
rule of its own.** That conversion is the endpoint's, placed there so it happens once rather
than once per client; a view that reimplemented it would be the second implementation the
arrangement exists to prevent.

A window the site refuses SHALL be reported to the operator in the terms the endpoint used,
rather than as a generic failure.

#### Scenario: Opening the view shows this week
- **WHEN** an operator opens the Bookings view without choosing anything
- **THEN** the bookings of the current week are listed, and the window's two dates are shown

**A change to the query SHALL return the view to the first page.** A page number counts into
one result set and means nothing in another: an operator on page three who narrows the window
would otherwise be shown page three of a different question — plausibly empty, beneath a
"showing 41–60 of 12" that cannot be true — with nothing on screen indicating the page number
is stale.

#### Scenario: The operator changes the window
- **WHEN** an operator changes either date
- **THEN** the list reloads for the new window

#### Scenario: Changing the query returns to the first page
- **WHEN** an operator on a later page changes the window or the status filter
- **THEN** the first page of the new result set is shown, rather than a page counted into the previous one

#### Scenario: An over-wide window is reported, not swallowed
- **WHEN** an operator asks for a window wider than the site permits
- **THEN** the view reports the refusal in terms of the dates asked for, and does not present an empty list as though nothing matched

#### Scenario: No time-zone conversion happens in the view
- **WHEN** the request the view issues is inspected
- **THEN** it carries the two dates as dates, with no instant, offset or zone computed by the view

### Requirement: Cancelled bookings are reachable from the view
The endpoint returns only the statuses that block time when none is asked for, so a cancelled
or declined booking is invisible by default. The view SHALL offer a status control that can
reach them.

**Selecting no status SHALL mean what the endpoint means by omitting it** — the statuses that
block time — and the view SHALL express that by **omitting the parameter** rather than by
naming a set of its own.

The reason is that a default restated in the view is a second default, and the one an
operator meets would then depend on which layer answered first. It is **not** that an empty
set would return nothing: the endpoint treats an empty set exactly as it treats an absent
one, so that particular request is harmless. What is not harmless is the view naming **all
four** statuses to mean "the default", which is a genuinely different request — it includes
cancelled and declined bookings, which the default excludes — and would silently answer a
question the operator did not ask.

Status values SHALL cross the wire as the names the package publishes. A localized label SHALL
NOT be sent as a value.

#### Scenario: Cancelled bookings can be listed
- **WHEN** an operator asks for cancelled bookings
- **THEN** they are listed

#### Scenario: Choosing no status shows what is booked
- **WHEN** an operator has selected no status
- **THEN** the result is what the endpoint returns for that window with no status filter, rather than an empty list

#### Scenario: The wire carries published names
- **WHEN** the view requests a status it displays under a localized label
- **THEN** the value sent is the name the package publishes, not the label shown

### Requirement: A booking's time is shown in the zone it was made in
Each booking carries the time zone it was placed against, and the view SHALL render its
interval in **that** zone rather than in the reader's or the site's — the booking is a record
of a local time, and restating it in another zone reports something that was never agreed.

The zone SHALL be shown beside the time **when the page contains more than one distinct
zone**, so two rows can never be read against each other as though they shared a clock. Where
every row shares a zone — every ordinary site — the label SHALL be omitted rather than
repeated on every row.

**It SHALL also be shown when a booking's recorded zone could not be resolved and its time is
therefore being displayed in some other zone.** A time shown in a zone the booking does not
name is exactly the misattribution this requirement exists to prevent, and it is worse
unlabelled than a mixed page is: the reader has no cue at all that the clock is not the
booking's own. Distinctness is judged on the zone a row is **displayed in** rather than the
identifier it carries, so two unresolvable identifiers showing the same clock count as one.

**The limit of this SHALL be stated rather than implied:** the view compares the bookings on
the page with each other, not with the site's configured zone, because the endpoint does not
report the site's zone and adding a field the read port cannot supply is forbidden. A page
where every booking shares one zone that is no longer the site's is therefore not labelled.
That is reachable only by changing a site's zone after it has taken bookings.

#### Scenario: A single-zone page is not cluttered
- **WHEN** every booking on the page was placed in the same zone
- **THEN** times are shown in that zone without a zone label on each row

#### Scenario: A mixed-zone page is disambiguated
- **WHEN** the page contains bookings placed in different zones
- **THEN** each row shows the zone its time is expressed in

#### Scenario: A time shown in a substituted zone says so
- **WHEN** a booking's recorded zone cannot be resolved and its time is shown in another
- **THEN** the zone it is displayed in is shown, even where every row on the page is affected alike

### Requirement: The bookings view meets the section's accessibility bar
The view's markup SHALL meet the same bar the section's editors do: every control labelled,
failures programmatically associated and announced, full keyboard operability with visible
focus, semantic table markup with header cells associated to their columns, and components
from the backoffice UI library preferred over hand-rolled controls. Focus order SHALL match
visual order.

This restates the bar rather than extending the existing requirement, which is written about
the resource editor and its scenarios: replacing it wholesale to reach a list view would put
six editing scenarios at risk of being lost to say something about a table.

#### Scenario: Keyboard-only operation
- **WHEN** an operator uses the view with only a keyboard
- **THEN** the window dates, the status control, paging and every row are reachable and operable, with visible focus throughout

#### Scenario: A failed load is announced
- **WHEN** loading the list fails
- **THEN** the failure is exposed to assistive technology rather than only rendered, and the view does not present an empty list as though nothing matched

#### Scenario: The table is semantic
- **WHEN** the rendered list is inspected
- **THEN** it is a table with header cells associated to their columns, not a grid of generic elements
