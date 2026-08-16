## 1. Core — the witness

- [x] 1.1 Change `SlotAssignment.TrySaturate` to return a deficient set rather than a bare null when no saturating assignment exists. `SlotAssignment` is `internal`, so this is not a public break — but every call site must be revisited, not just made to compile.
- [x] 1.2 Build the witness from the **failed augmenting walk** (design D4): when a slot cannot be filled, the resources its walk reached are the deficient set's neighbourhood, and the slots matched to them plus the unfilled slot are the set. No subset enumeration — that is exponential and unnecessary.
- [x] 1.3 Assert the witness is genuinely deficient: `|neighbourhood| < |slots|`, and short by exactly one where such a set exists. A witness that merely names "everything" would satisfy Hall's inequality trivially and tell an editor nothing.
- [x] 1.4 Collapse slots to roles at the Core boundary (design D1), and test the collapse on both shapes: a **count-based** deficiency (one role of count 3, two eligible) and a **same-type-roles** deficiency (two roles, one shared resource). They exercise different halves of the expansion.
- [x] 1.5 Determinism: the same input yields the same deficient set. Assert it, as ⑨-2 asserts its assignment order.
- [x] 1.6 **Mutation-check the witness, not just its presence.** Three wrong implementations, each tried separately: reporting *all* roles rather than the deficient ones; reporting the neighbourhood as the union of every role's pool rather than the reachable set; and reporting a set that is short by more than one where a tight set exists. Each must turn something red. A test that only asserts "a witness was returned" passes all three.

## 2. Core — the configuration-time check

- [x] 2.1 Add the sufficiency check over resolved `RoleCandidates`, mirroring the shape `StartAlignment.FindMisalignment` established — it takes the pools the booking path acts on, never a second eligibility filter of its own (⑧a design D1).
- [x] 2.2 Compute over **eligibility alone**; do not read the booking store (design D3). Assert it directly: the same configuration must report identically before and after a booking fills every eligible resource's calendar. ⑨-1a's equivalent test is the model.
- [x] 2.3 One-directional: report only insufficiency, never sufficiency. Assert that a sufficient configuration returns nothing at all rather than a positive finding or a zero-shortfall record.
- [x] 2.4 **The fixture trap, and it is the direct analogue of ⑨-2's greedy trap.** A check implemented as "is any role's count greater than its own pool size" passes every fixture where the pools are disjoint, and that is the shape most test data takes. **At least one fixture MUST be a case where every role alone has enough candidates and the roles together do not** — two roles of count 2 over three shared resources, or two roles each with two candidates that are the same two. Without it the suite cannot distinguish an assignment from a per-role count comparison.
- [x] 2.5 Test that sufficiency is not bookability: a configuration whose pools are sufficient but whose resources are never open together reports nothing, and still has no bookable start.

## 3. Core — the placement message

- [x] 3.1 Use the deficient set from the assignment the placement actually ran to build the `service-unavailable` message. Not a second computation (design D2) — the message must describe the failure that occurred.
- [x] 3.2 The **code** is unchanged, and so is its 400 mapping. Assert a consumer matching on the code is unaffected; codes are contract, messages are not.
- [x] 3.3 The message describes that instant, not the configuration. Test that a structurally sufficient service failing at a busy instant does **not** claim it can never be fulfilled.
- [x] 3.4 Confirm every existing all-fail classification outcome is unchanged — `conflict` vs `service-unavailable`, the request-level failures reported as themselves, the transient-refusal rule. This change touches the message only, and the ⑨-2 suite is the guard.
- [x] 3.5 **Structural test that both callers share one implementation** (design D2): break `SlotAssignment`'s deficiency detection and confirm **both** the configuration check and the placement message change. If only one moves, a second implementation has crept in — which is the fault ⑧a D1 and ⑨-1's classifier seam exist to prevent, and no ordinary test detects it.

## 4. Management API and the preview endpoint

- [x] 4.1 Carry the sufficiency finding as its own member of the preview response, beside the chains and the misalignment report — not folded into a chain, because the finding belongs to a set of roles and to none of them individually.
- [x] 4.2 Absent or explicitly empty when sufficient. Never a positive statement, and never a count a reader could take for one.
- [x] 4.3 The chains are unchanged by the finding: two roles sharing a pool still each report their own eligible count, and the joint claim lives only in the new member. Assert both in one test, or the two can drift apart unnoticed.
- [x] 4.4 An insufficient configuration still previews with 200 — the endpoint reports, never rejects, exactly as it does not reject duplicate types.
- [x] 4.5 Authorization and the endpoint's existing behaviour for unresolvable configurations are untouched.
- [x] 4.6 Regenerate the client against the running TestSite, and check the diff for unrelated drift.

