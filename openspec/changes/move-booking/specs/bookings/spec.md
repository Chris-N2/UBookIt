<!--
GUARANTEE DIFF for the two wholesale replacements below.

"Availability and placement service ports" — 8 SHALL blocks, 8 scenarios, 3 BREAKING notes:
  SHALL 1  availability service + booking service, enumerated verbs  → CARRIED, enumeration
                                                                        widened to name move
  SHALL 2  depend only on Core-defined ports; no package reference    → CARRIED unchanged
  SHALL 3  generation port substitutable, not the uniqueness guarantee → CARRIED unchanged
  SHALL 4  no Core service depends on a management store             → CARRIED unchanged
  SHALL 5  read port lists every resource of a type, unpaged         → CARRIED unchanged
  SHALL 6  booking store exposes a batched claims read               → CARRIED unchanged
  SHALL 7  pure bookable-start projection; one member only           → CARRIED unchanged
  SHALL 8  IBookingStore gains erasure / retention-id read (BREAKING) → CARRIED unchanged; a
                                                                        third BREAKING note is
                                                                        ADDED for the move write
  Scenarios 1-8 ALL CARRIED verbatim. One ADDED: move is exercisable without a database.
  The italic widening notes are history and are carried as they stand; a fifth is added.

"Placement and status changes are observable" — 6 SHALL blocks, 10 scenarios, 1 BREAKING note:
  SHALL 1  four events reported through a Core port; host observes   → CARRIED, amended to five
  SHALL 2  only after storage agreed, only on success                → CARRIED unchanged
  SHALL 3  an observer cannot affect the operation                   → CARRIED unchanged
  SHALL 4  the cost (no retry, no queue) is stated                   → CARRIED unchanged
  SHALL 5  a report carries the booking and nothing derived from it  → CARRIED, and the move's
                                                                        previous interval is
                                                                        stated to be a FACT the
                                                                        booking no longer holds,
                                                                        not a derivation
  SHALL 6  no status-change report need describe what changed       → SUPERSEDED by a stronger
                                                                        claim: the status-change
                                                                        reports still need not,
                                                                        and the ONE report that
                                                                        must is named, with why
  BREAKING (confirmed/declined members, pre-17.0.0)                  → CARRIED as history; a
                                                                        second BREAKING note is
                                                                        ADDED for the moved member
  Scenarios 1-10 ALL CARRIED verbatim. Three ADDED: a moved booking is reported with its
  previous interval; a refused move reports nothing; a throwing observer does not break a move.

  DELIBERATE DROPS: none. SHALL 6 is superseded, not dropped — the sentence it replaces is
  restated for the four reports it was true of.
-->

## ADDED Requirements

### Requirement: Moving a booking
The booking service SHALL expose an operation that moves a booking: given a booking's id, a
new start instant and a new length, it SHALL place that same booking at the new interval.
**A move is a placement of the booking it already is.** The booking's id, reference, status,
booker, service attribution and every resource claim SHALL be exactly what they were before;
only the interval changes, and the zone id the booking carries becomes the zone the new
interval was validated against, on the same terms as any placement.

**A move SHALL be permitted from `Requested` and `Confirmed` and from nothing else.** A
cancelled or declined booking holds no time to move. An attempt from any other status SHALL
fail with `invalid-status-transition`, the store SHALL NOT be touched, and nothing SHALL be
reported. The status machine's four transitions are unchanged: a move is not a transition,
and a moved booking's status is the status it had.

**The new interval SHALL run the placement validation pipeline** — the same rules, in the same
order, producing the same stable codes, against every resource the booking claims — with two
rules evaluated on an operator's terms rather than a visitor's:

- the **lead-time** rule SHALL be evaluated with a lead of **zero** rather than the resource's
  configured lead, so a booking can be moved to any instant that has not yet passed and to none
  that has. An operator moving a booking closer in is the person the site trusts to decide
  that; the guard that remains is the one nobody can be trusted to waive, since a booking in
  the past holds a slot that cannot be used and moves the booking into the retention sweep's
  window;
