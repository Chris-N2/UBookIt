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
- [x] 6.2 Keyboard operability end to end with visible focus, focus order matching visual
  order.
- [x] 6.3 A failed load announced to assistive technology, not merely rendered.
- [x] 6.4 Verify in the running backoffice, not only in tests — the previous UI change found
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
- [x] 9.3 Clean-build gates, then QA round 2.

## 10. QA round 2 — REJECT: one MAJOR, and my own fix falsified my own design

- [x] 10.1 **MAJOR — the newest-load-wins rule was a real guarantee with no scenario and no
  test.** This is round 1's finding repeating one commit later: 8.4 rejected the paging reset
  and the error/empty distinction for being *"real guarantees stated nowhere"*, and I then
  fixed a third one the same way and gave it neither. Deleting any of the three `current()`
  checks leaves the suite green, `tsc` clean and validate 13/13 — and the live check that
  found it is finished, so nothing would find it again. The delta now carries the scenario.
  <br>On the test: QA agrees no *current* suite could reach it — a DOM environment is not
  installed and every client suite is pure-module — and did not insist on adding one here. The
  code comment saying "no test could have reached it" was true of the suite, not of the code,
  and is now written that way.
- [x] 10.2 **The design document was falsified by this change's own round-1 fix.** D3 still
  said the zone shows *"only when the current page contains more than one distinct zone"*,
  which stopped being true when the fallback case was added. Task 8.6 said "spec updated to
  describe both" — the spec was; the design was not. A design document gets archived and
  consulted, and it said "only".
- [x] 10.3 **My accessibility claim overreached again, one layer up.** I recorded that moving
  `aria-describedby` to the fieldset made the hint announced. It makes it **valid**: the
  reference resolves, which is what I measured. A fieldset is `role=group`,
  `aria-describedby` is not inherited by descendants, and screen readers announce a group's
  *name* on entry rather than its description — reliably read only for composite widgets with
  one focus stop, which four independently tabbable toggles are not.
  <br>The markup stays, because the hint is a visible paragraph in DOM order between the
  legend and the toggles, which is where a reader meets it. **The claim is what changes.**
  This is the same fault as round 1's CRITICAL — asserting an association without measuring
  what I claimed — and catching it in the *record* rather than the code is only luck.
- [x] 10.4 **Fixed the cause behind the race, not only the symptom.** `#latestLoad` stops a
  stale failure outliving a good response; it does not stop the failure. A date input reads as
  empty mid-edit, so clearing a segment fired a request that 400s on model binding — an alert
  interrupting the operator, the table removed, and a framework-worded message about a
  parameter they have never heard of. `shouldLoad` now declines to ask until both ends are
  present, and deliberately judges nothing else: whether a window is backwards or too wide is
  the endpoint's answer, and a second opinion here would be a rule the view invented.
- [x] 10.5 **Guarded the half of defect 9.2 that nothing was watching.** The fix guarded the
  documentation and left the screen's own string — so reverting the hint alone would ship
  green, with the docs correctly describing behaviour the screen misdescribes. That is the
  original defect with its halves swapped, and the screen's string is the one that matters:
  nobody reads the documentation while standing in front of the filter.
- [x] 10.6 **NIT taken: `skipAfter`'s `"page"` arm had no caller**, so the extraction bought a
  name and a scenario but not detection. Both paging handlers now go through it. Recorded
  honestly: this does **not** make deleting the reset detectable — nothing drives the element
  — and pretending otherwise would be the overclaim 10.3 is about.
- [x] 10.7 **NITs recorded, not fixed** — all section-wide, all belonging to the follow-up
  change 8.8 already names: no retry affordance after a failure; paging focus and live-region
  loss; and `resource-list` claiming "no resources" after a failed load, which is the defect
  `showsEmptyMessage` fixes here. QA is right that fixing this one here made the two lists
  diverge — the argument in 8.8 for leaving paging alone cuts the other way for this, and both
  now need the same follow-up.
- [x] 10.8 Clean-build gates, then QA round 3.

## 11. QA round 3 — APPROVE with NITs; three taken

No must-fix. All three taken, because the first is the third pass over one claim and the
other two are false sentences in artifacts that get archived.

