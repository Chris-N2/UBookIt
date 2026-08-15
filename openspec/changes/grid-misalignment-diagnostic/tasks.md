## 1. Core — the alignment arithmetic

- [ ] 1.1 Add the pairwise grid test: two windows meet iff `gcd(step₁, step₂)` divides the offset between their starts (design D3). Keep it a pure function over two (start, step) pairs, so it is testable without a service, a store, or a clock.
- [ ] 1.2 Unit tests from the arithmetic, not from the implementation: the 09:00/30 against 09:15/20 case that never meets; 09:00/30 against 09:30/20 that does; equal steps with an offset that is and is not a multiple; coprime steps, which always meet; a zero offset, which always meets.
- [ ] 1.3 **Differential test against the real projector.** For a sweep of window/step pairs, compare the test's verdict with whether `ProjectBookableStarts` actually yields a shared start over a range. Every "permanently misaligned" verdict MUST have no shared start; a "may meet" verdict may legitimately have none. This is the guard that stops the arithmetic drifting from the grid rule it models — a predicate that merely looks right has been wrong here before.
- [ ] 1.4 Mutation-check 1.2 and 1.3: replace `gcd` with `min` of the two steps, and separately drop the divisibility test for equality of steps. Both are the plausible wrong implementations.

## 2. Core — the service-level check

- [ ] 2.1 Expose the check over a service's roles, projecting candidates from the same resolution the booking path uses — never a second eligibility filter (⑧a design D1).
- [ ] 2.2 Report a pair as misaligned only when **no** candidate of one role shares a grid with **any** candidate of the other, across every local date both are open (design D4, D5). Compare windows on the same local date in the site zone.
- [ ] 2.3 Apply pairwise across three or more roles, and return the first clashing pair with both resources, their window starts and their granularities.
- [ ] 2.4 Unit tests: a single awkward resource in a large pool does not fire; a pool where every pairing fails does; a clash on one weekday only does **not** fire; three roles where only two clash reports that pair; a single-role service never fires.
- [ ] 2.5 **Fixtures must vary what the scenarios vary.** Use pools of more than one resource, granularities that are neither equal nor coprime, and at least one case where the aligning candidate is not the lowest-id one — otherwise the "one aligning candidate is enough" test can pass on a degenerate pool.
- [ ] 2.6 Prove the claim is one-directional (design D1): a service the check clears is not thereby bookable. Assert that a cleared service with no free time still returns no starts, so nothing reads silence as a promise.
- [ ] 2.7 Prove bookings do not change the answer: check a configuration, fill a resource's calendar, check again, assert identical findings.
- [ ] 2.8 Confirm nothing else changed: resolution, availability, placement and `Service.Create` behave as before for a misaligned service. **Verify, do not assume** — assert the save succeeds and availability still returns its empty result.

## 3. Management API

- [ ] 3.1 Carry the finding on the preview response as its own member, never inside a role's chain (design D6).
- [ ] 3.2 Present it only when misaligned; absence is not a positive claim.
- [ ] 3.3 Tests: a misaligned configuration carries chains **and** the finding; an alignable one carries chains only; a single-role configuration never carries it; a misaligned configuration is previewed rather than rejected; the chains are byte-for-byte what they would have been without the finding.
- [ ] 3.4 Assert the authorization guarantee the way this repo asserts it, and that the endpoint stays read-only.

## 4. Backoffice client

- [ ] 4.1 Regenerate the client against the running TestSite — it reads live swagger, so the site must be up with the new contract.
- [ ] 4.2 Render the report as a statement separate from the per-role chains, naming both roles, both resources, and their opening times and step sizes.
- [ ] 4.3 Keep the not-known discipline: too incomplete to resolve, or a failed request, says nothing rather than implying either answer.
- [ ] 4.4 Keep the snapshot discipline: the wording comes from state captured with the response, never from the live form.
- [ ] 4.5 Extend the client test suite over the pure phrasing function — including absence (says nothing), presence, and a not-known state. The wording is where the truth claims live, and a false sentence here is exactly the ⑧a defect.
- [ ] 4.6 Wording rule: the report may say that starts can never coincide; it may **not** say the service is available, free or bookable, and its absence may not be phrased as reassurance. Extend the existing forbidden-vocabulary test to cover the new strings.
- [ ] 4.7 Accessibility: the report is associated with the form the way the summary is, announced when it appears, and its referenced ids resolve within the same shadow root.

## 5. Verification

- [ ] 5.1 Full solution build with `--no-incremental`, TestSite stopped first. Only the known NU1903 advisories are acceptable.
- [ ] 5.2 Full unit, integration and client runs green, including every ⑤/⑥/⑦/⑧/⑨-1 scenario unchanged.
- [ ] 5.3 Live backoffice verification: configure the 09:00/30 against 09:15/20 case and confirm the report appears with both resources named; then shift one opening time so the grids align and confirm it disappears without saving.
- [ ] 5.4 Confirm the misaligned service still saves, and that the resolution chains beside the report are unchanged.
- [ ] 5.5 Assert the report's association and live-region behaviour by reading the shadow DOM, not from screenshots.
- [ ] 5.6 Stop the TestSite and check port 44348 for orphaned processes.

## 6. Handover

- [ ] 6.1 Record what ⑨-2 inherits: same-type roles draw from one pool, so two roles of one type share a grid trivially and this check must not fire on them; matching changes which pairings matter.
- [ ] 6.2 Record the deferred delivery-API reason code and that it belongs with ⑩'s service-booking UI, so an empty availability response can explain itself to a booker.
- [ ] 6.3 At spec-sync time, run the guarantee diff `CLAUDE.md` requires for every MODIFIED requirement (this change has none, which is itself worth confirming rather than assuming), and the outward grep for sibling specs this change falsifies. That grep has now found something on five consecutive changes — including, last time, a hard contradiction found only **after** the sync's own edits.
