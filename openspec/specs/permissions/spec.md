# permissions Specification

## Purpose

Who may do what within the uBookIt section, decided by four permission verbs a site
assigns to its Umbraco user groups through the ordinary group editor: seeing bookings,
acting on them (which includes seeing them — the implication is a rule of the
authorization, never data on a group), configuring resources, services and
responsibility, and changing the site's own settings. The section grant stays the
unremovable outer gate that no verb can substitute for, and the Sensitive data group
stays the decisive control over contact details, joined by the read verb and replaced by
nothing.

The verbs live in Umbraco's own group permission storage; the package stores only a
one-shot marker recording that existing section-granted groups were seeded at upgrade,
so nobody loses access by upgrading and the toggles thereafter mean exactly what they
show — empty is nothing. **The seeded set is the first three; the settings verb is never
seeded**, on upgrade or on a fresh install and not for administrators either, because
granting it to every group that happened to hold the configure verb would widen privilege
into exactly what it exists to separate. The client hides what a user's verbs do not cover
as a courtesy; the server decides every request.

## Requirements

### Requirement: Access within the section is decided by four verbs

The package SHALL define exactly four permission verbs, assigned to Umbraco user groups
through the backoffice group editor's default-permissions surface:

- **`UBookIt.Bookings.Read`** — the bookings list and every read over bookings;
- **`UBookIt.Bookings.Manage`** — cancelling, confirming, declining and moving bookings;
- **`UBookIt.Configure`** — creating, editing and deleting resources and services,
  their supporting reads (types, capabilities, configuration preview), and
  responsibility assignment;
- **`UBookIt.Settings`** — reading and changing the site's own settings.

*Moving joined Manage rather than becoming a fifth verb because the verb already means "may act
on a booking", and moving a booking is a smaller act than cancelling one: it keeps the booking,
its time is still held, and the customer's reference still stands.*

**Manage SHALL imply Read**, in the authorization rule and not by copying verbs onto
groups: a group holding only Manage reads bookings, because managing what cannot be seen
is incoherent, and the implication living in one rule means no group's stored verbs need
restating when it changes.

**`UBookIt.Settings` SHALL imply nothing and SHALL be implied by nothing.** It is not a
senior form of `UBookIt.Configure`: configuring a bookable resource and configuring the
site are different privileges, and the settings reach the site's retention posture, its
anonymous exposure and the addresses bookers' details are sent to. A grant meaning "may
add a meeting room" does not carry them, in either direction.

A user's verbs SHALL be the union across every group they belong to, read from Umbraco's
own user-group permission storage; the package SHALL define no permission store of its
own.

#### Scenario: Read without Manage sees but cannot act

- **WHEN** a user whose groups hold only `UBookIt.Bookings.Read` lists bookings and then
  attempts to cancel one
- **THEN** the list is served and the cancellation is refused

#### Scenario: Read without Manage cannot move

- **WHEN** a user whose groups hold only `UBookIt.Bookings.Read` attempts to move a booking
- **THEN** the move is refused and the booking is unchanged

#### Scenario: Manage implies Read

- **WHEN** a user whose groups hold only `UBookIt.Bookings.Manage` lists bookings
- **THEN** the list is served, without `UBookIt.Bookings.Read` being present on any of
  their groups

#### Scenario: Configure does not reach bookings

- **WHEN** a user whose groups hold only `UBookIt.Configure` attempts to list bookings
- **THEN** the request is refused, and resource and service management remain available
  to them

#### Scenario: Verbs union across groups

- **WHEN** a user belongs to one group holding `UBookIt.Bookings.Read` and another
  holding `UBookIt.Configure`
- **THEN** they hold both capabilities

#### Scenario: Configure does not reach the settings

- **WHEN** a user whose groups hold only `UBookIt.Configure` requests the settings
- **THEN** the request is refused, and resource and service management remain available
  to them

#### Scenario: Settings does not reach resources, services or bookings

- **WHEN** a user whose groups hold only `UBookIt.Settings` attempts to manage a resource
  or list bookings
- **THEN** both are refused, and the settings remain available to them

#### Scenario: An administrator is not exempt

- **WHEN** a user who is an Umbraco administrator, but none of whose groups hold
  `UBookIt.Settings`, requests the settings
- **THEN** the request is refused

### Requirement: The section grant remains the outer gate

Access to the package's section SHALL remain required for every management endpoint,
exactly as before this capability existed, and a verb SHALL never substitute for it: a
user whose groups hold every verb but not the section reaches nothing. An endpoint that
has not been classified into a verb SHALL remain section-gated — never anonymous — and
the classification SHALL be total: an unclassified management endpoint SHALL be a
reported failure naming it, not a silently looser or tighter gate.

#### Scenario: Verbs without the section grant nothing

- **WHEN** a user whose groups hold every uBookIt verb but no uBookIt section access calls
  any management endpoint
- **THEN** the request is refused on the same terms as before this capability existed

