## 1. Core — the assignment

- [x] 1.1 Add the assignment: role slots (a role of count *N* contributes *N*) against candidates, returning a saturating matching or nothing. Keep it a pure function over slots and candidate lists — no store, no clock — so it is testable without a service (design D1).
- [x] 1.2 Augmenting paths (Kuhn's). Deterministic throughout: slots in role order then slot index, candidates in resource-id order, so identical inputs give an identical assignment and a test can assert **which** resources were chosen.
- [x] 1.3 Unit tests from the problem, not the implementation: the case a first-fit walk strands (the resource eligible for both slots must go to the slot with no alternative); a count of 2 against one free candidate; disjoint pools where assignment is trivially the independent choice; an empty pool; and a pool larger than the slots.
- [x] 1.4 **At least one fixture MUST use incomparable capability sets** — `{cert-x}` against `{welsh}`, where neither pool contains the other — and not merely a nested pair. Pools for one resource type are nested exactly when the capability sets are comparable, and `{cert-x}` against `{}` is nested, which is also the shape real configurations usually take ("two therapists, one of whom needs the certificate"). **Greedy assignment is optimal over nested pools**, so a suite built only from the realistic shape would pass a greedy implementation and prove nothing. The defect this change exists to prevent is only visible where the pools cross.
- [x] 1.5 **Mutation-check 1.3 and 1.4 at the threshold, not only the presence.** Three plausible wrong implementations, each to be tried separately: first-fit greedy in role order; greedy sorted most-constrained-first (which defeats a nested fixture and is why 1.4 exists); and dropping the distinctness constraint. Each must turn something red. ⑨-1a shipped a green suite twice; both defects were found this way, not by reading tests.
  - Run at apply, each mutation separately against the real source, then reverted: **first-fit greedy in slot order** → 2 red; **most-constrained-first greedy, recomputed after each choice** → 1 red; **distinctness dropped** → 6 red. The middle result is the one worth recording: most-constrained-first passes `The_resource_eligible_for_both_slots_goes_to_the_slot_with_no_alternative` (the nested shape) and is caught **only** by `Incomparable_pools_defeat_a_greedy_walk_in_either_order`. D5's trap is therefore demonstrated rather than argued — a suite drawn from realistic configurations alone would have shipped that mutation green.

## 2. Core — availability

- [ ] 2.1 Recompose service availability from the assignment: a (start, length) is offered exactly when a saturating assignment of distinct resources exists there (design D2).
- [ ] 2.2 Build each start's runs from the feasible length set rather than by pairwise run intersection. The set need not be one anchored run, so emit several where needed — `{30, 90}` without 60 must be expressible and exact.
- [ ] 2.3 **Differential test against today's answer (design D3).** For services whose roles all name distinct types with count 1, the new composition MUST return exactly what the old intersection returned — same starts, same runs, same order. This is the guarantee that makes the change safe for every service that exists, and it is the one a subtle matching bug would break silently.
- [ ] 2.4 Tests for the new ground: a start two same-type roles cannot both fill is not offered; one role of count 2 with a single free candidate offers nothing; two differently-capable same-type roles with one qualifying resource each *is* offered; a start whose only saturating assignment narrows the lengths offers only the narrower set.
- [ ] 2.5 Confirm the single-role guarantee still holds **for count 1**, and that a single role of count 2 is deliberately not covered by it — the old requirement said "a single-role service", which is now too broad.
- [ ] 2.6 Cost: no additional I/O, and the existing single batched claims read unchanged. Assert the claims read count in a test rather than assuming it.

## 3. Core — placement

- [ ] 3.1 Resolve a saturating assignment instead of walking the cartesian product. **`Booking.Create` throws on duplicate resource ids**, so an assignment that reuses a resource is not a refusal but an unhandled exception — the injectivity must be structural, not a filter after the fact.
- [ ] 3.2 A regression test that a service whose roles share a pool never produces a booking claiming one resource twice, and never *attempts* one.
- [ ] 3.3 Preference becomes booking-level (design D4): seek a saturating assignment including the preferred resource, in whichever slot it fits; fall back to any saturating assignment when none includes it; keep `resource-not-eligible` for a resource eligible for no slot. Note the preferred resource is today ordered to the head of **every** pool containing it, which with overlapping pools makes the duplicate the *first* attempt.
- [ ] 3.4 Rework the all-fail classification so its unit is a saturating assignment rather than a combination, preserving every existing outcome: request-level failures reported as themselves, claimed-but-would-have-refused not counted as a race, and one admitting candidate not making it `conflict` while a slot cannot be filled at all.
- [ ] 3.5 Keep the attempt bound: no growth as the product of pool sizes, the single advisory claims read, and one attempt in flight with locks acquired in deterministic order and released together.
- [ ] 3.6 Tests: a count claims that many distinct resources; a greedy choice that strands a slot still books; a count exceeding the free resources books nothing and claims nothing; every ⑦-2 and ⑨-1 placement scenario unchanged.

## 4. Core — validation, counts and ordering

- [x] 4.1 `ServiceRole.Count` validated as at least 1 and at most the bound, with its own stable failure code carrying the offending role (design D8). Pick the ceiling at apply and record it.
  - **Ceiling chosen: `ServiceRole.MaxCount = 20`**, with its own code `service-role-count-invalid` against `Roles[i].Count`. A service needing more than a couple of dozen of one resource type is a different kind of product — a hall booking rather than an appointment — and twenty slots is trivial for the assignment either way. Nothing in the algorithm depends on the value, so it stays trivially adjustable. The boundary itself is tested in both directions (20 accepted, 21 rejected), because a test asserting only that 21 fails would pass an implementation that rejected 20 too.
- [x] 4.2 Narrow `service-role-duplicate-type` to reject only roles matching on type **and** capabilities, with the message naming the count as the correction (design D5). Update the code's own documentation, which currently states the restriction as type-only and names this change as the one that lifts it.
- [x] 4.3 A count exceeding the eligible pool is **accepted** — a property of the pool, not the service. Assert it, so nobody later "fixes" it into a rejection.
- [x] 4.4 Give the canonical role ordering a total tiebreak — type, then sorted capability keys, then count (design D6). It is total *only because* 4.2 rejects an exact duplicate, so the two move together.
- [x] 4.5 Test round-trip stability: a service with two same-type roles saved and re-read repeatedly returns them in the same order and compares equal. Vary the supplied order, or the test passes on an accident of insertion order.
- [x] 4.6 Confirm no migration is needed — `Count` is an existing column and the role table has no unique index on `(ServiceId, ResourceType)`. **Verify against the model snapshot, do not assume**, and confirm an integration round-trip of two same-type roles.
  - Verified in `UBookItDbContextModelSnapshot.cs`: `ServiceRoleRow` carries `Count` as an existing `int` column and its only index is the non-unique `HasIndex("ServiceId")`. A snapshot states what EF believes rather than what the database will accept, so the round trip is also run for real — `Two_same_type_roles_and_a_count_round_trip_without_a_migration`, re-read from two separate contexts so a stable order cannot come from a change tracker.

## 5. Core — the alignment check

- [x] 5.1 Skip pairings of equal resource ids in `StartAlignment.Compare` (design D7). Without it, a shared resource silences the report through an assignment that can never happen.
- [x] 5.2 Do **not** skip same-type role pairs wholesale. Test the case that distinguishes them: two roles of one type requiring different capabilities, no resource satisfying both, no shared grid — must still be reported.
- [x] 5.3 Test that two roles over a single-resource pool report nothing (nothing to compare), and record that this is ⑨-2a's insufficiency case rather than a misalignment.
  - Mutation-checked both directions: removing the skip turns the shared-resource case red; replacing it with a wholesale same-type-role skip turns *both* new cases red, including the one that exists specifically to catch that over-correction. The skip also had to go **before** the daylight-saving guard — a self-pairing has `gcd(g, g) = g`, so a granularity not dividing an hour would have cleared the whole role pair by that route instead.

## 6. Management API and editor

- [ ] 6.1 Carry `Count` on the management role model in both directions, and stop the editor hard-coding 1.
- [ ] 6.2 Add a count control per requirement row: labelled, associated with its row as the other controls are, defaulting to 1, with server failures rendered against the offending row.
- [ ] 6.3 The editor still must not enforce the duplicate rule (⑨-1 design D1) — it stays a server rule so changing it stays a change in one place.
- [ ] 6.4 Accessibility: the count control is labelled within the row's fieldset, its failure is associated, and every referenced id resolves in the same shadow root. Verify by reading the shadow DOM, not from screenshots.
- [ ] 6.5 Regenerate the client against the running TestSite.

## 7. Delivery API

- [x] 7.1 Publish `Count` on the role read model — always present, 1 where a role needs one resource, never absent-as-default.
- [x] 7.2 Confirm the deterministic role order survives two roles sharing a type, across repeated reads.
- [x] 7.3 Check the change against the standing "eligibility remains derivable from public reads" guarantee: count is not an eligibility input, but publishing it is what stops a consumer computing pools correctly and still misreading what the service needs.
  - Checked and unchanged. Count is **not** an eligibility input — a role of count 3 draws on exactly the pool a role of count 1 does — so publishing it discloses nothing the resource and service reads did not already imply, and the `resource-not-eligible` failure's disclosure argument is untouched. What it fixes is the other half: the pools were already computable, but what the service *required* of them was not.

## 8. Verification

- [ ] 8.1 Full solution build with `--no-incremental`, TestSite stopped first. Only the known NU1903 advisories are acceptable.
- [ ] 8.2 Full unit, integration and client runs green, including every ⑤/⑥/⑦/⑧/⑨-1/⑨-1a scenario unchanged.
- [ ] 8.3 Live backoffice verification: a service with two `therapist` roles differing in capabilities saves; a role with a count of 2 saves and round-trips; two identical roles are rejected with a message naming the count.
- [ ] 8.4 Live: book a service whose roles share a pool and confirm the booking carries two distinct resources; confirm a start only one resource can fill is not offered.
- [ ] 8.5 Stop the TestSite and check port 44348 for orphaned processes.

## 9. Spec hygiene — this change replaces requirements

- [ ] 9.1 **Run the guarantee diff `CLAUDE.md` requires for every MODIFIED requirement.** Unlike ⑨-1a, this change genuinely replaces six requirements across three specs. For each: list every SHALL and every scenario in the current main spec, then decide explicitly whether it is carried forward, deliberately dropped (stated as a removal with its reason), or superseded by a stronger claim. A carried-forward guarantee needs a scenario, not an assumption.
- [ ] 9.2 Pay particular attention to "Booking a service resolves a resource by candidate loop" — 15 guarantee paragraphs and 12 scenarios, most of which are about claims-exclusion and failure classification and must survive the rewrite untouched.
- [ ] 9.3 The outward grep for sibling specs this change falsifies, **before and after** the sync's own edits. One candidate is already known: `service-booking`'s "Every bookable length run is anchored at its step" justifies itself partly by *"intersection across roles relies on both grids being anchored at zero"* — if composition no longer intersects runs across roles, that sentence needs revisiting even though the anchoring property is still required by subset elimination.
- [ ] 9.4 Confirm the two singular-drift items ⑨-1 deferred are discharged here, since this change legitimately modifies both requirements: `service-booking`'s "the service's role" and `services`' row/summary singulars.

## 10. Handover

- [ ] 10.1 Record what ⑨-2a inherits: the assignment's deficient-set witness is what makes both the pool-sufficiency diagnostic and the richer `service-unavailable` message possible, and both should be derived from it rather than recomputed.
- [ ] 10.2 Record whether the count ceiling chosen at 4.1 proved right in practice, and the ⑩ questions this change leaves open — per-role preference, and whether direct resource booking survives.
