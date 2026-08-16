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
- [x] 5.6 Accessibility: text in the form's own structure, every referenced id resolving in the same shadow root, not conveyed by colour alone. **Verify by reading the rendered shadow DOM, not from screenshots** — and check the error state as well as the healthy one, which is the half a static reading cannot reach.

## 6. Editor — the two surfaces ⑨-2 left misleading

- [x] 6.1 The collection view's requirement summary distinguishes two roles that share a resource type, by what each requires (design D5).
- [x] 6.2 A service whose roles all name distinct types keeps a short summary — capabilities are not stated everywhere. Test both, or the rule collapses into "always show everything".
- [x] 6.3 A counted role states its count in the summary.
- [x] 6.4 The resolution-chain readout stops implying disjoint pools where they overlap (design D6). The chains themselves do not change — each is still true of its own role — so this is presentation, and the joint fact belongs to the sufficiency report beside it.
- [x] 6.5 Confirm the ⑨-2 sync's corrected justification for that requirement and this presentation change now agree; ⑨-2 fixed the reasoning and left the rendering.

## 7. Verification

- [x] 7.1 Full solution build with `--no-incremental`, **TestSite stopped first** — it locks the output DLLs and the build will fail confusingly if it is running. Only the known NU1903 advisories are acceptable.
- [x] 7.2 Full unit, integration and client runs green, including every ⑤/⑥/⑦/⑧/⑨ scenario unchanged.
- [x] 7.3 Live: a service with a count exceeding its pool shows the report and still saves; adding a qualifying resource clears it without touching the service.
- [x] 7.4 Live: two same-type roles sharing one resource are reported together, and the collection view distinguishes them.
- [x] 7.5 Live: a booking attempt at a busy instant returns `service-unavailable` with a message naming the shortfall, and the code is unchanged.
- [x] 7.6 Stop the TestSite and check port 44348 for orphaned processes. Note Umbraco 17 intermittently renders the backoffice shell without registering package extensions on a first load — a fresh navigation clears it; importing the bundle directly to confirm the custom elements define is what distinguishes that flake from a real fault.

## 8. Spec hygiene

- [x] 8.1 Run the guarantee diff `CLAUDE.md` requires for the two MODIFIED requirements — `All candidates failing reports one of two distinct outcomes` and `Backoffice collection view for services`. Both are being replaced wholesale, both carry guarantees unrelated to this change (the whole conflict/deterministic classification; the duration summary and paging), and anything the new version forgets to restate is deleted with nothing in the diff resembling a deletion.
- [x] 8.2 The outward grep for sibling specs this change falsifies, **before and after** the sync. **Grep the vocabulary of the mechanisms being changed, not only of the change** — ⑨-2's first pass recorded three findings and the real number was nine, precisely because it searched for its own vocabulary rather than for the mechanisms it replaced. Candidate terms here: "cannot be filled", "no candidate", "unavailable", "each role", "independently", "eligible", "sufficient".
- [x] 8.3 Confirm nothing in the new wording asserts availability, across all three surfaces — Core's report, the preview payload, and the editor's text. It is one constraint stated in three places and is the easiest thing here to get subtly wrong.

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

### QA round 1 — REJECT, remediated

Two MAJOR findings, both real, both fixed. The reviewer's own verification is
worth reading beside this: it fuzzed 200,000 random bipartite graphs through
`TrySaturate` and confirmed the tight-witness and exact-neighbourhood properties
the collapse depends on, and it re-ran the mutation set independently.

- **MAJOR — the chain heading contradicted an unmodified requirement.** Task 6.4
  asked the readout to stop implying disjoint pools; I disambiguated it by
  borrowing the collection view's D5 capability rule, and `services` spec
  "The editor reports the resolution chain for each role" says in as many words:
  *the report SHALL NOT refer to required capabilities for a role that names
  none*. "psd1782a (no required capabilities)" is exactly the ⑧a defect that
  sentence exists to prevent. **Fixed by labelling the row, not the capabilities**:
  where two roles share a type each chain is headed "Requirement N: type",
  matching the fieldset legend a few inches below, and bare type otherwise. The
  requirement is untouched and needs no MODIFIED entry. `chainLabels` is a
  separate function from `roleLabels` rather than a flag, because the two surfaces
  answer to different rules — the collection view has no rows to point at, which
  is why its own requirement *was* modified to permit capability text.
  Mutation-checked three ways, including reverting to the rejected capability
  rule: 3 red, so the suite now catches the defect it missed.
