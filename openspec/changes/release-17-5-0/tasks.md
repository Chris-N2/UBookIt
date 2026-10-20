## 1. The version and the entry

- [x] 1.1 Branch `release-17-5-0` from local `main` at `6ced37b` (the `service-booker-instructions`
  close-out merge).
  - Bump `<Version>` to `17.5.0`, and move every row of the table in `docs/publishing.md`
    *Before any push*, step 2.
  - Re-read the *Tag the release* literals by eye, because no guard sees them.
  - **Do not move the history anchors.**

  Verify: `git grep 17.4.0` over `Directory.Build.props`, README and the runbook is empty, apart
  from history anchors. Record the counts before and after.
  *Done:* before, `17.4.0` appeared 1 time in `Directory.Build.props`, 21 in README (16 tag-pinned
  documentation links, 4 tag-pinned screenshot URLs and the "uBookIt is at" sentence) and 5 in the
  runbook (its "uBookIt is at" sentence and the four *Tag the release* literals) — the same 1/21/5
  as at `17.3.2`. Each was read in context; none is a history anchor. After: 0 of `17.4.0`, and
  1/21/5 of `17.5.0`; the diff is those 27 lines. The *Tag the release* literals were re-read by
  eye (the example URL, `git tag`, `git push` and `curl`).
