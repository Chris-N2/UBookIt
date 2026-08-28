## Context

Every backoffice booking screen waits on one missing capability: there is no way to
enumerate bookings. `IBookingStore` answers "which claims overlap this range for this
resource" (availability), "give me this booking" and "place/update this booking". Nothing
lists.

The repository has already solved this shape twice. `IResourceStore` /
`IResourceManagementStore` and `IServiceStore` / `IServiceManagementStore` separate the
delivery read from the backoffice read, and `resource-management` is a capability in its
own right. This change follows that path for bookings and stops at the port — no API, no
UI.

Two facts found while exploring shape the whole design, and both are measured rather than
assumed:

1. **A booking does not record its service.** `BookingRow` has no `ServiceId`; the only
   one in the schema is on `ServiceRoleRow`. `ServiceBookingService.PlaceAsync` resolves
   candidate resources *using* the service and then places an ordinary booking. So a
   service filter is not a feature this change declined — it is one the data cannot
   answer.
2. **A claim carries only a resource id.** `ClaimRow` is `(Id, BookingId, ResourceId)`.
   A list row that shows "Meeting Room A" needs a join; without it every row costs a
   lookup per claim.

## Goals / Non-Goals

**Goals:**

- One query that can back a backoffice list without a second round trip per row.
- A window the caller cannot forget, bounded by the guardrail that already exists for the
  same reason.
- Deterministic paging, so page 2 cannot repeat or skip a row from page 1.
- Follow the existing store/service conventions rather than inventing a third.
- Nothing in this change constrains what the eventual UI looks like.

**Non-Goals:**

- Any mutation, including cancel.
- Search by booker, or any unwindowed lookup.
- Filtering by service (see D1).
- Approval workflow, or making `Requested` reachable.
- Management API endpoints and backoffice elements.

## Decisions

### D1. The query is windowed, and the window is required

A booking table grows without bound. An unwindowed list is the same cost hole that
`SiteBookingSettings.MaxQueryRangeDays` (default 31) already exists to close for
availability, and `AvailabilityService` already rejects an over-wide span with
`FailureCodes.DateRangeTooLarge` (`date-range-too-large`). This query is bounded the same
way, by the same setting, returning the same code.

The window matches a booking by **overlap**, half-open `[fromUtc, toUtc)`, exactly as
`IBookingStore.GetClaimsAsync` matches claims. A booking that starts before the window and
runs into it is on that week's list, which is what a person asking "what's on this week"
means.

*Rejected: an optional window.* It makes the expensive call the easy one to write.
*Rejected: a separate unbounded "recent bookings" query.* That is the search feature, and
it wants its own index and its own change — see the proposal's non-goals.

**The cost of a required window is real and is accepted here rather than argued away:** a
booking whose date nobody remembers cannot be found through this port. That is the trap a
support query walks into, and it is why search is named as a follow-up rather than left
implied.

### D2. The guardrail lives in the query type, so an unusable query cannot exist

Stores here do not validate: `ListAsync` on both management stores returns a bare `XPage`,
only mutations return `DomainResult`, and the availability range guard sits in
`AvailabilityService` rather than in `IBookingStore`.

The first draft of this design followed that literally and put a `BookingQueryService` in
front of the store. **The outward sweep caught it**: `bookings` says, in as many words,
*"No Core service SHALL depend on a management store: the read ports are the only pathway
anonymous delivery traffic reaches storage through."* That service was a Core service
depending on a management store — the first exception to a standing `SHALL`, arrived at by
accident rather than by decision.

The guard therefore moved into `BookingQuery` itself:

```
  BookingQuery.Create(from, to, settings, …)  ──> DomainResult<BookingQuery>
          │                                         (an invalid one is never produced)
          ▼
  IBookingManagementStore.ListAsync(BookingQuery) ──> BookingPage
          │                                            (nothing to validate)
          ▼
  SqlBookingManagementStore ──the join
```

This is **stronger than the service version**, not merely a way around the rule. A guard in
front of the store is something a caller can route around; a guard in the constructor
leaves no invalid value to pass. It also lets creation settle the defaults once — statuses,
an empty resource set, page bounds — so that every implementation of the port sees the same
values rather than defaulting for itself.

`BookingQuery` is a **class, not a record**, for one reason: `with` would clone around the
factory and produce exactly the state the factory exists to prevent.

*Rejected: putting the guard in the store* — it would be the only validating store, and the
next person would reasonably copy the wrong one.
*Rejected: leaving the guard to the eventual API controller* — the port is then unsafe for
every other caller, and a guardrail callers may skip is not a guardrail. Same reasoning that
put theme-registration ordering inside the package rather than in the site's `Program.cs`.
*Rejected: amending the `bookings` rule to permit a management-facing Core service* —
defensible, since the rule's stated reason is about anonymous delivery traffic and this
service was not on that path. But it would have widened a sibling capability's guarantee to
accommodate an implementation detail, when a better implementation was available.
*Rejected: moving the service into `UBookIt.Backoffice`* — keeps the rule intact, but leaves
a Core-only consumer holding an unguarded port.

### D3. A summary carries what a list row renders, including resource names

`BookingSummary` carries the booking id, its interval (start, end, and the `TimeZoneId`
the booking was made in), status, created timestamp, the booker's name and email, and its
resources as `(ResourceId, Name)` pairs.

Names, not just ids, because of the `ClaimRow` finding: the alternative is a lookup per
row. The join is `BookingRow → ClaimRow → ResourceRow`, and it is the reason this port is
worth having rather than being a thin wrapper anyone could write.

