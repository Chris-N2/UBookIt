## 1. The window

- [ ] 1.1 Add a pure function computing the window from `today`, the subject's **lead time**, its **horizon**, and `SiteBookingSettings.MaxQueryRangeDays`. Pure so it is testable without a flow, like `BookingFormBuilder.TodayIn` and `LongestAvailableMinutes` already are.
- [ ] 1.2 **The preferred span (~30 days) is a preference, clamped — never a constant the read trusts.** A site may set `MaxQueryRangeDays` to 7; a 30-day read would then be refused with `date-range-too-large` on **every** step-1 render and that site would have no flow at all. This is the highest-severity failure in the change and it is invisible on a default configuration.
- [ ] 1.3 Use the same saturating arithmetic `CalendarBounds.AddDaysSaturating` already uses for `MaxDate` — a large horizon must not throw while rendering a form.
- [ ] 1.4 The window's start respects lead time. Listing a date the domain will refuse is the guessing game this change removes, moved one step earlier.

## 2. The read

- [ ] 2.1 Widen both flows' `GetBookableStartsAsync` call from `(selectedDate, selectedDate)` to the window. **One read, not two** — the date list and the selected day's times both come out of it.
- [ ] 2.2 Derive the selected day's times by filtering that result to the selected date, in the site time zone. **Group by LOCAL date, not by UTC date** — a start at 23:30 UTC is the next day in `Europe/London` in summer, and grouping in the wrong zone would put a date in the list that the times below disagree with.
- [ ] 2.3 The service flow passes the same `choice.Chosen` it passes today, so the list narrows to the chosen who exactly as the times do.
- [ ] 2.4 A date is in the list when at least one of its starts `Admits` the chosen duration — **the same predicate step 2 uses**, not a second one. Two answers to "does this fit?" would eventually disagree, and the disagreement would show as a date you can pick and then find empty.

## 3. The view model

- [ ] 3.1 Add the window's dates to `IBookingFormView` as a list of values — the date, and whether it is the selected one. Not pre-rendered markup: `theming` makes the model a published contract and a theme must be able to render it its own way.
- [ ] 3.2 **BREAKING — published contract.** `IBookingFormView` gains members; declare it, as `privacy-notice` did. A theme consumes the model rather than implementing it, so the practical impact is nil, but the promise is about the type.
- [ ] 3.3 Carry whether the selected date is **outside** the window, so the view can state which date it is showing without recomputing the window.
- [ ] 3.4 Carry enough to explain an empty window: that it is empty, and the longest length that *would* find something. The single-day case already does this via `LongestAvailableMinutes`; this is the same idea at window scale.

## 4. Rendering

- [ ] 4.1 New shared partial for the date list. Not into `_DateAndLength`, which already renders three controls and their aria wiring.
- [ ] 4.2 A `fieldset` with a `legend`, radios with associated labels — **the same shape `_Times` uses**, so the page has one idiom for "choose one of these" rather than two.
- [ ] 4.3 The list submits the existing date query parameter.
- [ ] 4.4 The "another date" field submits a **different** parameter and **wins when present and parseable**. Two controls on one parameter means the browser sends both values and binding picks arbitrarily — this is the concrete form of "two ways to do one thing".
- [ ] 4.5 Label both so they read as different questions — "when can I come soon?" versus "I want this specific date" — rather than as two ways to do the same job.
- [ ] 4.6 When the selected date is outside the window, state which date is being shown. Otherwise the page shows a list with nothing selected beside times for a date the list does not contain.
- [ ] 4.7 The empty-window state: say no date in the window is available at that length, and name what would change it. **Distinct wording from the empty-day state** — the two are different facts and a reader must be able to tell which they are being told.
- [ ] 4.8 Add the new class(es) to the published vocabulary deliberately — `default-frontend` makes the class names a stable contract, so this is a contract addition and both the vocabulary list and the block list need it.
- [ ] 4.9 Add the partial to `UBookItThemeContract.SharedPartials` and to the theming guide's table.
- [ ] 4.10 **No JavaScript.** Standing invariant. And no colour-only distinction: a date's state is conveyed in text.

## 5. Verification

