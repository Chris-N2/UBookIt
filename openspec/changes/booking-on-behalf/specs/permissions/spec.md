## MODIFIED Requirements

### Requirement: Access within the section is decided by four verbs

The package SHALL define exactly four permission verbs, assigned to Umbraco user groups
through the backoffice group editor's default-permissions surface:

- **`UBookIt.Bookings.Read`** — the bookings list and every read over bookings;
- **`UBookIt.Bookings.Manage`** — cancelling, confirming, declining and moving bookings,
  placing one on a booker's behalf, and **seeing the services and resources a booking may be
  placed for**, by name;
- **`UBookIt.Configure`** — creating, editing and deleting resources and services,
  their supporting reads (types, capabilities, configuration preview), and
  responsibility assignment;

  *`Configure` remains the verb for every read that returns a resource or a service as a
  configured thing — its open hours, constraints, capabilities and roles. What `Manage` reaches
  is a **name and an identifier**, and nothing else. The two are not a hierarchy and neither
  implies the other: a receptionist who may take a telephone booking must be able to see what
  there is to book, and must not thereby acquire the privilege to reconfigure it.*
- **`UBookIt.Settings`** — reading and changing the site's own settings.

*Moving joined Manage rather than becoming a fifth verb because the verb already means "may act
on a booking", and moving a booking is a smaller act than cancelling one: it keeps the booking,
its time is still held, and the customer's reference still stands.*

*Placing a booking on a booker's behalf joined Manage on related but not identical grounds. It is
a larger act than the other four — it commits the site's time rather than disposing of a
commitment already made — so the argument from size does not carry it. What carries it is that a
verb is not this endpoint's whole gate: it also requires sensitive-data access, because it
accepts a booker's contact details, and a group that may take a booking on the telephone is
therefore already a group the site has trusted with personal data. A fifth verb would divide
the smaller privilege while leaving the larger one undivided.*

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

#### Scenario: Manage reaches placing on a booker's behalf
- **WHEN** a user whose groups hold `UBookIt.Bookings.Manage`, and who has sensitive-data access, places a booking on a booker's behalf
- **THEN** the request is served

#### Scenario: Read alone cannot place on a booker's behalf
- **WHEN** a user whose groups hold only `UBookIt.Bookings.Read` attempts to place a booking on a booker's behalf
- **THEN** the request is refused

#### Scenario: Manage reaches what there is to book, and Configure still owns the configuration
- **WHEN** a user whose groups hold only `UBookIt.Bookings.Manage` asks what may be booked, and then asks a resource-management endpoint for the same resource
- **THEN** the first is served, carrying names and identifiers alone, and the second is refused

#### Scenario: The verb count is unchanged
- **WHEN** the package's permission verbs are enumerated
- **THEN** there are exactly four, and placing a booking on a booker's behalf introduced none

### Requirement: Sensitive-data gates are joined by verbs, never replaced

The endpoints gated by the **Sensitive data** group — reading contact details, searching
by booker, erasure, **and placing a booking on a booker's behalf** — SHALL keep that gate
unchanged and SHALL additionally require a booking verb: `UBookIt.Bookings.Read` for the
first three, and `UBookIt.Bookings.Manage` for the last, which acts rather than reads.
Sensitive-data access SHALL remain the decisive control over
contact details; a verb SHALL never disclose what that group withholds.

**Neither gate SHALL be sufficient alone, in either direction.** The placement endpoint accepts
contact details rather than disclosing them, so sensitive-data access is required of it as an
input rule rather than an output one; that it discloses nothing SHALL NOT be read as licence to
drop the gate, and holding the gate SHALL NOT be read as carrying the verb.

#### Scenario: Sensitive data without the read verb reaches no bookings

- **WHEN** a user with sensitive-data access whose groups hold no uBookIt booking verb
  calls the find-by-booker endpoint
- **THEN** the request is refused

#### Scenario: The read verb without sensitive data still withholds

- **WHEN** a user whose groups hold `UBookIt.Bookings.Read` but who lacks sensitive-data
  access lists bookings
- **THEN** the response withholds contact details exactly as the `sensitive-data`
  capability requires

#### Scenario: Manage without sensitive data cannot place on a booker's behalf
- **WHEN** a user holding `UBookIt.Bookings.Manage` but lacking sensitive-data access places a booking on a booker's behalf
- **THEN** the request is refused, and no booking is placed

#### Scenario: Sensitive data without Manage cannot place on a booker's behalf
- **WHEN** a user with sensitive-data access whose groups hold only `UBookIt.Bookings.Read` places a booking on a booker's behalf
- **THEN** the request is refused, and no booking is placed
