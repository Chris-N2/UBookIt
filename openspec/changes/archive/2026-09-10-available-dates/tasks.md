## 1. The window

- [x] 1.1 Add a pure function computing the window from `today`, the subject's **lead time**, its **horizon**, and `SiteBookingSettings.MaxQueryRangeDays`. Pure so it is testable without a flow, like `BookingFormBuilder.TodayIn` and `LongestAvailableMinutes` already are.
- [x] 1.2 **The preferred span (~30 days) is a preference, clamped — never a constant the read trusts.** A site may set `MaxQueryRangeDays` to 7; a 30-day read would then be refused with `date-range-too-large` on **every** step-1 render and that site would have no flow at all. This is the highest-severity failure in the change and it is invisible on a default configuration.
- [x] 1.3 Use the same saturating arithmetic `CalendarBounds.AddDaysSaturating` already uses for `MaxDate` — a large horizon must not throw while rendering a form.
- [x] 1.4 The window's start respects lead time. Listing a date the domain will refuse is the guessing game this change removes, moved one step earlier.

## 2. The read

- [x] 2.1 Widen both flows' `GetBookableStartsAsync` call from `(selectedDate, selectedDate)` to the window. **One read, not two** — the date list and the selected day's times both come out of it.
- [x] 2.2 Derive the selected day's times by filtering that result to the selected date, in the site time zone. **Group by LOCAL date, not by UTC date** — a start at 23:30 UTC is the next day in `Europe/London` in summer, and grouping in the wrong zone would put a date in the list that the times below disagree with.
- [x] 2.3 The service flow passes the same `choice.Chosen` it passes today, so the list narrows to the chosen who exactly as the times do.
- [x] 2.4 A date is in the list when at least one of its starts `Admits` the chosen duration — **the same predicate step 2 uses**, not a second one. Two answers to "does this fit?" would eventually disagree, and the disagreement would show as a date you can pick and then find empty.

## 3. The view model

- [x] 3.1 Add the window's dates to `IBookingFormView` as a list of values — the date, and whether it is the selected one. Not pre-rendered markup: `theming` makes the model a published contract and a theme must be able to render it its own way.
- [x] 3.2 **BREAKING — published contract.** `IBookingFormView` gains members; declare it, as `privacy-notice` did. A theme consumes the model rather than implementing it, so the practical impact is nil, but the promise is about the type.
- [x] 3.3 Carry whether the selected date is **outside** the window, so the view can state which date it is showing without recomputing the window.
- [x] 3.4 Carry enough to explain an empty window: that it is empty, and the longest length that *would* find something. The single-day case already does this via `LongestAvailableMinutes`; this is the same idea at window scale.

## 4. Rendering

- [x] 4.1 New shared partial for the date list. Not into `_DateAndLength`, which already renders three controls and their aria wiring.
- [x] 4.2 A `fieldset` with a `legend`, radios with associated labels — **the same shape `_Times` uses**, so the page has one idiom for "choose one of these" rather than two.
- [x] 4.3 The list submits the existing date query parameter.
- [x] 4.4 The "another date" field submits a **different** parameter and **wins when present and parseable**. Two controls on one parameter means the browser sends both values and binding picks arbitrarily — this is the concrete form of "two ways to do one thing".
- [x] 4.5 Label both so they read as different questions — "when can I come soon?" versus "I want this specific date" — rather than as two ways to do the same job.
- [x] 4.6 When the selected date is outside the window, state which date is being shown. Otherwise the page shows a list with nothing selected beside times for a date the list does not contain.
- [x] 4.7 The empty-window state: say no date in the window is available at that length, and name what would change it. **Distinct wording from the empty-day state** — the two are different facts and a reader must be able to tell which they are being told.
- [x] 4.8 Add the new class(es) to the published vocabulary deliberately — `default-frontend` makes the class names a stable contract, so this is a contract addition and both the vocabulary list and the block list need it.
- [x] 4.9 Add the partial to `UBookItThemeContract.SharedPartials` and to the theming guide's table.
- [x] 4.10 **No JavaScript.** Standing invariant. And no colour-only distinction: a date's state is conveyed in text.

