<!--
GUARANTEE DIFF for the one wholesale replacement below.

"Access within the section is decided by four verbs" — 4 SHALL blocks, 7 scenarios:
  SHALL 1  exactly four verbs, assigned via the group editor;       → CARRIED; Manage's definition
           each verb's definition                                     amended to name moving. The
                                                                      other three restated verbatim
  SHALL 2  Manage implies Read, in the rule not by copying          → CARRIED unchanged
  SHALL 3  Settings implies nothing, is implied by nothing          → CARRIED unchanged
  SHALL 4  verbs are the union across groups; no own store          → CARRIED unchanged
  Scenarios 1-7 ALL CARRIED verbatim. One ADDED: Read without Manage cannot move.

  DELIBERATE DROPS: none. The verb count is unchanged.
-->

## MODIFIED Requirements

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