- the **horizon** rule SHALL NOT be applied.

**That relaxation is a property of operator placement, not of moving**, and it is stated here
so that recording a booking on a customer's behalf, when that arrives, inherits it rather than
inventing a third set of terms. Open hours, granularity, duration bounds and conflict are
facts about what is physically bookable and SHALL bind an operator exactly as they bind a
visitor.

**A move to the interval the booking already holds SHALL be refused** with a new stable code,
`interval-unchanged`, on the same grounds as cancelling twice: a caller told "moved" when
nothing changed cannot tell a completed action from a rejected one. The check SHALL run before
any store access.

**Moving SHALL keep every claim.** A booking placed for a service moves with the resources it
was assigned; no assignment is re-run, and a claimed resource that is unavailable at the new
interval SHALL produce `conflict` (or the open-hours failure, as the pipeline decides) rather
than a substitution. Changing which resources a booking claims is a different operation, not
built here.

**An unknown booking SHALL fail with `booking-not-found`.** No other new failure code is
introduced.

**An erased booker's booking SHALL move on the same terms as any other.** Erasure removed the
person's details, not the booking's claim on its time.

#### Scenario: A confirmed booking is moved to a free interval
- **WHEN** the move operation is called for a `Confirmed` booking with a new start and length that every claimed resource would accept
- **THEN** the result is success, the stored booking holds the new interval and no longer holds the old one, and its reference, status, booker, service and claims are unchanged

#### Scenario: A requested booking moves without changing status
- **WHEN** the move operation is called for a `Requested` booking
- **THEN** the stored booking holds the new interval and remains `Requested`

#### Scenario: Moving a cancelled booking is refused
- **WHEN** the move operation is called for a `Cancelled` or `Declined` booking
- **THEN** it fails with `invalid-status-transition`, the stored booking is unchanged, and nothing is reported

#### Scenario: A move inside the resource's lead time succeeds
- **WHEN** a resource requires 24 hours' notice and a booking on it is moved to an instant one hour from now
- **THEN** the move succeeds, because operator placement evaluates lead time as zero

#### Scenario: A move into the past is refused
- **WHEN** a booking is moved to an interval whose start has already passed
- **THEN** it fails with `lead-time`, and the stored booking is unchanged

#### Scenario: A move beyond the horizon succeeds
- **WHEN** a resource's booking horizon is 90 days and a booking on it is moved to a date 120 days ahead
- **THEN** the move succeeds, because operator placement applies no horizon

#### Scenario: A move outside open hours is refused
- **WHEN** a booking is moved to an interval not fully inside the claimed resource's open hours
- **THEN** it fails with `outside-open-hours`, exactly as a placement there would

#### Scenario: A move onto another booking is refused
- **WHEN** a booking is moved to an interval that overlaps a blocking booking on one of its claimed resources
- **THEN** it fails with `conflict`, and the stored booking still holds its original interval

#### Scenario: A move to the interval already held is refused
- **WHEN** the move operation is called with the booking's own current start and length
- **THEN** it fails with `interval-unchanged` and the store is not touched

#### Scenario: A service booking moves with its claims
- **WHEN** a booking placed for a two-role service is moved to an interval at which both claimed resources are free
- **THEN** it holds the new interval with the same two claims, and no other resource is considered

#### Scenario: A service booking whose resource is busy does not swap it
- **WHEN** a booking placed for a service is moved to an interval at which one of its claimed resources is already booked, while another eligible resource is free
- **THEN** it fails with `conflict` rather than claiming the free resource

#### Scenario: A booking with an erased booker can move
- **WHEN** the move operation is called for a booking whose booker has been erased
- **THEN** it succeeds on the same terms as any other, and the booker remains erased

#### Scenario: An unknown booking is reported as not found
- **WHEN** the move operation is called with an id no booking has
- **THEN** it fails with `booking-not-found` and nothing is reported

