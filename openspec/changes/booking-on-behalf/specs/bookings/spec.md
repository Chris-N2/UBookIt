## ADDED Requirements

### Requirement: Placing a booking on a booker's behalf
The booking service SHALL expose an operation that places a booking **on a booker's behalf**:
given a resource, a start instant, a length and the booker's details, it SHALL place a booking
for that booker exactly as a visitor's placement would, save for the terms named below. The
booking it produces SHALL be an ordinary booking in every later respect — the same reference
format drawn from the same port, the same status machine, the same claims, and the same
cancellation, confirmation, decline, move and erasure operate on it unchanged.

**The request SHALL run the placement validation pipeline** — the same rules, in the same
order, producing the same stable codes — with the two policy rules evaluated on an operator's
terms rather than a visitor's, exactly as *Moving a booking* already defines them:

- the **lead-time** rule SHALL be evaluated with a lead of **zero** rather than the resource's
  configured lead, so a booking may be taken for any instant that has not yet passed and for
  none that has;
- the **horizon** rule SHALL NOT be applied.

**These SHALL be the same terms a move places under, not a second set.** The value that carries
them is shared, and the rule bodies SHALL be identical for both callers: a test that the
visitor path is unchanged is a test that the terms differ and that nothing else does.

**Open hours, granularity, duration bounds and conflict SHALL bind an operator exactly as they
bind a visitor.** They are facts about what is physically bookable rather than policy about who
is asking, and an operator who could override them would be recording a booking the business
cannot honour.

**A booking placed on a booker's behalf SHALL be `Confirmed`, whatever the site's `AutoConfirm`
setting says.** Approval exists so that a stranger's request can be reviewed before the time is
committed; the operator placing this one has reviewed it by placing it, and leaving it
`Requested` would ask them to approve their own action — a queue item that means nothing and
that the booker's message would describe as awaiting a decision nobody is waiting for.

**That decision SHALL be taken where a placement's status is already decided**, so that the two
kinds of placement cannot come to disagree about what a new booking is. It SHALL be expressed as
a property of the terms the placement is made under and SHALL NOT be a second reading of the
site's settings.

**The booker's details SHALL be validated exactly as a visitor's are.** A name and a
well-formed email address are required; a telephone number is optional. Operator placement
SHALL NOT create a booker in any state a visitor's placement could not create, and in
particular SHALL NOT produce one without contact details — the two states a booker has are
unchanged, and placement still reaches only the first of them.

**Operator placement SHALL NOT set a member key.** A booking taken at a desk is not evidence
that the person holding it is a member of the site, and recording one on the operator's say-so
would attach a booking to an identity nobody verified.

**An operator's placement SHALL be reported as its own observation.** The observation port
SHALL gain a member for it, for the reason confirming, declining and moving each have one: they
are distinct **acts**, not distinct kinds of booking. Because the booking carries no marker of
who placed it, who placed it is knowable at the moment of placing and at no other — an observer
told only "a booking was placed" could not recover it afterwards, and the package's own
notification adapter must, since the site's internal recipients are not written to.

**That member SHALL default to reporting an ordinary placement, so it is an addition and not a
break.** An implementation written before operator placement existed SHALL keep compiling and
SHALL keep being told that a booking was placed — which is true, and is what such an
implementation meant. The default SHALL err in the direction of under-reporting the
distinction: it SHALL never invent one, and SHALL never lose the placement itself.

**A refused placement SHALL report nothing at all**, exactly as a refused placement always has.

#### Scenario: An operator places a booking for a booker
- **WHEN** the operation is called with a resource, a start and length that resource would accept, and a booker's name and email
- **THEN** a booking is placed for that booker, carrying a reference, claiming that resource over that interval

#### Scenario: Placement inside the resource's lead time succeeds
- **WHEN** a resource requires 24 hours' notice and a booking is placed on a booker's behalf for an instant one hour from now
- **THEN** placement succeeds, because operator placement evaluates lead time as zero

#### Scenario: Placement in the past is refused
- **WHEN** a booking is placed on a booker's behalf for an interval whose start has already passed
- **THEN** it fails with `lead-time`, and nothing is persisted

#### Scenario: Placement beyond the horizon succeeds
- **WHEN** a resource's booking horizon is 90 days and a booking is placed on a booker's behalf for a date 120 days ahead
- **THEN** placement succeeds, because operator placement applies no horizon

#### Scenario: Open hours still bind an operator
- **WHEN** a booking is placed on a booker's behalf for an interval outside the resource's open hours
- **THEN** it fails with `outside-open-hours`, and nothing is persisted