**It is a summary and it is allowed to stay one.** When the backoffice needs a detail
view, `IBookingStore.GetBookingAsync` already returns the full `Booking`. Fattening this
type to serve both is how a list query acquires columns nobody renders.

*On personal data:* the summary necessarily carries booker name and email, because that is
what a list row shows. Nothing logs it, and fixtures use invented people — the standing
rule, restated because this is the first type in Core built to carry contact details in
bulk.

### D4. The default status filter is "blocking", which is an existing domain concept

The query takes a set of statuses. Supplied, it is used as given — including asking for
`Cancelled` alone, which is how someone answers "what got cancelled this week".

Unsupplied, it defaults to the **blocking** statuses, `Requested` and `Confirmed`. That is
not a new list invented here: `Booking.IsBlocking` already means exactly "this claims time
and blocks others", and the default answer to "what is booked" should not silently include
things that are not.

Nothing is hidden irrecoverably — a cancelled booking is one filter away, and the spec
requires the default to be documented rather than discovered.

### D5. Ordering is a total order, so paging is stable

`ORDER BY StartUtc, Id` — both ascending, with the id as the tiebreak. Two bookings can
easily share a start time, and `skip`/`take` over a non-total order silently repeats or
drops rows between pages. A paging test written against distinct start times would pass
against a broken ordering, so the fixture must include a tie deliberately.

### D6. Window, status and resource are the filters; resource is a **set**

Status (D4), the window itself (D1), and the resources a booking claims.

The resource filter is a predicate on the `ClaimRow` join that is already happening for
the names, so it costs nothing to compute, and "what is booked into Room A this week" is
the second question anyone asks of a booking list. It was briefly cut on the grounds that
a filter shipped before a screen renders it is a guess — but that argument only holds
where the need is uncertain, and this one is not.

**It is a set of resource ids, not a single id, and the reason is compatibility rather
than ambition.** Public API is a compatibility promise once published: widening a
`Guid?` to a collection later is a *breaking* change, while starting with a collection
covers the single-resource case at no cost. The query already takes a set of statuses, so
a set here is the query's own shape rather than a new one.

Semantics, stated so they are not guessed at apply time: a booking matches when it claims
**any** of the named resources. That is what a multi-select in a picker means, and it is
the only reading under which naming one resource behaves identically to the single-id
version.

Empty or absent means no resource filter — not "match nothing".

**The join now carries a filter as well as a projection, which sharpens an existing
risk:** filtering on a joined child table can duplicate the parent row. A booking claiming
two resources must come back once, and a single-claim fixture cannot tell a correct query
from one that fans out. That is already guarded, and now has a second reason to be.

Not booker (a lookup, not a filter — see D1), not service (impossible — see D1), not
date-of-creation.

### D7. `booking-management` is about who is acting, not about reading versus writing

The capability is named for the **backoffice** view of bookings: an operator looking at
what a site has taken, rather than a visitor making one. That is a different actor with
different questions, different authorization and — eventually — different verbs.

This resolves what would otherwise be a naming wobble, since this change ships only a
read and "management" implies more. The name is right anyway, because what lands here
next is an operator *recording* a booking on someone's behalf — a phone booking — which
is genuinely a separate thing from a visitor placing one through the front end and does
not belong in `bookings` alongside the placement pipeline.

`bookings` keeps the domain: what a booking is, its status machine, the placement
validation pipeline, conflict and atomicity. `booking-management` is what an operator can
see and do.

## Risks / Trade-offs

- **A required window makes an unknown-date booking unfindable** → accepted in D1, and
  search is named as the follow-up rather than left to be discovered.
- **No service filter, and the reason is a schema gap** → stated in the proposal as a
  measured limitation with the migration it would need, so a UI change does not discover
  it halfway through.
- **The join could be the expensive part** → it is one join over rows the window has
  already narrowed; the alternative is N lookups. If a site's window returns thousands of
  bookings, paging bounds the join too, because it applies after ordering.
- **`Total` on a windowed, filtered query costs a second count** → accepted, because a
  pager without a total is a worse experience than one extra count, and both existing
  management stores already return `Total`.
- **The port is public surface a site could implement** → same as the existing management
  stores; the guarantees are stated in the spec so an alternative implementation has
  something to satisfy.
- **Default-excluding cancelled bookings could read as data loss** → D4, and the default
  is a spec requirement rather than an implementation detail, so it is documented where a
  reader looks.

## Open Questions

- Whether `BookingSummary` should carry the booking's **duration** as well as its
  interval. Trivial to derive, and a list may want to sort by it later — but sorting is
  not offered here, so deriving it at the call site is enough for now.
- Whether the eventual API should expose `skip`/`take` or cursor paging. Not this change's
  problem, but a cursor would want the same total order D5 establishes, so nothing here
  forecloses it.

*Settled by Chris, 2026-08-28:* windowed rather than unbounded; cancelled bookings visible
but hidden by default; window and status as the only v1 filters; and a new
`booking-management` capability rather than an annex of `bookings`, on the reasoning now
recorded as D7.

## A correction to the record, so a later change does not inherit it

The question that produced this change carried an assumption worth writing down as false:
that bookings are *"currently entirely separate from Services/Resources"*.

- **Bookings and resources are already joined.** `ResourceClaim` / `ClaimRow` binds a
  booking to every resource it claims, and `AvailabilityService` computes free time from
  exactly those claims through `IBookingStore.GetClaimsAsync`. Showing a visitor what is
  unavailable is not a future domain change — it is the mechanism the front end already
  runs on.
- **Bookings and services are not joined**, and that is the real gap (D1). A service
  chooses the resources and is then discarded.

The distinction matters for planning: the front-end "what's not available" work is already
served, while anything wanting to say *which service* a booking was for needs the additive
column first.