### Requirement: Atomic move contract
The booking store port SHALL define a move write: given a booking's id, its new interval, and
the statuses from which a move is permitted, it SHALL release the booking's claim on its old
interval and take its claim on the new one **in one atomic step** with respect to conflict
detection. At no instant SHALL a concurrent placement observe the booking holding both
intervals or neither.

**The conflict check SHALL exclude the booking being moved.** A booking's own claim rows
overlap its own new interval whenever the two intervals overlap; a check that counted them
would refuse every small shift.

**The check and the write SHALL run under the same locks placement takes**, for every resource
the booking claims, in the same deterministic order, so that a move and a placement on the
same resource cannot both succeed for overlapping intervals, and two moves sharing resources
cannot deadlock.

**The write SHALL be conditional on the stored status still permitting a move.** The service
reads the booking, decides, then writes; a cancellation committing between the two would
otherwise let a cancelled booking move. The condition SHALL be inside the write, and a write
that changed no row because the status had moved on SHALL be reported to the caller as
`invalid-status-transition` rather than as success.

**The write SHALL touch the interval columns and nothing else** — not the status, not the
booker — on the same disjoint-columns reasoning the status and erasure writes already follow.

Core defines this contract and SHALL honour it in its in-memory test double.

#### Scenario: A move and a placement race for the new interval
- **WHEN** a move to interval I and a placement at interval I on the same resource execute concurrently
- **THEN** exactly one succeeds and the other fails with `conflict`

#### Scenario: A placement takes the old interval only after the move commits
- **WHEN** a booking is moved from interval I to interval J and, concurrently, a placement is attempted at I on the same resource
- **THEN** the placement succeeds only if the move has committed, and never while the booking still holds I

#### Scenario: A move does not conflict with itself
- **WHEN** a booking holding 09:00–10:00 is moved to 09:30–10:30 on a resource with no other booking
- **THEN** the move succeeds

#### Scenario: A cancel landing between read and write wins
- **WHEN** the move operation has read a `Confirmed` booking and, before its write, another caller cancels that booking
- **THEN** the move fails with `invalid-status-transition`, the booking remains `Cancelled`, and its interval is unchanged

#### Scenario: Two moves of the same booking
- **WHEN** two moves of the same booking to different free intervals execute concurrently
- **THEN** both complete without deadlock, the booking ends holding exactly one of the two intervals, and no claim on the other remains

#### Scenario: The move write leaves the status and booker alone
- **WHEN** a booking is moved while a stale aggregate of it would have carried a different status or booker
- **THEN** the stored status and booker are what the store held, not what the aggregate carried

## MODIFIED Requirements

