## 1. Core — the domain rule

- [x] 1.1 Let `Service.Create` accept several roles; keep `Count == 1` per role.
- [x] 1.2 Reject two roles naming the same resource type with a new stable code, distinct from `type-key-invalid`, naming the duplicated type (design D1). Add the code alongside the existing ones.
- [x] 1.3 Unit tests: several roles of distinct types accepted; same-type roles rejected **whatever their capabilities differ by**; role order not observable; existing single-role scenarios unchanged.
- [x] 1.4 Mutation-check 1.3: make the duplicate check compare type **and** capabilities, and confirm the "differing capabilities do not make two roles distinct" test fails. A rule that only rejects identical roles is the plausible wrong implementation, and it would leave exactly the overlapping-pool case unguarded.

## 2. Core — the length-run invariant

- [x] 2.1 Enforce `Min ≡ 0 (mod Step)` where `LengthRun` is constructed, rather than relying on every call site (design D3). Three algorithms will depend on it once intersection lands.
- [x] 2.2 Unit tests: every run produced by availability projection and duration resolution is anchored; an out-of-phase construction is rejected.
- [x] 2.3 Mutation-check: remove the enforcement and confirm 2.2 fails. Then confirm the existing subset-elimination tests still pass without it — demonstrating that the invariant was previously unguarded, which is the reason for 2.1.

## 3. Core — composite availability

- [x] 3.1 Add run intersection: `lcm` of steps over the overlap of ranges, computed as `a / gcd(a, b) * b` so the intermediate cannot overflow where `a * b` would (design D3).
- [x] 3.2 Compose across roles: intersect the starts, then at each common start take every pairwise run intersection and pass the result through the existing `Collapse` (design D2).
- [x] 3.3 Unit tests from the spec scenarios: a start only one role can fulfil is dropped; `{30,120,30}` ∩ `{20,120,20}` = `{60,120,60}`; disjoint length ranges drop the start entirely; 30 and 45 grids intersect to 90; intersection distributes over each role's union.
- [x] 3.4 Property test: the composite's length set equals `{ d : every role has a candidate offering d }`, computed independently by enumerating each role's lengths. This is the definition the run arithmetic is an optimisation of, and it is the only check that would catch a wrong `lcm`.
- [x] 3.5 Equivalence test: a single-role service's composite availability is **identical** to its union availability. This is the regression gate for every ⑦-2 scenario.
- [x] 3.6 Mutation-check 3.3 and 3.4: replace `lcm` with `max` of the two steps, and separately intersect only the outer bounds ignoring steps. Both are the plausible wrong implementations; a suite that stays green under either is not testing the arithmetic.

## 4. Core — placement

- [x] 4.1 Resolve one candidate per role and build a claim set; place one booking through the existing atomic contract (design D4).
- [x] 4.2 Attempt combinations deterministically; a preferred resource orders only its own role's candidates and is rejected with `resource-not-eligible` when in no role's pool.
- [x] 4.3 Unit tests: one claim per role over one interval; a role with no free candidate places nothing at all; every preference scenario carried forward from the single-role requirement.
- [x] 4.4 Confirm `SqlBookingStore.PlaceAsync` needs no change — it already sorts claim ids and locks each in one transaction. **Verify, do not assume**: read it against the modified atomic-placement requirement before concluding.

## 5. Persistence — proving atomicity rather than inheriting it

- [x] 5.1 Racing integration test: `{A,B}` against `{B,C}` over overlapping intervals, concurrently — exactly one succeeds, the other fails `conflict`. **Design it to fail against a non-atomic store**: confirm it goes red if the per-resource lock is dropped, otherwise it proves nothing (③'s QA found a CRITICAL hole in code that also looked right).
- [x] 5.2 Racing test: disjoint claim sets `{A,B}` and `{C,D}` both succeed — the counterpart that stops 5.1 passing by locking too much.
- [x] 5.3 A failed multi-claim placement leaves no booking row and no claims.
- [x] 5.4 Crossing claim orders do not deadlock.
- [x] 5.5 No schema change and no migration — assert the migration set is untouched.

## 6. Management API

- [ ] 6.1 Preview request takes a list of roles; response carries a chain per role, each identifying its role, in the order supplied.
- [ ] 6.2 Preview accepts duplicate types and reports each role independently (design D5) — an editor fixing that fault needs to see what each role resolves to.
- [ ] 6.3 Reject an empty role list; a malformed key identifies which role it came from.
- [ ] 6.4 Service CRUD accepts and returns several roles; the duplicate-type failure reaches the editor with its stable code.
- [ ] 6.5 Tests: a chain per role; duplicate types previewed but not saved; per-role failure attribution; the authorization guarantee asserted the way this repo asserts it.

## 7. Delivery API

- [ ] 7.1 Service read model publishes every role as a collection, deterministically ordered, including for a single-role service.
- [ ] 7.2 Service placement response carries the resolved resources as a collection (**BREAKING**, unpublished); `POST /bookings` unchanged in route, request, response and semantics.
- [ ] 7.3 Tests: multi-role read; single-role read still returns a collection of one; both resolved resources reported; direct placement byte-for-byte unchanged.

## 8. Backoffice client

- [ ] 8.1 Regenerate the client against the running TestSite — it reads live swagger, so the site must be up with the new contract.
- [ ] 8.2 Requirement rows become add/remove; the last row cannot be removed; count still sent as 1.
- [ ] 8.3 Do **not** pre-filter types another role uses; render the server's duplicate-type failure against the offending row (design D1 — relaxing the rule later must be a server change alone).
- [ ] 8.4 The summary renders a chain per role, each labelled by its type, keeping the not-known, snapshot and stale-token disciplines and the no-availability-vocabulary rule.
- [ ] 8.5 Extend the client test suite for the per-role phrasing, including a role requiring no capabilities and a role whose type matches nothing.
- [ ] 8.6 Accessibility: each requirement row's controls are labelled and its error associated; add/remove are real buttons with sensible focus handling after removal; the summary remains a live region whose referenced ids resolve in its own shadow root.

## 9. Verification

- [ ] 9.1 Full solution build with `--no-incremental`, TestSite stopped first. Only the known NU1903 advisories are acceptable.
- [ ] 9.2 Full unit, integration and client runs green, including every ⑤/⑥/⑦/⑧ scenario unchanged — a single-role service must behave identically by construction.
- [ ] 9.3 Live backoffice verification: build a two-role service, confirm a chain per role; give it two roles of one type and confirm the failure lands on the right row while the preview still reports both.
- [ ] 9.4 Assert the editor's row associations and the summary's live region by reading the shadow DOM, not from screenshots.
- [ ] 9.5 Live end-to-end: book a two-role service and confirm one booking with two claims over one interval.
- [ ] 9.6 Stop the TestSite and check port 44348 for orphaned processes.

## 10. Handover

- [ ] 10.1 Record that ⑨-1a owns the grid-misalignment diagnostic, with the exact condition (`gcd(step₁, step₂)` divides the offset between interval starts) and the reason deferring is safe rather than merely tolerable — ⑧a's D5 means the summary is correctly silent, not falsified.
- [ ] 10.2 Record what ⑨-2 inherits: same-type roles, matching in **both** availability and placement, and `Count > 1`. Note that the overlapping-pool fixtures already exist and their assertions flip from rejection to assignment.
- [ ] 10.3 At spec-sync time, run the guarantee diff `CLAUDE.md` requires for every MODIFIED requirement, and the outward grep for sibling specs this change falsifies. Both have found something on every recent change.
