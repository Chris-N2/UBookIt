## Why

The booking flow asks a visitor to choose a date before telling them anything about it. The
control is an `<input type="date">` bounded by the resource's lead time and horizon, so every
date inside that range looks equally plausible — and most of them are not. A visitor picks, waits
for a reload, reads *"No times are available on Tuesday 15 September"*, and picks again. On a
busy resource that is a guessing game with a page load per guess.

The package already knows the answer. `GetBookableStartsAsync` takes a date **range**, and every
start it returns carries the length range it admits. One query over the next few weeks says
exactly which days are bookable and at what lengths; the flow simply never asks it, because it
asks about one day at a time.

This is roadmap 0.4.0, brought forward because it depends on nothing else and is the most visible
improvement on the list.

## What Changes

- **The first step lists the dates that actually have availability**, over a window of up to
  ~30 days, as a grouped set of radio buttons beside the existing length and resource controls.
- **The list is filtered to the chosen length.** A date offering only 30-minute gaps does not
  appear when the visitor has asked for two hours, and changing the length re-renders the list.
  *Decided with sign-off.*
- **The date input stays**, as the route to dates beyond the listed window. It is not a duplicate
  control: the list answers *"when can I come soon?"* and the field answers *"I want this
  specific date"*. **The two submit different parameters and the precedence between them is
  stated**, because two controls posting one parameter is a browser sending both values and the
  server picking arbitrarily.
- **One query replaces one query.** The flow currently reads availability for the selected day;
  it will read the window instead and derive both the date list and that day's times from the
  same result. The work grows from one day to about thirty; the number of queries does not.
- **The empty window says so.** *"No dates in the next 30 days have availability for 2 hours"* is
  a different sentence from *"no times on this date"*, and it has to name what would change the
  answer.

### Not a breaking change

No public contract moves. `IBookingFormView` gains members — which `theming` makes a published
compatibility promise, so it is called out in the design — but a theme consumes the model rather
than implementing it, and every existing view renders unchanged if it ignores them. The delivery
API is untouched: this change consumes an endpoint that already exists.

## Capabilities

### New Capabilities

None. This is the shipped front end doing better with data the package already produces, and it
belongs in `default-frontend` beside the flow requirements it changes.

### Modified Capabilities

- `default-frontend`: the single-resource and service flows both currently describe choosing a
  date from a bounded input. Both gain the date list, its coupling to the chosen length, the
  relationship between the list and the field, and the empty-window state. The accessible-markup
  requirement already governs how a grouped choice is rendered and needs no change — the new
  control is the same shape as the start-time group it sits above.

## Impact

**Code**

- `UBookIt.Web`: the date window on `IBookingFormView`; a new shared partial rendering the list;
  both flow builders widening their availability read to the window and deriving the selected
  day's times from it; the query-parameter and precedence handling in the flow input.
- `UBookIt.Core`: nothing. The availability service already answers this question.

**Cost**

A first-step render goes from projecting one day of availability to about thirty. That is the
change's one real cost, it is bounded by the existing `MaxQueryRangeDays` guardrail, and it is
**measured before anything is done about it** — see the design. *Decided with sign-off.*

**Contracts**

A new shared partial joins the published building-block list a theme may call.

## Non-goals

- **Caching.** Not in this change. A cache over availability is a correctness risk — stale means
  offering a slot somebody has taken — and the package has no caching anywhere today, so
  introducing one is a change with its own invalidation questions. This change measures the cost
  and records the number; if it warrants a cache, that is the next change and it starts from
  evidence. *Decided with sign-off.*
- **Paging beyond the window.** The date field is the route to a further date, not a "next 30
  days" link. Each page of a window is a fresh thirty-day query, on the least common journey.
- **Reducing the horizon.** A site offering 90 days goes on offering 90 days. Showing a shorter
  window is a rendering decision and must not become a booking-policy one.
- **A calendar grid.** A month view is a widget: it implies dates the package would then have to
  render as disabled, it is markedly harder to make operable without a stylesheet, and the
  question being answered is "which days are free", not "what does September look like".
- **Changing what counts as available.** No projection logic moves. If the list and the times
  ever disagreed, the bug would be in this change and not in `availability`.
- **JavaScript.** The flow stays server-rendered and operable with scripting disabled, per the
  standing invariant. A list of dates is easier to do this way than a picker was.
