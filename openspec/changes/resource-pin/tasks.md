## 1. Core — the pin

- [ ] 1.1 Rename `ServiceBookingRequest.PreferredResourceId` to `PinnedResourceId`. The compiler finds every C# site; there is no dynamic access by name.
- [ ] 1.2 Remove the fallback in `Resolve`: `TrySaturateIncluding(ids, pinned) ?? TrySaturate(ids).Assignment` loses its `??`. That single `??` is the whole behaviour being changed.
- [ ] 1.3 Fail with the new stable code `pinned-resource-unavailable` when no saturating assignment includes the pinned resource. **Not** by letting the loop run dry — see 1.5.
- [ ] 1.4 The ineligible case is unchanged: a pin in no candidate pool is still `resource-not-eligible`, still reported before any availability question, and still ahead of the empty-pool guard.
- [ ] 1.5 **The pin failure must not go through `RuleClassification.AllFail`.** That answers "why did every assignment fail" for a *pool*, and assignments may exist in abundance without the pinned resource — so routing it there yields `conflict` or `service-unavailable` for a request with a precise and different answer (design D5). Assert the codes directly.
- [ ] 1.6 The code is **transient**, not deterministic: assert it is absent from `IsDeterministic`, exactly as the previous change asserted `resource-not-directly-bookable` was.

## 2. Core — tests that can actually fail

- [ ] 2.1 **The negative is the whole change.** A suite asserting "the pinned resource is used when free" passes the implementation being removed. The covering test MUST be: pinned, eligible, unusable, **and a perfectly good assignment available without it** — asserting the failure rather than the substitution.
- [ ] 2.2 **Mutation:** restore the `??` fallback and confirm 2.1 turns red. If it stays green the fixture has no alternative assignment and proves nothing.
- [ ] 2.3 Cover all three reasons a pin fails, since they must not collapse: the pinned resource is claimed; its own rules refuse the request; and claiming it would strand another slot (needs a multi-slot fixture where the pin is the only candidate for two slots).
- [ ] 2.4 A pin that CAN be honoured still books that resource, in a multi-role service, in whichever slot fits — the ⑨-2 guarantee, carried forward unchanged.
- [ ] 2.5 Every unpinned outcome is untouched: the ⑨-2 all-fail suite is the guard, and it must stay green without modification.
- [ ] 2.6 A pin naming a resource outside every pool still reports `resource-not-eligible`, including when a role's pool is empty.

## 3. Delivery API

- [ ] 3.1 Rename the request member to `pinnedResourceId`. **BREAKING (unpublished).**
- [ ] 3.2 Map `pinned-resource-unavailable` to 400 through the existing catch-all. Assert the status — that mapping requirement is not being modified, so the assertion is the only thing holding it.
- [ ] 3.3 Assert it is distinguishable from `conflict` at the boundary, with a test that provokes both and compares codes. That distinction is the reason the code exists.
- [ ] 3.4 Regenerate the client and read the diff: one renamed member and nothing else.
- [ ] 3.5 Confirm `POST /bookings` (direct placement) is untouched — it has no pin and never did.

## 4. Spec hygiene

- [ ] 4.1 **Guarantee diff both MODIFIED requirements, and read the deletions by hand.** `Booking a service resolves an assignment of distinct resources` is 150 lines and 17 scenarios, almost none of it about pinning; `Service booking placement` is 10 scenarios. The tool compares scenario *titles* and is blind to a changed WHEN — that is how a scoping change slipped past on the previous change, so hand-read every `-` line in the sync diff.
- [ ] 4.2 Account for all 17 original scenarios explicitly: 4 renamed, 1 replaced, 12 verbatim. A rename and a deletion look identical to the tool.
- [ ] 4.3 The outward grep, **before and after** the sync. **Include the words you wrote down** — the previous change listed "directly" as a candidate and omitted it from the command, which is how a falsified requirement survived a round. Candidates here: "preferred", "preference", "hint", "falls back", "substitut", "pin", "constrain only the role".
- [ ] 4.4 Confirm the stale-sentence correction landed: `delivery-api` said a preferred id "SHALL constrain only the role whose pool contains it", which `service-booking` has contradicted since ⑨-2. Check no third spec repeats the same stale claim.

## 5. Verification

- [ ] 5.1 Clean `dotnet build --no-incremental` with the **TestSite stopped**, and only the NU1903 advisories. **Not optional and not deferrable**: `dotnet test` builds incrementally, and the previous change shipped a nullable regression that only this build could see.
- [ ] 5.2 Unit, integration and client suites green, including every ⑤–⑨ scenario unchanged.
- [ ] 5.3 Live: a service placement pinning a busy-but-eligible resource returns 400 `pinned-resource-unavailable` while another assignment plainly exists.
- [ ] 5.4 Live: the same placement without the pin succeeds — the pair that proves the failure is the pin's doing and not the pool's.
- [ ] 5.5 Live: a pin that can be honoured books that resource, and the response names it.
- [ ] 5.6 **Launch the TestSite detached** (`Start-Process -WindowStyle Hidden`), never as a tracked background task: stopping the task truncates its build output and the next start serves a harness that 500s on every page. Afterwards, check port 44348 for orphans.

## 6. Handover

- [ ] 6.1 Record what ⑩ inherits: a pin that fails honestly, so a "pick your therapist" screen can report *that person is not free then* and offer the ones who are.
- [ ] 6.2 Record the availability question deliberately not built (design D6): a "starts at which this resource can be assigned" query, additive, needed only if the flow ever inverts to who-then-when. State that it is the same `TrySaturateIncluding` seam.
- [ ] 6.3 Record whether removing soft preference was missed in practice — the alternative kept in reserve is a second, differently named field, not a flag on this one.