- [x] 11.1 **Stopped re-wording the hint claim and delivered it instead.** Rounds 1 and 2 both
  moved `aria-describedby` and both described the result too generously; round 3 pointed at a
  fix the repo had already made three files away. `resource-editor` uses a **native checkbox**
  rather than `uui-toggle` for precisely this — *"The explanation has to be ASSOCIATED with
  the control, not merely sitting next to it"* — for a hint it judged **less** load-bearing
  than this one. My comment had also cited the wrong precedent: the service editor's
  fieldset-level reference carries group **errors** that are separately announced, not a hint.
  <br>The status filter is now four native checkboxes, each carrying the reference. It is what
  I twice said was true, and relying on DOM order would have failed anyone in focus mode
  tabbing between the controls — the residue round 3 named and the second attempt did not.
  Checkboxes are also the better semantics: four independent filters, not four switches.
- [x] 11.2 **The accessibility requirement now says what this view does and why it differs.**
  It promises failures are *announced* rather than *associated with their fields* — deliberate
  and narrower, because this view has one whole-request failure and no per-field ones, and
  promising an association with nothing to associate is a requirement written for a different
  screen. It also now states that an explanation which changes what a control does must be
  attached to the control, and that a native control is preferred where a library one cannot
  carry the association — so the library-components preference cannot be read as licence to
  drop a guarantee the section's editors already keep.
- [x] 11.3 **Two false sentences in the artifacts.** The proposal claimed `uui-combobox` was
  "already present and already used" — this client uses it nowhere and has twice recorded a
  decision against it. The conclusion (no new dependency) survives; the statement did not.
  And the zone sentence in the proposal still omitted the fallback arm that D3 had already
  been corrected for, so the two read inconsistently in the archive. Both fixed.
- [x] 11.4 **NIT — `docs/backoffice.md` opened by saying the section is where resources and
  services are configured.** True and now incomplete: it is also where bookings are read.
- [x] 11.5 **NIT recorded, not fixed — the zone column renders the raw IANA identifier** while
  the spec requires displayed strings come from localization. It is endpoint data rather than
  package prose, on the same footing as a booker's name or a resource's; noted because the
  distinction is worth having written down before somebody reads the requirement literally.
- [x] 11.6 **Live-verified.** Chris confirmed the date filter and the status checkboxes still
  work and that the hint's id appears in `aria-describedby`; measured in the page, all four
  checkboxes carry a reference that **resolves** to the hint element in the same root, each is
  labelled, and no `uui-toggle` remains. Resolution is the part an attribute inspection cannot
  show — an id that points at nothing looks identical in the markup.
  <br>**What is still not proven, and is worth saying plainly a third time:** no screen reader
  has been run. What differs from the two failed attempts is the *mechanism*, not the strength
  of the evidence — a native `<input aria-describedby>` resolving within its own root is the
  canonical case the attribute exists for, where the fieldset version rested on group
  descriptions being inherited and announced on entry, which they are not. That is a reason to
  expect this to hold; an actual AT pass is the only thing that would settle it, and it belongs
  in the section-wide UI review rather than here.
- [x] 11.7 Clean-build gates, then QA round 4.

## 12. Carried forward, for the section-wide UI review

Chris has a UI review planned once the functionality is in. These belong to it rather than
here, and are recorded so they are not lost when this change archives:

- **An actual screen-reader pass** over the whole section (11.6). Three rounds of this change
  turned on what an assistive technology would announce, and every answer so far has been
  reasoned rather than heard.
- **Paging loses focus and rebuilds its live region** (8.8) — the `<nav>` sits inside the
  branch the loading state swaps out, so Next drops focus to the document and the
  "showing X–Y of Z" span is recreated rather than updated. Verbatim in `resource-list` too.
- **No route back after a failed load** (10.7): the paging controls disappear while `skip`
  keeps its value, so the only way back to page one is changing a filter. Section-wide.
- **`resource-list` says "no resources" after a failed load** (10.7) — the defect
  `showsEmptyMessage` fixes here, still present next door. The two lists now differ, which is
  the cost of fixing one in isolation and an argument for doing the rest together.
- **A DOM test environment**, installed once, would let all three element-level guarantees be
  asserted rather than reasoned about — including the newest-load-wins rule this change could
  only write into the spec.
- **Cosmetic polish** Chris has already noted.

**Outside this change, worth raising with Chris:** the `qa-review` skill's DevExpress scan
greps the working tree, which on this machine hits gitignored `obj/*.nuget.g.props` recording
local DevExpress NuGet fallback folders. A reviewer following it verbatim after a build would
fail a hard gate on nothing. `git grep` over tracked files is the scan that means something.