- [x] 1.2 Write the `17.5.0` entry, undated. **Check every claim against the code and the archived
  change (`2026-10-19-service-booker-instructions`), not against the proposal.** In particular:
  - the field's label, place, limit and normalisation;
  - which messages carry the instructions, by status, including a booking recorded on somebody's
    behalf;
  - where they sit in uBookIt's own wording, and that they are read at send time;
  - that a save without `bookerInstructions` clears them, and what that means for an open tab;
  - the downgrade claim, **checked against `17.4.0`'s code**;
  - the public additions, by exact name;
  - "no dependency changes", against `17.4.0`, and again from the artifact at 4.2.

  Verify: `ChangelogTests` and `VersionTruthTests` pass.
  *Done.* Each claim against its source:
  - **Label, place, limit, normalisation:** `en-us.ts` (`bookerInstructions`), the Details box in
    `services-editor.element.ts`, `Service.BookerInstructionsMaxLength` (2000) and
    `Service.NormalizeBookerInstructions` (trim; empty or whitespace is none; line breaks kept).
  - **Which messages:** `BookingMessageComposer.BookerInstructionsAsync` (status `Confirmed` and a
    service attribution, nothing else), live-checked in the feature's 9.3. **On behalf:**
    `docs/notifications.md` *Bookings your own people record* ("confirmed, whatever `AutoConfirm`
    says") and the feature's composer test over `PlacedOnBehalf`.
  - **Position and send-time read:** design 3, `ForBookerAsync`, and the feature's tests and live
    check (after the closing line, before the cancellation link).
  - **A save without the field clears:** `ServiceModelMapper.ToDomain` normalises an absent
    `bookerInstructions` to none, and the update replaces the service; QA's release-notes item.
  - **Downgrade, against `17.4.0`'s code:** `RunUBookItMigrations` migrates only when
    `GetPendingMigrationsAsync` is non-empty, which an extra applied row does not make it; the
    `ServiceRow` there has no `BookerInstructions` property, so EF neither reads nor writes the
    column; `SqlServiceManagementStore.UpdateAsync` loads the tracked row and sets only the mapped
    scalars, so an update leaves the column as it was; a create leaves it `NULL`.
  - **Public additions:** from the archived feature's code, by name.
  - **No dependency changes:** `git diff 17.4.0 HEAD` over `Directory.Packages.props`, every
    `.csproj` and the client's `package*.json` is empty.
  - The README gains one bullet naming the field, conditional on booker emails being sent.

  `ChangelogTests` and `VersionTruthTests`: 20 of 20.

## 2. Clean build and suites

- [x] 2.1 Stop the TestSite. Clean the outputs (including the stale hashed client assets), build
  the client, then `CI=true dotnet build UBookIt.slnx -c Release --no-incremental`. Verify:
  0 warnings, 0 errors.
  *Done (2026-10-19, at `1966e06`):* no TestSite running. The hashed client assets and both
  `obj` configurations of `UBookIt.Backoffice` were deleted, the client built, then
  `CI=true dotnet build UBookIt.slnx -c Release --no-incremental`: 0 warnings, 0 errors.
- [x] 2.2 Run the suites per project, one after another. Verify, each with 0 skipped:
  - unit **2263**, integration **214**, rendering **1178**, client **370**;
  - `openspec validate --all --strict` passes (26 specs, plus this change), run as CI runs it,
    after the last commit.
  *Done, Release, 0 skipped:* unit **2263**, integration **214**, rendering **1178**, client
  **370**. `openspec@1.13.0 validate --all --strict`: 27 passed (re-run after the last commit).
- [x] 2.3 Run the audit as `audit.yml` does. Verify: exit 0, the ImageSharp TestSite exception
  reported as accepted, not stale.
  *Done:* `Assert-NoVulnerablePackages.ps1 -Target UBookIt.slnx` exits **0**: 10 projects,
  Debug and Release, "5 finding(s) accepted by a suppression", all the ImageSharp TestSite
  exception, none stale. "No unaccepted advisories in the graph."

## 3. Review and merge

- [x] 3.1 QA (`qa-review`) in one subagent, reused across rounds, covering this change and
  `release-18-4-0`. Verify: APPROVE.
  *Done (2026-10-19): APPROVE WITH NITS at round 1*, one fresh reviewer for both changes. It
  re-ran every claim (clean Release builds, all suites on both lines, the audit, openspec
  27/27, `ChangelogTests` + `VersionTruthTests` 20/20) and checked every entry sentence against
  the code, the downgrade paragraph against the code at the previous tag. Nits taken, without
  re-review: 4.2 restores the EF Core SqlServer `10.0.11` check and the "exactly / no other
  package declares MessagePack" form; v18's 3.1 named itself instead of `release-17-5-0`; the
  README bullet says "in its own emails" (a site's own views must render the member); the
  reload advice is now a hard reload, since the reviewer could not confirm the backoffice
  entry bundle is cache-busted. Not taken: a cross-line note in `18.4.0` for a 17.5.0 site
  moving to Umbraco 18 on 18.3.x — earlier v18 entries do not cross-reference the 17 line.
  Unverified by anyone: the downgrade by measurement (reasoned from the tag code only).
- [x] 3.2 Chris pushes this branch and opens the PR. Verify: the PR's CI is green, parity included.
  *Done (2026-10-20):* PR #48, CI green (Chris).
- [x] 3.3 Chris merges with "Create a merge commit". Verify: `origin/main` is the merge commit, and
  its push run is green.
  *Done:* `origin/main` is `195edd7`, parents `6ced37b` and `2f8d3cb`.

## 4. Release through the workflow

- [x] 4.1 Chris pushes tag `17.5.0` on the merge commit. Verify: `check` and `pack` are green.
  *Done:* `17.5.0` resolves to `195edd7`; the run reached the approval, so `check` and `pack`
  were green.
- [x] 4.2 **Before approving**, check the run's `packages` artifact, parsing each nuspec as XML:
  - the tagged `booking-flow.png` returns 200;
  - there are exactly 5 `.nupkg` and 4 `.snupkg`, all `17.5.0`, each `.snupkg` holding its `.pdb`;
  - all five nuspecs carry the authors, copyright, licence, readme, icon, project URL and the
    merge commit, with `umbraco-marketplace` on `UBookIt` only;
  - every `Umbraco.Cms.*` dependency is `[17.7.1, 18.0.0)`, confirmed against
    `Directory.Packages.props` at the tag, and EF Core SqlServer is `10.0.11`; the dependencies
    between the uBookIt packages are at `17.5.0`;
  - carried from `17.4.0`: `Pkcs` `10.0.13` on exactly Persistence and Web; MessagePack `3.1.9`
    on exactly Backoffice and Web, and no other package declares MessagePack; the
    `Microsoft.AspNetCore.App` framework reference on `UBookIt`, Backoffice and Web; no `System.Security.Cryptography.Xml`; the backoffice bundle's only
    localization culture is `en`;
  - **no dependency differs from the published `17.4.0` packages**, downloaded from nuget.org;
  - **new this release:** `UBookIt.Persistence.dll` contains the migration
    `20261019195550_AddServiceBookerInstructions`; the packed backoffice bundle carries the
    `bookerInstructions` label and hint; `UBookIt.Web.dll`'s embedded `package.xml` is the tag's,
    byte-for-byte;
  - the packed README is identical to the tag's, all its addresses return 200, and every
    repository address is pinned to `17.5.0`.

  Show the check script can fail (against the `17.4.0` artifact with this release's expectations).
  *Done:* a script parsing each nuspec as XML, adapted from `17.4.0`'s (no dependency difference
  allowed this time; the migration and label checks added). Against the run's artifact, with merge
  commit `195edd7…` and `[17.7.1, 18.0.0)` confirmed against `Directory.Packages.props` at the
  tag: **91 of 91 PASS, exit 0**. The 5 dependency comparisons were against the published `17.4.0`
  packages downloaded from the flat container; all 19 README addresses returned 200. Negative
  control: the published `17.4.0` packages with this release's expectations give **26 FAIL,
  exit 1**, the migration and label checks among them. The two new strings were also confirmed
  present in a local Release build, so neither check is one that cannot pass.
- [x] 4.3 Chris approves. Record the summary table and the conclusion.
  *Done (2026-10-20):* approved after 4.2; the publish run completed (Chris). The packages'
  required signer stays "Any": no owner has a certificate registered, so unsigned submissions
  are accepted, as before.
- [x] 4.4 Confirm `17.5.0` per package on the flat container, and that the package page renders.
  *Done:* `17.5.0` is in the flat container index of all five ids; `nuget.org/packages/UBookIt/17.5.0`
  returns 200.

## 5. Close out

- [x] 5.1 Stamp the entry's date.
  *Done:* `## 17.5.0 — 2026-10-20`.
- [x] 5.2 Sibling sweep for `17.4.0` and for sentences the release falsifies. Fix or record each.
  *Done:* outside `CHANGELOG.md` and `openspec`, `17.4.0` appears nowhere, tests included; a
  sweep of `docs`, README and `src` for `17.4` finds only an npm dependency's `17.4.2` in
  `package-lock.json`. No sentence about "the latest/current release" needs moving.
- [x] 5.3 Update the deferred-obligations memory: the `service-booker-instructions` release-notes
  obligation (a save without the field clears it) is paid by the entry.
  *Done:* recorded paid (the obligation lived in the feature's handover memory, and is marked
  there too).
- [ ] 5.4 Commit and run the unit suite. Chris pushes. Verify: `main`'s CI is green.
- [ ] 5.5 Archive last, running the unit suite before the push.