### Requirement: Availability and placement service ports
`UBookIt.Core` SHALL expose an availability query service (free-time and slot projection for a resource and date range, per `availability`) and a booking service (placement running the validation pipeline, cancellation, **confirmation and decline** applying the status machine, **erasure of a booking's booker contact details**, and **moving a booking to a new interval** per *Moving a booking*).

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

### Requirement: Placement and status changes are observable
`UBookIt.Core` SHALL report, through a port it declares itself, that a booking has been
**placed**, that one has been **confirmed**, that one has been **declined**, that one has
been **cancelled**, and that one has been **moved**. A host SHALL be able to observe all five
without the package sending anything on its behalf.

**Each SHALL be reported only after storage has agreed, and only on success.** An event means
*this happened*; raising one before the store has committed would announce a booking that may
not exist, and raising one on failure would announce one that does not.

**Nothing an observer does SHALL affect the operation it is observing.** An observer that
throws SHALL NOT change what the caller is told, SHALL NOT undo the booking, and SHALL NOT
prevent the operation from reporting success. The booking is already stored by the time an
observer runs, so an exception escaping would tell a visitor their booking failed when it did
not — and a visitor told that books again. The failure this prevents is a double booking
caused by somebody else's handler, which is worse than any notification is valuable.

**The cost of that SHALL be stated rather than implied:** an observer that throws is a
notification nobody receives, and the package neither retries nor queues it.

A report SHALL carry the booking and nothing derived from it, so there is one source of truth
and no computed value to keep in step. **The move report additionally carries the interval the
booking held before the move.** That is not a derivation — it is a fact the booking no longer
holds and nothing else records — and it is carried so that whoever is told can say where the
booking moved *from*, which a message saying only where it now is cannot.

**No status-change report SHALL need to describe what changed, and the move report is the one
report that must.** The status machine permits cancellation only from `Requested` or
`Confirmed`, and confirmation and decline only from `Requested`, so each of those reports is by
construction "this booking has just become what its status says"; an attempt from any other
status fails and reports nothing. A move changes no status: the booking after is in every
respect the booking before except its interval, so "this booking has just moved" is meaningless
without the interval it left. The exception is named here, with its reason, so that it cannot be
read as licence for any other report to grow a before-and-after.

**BREAKING — published port.** The observation port gains a confirmed and a declined member. A
host supplying its own observer must add both. There SHALL be no default implementations: an
observer silently deaf to declines would be a worse outcome than a compile error, on a port
whose entire purpose is that a host hears what happened.

**BREAKING — published port, the second such addition, and the first after 17.0.0.** The
observation port gains a moved member, carrying the booking and its previous interval. A host
supplying its own observer must add it, and on the reasoning above there SHALL be no default
implementation. It lands in a minor release, the only place the versioning policy permits a
break.

#### Scenario: A placed booking is reported
- **WHEN** a booking is placed successfully
- **THEN** the placement is reported once, after the booking is stored, carrying that booking

#### Scenario: A confirmed booking is reported
- **WHEN** a booking is confirmed successfully
- **THEN** the confirmation is reported once, after the status is stored, carrying that booking

#### Scenario: A declined booking is reported
- **WHEN** a booking is declined successfully
- **THEN** the decline is reported once, after the status is stored, carrying that booking

#### Scenario: A cancelled booking is reported
- **WHEN** a booking is cancelled successfully
- **THEN** the cancellation is reported once, after the status is stored, carrying that booking

#### Scenario: A moved booking is reported with where it came from
- **WHEN** a booking is moved successfully
- **THEN** the move is reported once, after the new interval is stored, carrying that booking as it now stands and the interval it held before

#### Scenario: A failed placement reports nothing
- **WHEN** placement fails for any reason
- **THEN** nothing is reported

#### Scenario: A refused transition reports nothing
- **WHEN** confirmation, decline, or cancellation is attempted on a booking whose status does not permit it
- **THEN** it fails with `invalid-status-transition` and nothing is reported

#### Scenario: A refused move reports nothing
- **WHEN** a move fails for any reason — a rule of the pipeline, an unchanged interval, a status that does not permit it, or a booking that does not exist
- **THEN** nothing is reported

#### Scenario: Cancelling an already-cancelled booking reports nothing
- **WHEN** cancellation is attempted on a booking that is already cancelled
- **THEN** it fails with `invalid-status-transition` and nothing is reported

#### Scenario: A throwing observer does not break the booking
- **WHEN** an observer throws while being told a booking was placed
- **THEN** the placement still reports success to its caller, and the booking remains stored and unchanged

#### Scenario: A throwing observer does not break a confirmation
- **WHEN** an observer throws while being told a booking was confirmed
- **THEN** the confirmation still reports success to its caller, and the stored status remains `Confirmed`

#### Scenario: A throwing observer does not break a move
- **WHEN** an observer throws while being told a booking was moved
- **THEN** the move still reports success to its caller, and the stored booking holds the new interval

#### Scenario: Observation is substitutable
- **WHEN** the booking service is constructed with an observer that only records what it was told
- **THEN** placement, confirmation, decline, cancellation and move behaviour is unchanged and every report is exercisable without a database
