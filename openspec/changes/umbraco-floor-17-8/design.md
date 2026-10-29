## Context

See proposal.md, *Why*, for the motivation. The current state that shapes the approach:

- `Directory.Packages.props` pins every `Umbraco.Cms*` package to `[17.7.1,18.0.0)` on `main` and
  `[18.2.1,19.0.0)` on `dev/v18`. EF Core is pinned at exactly 10.0.11 on both lines; v18 also pins
  `Microsoft.AspNetCore.OpenApi` at 10.0.11.
- Two security declarations are in force, each with an EXIT comment naming this change:
  - `System.Security.Cryptography.Pkcs` 10.0.13, declared by Persistence and Web;
  - `MessagePack` 3.1.9, declared by Backoffice and Web.

  `UBookIt` reaches both through its project references.
- The audit script (`scripts/ci/Assert-NoVulnerablePackages.ps1`):
  - fails on any advisory in a packable project;
  - accepts a suppression only in a non-packable project;
  - **fails on a suppression that no longer matches a finding.**

  So the TestSite's five ImageSharp suppressions will turn the audit red the moment the floor rises.
  Removing them is forced, not optional tidying.
- The retired-claims sweep (`RetiredClaims.All`, applied by `NotificationDocumentationTests`) holds
  false sentences out of **shipped markdown**. The backoffice's English terms
  (`src/UBookIt.Backoffice/Client/src/localization/en-us.ts`) are shipped text too, but nothing sweeps
  them. That is how three refusal messages kept a claim the docs had already retired.
- Client tests are vitest over **pure functions** (`*-fields.ts`), with no DOM rendering of Lit
  elements. A C# test cannot render a Lit element either.
- Line parity (`Assert-LineParity.ps1`) compares only `.github`, `global.json`, `scripts/ci` and
  `Directory.Build.props`. This change touches none of them, so each line can merge on its own.

## Goals / Non-Goals

**Goals:**
- Each line's packed nuspecs: a raised Umbraco minimum, no `Pkcs` or `MessagePack` dependency, and
  EF Core `>= 10.0.12` on Persistence. Nothing else differs from the last release's artifacts.
- Every packable project still resolves `Pkcs >= 10.0.13` and `MessagePack >= 3.1.9`, now through
  Umbraco. This is proved from the restore graph, not inferred from the nuspecs read on nuget.org.
- The audit is green on both lines with **zero accepted findings**.
- The refusal messages tell the truth about where a grant is given, and a guard keeps the retired
  wording out of the terms.

**Non-Goals:**
- Changing the audit script. Its stale-suppression rule already does what is needed.
- Any change to the permission model or the services editor's behaviour beyond the hint's visibility.

## Decisions

### D1. The floor is 17.8.0 / 18.3.0, not 17.7.2 / 18.2.2

17.7.2 / 18.2.2 fix Umbraco's seven advisories, but they still depend on MessagePack 3.1.8 and do not
declare `Pkcs` (nuspecs read 2026-10-29). With them as the floor, both declarations would have to stay.
17.8.0 / 18.3.0 is the lowest release that satisfies both reasons at once. One floor change serves
both purposes, and a site upgrading to a current Umbraco loses nothing.

### D2. Remove the declarations entirely, and prove the graph still reaches the fixes

The packaging requirement says a declaration "SHALL be removed once a supported Umbraco release
depends on the fixed version". So keeping one as harmless belt-and-braces is not an option.

The proof is taken from each packable project's own restore, after the floor raise. It is not taken
from the nuspecs alone:
- `dotnet list package --include-transitive` on each packable project (or `project.assets.json`);
- it must show `System.Security.Cryptography.Pkcs >= 10.0.13` wherever Pkcs appears, and
  `MessagePack >= 3.1.9` wherever MessagePack appears;
- the audit script must exit 0 with no accepted findings.

Then the packed nuspecs are compared with the published `17.6.0` / `18.5.0` nuspecs, parsed as XML as
in previous releases. The only expected differences are:
- the Umbraco minimum;
- the two removed dependencies;
- EF Core 10.0.12 on Persistence.

*Alternative rejected:* keep the declarations until a later tidy. The requirement forbids it, and each
declaration's EXIT comment names this change.

### D3. EF Core (and v18's OpenApi) move to exactly 10.0.12, not to a floating range

They are pinned exactly today, and the reason is unchanged: the repository builds against one known
version. 10.0.12 is the minimum Umbraco's new floor imposes, so the packed Persistence nuspec's
`>= 10.0.12` adds no constraint a site does not already have.

The migrations' `ProductVersion` annotations record what generated them. They stay as they are, as at
the previous raise.

### D4. The refusal messages name the box, and a guard covers the terms

