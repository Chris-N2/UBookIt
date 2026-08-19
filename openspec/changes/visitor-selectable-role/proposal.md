## Why

A visitor booking a service cannot say **who** they want. The service front end
(⑩) picks a date, a length and a start; the assignment then chooses the resources
and the visitor is told afterwards who they got. For the single most ordinary
booking there is — "I want my usual stylist" — the product has no answer.

The machinery is already built and idle. `resource-pin` made a pinned resource
fail *honestly*: when no saturating assignment can include it while one exists
without it, placement refuses with `pinned-resource-unavailable` rather than
booking somebody else. Nothing calls it from the front end, and ⑩ deliberately
shipped with no control, no hidden field and no code path that could set one
(⑩ design D7). This change is the screen that consumes it.

Now, because the read it needs turned out to already exist. `GetBookableStartsAsync`
builds the full per-start graph of slots against candidates — filtered by claims,
open hours, lead time, horizon and granularity — and asks
`SlotAssignment.TrySaturate` whether it saturates. Answering "when can this
*particular* resource be assigned" is that same computation routed to
`TrySaturateIncluding`. `resource-pin` deferred a who-then-when query on the
grounds that it was a new seam; ⑨-2 built the seam for other reasons, and the
deferral has expired.

## What Changes

- **A service role carries whether a visitor may choose which resource fills it**,
  defaulting to **not**. **At most one role per service** may be selectable, and
  `Service.Create` refuses a configuration naming two.

  The flag is not only "which role". A resource pin names the *booking*, not a
  role, so for a multi-role service the honest list of pinnable resources includes
  the rooms — a choice no visitor wants, offered beside the one they do. The flag
  says which pool is worth showing.

  Its second justification is the one that decides the default: **a picker is a
  published list of the site's staffing**, and whether to publish it is the
  business's call. Defaulting off is the same register as `Resource.DirectlyBookable`,
  for the same reason — until an editor says, the product must not guess.

- **Availability answers the question conditionally.** The Core bookable-starts
  query gains an optional pinned resource id; the starts it returns are those at
  which a saturating assignment **including that resource** exists. This is a new
  guarantee alongside the unpinned one, not a replacement for it: the unpinned
  query still names no resource, because it still makes no promise about which
  candidate a booker will get.

- **The flow asks who before when.** The who-select joins the existing step-1 GET
  form beside the date and the length, and the start list is filtered to that
  choice. No new round trip, and the choice is carried in the URL like the others,
  so every step stays linkable. `resource-pin` design D6 put time before who on
  cost grounds; the cost is gone, and "I want Jane, when can I have her?" is the
  question the feature exists to answer.

- **A refused pin says so, and offers the alternative.** "That person is not
  available at the time you chose" is a different fact from "there are no times",
  and the flow must not collapse one into the other — the failure `resource-pin`
  built exists precisely so a front end can say which.

- **A service role of count *N* with a selection means "this one, plus *N*−1
  chosen for you"**, and the control must say so. A picker that implies a choice
  and then books resources the visitor never chose is the quiet substitution the
  `??` fallback was removed for, wearing a friendlier face.

- **The delivery API publishes all of it**: the role flag on the service read
  model, the pinned availability query, and a pin naming a resource outside every
  pool rejected there as it already is at placement. A headless consumer must be
  able to build this flow without `UBookIt.Web`, so a picker the Razor front end
  can render and a SPA cannot would be a failure of the contract, not a scope cut.

- **An empty bookable-starts response says when it is permanent** — ⑨-1a's
  deferred reason code, folded in here because ⑩-1 opens the contract anyway. A
  service whose roles can never be filled together, whose start grids can never
  coincide, or for which no length exists that every role can provide is
  indistinguishable today from a fully booked week. Constrained as ⑩ design D9
  constrained its in-process twin: it may say the roles can never be filled
  together; it may never say a service *is* available.

  Serving that code requires the three structural questions to be askable from
  Core. Two already are; the third (`DurationOptions`) lives in
  `UBookIt.Web/Rendering`, which is why the delivery API cannot reach it. The
  triad lifts into Core and ⑩'s in-process check repoints at it, so both surfaces
  derive from one computation rather than two that are free to disagree.

