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
- [x] 7.9 Hand to `qa-review` in a **fresh context or subagent**.

## 8. QA round 1 — REJECT: one CRITICAL, five MAJORs

The CRITICAL and the first MAJOR are the same fault, and it is the one I flagged as least
certain: **I asserted an accessibility association without measuring it, in a codebase that
had already measured it and written the answer down.**

- [x] 8.1 **CRITICAL — the two date controls had no accessible name.** `uui-label` is not a
  native label: its `for` is a click handler that focuses the target, and it sets no
  `aria-labelledby`. `uui-input` names its internal input from its own `label` property or
  `aria-label` **and nothing else**. So both controls were announced as unlabelled edit
  fields, and only a pointer user got the association. Every other `uui-input` in this client
  pairs `uui-label for=` with `label=` — I diverged from an established pattern without
  noticing it was a pattern. Fixed.
- [x] 8.2 **MAJOR — the status hint was associated with nothing.** `aria-describedby` on a
  `uui-toggle` host is dropped: `uui-boolean-input` forwards `aria-label` and
  `aria-labelledby` to its internal input and no more. The hint is the load-bearing part of
  that control — it is how an operator learns a cancelled booking is one toggle away rather
  than gone — and it was announced with nothing. **The editor had already hit this exact wall
  and left a comment about it.** Moved to the `fieldset`, following the service editor.
- [x] 8.3 **MAJOR — the "dates, not instants" guard survived its own named mutation.** QA
  replaced the passthrough with a `new Date(...T00:00:00Z)` round-trip and all 21 tests
  passed — the defect the test's own comment claims to catch. The shape assertions catch
  `toISOString` and epochs; they cannot catch a value-shifting round-trip, because on a UTC
  runner that round-trip is the identity. Now also asserts that a value which is **not a date
  at all** passes through unchanged, which no conversion survives on any runner. Task 5.7
  singled this guard out for particular attention and I checked it against mutations it was
  already immune to.
- [x] 8.4 **MAJOR ×2 — two decisions were left outside the seam the design argues for.** The
  paging reset on a query change, and the "don't say *no bookings* after a failure"
  distinction, were both inline in the element and both unasserted — the same class of
  invisible failure the pure module exists for, sitting one line outside it. Extracted as
  `skipAfter` and `showsEmptyMessage`, both mutation-checked. The paging reset also gained a
  spec scenario, since it was a real guarantee stated nowhere.
- [x] 8.5 **MAJOR — an ADDED requirement asserted something the server contradicts.** It said
  selecting no status must not mean "no statuses", *"which would return nothing"*. It does
  not: `BookingsController` and `BookingQuery` both treat an empty set exactly as an absent
  one. The **conclusion** (omit the parameter) is right and the **reason** was false, in the
  requirement and three comments — and a requirement is permanent once synced. Corrected to
  the true reason: a view that states a default creates a second one, and the genuinely
  different request is naming all four statuses.
- [x] 8.6 **MINOR — the UTC fallback did not say so on a single-zone page.** `zoneLabelNeeded`
  deduped on the raw identifier, so a page where every booking carried an unresolvable zone
  counted as one zone and showed UTC times with no label — the exact misattribution
  `formatInterval`'s comment promised it would not make. Dedupe is now on the resolved zone,
  and `zoneFallbackOccurred` reports the case dedupe cannot see. Spec updated to describe
  both, since the code was doing more than the requirement said.
  <br>Worth recording: the first mutation for this was **not caught**, because the element's
  behaviour is identical either way once the fallback flag exists. The test now asserts the
  function's own contract, where the difference is real.
- [x] 8.7 **MINOR — an interval crossing midnight read backwards.** A 22:00–01:00 booking
  showed as one date with an end apparently before its start. Both dates are now named when
  they differ, and only then.
- [x] 8.8 **Flagged, not fixed: paging destroys focus and the live region.** The `<nav>` is
  inside the branch `_loading` swaps out, so Next drops focus to `<body>` and the
  `aria-live` span is recreated rather than updated. It is a verbatim copy of the shipped
  `resource-list` pattern, so it is a **section-wide** defect and fixing it here would leave
  two lists behaving differently. Recorded for its own change; task 6.2 will see it.
- [x] 8.9 **I walked into a documented trap.** The first draft of the label fix put backticks
  inside a comment in a Lit template literal, which ends the template — `resource-editor`
  carries a warning about exactly that, three lines from code I had just read.
- [x] 8.10 Clean-build gates, then QA round 2.

## 9. The live check (6.2, 6.4) — and it found two more

Run in the real backoffice against real data. **QA's prediction held: 6.4 is the gate that
would have caught the CRITICAL**, and it went on to find two defects nothing else could.

Verified working: the section view registers and opens; the window defaults to Monday–Sunday
of the current week; a week with no bookings shows the empty message; changing the window
loads the rows; times render in the booking's own zone (09:00Z showing as 10:00 BST); no zone
label on a single-zone page; "Booked directly" against the direct booking and "Massage"
against the service one; "Showing 1–2 of 2" with both paging buttons correctly disabled; the
status filter reaches the cancelled booking; a failed load is announced. **The CRITICAL fix
was confirmed at the source that matters** — both native date inputs carry a real
`aria-label`, and the fieldset's `aria-describedby` resolves to the hint in the same shadow
root. All eight controls are keyboard-reachable in visual order with a visible focus ring.

- [x] 9.1 **A stale error could sit above correct results.** Changing From and then To starts
  two requests; the first went out with the second date momentarily blank, and its failure
  arrived *after* the good response — leaving an error contradicting the data beside it. That
  is a worse version of the state `showsEmptyMessage` exists to prevent.
  <br>**No test could have reached it**: every test starts one load and awaits it. The view
  now stamps each load and lets only the newest write state, verified by reproducing the
  exact sequence in the browser.
- [x] 9.2 **The status hint described the behaviour backwards.** It said "Tick a status to
  include others", which reads as adding to what is shown — and ticking Cancelled shows
  *only* cancelled, because the endpoint uses supplied statuses exactly as given. An operator
  ticking Cancelled to see a cancelled booking alongside today's would watch today's
  disappear and conclude the filter was broken. The behaviour is right and the sentence was
  wrong; both the hint and the documentation now say "only those instead", with a test.
  <br>This is the load-bearing hint QA and I had both already looked at twice, in a file
  whose comment calls it load-bearing. Reading it is not operating it.
- [ ] 9.3 Clean-build gates, then QA round 2.
