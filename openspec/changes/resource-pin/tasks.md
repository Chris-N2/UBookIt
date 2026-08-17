## 1. Core — the pin

- [x] 1.1 Rename `ServiceBookingRequest.PreferredResourceId` to `PinnedResourceId`. The compiler finds every C# site; there is no dynamic access by name.
- [x] 1.2 Remove the fallback in `Resolve`: `TrySaturateIncluding(ids, pinned) ?? TrySaturate(ids).Assignment` loses its `??`. That single `??` is the whole behaviour being changed.
- [x] 1.3 Fail with the new stable code `pinned-resource-unavailable` when no saturating assignment includes the pinned resource. **Not** by letting the loop run dry — see 1.5.
- [x] 1.4 The ineligible case is unchanged: a pin in no candidate pool is still `resource-not-eligible`, still reported before any availability question, and still ahead of the empty-pool guard.
- [x] 1.5 **The pin failure must not go through `RuleClassification.AllFail`.** That answers "why did every assignment fail" for a *pool*, and assignments may exist in abundance without the pinned resource — so routing it there yields `conflict` or `service-unavailable` for a request with a precise and different answer (design D5). Assert the codes directly.
- [x] 1.6 The code is **transient**, not deterministic: assert it is absent from `IsDeterministic`, exactly as the previous change asserted `resource-not-directly-bookable` was.

## 2. Core — tests that can actually fail

- [x] 2.1 **The negative is the whole change.** A suite asserting "the pinned resource is used when free" passes the implementation being removed. The covering test MUST be: pinned, eligible, unusable, **and a perfectly good assignment available without it** — asserting the failure rather than the substitution.
- [x] 2.2 **Mutation:** restore the `??` fallback and confirm 2.1 turns red. If it stays green the fixture has no alternative assignment and proves nothing.
- [x] 2.3 Cover every reason a pin fails, since they must not collapse: the pinned resource is claimed, and its own rules refuse the request. **Corrected at apply — this task originally named a third, "claiming it would strand another slot", which does not exist** (design D4): an assignment can always be swapped to contain an eligible resource, so a pin has no structural failure mode. The test written for it failed, which is how the design error surfaced. Replaced by a test asserting the opposite — a satisfiable pin over a counted role IS honoured — plus D4a's case, where nothing could be assigned either way.
- [x] 2.4 A pin that CAN be honoured still books that resource, in a multi-role service, in whichever slot fits — the ⑨-2 guarantee, carried forward unchanged.
- [x] 2.5 Every unpinned outcome is untouched: the ⑨-2 all-fail suite is the guard, and it must stay green without modification.
- [x] 2.6 A pin naming a resource outside every pool still reports `resource-not-eligible`, including when a role's pool is empty.

## 3. Delivery API

- [x] 3.1 Rename the request member to `pinnedResourceId`. **BREAKING (unpublished).**
- [x] 3.2 Map `pinned-resource-unavailable` to 400 through the existing catch-all. Assert the status — that mapping requirement is not being modified, so the assertion is the only thing holding it.
- [x] 3.3 Assert it is distinguishable from `conflict` at the boundary, with a test that provokes both and compares codes. That distinction is the reason the code exists.
- [x] 3.4 Regenerate the client and read the diff: one renamed member and nothing else.
- [x] 3.5 Confirm `POST /bookings` (direct placement) is untouched — it has no pin and never did.

## 4. Spec hygiene

- [x] 4.1 **Guarantee diff both MODIFIED requirements, and read the deletions by hand.** `Booking a service resolves an assignment of distinct resources` is 150 lines and 17 scenarios, almost none of it about pinning; `Service booking placement` is 10 scenarios. The tool compares scenario *titles* and is blind to a changed WHEN — that is how a scoping change slipped past on the previous change, so hand-read every `-` line in the sync diff.
- [x] 4.2 Account for all 17 original scenarios explicitly: 4 renamed, 1 replaced, 12 verbatim. A rename and a deletion look identical to the tool.
- [x] 4.3 The outward grep, **before and after** the sync. **Include the words you wrote down** — the previous change listed "directly" as a candidate and omitted it from the command, which is how a falsified requirement survived a round. Candidates here: "preferred", "preference", "hint", "falls back", "substitut", "pin", "constrain only the role".
- [x] 4.4 Confirm the stale-sentence correction landed: `delivery-api` said a preferred id "SHALL constrain only the role whose pool contains it", which `service-booking` has contradicted since ⑨-2. Check no third spec repeats the same stale claim.

## 5. Verification

