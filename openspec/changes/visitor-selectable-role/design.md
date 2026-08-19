## Context

`resource-pin` (⑩'s predecessor) made a pinned resource fail honestly and left the
consumer unbuilt. ⑩ then shipped the service flow with the pin deliberately absent
— design D7: no control, no hidden field, no code path, asserted by a source scan
over `src/UBookIt.Web/Rendering` and `Views`. This change builds the consumer.

Three facts about the code as it stands decide most of what follows.

**The pin is already slot-agnostic.** `SlotAssignment.TrySaturateIncluding`
(`src/UBookIt.Core/Services/SlotAssignment.cs:214`) tries each slot the required
resource is eligible for, pinning it there and removing it from every other, so it
returns a genuine assignment containing the resource rather than one patched
afterwards. A resource may be eligible for several slots, so the pin names the
booking and the assignment chooses where it goes.

**The graph the picker needs is already built.** `GetBookableStartsAsync` resolves
the pools, takes one claims read over all of them, projects each candidate's
bookable starts through `IAvailabilityQueryService`, and hands `FeasibleRuns` a
per-slot list of candidate ids at each `(start, length)` — filtered by claims, open
hours, lead time, horizon *and* granularity. It then asks
`SlotAssignment.TrySaturate` whether that saturates
(`src/UBookIt.Core/Services/ServiceBookingService.cs:448`) and discards the
identities. Every projection this change needs is that computation asked a slightly
different question.

**A "free" list would be a lie.** `FreeShortlistsAsync` — the other place candidate
identities survive — drops only resources with *claims* at the interval. It says
nothing about open hours, lead time or horizon; those are evaluated per attempt by
the placement pipeline. A picker built from it would offer people the pin then
refuses, which is the ⑩ defect arriving from a new direction.

The constraint that shapes the front end: the service flow is one GET step (date +
length) rendering start radios *and* the booker fields, submitted once by POST. A
control that had to sit *after* the start would need a third page in a no-JS flow.

## Goals / Non-Goals

**Goals:**

- A visitor can choose which resource fills one role of a service, and the times
  offered are the times that choice can actually be honoured.
- A refused choice is distinguishable from no availability, and says which.
- Whether a picker appears at all is the site owner's decision, defaulting to no.
- A headless consumer can build the same flow from the delivery API alone.
- An empty availability response can say when it is permanent.
- Every surface answering "can this service be fulfilled at all" derives from one
  computation.

**Non-Goals:**

- Pinning more than one resource per booking (see D1).
- A soft preference, or any fallback that substitutes a different resource.
- Secrecy about which resources can fill a role — see D8; that is not achievable
  and is not what the flag is for.
- Reordering candidate pools. Pool order is `resource-pin`'s and ⑨-2's determinism
  guarantee; only *display* order is this change's business (D10).

## Decisions

### D1 — The flag is on the role, and at most one role per service may carry it

`ServiceRole` gains a visitor-selectability flag. `Service.Create` refuses a
configuration in which two roles carry it, with a stable failure code.

The flag has to be per role rather than per service because a pin names the
*booking*: a resource eligible for several slots may be pinned into any of them,
so "this service offers a choice" does not say *which* pool to show. For a massage
needing a room and a therapist, the honest list of pinnable resources is every
room and every therapist — a choice no visitor wants, offered beside the one they
do.

The at-most-one restriction is what keeps `PinnedResourceId` a single `Guid?`.

**Alternatives considered.**

*A pin set (`PinnedResourceIds`), several selectable roles.* Rejected for now, and
the reason is not only contract surface. `TrySaturateIncluding`'s try-each-slot
loop is correct for exactly one required vertex; forcing a *set* into the matching
is matching with required vertices — flow with lower bounds, or an injective-map
search over slots — a genuinely different algorithm. It also makes failure
attribution ambiguous: `resource-pin` D4a blames the pin only when an assignment
existed without it, which for a set becomes one probe per member plus a message
naming which choice failed. One to many is a *widening* later, not a
re-derivation: the flag stays where it is and the request field changes type.

*No flag; offer every resource in every pool.* Rejected — it is the "choose your
room beside your therapist" screen, and it publishes staffing nobody asked to
publish.

*A service-level flag naming the role by type key.* Rejected: a service may have
two roles of one type distinguished only by capabilities (⑨-2), so a type key does
not identify a role.

### D2 — Who is chosen before when

The who-select joins the existing step-1 GET form beside the date and the length,
and the start list is filtered to the choice. `resource-pin` design D6 put time
before who; it is reopened here because its stated reason was **cost** — a
who-then-when query was described as a new seam — and ⑨-2 built that seam for
other reasons. Filtering starts by a pin is `TrySaturate` → `TrySaturateIncluding`
at one call site in a computation that already runs.

With the cost gone the question is UX, and who-before-when wins twice: it is the
question the feature exists to answer ("I want Jane, when can I have her?"), and
it reuses a round trip that already exists. When-before-who would need a third
page in a no-JS flow, between the start radios and the booker fields.

The choice is carried in the URL as its own query parameter beside `ubDate` and
`ubMins`, so every step of the flow stays linkable, unchanged from ⑩.

**Alternative considered.** *Rendering a who-list per start on the existing page*
— no round trip, no ordering commitment. Rejected: a day with twenty starts and
six candidates is a hundred and twenty controls, and the resulting markup is a
WCAG problem rather than a convenience.

### D3 — The pinned query is a new guarantee beside the unpinned one, never a replacement

`ServiceBookableStart` carries no resource id, deliberately: ⑦-2 design D11 says
naming one would imply a guarantee placement does not make. That stays exactly
true for an unpinned query and this change does not touch it.

A pinned query is a different question — *given* this resource, when can the
service be booked — and its answer is conditional on the same pin being supplied
at placement. It is therefore stated as a new requirement rather than by rewriting
D11's. Replacing a requirement wholesale for an adjacent guarantee is how
guarantees get dropped silently, which this project has now been bitten by often
enough to have a rule about it.

Concretely: the pinned response still names no resource *in its payload*. The
resource is in the request.

### D4 — The single-slot fast path must filter, not bypass

`FeasibleRuns` short-circuits when there is exactly one slot
(`ServiceBookingService.cs:406`): it returns that role's plain union of runs
without asking the assignment at all, because for one slot a saturating assignment
exists exactly when some candidate admits the length — and because the spec
guarantees equality with union availability, which rebuilding from a length set
would not preserve.

Under a pin that path is **wrong**, and wrong in the worst possible way: a
single-role service of count 1 is the commonest configuration there is, so an
implementation that threads the pin only through the general path ships a picker
that is silently ignored on most sites while every multi-role test passes. The
fast path must filter its runs to the pinned candidate before collapsing them.

This is a design decision and not merely an implementation note because it
constrains testing: the covering test has to be a **single-role count-1** service,
and it has to fail if the pin is dropped.

### D5 — A role of count *N* with a selection means "this one, plus *N*−1 chosen for you"

`TrySaturateIncluding` pins the chosen resource into one slot; the assignment
fills the rest of that role's slots from the same pool. The behaviour is right and
needs no code.

What it requires is that the control **say so**. A picker labelled as though the
visitor is choosing, which then books resources they never chose, is the quiet
substitution the `??` fallback was removed for wearing a friendlier face. The
labelling obligation is a spec requirement, not a UI nicety.

**Alternative considered.** *Suppress the picker when count > 1.* Rejected: "book
one of our two trainers, and I want it to be Sam" is a perfectly ordinary request,
and refusing to answer it is worse than answering it plainly.

### D6 — The structural-unfulfillability triad lifts into Core

⑩ answers "can this service ever be fulfilled" with three questions:
`PoolSufficiency.FindShortfall`, `StartAlignment.FindMisalignment`, and whether
any length exists that every role can provide. The first two are Core. The third
is `ServiceBookingFormBuilder.DurationOptions` in `UBookIt.Web/Rendering`, over
`BookingForm.LengthGrid` — so `ServiceUnavailableModel.IsUnavailable`, a domain
question, lives in the Web project and the delivery API cannot reach it.

Publishing ⑨-1a's reason code from the delivery API therefore means either a
second implementation of the rule in the mapper — the fault ⑧a D1 and ⑨-1's
classifier seam both exist to prevent — or lifting the triad into Core and
repointing ⑩'s check at it. It lifts.

⑩'s existing tests are the regression net for that move and must stay green
**unmodified**; a test edited to accommodate the lift is a test that has stopped
being evidence.

**Scope boundary, agreed up front.** If the lift pulls in materially more of
`BookingForm` than `LengthGrid`, the triad and the reason code leave this change
as a follow-up. The flag, the pin threading and the picker do not depend on either.
This is a decision to be taken during implementation and recorded there — not
discovered at review.

### D7 — A pin outside every pool is rejected on the availability path too

`PlaceAsync` already refuses a pin naming a resource in no pool with
`resource-not-eligible` — rejected rather than ignored, because quietly booking a
different resource discards the caller's choice invisibly (⑦-2 design D9). The
availability path has no such check today because nothing could pin there.

It gains one, for the same reason: a query silently answering "here is when the
service is available" while ignoring the resource the caller named is the same
fault one step earlier, and it is worse there, because the caller would then
submit a start it had been told was good.

### D8 — The flag governs what is offered, not what placement accepts, and it is not secrecy

Placement is **unchanged**. A caller pinning a resource on a role no editor marked
selectable still gets the booking, exactly as it would today.

Two reasons. First, the pin has always been accepted and nothing about this change
makes it wrong: the booking is perfectly deliverable. Second, and decisively, the
flag could not create secrecy even if it tried. `service-booking`'s standing
requirement "Eligibility remains derivable from public reads" is satisfied by the
delivery API publishing role required-capabilities and resource capabilities, so
any caller can already compute every role's pool without asking. A gate on
placement would buy nothing and would invite the flag to be read as an access
control it is not.

So the flag decides two things and only two: whether the default front end renders
a picker, and whether the delivery API advertises the role as one a visitor may
choose from. This must be stated explicitly in the spec, or a later change will
mistake it for authorization.

**Alternative considered.** *Enforce the flag in Core placement, as
`DirectlyBookable` is enforced.* Rejected on the disanalogy: `DirectlyBookable`
prevents manufacturing a booking that cannot be delivered — a therapist held for
an hour with no room. A pin on an unflagged role manufactures nothing wrong.

### D9 — Default false, one additive column

The flag defaults to **not selectable**, and the column is a `bit` with a `false`
default on `uBookItServiceRole` — the shape `AddDirectBookability` established for
`Resource.DirectlyBookable`.

Defaulting off is not timidity about a new feature. A picker publishes the site's
staffing as a list, and whether to do that is knowledge only the business has: a
salon wants it, a clinic that rotates whoever is free may actively not. Until an
editor says, the product must not guess — the same argument, in the same words,
that `direct-booking-opt-in` made about lettable rooms.

No existing service changes behaviour: every role loads as not selectable, and the
flow renders exactly what it renders today.

### D10 — The list is the role's pool, ordered for reading, not filtered by date

The who-select offers the selectable role's **resolved candidate pool** — the
resources that survive type, required capabilities and the service's duration
range. It is not filtered by the currently chosen date.

Filtering the people by date would make the control's contents change as the
visitor changes the date, so a name could vanish from under the cursor; and it
would answer with the *control* a question the **start list** already answers
directly and better. Jane appearing in the list and offering no times on Tuesday
is not a defect: the flow already says "no times available" and already explains
an unavailable length.

Display order is by display name, with resource id as the tiebreak so it is total
and stable. **This is presentation only.** Pool order stays ascending by resource
id — `resource-pin`, ⑦-2 and ⑨-2 all draw determinism guarantees from it, and
⑨-1a's misalignment witness is "the first pairing compared", which pool order
decides.

### D11 — A stale choice degrades to "any", and says so

A URL carrying a choice is linkable and therefore bookmarkable, so it can name a
resource that has since been deleted, lost a capability, or had its role's flag
cleared. At GET time the flow drops the selection back to "any" **and tells the
visitor it did**.

Falling back silently would be quiet substitution; refusing to render the form at
all would punish a visitor for a change they had no part in. Nothing has been
committed at GET time, so saying "the person you chose is no longer offered for
this service — choose again" is both honest and unblocking.

At POST time the same condition is a different matter and is already handled:
placement answers `resource-not-eligible`, which is a refusal rather than a
fallback.

### D12 — A refused pin may name the resource the visitor named

`default-frontend`'s standing requirement is that a visitor-facing refusal carries
no *configuration*: no role, no resource type, no required capability, no count, no
pool size. A pin refusal names a resource — "that person is not available at the
time you chose" — and that is compatible with the requirement rather than an
exception to it: the visitor supplied the name, and none of the five prohibited
facts is disclosed.

The requirement is modified to say this explicitly rather than left to be inferred,
because the inference is not obvious and the next person to read it will be
deciding whether some other message may name something.

Its guarantees are diffed rather than reworded, per the project's standing rule:
every SHALL and every scenario in the current version is carried forward, and the
deterministic refusal's silence about configuration is unchanged and re-stated.

## Risks / Trade-offs

**The pin is silently ignored on single-role services** (D4) → The covering test is
a single-role count-1 service, written so it fails if the pin is dropped, and
mutation-checked by removing the pin from the fast path and confirming red. The
general-path tests cannot catch this, which is the whole risk.

**The triad lift changes ⑩'s code while ⑩'s guarantees must not move** (D6) → ⑩'s
tests are the net and stay unmodified; if a test needs editing, that is the signal
to take the escape hatch rather than to edit the test.

**Two computations of "who can be assigned here" could drift** — the picker's list
and placement's assignment → Both route through `TrySaturateIncluding` over the
same pools. Nothing recomputes eligibility, exactly as ⑧a D1 and ⑨-2a's shared
witness require.

**A markup defect ships green** — no C# test renders Razor → Every view change is
verified in a browser before the change is called done; tag helpers stay banned in
`src/UBookIt.Web/Views`, since without a `_ViewImports.cshtml` they degrade to
visible text rather than to a build error.

**The picker is an accessibility surface** — a select whose choice changes the
meaning of the list below it → It is a labelled `<select>` in the existing GET
form, submitted explicitly, with no JavaScript-dependent behaviour: the same shape
as ⑦-1's length picker, which already meets the bar. The relationship between the
choice and the times is stated in text, not implied by layout.

**Publishing a picker publishes staffing** → Opt-in, defaulting off (D9), and it
discloses nothing a caller could not already compute (D8).

## Migration Plan

One additive migration: a `bit` column with default `false` on `uBookItServiceRole`.
No data is invalidated and no existing service changes behaviour. Rollback is the
generated `Down`, which drops the column; nothing else in the change persists
state.