- **MAJOR — the single-role message.** `Required == 1` produced "needs 1 distinct
  resources ... only 0 were available" — ungrammatical, and the commonest
  `service-unavailable` in the product, since every one-role service refused on
  open hours, grid, lead time or horizon arrives there. **Fixed by suppressing the
  richer message at `Required == 1`**: counting to one describes nothing a booker
  can act on, and the requirement permits the message rather than requiring it.
  Three tests added, including the pair that proves the suppression is a property
  of `Required == 1` and not of the fixture.
- **MINOR, folded into the same fix — "available" overstated it.** That count
  comes from the rule-admitting graph, taken *before* the claims filter, so a
  resource already booked is still in it. Now "only N can provide it then", which
  is the vocabulary the chains already use for exactly this notion and is true
  whether or not the resource is free.
- **MINOR — the count bound was half tested.** `MaxCount + 1` rejected and
  `MaxCount` accepted both added; the second pins the boundary so the first cannot
  pass with an off-by-one.
- **NIT — `ClaimsBrokeIt` rebuilt the admitting graph.** It now takes it, since
  the caller computes it to establish that method's own precondition.
- **Two NITs deliberately not actioned, and flagged for round 2.** (a) An
  out-of-range count blanks all three reports — spec-permitted ("say nothing when
  ... the request failed") and consistent with every other preview failure, but a
  *new* route to that state which ⑧a's fixed count of 1 structurally prevented.
  Recorded rather than changed. (b) `ShortfallRoleModel` carries no role index;
  two roles equal in type *and* capabilities are indistinguishable in the payload,
  which is only reachable for a configuration the domain rejects, and positional
  matching is exact end to end.
- **One thing the reviewer and I both hit: the backoffice registered no uBookIt
  extension at all**, Resources included, and a fresh navigation did not clear it.
  Ruled out as the known Umbraco 17 flake rather than a bundle break by importing
  every chunk directly — all import, the manifest exports its 5 entries, and both
  custom elements define on import. The editor pass was then done by instantiating
  the real elements directly, which exercises everything except the section
  routing this change does not touch. **A rebuilt bundle is not the cause**: the
  flake reproduced across a full TestSite restart.

Re-verified after remediation: clean `--no-incremental` build (38 NU1903 only),
607 unit / 54 integration / 47 client, `validate --strict` under the pinned 1.6.0.
Live: the single-role service reads "This service cannot be booked at that time.";
count 2 with one admitting resource reads "needs 2 distinct resources ... only 1
can provide it then"; count 3 with none reads "none can provide it then"; the
chain headings read "Requirement 1: psd1782a" / "Requirement 2: psd1782a" with no
capability text anywhere in the readout; a distinct-type service keeps "gmdroom" /
"gmdtherapist"; the collection view is unchanged; no dangling ids; save enabled.

### QA round 2 — REJECT, remediated

One MAJOR, and it was a fault **in the round-1 remediation** — which is why fixes
get the same scrutiny as the original code.

- **MAJOR — the ordinal named the wrong row.** `chainLabels` took the requirement
  number from the response array's index, and `#buildPreviewRequest` drops rows
  whose resource type is still blank *before* sending. The two indices differ
  whenever a blank row sits above a same-type pair — the state of any row just
  added, or whose type has been cleared to retype — so the chain headed
  "Requirement 1" described the row legended "Requirement 2". Worse than the
  ambiguity it replaced: round 1's heading referred to something never entered,
  this one pointed confidently at the wrong control.
  **Fixed by computing the mapping where the request is built and carrying it**,
  never re-deriving it from an array position at render time. `preview-rows.ts`
  is a new pure module holding exactly that seam, because the defect was
  structurally invisible to every existing test: they called the renderer with an
  already-filtered list, so the filter and the renderer were each right in
  isolation while disagreeing about what an index meant. Reintroducing the exact
  defect now turns three of its tests red.
- **MINOR — two identical lines in the sufficiency report.** Pre-existing, missed
  in round 1, and made conspicuous by the fix: the chains above had begun
  numbering rows correctly while the report below could not tell two roles equal
  in type *and* capabilities apart. Fixed by the same mechanism — `RoleShortfall`
  now carries each role's position, `ShortfallRoleModel` publishes it, and the
  editor maps it back to the row.
- **A third gap found while verifying the second, in the same class.** The
  sufficiency report is a **subset** of the configuration, so asking "do the named
  roles share a type" asks the wrong question: a service with two `therapist` rows
  can produce a finding naming one, which then reads as unambiguous while leaving
  the editor unable to tell which row is short. The finding therefore names the
  row **unconditionally** (`findingLabels`), where the chains still decide by
  shared type — they see every role, and heading every chain would be noise.
  Mutating the report back to the chain rule turns four tests red.
- **NIT — dead `Eligible == 1` branch** removed; it was byte-identical to the
  general case, and 607 tests could not tell the difference.
- **NIT — the `Required == 1` comment overclaimed.** The condition is a property of
  the deficient set, not of the service, so it also fires for a multi-role service
  whose shortfall is one count-1 role. Comment corrected rather than the condition
  narrowed: "needs 1 distinct resources for 'therapist'" is no better inside a
  two-role service than outside one.
- **Both deferred items resolved with the reviewer's view on the record.** The
  out-of-range-count blanking stays as it is, with its note; `ShortfallRoleModel`
  gained the index after all, because the MAJOR's fix wanted it anyway — exactly
  the folding the reviewer recommended.

Re-verified: clean build, **611** unit / 54 integration / **57** client, `validate
--strict` under 1.6.0. Live, in the state that broke it — three rows, the first
blank, the surviving pair deficient — the chains read "Requirement 2: psd1782a" and
"Requirement 3: psd1782a" against legends 1/2/3, and the report reads "Requirement
2: psd1782a — 2 required." The management API carries `roleIndex` 1 and 2 for a
finding whose first role is healthy, and 0 and 1 for two byte-identical roles.

## 9. Handover

- [x] 9.1 Record what ⑩ inherits: the delivery-API reason code ⑨-1a deferred is now more valuable, because two different structural faults (misalignment and insufficiency) both surface to a booker as an empty result, and Core can now distinguish them.
- [x] 9.2 Record whether the deficient set proved to be the right unit in practice, and settle the two open questions design leaves — whether the report names the qualifying resources or only counts them, and whether the collection view states capabilities for a counted role.

### What ⑩ inherits

- **The delivery-API reason code ⑨-1a deferred is now worth more, and is now
  cheap.** A booker still cannot distinguish an empty `bookable-starts` response
  from a fully booked week — but Core can now tell the two *structural* causes
  apart, misalignment and insufficiency, and both already return everything such a
  code would need. The work is contract and wording, not computation. The same
  constraint applies as to ⑨-1a's: it may say the roles can never be filled
  together, never that a service *is* available.
- **The placement message is the shape a delivery-side reason code would echo.**
  `service-unavailable` now says what was short at that instant; a configuration-time
  code would say the complementary thing, and ⑩ should keep them worded so a reader
  can tell which question each answers (design D4's risk note).
- **A count above the pool remains saveable**, and ⑩ must not quietly turn the
  report into a block. ⑨-2 design D8 decided that and its own test asserts it; this
  change's task 5.2 asserts the editor half.

### Both open questions, settled against the real screen

- **The report counts the qualifying resources; it does not name them.** Seeing it
  live settles it: the resolution chains directly above already carry the resources
  per role, and the sufficiency claim is about a *set* of roles — a merged list of
  names could not be attributed to a row, and would duplicate the chains for a
  small pool while being unreadable for a large one. The alignment check names
  exactly two because a pair is its unit; this one's unit is a group of rows.
- **The collection view does not state capabilities for a counted role.** Count and
  capabilities are orthogonal, and a counted role is ambiguous with nothing:
  "2 × therapist-mrm" reads unambiguously beside "1 × room". D5's rule keys off a
  *shared type*, which is the only thing that makes two entries indistinguishable,
  and widening it would make every service noisier to fix a case that does not
  exist.
- **The deficient set was the right unit.** In the editor it renders as a headline
  plus one line per row — "psd1782a (no required capabilities): 2 required.",
  "psd1782a (cert-x): 1 required." — which is exactly what an editor points at.
  The two numbers being separate (required against eligible) is what distinguishes
  the two repairs, and both were needed live: raising a count produced one shape,
  a missing resource the other.

### Fixtures left in the TestSite for the reviewer

Type `psd1782a`: resources **PSD Mary** (`cert-x`) and **PSD Gwen**; services
**PSD Counted 1782a** (count 2 — sufficient as it stands, raise it to 3 to see the
report) and **PSD Pair 1782a** (two roles of one type, `cert-x` and none — raise
either count to see a two-role finding). Type `psxad4`: **PSX Mary** (`cert-x`,
opens 09:00) and **PSX Late** (opens 14:00), with service **PSX Counted ad4**
(count 2) — placing that service at 09:00 is the deterministic shortfall whose
message names what was short.