- [x] 5.1 Clean `dotnet build --no-incremental` with the **TestSite stopped**, and only the NU1903 advisories. **Not optional and not deferrable**: `dotnet test` builds incrementally, and the previous change shipped a nullable regression that only this build could see.
- [x] 5.2 Unit, integration and client suites green, including every ⑤–⑨ scenario unchanged.
- [x] 5.3 Live: a service placement pinning a busy-but-eligible resource returns 400 `pinned-resource-unavailable` while another assignment plainly exists.
- [x] 5.4 Live: the same placement without the pin succeeds — the pair that proves the failure is the pin's doing and not the pool's.
- [x] 5.5 Live: a pin that can be honoured books that resource, and the response names it.
- [x] 5.6 **Launch the TestSite detached** (`Start-Process -WindowStyle Hidden`), never as a tracked background task: stopping the task truncates its build output and the next start serves a harness that 500s on every page. Afterwards, check port 44348 for orphans.

### Notes from the apply

- **Two corrections to my own design, both found by writing the code.**
  **D4 listed three reasons a pin can fail and there are two.** "Claiming it would
  strand another slot" does not exist: whenever an assignment exists and the pinned
  resource is eligible for a slot, an assignment *containing* it exists too — swap
  along an alternating path. The codebase already proved it, in
  `SlotAssignmentTests.A_preference_is_reported_as_none_only_when_nothing_saturates`,
  whose comment warns against strengthening exactly that. My test for the
  non-existent third reason failed, which is how I found it.
  **D4a followed from D4 and the first draft missed it.** If nothing saturates with
  *or* without the pin, blaming the pin is true and misleading — it invites a front
  end to offer the other people when there are none. The pin is now reported only
  when an assignment existed without it, and the requirement says so. One extra call
  to the same function over the same graph; not a second implementation.
- **The delta was transformed from the existing spec text, never retyped.** All 17
  scenarios of a 150-line requirement accounted for by hand: 4 renamed, 1 replaced,
  12 verbatim. The guarantee-diff tool compares titles and cannot tell a rename from
  a deletion, so the accounting was scripted separately and printed.
- **A pre-existing contradiction corrected on the way past.** `delivery-api` said a
  preferred id "SHALL constrain only the role whose pool contains it" — untrue since
  ⑨-2 let a resource belong to several pools, and contradicted by `service-booking`
  ever since. ⑨-2's sync grep missed it. Fixed in the same requirement this change
  modifies, with the correction stated rather than slipped in.
- **Mutations:** restoring the `??` fallback turns **4** red; dropping D4a's guard
  turns **1**. Both were run after the tests were written, not before.
- **Live, over HTTP:** a pin that can be honoured books that resource; the same pin
  once busy returns 400 `pinned-resource-unavailable` while the other resource is
  plainly free; the identical request *without* the pin succeeds on that other
  resource — the pair that proves the failure is the pin's doing; with both busy the
  answer reverts to 409 `conflict`, D4a working; and a pin outside every pool is
  still `resource-not-eligible`.
- **The delivery contract**, read from the running OpenAPI document:
  `ServicePlacementRequestModel` is `[start, durationMinutes, pinnedResourceId,
  booker]` and `PlacementRequestModel` is untouched. The backoffice client
  regenerated to **no diff at all**, correctly — the pin is a delivery concern and
  the backoffice does not consume it.
- **TestSite discipline, learned last change and needed twice today.** A stale
  instance held the output DLLs and failed the build; launching detached with
  `Start-Process` while another already ran left two. Stop every instance, build,
  then launch exactly one.

## 6. Handover

- [x] 6.1 Record what ⑩ inherits: a pin that fails honestly, so a "pick your therapist" screen can report *that person is not free then* and offer the ones who are.
- [x] 6.2 Record the availability question deliberately not built (design D6): a "starts at which this resource can be assigned" query, additive, needed only if the flow ever inverts to who-then-when. State that it is the same `TrySaturateIncluding` seam.
- [x] 6.3 Record whether removing soft preference was missed in practice — the alternative kept in reserve is a second, differently named field, not a flag on this one.

### What follows this change

- **⑩ inherits a pin that fails honestly.** A "pick your therapist" screen can now
  report *that person is not free then* and offer the ones who are, because
  `pinned-resource-unavailable` is distinguishable from `conflict`. Before this it
  would have shown a confirmation naming somebody the booker did not choose.
- **The availability question deliberately not built** (design D6): "bookable starts
  at which this resource can be assigned". Needed only if the flow ever inverts to
  who-then-when; additive if it does, and it is the same `TrySaturateIncluding` seam
  — service bookable-starts already ask, per (start, length), whether a saturating
  assignment exists, so a pinned variant asks the same question of the same function.
- **Soft preference is gone and its replacement is named, not improvised.** If
  "book Mary if you can, anyone otherwise" is ever wanted, it is a second,
  differently named field — never a boolean flag on this one, which would restore
  the silent substitution as the answer for anyone who forgot to set it.
- **A pin cannot fail structurally**, and that is worth carrying because it is
  counter-intuitive and was got wrong once already in this change's own design. Any
  future reasoning about pins should start from it: `TrySaturateIncluding` returns
  null exactly when `TrySaturate` does over the same graph.
