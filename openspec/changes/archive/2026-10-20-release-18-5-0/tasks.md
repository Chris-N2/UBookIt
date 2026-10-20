## 1. The version and the entry

- [x] 1.1 Branch `release-18-5-0` from local `dev/v18` at `1776007` (the `service-email-templates`
  close-out merge).
  - Bump `<Version>` to `18.5.0`, and move every row of the table in `docs/publishing.md`
    *Before any push*, step 2.
  - Re-read the *Tag the release* literals by eye, because no guard sees them.
  - **Do not move the history anchors.**

  Verify: `git grep 18.4.0` over `Directory.Build.props`, README and the runbook is empty, apart
  from history anchors. Record the counts before and after.
  *Done:* before, `18.4.0` appeared 1 / 21 / 5 times in `Directory.Build.props` / README / the runbook, the same pattern as at `18.3.0` → `18.4.0`. Each was read in context: the `<Version>`, 16 tag-pinned documentation links, 4 tag-pinned screenshots, the two *uBookIt is at* sentences, and the four *Tag the release* literals. None is a history anchor. After: 0 of `18.4.0`, 1 / 21 / 5 of `18.5.0`, and the diff is exactly those 27 lines. The *Tag the release* literals were re-read by eye (the example URL, `git tag`, `git push` and `curl`).
- [x] 1.2 Write the `18.5.0` entry, undated. **Check every claim against the code and the archived
  change (`2026-10-20-service-email-templates`), not against the proposal.** In particular:
  - the field's label, place, limit and normalisation (lower case, the character rule);
  - the lookup order, and that only "nothing supplied" moves down it;
  - the two failure rules (a failing set view; an unreadable service), for both audiences;
  - the startup report and when it is taken;
  - that a save without `templateSet` clears it, and what that means for an open tab;
  - the changed behaviour for a site that supplies content, when the service cannot be read;
  - the downgrade claim, **checked against `18.4.0`'s code**;
  - the public additions, by exact name;
  - "no dependency changes", against `18.4.0`, and again from the artifact at 4.2.

  Verify: `ChangelogTests` and `VersionTruthTests` pass.
  *Done.* Each claim against its source:
  - **Label, place, rule:** `en-us.ts` (`templateSet`, "Email template set"), `#renderTemplateSet` after the instructions in the Details fieldset, `Service.NormalizeTemplateSet` / `IsValidTemplateSetName` (trim, blank is none, `ToLowerInvariant`, a–z 0–9 single inner hyphens, at most `TemplateSetMaxLength` = 64).
  - **Order and failure rules:** `RazorBookingTemplateRenderer.RenderAsync` (set path, then `PathFor`; a found set view is rendered and its result returned, Failed included). `BookingMessageComposer.ForBookerAsync` / `ForSiteAsync` return the fallback without the renderer on a failed read, for both audiences. The archived change's 11.2 live check covers both.
  - **Startup report:** `UBookItEmailTemplateSetBootCheck` (Information per set with both halves, Warning naming the folder and the lower-case rule), run by `RunUBookItMigrations` after migrating.
  - **A save without the field clears:** `ServiceModelMapper.ToDomain` normalises an absent `templateSet` to none, and the update replaces the scalars.
  - **Changed behaviour, content-supplying sites:** the `booking-emails` MODIFIED requirement, `ServiceTemplateSetMessageTests`.
  - **Downgrade, against `18.4.0`'s code:** `ServiceRow` there has no `TemplateSet` (`git show 18.4.0:…/Rows.cs`), so EF neither reads nor writes the column. `SqlServiceManagementStore.UpdateAsync` at `18.4.0` loads the tracked row and applies only the mapped scalars, so an update leaves the column as it was. An extra applied migration row does not make `GetPendingMigrationsAsync` non-empty, as recorded for `18.4.0`. The 18.4.0 renderer has no set path, so set folders are not consulted.
  - **The read cost:** design 3's table, pinned by `What_a_message_costs_in_service_reads`. 18.4.0's composer read only for a confirmed booker.
  - **Public additions:** from the merged code, by name. The internal storage-ready hook is not named.
  - **No dependency changes:** `git diff 18.4.0 dev/v18` over `Directory.Packages.props`, every `.csproj` and the client's `package*.json` changes only `UBookIt.Core.csproj`'s `InternalsVisibleTo` items, which are not dependencies.
  `ChangelogTests`, `VersionTruthTests` and the documentation tests: 138 of 138.
- [x] 1.3 Add one README bullet under *For developers* naming template sets. Verify: the README
  documentation tests pass.
  *Done:* the same bullet as `release-17-6-0`'s, under *For developers*, after the theming bullet. In the 138 above.

