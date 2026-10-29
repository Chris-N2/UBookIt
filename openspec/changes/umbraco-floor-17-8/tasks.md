## 1. Dependencies on `main` (branch `umbraco-floor-17-8`)

- [ ] 1.1 Move every `Umbraco.Cms*` entry in `Directory.Packages.props` to `[17.8.0,18.0.0)`, and
      `Microsoft.EntityFrameworkCore.SqlServer` / `.Design` to `10.0.12`. Update the comments that
      name 17.7.1 / 17.7.x. Verify: restore succeeds with no NU1605; `dotnet list package
      --include-transitive` shows every `Umbraco.Cms*` reference at 17.8.0 and nothing at 18.x.
- [ ] 1.2 Remove the `System.Security.Cryptography.Pkcs` and `MessagePack` `PackageVersion` entries and
      their comment blocks. Remove `Pkcs` from `UBookIt.Persistence.csproj` and `UBookIt.Web.csproj`,
      and `MessagePack` from `UBookIt.Backoffice.csproj` and `UBookIt.Web.csproj`. Keep every
      `Microsoft.AspNetCore.App` framework reference. Verify, per design D2:
      - in each packable project's restored graph, Pkcs resolves `>= 10.0.13` wherever it appears and
        MessagePack resolves `>= 3.1.9` wherever it appears (record the versions and the projects);
      - no source file still mentions the removed declarations as current (grep `Pkcs` and
        `MessagePack` outside `CHANGELOG.md` and `openspec/changes/archive`).
- [ ] 1.3 Remove the five ImageSharp `NuGetAuditSuppress` entries and their comment from
      `src/UBookIt.TestSite/UBookIt.TestSite.csproj`. Verify:
      - the TestSite resolves `SixLabors.ImageSharp` 3.2.0;
      - `scripts/ci/Assert-NoVulnerablePackages.ps1` exits 0 against the solution with **0 accepted**
        findings (record its summary line).
- [ ] 1.4 Run the tests that count or name the declarations (`AuditScriptTests` and any packaging or
      composition test that fails after 1.2), and update only those whose subject was the removed
      declarations. Verify: each update is listed here with its reason. No assertion is weakened that
      guards something other than the removed declarations.

## 2. Text and hint (both lines' code; written on `main`)

- [ ] 2.1 Rewrite `notPermitted`, `notPermittedWrite` and `notPermittedRead` in
      `src/UBookIt.Backoffice/Client/src/localization/en-us.ts` per design D4. Correct the stale comments
      in `UBookItPermissionSeed.cs` and `section/manifest.ts`. Verify:
      - no client or server source says "Default permissions" as the place of a grant (grep);
      - the wording matches `docs/configuration.md` and `docs/backoffice.md`.
- [ ] 2.2 Add the localization guard (design D4) to `UBookIt.Tests`:
      - expose the user-group-editor retired claims from `RetiredClaims` (internal, not by copying them);
      - for every file under `Client/src/localization/`, apply `DocumentationAssert.DoesNotSay` with
        each of those claims;
      - pin that each of the three refusal terms says `box headed “uBookIt”`.

      Verify:
      - green;
      - restoring one old term turns it red (record the test and message), and the term is then
        reverted;
      - emptying one term's text turns the positive pin red, and is then reverted.
- [ ] 2.3 Move the hint decision into a pure function in a `*-fields.ts` module (design D5). The
      services editor renders `durationVariableHint` only when that function says so. Add vitest cases
      for `fixed` and `variable`. Verify:
      - the client suite is green (record the count against the 389 baseline);
      - forcing the function to always show the hint turns a test red, and is then reverted.

## 3. Documentation on `main`

- [ ] 3.1 `README.md`: the opening floor sentence and the *Requirements* row say `17.8.0`, including
      "below 17.8.0" and "upgrade `Umbraco.Cms` itself to 17.8.0 first". `docs/getting-started.md`:
      *Requirements* and the upgrade-first sentence say the same. Verify:
      - `UmbracoFloorDocumentationTests` is green, with its patterns moved to 17.8.0;
      - no shipped page states 17.7.1 as the floor (grep README and `docs/`).