#### Scenario: The section alone shows the shell

- **WHEN** a user holds the section grant and their groups hold no uBookIt verb, and the
  one-time seed has already run
- **THEN** the section is visible and every verb-gated endpoint refuses them

#### Scenario: Every endpoint is classified

- **WHEN** the management endpoints are enumerated
- **THEN** each is classified into exactly one verb, and an unclassified endpoint is
  reported as a failure naming it

### Requirement: Sensitive-data gates are joined by verbs, never replaced

The endpoints gated by the **Sensitive data** group — reading contact details, searching
by booker, erasure — SHALL keep that gate unchanged and SHALL additionally require
`UBookIt.Bookings.Read`. Sensitive-data access SHALL remain the decisive control over
contact details; a verb SHALL never disclose what that group withholds.

#### Scenario: Sensitive data without the read verb reaches no bookings

- **WHEN** a user with sensitive-data access whose groups hold no uBookIt booking verb
  calls the find-by-booker endpoint
- **THEN** the request is refused

#### Scenario: The read verb without sensitive data still withholds

- **WHEN** a user whose groups hold `UBookIt.Bookings.Read` but who lacks sensitive-data
  access lists bookings
- **THEN** the response withholds contact details exactly as the `sensitive-data`
  capability requires

### Requirement: Existing section-granted groups are seeded once

At startup, exactly once per installation, the package SHALL grant the three verbs
`UBookIt.Bookings.Read`, `UBookIt.Bookings.Manage` and `UBookIt.Configure` to
every user group that holds the uBookIt section grant and holds **no** verb beginning
`UBookIt.` — so an installation upgrading to this version keeps, group for group, exactly
the access it had. The seed SHALL record its completion in package-private storage and
SHALL NOT run again once recorded; a group whose verbs an administrator later empties
stays emptied.

**`UBookIt.Settings` SHALL NOT be seeded, on any installation.** The seeded set is closed
at those three. Granting it to every group that happens to hold `UBookIt.Configure` would
widen privilege during an upgrade, silently, into exactly the settings the verb exists to
separate — and because the seed has already recorded completion on installations upgrading
from a version where it ran, an attempt to seed it would in any case reach only fresh
installations, making a freshly installed site and an upgraded one differ in who may change
the site's settings. It SHALL therefore be granted only by an administrator, deliberately.

The seed SHALL touch nothing else: groups without the section grant are not modified,
groups already holding any uBookIt verb are not modified, and no verb is ever removed.
Where the seed cannot complete, it SHALL report the failure without failing startup and
SHALL leave its completion unrecorded, so the next startup retries; its
only-groups-with-no-uBookIt-verb condition SHALL make that retry idempotent.

#### Scenario: An upgraded install keeps its access

- **WHEN** the package starts for the first time at this version on an installation
  whose groups hold the section grant and no uBookIt verbs
- **THEN** each of those groups afterwards holds the three seeded verbs, and every other
  group is unchanged

#### Scenario: The seed runs once

- **WHEN** an administrator empties a seeded group's uBookIt verbs and the site restarts
- **THEN** the group still holds no uBookIt verbs

#### Scenario: A failed seed retries without doubling

- **WHEN** the seed fails partway and the site restarts
- **THEN** startup was not failed by it, the seed runs again, and no group ends holding
  a duplicated or partial grant

#### Scenario: Upgrading grants the settings verb to nobody

- **WHEN** an installation whose groups already hold uBookIt verbs is upgraded to the
  version introducing `UBookIt.Settings`
- **THEN** no group holds `UBookIt.Settings`, and each group's other verbs are unchanged

#### Scenario: A freshly seeded install grants the settings verb to nobody either

- **WHEN** the seed runs for the first time on an installation with section-granted groups
  holding no uBookIt verbs
- **THEN** those groups hold the three seeded verbs and not `UBookIt.Settings`

### Requirement: The client hides what the server would refuse, and the server remains the truth

The backoffice client SHALL surface each verb as a toggle in the user group editor's
default-permissions pane, using Umbraco's own extension surface and no custom management
UI, with the verb strings identical to the server's — a guard SHALL fail when the two
vocabularies disagree. The section's own views SHALL hide surfaces and actions the
current user's verbs do not cover, as convenience: the authorization decision SHALL be
the server's for every request, so a deep link or direct call against a hidden surface
is refused regardless of what the client shows.

#### Scenario: The toggles appear in the group editor

- **WHEN** an administrator opens a user group in the backoffice
- **THEN** the three uBookIt permissions can be toggled in the default-permissions pane
  and persist with the group

#### Scenario: Hidden is not the control

- **WHEN** a user without `UBookIt.Configure` requests a resource-management endpoint
  directly
- **THEN** the request is refused by the server, whatever the client did or did not show

#### Scenario: One vocabulary

- **WHEN** the client manifest's verbs and the server's verb constants are compared
- **THEN** they are identical, and a difference is a reported failure