#### Scenario: A conflict still binds an operator
- **WHEN** a booking is placed on a booker's behalf over an interval a blocking claim already holds
- **THEN** it fails with `conflict`, and nothing is persisted

#### Scenario: Duration bounds and granularity still bind an operator
- **WHEN** a booking is placed on a booker's behalf for a length below the resource's minimum, or for a start the resource's granularity does not admit
- **THEN** it fails with the pipeline's own stable code for that rule, and nothing is persisted

#### Scenario: An operator's booking is confirmed on an approval site
- **WHEN** a booking is placed on a booker's behalf while the site's `AutoConfirm` setting is off
- **THEN** the booking's status is `Confirmed`

#### Scenario: A visitor's booking is unaffected by that decision
- **WHEN** a visitor places a booking while the site's `AutoConfirm` setting is off
- **THEN** the booking's status is `Requested`, exactly as before

#### Scenario: A booker without an email is refused
- **WHEN** a booking is placed on a booker's behalf with no email address, or with one that is not well formed
- **THEN** placement is refused on the same terms a visitor's placement is refused, and nothing is persisted

#### Scenario: The operator's booking carries no member key
- **WHEN** a booking placed on a booker's behalf is read back
- **THEN** its booker holds contact details and no member key

#### Scenario: An operator's booking behaves as any other afterwards
- **WHEN** a booking placed on a booker's behalf is subsequently confirmed, declined, cancelled, moved or erased
- **THEN** each operation behaves exactly as it does for a booking a visitor placed, and the booking carries no marker distinguishing how it was taken

#### Scenario: An operator's placement is reported as its own observation
- **WHEN** a booking is placed on a booker's behalf and an observer is attached
- **THEN** the observer is told of an operator's placement, and not of an ordinary one

#### Scenario: A visitor's placement is reported as it always was
- **WHEN** a visitor places a booking and an observer is attached
- **THEN** the observer is told of a placement, exactly as before

#### Scenario: An observer written before this change still hears the placement
- **WHEN** a booking is placed on a booker's behalf and the attached observer implements only the members that existed before operator placement
- **THEN** it compiles unchanged and is told that a booking was placed

#### Scenario: A refused operator placement reports nothing
- **WHEN** an operator's placement is refused by any rule
- **THEN** no observation of any kind is made

## MODIFIED Requirements