## 5. Editor — the sufficiency report

- [x] 5.1 Render the finding at **form level**, beside the resolution chains and the alignment report; never against a requirement row (design: the fault is as often a missing resource as a wrong count).
- [x] 5.2 Informational only — saving stays available. Assert it, so nobody later "improves" it into a block; ⑨-2 design D8 decided this deliberately and its own test asserts the save succeeds.
- [x] 5.3 Wording states required against eligible and names the roles. It must not say *available*, *free*, or *bookable* — the standing ⑧a D5 constraint, now with two prior surfaces worded under it to follow.
- [x] 5.4 Say nothing when sufficient, and say nothing when the configuration is unresolvable or the request failed — never a shortfall of zero, which is the answer that tells an editor their configuration is wrong.
- [x] 5.5 Wording derived from state captured with the response it describes, never from the live form.
- [ ] 5.6 Accessibility: text in the form's own structure, every referenced id resolving in the same shadow root, not conveyed by colour alone. **Verify by reading the rendered shadow DOM, not from screenshots** — and check the error state as well as the healthy one, which is the half a static reading cannot reach.

## 6. Editor — the two surfaces ⑨-2 left misleading

- [x] 6.1 The collection view's requirement summary distinguishes two roles that share a resource type, by what each requires (design D5).
- [x] 6.2 A service whose roles all name distinct types keeps a short summary — capabilities are not stated everywhere. Test both, or the rule collapses into "always show everything".
- [x] 6.3 A counted role states its count in the summary.
- [x] 6.4 The resolution-chain readout stops implying disjoint pools where they overlap (design D6). The chains themselves do not change — each is still true of its own role — so this is presentation, and the joint fact belongs to the sufficiency report beside it.
- [x] 6.5 Confirm the ⑨-2 sync's corrected justification for that requirement and this presentation change now agree; ⑨-2 fixed the reasoning and left the rendering.

## 7. Verification

- [ ] 7.1 Full solution build with `--no-incremental`, **TestSite stopped first** — it locks the output DLLs and the build will fail confusingly if it is running. Only the known NU1903 advisories are acceptable.
- [ ] 7.2 Full unit, integration and client runs green, including every ⑤/⑥/⑦/⑧/⑨ scenario unchanged.
- [ ] 7.3 Live: a service with a count exceeding its pool shows the report and still saves; adding a qualifying resource clears it without touching the service.
- [ ] 7.4 Live: two same-type roles sharing one resource are reported together, and the collection view distinguishes them.
- [ ] 7.5 Live: a booking attempt at a busy instant returns `service-unavailable` with a message naming the shortfall, and the code is unchanged.
- [ ] 7.6 Stop the TestSite and check port 44348 for orphaned processes. Note Umbraco 17 intermittently renders the backoffice shell without registering package extensions on a first load — a fresh navigation clears it; importing the bundle directly to confirm the custom elements define is what distinguishes that flake from a real fault.

## 8. Spec hygiene

- [x] 8.1 Run the guarantee diff `CLAUDE.md` requires for the two MODIFIED requirements — `All candidates failing reports one of two distinct outcomes` and `Backoffice collection view for services`. Both are being replaced wholesale, both carry guarantees unrelated to this change (the whole conflict/deterministic classification; the duration summary and paging), and anything the new version forgets to restate is deleted with nothing in the diff resembling a deletion.
- [x] 8.2 The outward grep for sibling specs this change falsifies, **before and after** the sync. **Grep the vocabulary of the mechanisms being changed, not only of the change** — ⑨-2's first pass recorded three findings and the real number was nine, precisely because it searched for its own vocabulary rather than for the mechanisms it replaced. Candidate terms here: "cannot be filled", "no candidate", "unavailable", "each role", "independently", "eligible", "sufficient".
- [ ] 8.3 Confirm nothing in the new wording asserts availability, across all three surfaces — Core's report, the preview payload, and the editor's text. It is one constraint stated in three places and is the easiest thing here to get subtly wrong.

### Notes from the apply