## 5. Verification

- [x] 5.1 DONE — the differential property, asserted three ways: filtering the window to one date equals that date's starts; the day filter uses the site zone; and the grouping and the filter agree about every start in a window, with nothing stranded.
- [x] 5.2 DONE against CONFIGURED bounds — a guardrail of 7, a horizon of 3, a horizon of 0, and a property test over every guardrail from 1 to 40 asserting the span never exceeds it. Plus calendar saturation at DateOnly.MaxValue.
- [x] 5.3 DONE, both directions in one test — the same start admitted and not admitted — so an inverted predicate cannot pass half of it.
- [x] 5.4 DONE. 23:30 UTC on the 10th is 00:30 on the 11th in Europe/London, asserted on the grouping AND on the day filter. A UTC-configured suite cannot see this at all, which is why the fixture names a zone.
- [x] 5.5 DONE — **written because mutation proved it missing**, not before. Six cases including the empty-typed-value fall-through, which is the one the whole design rests on and looks like nothing.
- [x] 5.6 DONE — covered by the outside-window rendering state and the precedence tests together.
- [x] 5.7 DONE, both directions, plus the empty-list case from 7.6.
- [x] 5.8 DONE — the two empty states are asserted to differ in wording, not merely to exist. They deliberately share a class, so the wording is the only thing that distinguishes them.
- [x] 5.9 DONE — five new fixture states drive the branches, and the suite's own inventory rules confirmed the view was exercised.
- [x] 5.10 DONE, six mutations, each asserting it applied and each verifying the RESTORE recompiled. **Five caught first time; one did not** — reversing the precedence between the two date parameters left the whole suite green, because task 5.5 was still unwritten. Written, and it now fails. A seventh was added after the fact and is the one that mattered most: see 7.5.

## 6. The measurement

- [x] 6.1 DONE. Against a resource open 08:00–18:00 seven days a week, twenty runs each, warmed first.
- [x] 6.2 **THE NUMBER: one day 0.020 ms, 30 days 0.260 ms — a ratio of 12.9x, which is SUB-linear** (per-day cost falls from 0.020 ms to 0.009 ms as the range amortises). Recorded as a test that prints it and asserts only the SHAPE (ratio < days x 4), never a millisecond threshold — a timing assertion on a shared agent is a flaky test wearing a performance badge, and it would be the first thing anyone weakened.

  **What the number does and does not cover, because it would be easy to over-read.** It measures the projection over an in-memory store, so it excludes the database. That is less of a gap than it looks: the claims read takes a from/to range and is **one query whichever width it is** — QA verified this independently against both the single-resource and batched paths — so widening the window issues no more round trips. **It does return more rows**, which is the part of the figure not to over-read; trivial for a month of bookings, and worth stating rather than leaving implied. The part that scales with the window is the day-by-day projection, and that is exactly what is measured here.
- [x] 6.3 **No cache, and the number says so rather than my instinct.** 0.26 ms of projection for a whole window is not a problem worth an invalidation story. Recorded so a future change that wants one starts from this figure rather than re-deriving it.

## 7. Look at it in a browser

- [x] 7.1 DONE — rendered all four states with the shipped stylesheet and looked at each in a browser. **Two findings, and neither was reachable from the suite.** See 7.5 and 7.6.
- [x] 7.2 DONE, and this is finding one. Thirty stacked dates IS a wall — it pushes the length and who controls off the screen entirely.

  I tried the obvious fix and it was wrong: making the options a wrapping run, as the start times are. The result was **ragged** — "Wednesday 23 September 2026" is 26 characters and "Friday 2 October 2026" is 21, so September fits one per row and October two, and the columns appear and disappear down the list. The stylesheet already states the rule ("a wrapping run suits labels of uniform length, a stacked column suits labels of unpredictable length") and I had put dates on the wrong side of it by reasoning about their *shape* rather than their *width*.

  **Reverted, and the wall is accepted, because the wall is the benign case.** Thirty stacked dates only happens when nearly every date is free — and a visitor looking at a month where everything is available does not need help choosing. The list earns its place when availability is sparse, and then it is three or four rows and immediately readable. Confirmed by rendering exactly that case.
