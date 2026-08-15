## Why

Change ⑨-1 made a service's availability the **intersection** of its roles'
start grids, and in doing so created a way for a perfectly valid configuration
to be **permanently unbookable with nothing saying why**. Start times advance in
granularity steps from each resource's open window, so two roles whose resources
open at different times with different granularities can miss each other on
every day, forever:

```
room       opens 09:00, granularity 30  →  09:00  09:30  10:00  …
therapist  opens 09:15, granularity 20  →  09:15  09:35  09:55  …

30k = 15 + 20m  ⇒  30k − 20m = 15
LHS is divisible by gcd(30, 20) = 10;  15 is not  ⇒  no solution, ever
```

Both roles resolve healthily. Every resource has the right type, the right
capabilities and a duration range that fits. The configuration summary says so,
truthfully. The service simply never has a bookable start, and today the only
symptom is an empty availability response that looks exactly like a fully booked
week.

This was deferred out of ⑨-1 deliberately and is now live rather than
hypothetical. It was safe to defer for one specific reason worth restating,
because it also constrains this change: ⑧a's design D5 means the configuration
summary asserts nothing about opening hours or bookable times, so it is
**correctly silent** here rather than falsified. What is missing is an
explanation, not a correction.

## What Changes

- **Core gains a start-alignment check.** Given a service's roles, it reports
  whether the roles' start grids can coincide **at all**. The test is exact and
  cheap: two arithmetic grids of steps `s₁`, `s₂` anchored at window starts `a₁`,
  `a₂` share an instant iff `gcd(s₁, s₂)` divides `(a₁ − a₂)`.

- **The check answers only "never".** It reports impossibility or it says
  nothing. It never reports that a service *is* bookable, because that would
  additionally require free time, lead time, horizon and a length that fits —
  none of which it evaluates. A negative claim is safe where the positive one
  would over-claim exactly as ⑧a's D5 forbids.

- **The backoffice services editor reports it**, as a statement separate from
  the resolution chains rather than folded into them. The chains keep their
  meaning and their silence about opening hours; this is a different sentence
  about a different thing.

- **The report names the clash**: which two roles, which two resources, and the
  opening times and granularities that cannot meet — because that is what an
  editor acts on, and the fix is to edit a *resource*, not the service.

- **Nothing is rejected.** A service whose roles cannot currently align is still
  saved, still valid, and still resolves. Misalignment is a property of two
  resources' opening hours, not of the service: adding one resource, or shifting
  one opening time by five minutes, fixes it without the service changing at all.

- No schema change, no migration, and **no delivery-API change**. The
  service-oriented front end is `⑩`; until it exists there is no consumer for a
  reason code on an empty availability response, and adding one now would be
  contract surface with nothing behind it.

## Capabilities

### New Capabilities

None. The diagnostic extends behaviour existing specs already own.

### Modified Capabilities

Three specs gain a delta, and every operation in all three is an **ADDED**
requirement — no existing requirement is replaced. That is not a technicality:
this change adds a statement beside the existing ones and alters none of them,
so rewriting a requirement wholesale would risk dropping a guarantee for no gain.

- `service-booking`: Core exposes the start-alignment check over a service's
  roles, stated as a one-directional claim — it may report that no start can
  ever exist, and may never report that one does.
- `resource-management`: the configuration preview endpoint carries the
  alignment finding alongside the per-role chains, as a distinct member.
- `services`: the editor reports a permanent misalignment, separately from the
  resolution chains, naming the roles and resources responsible; and saving is
  explicitly unaffected.

## Non-goals

- **"No common start in the range you asked about."** A service whose grids
  *can* align may still have no bookable start next week because everything is
  booked. That is what an empty availability response already says, and
  conflating the two would cry wolf on a busy week — the diagnostic would stop
  being believed exactly when the structural case appeared.

- **Rejecting or blocking a save.** Explicitly out, on the same reasoning as
  ⑨-1's design: refusing to save would block a configuration that is not wrong.

- **A delivery-API reason code.** Deferred to `⑩` with the service-booking UI
  that would consume it.

- **Diagnosing anything else that empties availability.** Lead time, horizon,
  and exception dates can each empty a range. They are not permanent properties
  of the configuration, and each would need its own wording; this change is
  about the failure that no existing surface can explain.

- **Suggesting a fix.** Naming the clash is the deliverable. Computing the
  nearest opening time that would align is a larger question — it depends which
  resource the editor is willing to move — and belongs with a real editor.

## Impact

**Core** — a pure alignment computation over the candidates' open windows and
granularities, and a service-level entry point that projects it from the same
resolution the booking path uses.

**Backoffice** — an additional member on the configuration preview response, and
a distinct region in the services editor rendering it. The existing resolution
summary is untouched in meaning; the new statement sits beside it.

**Delivery API** — unchanged.

**Persistence** — unchanged. No schema change, no migration.

**Tests** — the arithmetic against enumerated grids over real open hours, the
DST case where a day's window shifts, the one-directional property (a service
the check clears may still be unbookable for ordinary reasons), and the
guarantee that a misaligned service still saves and still resolves.
