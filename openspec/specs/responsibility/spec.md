# responsibility Specification

## Purpose

Who is responsible for which resource or service — backoffice users and user groups,
assigned where the resource or service is edited — and what being responsible means:
you are emailed about its bookings, and nothing else. Responsibility decides who the
internal messages of the `booking-emails` capability reach, as a targeted tier in
union with the site-wide configured list; it is explicitly not a permissions model,
grants nothing, restricts nothing, and only ever reads a group's membership.

The parties are references into Umbraco's own user store, resolved at the moment they
are needed — never copied — so the reference is the only thing that can go stale, and
the capability says what happens when it does: sending skips it silently, the editing
surface shows it marked.

## Requirements

### Requirement: A resource or service can be assigned responsible parties

The package SHALL allow a resource or a service to be assigned **responsible parties**: zero or
more backoffice **users** and zero or more backoffice **user groups**, in any combination. An
assignment SHALL reference the party by its Umbraco key and SHALL carry no other payload — no
copied names, and above all **no copied email addresses**, so that a fact Umbraco owns is never
duplicated into a place where it can go stale.

Writing a subject's assignments SHALL replace the set wholesale, the same shape a resource's
capability set already uses, so that what is saved is what is seen. Assigning to a resource or
service that does not exist SHALL be rejected; assigning a party key that does not currently
resolve SHALL NOT be rejected — a save must not fail because it raced the deletion of a user,
and a dangling party is an expected state handled at read and send time rather than a write
error.

#### Scenario: Users and groups are assigned to a resource
- **WHEN** a resource is assigned two users and one user group and its assignments are read back
- **THEN** exactly those three assignments are returned, each identifying its party kind and key

#### Scenario: A service takes assignments the same way
- **WHEN** a service is assigned a user and its assignments are read back
- **THEN** that assignment is returned, on the same terms as a resource's

#### Scenario: Writing replaces the set
- **WHEN** a subject with three assignments is written with a set of one
- **THEN** reading back returns exactly the one

#### Scenario: Assigning to a missing subject is rejected
- **WHEN** assignments are written for a resource or service id that does not exist
- **THEN** the write is rejected and no assignment is stored

#### Scenario: A party that no longer resolves is not a write error
- **WHEN** an assignment set containing a key that matches no current user or group is written
- **THEN** the write succeeds and the assignment is stored as given

### Requirement: A booking's responsible parties are resolved as a union

For a booking, the responsible parties SHALL be resolved as the **union** of the assignments on
the booking's service (where the booking is attributed to one) and the assignments on **every
resource the booking claims** — never a precedence between them. The people who look after a
room are told about a service booking that occupies it on the same terms as the people who look
after the service.

A user group assignment SHALL resolve to the group's **current members at the time of sending**,
so that membership changes take effect without any uBookIt action. Resolution SHALL produce
email addresses **deduplicated case-insensitively** across everything that contributed them — a
person assigned directly, via a group, and present in the configured recipient list SHALL
receive one message.

#### Scenario: A resource's responsible user is told of a direct booking
- **WHEN** a booking is placed directly against a resource with an assigned responsible user, with internal sending in effect
- **THEN** the internal message is sent to that user's account address

#### Scenario: A service booking reaches both service and resource parties
- **WHEN** a service booking claims a resource, the service and the resource each have a distinct responsible user, and the internal message is sent
- **THEN** both users receive it

#### Scenario: A group member is resolved at send time
- **WHEN** a user joins a group after the group was assigned to a resource, and a booking is then placed against that resource
- **THEN** that user receives the internal message without any change to the assignment

#### Scenario: One person, many routes, one message
- **WHEN** the same address would be reached as a direct assignee, as a group member and through the configured recipient list
- **THEN** exactly one message is sent to it

#### Scenario: A booking touching nothing assigned falls back to the list alone
- **WHEN** a booking's service and claimed resources carry no assignments and the site has configured internal recipients
- **THEN** the internal message goes to the configured recipients exactly as before this capability existed

### Requirement: Resolution skips who cannot or will not act, silently

