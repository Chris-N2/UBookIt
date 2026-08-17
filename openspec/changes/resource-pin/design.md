## Context

Placement resolves an assignment once per attempt:

```csharp
var assignment = preferredResourceId is { } preferred
    ? SlotAssignment.TrySaturateIncluding(ids, preferred) ?? SlotAssignment.TrySaturate(ids).Assignment
    : SlotAssignment.TrySaturate(ids).Assignment;
```

The `??` is the whole behaviour being changed. Everything else — finding an
assignment that contains a named resource, placing it in whichever slot fits,
rejecting an id that is in no pool — already works and is already specified.

Three existing decisions constrain how:

- **⑨-2 design D4 — preference names the booking, not a role.** A resource may be
  eligible for several slots, so "prefer this one" cannot identify which it fills;
  the assignment chooses. A pin inherits that unchanged.
- **⑨-2's all-fail classification** distinguishes a lost race (`conflict`, retry may
  help) from a deterministic refusal (`service-unavailable`, it cannot). Any new
  failure has to say which family it is in, or it pollutes a signal that exists to
  be trusted.
- **⑦-2 design D9 — `resource-not-eligible` is a pool-membership oracle**, harmless
  only while eligibility is derivable from public reads. A pin failure must not
  become a second, finer oracle: "Mary is in the pool but busy" discloses more than
  "Mary is not in the pool", and the two must stay distinguishable for the caller
  without the second leaking a calendar.

## Goals / Non-Goals

**Goals:**

- A booker who names a resource gets that resource or an honest failure.
- The failure is distinguishable from "you named something impossible" and from
  "nothing could be booked at all".
- Retrying is correctly signalled as worthwhile.
- The name of the field matches what it does.

**Non-Goals:** as the proposal states — no availability filtering, no soft
preference kept alongside, no per-role pinning, no change to the ineligible case.

## Decisions

### D1 — Remove the fallback; do not add a flag to select behaviour

The change is deleting `?? SlotAssignment.TrySaturate(ids).Assignment` when a pin is
supplied. A `bool RequirePinned` switching between the two would keep both
behaviours alive and make every caller state which it wanted, with the silent
substitution as the answer for anyone who forgot.

One field, one meaning. If soft preference is ever needed it is a new, differently
named thing — and it will need a consumer to justify it, which is the test the
current one fails.

### D2 — The rename is the point, not decoration

`preferredResourceId` describing a hard requirement would be exactly the drift this
project keeps paying for: a name that documents behaviour the code no longer has.
`pinnedResourceId` says the thing, and "pin" has no other meaning in this domain —
unlike "required", which is already spoken for by a role's required capabilities.

Free now, expensive later: the field is on an unpublished delivery contract, and
CLAUDE.md makes the public surface a compatibility promise only once published.

*Alternative considered — keep the name and document the meaning.* Rejected: the
documentation would live in a spec and the name would live in every consumer's code.

### D3 — A pin that cannot be honoured is transient, and gets its own code

`pinned-resource-unavailable`. It joins the family that invites a retry, alongside
`conflict`, because the pinned resource may be free at another time or may free up.
It must **not** be treated as a deterministic refusal: telling a booker not to
bother because one named person is busy would be false.

Distinct from `conflict`, which says nothing could be booked and would leave the
front end unable to offer the one useful next step — *someone else is free then*.
Distinct from `resource-not-eligible`, which says the named resource could never
fulfil this service at any time, and is the caller's own mistake.

Its status is 400 through the existing catch-all in the failure mapping. That
requirement is not modified: it already routes every code outside the conflict and
not-found families to 400, and a special case would be a second rule.

### D4 — The pin is judged against the *rule-admitting, free* candidates, not the raw pool

A pin can fail for three different reasons and they must not collapse:

```
  pinned id in no candidate pool at all   → resource-not-eligible   (unchanged)
  pinned resource eligible, but no saturating assignment contains it
        because it is claimed, or its own rules refuse this request,
        or claiming it strands another slot                          → pinned-resource-unavailable
  pin honoured, but the placement then loses a race                  → conflict (unchanged)
```

The middle case is judged where the assignment is already judged — over the slot
graph placement is actually working with — so it cannot disagree with what the
attempt would have done. That is ⑧a design D1 restated: a second computation of
"could Mary have been used" would be free to differ from the one that decides.

### D5 — The pin failure is reported directly, never through the all-fail classification

`RuleClassification.AllFail` answers "why did every assignment fail" for a pool. A
pin that cannot be honoured is not a fact about the pool — assignments may exist in
abundance without the pinned resource — so routing it through that classification
would produce `conflict` or `service-unavailable` for a request that has a precise
and different answer.

So the pin is checked before the attempt loop commits to that reasoning, and returns
its own failure. The existing all-fail outcomes are untouched for every unpinned
request, which is what the ⑨-2 suite guards.

### D6 — Availability is not filtered by the pin

Settled with Chris on 2026-08-17: the front-end flow is *pick a time, then pick who*.
A booker therefore chooses from people already free at a chosen instant, and a
"starts at which Mary can be assigned" query has no consumer.

It is a coherent query and would be **additive** if the flow ever inverts to
who-then-when, so nothing here forecloses it. What it would take is stated so a
later change does not have to rediscover it: service bookable-starts are computed
per (start, length) by asking whether a saturating assignment exists, so a pinned
variant asks the same question with `TrySaturateIncluding` — the same seam again.

### D7 — Do not let the pin become a disclosure oracle

`resource-not-eligible` is safe because pool membership is derivable from public
reads. `pinned-resource-unavailable` says something a caller could *also* already
derive — a resource's free time is published — so it discloses nothing new. That is
worth stating because it is the property that could quietly stop being true: if a
future change hides per-resource availability, this code starts leaking a calendar.

## Risks / Trade-offs

- **[Replacing a 150-line, 17-scenario requirement to change one sentence]** → The
  main risk, and unavoidable: an ADDED requirement would contradict the
  fall-through sentence and leave two rules disagreeing. The guarantee diff must be
  run **and read by hand** — the tool compares scenario titles and is blind to a
  changed WHEN, which is how a scoping change slipped past it on the previous
  change. Every one of the 17 scenarios is about locking, exclusion, claims or
  ordering, and exactly two are about preference.

- **[A test suite that only proves the happy path]** → "The pinned resource is used
  when it is free" passes the implementation being removed. The covering test has to
  be the **negative**: pinned, eligible, and unusable — with a perfectly good
  assignment available without it — asserting the failure rather than the
  substitution.

- **[The new code is mistaken for a deterministic refusal]** → It would tell a
  booker not to retry when retrying is exactly what might work. Asserted directly
  against `IsDeterministic`, the same way the previous change asserted its code was
  absent from that whitelist.

- **[Renaming misses a caller]** → The compiler catches every C# site; the delivery
  request model and the OpenAPI document are checked by regenerating the client and
  reading the diff. There is no dynamic access to the field by name.
