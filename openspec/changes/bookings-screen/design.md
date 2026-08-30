## Context

Two section views already exist and set the pattern, measured rather than assumed:

- `resources-view.element.ts` (55 lines) is a shell that routes between a collection element
  and a workspace editor. `services-view.element.ts` is the same shape.
- `resource-list.element.ts` loads through the generated `UBookItBackofficeService`, holds
  `_items` / `_total` / `_skip` / `_loading` / `_error` as `@state`, and normalises failures
  through `toApiErrors`.
- **Paging is prev/next `uui-button`s with a "showing X–Y of Z" label**, not `uui-pagination`
  — even though `uui-pagination` is installed. The precedent is prev/next in both existing
  lists.
- `uui-input` supports `type="date"` (confirmed in
  `node_modules/@umbraco-ui/uui-input/lib/uui-input.element.d.ts`), so the window needs no
  custom picker and no date library.

The endpoint this consumes takes `from` and `to` as **dates** and resolves them against the
site's zone server-side. That was decided so no client would reimplement daylight saving.

## Goals / Non-Goals

**Goals:**

- An operator can see what the site has taken, over a window they choose, without leaving the
  backoffice.
- Cancelled bookings are reachable, because the endpoint's default hides them.
- The screen adds no rule of its own — no client-side zone maths, no second default.

**Non-Goals:**

- Any mutation, including cancel. Read-only.
- A resource filter, a service filter, or a detail view (see the proposal).
- Visual refinement beyond the accessibility bar.

## Decisions

### D1. The window is two date inputs defaulting to the current week, computed in the browser's local calendar

**Decision:** `<uui-input type="date">` for each end. On first load, `from` = Monday of this
week, `to` = Sunday, derived from the browser's local date.

**Why the browser's calendar, when the server resolves against the site's zone:** the screen
needs *a* sensible starting week, and the only calendar it has is the user's. The two can
disagree by a day for an operator working from a different country at the turn of a week —
and the consequence is that the default window is off by a day, which the operator can see
and change. The alternative is asking the server what "this week" means, which is a new
endpoint for a default value.

**What the screen must NOT do** is convert those dates to instants. It sends `2026-08-31`,
not midnight-in-some-zone. The endpoint owns that conversion; a screen that did its own would
be the second implementation the API exists to prevent.

**Alternatives considered:** *A single "this week / this month / custom" preset control.*
More convenient, more to build, and it hides the actual window — which an operator needs to
see, because the guardrail refuses over-wide ones and the refusal names dates. Presets can be
added later on top of visible dates; visible dates cannot be added on top of presets without
redesigning.

### D2. Status is a multi-select of the four published names, defaulting to "what's booked"

**Decision:** a control offering `Requested`, `Confirmed`, `Cancelled`, `Declined`, with none
selected meaning "the endpoint's default" rather than "none of them".

**Why "none selected" must mean the default and not an empty result:** it is what the port
does, and restating it here as a different rule is the thing the endpoint's own requirement
forbids. The screen sends no `statuses` parameter when the user has selected none.

**The labels come from localization, the values do not.** Status crosses the wire as the
name the package publishes; the screen sends `Confirmed`, and shows whatever `en-US` says.
A localized label must never become the wire value.

### D3. Times render in each booking's own zone; the zone is shown when a page holds more than one

Covered in the proposal's Risks with its narrowing stated. The rule the screen implements:
format each booking's interval in its own `timeZoneId`, and render the zone beside the time
**only when the current page contains more than one distinct zone**. On a single-zone page —
every ordinary site — the column stays quiet.

**Why not compare with the site's zone:** the screen does not have it, and obtaining it means
either a field the read port cannot supply (forbidden by that capability) or a second request
for one string. Neither is worth it for a label.

### D4. Paging is prev/next, matching both existing lists

`uui-pagination` is installed and unused. Introducing it here would give the section two
paging idioms — the bookings list working one way and the two lists beside it another —
which is worse than the mild plainness of prev/next. If the section ever moves to
`uui-pagination`, it should move all three together.

### D5. The service column distinguishes "booked directly" from "no name"

A booking with no service was placed directly, and the screen SHALL say so in words rather
than leaving the cell blank. A blank cell reads as missing data; the absence is a fact. A
booking whose recorded service name is empty — which nothing produces, but which the store's
mapper tolerates rather than discarding an id — renders as its id so the row is still
traceable.

## Risks / Trade-offs

- **The default week can be a day off for an operator in another country.** → Visible and
  changeable; the alternative is an endpoint for a default value.
- **Two accessibility requirements now state the same bar** (resource editing, and this
  view). → The cost of not replacing a correct requirement wholesale. Stated in the proposal
  as the deliberate choice it is.
- **The zone label misses the all-rows-share-a-non-site-zone case.** → Stated in the spec.
  Reachable only if a site changed its zone after taking bookings.
- **A screen is where "the endpoint doesn't quite return what I need" gets papered over.** →
  If the list wants a field the endpoint lacks, that is a finding to report and a separate
  change, not a computation added to the client. The endpoint's own requirement already
  forbids adding fields the port cannot supply; this is the same rule pointed at the screen.

## Open Questions

- **Empty state wording.** "No bookings in this window" is right for an empty week; it is
  misleading if the request failed. The list already distinguishes `_error` from an empty
  `_items`, so this is a question about strings, to settle while writing them.
- **Whether the window controls belong above or below the table.** Above matches the reading
  order of "choose, then see"; the existing lists have no filters to compare against. Settle
  at apply with the accessibility bar in mind — whichever it is, focus order must match
  visual order.
