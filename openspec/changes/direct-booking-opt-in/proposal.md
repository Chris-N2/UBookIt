## Why

A resource can be booked on its own, unconditionally, and nothing in the product
can say otherwise. For a business whose service needs two resources at once — a
massage needing a therapist **and** a room — that manufactures bookings it cannot
deliver: a customer holds a therapist for an hour with no room to use them in.
Availability is correct, placement is correct, the booking is confirmed, and the
appointment cannot happen.

Whether a resource means anything on its own is knowledge only the business has.
Six therapists and four rooms: the rooms are lettable, the therapists are not, and
no rule the package could infer distinguishes them. So the product has to let the
editor say, and until they say, it must not guess.

Now, because the window is open. Nothing is published — the repository is private
and this package has no consumers — so making the safe answer the default costs
nothing today and is a breaking change to every installation later.

## What Changes

- **A resource carries whether it may be booked on its own**, defaulting to **not**.
  **BREAKING (behaviour):** every resource that exists stops being directly
  bookable until an editor says otherwise. No consumer exists to break; this is
  the last moment that is true.

  **BREAKING (source):** `Resource.Create` gains the parameter *before* its
  trailing optional `id`, so any positional caller passing an id shifts. Two
  in-repo callers did, and the compiler caught both only because `bool` and
  `Guid?` differ — luck, not design; both now pass arguments by name. Appending
  after `id` would have been source-compatible and was rejected: `id` last is the
  convention across every factory in Core, and nothing is published, so the
  compatibility being spent is worth less than the consistency being kept.

- **The single-resource placement path is refused in Core** for a resource that
  carries no such permission. The gate sits on `IBookingService.PlaceAsync(BookingRequest)`
  — the one function both direct callers reach and the one service placement does
  not, since it composes a `MultiClaimBookingRequest` itself. A resource may
  therefore always be booked **as part of a service**, which is the entire point:
  the therapist is bookable, just not alone.

- **A distinct stable failure code**, never folded into unavailability. "This
  resource is not offered on its own" and "this resource has no free time" are
  different facts with different fixes, and conflating them is how a correctly
  configured resource comes to look broken.

- **The permission is published on the resource read model**, so a consumer can
  tell before it asks. That keeps direct bookability *derivable from public reads*,
  the same property the capability publication guarantees for eligibility, and it
  means a conforming client never provokes the refusal.

- **Availability reads are unchanged.** A resource that cannot be booked alone
  still publishes its free time, slots and bookable starts — the service-oriented
  front end needs a therapist's availability in order to offer one, and the
  eligibility oracle already depends on those reads.

- **The no-JS booking flow says which fact it means.** Pointed at a resource that
  is not offered on its own, it states that, rather than reporting no availability.

- **The backoffice edits it**, and the resources list shows it, so the answer to
  "why can nothing book this room?" is on screen rather than inferred.

## Capabilities

### New Capabilities

None. Every operation extends behaviour an existing spec already owns.

### Modified Capabilities

- `resources`: a resource carries whether it may be booked on its own, defaulting
  to not.
- `bookings`: single-resource placement is refused unless the resource carries that
  permission; placement as part of a service is unaffected.
- `delivery-api`: the resource read model publishes it; direct placement maps the
  new code; availability reads are explicitly unchanged.
- `resource-management`: the management API carries it on create and update, and
  the backoffice resource editor and list surface it.
- `default-frontend`: the no-JS flow distinguishes "not offered on its own" from
  "no times available".

## Non-goals

- **Removing direct booking.** Considered and rejected at explore: the two paths do
  not diverge in capability over time, because every multi-role concept — matching,
  start-grid alignment, pool sufficiency — is meaningless for a single resource.
  Removing the path would cost the delivery endpoint, the whole no-JS flow's
  retargeting, and the project's first REMOVED spec operations, to buy a uniformity
  worth less than that.

- **Auto-creating a service per resource.** The reason for it was to replace direct
  booking; direct booking stays, so it has nothing to do. It also cannot mean what
  it appears to: a role resolves by type and capabilities, so a service generated
  for "Room 3" would book *any* room.

- **Roles that name a specific resource.** A fourth eligibility dimension, after
  three consecutive changes built on there being three. The cases that seem to need
  it are already expressible: a room let out as a yoga studio carries a
  `yoga-studio` capability and the service requires it. Revisit only against a real
  configuration that capabilities cannot express.

- **The preferred-resource pin.** `preferredResourceId` silently falls through to a
  different resource when the one asked for cannot take the booking — deliberate and
  specified, and wrong for a front end that lets a booker choose a person. It is a
  separate idea about a different actor's control, and it follows in its own change
  before the service front end.

- **Bulk or per-type setting of the permission.** Ticking six therapists is
  tolerable; a bulk affordance is a later convenience, not part of the rule.

## Impact

**Core** — `Resource` gains the permission; `BookingService`'s single-resource
overload gains one guard before it delegates. No change to what any other path
decides.

**Persistence** — one additive, non-nullable column with a default. Existing rows
take the default, which is the behaviour change stated above.

**Delivery API** — the resource read model gains a member; direct placement gains a
failure code and its 400 mapping. Availability endpoints unchanged.

**Management API and backoffice** — the resource model carries it on read and write;
the editor gains a control and the list a column.

**Default front end** — the ViewComponent distinguishes the two cases; the existing
"unavailable" view gains a second reason.

**Tests** — the new ground is the **seam**: that the guard catches the direct path
and cannot catch service placement. A test asserting only "an unflagged resource
cannot be booked" would pass an implementation that also broke every service, so
the covering test has to book the same resource both ways.
