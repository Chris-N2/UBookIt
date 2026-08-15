## 1. Core — the alignment arithmetic

- [x] 1.1 Add the pairwise grid test: two windows meet iff `gcd(step₁, step₂)` divides the offset between their starts (design D3). Keep it a pure function over two (start, step) pairs, so it is testable without a service, a store, or a clock.
- [x] 1.2 Unit tests from the arithmetic, not from the implementation: the 09:00/30 against 09:15/20 case that never meets; 09:00/30 against 09:30/20 that does; equal steps with an offset that is and is not a multiple; coprime steps, which always meet; a zero offset, which always meets.
- [x] 1.3 **Differential test against the real projector.** For a sweep of window/step pairs, compare the test's verdict with whether `ProjectBookableStarts` actually yields a shared start over a range. Every "permanently misaligned" verdict MUST have no shared start; a "may meet" verdict may legitimately have none. This is the guard that stops the arithmetic drifting from the grid rule it models — a predicate that merely looks right has been wrong here before.
- [x] 1.4 Mutation-check 1.2 and 1.3: replace `gcd` with `min` of the two steps, and separately drop the divisibility test for equality of steps. Both are the plausible wrong implementations.
  - `gcd` → `min`: 5 failures, including the differential sweep — the mutation invents *shared* starts, which is the direction 1.3 asserts.
  - equal steps short-circuited to "meets": 3 failures, all from 1.2. The sweep cannot see this one and is not meant to: it only loses accusations, and 1.3 asserts nothing about a "may meet" verdict. That is why 1.2 states the equal-step case in both directions rather than leaving it to the sweep.

## 2. Core — the service-level check

- [x] 2.1 Expose the check over a service's roles, projecting candidates from the same resolution the booking path uses — never a second eligibility filter (⑧a design D1).
- [x] 2.2 Report a pair as misaligned only when **no** candidate of one role shares a grid with **any** candidate of the other, across every local date both are open (design D4, D5). Compare windows on the same local date in the site zone.
- [x] 2.3 Apply pairwise across three or more roles, and return the first clashing pair with both resources, their window starts and their granularities.
- [x] 2.4 Unit tests: a single awkward resource in a large pool does not fire; a pool where every pairing fails does; a clash on one weekday only does **not** fire; three roles where only two clash reports that pair; a single-role service never fires.
- [x] 2.5 **Fixtures must vary what the scenarios vary.** Use pools of more than one resource, granularities that are neither equal nor coprime, and at least one case where the aligning candidate is not the lowest-id one — otherwise the "one aligning candidate is enough" test can pass on a degenerate pool.
- [x] 2.6 Prove the claim is one-directional (design D1): a service the check clears is not thereby bookable. Assert that a cleared service with no free time still returns no starts, so nothing reads silence as a promise.
- [x] 2.7 Prove bookings do not change the answer: check a configuration, fill a resource's calendar, check again, assert identical findings.
- [x] 2.8 Confirm nothing else changed: resolution, availability, placement and `Service.Create` behave as before for a misaligned service. **Verify, do not assume** — assert the save succeeds and availability still returns its empty result.

## 3. Management API

- [x] 3.1 Carry the finding on the preview response as its own member, never inside a role's chain (design D6).
- [x] 3.2 Present it only when misaligned; absence is not a positive claim.
- [x] 3.3 Tests: a misaligned configuration carries chains **and** the finding; an alignable one carries chains only; a single-role configuration never carries it; a misaligned configuration is previewed rather than rejected; the chains are byte-for-byte what they would have been without the finding.
- [x] 3.4 Assert the authorization guarantee the way this repo asserts it, and that the endpoint stays read-only.

## 4. Backoffice client

- [x] 4.1 Regenerate the client against the running TestSite — it reads live swagger, so the site must be up with the new contract.
- [x] 4.2 Render the report as a statement separate from the per-role chains, naming both roles, both resources, and their opening times and step sizes.
- [x] 4.3 Keep the not-known discipline: too incomplete to resolve, or a failed request, says nothing rather than implying either answer.
- [x] 4.4 Keep the snapshot discipline: the wording comes from state captured with the response, never from the live form.
- [x] 4.5 Extend the client test suite over the pure phrasing function — including absence (says nothing), presence, and a not-known state. The wording is where the truth claims live, and a false sentence here is exactly the ⑧a defect.
- [x] 4.6 Wording rule: the report may say that starts can never coincide; it may **not** say the service is available, free or bookable, and its absence may not be phrased as reassurance. Extend the existing forbidden-vocabulary test to cover the new strings.
- [x] 4.7 Accessibility: the report is associated with the form the way the summary is, announced when it appears, and its referenced ids resolve within the same shadow root.

## 5. Verification

- [x] 5.1 Full solution build with `--no-incremental`, TestSite stopped first. Only the known NU1903 advisories are acceptable.
- [x] 5.2 Full unit, integration and client runs green, including every ⑤/⑥/⑦/⑧/⑨-1 scenario unchanged.
- [x] 5.3 Live backoffice verification: configure the 09:00/30 against 09:15/20 case and confirm the report appears with both resources named; then shift one opening time so the grids align and confirm it disappears without saving.
- [x] 5.4 Confirm the misaligned service still saves, and that the resolution chains beside the report are unchanged.
- [x] 5.5 Assert the report's association and live-region behaviour by reading the shadow DOM, not from screenshots.
- [x] 5.6 Stop the TestSite and check port 44348 for orphaned processes.

