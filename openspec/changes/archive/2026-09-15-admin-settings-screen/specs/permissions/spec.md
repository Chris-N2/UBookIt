<!--
GUARANTEE DIFF for the two wholesale replacements below.

"Access within the section is decided by three verbs" — 3 SHALL blocks, 4 scenarios:
  SHALL 1  exactly three verbs, assigned via the group editor  → CARRIED, amended to four.
                                                                 Each existing verb's definition
                                                                 restated verbatim.
  SHALL 2  Manage implies Read, in the rule not by copying      → CARRIED unchanged
  SHALL 3  verbs are the union across groups; no own store      → CARRIED unchanged
  Scenarios 1-4 ALL CARRIED verbatim. Three ADDED for the new verb's isolation.
  NOTE: the new verb implies nothing and is implied by nothing — stated explicitly, because
  "Manage implies Read" establishes that implication is a thing this package does.

"Existing section-granted groups are seeded once" — 2 SHALL blocks, 3 scenarios:
  SHALL 1  seed all three verbs once to section-granted groups   → CARRIED, amended to name the
             holding no UBookIt. verb; record completion;          three verbs as a closed set that
             never run again                                       the settings verb is outside of
  SHALL 2  touch nothing else; report failure without failing    → CARRIED unchanged
             startup; leave unrecorded so retry is idempotent
  Scenarios 1-3 ALL CARRIED. Two ADDED for the settings verb's exclusion, fresh and upgraded.

  DELIBERATE DROPS: none.
-->

## RENAMED Requirements

- FROM: `### Requirement: Access within the section is decided by three verbs`
- TO: `### Requirement: Access within the section is decided by four verbs`

## MODIFIED Requirements

### Requirement: Access within the section is decided by four verbs

The package SHALL define exactly four permission verbs, assigned to Umbraco user groups
through the backoffice group editor's default-permissions surface:

- **`UBookIt.Bookings.Read`** — the bookings list and every read over bookings;
- **`UBookIt.Bookings.Manage`** — cancelling, confirming and declining bookings;
- **`UBookIt.Configure`** — creating, editing and deleting resources and services,
  their supporting reads (types, capabilities, configuration preview), and
  responsibility assignment;
- **`UBookIt.Settings`** — reading and changing the site's own settings.

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
