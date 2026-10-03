## 1. Runbook on `main`: remove the route

- [x] 1.1 Delete *Fallback: pushing by hand*: the API-key acquisition, the push command, the
      `setapikey` note and the `.snupkg`/`--skip-duplicate` note. Verify: none of `--api-key`,
      `Read-Host`, `setapikey` or `dotnet nuget push` appears in `docs/publishing.md`.
- [x] 1.2 Remove each cross-reference to the fallback:
      - the *Status* outstanding bullet announcing this change, keeping its still-open half (the
        unobserved symbol-package 409 wording) as a bullet of its own;
      - the ownership bullet's *"under Fallback: pushing by hand"* pointer, re-pointed at the
        re-homed 403 section;
      - *"If the workflow cannot publish … use Fallback"* (*Publishing a release*);
      - *"On the fallback route the order is the same … reject"* (*Tag the release*);
      - *"Either route"* and the wildcard-order paragraph (*After the push*).

      Verify: a case-insensitive search for `fallback` in `docs/publishing.md` finds only the
      sentence written by 1.3, if any.
- [x] 1.3 Write the "no hand route" statement under *Publishing a release*: there is no hand route;
      the release waits, or the workflow is repaired in its own change first; a hand push skips
      the tag check, the clean pack, the verification, the approval and the push order (spec:
      *The workflow cannot publish*). Verify by reading it against that scenario.

## 2. Runbook on `main`: reframe and re-home

- [x] 2.1 Reframe *The order that makes SourceLink correct* (design D1). Keep the three-surface
      table and the stale-links explanation. Replace the numbered command block with the
      fresh-clone account, and keep the `GenerateNuspec` explanation as the reason a clean clone
      matters. Verify: no `git remote set-url`, `dotnet clean`, `dotnet build` or `dotnet pack`
      command remains in the section, and `packaging`'s *The ordering that makes source links
      correct is documented* scenario still reads true against it.
- [x] 2.2 Reframe *Verify, do not assume* as the pre-approval read of a `.nuspec` from the run's
      `packages` artifact, covering what nothing checks (authors, copyright, licence,
      description). Keep the zip note and the `icon`/`readme` rationale. Verify: the snippet reads
      a downloaded artifact rather than `src/**/bin/Release`, and the *Publishing a release*
      section's "read one `.nuspec`" sentence and this section do not contradict each other on how
      many to read.
- [x] 2.3 Reframe *Publish only from a commit that is already on the public repository*. Keep the
      SHA explanation. Replace *"Pack from the merge commit on main, after it is pushed"* and its
      `git branch -r --contains` block with the check job's refusal of a commit no published line
      contains. Verify: the section names no branch, so it is true on both lines.
- [x] 2.4 Move the 403 lesson under *Trusted Publishing setup* (design D3). Order it: policy owner,
      then scope, then package selection (the spec's order), then whether the ID is somebody
      else's. *(QA round 1 corrected this from "owner, scope, package ID", which disagreed with
      the spec.)* Say in one clause that the key the message names is the one
      the token exchange returned. Merge in the old *"The same `403` on the workflow route"*
      paragraph and the narrow-scope policy note so the lesson is stated once. Keep the
      ownership-is-transferable paragraph. Verify: `packaging`'s three *records what a publish
      actually cost* scenarios each still read true.

## 3. README and guards on `main`

- [x] 3.1 `README.md`, *Gated releases*: remove "A manual route is documented as the fallback…".
      Verify: `fallback` no longer appears in `README.md`.
- [x] 3.2 Update `VersionTruthTests.The_publishing_runbook_states_what_cannot_be_undone` per design
      D5's table. Keep the surviving pins. Re-pin `DELETE the old artifacts`/`Step 3 is not
      housekeeping`, `Pack from the merge commit on main…` and `push new packages and package
      versions` on their replacement sentences, with comments saying what each replaced and why.
      Add a pin on the "no hand route" statement. Verify: each re-pinned sentence states the same
      hazard as the one it replaces (a comment pairs each old pin with its new one), and deleting
      any newly pinned sentence fails the test. Mutate one by hand and confirm it goes red.
- [x] 3.3 Recount `nuget.org` in `docs/publishing.md` and update its `AcceptedPublicationMentions`
      entry. Append a `23 -> N at remove-publish-fallback` paragraph that judges each removed and
      added mention, as the `22 -> 23` entry does. Verify:
      `Every_mention_of_the_feed_is_accounted_for` passes, and the reason text accounts for the
      difference mention by mention.
- [x] 3.3a **Scope addition (Chris, 2026-10-03):** fix the clock time bomb in
      `ClosuresControllerTests.The_list_omits_past_closures_by_default`. Date its closures from
      `TestData.Now` (the controller's fixed clock), not `DateTime.UtcNow`. Verify: the test
      fails before the fix (deterministically since 2026-10-02) and passes after it, and a search
      for `DateTime(Offset)?.(UtcNow|Now|Today)` in `tests/` finds no other code use. That
      search does not cover `TimeProvider.System`, which other tests pass (`BookingEmailTests`,
      `ResponsibilityGateTests`, integration stores). QA round 1 found no date-dependent assertion
      behind any of them.
- [x] 3.4 Full verification on `main`: client build first, then a Release build with 0 warnings,
      then the unit, integration and rendering suites per project, sequentially. Then run
      `openspec validate --all --strict`. Verify: all green at the recorded baselines (unit 2005
      ± this change's test delta, integration 194, rendering 1168, client 335). Report the
      counts.

## 4. Port to `dev/v18`

- [x] 4.1 Branch `remove-publish-fallback-v18` from `dev/v18`. Apply the `docs/publishing.md` and
      `README.md` hunks, keeping v18's version literals (`18.1.2`). Verify:
      `git diff remove-publish-fallback remove-publish-fallback-v18 -- docs/publishing.md` shows
      only version-literal differences and v18's existing unrelated wording.
- [x] 4.2 Redo 3.2, 3.3 and 3.3a by hand in v18's `VersionTruthTests.cs`. Do not cherry-pick it: the file
      differs by about 290 lines. Verify: the same pins exist on both lines (compare the
      `SaysOnce` lists), and v18's `nuget.org` count matches its own recount.
- [x] 4.3 Copy the change folder (`openspec/changes/remove-publish-fallback/`) onto the v18 branch.
      Verify: `openspec validate remove-publish-fallback --type change --strict` is valid there.
- [x] 4.4 Full verification on v18, as in 3.4. Verify: green at v18's baselines (unit 2017 ± delta,
      integration 194, rendering 1168, client 335).

## 5. QA, merge, archive (both lines)

- [x] 5.1 QA review in a fresh subagent, covering both branches in one review. Give the reviewer
      the claims (counts, greps, the D5 pin pairs) to verify rather than trust. Verify: APPROVE. *(Round 1 REJECT: 1 MAJOR, 3 MINOR. Round 2 APPROVE WITH NITS, both branches; two cosmetic nits taken.)*
- [ ] 5.2 Chris pushes both branches and opens PRs into `main` and `dev/v18`, and CI is green on
      each. Verify: run IDs recorded.
- [ ] 5.3 After merge, archive on each line so its `release-publishing` spec gains the ADDED
      requirement and loses the REMOVED one. Do the outward grep of `openspec/specs/` for any
      sibling sentence that still promises a manual route. Verify: `openspec validate --all
      --strict` passes on both lines, and the grep result is recorded.
- [ ] 5.4 Outside the repository (Chris): delete the remaining nuget.org API key, and confirm no
      nuget.org key exists among the repository's Actions secrets. Verify: Chris confirms both.
