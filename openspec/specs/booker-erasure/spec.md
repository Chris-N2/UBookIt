# booker-erasure

## Purpose

What it means to erase the person from a booking: the operation that removes a booker's name,
email address, phone number and member key while the booking itself — its reference, its
interval, its status and the time it holds — survives untouched; the erased state the booker is
left in, which records that it happened and when rather than blanking values; why erasure is
terminal, irreversible and safe to retry; who may perform it; how it is reported to every caller
who may see the booking, and never confused with a detail merely withheld; that contact details
have exactly one durable home **among the stores the package owns**, so erasing that home erases
the data it holds; and the boundaries the documentation has to state — one erasure reaches one
booking, it may be performed on a booking that has not yet happened, and it reaches neither a
message already sent nor an address a mail server quoted into the host's log.

Stated as its own capability rather than inside `bookings` or `booking-management` because it is
a promise made to a data subject that spans both, and the requirements at each layer answer to
it.

## Requirements
### Requirement: Erasure removes the person and keeps the booking

The package SHALL provide an operation that removes a booking's personal data while leaving
the booking itself intact. Erasure SHALL remove the booker's name, email address, phone number
and member key, and SHALL leave unchanged the booking's identity, **its quotable reference**,
its interval, its time zone, its status, its creation time, its resource claims and its service
attribution.

**Erasure SHALL NOT delete the booking row.** An erased booking still occupies its interval,
still blocks its resources against other placements on the same terms as before, and still
counts toward every total it counted toward. Deleting it would silently return time a site had
sold, and would destroy the site's own record of what happened — which is not what a person
asking to be forgotten has asked for, and not something a site may agree to on their behalf.

**The booking SHALL remain discussable after erasure.** The reference is what makes that
possible and SHALL NOT be derived from, or invalidated by, the person: it identifies a booking,
not a booker.

**The member key SHALL be removed with the contact details.** An Umbraco member key identifies
a person as reliably as their email address; removing the name and address while keeping the
key would leave the identity intact and the erasure a gesture.

#### Scenario: The booking survives its booker's erasure
- **WHEN** a booking's booker is erased
- **THEN** the booking still exists with the same id, reference, interval, time zone, status, creation time, resource claims and service attribution

#### Scenario: An erased booking still holds its time
- **WHEN** a placement is attempted that overlaps an erased booking whose status still blocks
- **THEN** it conflicts exactly as it would have before the erasure

#### Scenario: Every piece of the person is removed
- **WHEN** a booking's booker is erased
- **THEN** the stored name, email address, phone number and member key are all absent

#### Scenario: The reference survives erasure
- **WHEN** an erased booking is read back from storage and listed for management
- **THEN** its reference is unchanged and still identifies it, so an operator can match what a caller reads out

<!-- This scenario said "looked up BY the reference", which names an operation the package does
     not have: there is no lookup-by-reference on any port, and `booking-management` records
     that finding a booking from a reference "is a different query with different indexing, and
     is not provided here". A scenario describing behaviour that does not exist cannot be
     verified and quietly asserts the feature is there. What erasure actually guarantees — that
     the reference is untouched and the row stays findable through the reads that do exist — is
     what is stated now. -->

### Requirement: An erased booker is a state, not a blank

After erasure a booking SHALL still have a booker, and that booker SHALL record **that the
details were erased and when**, rather than carrying empty, placeholder or masked values.

**The state SHALL be expressible by construction rather than by convention.** It SHALL NOT be
possible to produce a booker that neither carries contact details nor records an erasure, and
a reader SHALL NOT be able to reach the contact details without confronting the possibility
that they are absent.

**A booking without a booker SHALL remain inexpressible.** What erasure removes is the
person's details, not the fact that the booking was made for somebody. This is what allows
absence at the outer level to keep a single meaning elsewhere in the package.

#### Scenario: An erased booker records the erasure
- **WHEN** a booker's details have been erased
- **THEN** the booker reports that it is erased and the instant at which it happened, and carries no name, email or phone

#### Scenario: Blanking is not how erasure is represented
- **WHEN** an erased booker is inspected
- **THEN** no empty string, placeholder or masked form of a name or email is present

#### Scenario: The invalid combination cannot be constructed
- **WHEN** a caller attempts to construct a booker with neither contact details nor an erasure instant
- **THEN** no such value can be produced

#### Scenario: A reader cannot skip the question
- **WHEN** code reads a booker's name or email without establishing that the details are present
- **THEN** it does not compile