- [ ] 5.1 **The differential test, and it is the important one.** For the same subject, date and length, the times derived from the window equal the times a single-date read returns. Widening the read must change how much is asked for and nothing about what is answered. A test asserting only "some times render" would pass while the filter dropped one.
- [ ] 5.2 Window derivation, tested against **configured** bounds and not just defaults: a `MaxQueryRangeDays` of 7 shortens the list and the read still succeeds; a short horizon shortens it; a lead time moves the start. **The default configuration cannot show any of these**, which is exactly why they are the tests that matter.
- [ ] 5.3 Length coupling: a date with only 30-minute gaps is absent at 2 hours and present at 30 minutes. Both directions, or the filter could be inverted and still pass one of them.
- [ ] 5.4 **Local-date grouping**, with a fixture whose start is on the far side of midnight in the site zone. A UTC-grouped implementation passes every test with a UTC site and fails only for the sites that actually have this problem.
- [ ] 5.5 The two controls submit different parameters, and the precedence holds when both are present.
- [ ] 5.6 A date beyond the window is reachable and renders its times.
- [ ] 5.7 The outside-window statement appears exactly when the selected date is not listed.
- [ ] 5.8 Empty-window state, and that its wording differs from the empty-day state.
- [ ] 5.9 Rendering tests for every state the model can now express — listed/empty, inside/outside window, length-limited. `default-frontend` requires a view render every state its model can express, and this model gains several.
- [ ] 5.10 **Mutation-check the ones that could be vacuous**, one at a time and asserting the mutation applied: invert the `Admits` filter; group by UTC date; drop the `MaxQueryRangeDays` clamp; make the field lose to the list. Each must fail something. **Verify each restore recompiled** — restoring a file with an older timestamp leaves the mutant binary in place and the re-run green.

## 6. The measurement

- [ ] 6.1 **Measure the step-1 render cost** — today's single-day read against the window read — on a resource with a full year of open hours and a realistic booking density, at the widest window the guardrail allows.
- [ ] 6.2 **Write the number into this task list**, whatever it is. A measurement nobody records is an impression.
- [ ] 6.3 If it is bad enough to want a cache, that is the **next change**, starting from this number. Do not add caching here: availability is the worst thing in this domain to serve stale, and the package has no caching anywhere to model an invalidation story on.

## 7. Look at it in a browser

- [ ] 7.1 **Render both flows and actually look.** This change is judged by whether a page is clearer, and no assertion in the suite can tell me that. Two of this project's front-end changes had defects only the browser found.
- [ ] 7.2 Check the long case — a window where nearly every date is available — reads as a usable list rather than a wall.
- [ ] 7.3 Check it with **no author stylesheet at all**, per the standing invariant: the flow must stay operable, and a 30-item radio group is where that is most likely to strain.
- [ ] 7.4 Check keyboard order through list → length → who → submit, and that the list's legend says what the group is for.

## 8. Modified requirements — the guarantee diff

- [ ] 8.1 `default-frontend` → *No-JavaScript single-resource booking flow*. Carried forward: the whole body and all three scenarios. Added: one paragraph and one scenario. **Dropped: nothing** — verified mechanically (3 → 4 scenarios, 3 → 4 SHALLs, 0 dropped), not by eye.
- [ ] 8.2 `default-frontend` → *No-JavaScript service booking flow*. Carried forward: the whole body and all four scenarios. Added: one paragraph and one scenario. **Dropped: nothing** — verified mechanically (4 → 5 scenarios, 5 SHALLs unchanged, 0 dropped).
- [ ] 8.3 The cross-flow behaviour is in **ADDED** requirements rather than duplicated into both flows — "one bar, stated once", the principle `theming` already uses. Confirm at apply time that neither flow requirement needs it restated to stay comprehensible.
- [ ] 8.4 `ChangeDeltaIntegrityTests` is the authority on delta correctness, not `openspec validate --strict`.

## 9. Sweep — sibling specs this change falsifies

- [ ] 9.1 `default-frontend` → *Accessible, semantic markup (WCAG 2.2 AA)*. A third grouped control joins the step. Satisfied by using the existing pattern — confirm rather than assume, and check nothing there enumerates the step's controls.
- [ ] 9.2 `default-frontend` → *Rendered markup resolves its own references* and *A view renders every state its model can express*. Both now cover more states.
- [ ] 9.3 `default-frontend` → *The styling contract is a stable class vocabulary*. New classes are contract additions.
- [ ] 9.4 `default-frontend` → *Flow state is carried in the URL* and *A site may enter the flow at any point*. A second date parameter is new flow state — does either constrain what the URL may carry?
- [ ] 9.5 `availability` — nothing should change, but confirm no requirement constrains who may issue a ranged read or how wide.
- [ ] 9.6 `delivery-api` — untouched. Confirm the ranged endpoint's contract is unaffected by a Razor consumer using it differently.
- [ ] 9.7 `theming` → *The building blocks a theme may call are a promised contract*. A new partial joins the list.
- [ ] 9.8 `service-booking` — the service flow's availability semantics, particularly around the who choice narrowing the pool.
- [ ] 9.9 **Find the one not already on this list.** The capability this change makes a difference to is the one most certain to be affected, and it is not always the one being modified.