### Requirement: Availability and placement service ports
`UBookIt.Core` SHALL expose an availability query service (free-time and slot projection for a resource and date range, per `availability`) and a booking service (placement running the validation pipeline, cancellation, **confirmation and decline** applying the status machine, **erasure of a booking's booker contact details**, and **moving a booking to a new interval** per *Moving a booking*, and **placing a booking on a booker's behalf** per *Placing a booking on a booker's behalf*).

**Both SHALL depend only on ports `UBookIt.Core` itself defines**, so implementations can be swapped without changing Core and `UBookIt.Core` continues to carry **no package reference of any kind**. The availability service SHALL depend only on the two store ports (`IResourceStore`, `IBookingStore`). The booking service SHALL depend on those two and, additionally, on the **observation port** through which it reports what it has done — see *Placement and status changes are observable* — and on the **reference-generation port** from which it draws the quotable reference it assigns at placement.

**The generation port SHALL be a Core-defined port a host may substitute**, and it SHALL NOT be the thing that makes a reference unique — the store guarantees that, so the port promises only a well-formed reference drawn unpredictably. It exists as a port rather than as a helper so that a caller can hand placement a reference already in use and observe what it does; at 27⁸ values, waiting for a real collision is not a test strategy. Core MAY ship a default implementation, since drawing a random value needs no package reference and Core already generates identity inline.

*The enumeration is widened rather than dropped — for the second time, and on the same reasoning. What it protected is unchanged: the constraint was never about the number two. It was that Core owns its own dependencies, that a host can substitute any of them, and that nothing drags a framework into the domain — all of which a Core-defined observation port satisfies, and which an Umbraco type in this assembly would not.*

Core services concerned with services (per `service-booking`) MAY additionally depend on the service read port (`IServiceStore`). No Core service SHALL depend on a management store: the read ports are the only pathway anonymous delivery traffic reaches storage through.

The read port SHALL additionally expose a listing of every resource of a given type key, unpaged. It is unpaged deliberately — its consumer is a candidate pool, and a truncated pool silently produces a wrong answer rather than an error. This is distinct from the management port's type-key listing, which reports type keys with usage counts for a picker and SHALL remain management-only.

The booking store port SHALL additionally expose a claims read spanning several resource ids in one call, with the same half-open overlap and status-neutral semantics as the single-resource read. It exists so a query over a candidate pool does not issue one round trip per candidate; it SHALL return the same claims the single-resource read would return for each of those resources.

The availability query service SHALL additionally expose a pure bookable-start projection taking an already-loaded resource aggregate together with already-read claims, for callers that must issue no reads of their own. It SHALL produce results identical to the id-based query for the same resource, date range, and stored state — it changes only who performs the reads, never what is computed. Claims belonging to other resources SHALL be ignored rather than rejected, so one batched read can be passed for every candidate in turn.

Only one such member SHALL be added. An additional overload taking the resource but reading claims itself would leave the batched claims read without a caller on the path it exists to serve, and would ship untested public surface on a port interface.

#### Scenario: Services are testable with in-memory stores
- **WHEN** the availability and booking services are constructed with in-memory store implementations
- **THEN** all placement, cancellation, confirmation, decline, free-time, and slot-projection behaviour in these specs is exercisable without a database

#### Scenario: Moving is testable with in-memory stores
- **WHEN** the booking service is constructed with an in-memory store implementation
- **THEN** every move behaviour in *Moving a booking* and *Atomic move contract* is exercisable without a database

#### Scenario: Operator placement is testable with in-memory stores
- **WHEN** the booking service is constructed with an in-memory store implementation
- **THEN** every behaviour in *Placing a booking on a booker's behalf* is exercisable without a database, and it needs no store member the other operations do not already use

#### Scenario: A host can substitute reference generation
- **WHEN** a caller constructs the booking service with its own implementation of the generation port
- **THEN** placement assigns the references that implementation returns, and a reference already in use is answered by drawing another rather than by failing the booking

#### Scenario: Core carries no framework dependency
- **WHEN** `UBookIt.Core`'s package references are inspected
- **THEN** there are none, and every port it depends on is a type it declares itself

#### Scenario: Type listing returns every resource of the type
- **WHEN** the read port is asked for resources of a type key whose population exceeds any default page size
- **THEN** every resource of that type is returned, with no paging parameters available to truncate the result

#### Scenario: Management type listing stays management-only
- **WHEN** the read port's surface is inspected
- **THEN** it exposes no type-key-with-usage-count listing; that remains on the management port

#### Scenario: Batched claims match single-resource reads
- **WHEN** claims are read for three resource ids in one call over a range
- **THEN** the result is exactly the union of what the single-resource read returns for each of those three ids over the same range

#### Scenario: The pure projection matches the id-based query
- **WHEN** the availability query is issued for a resource id that has bookings in range, and the pure projection is issued for that same resource with the claims read separately
- **THEN** both produce identical results, entry for entry, including each entry's minimum and maximum

#### Scenario: Claims for other resources are ignored
- **WHEN** the pure projection is passed a claim belonging to a different resource that would, if applied, remove all of this resource's free time
- **THEN** the result is unchanged from passing no claims at all

**BREAKING — published port.** `IBookingStore` gains an operation that erases a booking's
booker. A host supplying its own store implementation must add it, and must satisfy the
absorption and irreversibility the port documents — those are guarantees the package makes to a
data subject, so they belong to the port rather than to one storage engine.

*The booking service's enumeration is widened a third time, on the reasoning already recorded
above: the constraint was never about how many verbs there are. Erasure is a domain operation
on a booking — it mutates the aggregate through a named method and persists through the store
port Core already defines — so it belongs to this service rather than beside it, and it drags
nothing new into Core. Listing it matters because this requirement is where a reader derives
what `UBookIt.Core`'s booking service IS; leaving it at two would describe a surface that no
longer exists.*

**BREAKING — published port, and the second such addition in this capability.** `IBookingStore`
gains a read returning the **ids** of bookings whose interval ended before a given instant and
whose booker has not been erased. A host supplying its own store implementation must add it.

**It returns identifiers rather than bookings, and that is a constraint on the port rather than a
convenience for its caller.** Its consumer is the unattended retention sweep, which per
`booker-erasure` must handle no contact detail at any point — that obligation is what allows an
erasure with no caller to exist without weakening the rule that only somebody permitted to read
contact details may destroy them. A substituted implementation that returned whole bookings would
satisfy the compiler and falsify the guarantee, so the port SHALL NOT be widened to carry a
booker, and a host implementing it inherits that restriction.

**It SHALL apply no status filter**, for the same reason the subject search carries none: a
booking that was cancelled, declined or never confirmed holds a real person's details exactly as
firmly as one that went ahead, and a filter here would be a way for the sweep to under-erase.

*The store port's enumeration grows for the same reason the service's did, and the note above
applies unchanged: the constraint was never about how many members there are. This one adds no
dependency to Core and no verb to the booking service — retention erases through the service
operation that already exists, so what is new here is only a way to find what the clock has
caught.*

*The booking service's enumeration is widened a **fourth** time, and the recorded reasoning
still holds: the constraint was never about how many verbs there are. Confirmation and decline
are domain operations on a booking — each drives a transition the status machine has declared
since v1 and persists through the store port Core already defines — so they belong to this
service rather than beside it, and they drag nothing new into Core.*

**BREAKING — published port, the third such addition in this capability, and the first in a
release after 17.0.0.** `IBookingStore` gains the move write defined by *Atomic move contract*.
A host supplying its own store implementation must add it, and must satisfy that contract's
atomicity, self-exclusion and conditional-write obligations — they are what make a move safe
under concurrent placement, so they belong to the port rather than to one storage engine. It
lands in a minor release, which is the only place the versioning policy permits a break.

*The booking service's enumeration is widened a **fifth** time. Moving is a domain operation on
a booking — a named method on the aggregate, validated by the pipeline placement already runs,
persisted through the store port Core already defines — and it drags nothing new into Core.*

*The booking service's enumeration is widened a **sixth** time, and the recorded reasoning is
unchanged: the constraint was never about how many verbs there are. Operator placement is a
domain operation on a booking — it runs the pipeline placement already runs, assigns a
reference from the port Core already defines, and persists through the store's existing atomic
placement. **It adds no member to `IBookingStore`**, which is why this widening carries no
BREAKING note: what is new is a second set of terms to place under, not a second way to write.*

### Requirement: Booking a single resource requires that resource to permit it
**Visitor** placement of a booking that claims **one resource, named by the caller** SHALL be
refused when that resource withholds permission to be booked on its own, with the
stable code `resource-not-directly-bookable`.

The refusal SHALL be evaluated in `UBookIt.Core`, on the visitor's single-resource
placement entry point, **before** the request is composed into a claim set. Placing it in a
transport layer would leave every other caller open — the no-JavaScript booking
flow reaches placement in process without crossing an HTTP boundary at all — and
the rule is a property of booking, not of any one way of asking for one.

Placement that claims resources **derived from a service's roles** SHALL NOT be
subject to this rule, whatever those resources permit. The distinction SHALL be
structural rather than a flag on the request: a single-resource entry point *is*
the direct path, and service placement composes its own claim set without passing
through it. A parameter asserting "this is a direct booking" would restate what the
call site already means, in a form a caller can get wrong.

**`UBookIt.Core` SHALL offer more than one single-resource placement entry point, and which
one a caller reaches SHALL be what decides whether this rule applies.** The visitor's entry
point carries it. **Operator placement on a booker's behalf SHALL NOT**, per *Placing a
booking on a booker's behalf*: the permission exists so that a stranger cannot assemble a
combination the business cannot deliver — a therapist with no room — and an operator taking a
booking at the desk is the person the site trusts to make exactly that judgement. A rule that
refused them would be protecting the site from its own staff.

**That waiver SHALL be structural on the same terms as the service exemption, and for the same
reason.** A parameter asserting "this caller is an operator" would restate the call site in a
form a caller can get wrong, and the value that already carries an operator's terms SHALL NOT
acquire a member for it: the rule is not evaluated on the pipeline's terms, it is not reached
at all. Adding a third policy member to those terms would put a rule in two places and let them
disagree.

A service of one role and a count of one therefore books a resource that withholds
the permission, and SHALL succeed. That is not a loophole: the business has offered
that service, and the resource is being booked as part of it.

The refusal SHALL be reported as **its own** cause, distinct from unavailability.
It does not vary with the instant asked for, the length, the calendar or how busy
the resource is, so reporting it as no-availability would send a booker to try
another time that cannot help, and an editor to inspect opening hours that are not
wrong.

The refusal SHALL NOT be treated as a deterministic per-candidate refusal in
service placement's all-fail classification, because service placement never
reaches it.

#### Scenario: A resource withholding the permission cannot be booked alone
- **WHEN** a booking is placed naming a single resource that withholds direct booking, at a time it is otherwise free and open
- **THEN** placement fails with the stable code `resource-not-directly-bookable`, and no claim is persisted

#### Scenario: The same resource is bookable as part of a service
- **WHEN** the same resource, at the same instant, is claimed by a service booking whose role resolves to it
- **THEN** placement succeeds

#### Scenario: A single-role service of count one is still a service
- **WHEN** a service with one role of count 1 resolves to a resource that withholds direct booking, and is placed
- **THEN** placement succeeds, because the resource is being booked as part of a service rather than on its own

#### Scenario: A resource granting the permission is unaffected
- **WHEN** a booking is placed naming a single resource that permits direct booking
- **THEN** placement proceeds through the existing validation pipeline exactly as before

#### Scenario: The refusal precedes the rule pipeline
- **WHEN** a booking is placed naming a resource that withholds direct booking, for an interval that would also fail on open hours
- **THEN** the result carries `resource-not-directly-bookable` rather than `outside-open-hours`, because the request was never one this resource accepts

#### Scenario: The refusal is not a conflict and invites no retry
- **WHEN** a booking naming a withholding resource is refused
- **THEN** the code is `resource-not-directly-bookable`, never `conflict`, and the same request will be refused identically however many times it is retried

#### Scenario: An operator books a withholding resource directly
- **WHEN** an operator places a booking on a booker's behalf naming a single resource that withholds direct booking, at a time it is otherwise free and open
- **THEN** placement succeeds, and the booking claims that resource exactly as any other booking would

#### Scenario: The visitor path is unchanged by the operator path existing
- **WHEN** a visitor places a booking naming a resource that withholds direct booking, on an installation where operator placement is available
- **THEN** it fails with `resource-not-directly-bookable` exactly as before, and no visitor-reachable parameter, header or request field can select the operator's entry point

#### Scenario: The waiver is not a member of the operator's terms
- **WHEN** the value carrying an operator's placement terms is inspected
- **THEN** it carries no member concerning direct bookability, because that rule is decided by which entry point was called rather than evaluated on those terms

### Requirement: Booking status machine
Booking status SHALL be one of `Requested`, `Confirmed`, `Declined`, `Cancelled`. Permitted transitions SHALL be exactly: `Requested → Confirmed`, `Requested → Declined`, `Requested → Cancelled`, and `Confirmed → Cancelled`. Any other transition SHALL be rejected with a stable failure code `invalid-status-transition`.

The status a successful **visitor** placement yields SHALL be derived from the site's `AutoConfirm`
setting: `Confirmed` when it is on, `Requested` when it is off. `AutoConfirm` SHALL default to
**on**, so a site that has configured nothing gets exactly the auto-confirm behaviour every
prior version shipped. The derivation SHALL happen at the single placement site the
direct, the service and the operator paths all run through, so they cannot disagree about what a
new booking is.

**A placement on a booker's behalf SHALL yield `Confirmed` whatever `AutoConfirm` says**, per
*Placing a booking on a booker's behalf*. The single derivation site therefore reads the terms
the placement was made under as well as the site's setting; it SHALL NOT be duplicated, and no
caller SHALL choose a status of its own. Approval is a gate on a stranger's request, and the
operator placing this one passed it by placing it.

`Declined` SHALL be produced only by the decline operation. `Requested` SHALL be produced only
by a **visitor's** placement under `AutoConfirm` off — an operator's placement never produces it,
under either setting. No other pathway SHALL produce either status.

#### Scenario: Placement under auto-confirm
- **WHEN** a booking is successfully placed while `AutoConfirm` is on
- **THEN** its status is `Confirmed`

#### Scenario: Placement under approval
- **WHEN** a visitor's booking is successfully placed while `AutoConfirm` is off
- **THEN** its status is `Requested`, and it holds its slot exactly as a `Confirmed` booking would

#### Scenario: Service placement agrees with direct placement
- **WHEN** a booking is successfully placed for a service while `AutoConfirm` is off
- **THEN** its status is `Requested`, the same status a direct placement yields under the same setting

#### Scenario: Cancelling a confirmed booking
- **WHEN** a `Confirmed` booking is cancelled
- **THEN** its status becomes `Cancelled`

#### Scenario: Cancelling a requested booking
- **WHEN** a `Requested` booking is cancelled
- **THEN** its status becomes `Cancelled` — a booker who withdraws does not need the request approved first

#### Scenario: Cancelling twice is rejected
- **WHEN** a `Cancelled` booking is cancelled again
- **THEN** the operation fails with code `invalid-status-transition` and the status remains `Cancelled`

#### Scenario: An operator's placement under approval is confirmed
- **WHEN** a booking is successfully placed on a booker's behalf while `AutoConfirm` is off
- **THEN** its status is `Confirmed`, not `Requested`

#### Scenario: An operator's placement under auto-confirm is confirmed too
- **WHEN** a booking is successfully placed on a booker's behalf while `AutoConfirm` is on
- **THEN** its status is `Confirmed`, the same status the setting would have produced anyway

#### Scenario: The status is still decided in one place
- **WHEN** the direct, service and operator placement paths are each followed to where a new booking's status is chosen
- **THEN** all three reach the same single site, and none of them carries a status chosen by its caller