### Requirement: Erasure is terminal, irreversible and idempotent

Erasure SHALL NOT be reversible by any operation the package provides. Erasing a booking whose
booker is already erased SHALL succeed and SHALL change nothing — including the recorded
erasure instant, which SHALL remain the instant of the **first** erasure.

**Idempotence is deliberate, and is the opposite conclusion from cancellation.** Cancelling
twice is refused because cancelling is a transition whose starting state matters, and a caller
told "cancelled" when nothing changed cannot tell a completed action from a rejected one.
Erasure has no such starting state: it is absorbing, its observable outcome is identical either
way — the details are gone — and it is invoked by machinery that must be safe to retry. A verb
that failed on already-done would make a retry indistinguishable from a fault.

#### Scenario: Erasing twice succeeds and changes nothing
- **WHEN** a booking whose booker is already erased is erased again
- **THEN** the operation succeeds, and the booking including its recorded erasure instant is unchanged

#### Scenario: The first erasure's instant is the one kept
- **WHEN** a booking is erased and then erased again at a later instant
- **THEN** the recorded erasure instant is the earlier one

#### Scenario: No operation restores erased details
- **WHEN** the package's operations on a booking are inspected
- **THEN** none of them returns an erased booker to carrying contact details

#### Scenario: A concurrent write cannot restore erased details
- **WHEN** one caller is holding a copy of a booking read before its erasure, and writes that copy back after the erasure has happened
- **THEN** the booking remains erased, and the details are not restored by that write

**Irreversibility SHALL hold against writes that never intended to reverse it.** The operation
that undoes an erasure is not a restore anybody wrote; it is an ordinary write — a
cancellation, a status change, anything that carries a booker — arriving with a copy read
before the erasure. Stating irreversibility only against operations that *mean* to restore
would leave the actual failure mode outside the requirement.

### Requirement: Only a caller permitted to read contact details may erase them

Erasure SHALL require the same **sensitive-data access** that reading a booker's contact
details requires, per the `sensitive-data` capability, **in addition to** the section access
every management endpoint requires.

**The gate SHALL be a property of the operation, not a condition inside it.** A caller without
sensitive-data access SHALL be refused before any erasure occurs, rather than reaching a
handler that decides.

The reasoning is that erasure is the most consequential thing the package will do to a booking
and it cannot be undone. A user the site has decided may not so much as read a booker's name
should not be able to destroy it.

**This requirement governs erasure that somebody asks for, and says nothing about erasure that
nobody asks for.** It was written when a caller was the only way an erasure could happen, so its
name and its wording both assume one. A site's own configured retention policy erases with no
user to test, and this rule cannot be applied to it — but it must not be read as forbidding it
either, or a site could never operate a retention policy at all. What governs the unattended case
instead is *An unattended erasure path handles no contact detail*, below, which is where the same
guarantee is delivered by different means. **Neither requirement is an exception to the other:
between them they cover every way an erasure can happen, and the second one says so explicitly.**

#### Scenario: A permitted user can erase
- **WHEN** a backoffice user with section access and sensitive-data access erases a booking's booker
- **THEN** the operation succeeds

#### Scenario: Section access alone is not enough
- **WHEN** a backoffice user with section access but without sensitive-data access attempts an erasure
- **THEN** the request is refused and no booking is changed

#### Scenario: The gate belongs to the operation
- **WHEN** the erase operation's authorization is inspected
- **THEN** sensitive-data access is required to reach it at all, rather than tested within a handler that would otherwise proceed

### Requirement: Erasure is visible to every caller who may see the booking

That a booking's personal data was erased SHALL be reported to any caller permitted to see the
booking, **including one without sensitive-data access**, and SHALL be distinguishable from
details merely withheld from that caller.

**The fact of erasure is not personal data.** It says nothing about who the booker was; it is
an administrative fact about the record. Withholding it would gain no privacy and would cost an
operator the difference between two situations that call for different actions: *"ask a
colleague who can see this"* and *"this is gone, and nobody can retrieve it"*. Reporting one as
the other is the same failure that makes blanking unacceptable, relocated.

**Erasure SHALL take precedence over withholding.** Where details have been erased there is
nothing to withhold, so a caller without sensitive-data access SHALL be told the booking is
erased rather than that its details are withheld.

#### Scenario: A user without sensitive-data access is told the booking is erased
- **WHEN** a caller without sensitive-data access reads a booking whose booker was erased
- **THEN** the response reports the booking as erased, not as withheld

