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

The domain distinguishes `conflict` (transient) from `service-unavailable`
(deterministic). The front end renders that distinction and nothing finer:

```
  conflict              → "That time is no longer available." + refreshed times, retry invited
  service-unavailable   → "This service is not currently available for booking."  no retry
  duration-*            → the existing length messages, unchanged
```

The mapping is from the **stable code**, never from message text, which is the rule
the failure-handling requirement already sets for the resource flow.

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