Resolution SHALL include a user whose state is **Active**, **Inactive** (an account created but
not yet logged into — a real colleague with a real inbox) or **LockedOut** (a transient state
that expires; a lockout must not cost the site a booking notification). It SHALL skip a user who
is **Disabled** (an administrator deliberately ended their access) or **Invited** (no accepted
account, and an address that may never have been verified), and a user with no usable email
address.

An assignment whose user or group **no longer exists** SHALL resolve to nothing, silently: the
mail path SHALL neither fail nor log because of it. Staleness is an editing concern, surfaced
where assignments are edited — not a sending concern.

#### Scenario: A disabled user is skipped
- **WHEN** a responsible user is disabled and a booking triggers the internal message
- **THEN** that user receives nothing and every other recipient is unaffected

#### Scenario: A locked-out user still receives
- **WHEN** a responsible user is locked out and a booking triggers the internal message
- **THEN** that user receives the message

#### Scenario: A deleted user is skipped without a fault
- **WHEN** an assignment references a user that has been deleted and a booking triggers the internal message
- **THEN** sending completes normally for everyone else, with no error raised for the dangling assignment

#### Scenario: A deleted group is skipped without a fault
- **WHEN** an assignment references a user group that has been deleted and a booking triggers the internal message
- **THEN** sending completes normally for everyone else, with no error raised for the dangling assignment

### Requirement: Assignments are managed through section-authorized endpoints

The management API SHALL expose reading and writing of a subject's assignments under the same
backoffice authorization every other management endpoint uses — the section grant as the outer
gate, refined since the `permissions` capability by its verbs, and for these endpoints the
configuration verb, responsibility being configuration — and nothing weaker. The read SHALL annotate each assignment with what its party currently
resolves to — a display name, whether it still exists, and its state where the party is a user —
so an editor can show a stale or disabled assignment rather than hide it.

#### Scenario: Anonymous request is rejected
- **WHEN** an unauthenticated caller reads or writes assignments
- **THEN** the request is refused on the same terms as every other management endpoint

#### Scenario: Assignments round-trip through the API
- **WHEN** an authorized caller writes assignments for a resource and reads them back
- **THEN** the same set returns, each annotated with the party's current display name

#### Scenario: A dangling assignment is reported, not hidden
- **WHEN** assignments are read for a subject whose assigned user has since been deleted
- **THEN** the assignment is returned and marked as no longer resolving

### Requirement: The editors surface responsibility where the subject is edited

The backoffice SHALL let an operator edit a resource's or service's responsible parties in the
same editing surface where that resource or service is edited, using the backoffice's own user
and user-group pickers. An assignment whose party no longer exists, or whose user is in a state
resolution skips, SHALL be visibly marked rather than silently dropped from the display — an
operator must be able to see that a subject's contact went away.

The surface SHALL state, in plain words, that responsibility decides **who is emailed about
bookings** and neither grants nor restricts anything — assigning responsibility to a user SHALL
change nothing about what that user can see or do, and reading a group's membership SHALL grant
nothing to its members.

#### Scenario: Assignments are edited beside the subject
- **WHEN** an operator opens a resource or a service for editing
- **THEN** its responsible users and groups can be viewed and changed there

#### Scenario: Staleness is shown, not swallowed
- **WHEN** an operator opens a subject whose assigned user has been deleted or disabled
- **THEN** that assignment is shown with its condition marked

#### Scenario: Assignment does not touch access
- **WHEN** a user with no access to the package's section is assigned as responsible for a resource
- **THEN** their access to the section, and to every management endpoint, is exactly what it was

### Requirement: Assignments do not outlive their subject

Deleting a resource or a service SHALL remove its assignments in the same operation, so a
subject id can never acquire assignments from a predecessor: a later resource or service given
a recycled id SHALL NOT inherit anybody.

#### Scenario: Deleting a resource removes its assignments
- **WHEN** a resource with assignments is deleted
- **THEN** no assignment for it remains

#### Scenario: Deleting a service removes its assignments
- **WHEN** a service with assignments is deleted
- **THEN** no assignment for it remains