## 5a. QA remediation (round 1, 2026-08-15)

- [x] 5a.1 **CRITICAL — a DST transition between two windows produced a false accusation.** Design D5's "both windows shift together" holds only for windows on the same side of a transition. Fixed by reporting only when `gcd` divides an hour, which is exactly when the wall-clock offset gives the real verdict; otherwise silent, and one unsettleable pairing clears the role pair. D5 amended with the repro, the rejected alternative, and the Lord Howe residue as a recorded limit.
- [x] 5a.2 **MAJOR — a booking predating an opening-hours change leaves a start off the current grid.** Resolved as Chris chose: the report still fires, because it describes the configuration, which is broken once that booking clears. D2 and the service-booking delta amended to state the premise the superset argument actually needs, plus a scenario pinning the behaviour.
- [x] 5a.3 Covering tests for both, and `proposal.md`'s claim of a DST test made true rather than deleted. Mutation-checked the guard: disabling it fails 3 tests including the CRITICAL repro.
- [x] 5a.4 Re-run: full build clean, full unit/integration/client suites green.

## 5b. QA remediation (round 2, 2026-08-15)

- [x] 5b.1 **MAJOR — a test that could not fail.** `The_wall_clock_offset_is_exact_whenever_the_shared_step_divides_an_hour` reimplemented `gcd` locally and asserted an arithmetic identity, never touching the check; QA showed that narrowing the guard's divisor to 30 minutes left all 529 tests green while silencing the very pairs the dead test's failure message claimed to guard. Replaced with a theory over the five gcds that divide an hour but not half of one (4, 12, 20, 60), asserted through the real check. That mutation now fails all five.
- [x] 5b.2 **MINOR — the recorded residue was wrong in both directions.** It named 45 minutes, which the guard already silences, and omitted 20, which is two resources on the same ordinary grid. Corrected to {4, 12, 20, 60}, with the empirical finding that Lord Howe Island is the only zone still carrying a sub-hour delta, and the standard-offset variant noted.
- [x] 5b.3 Recorded the *better* alternative QA identified — require the gcd to divide every `DaylightDelta` of the site zone, which is zone-aware but date-free — so D5's rejection does not stand against a weaker alternative than the best available.
- [x] 5b.4 NIT: signposted at the delta's "permanently disjoint otherwise" SHALL that the daylight-saving condition narrows when it may be reported.
- [x] 5b.5 QA verified the three live-DOM scenarios itself rather than accept the applying session's pass — 22/22 spec criteria, APPROVE. It read `.alignment` and `.resolution` as sibling children of the form (neither containing the other), audited all 10 id references in the shadow root (0 dangling), and watched the live region keep its role and label while its content went from four lines to zero. For the disappears-without-saving scenario it read an existing resource's real configuration first, so the aligning case was predictable rather than hoped for, and excluded both confounds: the chains stayed populated (so the request succeeded rather than failed) and the gcd divided an hour (so the DST guard was not what silenced it). Fixture restored, nothing saved.

## 6. Handover

- [x] 6.1 Record what ⑨-2 inherits: same-type roles draw from one pool, so two roles of one type share a grid trivially and this check must not fire on them; matching changes which pairings matter.
- [x] 6.2 Record the deferred delivery-API reason code and that it belongs with ⑩'s service-booking UI, so an empty availability response can explain itself to a booker.
- [x] 6.3 At spec-sync time, run the guarantee diff `CLAUDE.md` requires for every MODIFIED requirement (this change has none, which is itself worth confirming rather than assuming), and the outward grep for sibling specs this change falsifies. That grep has now found something on five consecutive changes — including, last time, a hard contradiction found only **after** the sync's own edits.
  - **No MODIFIED requirements — confirmed, not assumed.** All three deltas carry only `## ADDED Requirements`. The sync is provably append-only: 205 insertions, 0 deletions, and no existing requirement in any main spec was touched. (The guarantee diff was nonetheless exercised three times *within* this change, on the service-booking requirement's own body as QA remediation amended it; clean each time — 10 scenarios / 17 SHALLs preserved.)
  - **Outward grep, run before AND after the sync's own edits.** Nothing falsified either time — the streak ends at five. The one candidate, `openspec/specs/services/spec.md:315` ("the chains say nothing about … whether the roles' start grids ever coincide"), remains **true**: the chains still say nothing about it, and that requirement's own title scopes "the report" to the resolution chain. Left alone deliberately, on the ⑨-1 precedent: replacing an eight-paragraph requirement wholesale to disambiguate a noun phrase risks dropping a scenario for no behavioural gain. Recorded as a standing drift item.
  - **Confirmed rather than assumed to be consistent**: `bookings` rule 7 (placement aligns to the coalesced open window) *supports* the new requirement's premise; `availability` lines 60/77 anchor starts at free-interval starts, which are window starts or booking ends, and the one exception — a booking predating an opening-hours change — is exactly what this change's new clause states explicitly. `preview`/`chains` appear in no other spec.
  - **Observation for ⑨-2, not a defect.** Placement aligns to the **coalesced** open window; this check computes over **configured** windows. Where two same-day windows touch, the configured set offers an anchor the coalesced grid does not — which can only add candidate offsets, so it can only make the check stay *silent*. That is a false negative, which one-directionality explicitly tolerates, and it cannot produce a false report. Anyone "fixing" it must not do so in the direction that breaks one-directionality.
