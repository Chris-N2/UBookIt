## 1. The view

- [x] 1.1 Register a third `sectionView` in the client manifest, beside Resources and
  Services, under the same `Umb.Condition.SectionAlias` condition. Weight below both, so the
  section still opens on Resources.
- [x] 1.2 `bookings-view.element.ts` as the shell, matching `resources-view`. **No editor to
  route to** — this change is read-only, so the shell renders the list and nothing else. Do
  not build a routing seam for a workspace that does not exist.
- [x] 1.3 `bookings-list.element.ts`: `_items` / `_total` / `_skip` / `_loading` / `_error` as
  `@state`, loading through the generated client and normalising failures through
  `toApiErrors`, exactly as `resource-list` does.

## 2. The window

- [x] 2.1 Two `uui-input type="date"` controls, labelled, defaulting to Monday and Sunday of
  the current week in the browser's local calendar (design D1).
- [x] 2.2 **Send dates.** No `Date` arithmetic that produces an instant, no offset, no zone.
  The value posted is what the input holds.
- [x] 2.3 Reload on change of either date, resetting paging to the first page — a window
  change makes the current page number meaningless.
- [x] 2.4 Surface an over-wide window as the endpoint reports it, in terms of the dates sent.
  `toApiErrors` already flattens the problem-details shape; this needs the message shown, not
  a new mechanism.

## 3. Status

- [x] 3.1 A multi-select over the four published names, labelled from localization.
- [x] 3.2 **None selected sends no `statuses` parameter at all** (design D2), so the
  endpoint's default applies. Sending an empty array, or sending all four, are both different
  requests and at least one of them is a restated default.
- [x] 3.3 Send published names, never localized labels.

## 4. The table

- [x] 4.1 Semantic `uui-table` with header cells: when, booker, resources, service, status.
- [x] 4.2 Resources rendered by name — the endpoint already supplies them, so no lookup.
- [x] 4.3 The service column says **booked directly** in words when there is none (design
  D5), and falls back to the id if a recorded service somehow carries no name.
- [x] 4.4 Times in each booking's own zone; zone shown beside the time only when the page
  holds more than one distinct zone (design D3).
- [x] 4.5 Prev/next paging with a "showing X–Y of Z" label, matching both existing lists
  (design D4). Do **not** introduce `uui-pagination` for one screen.

## 5. Guards that would catch the real mistakes

- [x] 5.1 **The request carries dates, not instants.** Assert over what the view sends. This
  is the guarantee the whole server-side-conversion design rests on, and a client that starts
  "helpfully" converting would produce a subtly wrong window that still returns results.
- [x] 5.2 **No status selected sends no status parameter** — not an empty array, not all
  four. Assert the request, because all three produce a plausible-looking list and only one
  is the endpoint's default.
- [x] 5.3 **A cancelled booking is reachable** through the control. Covered at the seam that
  can be wrong: the control sends the published name `Cancelled`, and the endpoint's own
  suite already asserts that asking for it returns cancelled bookings. The remaining link —
  that a human can operate the toggle — is the live check at 6.4, not something a unit test
  can claim.
- [x] 5.4 **The zone label appears only on a mixed-zone page**, and the times themselves are
  formatted in each booking's own zone — assert both directions, since a formatter that
  ignored the zone entirely would pass a single-zone test.
- [x] 5.5 **A booking with no service reads as booked directly**, not as an empty cell.
- [x] 5.6 **A failed load does not render as an empty list.** The two states look identical to
  a reader and mean opposite things.
- [x] 5.7 Mutation-check every guard above. Particular attention to 5.1 and 5.2: both assert
  the *shape of a request*, which is the kind of assertion that passes when the code under it
  has been replaced by something equally plausible.

## 6. Accessibility

- [x] 6.1 Every control labelled; the table's header cells associated with their columns.
- [ ] 6.2 Keyboard operability end to end with visible focus, focus order matching visual
  order.
- [x] 6.3 A failed load announced to assistive technology, not merely rendered.
- [ ] 6.4 Verify in the running backoffice, not only in tests — the previous UI change found
  two defects only the browser could show.

## 7. Close

- [x] 7.1 `en-US` strings for every new label, message and column heading.
- [x] 7.2 `tsc` clean and the client vitest suite green, compared against the 69 baseline.
- [x] 7.3 Full solution build at **zero** warnings from a clean `bin`/`obj` — and do not sweep
  `node_modules` while doing it.
- [x] 7.4 Full test suite green against the 1710 baseline.
- [x] 7.5 `openspec validate --all --strict`.
- [x] 7.6 **Confirm no server change was needed.** If the screen wanted a field the endpoint
  does not return, stop and report it rather than computing it in the client — that is a
  finding about the endpoint.
- [x] 7.7 Sweep sibling specs for sentences this change falsifies. **Start with
  `booking-management` itself** — the capability being modified is the one the last change
  proved easiest to skip, because editing its requirements feels like having read it. Then
  `resource-management`'s section and accessibility requirements, and `packaging`.
- [ ] 7.8 **At sync: correct `booking-management`'s Purpose.** Its closing line —
  *"This capability currently covers only the reading half of see"* — is true today and false
  once this lands. It is prose outside any requirement, so no delta carries it and only the
  sync can fix it. Left undone here would leave a capability's own summary contradicting its
  requirements.
- [ ] 7.9 Hand to `qa-review` in a **fresh context or subagent**.
