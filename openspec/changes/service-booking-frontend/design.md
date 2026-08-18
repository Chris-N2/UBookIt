## Context

The shipped front end is one ViewComponent, three views, a surface controller and a
pure form builder:

```
  BookingViewComponent(resourceId)      reads Core in-process, reads ?date=&duration=
    ├── Default.cshtml        167 lines  date+length form (GET) · times · details (POST)
    ├── Confirmation.cshtml              PRG target, read from TempData
    └── Unavailable.cshtml               "not offered on its own" / broken site zone
  BookingSurfaceController              POST, anti-forgery via BeginUmbracoForm, 303
  BookingFormBuilder                    pure; testable with no host
```

Everything a service flow needs already exists behind it. `IServiceBookingService`
answers bookable starts per (start, length) and places a booking claiming several
resources; `Service`, `Resource` and `Resource.DirectlyBookable` are all published
reads. No endpoint, no migration, no domain type.

Three prior decisions constrain the shape:

- **resource-pin D6 — time before who.** Settled 2026-08-17 and load-bearing: it is
  why "starts at which this resource can be assigned" was not built. A who-first flow
  would reopen it. Chris's answers on 2026-08-18 ("I don't care who I get" as the
  default, "in general a user won't really care") confirm who is a refinement, not an
  axis.
- **direct-booking-opt-in — the direct path survives, gated per resource.** So the
  front end has two kinds of bookable thing, permanently, and `DirectlyBookable`
  decides which resources are among them.
- **⑤'s withholding-resource requirement — a thing that yields nothing must say why.**
  The precedent this change extends to services.

## Goals / Non-Goals

**Goals:**

- A site owner can put service booking on a page without writing a client.
- A visitor cannot tell which bookable things are services.
- A refusal tells the visitor something true and actionable, or true and final, and
  never tells them the site's staffing.
- One accessibility bar, not two.

**Non-Goals:** as the proposal states — no pin control (⑩-1), no JavaScript, no
restyling mechanism, no new query, no change to the resource flow.

## Decisions

### D1 — Two flows, with the seam below the view

Chris's call, and the reason to hold it: the flows differ by *what is being booked*
and eventually by the who control, and are otherwise the same page. A single
component parameterised over both would have to be written in the language of
neither.

But copying the markup is how the accessibility guarantee dies. The clauses most
likely to be got subtly wrong — `aria-describedby` wiring, the error summary's links
to offending fields, `aria-invalid`, the fieldset/legend pairing — are exactly the
clauses that are identical between the flows. Two copies drift, and the flow written
second gets the attention.

So the seam sits below the view rather than at it:

```
  SHARED, pure (the BookingFormBuilder pattern, extended)
    date bounds · permitted lengths · times projection · error model · PRG plumbing

  SHARED partials (the markup that must not drift)
    _DateAndLength · _YourDetails · _ErrorSummary · _Times

  SEPARATE per flow
    what am I booking (heading, hidden ids) · the outcome wording · the confirmation
```