All three terms get the same wording pattern as the docs (`docs/configuration.md`,
`docs/backoffice.md`): **Users → User Groups**, a group, the box headed **uBookIt**, and the switch by
its label. For example: *"An administrator can grant it in Users → User Groups: open a group and, in
the box headed “uBookIt”, switch on “Change site settings”."* The read message keeps its "or" between
two switches. The settings and write messages keep "It is not granted automatically, including on
upgrade."

**The guard:** a test in `UBookIt.Tests` reads every file under `Client/src/localization/` and applies
`DocumentationAssert.DoesNotSay` with each **user-group-editor** retired claim
(`RetiredClaims.WrongAboutTheUserGroupEditor`). This guarantee spans two languages, so the crossing
guard lives in the test project, which can read both sides. A positive pin goes with it:
each of the three refusal terms says `box headed “uBookIt”`. A negative sweep alone passes over an
emptied term.

*Why only that claim set, not `RetiredClaims.All`:* the other sets are sentences about documentation
pages. Applying them to UI terms would be guarding by coincidence. The user-group-editor claims are
about the Umbraco screen itself, which is exactly what the terms describe.

*Can it fire?* Apply verifies by restoring one old term and seeing the test go red, then reverting.

The stale comments (`UBookItPermissionSeed.cs`, `section/manifest.ts`) are corrected by hand. Comments
are not shipped text, and no guard is proposed for them.

### D5. The hint renders only in Variable mode, decided by a tested pure function

The hint's text describes variable length only. Showing it for **Fixed length** states something false
about the service being edited. Two choices:
- the element renders the hint only when the mode is `variable`;
- or the decision moves into a small pure function in a `*-fields.ts` module (the client's established
  test seam), with a vitest test for both modes.

The element calls the function. **What the test does not cover:** that the element calls it. That
seam is checked by the live pass (task 4), not claimed by a test.

*Alternative rejected:* a separate fixed-mode hint. Fixed length needs no explanation beyond its
field's label, and new copy would be scope this change does not need.

The hint is not referenced by any `aria-describedby`, so removing it from the DOM breaks no
reference.

### D6. v18's client moves to the 18 package, with a stop condition

`@umbraco-cms/backoffice` moves from `^17.5.3` to `^18.3.0` (npm `latest` is 18.3.0). Vite marks
`@umbraco…` imports as external, so the built bundle does not change because of the typings alone.
What changes is what `tsc` and vitest check against. So the expected outcome is either nothing, or
type errors that point at real 18 differences.

**Stop condition:** if `npm run build` or the client tests need more than small, local edits, apply
stops and reports to Chris before going on. Examples of more than small: an API removed with no
direct replacement, or changes in more than a handful of files. That item can then be split into its
own change, without holding up the floor raise.

`LocalizationCultureTests` reads `UMB_DEFAULT_LOCALIZATION_CULTURE` from the installed package. After
the move it reads 18's, which is the intent.

### D7. Order: `main` first, then a port branch for `dev/v18`

Same procedure as `umbraco-security-2026-10`:
- apply and verify on `main` (branch `umbraco-floor-17-8`);
- branch `umbraco-floor-17-8-v18` from `dev/v18` and cherry-pick the change folder, the text, hint and
  guard commits;
- redo the version ranges by hand (they differ per line), add OpenApi, and do the client move.

One QA reviewer covers both branches. No parity-checked path is touched, so the two PRs do not need to
merge in a particular order.

## Risks / Trade-offs

- **[Umbraco 17.8.0 changes the management API or the migrations surface]** → The checklist
  regenerates the client against the running TestSite, reads the diff (expected empty within a
  minor), and runs the integration suite against a real database.
- **[Removing a declaration silently lets a lower version in through a path the nuspec reading
  missed]** → D2's proof uses each project's own restore graph, and the audit runs with zero
  accepted findings.
- **[The ImageSharp advisories' 3.2.0 backport metadata is wrong, so the audit still finds them]** →
  The audit would fail loudly with the findings named. The remedy then is a decision for Chris, not
  re-adding the suppressions quietly.
- **[The localization guard is vacuous]** → It has a positive pin, and D4 requires seeing it fire.
- **[The hint test covers the function, not the element]** → Stated in D5. The live pass confirms
  the element on both modes.
- **[v18's client move grows]** → D6's stop condition.
- **[The audit still cannot see Umbraco's own advisories after this change]** → Accepted, and
  documented in `docs/publishing.md` so a green audit is not read as covering them.

## Migration Plan

No schema or data migration. A site gets the change by updating uBookIt, and NuGet raises its Umbraco
packages to the new floor (README, *Requirements*, says to upgrade `Umbraco.Cms` itself first).
Rollback is installing the previous uBookIt version. That puts back the old declarations, which are
harmless on the newer Umbraco.
