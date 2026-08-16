## Why

⑨-1 restricted a service's roles to **distinct resource types**, and recorded that
restriction as deliberate rather than as a property of services: roles of one type
draw from overlapping pools, where assigning each role its first available
candidate can report a service unavailable when it was bookable. The failure code
`service-role-duplicate-type` says so in its own documentation, and names this
change as the one that lifts it.

The configurations it blocks are ordinary. Two therapists for a joint session. Two
rooms for a workshop split across them. Any two of a set of identical machines.
Today `Service.Create` rejects all of these, and `ServiceRole.Count` is validated
as exactly 1, so a role cannot ask for two of anything either.

Both restrictions have one cause. With distinct types, a resource has exactly one
type and therefore belongs to at most one role's pool, so the pools are provably
disjoint and choosing each role independently is **optimal**, not merely adequate.
Lifting either restriction destroys that, and three guarantees fall with it:
assignment stops being trivially optimal, availability stops composing as set
intersection, and a preferred resource stops identifying one role. Real assignment
has to exist before either restriction can go, which is why they lift together.

## What Changes

- **Core gains assignment over role slots.** A role of count *N* contributes *N*
  slots; a slot may be filled by any eligible resource; one resource fills at most
  one slot. A service is fulfillable at an instant exactly when every slot can be
  filled simultaneously by distinct resources — a bipartite matching that saturates
  the slots.

- **Availability becomes exact rather than compositional.** A start and length are
  offered only when a saturating assignment of **distinct** resources exists there.
  Today's fold — union within a role, intersection across roles — is correct only
  for disjoint pools: two roles drawing on one pool with a single free resource
  currently produce identical start maps whose intersection keeps every start, so
  the service advertises slots it can never honour. **For a service whose roles
  already name distinct types the answer SHALL be identical to today's**, which is
  the guarantee that makes this a generalisation rather than a rewrite.

- **Placement resolves an assignment instead of walking a cartesian product.** The
  current combination loop pairs every candidate of one role with every candidate
  of another, which once pools overlap generates a combination naming the same
  resource twice — and `Booking.Create` throws on duplicate claims, so that is an
  unhandled exception rather than a refusal. A preferred resource makes it the
  *first* attempt, because it is ordered to the head of every pool containing it.

- **Two roles of one resource type become valid**, provided they differ in their
  required capabilities. `service-role-duplicate-type` is **narrowed rather than
  retired**: it now rejects two roles identical in type *and* capabilities, which
  are two spellings of one requirement and are exactly what a count expresses. The
  message points at the count, so the correction is obvious. Previewing such a
  configuration already works — ⑧a's endpoint deliberately never rejected it — so
  only saving changes.

- **A role may require more than one resource.** `Count` becomes a bounded positive
  number rather than exactly 1, with its own stable failure code. It is bounded
  above because each unit is a slot to be filled, so an absurd count is an
  unbounded amount of work for a configuration that cannot succeed — the same
  reasoning that made `NormalizedKey.MaxLength` stop an over-long key becoming a
  500 at INSERT rather than a validation failure.

- **Roles gain a canonical ordering that survives duplicate types.** Roles are
  sorted by type key, and that comment states it is a total order *"which is unique
  across a service's roles by the rule just applied"* — the rule being lifted.
  `List.Sort` is unstable, so without a tiebreak two same-type roles could exchange
  places between saves, breaking round-trip equality and the delivery contract's
  promise of a deterministic role order.

- **A preferred resource means "this resource must appear somewhere in the
  booking"** rather than "this resource fills role *N*". A resource may now be
  eligible for several roles, so naming one no longer names a slot; the assignment
  chooses. This keeps the existing request contract unchanged.

- **The start-alignment diagnostic stops being silenced by a self-pairing.** ⑨-1a
  compares every candidate of one role against every candidate of the other; once
  pools overlap, a resource paired with *itself* has a zero offset, always "meets",
  and silences the report — via an assignment that can never happen, since a booking
  cannot claim one resource twice. It must skip equal resources, and must **not**
  skip same-type role pairs wholesale: two roles of one type requiring different
  capabilities can draw genuinely disjoint sets of resources, so a real misalignment
  between them remains possible.

