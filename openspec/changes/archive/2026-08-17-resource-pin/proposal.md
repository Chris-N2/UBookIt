## Why

A booker who names a resource can be given a different one, silently. Placement
seeks an assignment including the named resource and, when none exists, falls back
to any assignment at all — so asking for Mary and being booked with Bob is a
success response that says nothing about the substitution.

That was a defensible reading while the field was a hint nobody surfaced. The
service-oriented front end makes it a wrong answer: its flow is *pick a time, then
pick who*, so by the time a booker names someone they have chosen that person from
a list, deliberately. Substituting is not a graceful degradation of that choice —
it is a different booking, confirmed as though it were the one requested.

It is also reachable through no fault of the caller. A booker picks a start at
which Mary was free, and Mary is taken between the query and the placement. Today
that race resolves as "here is Bob", with nothing in the response to say so.

## What Changes

- **The named resource becomes a pin, not a preference.** When no saturating
  assignment includes it, placement SHALL fail rather than substitute. The
  mechanism already exists — `SlotAssignment.TrySaturateIncluding` finds an
  assignment containing a given resource — so this removes a fallback rather than
  adding a search.

- **The field is renamed to say what it now means.** `preferredResourceId` becomes
  `pinnedResourceId` on the delivery request and in Core.
  **BREAKING (unpublished contract):** a field whose name says "preferred" and
  whose behaviour is "required" is the kind of drift that outlives the person who
  introduced it. Nothing is published, so the rename is free today and is not
  free later.

- **A distinct stable code for a pin that cannot be honoured**, separate from both
  `resource-not-eligible` (the resource is in no pool — the caller's mistake) and
  `conflict` (nothing could be booked). The front end's useful answer is "Mary is
  not free then, but these people are", and it cannot say that from a code meaning
  "something clashed".

- **Retrying is meaningful, and the code says so.** A pinned resource that is busy
  may be free later, unlike `service-unavailable`, which exists to tell a caller
  not to bother. The pin's failure joins the transient family.

- **Availability is unchanged.** Bookable-start queries answer for the service, not
  for a nominated person, because the flow chooses the time first. A "starts at
  which Mary can be assigned" query is a real thing to want and is deliberately not
  built here — see Non-goals.

## Capabilities

### New Capabilities

None. Both operations belong to specs that already own them.

### Modified Capabilities

- `service-booking`: the preferred-resource hint becomes a pin; the fall-through
  guarantee is replaced by a failure, with its own stable code.
- `delivery-api`: the service placement request carries `pinnedResourceId`, and the
  new code maps to 400 through the existing rule.

## Non-goals

- **Availability filtered by a pinned resource.** Chris settled the front-end flow
  as *time first, then who* (2026-08-17), which removes the need: a booker chooses
  from people free at a time they already picked. "Bookable starts at which this
  resource can fill a slot" remains a coherent query and would be additive if the
  flow ever inverts. Building it now would be contract surface with no consumer.

- **Keeping a soft preference alongside the pin.** A second field (`preferred` plus
  `required`) would preserve "book Mary if you can, anyone otherwise". No screen in
  the planned front end expresses that, and a pair of near-identical fields whose
  difference is one word is a reliable source of the wrong one being used. Add it
  when something needs it.

- **Per-role pinning.** The pin names the booking — "this resource must appear
  somewhere in it" — exactly as the preference did. Naming a resource *and* the
  role it must fill needs a request shape no consumer has, and the assignment
  already places a pinned resource wherever it fits.

- **Changing what an ineligible resource does.** A pinned id in no candidate pool
  is still `resource-not-eligible`. It is a different fault — the caller named
  something that could never work, rather than something that could not work now —
  and the distinction is already correct.

## Impact

**Core** — `ServiceBookingRequest.PreferredResourceId` is renamed and its
fall-through in `Resolve` removed. The all-fail classification must not absorb the
new failure: a pin that could not be honoured is its own answer, not evidence about
the pool.

**Delivery API** — one renamed request member and one new failure code, mapping to
400 through the existing catch-all.

**Persistence** — unchanged. A pin is a property of a request, not of anything
stored.

**Default front end** — unchanged. It books a single resource directly and has no
service placement path.

**Spec risk, and it is the main risk of this change.** The fall-through sentence
lives inside `Booking a service resolves an assignment of distinct resources` — 150
lines, 17 scenarios, 26 SHALL clauses, covering the locking order, one-attempt-at-a-
time, claims exclusion and the whole assignment contract. Modifying it replaces all
of that wholesale, and almost none of it has anything to do with pinning. An ADDED
requirement is not an option: it would contradict a sentence in an unmodified
requirement and leave two rules disagreeing in one spec. The guarantee diff is
therefore load-bearing here in a way it usually is not, and must be read by hand as
well as run — the tool compares scenario titles and is blind to a changed WHEN.

**Tests** — the new ground is the *negative*: that a pin which cannot be honoured
fails, and fails as itself. A suite that only asserts "the pinned resource is used
when free" passes the implementation this change is removing.