## 2. Clean build and suites

- [x] 2.1 Stop the TestSite. Clean the outputs (including the stale hashed client assets), build
  the client, then `CI=true dotnet build UBookIt.slnx -c Release --no-incremental`. Verify:
  0 warnings, 0 errors.
  *Done (2026-10-20):* no TestSite running. The hashed client assets and both `obj` configurations of `UBookIt.Backoffice` were deleted, the client built, then `CI=true dotnet build UBookIt.slnx -c Release --no-incremental`: 0 warnings, 0 errors.
- [x] 2.2 Run the suites per project, one after another. Verify, each with 0 skipped:
  - unit **2414**, integration **222**, rendering **1211**, client **389**;
  - `openspec validate --all --strict` passes (26 specs, plus this change), run as CI runs it,
    after the last commit.
  *Done, Release, 0 skipped:* unit **2414**, integration **222**, rendering **1211**, client **389**. `openspec@1.13.0 validate --all --strict`: see the record under 3.1 (run after the last commit).
- [x] 2.3 Run the audit as `audit.yml` does. Verify: exit 0, the ImageSharp TestSite exception
  reported as accepted, not stale.
  *Done:* `Assert-NoVulnerablePackages.ps1 -Target UBookIt.slnx` exits **0**: 10 projects, Debug and Release, "5 finding(s) accepted by a suppression", all of them the ImageSharp TestSite exception, none stale. "No unaccepted advisories in the graph."

## 3. Review and merge

- [x] 3.1 QA (`qa-review`) in one subagent, reused across rounds, covering this change and
  `release-17-6-0`. Brief it that temporary source mutations are allowed if reverted. Verify:
  APPROVE.
  *Done: APPROVE WITH NITS at round 2*, one reviewer for both changes, reused across rounds. Its brief allowed reverted mutations. In round 1 it independently confirmed the clean Release builds, every suite on both lines, the audit, openspec 27/27, the 27-line bump with no history anchor moved (and the version guard firing on a mutated README, reverted), the downgrade paragraph against the code at `17.5.0` and `18.4.0`, the dependency diff, and v18's entry as main's with version names only.
  **QA round 1: REJECT (two MINOR wording defects, both lines).** (1) The unreadable-service paragraph was scoped to "if you supply your own email views" and ended "on a site that supplies no views, nothing changes". That is false for a site with its own `IBookingTemplateRenderer`, because the composer's `!SupportsTemplates || Failed` gate means any renderer is no longer called on a failed read. It is now scoped to "your own email content … Razor views or a renderer of your own", ends "no content of its own", and the renderer paragraph and the developers' `IBookingTemplateRenderer` bullet both say the renderer is not called when the service cannot be read. (2) "every message is looked up exactly as before until somebody names a set" contradicted that paragraph. It now reads "no set applies until somebody names one", with a pointer to the content-supplying change. NIT taken: `SetPathFor` is labelled static. NITs not taken, on the 17.5.0 precedent: naming the request/response model properties, and a cross-line note on moving to Umbraco 18. v18's entry was regenerated from main's (version names only). `ChangelogTests` + `VersionTruthTests`: 20/20 on both lines.
  **QA round 2: APPROVE WITH NITS.** Every new sentence checked against the composer. The case "Razor renderer, no views" sends the same fallback as before; only the renderer call and the read's warning on a fault differ. No mutations needed; both trees clean. Remaining nit taken: this record moved here under 3.1. The two declined nits were accepted on the 17.5.0 precedent.
- [x] 3.2 Chris pushes this branch and opens the PR. Verify: the PR's CI is green, parity included.
  *Done (2026-10-20):* PR #56, CI green (Chris).

- [x] 3.3 Chris merges with "Create a merge commit". Verify: `origin/dev/v18` is the merge commit, and
  its push run is green.
  *Done:* `origin/dev/v18` is `1d6f81c`, parents `1776007` and `b80b28d`; CI green (Chris).

## 4. Release through the workflow

- [x] 4.1 Chris pushes tag `18.5.0` on the merge commit. Verify: `check` and `pack` are green.
  *Done, at the second attempt.* The first tag was pushed on `1776007`, the close-out merge before the release: the local branch had not been pulled after the PR merged. `publish.yml`'s first check refused it (run `39506951708`: "The tag is 18.5.0, but 1776007… declares <Version>18.4.0</Version>"), so nothing was packed or published, and nuget.org confirmed neither version existed. The tag was deleted on origin and locally and re-pushed on `1d6f81c` by explicit SHA. Run `39508039968` reached the approval, so `check` and `pack` were green. `18.5.0` resolves to `1d6f81c` locally and on origin.