- **The editor gains a count control per requirement row**, and stops being the
  only thing that cannot express a service the domain now permits.

- **The delivery API publishes a role's count.** `ServiceRoleReadModel` carries the
  resource type and required capabilities but no count, so a headless consumer
  cannot distinguish one therapist from two — and could not build a correct booking
  UI against it. Additive.

- **No persistence change, and no migration.** Verified rather than assumed: `Count`
  is already a column on `uBookItServiceRole` from ⑥ and round-trips through
  `ServiceRowMapper`; the table carries only a non-unique index on `ServiceId`, so
  same-type roles are already storable. Only the domain rule forbids them.

## Capabilities

### New Capabilities

None. Every operation extends behaviour an existing spec already owns.

### Modified Capabilities

Unlike ⑨-1a, this change genuinely **replaces** requirements rather than adding
beside them, so `CLAUDE.md`'s rule applies in full: a `## MODIFIED Requirements`
entry replaces its requirement wholesale, and any guarantee the new version forgets
to restate is deleted with nothing in the diff resembling a deletion. Each modified
requirement below must be diffed guarantee by guarantee, not prose by prose.

- `services`: a service's roles need no longer name distinct types; a role's count
  may exceed one; the canonical role ordering gains a tiebreak; the editor expresses
  a count.
- `service-booking`: availability and placement are defined by a saturating
  assignment of distinct resources rather than by union-then-intersection and a
  cartesian product; a preferred resource is a booking-level rather than role-level
  constraint; the start-alignment check ignores a resource paired with itself.
- `delivery-api`: a published service role carries its count.

## Non-goals

- **The pool-sufficiency diagnostic.** "This service needs two therapists with
  `cert-x` and only one exists" is a structural, configuration-time claim that
  follows from the same theory as the assignment — a saturating assignment exists
  only if every set of slots has at least as many distinct eligible resources
  between them. It belongs with ⑨-2a, as ⑨-1a followed ⑨-1, and is only meaningful
  once counts exist.

- **A richer `service-unavailable` message.** The assignment yields a witness — the
  slots that could not be filled and the resources they were competing for — which
  would say "two therapists are needed and only one is free then" instead of the
  current generic refusal. Same reasoning as above: it lands with ⑨-2a rather than
  enlarging the surface QA must review here.

- **Per-role preference.** Naming which role a preferred resource should fill is a
  richer request contract than anything consumes today; ⑩'s "pick who" UI is the
  first plausible consumer and can decide it then.

- **Fair or round-robin assignment.** Deterministic-by-id selection remains, as it
  has since ⑦-2. Spreading work evenly across a pool is a scheduling policy, and
  correctness holds either way.

- **Bounding resource granularity.** Still logged separately; unrelated to counts.

- **Whether direct resource booking survives to v1.** An open product question,
  recorded for ⑩.

## Impact

**Core** — the assignment itself, plus the composition of availability and the
placement path rebuilt on it. `ServiceRole` count validation, `Service.Create`'s
duplicate-type rule and role ordering, and `StartAlignment`'s candidate pairing.

**Backoffice** — a count control per requirement row and the count on the
management contract. The duplicate-type rule was deliberately never enforced in the
editor (⑨-1 design D1, so that relaxing it would be a server change in one place),
so the editor already submits and previews duplicate types unchanged.

**Delivery API** — one additive field on the published role.

**Persistence** — unchanged. No schema change, no migration.

**Tests** — ⑨-1's overlapping-pool fixtures exist today as *rejection* tests, so
several assertions invert rather than being written from scratch. The new ground is
assignment: pools where a greedy choice strands a role, availability that must not
offer a start two roles cannot both fill, placement that must never claim one
resource twice, and the guarantee that every distinct-type service answers exactly
as it does today.