- **`delivery-api`'s Purpose stops naming the default front end as a consumer of
  the delivery API.** It is not one — the shipped flows read Core in-process, and
  `default-frontend` forbids otherwise. The tension dates from ⑤ and was parked
  for whichever change next legitimately modified that spec. This is that change.

**BREAKING (source):** `ServiceRole` gains a member and `ServiceRole.Create` a
parameter; `IServiceBookingService.GetBookableStartsAsync` gains an optional
pinned resource id. Nothing is published, the repository is private, and there
are no consumers outside it.

**No breaking schema change.** One nullable-free `bit` column with a `false`
default on `uBookItServiceRole`, additively — the shape `AddDirectBookability`
established.

## Non-goals

- **A pin *set*.** Several selectable roles — "choose your stylist *and* your
  room" — needs matching with required vertices rather than
  `TrySaturateIncluding`'s try-each-slot loop, and makes failure attribution
  ambiguous when one of two choices is refused. `PinnedResourceId` stays a single
  `Guid?`. Going from one to many later is a widening, not a re-derivation.
- **Choosing all *N* resources for a role of count *N*.** That is the pin set
  wearing a different hat.
- **A soft preference.** "Book Jane if you can, anyone otherwise" was removed
  deliberately by `resource-pin`; if it is ever wanted it is a second,
  differently named field, never a boolean on this one.
- **Naming resources in a *deterministic* refusal.** A service that can never be
  fulfilled still tells the visitor nothing about the configuration. Only a pin
  the visitor themselves named may be named back to them.
- **Changing what an unpinned availability response contains.** No resource id
  appears on an unpinned start, then or now.
- **A pick-who control for direct resource booking.** A directly booked resource
  is already the resource the visitor chose.

## Capabilities

### New Capabilities

None. Every requirement lands in a capability that already exists.

### Modified Capabilities

- `services`: a role carries visitor-selectability; at most one role per service
  may carry it, with a stable failure code; the management DTO and the backoffice
  role editor express it.
- `service-booking`: a new pinned bookable-starts guarantee alongside the existing
  unpinned one; a pin outside every pool rejected on the availability path as it
  already is on the placement path; the deterministic-unfulfillability triad
  stated as a Core function so every surface derives from one computation.
- `delivery-api`: the role flag published on the service read model; the pinned
  availability query; the structural reason code on an empty response; the Purpose
  correction so the delivery API no longer claims the default front end as a
  consumer.
- `default-frontend`: the who-select in the service flow's first step, carried in
  the URL; a refused pin distinguished from no availability; the count-*N*
  labelling obligation; and the disclosure boundary — an opt-in picker discloses
  the pool by the site owner's choice, while a refusal still discloses no
  configuration detail.

## Impact

**Code**

- `UBookIt.Core`: `ServiceRole`, `Service.Create` validation,
  `IServiceBookingService.GetBookableStartsAsync` and its `FeasibleRuns` path, a
  new who-is-assignable projection, and the lifted structural-unfulfillability
  function.
- `UBookIt.Persistence`: one column on `uBookItServiceRole`, its migration, and
  the role mapper both ways.
- `UBookIt.Backoffice`: the service management DTO and the Lit role-row editor.
- `UBookIt.Web`: the delivery service read model, the bookable-starts endpoint,
  the service booking flow's first step and form builder, the surface controller's
  pin threading, and the refusal messages.
- `UBookIt.Tests`: unit and integration coverage for each of the above.

**Scope boundary.** The reason code is separable from everything else here: the
flag, the pin threading and the picker do not depend on it. If lifting the
structural triad into Core pulls in materially more of `BookingForm` than
`LengthGrid`, it leaves this change as a follow-up and the reason code defers once
more — a decision to be taken during implementation and recorded, not discovered
at review.

**Risk carried from ⑩.** No test in the C# suite renders Razor, so no test can
fail on a markup defect. Every view change here is verified in a browser before it
is called done, and tag helpers remain banned in `src/UBookIt.Web/Views`.
