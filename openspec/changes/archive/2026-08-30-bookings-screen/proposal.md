## Why

The bookings endpoint shipped and nothing can reach it. The backoffice section has views for
resources and services — the things an operator *configures* — and none for bookings, the
thing the package exists to collect. An operator cannot see what their site has taken
without querying the database.

This is the last piece of *seeing* bookings. Cancelling them is the only other v1 verb and is
its own change.

## What Changes

- **A third section view, `Bookings`**, registered in the client manifest beside Resources
  and Services, under the same section-alias condition.
- **A bookings collection view**: a semantic `uui-table` listing what the endpoint returns —
  when, who, which resources, which service, and status — with the window and status controls
  below, and prev/next paging.
- **The window is two date inputs, defaulting to the current week** (Monday to Sunday of
  today). The endpoint requires a window and the screen must therefore open on one; a week is
  an operator's unit and sits comfortably inside the default 31-day guardrail.
- **A status filter.** The endpoint's default returns only bookings that block time, so
  without a control a cancelled booking is unreachable from the screen — which the port's own
  requirement says must never be true.
- **Dates are sent as dates.** The screen SHALL NOT convert to instants or reason about
  daylight saving: that rule lives on the server precisely so no client reimplements it.
- **`en-US` localization** for every new string, as the existing views do.

## Non-goals

- **Cancelling a booking.** The only other v1 verb, and its own change: it is a mutation,
  needs an endpoint that does not exist, and needs a confirmation flow. This screen is
  read-only.
- **Any other verb.** No placing on someone's behalf, no approve/decline, no rescheduling.
  Those are each domain changes, and the backoffice documentation already says the section
  does not do them.
- **Filtering by resource.** The endpoint supports it; the control does not exist. A
  multi-select over a paged resource list is its own component, and the screen is useful
  without it. Deferred deliberately rather than overlooked.
- **Filtering by service**, for the reason `booking-management` now states: the data exists,
  and the filter belongs with a design that has somewhere to put it.
- **A booking detail view.** The read port returns everything a row shows and nothing more,
  so a detail panel would have nothing further to display. When cancelling arrives it may
  need one; that change can decide.
- **Changing the endpoint.** If the screen wants something the endpoint does not return, that
  is a finding to report, not a field to add here.

## Capabilities

### New Capabilities
- *(none — the screen extends `booking-management`)*

### Modified Capabilities

- `booking-management`: gains the backoffice view over the endpoint it already specifies —
  what the collection shows, that the window defaults to something usable rather than empty,
  that cancelled bookings are reachable through a control, that the screen sends dates rather
  than instants, and its own accessibility bar.

**`booking-management`'s Purpose statement is falsified and must be corrected at sync.** Its
last line reads *"This capability currently covers only the reading half of see."* — true
while there was an endpoint and no screen, and false once this lands: the capability then
covers *see* entirely, and only *cancel* remains. It is prose outside any requirement, so it
carries no delta; it is recorded here and in the task list so that syncing this change
includes fixing it rather than leaving the capability's own summary contradicting its
contents.

Found by sweeping the capability being modified **first** — which is exactly where the
previous change's sweep failed, for the same reason: having edited its requirements feels
like having read it.

**Deliberately NOT modified:** `resource-management`'s *Editor accessibility baseline*. It
would be the obvious place to widen, and widening it means replacing it wholesale — carrying
six scenarios about resource editing forward to say something about a bookings list. The
bookings view gets its own accessibility requirement in its own capability instead. Two
requirements stating the same bar is a smaller cost than a wholesale replacement of one that
is currently correct.

## Impact

- **`UBookIt.Backoffice/Client`**: a `bookings-view` element and a `bookings-list` element,
  a manifest entry, and `en-US` strings. No new npm dependency — `uui-input type="date"`,
  `uui-table`, `uui-button`, `uui-label` and `uui-loader-bar` are all already present and
  already used, and the status filter is native checkboxes, following the resource editor's
  documented reason for preferring them where a hint must be associated with the control.
  *(An earlier draft of this sentence listed `uui-combobox`, which this client does not use
  anywhere and has twice recorded a decision against.)*
- **No server change.** No endpoint, no DTO, no Core, no persistence, no migration. If this
  change needs one, that is a finding.
- **No public C# API change.** The TypeScript client is regenerated only if the endpoint
  changes, which it should not.

## Risks

**The zone a booking was made in is shown, and the screen cannot fully honour the rule it is
meant to follow.** Times render in each booking's own recorded zone. The intent is to label
the zone whenever it differs from the site's — but **the screen does not know the site's
zone**: the endpoint returns each booking's zone and nothing page-level, and adding a field
the read port cannot supply is exactly what that capability forbids.

So the screen labels the zone when **the page holds more than one distinct zone** — which
catches the case that actually misleads, two rows read against each other — and also when a
zone could not be resolved and the time is being shown in another one. It does **not**
catch every row sharing one zone that is not the site's — possible only if the site's zone
changed after those bookings were placed. That narrowing is stated in the spec rather than
papered over, and the fix, if it is ever worth making, is a decision about the endpoint
rather than something to smuggle into a screen.