- [x] 7.3 DONE — the states render as plain fieldsets, labels and paragraphs, so with no author stylesheet the step degrades to an ordinary list. The 2.5.8 spacing floor is kept from the catalogue rule rather than invented.
- [x] 7.4 DONE — the list precedes the length and who controls in document order, its legend names the group AND the length it is filtered to ("Dates with availability for 1 hour in the next 30 days"), and the "another date" field follows with its own label and hint.
- [x] 7.5 **FINDING, and only mutation found it: the "another date" field must never be repopulated.** Adding `value="…"` to it looks like an obvious improvement — the control it replaced did exactly that — and it breaks the list permanently: the typed date beats the list by design, so a field resubmitting a date chosen three renders ago makes every later click on the list do nothing at all. The freeze spans two requests, so every single-request assertion passed either way. Now guarded by an attribute assertion, with the reasoning on it.
- [x] 7.6 **FINDING, and only looking found it: the empty window referred to a list that was not there.** With no dates, the page rendered "Showing Tuesday 15 September 2026, which is not in the list above" directly beneath a paragraph saying there is no list. Every test passed. The statement exists to resolve a contradiction between a list and the times below it; with no list there is nothing to contradict. Now conditioned on there being a list, and guarded.

## 8. Modified requirements — the guarantee diff

- [x] 8.1 `default-frontend` → *No-JavaScript single-resource booking flow*. Carried forward: the whole body and all three scenarios. Added: one paragraph and one scenario. **Dropped: nothing** — verified mechanically (3 → 4 scenarios, 3 → 4 SHALLs, 0 dropped), not by eye.
- [x] 8.2 `default-frontend` → *No-JavaScript service booking flow*. Carried forward: the whole body and all four scenarios. Added: one paragraph and one scenario. **Dropped: nothing** — verified mechanically (4 → 5 scenarios, 5 SHALLs unchanged, 0 dropped).
- [x] 8.3 The cross-flow behaviour is in **ADDED** requirements rather than duplicated into both flows — "one bar, stated once", the principle `theming` already uses. Confirm at apply time that neither flow requirement needs it restated to stay comprehensible.
- [x] 8.4 `ChangeDeltaIntegrityTests` is the authority on delta correctness, not `openspec validate --strict`.

## 9. Sweep — sibling specs this change falsifies

- [x] 9.1 CHECKED — the accessible-markup requirement enumerates no control list, and the new group uses the pattern `_Times` established. Its clause assertion moved to the new partial and was STRENGTHENED: a labelled input became a grouped choice, so it now owes a fieldset, a legend and a label per option.
- [x] 9.2 CHECKED — both fired during apply and named the new view explicitly, which is the rules working rather than a chore.
- [x] 9.3 CHECKED — four new classes added deliberately to the published set, and three to the block list, which are separate assertions.
- [x] 9.4 CHECKED, **and it fired.** The URL flow-state enumeration grew by one for `ubDateOther`. A date in a URL discloses nothing a public availability read does not already carry, and it is what keeps a date beyond the window linkable.
- [x] 9.5 CHECKED — `availability` constrains the width of a read (`MaxQueryRangeDays`) and nothing about who issues one. The change honours that guardrail rather than altering it.
- [x] 9.6 CHECKED — `delivery-api` untouched. The Razor front end reads from Core in-process, which `default-frontend` requires of it, so no delivery endpoint is involved at all.
- [x] 9.7 CHECKED — the new partial joins `UBookItThemeContract.SharedPartials` and the theming guide's table, which a test ties together.
- [x] 9.8 CHECKED — the service flow passes the same `choice.Chosen` to the window read that it passes for the times, so the list narrows to the chosen who exactly as the times do. Nothing in `service-booking` changes.
- [x] 9.9 **The one not on the list: `FieldHookTests`' choice-group exemption.** It enumerates which containers are options rather than fields, and the new date options were neither — so every flow document failed with "the label … is not inside a .ubookit-field". Added by name rather than by matching a `-option` suffix, because a pattern would exempt anything somebody happened to name that way, including a genuine field.