#### Scenario: Erased and withheld are distinguishable
- **WHEN** a caller reads a booking whose details are withheld and one whose details were erased
- **THEN** the two are reported as different states, without the caller having to infer which is which from the absence of a value

#### Scenario: Erasure reveals nothing about the person
- **WHEN** an erased booking is reported to any caller
- **THEN** the response carries the fact and instant of erasure, and no name, email, phone or member key

### Requirement: Booker contact details SHALL have exactly one durable home

The package SHALL keep a booker's contact details in **one** durable location among the stores it
owns, so that erasing that location erases the data it holds. No feature SHALL introduce a second
durable copy **inside the package** — an audit record, a log entry, a report, a cached projection
or a queued message — without erasing it on the same terms.

**This constraint is stated rather than left as an accident of what has been built.** It holds
today: the booking row is the only durable home the package owns, and the delivery API has no
endpoint that reads a booking back. The point of writing it down is that the roadmap adds features
which would each naturally make a copy — an erasure audit trail, retention reporting — and each
would silently reduce erasure to a partial gesture with nothing in the code that looks like a
removal of this guarantee.

**Where the package hands contact details to something it does not own, it SHALL disclose that
rather than claim to erase it.** A message the package sends is delivered to a mailbox, and a
mail server's rejection may quote the recipient into text the host then logs. Neither is a store
the package can enumerate, reach or erase, and pretending otherwise would be the more dangerous
failure: an operator told erasure is complete stops looking.

So the boundary is drawn by **ownership**, and it is drawn explicitly because the alternative
readings both fail. Read as "no copy anywhere", this requirement forbids sending email at all —
which is not what it was written to prevent, since it names confirmation emails as a *feature to
be reconciled*, not as one to be refused. Read as silence, a feature ships that reduces erasure
without anything looking like a decision.

**What the package SHALL therefore do for any such hand-off is:** send the minimum that serves the
purpose, never route contact details anywhere they are not already required, and state the
boundary in the documentation where an operator performing an erasure will meet it.

**An audit record of an erasure SHALL NOT record the erased contact details.** Recording who
erased what is legitimate; recording the address that was erased re-creates the data the
operation exists to remove.

#### Scenario: The single home is verifiable
- **WHEN** the package's durable stores are enumerated
- **THEN** booker contact details appear in exactly one, and erasing there leaves no other copy

#### Scenario: A feature adding a second copy must erase it too
- **WHEN** a change introduces a durable record carrying a booker's contact details inside the package
- **THEN** it either erases that record with the booking or is rejected

#### Scenario: An erasure record names the booking, not the person
- **WHEN** the package records that an erasure occurred
- **THEN** the record identifies the booking and the actor, and carries no erased name, email, phone or member key

#### Scenario: A hand-off outside the package is disclosed rather than claimed
- **WHEN** a feature hands a booker's contact details to something the package does not own, such as the site's mail infrastructure
- **THEN** the package does not claim to erase it, and the documentation states that erasure does not reach it

#### Scenario: A hand-off carries no more than its purpose requires
- **WHEN** the package composes something that leaves it carrying booker contact details
- **THEN** it addresses only the recipient the purpose requires, and no message to any other recipient carries a booker's name, address or telephone number

### Requirement: What erasure does not reach is documented

The package's documentation SHALL state what erasure does, that it cannot be undone, who may
perform it, **that it may also happen without anybody performing it**, and **the boundaries of
what a single erasure achieves**:

- **It erases one booking.** A person who booked more than once has one booking erased per
  operation; erasing everything they booked means erasing each. The package **does** provide a
  way to find them — a search by the booker's email address, per `booking-management` — so the
  documentation SHALL direct an operator to search first and erase each result, and SHALL state
  that the search finds bookings made with **that** address: somebody who booked under two
  addresses has one set found.
- **It can be performed on a booking that has not yet happened**, and doing so leaves the site
  unable to contact somebody who is going to arrive. The package SHALL NOT refuse this — whether
  the basis for holding the details still applies is the site's judgement and not the package's
  — but the documentation SHALL state the consequence plainly.
- **It does not reach anything already sent, on a site that sends messages.** Where a site has
  configured the package to write to bookers, a message delivered before the erasure is in a
  mailbox the package cannot reach, and a mail server's rejection may have quoted the address
  into the host's error log. The documentation SHALL state this boundary **where an operator
  performing an erasure will meet it**, not only where sending is described — an operator
  honouring a right-to-be-forgotten request is reading about erasure, and being told there that
  erasure is complete when it is not is the failure this requirement exists to prevent.