*Alternative considered — one component, two views.* Rejected: the branch would live
in the component and every future divergence (⑩-1's who control first) widens it.

*Alternative considered — two flows, nothing shared.* Rejected above: it is the
accessibility regression waiting to happen, and the spec now states the bar once
precisely so there is one place to satisfy.

### D2 — A thin dispatcher gives "enter anywhere" without a router

```
  Component.InvokeAsync("BookingFlow")                    → catalogue, then either flow
  Component.InvokeAsync("BookingFlow", new { serviceId })  → straight into the service flow
  Component.InvokeAsync("BookingFlow", new { resourceId }) → straight into the resource flow
```

The catalogue links to the host page with a query parameter, so the dispatcher reads
the same URL every other step reads and the site author places exactly one component.
An explicitly supplied id wins over the query, which is what makes "a site with one
service" work: the author names it, and no catalogue exists to be traversed.

The existing `Booking` component keeps working unchanged for anyone already using it.
The dispatcher is a few lines and holds no state.

### D3 — State in the query string, contact details never

Every step is a GET round trip, so the accumulated choices have to live somewhere the
browser understands. The query string makes each step linkable, bookmarkable and
back-button-correct; a server-side wizard state does not, and produces the classic
defect where going back shows a step that disagrees with the state behind it.

Nothing at those steps is sensitive: what is being booked, a date, a length. Name and
email are entered at the last step and leave by POST, governed by the existing
anti-forgery and PRG requirements. The spec states this as a prohibition rather than
a convention, because it is the kind of thing a later "make the confirmation
linkable" change would breach without noticing.

### D4 — Refusals map to exactly two visitor-facing shapes

**REVISED at apply, after QA rejected the original. The premise below was false and
the correction matters, so both are kept.**

*As proposed:* "The domain distinguishes `conflict` (transient) from
`service-unavailable` (deterministic)", mapping `service-unavailable` → "This service
is not currently available for booking", no retry.

*Why that was wrong:* **`service-unavailable` is not a deterministic code.** Core says
so in the method that raises it — the placement-time refusal is "about *that instant*
and nothing more… this may not claim a configuration can never be fulfilled — that is
the configuration-time check's claim to make, over a different graph, and it is what
distinguishes the two" (`ServiceBookingService.Shortfall`). A structurally impossible
service and one whose resources merely happen to be busy fail *identically* there.

QA demonstrated the consequence live rather than arguing it: a perfectly bookable
service, submitted with a start outside its opening hours, rendered "This service is
not currently available for booking" **above its own nine bookable start times**. The
change written to stop a refusal lying to a visitor had it lying from the other
direction.

*The rule that replaces it —* **the distinction is which question was asked, not which
code came back:**

```
  ANY placement failure   → an answer about one instant → retry invited
    conflict              → "That time is no longer available. Please choose another."
    service-unavailable   → "That time is not available for this service. Please choose another."
    duration-*            → the existing length messages, unchanged

  The configuration-time check (D9), asked over the resolved pools before any form
  is offered → "This service is not currently available for booking."  no retry
```

So the permanent claim is made in exactly one place, by the only question that can
support it, and *no* placement failure can produce it. The mapping is still from the
**stable code** and never from message text — that rule was never the problem, and it
is what keeps the shortfall diagnostic off the page.

A consequence worth stating, because it is what makes the shapes safe: the
deterministic page is reached *before* a form exists, so a failed submission against a
genuinely unfulfillable service redraws to that page rather than to a form carrying an
inviting message. A test names this rather than leaving it to be inferred.

Deliberately *not* rendered: ⑨-2a's shortfall diagnostic. It names roles, counts and
capabilities for the person who can fix the configuration. A visitor cannot fix it and
is not owed the staffing figures; "two of the three therapists lack the required
capability" tells a competitor how many therapists there are. The spec forbids it at
the point of rendering rather than by suppressing the diagnostic, so the backoffice
report stays exactly as it is.

### D5 — A fixed-duration service offers no length choice

`ServiceDuration` is `Fixed` or `Variable`. A fixed service has one permitted length,
so a control offering it is a choice of one — noise, and a WCAG-adjacent nuisance
since it is a labelled control that does nothing.

So: `Variable` renders the length control, bounded by the service's own min/max
intersected with what its resources permit; `Fixed` renders the length as text and
submits it as a hidden value. **The submitted length is still required and still
validated server-side** — the delivery contract requires it for a fixed service
precisely so a permitted length is never silently substituted, and the same reasoning
applies in-process. The control is an affordance, not a trust boundary; removing the
control does not remove the field.

### D6 — The catalogue composes existing reads and owns no truth

"What can be booked" is: every service, plus every resource with
`DirectlyBookable == true`. Both are already published on the delivery API, so a
headless consumer builds the same list from `GET /services` and `GET /resources` —
which is why this change adds no endpoint. The in-process catalogue asks the same
question of the same stores.

There is deliberately **no** stored notion of "the bookable catalogue", no ordering
field and no visibility flag. Adding one would create a second answer to a question
already answered, and the first thing to go stale.

### D7 — The pin is absent, not defaulted

This flow never sends `pinnedResourceId`. That is not "pinning is off by default" —
there is no control, and no code path that could set it. ⑩-1 adds the control, and
its spec surface is a role-level flag deciding which role a visitor may choose from.

Recorded because the alternative is tempting and wrong: a hidden field defaulting to
"any" invites someone to make it settable later without thinking about which role's
resources belong in the list, which is the whole reason ⑩-1 exists.

### D8 — `services` spec line 238 is about behaviour, and survives unmodified

Decided at apply, as task 7.5 required, and deliberately not resolved the
convenient way — so here is the argument rather than the conclusion.

The requirement reads:

> **Defining services does not affect direct-resource booking.** Introducing
> services SHALL NOT change the existing single-resource booking path in any way.
> A site that defines no services SHALL behave exactly as before this change;
> availability, placement, and the default front-end for direct-resource booking
> SHALL be unaffected.
>
> *Scenario: No services defined* — WHEN no service has been created THEN
> direct-resource availability queries and booking placement behave exactly as
> before this change.

Three things fix its meaning as behavioural:

1. **Its subject is "introducing services"** — the presence of service
   *definitions* in a site, not the presence of service *code* in the package. Its
   title says so, and its one scenario quantifies over sites with no services
   rather than over source files.
2. **"Behave exactly as before" is the operative clause.** A requirement about
   the code path would have to name the code path, and this one names
   availability, placement and the front end — the three things a visitor and a
   site owner can observe.
3. **The code-path reading is not satisfiable by anything.** Under it, no change
   could ever touch a file the resource flow reads, which would freeze the flow
   permanently rather than protect it.

So: it survives unmodified, and **⑩ adds no MODIFIED delta for it**.

That reading obliges evidence rather than assertion, which is task 1.2 and is
discharged: the resource flow's guts moved onto the shared builder and the shared
partials, and its tests passed before and after **unmodified** — `git diff` over
`tests/` is empty for every pre-existing file. Two further guards were built for
the same reason:

- The `Booking` component's redirect target is unchanged, because the hidden
  `Subject` field that would change it is emitted *only* when the dispatcher put
  the subject in the URL. A component-invoked flow has no token, so its
  Post-Redirect-Get is byte-for-byte what it was.
- `LengthIsFixed` is `false` for a resource, always — even when its grid holds one
  value. Rendering a one-option grid as settled text would have been a behaviour
  change smuggled in as a shared-partial nicety.

Verified live afterwards: the existing `Booking` component renders and books
exactly as before, and its redirect carries no query string.

### D9 — the deterministic refusal asks Core all three of its questions

The spec's scenario names one deterministic cause ("a role with no eligible
resource at all"), but the requirement it belongs to says *"can never be fulfilled
as configured"* — and Core can already answer that in three ways, all of which
render as an empty times list on every date, forever:

```
  PoolSufficiency.FindShortfall     the roles cannot be filled at once by
                                    distinct resources (this subsumes an empty
                                    pool: a slot with no candidates saturates
                                    nothing, so it is not asked separately)
  StartAlignment.FindMisalignment   two roles' start grids can never coincide
  no common length                  no length every role can provide
```

All three are asked, over the resolved pools the booking path itself acts on.
Answering only the first would leave the other two rendering "no times available,
please choose another date" in perpetuity, which is the exact sentence the
requirement exists to remove.

This **partly discharges the reason code ⑨-1a deferred to ⑩** — the distinction
is now drawn, and drawn from Core — but only in-process. The *delivery-API* reason
code stays deferred: this change adds no contract surface, which is the claim that
keeps it a rendering-only change, and a headless consumer can still ask Core's
diagnostics through the backoffice surfaces. Carry it forward.

The wording constraint holds in both directions and is tested: the page may say a
service cannot currently be booked, never that one *is* available, and never which
role was short of what.

## Guarantee diff — the one MODIFIED requirement (task 7.1)

Done by hand against `openspec/specs/default-frontend/spec.md`, reading for
guarantees rather than for prose. Every clause and every scenario of *Accessible,
semantic markup (WCAG 2.2 AA)* accounted for:

| Guarantee as it stood | Disposition |
| --- | --- |
| The rendered flow SHALL meet WCAG 2.2 AA and use semantic HTML | Carried, **widened** to name all three surfaces |
| Every form control SHALL have a programmatically associated label | Carried, verbatim |
| Start times SHALL be radio controls in a `fieldset` with a naming `legend` | Carried, verbatim |
| Hints and error text SHALL be associated with their controls | Carried, verbatim |
| Required inputs SHALL be indicated in text, not colour or placeholder alone | Carried, verbatim |
| The flow SHALL be fully operable by keyboard | Carried, **widened** ("Each flow") |
| The page SHALL remain usable, logical order, no author stylesheet | Carried, **widened** ("Each page") |
| *Scenario:* Every control is labelled | Carried, reworded "in either flow" |
| *Scenario:* Start times are a labelled radio group | Carried, reworded "in either flow" |
| *Scenario:* Usable without an author stylesheet | Carried, reworded "any flow" |

**Nothing dropped, deliberately or otherwise.** Two scenarios are added inside the
requirement (the catalogue; the service flow meeting every clause), and one clause
is added (a set of related controls is a grouped set with a `legend`). The `-`
lines of the sync diff must still be read at archive time, since a scenario-title
comparison cannot tell a rename from a deletion — but the reworded titles above
are the renames to expect.

## Risks / Trade-offs

- **[Shared partials become a lowest-common-denominator component by accretion]** →
  The seam is defined by *what must not drift* (accessibility-critical markup), not by
  what happens to be similar. If a partial starts taking a flag to render differently
  per flow, that is the signal it belongs to neither and should be split back.

- **[A suite that only proves the happy path]** → "A service books on a good day"
  passes an implementation that renders every refusal as "no times available", which
  is the exact defect this project has shipped guards against three times. The
  covering tests are the two refusals, proved to differ, plus the disclosure test that
  the deterministic one names no role.

- **[The multi-resource confirmation renders one resource]** → The response carries a
  collection and a single-role service makes a collection of one, so a `.First()`
  passes every single-role test and fails only for the multi-role case this change
  exists to serve. The covering test is a room-and-therapist booking whose
  confirmation names both.

- **[Scope drift into Core]** → The claim that this is rendering-only is checkable:
  if a task starts needing a domain type, an endpoint or a migration, the scope has
  drifted and it belongs in ⑩-1. Stated so the apply notices rather than absorbs it.

- **[⑩-1 is remembered as "nice to have"]** → The pin exists, is specified, is tested
  and is reachable by headless consumers; only the default UI cannot use it. That is a
  gap between what the package does and what it shows, and gaps like that are how a
  feature quietly becomes dead code.