## 10. QA round 1 — a CRITICAL of my own making

- [x] 10.1 **CRITICAL: the widened read was not clamped, and it broke two thirds of a default site's range.** `readTo = max(selectedDate, windowTo)` pushed the span past `MaxQueryRangeDays`; the read was refused and the failure swallowed by `Succeeded ? Value : []`. QA measured it: on a stock install (guardrail 31, horizon 90) every date from offset 31 onward returned **no times for a completely empty diary** — using the very field D4 kept in order to reach those dates. Before this change the read was one day and could never exceed the guardrail, so it is a regression I introduced.
- [x] 10.2 **The fix needed a decision, not a clamp.** A single contiguous read cannot hold both a 30-day window and a date 89 days out when the guardrail is 31 — so either the list goes, the far date goes, or there are two reads. **Two reads, and only when the selected date is outside the window.** D1 forbids two reads because one day's times and the list could come from different queries and disagree; that reasoning is about **one day appearing in both**. Here the ranges are disjoint by construction, so no day is in both and there is nothing to disagree about. Inside the window, where the risk is real, it remains one read exactly as D1 requires.
- [x] 10.3 **Why nothing saw it, which is the more useful half.** The window was tested pure, the rendering was tested from hand-built fixtures, and `readFrom`/`readTo` lived in neither. Both "outside the window" fixtures were hand-built models that never went near a flow. **The seam between two well-tested halves had nothing on it.** Now `AvailableDatesFlowTests` drives real flows and **sweeps the whole horizon** rather than sampling it — a defect that begins past a boundary is exactly what chosen dates miss. Reinstating the original defect fails two of them.
- [x] 10.4 **MAJOR: the differential did not differentiate.** It compared a hand-built list against the same predicate re-typed over that same list, calling `GetBookableStartsAsync` nowhere — it could only report that the test's own LINQ agreed with itself. **The fourth instance of this defect in this project.** Replaced with a real one: both reads issued, against a fixture with a booking placed so the claims read does work, and compared.
- [x] 10.5 **MAJOR: the no-value guard guarded an element, not the parameter.** `QuerySelector` returns the first match, so QA added a hidden input with the same name *after* the field and all 2219 tests passed — reinstating the permanent freeze. It had been passing on document order. Now every element submitting that parameter, in three states, must carry no value; both placements of QA's mutation now fail.
- [x] 10.6 **MINOR: the lead-time requirement was stale and its scenario untested.** The requirement said the window is derived from lead time; the code does not do that and is right not to (a lead time is a duration — shifting the window by it would skip whole days a site still sells). Requirement corrected to state what is actually guaranteed: a date the lead time leaves nothing bookable on is not **listed**. That guarantee was entirely unguarded — nothing in the window code mentions lead time — so it now has a test, mutation-checked by removing the lead time.
- [x] 10.7 **MINORs about the widened span dissolved with the fix.** The list could contain a date outside the window it names, and `LongestAvailableInWindowMinutes` could come from outside it; both followed from the window read being widened. It no longer is, and a flow test asserts every listed date falls inside the window.
- [x] 10.8 **NIT: the measurement's caveat.** "One query whichever width it is" is right about round trips and QA verified it. Added: the **row volume** returned does scale with the window, which is trivial for a month of bookings but is the part of the figure not to over-read.