- **The witness is `SlotSaturation` — an assignment or a `SlotDeficiency`, never
  both.** `TrySaturateIncluding` deliberately keeps returning a bare `Guid[]?`: its
  failures belong to the *constrained* graphs it builds, one per slot the preferred
  resource could fill, and a witness drawn from one of those would answer a
  question nobody asked. Preference falling through is not a shortage.
- **The tight witness costs nothing, and the proof is worth keeping.** On failure
  every resource the walk reached is already filled — an unfilled candidate would
  have been taken outright rather than walked through — so the deficient set is the
  unfilled slot plus the slots holding those resources, and `|reached| = |slots| −
  1` falls out. That is why the code takes `filledBy[resource]` with the indexer
  rather than `TryGetValue`: a violation of that invariant is a wrong witness, and
  should be loud.
- **The collapse to roles is exact, not approximate.** Slots of one role share one
  candidate list, so the neighbourhood of the collapsed set *equals* the
  neighbourhood the assignment reported — `Eligible` is therefore taken straight
  from the witness, and `Required` (the whole roles' counts) can only exceed it.
- **`RuleClassification` reordered, outcome for outcome unchanged.** It now checks
  `raced` first, then asks the assignment about the rule-admitting graph, then asks
  whether the claims broke it. That is exactly the old `raced ||
  (saturates && !saturatesWhenFree)` in a shape that keeps the witness. The 589
  pre-existing unit tests were green throughout (task 3.4).
- **The preview request now carries `Count`** (⑧a had fixed it at 1). The chains
  are unchanged by it — a role of count 3 draws on exactly the pool a role of count
  1 does — but the sufficiency finding is an assignment question and cannot be
  asked without it. An out-of-range count is therefore a validation failure, which
  is the rule a malformed type key already followed: both make the answer describe
  something other than what is on screen. **This falsifies a sentence in the main
  `resource-management` spec** — see the note under 8.2.
- **Mutation results (1.6, 2.4, 3.5).** Report every slot → 5 red. Neighbourhood as
  every filled resource → 3 red. Neighbourhood as the union of every role's pool →
  2 red. Report every role *and* the whole universe → 2 red. Per-role count
  comparison instead of an assignment → 2 red, and both are the deliberately
  overlapping fixtures. Breaking the deficiency detection moved **both** surfaces —
  `PoolSufficiencyTests` and `MultiRolePlacementTests.Both_surfaces_report_the_same_shortfall_from_one_computation`
  — which is 3.5's structural guarantee. Client: `sharedTypes` forced true → 8 red,
  forced false → 4 red, so both halves of design D5 are held.
- **8.1 found nothing, and the tool was proved able to find something.** Both
  MODIFIED requirements carry every scenario and every SHALL forward. The diff
  script was checked by deleting one scenario and one SHALL from a copy of the
  delta: it reported exactly those two. A spec-diff reporting *nothing* is as
  suspect as one reporting everything.
- **8.2 found one, and it is an artifact question rather than a code one.** The
  main `resource-management` requirement "Service configuration preview endpoint"
  enumerates the request as "a list of roles (each a resource type key and a set of
  required capability keys) and a duration specification", and lists the endpoint's
  validation failures. Both are now incomplete: the request also carries a count,
  and an out-of-range one is a failure. Nothing else was falsified — checked
  against "cannot be filled", "no candidate", "each role", "independently",
  "eligible", "sufficient", "disjoint", "saturating", "unavailable", and the
  vocabulary of the mechanisms changed (count, chain, requirement summary).
- **Live (7.3–7.5), over HTTP with no backoffice login.** A count of 2 over one
  resource previews with `poolShortfall {required 2, eligible 1}` and still saves
  200; adding a second resource clears it with no edit to the service. Two
  same-type roles over one resource are reported as one finding naming both, with
  `startMisalignment` null beside it — ⑨-2's deliberate silence, and the case this
  change exists for. A placement at an instant only one resource can take returns
  400 `service-unavailable` with "This service needs 2 distinct resources for
  'psx…' at that time, and only 1 was available", while the same configuration
  reports no shortfall — the two surfaces distinguishing configuration from
  instant, live.

## 9. Handover

- [ ] 9.1 Record what ⑩ inherits: the delivery-API reason code ⑨-1a deferred is now more valuable, because two different structural faults (misalignment and insufficiency) both surface to a booker as an empty result, and Core can now distinguish them.
- [ ] 9.2 Record whether the deficient set proved to be the right unit in practice, and settle the two open questions design leaves — whether the report names the qualifying resources or only counts them, and whether the collection view states capabilities for a counted role.
