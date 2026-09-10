## Context

Step 1 of both flows is a GET form carrying a date, a length and — for a service with a
selectable role — a who. Submitting it reloads the page, and step 2 renders the start times for
that one day. The availability read behind it asks for a single day:

```
GetBookableStartsAsync(subject, selectedDate, selectedDate, …)
```

`BookableStart` is `(StartUtc, MinDuration, MaxDuration)` with `Admits(duration)`. So a **range**
read already returns everything this change needs: which days have starts, and which of those
starts fit the length the visitor asked for. Nothing in `UBookIt.Core` has to change.

**This is a usability change, and its verification is not the shape of the last four.** Those
were about making a guarantee true and proving a guard could see it. The failure modes here are a
page that misleads and a page that is slow — neither of which a tripwire catches. What this change
needs instead is a **differential test** (the new path must agree with the old one) and a
**measurement** (the cost must be a number, not an impression). Reaching for the familiar
guard-shaped tooling here would produce a lot of tests about very little.

## Goals / Non-Goals

**Goals:**

- A visitor sees which days are bookable before choosing one.
- The list and the times below it can never disagree about the same day.
- Nothing a site offers becomes unreachable.
- The cost of the wider read is measured and written down.

**Non-Goals:**

- Caching, paging, a calendar grid, or any change to what counts as available. See the proposal.

## Decisions

### D1. One range read replaces the single-day read, and the day's times come out of it

The flow reads the window once and derives both answers:

```
       window read  ──▶  every BookableStart in [from … to]
                              │
              ┌───────────────┴───────────────┐
              ▼                               ▼
    group by local date              filter to SelectedDate
    keep dates with a start          → the start times (step 2)
    admitting the length
    → the date list (step 1)
```

Two reads would be the obvious alternative and are worse than they look: the day's times would
come from one query and the list from another, so a booking landing between them could produce a
page whose list says Tuesday is free and whose times say it is not. One read cannot disagree with
itself.

**The times derived this way SHALL be identical to what the single-day read returns.** That is
the load-bearing claim of this decision and it is a *differential* property, so it gets a
differential test: run both for the same subject, date and length, and compare. A behavioural
test that merely asserts "some times render" would pass while the filter quietly dropped one.

### D2. The window is derived, never a literal 30

Three things bound it, and the guardrail is the one most easily forgotten:

| Bound | Where from | Why it must be honoured |
|:--|:--|:--|
| Earliest | today + the resource's **lead time** | Offering a date the domain will refuse is the current defect, reintroduced. |
| Latest | today + **horizon** | A site's own booking policy. |
| Widest | **`MaxQueryRangeDays`** | A site may set this to 7. A hardcoded 30-day read would then be **rejected on every page load** with `date-range-too-large`, breaking the flow for that site entirely. |

So the window is `[today + lead, min(today + horizon, today + MaxQueryRangeDays − 1, today + 29)]`,
and the ~30 is a *preference* clamped by the other three rather than a constant the code trusts.

**A site with a tight guardrail gets a shorter list, not a broken page.** That is the property
worth testing directly, because it is invisible on a default configuration and catastrophic on a
configured one.

### D3. The list is filtered to the chosen length

A date appears when at least one of its starts `Admits` the chosen duration — the same predicate
step 2 already uses to choose which times to render. Reusing it is the point: two predicates for
"does this fit?" would eventually disagree, and the disagreement would show as a date you can
select and then find empty.

Changing the length reloads and the list changes with it, which the GET form already does.

**When the list shrinks because the length grew, that is explained rather than left to be
inferred.** The package already does this for a single day (`LengthIsTheProblem` produces *"the
longest available that day is …"*), and an unexplained short list is the same failure at window
scale.

### D4. Two controls, two parameters, and a stated precedence

The list and the field both set "which date", and **they cannot share a query parameter**: a form
containing `<input name="date">` and `<input type="radio" name="date">` submits *both* values, and
which one binds is an accident of model binding rather than a decision.

So they are separate parameters, and the rule is written down:

- The **list** submits the existing date parameter.
- The **field** submits its own, and **wins when present and parseable**.

The field wins because typing a date is the more deliberate act: a visitor who selects a radio and
then types is correcting themselves. The reverse rule would silently discard what they typed.

*Alternative considered — one control, the field removed.* Rejected at explore with sign-off: the
default horizon is 90 days and a window is at most 31, so removing the field makes two thirds of
what a site offers unreachable from its own front end. A rendering change must not become a
booking-policy change.

### D5. A date outside the window is shown, and said

Jumping to a date beyond the list leaves no radio checked, and a page whose list shows nothing
selected while its times show 20 October contradicts itself.

So when the selected date is outside the listed window, the step **states which date it is
showing**. The list stays — it is still the answer to "when can I come soon?" — and the statement
resolves the contradiction rather than hiding it.

### D6. An empty window is a different sentence from an empty day

*"No times are available on Tuesday"* and *"no date in the next 30 days has availability for 2
hours"* are different facts and need different wording. The second SHALL name what would change
the answer — a shorter length, or a date further ahead through the field — because a visitor who
is told only "nothing" has no next move.

The distinction matters most when the length is the cause: a window empty for 2 hours may be full
of 30-minute gaps, and saying so is the difference between a dead end and a choice.

### D7. Measure the cost; do not cache in this change

The read goes from one day to as many as thirty. That is the change's one real cost and the honest
thing to do is put a number on it before deciding anything.

**What gets measured:** the wall-clock cost of a first-step render against a resource with a full
year of open hours and a realistic booking density, at the widest window the guardrail allows,
compared with the same render today. **The number goes in the change's tasks**, whatever it is.

A cache is deliberately not here. Availability is the worst thing in this domain to serve stale —
it means offering a slot somebody already took — and the package has no caching anywhere, so the
first one needs an invalidation story of its own. If the measurement says it is needed, that is
the next change, starting from evidence rather than from instinct.

### D8. A new shared partial

The list goes in its own `.cshtml` rather than into `_DateAndLength`, which already renders three
controls and their aria wiring. It joins the published building-block list a theme may call —
which is a contract addition and is declared as one.

## Risks / Trade-offs

**The list and the times disagree.** → They come from one read (D1) and one predicate (D3), so
disagreement requires a bug in the grouping rather than a race. The differential test in D1 is
the guard, and it compares against the code path this change is replacing.

**A site with a tight `MaxQueryRangeDays` gets a broken flow.** → The window is derived from it
(D2). This is the highest-severity failure in the change and the least visible, because the
default configuration never shows it. Tested directly with a configured guardrail, not a default
one.

**Two date controls confuse.** → They answer different questions and are labelled to say so, and
the precedence is defined rather than emergent (D4). The residual risk is presentational and is
the kind of thing worth looking at in a browser rather than asserting in a test.

**Thirty days of projection on every step-1 render.** → Bounded by the existing guardrail,
measured rather than assumed (D7), and no worse per-day than what the flow already does.

**A long list is hard to scan.** → It is at most ~30 rows of a single date each, rendered as the
same grouped-radio pattern the start times already use, so it inherits an accessible shape rather
than inventing one. If it proves unwieldy in the browser that is a finding worth acting on, and it
is a reason to look at it rather than to guess now.

## Migration Plan

None. No schema, no configuration, no contract removal. A site upgrading sees a fuller first step
and the same second step.

## Open Questions

None blocking. One recorded: **whether the window should be configurable.** It is derived from
existing settings today, and adding `UBookIt:AvailableDaysShown` would be a knob whose right value
a site owner cannot reason about better than the code can. Revisit only if the measurement in D7
gives a site a reason to want it smaller.