- [ ] 3.2 `docs/publishing.md`, *Continuous integration*:
      - replace "Today the only accepted advisories are `UBookIt.TestSite`'s…" with the true state
        (none);
      - add Umbraco's own repository advisories to *What the audit cannot see*: not in the database
        NuGet reads, and filed against `Umbraco.Cms`, which no shipped package references. The check is
        to read Umbraco's advisories when Umbraco releases.

      Verify: the documentation suites (`DocumentationLinkTests`, the publishing-runbook guards) are
      green.

## 4. Verify `main`

- [ ] 4.1 Run the floor-raise checklist on `main`:
      - build the client first, then a clean `--no-incremental` Release build: 0 warnings, 0 errors;
      - regenerate the backoffice client against the running TestSite, read the diff, and record it
        (expected empty ignoring CR); restore the working copies;
      - confirm the migration list still holds (no new migration is needed or generated);
      - the four suites, separately and in order: client, unit, integration, rendering, against the
        baselines (client 389, unit 2491, integration 222, rendering 1211) plus this change's delta;
      - the audit exits 0 with 0 accepted findings.

      Verify: each result recorded here.
- [ ] 4.2 Live pass on the TestSite, running 17.8.0, in Chrome:
      - the section and all its views load;
      - a booking is placed on the front end and found on the Bookings screen;
      - as a user without *Change site settings*, the Settings and Closures views show the corrected
        messages;
      - in the services editor, the hint shows with **Variable length** and not with **Fixed length**,
        switching both ways;
      - no uBookIt console errors.

      Verify: each observation recorded, with the booking reference.
- [ ] 4.3 Pack Release and compare each nuspec with the published `17.6.0` one, parsed as XML (design
      D2). Verify: the only differences are the Umbraco minimum, the removed `Pkcs` / `MessagePack`
      dependencies, and EF Core `10.0.12` on Persistence (record the diff).
- [ ] 4.4 `openspec validate --all --strict` after the last commit. Verify: all pass.

## 5. Port to `dev/v18` (branch `umbraco-floor-17-8-v18`)

- [ ] 5.1 Branch from `dev/v18` and unset any upstream to `origin/dev/v18`. Cherry-pick the change
      folder, the text/guard/hint commits and the `docs/publishing.md` commit. Redo by hand:
      - the floor as `[18.3.0,19.0.0)`;
      - EF Core and `Microsoft.AspNetCore.OpenApi` at `10.0.12`;
      - the declaration and suppression removals (1.2, 1.3), checking v18's own graph rather than
        assuming it;
      - the README and `getting-started.md` in v18's wording (18.3.0).

      Verify: as 1.1–1.3 and 3.1, at 18.3.0.
- [ ] 5.2 Move the client's `@umbraco-cms/backoffice` to `^18.3.0` and regenerate `package-lock.json`
      (design D6). Verify: `npm run build` and `npm test` are green. **If they need more than small,
      local edits, stop and report to Chris before going on.** Record every edit made for 18's typings.
- [ ] 5.3 Run 4.1–4.4 on v18, at v18's baselines (unit 2485) plus the delta, against the published
      `18.5.0` nuspecs. The live pass uses an 18.3.0 TestSite. Verify: each result recorded.

## 6. QA, merge, archive

- [ ] 6.1 QA in a fresh subagent, one reviewer covering both branches and reused across rounds.
      Hand over the claims (build state, test counts, audit output, nuspec diffs, live observations)
      to be verified rather than trusted. Allow reverted mutations. Verify: APPROVE.
- [ ] 6.2 Push both branches and open PRs; merge each once its CI is green and QA has approved.
      Verify: PR numbers, run IDs and merge SHAs recorded.
- [ ] 6.3 Run the `audit` workflow on demand after both merge. Verify: green on both lines with no
      `Accepted advisory` notices; run ID recorded.
- [ ] 6.4 Archive on each line, after an outward search of `openspec/specs` and `docs/` for any
      sentence the floor raise or the removals falsify. Verify: `openspec validate --all --strict`
      passes on both lines, and the search result is recorded.
- [ ] 6.5 Then the release change (`17.7.0` / `18.6.0`). It carries the changelog: the Umbraco
      advisories and new floor, the removed declarations, the fixed messages and hint, and the
      `docs-single-source` debt. It also carries the Trusted Publishing org-policy observation, and
      **Chris is told to disable his personal policy before the publish step.**