**The documentation SHALL state that erasure also happens on a timer where a site has configured
one**, so that a booking's details disappearing is not read as a fault or as somebody's action.
An operator who finds a booking erased and no record of anyone erasing it SHALL be able to
discover from the documentation that a configured retention period is the other explanation. The
retention setting itself is documented under `booking-retention`; what belongs here is that
erasure is not exclusively something a person does.

Left unstated, the first arrives as a data-protection failure, the second as a support call about
a booking nobody can ring, the third as an answer given in good faith to a data subject that turns
out to be untrue, and the fourth as a bug report about vanishing data.

#### Scenario: The limits of one erasure are documented
- **WHEN** a reader consults the backoffice documentation
- **THEN** it states that erasure applies to one booking, that a person's other bookings are unaffected by it, and how to find them

#### Scenario: Erasing a future booking is permitted and its cost is documented
- **WHEN** an operator erases a booking whose interval has not yet started
- **THEN** the operation succeeds, and the documentation states that the site will no longer be able to contact that booker

#### Scenario: Erasure without an actor is documented
- **WHEN** a reader consults the backoffice documentation
- **THEN** it states that a booking's personal data may also be erased automatically where the site has configured a retention period, and points to where that is described

#### Scenario: What erasure cannot reach on a sending site is documented where erasure is
- **WHEN** a reader consults the backoffice documentation on a site that has configured the package to send messages
- **THEN** it states that erasure does not reach a message already delivered, nor a booker's address quoted by a mail server into the site's own logs

### Requirement: An unattended erasure path handles no contact detail

An erasure performed without a caller — by a site's configured retention policy, or by any
future unattended path — SHALL meet **all** of the following:

- It SHALL select bookings by **time and erasure state alone**, and SHALL accept no contact
  detail as input.
- It SHALL receive **booking identifiers only** from storage: no name, email address, phone
  number or member key.
- It SHALL NOT read, log, report or otherwise handle a contact detail at any point.
- It SHALL NOT be reachable from any HTTP request, and SHALL be configured only where a site's
  configuration is.

**These obligations deliver the same guarantee the caller gate delivers, by a different route.**
The guarantee is that *nothing reaches or destroys a booker's contact details without the access
to read them*. A caller is held to it by being asked for that access. An unattended path cannot
be — there is nobody to ask — so it is held to it by having nothing to read: it never sees a
contact detail, so there is no access it could be exceeding. The exemption is from the
*mechanism*, never from the guarantee.

**It is not "the timer is trusted".** Trust would be an exemption; this is a construction. A
retention sweep that selected rows carrying names and addresses would put personal data in the
hands of the one code path with no user accountable for it, and would falsify this requirement
without a word of it changing — which is precisely why the obligations are on the *shape of the
data the path handles* rather than on the path's good intentions.

**Every erasure path SHALL be covered by one of the two requirements.** A new way to erase SHALL
either require sensitive-data access as its own authorization, per *Only a caller permitted to
read contact details may erase them*, or satisfy every obligation above. **There is no third
option**, and in particular an unattended path that handles contact details for any reason is not
one. This clause is the tripwire: without it, "the retention job is exempt" becomes a precedent
any caller-less code can claim, and the caller gate becomes advisory.

**Configuration is not a weaker gate than the backoffice one.** An unattended path is switched on
in a site's configuration, which needs deployment or server access — strictly more than
membership of a backoffice group. Stated so that "it has no permission check" is not mistaken for
"anybody can turn it on".

#### Scenario: The unattended path handles no contact detail
- **WHEN** the package's unattended erasure path is inspected
- **THEN** it selects bookings by time and erasure state, receives only booking identifiers, and handles no name, email address, phone number or member key

#### Scenario: The selection cannot be widened to carry a person
- **WHEN** the read that feeds an unattended erasure is inspected
- **THEN** its inputs carry no contact detail and its result carries booking identifiers and nothing else

#### Scenario: The unattended path is not reachable from a request
- **WHEN** the package's endpoints are enumerated
- **THEN** none of them performs an unattended erasure sweep or erases more than one booking

#### Scenario: Every erasure path is accounted for
- **WHEN** the package's erasure paths are enumerated
- **THEN** each one either requires sensitive-data access as its own authorization or satisfies every obligation on an unattended path, and none is exempt on any other ground
