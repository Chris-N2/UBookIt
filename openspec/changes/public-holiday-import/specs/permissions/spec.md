## MODIFIED Requirements

### Requirement: Access within the section is decided by four verbs

The package SHALL define exactly four permission verbs, assigned to Umbraco user groups
through the backoffice group editor's default-permissions surface:

- **`UBookIt.Bookings.Read`** — the bookings list and every read over bookings;
- **`UBookIt.Bookings.Manage`** — cancelling, confirming, declining and moving bookings,
  placing one on a booker's behalf, and **seeing the services and resources a booking may be
  placed for**, by name;
- **`UBookIt.Configure`** — creating, editing and deleting resources and services,
  their supporting reads (types, capabilities, configuration preview), responsibility
  assignment, **reading the site closure list, and setting a resource's closure opt-outs**;

  *`Configure` remains the verb for every read that returns a resource or a service as a
  configured thing — its open hours, constraints, capabilities and roles. What `Manage` reaches
  is a **name and an identifier**, and nothing else. The two are not a hierarchy and neither
  implies the other: a receptionist who may take a telephone booking must be able to see what
  there is to book, and must not thereby acquire the privilege to reconfigure it.*
- **`UBookIt.Settings`** — reading and changing the site's own settings, **reading the site
  closure list, changing it, and both halves of a public holiday import** — asking a registered
  source what it offers for a window, and creating closures from the rows an operator chose.

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

*Site closures split across the two existing verbs rather than introducing a fifth, and the split
is between three acts rather than two. **Changing the list is a site-level act** — one entry shuts
every resource the site has, including those created after it — so it sits with `Settings` alone,
alongside the other decisions a grant meaning "may add a meeting room" does not carry.
**Exempting one resource from a closure is a resource-level act**, and sits with `Configure`
alone: it is a resource write, gated exactly as every other resource write is. **Reading the list
sits with BOTH**, because each verb needs it on its own account — an operator editing a resource
cannot exempt what they cannot see, and whoever decides the site's closures must be able to read
the list they are deciding. The line is between deciding the site's policy and applying an
exemption to one thing under it; the read is on both sides of that line, which is why it is the
one act neither verb owns exclusively.*

*Importing public holidays added a fourth act to that split rather than a fifth verb, and it sits
with `Settings` in both halves. **Creating the closures is plainly a site-level act** — it is the
same act as typing them, and produces closures indistinguishable from typed ones. **Previewing is
gated identically although it creates nothing**, which the three-act reasoning above does not by
itself decide: a preview neither decides the site's policy nor applies an exemption, so the
"deciding versus exempting" line does not place it. What places it is that previewing makes the
site's own code reach outward on an operator's behalf, and that it is the first half of an act
whose second half closes every resource the site has. Granting the preview separately would hand
somebody the outbound call without the decision it exists to serve, and would make `Configure` —
a grant meaning "may add a meeting room" — a grant that can make the site call out.*

**Manage SHALL imply Read**, in the authorization rule and not by copying verbs onto
groups: a group holding only Manage reads bookings, because managing what cannot be seen
is incoherent, and the implication living in one rule means no group's stored verbs need
restating when it changes.

**`UBookIt.Settings` SHALL imply nothing and SHALL be implied by nothing.** It is not a
senior form of `UBookIt.Configure`: configuring a bookable resource and configuring the
site are different privileges, and the settings reach the site's retention posture, its
anonymous exposure, the addresses bookers' details are sent to, and — where a site registered a
holiday source — the invocation of that site's own code on an operator's behalf. A grant meaning "may
add a meeting room" does not carry them, in either direction. **That closures are readable
under either verb is not an implication between them**: each verb reaches that read on its own
account, and neither acquires anything else the other holds.

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

#### Scenario: Configure reads the closure list but cannot change it
- **WHEN** a user whose groups hold only `UBookIt.Configure` reads the closure list and then attempts to create a closure
- **THEN** the list is served and the creation is refused

#### Scenario: Configure opts a resource out of a closure
- **WHEN** a user whose groups hold only `UBookIt.Configure` saves a resource with a closure opt-out
- **THEN** the write is served

#### Scenario: Settings alone reads the closure list
- **WHEN** a user whose groups hold only `UBookIt.Settings` reads the closure list
- **THEN** the list is served

#### Scenario: Settings changes the closure list
- **WHEN** a user whose groups hold only `UBookIt.Settings` creates, edits and deletes a closure
- **THEN** each write is served

#### Scenario: Settings alone still cannot opt a resource out
- **WHEN** a user whose groups hold only `UBookIt.Settings` attempts to save a resource carrying a closure opt-out
- **THEN** the write is refused, because saving a resource is a `Configure` act

#### Scenario: Neither verb reaches the closure list
- **WHEN** a user holding the section but neither `UBookIt.Configure` nor `UBookIt.Settings` requests the closure list
- **THEN** the request is refused

#### Scenario: The verb count is unchanged
- **WHEN** the package's permission verbs are enumerated
- **THEN** there are exactly four, and none of placing a booking on a booker's behalf, site closures or the public holiday import introduced any

#### Scenario: Settings reaches both halves of an import
- **WHEN** a user whose groups hold only `UBookIt.Settings` previews public holidays and then imports the rows they chose
- **THEN** both are served

#### Scenario: Configure alone reaches neither half, and still reads the list
- **WHEN** a user whose groups hold only `UBookIt.Configure` requests a holiday preview and an import
- **THEN** both are refused, and reading the closure list remains available to them

#### Scenario: The source probe is gated too
- **WHEN** a user holding the section but not `UBookIt.Settings` asks whether a holiday source is registered
- **THEN** the request is refused, so the question is not answerable below the verb that acts on the answer