- [x] 4.2 **Before approving**, check the run's `packages` artifact, parsing each nuspec as XML:
  - the tagged `booking-flow.png` returns 200;
  - there are exactly 5 `.nupkg` and 4 `.snupkg`, all `18.5.0`, each `.snupkg` holding its `.pdb`;
  - all five nuspecs carry the authors, copyright, licence, readme, icon, project URL and the
    merge commit, with `umbraco-marketplace` on `UBookIt` only;
  - every `Umbraco.Cms.*` dependency is `[18.2.1, 19.0.0)`, confirmed against
    `Directory.Packages.props` at the tag, and EF Core SqlServer is `10.0.11`; the dependencies
    between the uBookIt packages are at `18.5.0`;
  - carried from `18.4.0`: `Pkcs` `10.0.13` on exactly Persistence and Web; MessagePack `3.1.9`
    on exactly Backoffice and Web, and no other package declares MessagePack; the
    `Microsoft.AspNetCore.App` framework reference on `UBookIt`, Backoffice and Web; no
    `System.Security.Cryptography.Xml`; the backoffice bundle's only localization culture is `en`;
  - **no dependency differs from the published `18.4.0` packages**, downloaded from nuget.org;
  - **new this release:** `UBookIt.Persistence.dll` contains the migration
    `20261020171452_AddServiceTemplateSet`; the packed backoffice bundle carries the
    `templateSet` label and hint; `UBookIt.Web.dll`'s embedded `package.xml` is the tag's,
    byte-for-byte;
  - the packed README is identical to the tag's, all its addresses return 200, and every
    repository address is pinned to `18.5.0`.

  Show the check script can fail (against the `18.4.0` artifact with this release's expectations).
  *Done:* the script adapted from `18.4.0`'s (the template-set migration and the `templateSet` label and hint replacing last release's checks), run against the artifact Chris downloaded. Merge commit `1d6f81c…`, `[18.2.1, 19.0.0)` confirmed against `Directory.Packages.props` at the tag, the README and `package.xml` taken byte-exact from the tag. Result: **91 of 91 PASS, exit 0**. The 5 dependency comparisons were against the published `18.4.0` packages downloaded from the flat container, and all README addresses returned 200. Negative control: the `18.4.0` artifact with this release's expectations gave **24 FAIL, exit 1**, the migration and label/hint checks among them.

- [x] 4.3 Chris approves. Record the summary table and the conclusion.
  *Done (2026-10-20):* approved after 4.2 (Chris). Run `39508039968` completed with success at 22:31 UTC.

- [x] 4.4 Confirm `18.5.0` per package on the flat container, and that the package page renders.
  *Done:* `18.5.0` is in the flat container index of all five ids. Indexing was staggered: 7 of the 10 packages across both lines were visible first, and all 10 after a few more minutes. `nuget.org/packages/UBookIt/18.5.0` returns 200 to GET and names the version. (nuget.org answers HEAD with 404 for every package page, the long-published `17.5.0` included, so HEAD is not a valid probe.)

## 5. Close out

- [x] 5.1 Stamp the entry's date.
  *Done:* `## 18.5.0 — 2026-10-20`, the publish date.

- [x] 5.2 Sibling sweep for `18.4.0` and for sentences the release falsifies. Fix or record each.
  *Done:* outside `CHANGELOG.md` and `openspec`, `18.4.0` appears only as history: `BookingMessageModels.cs:213` ("Added in 17.5.0 and 18.4.0", the instructions member). The new members' "Added in 17.6.0 and 18.5.0" is now true. No sentence about "the latest/current release" needs moving.

- [x] 5.3 Update the deferred-obligations memory: the `service-email-templates` release-notes
  obligation (a save without `templateSet` clears it) is paid by the entry.
  *Done:* recorded paid in the feature's handover memory (`ubookit-service-email-templates-handover`): the entry says a save without `templateSet` clears it, under *What you have to do*.

- [x] 5.4 Commit and run the unit suite. Chris pushes. Verify: `dev/v18`'s CI is green.
  *Done:* committed on `release-18-5-0-closeout`; unit **2414**, 0 skipped. Chris pushes; `dev/v18`'s CI is his to confirm.
- [x] 5.5 Archive last, running the unit suite before the push.
  *Done (2026-10-20):* archived after the records above. The unit suite and `openspec validate --all --strict` were run after the archive commit.
