## 1. Baseline (main)

- [x] 1.1 Record the audit before any change: `dotnet list UBookIt.slnx package --vulnerable
  --include-transitive`, as Debug and as Release (`-p:Configuration=Release` on restore). Verify:
  the findings match proposal.md's table, or the table is corrected first.
  *Done 2026-10-14 with `scripts/ci/Assert-NoVulnerablePackages.ps1 -Target UBookIt.slnx` (Debug
  and Release, as `audit.yml` runs it): exit 1. Vulnerable: `UBookIt`, `UBookIt.Backoffice`,
  `UBookIt.Tests` (Xml 10.0.10 + Pkcs 10.0.10); `UBookIt.Persistence`, `UBookIt.Web`,
  `UBookIt.Tests.Integration`, `.Rendering`, `.ThemeFixture`, `UBookIt.TestSite` (Pkcs 10.0.0).
  `UBookIt.Core` clean. ImageSharp x5 accepted in the TestSite. Matches the proposal's table.*
- [x] 1.2 Pack every packable project (Release) into a scratch folder and extract each nuspec's
  dependency groups as the "before" set (design D3). Verify: one dependency list per package is
  saved, outside the repository.
  *Done: `dotnet pack UBookIt.slnx -c Release` (as `publish.yml`) into the session scratchpad
  (`pack-before`, 5 nupkgs); dependency groups extracted by a scratch script to
  `deps-before.txt`. 14 dependencies; `UBookIt.Core` declares none.*
- [x] 1.3 Read `System.Security.Cryptography.Xml` 10.0.13's nuspec (NuGet flat container). Record
  whether it depends on `…Pkcs` and at what version (design D2). Verify: recorded in this task.
  *Done: Xml 10.0.13's nuspec (net10.0 group) depends on Pkcs 10.0.13; Pkcs 10.0.13 (net10.0)
  has no dependencies. Both MIT.*
## 2. Pin (main)

- [x] 2.1 Add `PackageVersion` entries for `System.Security.Cryptography.Xml` and
  `System.Security.Cryptography.Pkcs` at `10.0.13` in `Directory.Packages.props`, with a comment
  naming the five advisories and the exit condition (design D5). Add `PackageReference`s to the
  expected set (design D2). Verify: restore succeeds.
  *Done: `PackageVersion` Xml and Pkcs 10.0.13 with the advisories/exit comment;
  `PackageReference` Pkcs in Persistence and Web, Xml in Backoffice. Audit: exit 0, "No
  unaccepted advisories in the graph", 10 projects, Debug and Release, ImageSharp x5 still
  accepted.*
- [x] 2.2 Minimise: for each added `PackageReference`, remove it and re-run the audit (Debug and
  Release). Keep it only if its removal brings an advisory back. Verify: the final set and the
  reason each reference stays are recorded here, and the audit reports no vulnerable package in
  ANY project, test projects and TestSite included.
  *Done. Each reference removed in turn and the audit re-run (Debug and Release), then restored:
  without Persistence's Pkcs -> Persistence and Tests.Integration vulnerable (Pkcs 10.0.0);
  without Web's Pkcs -> Web, Tests.Rendering, Tests.ThemeFixture (Pkcs 10.0.0); without
  Backoffice's Xml -> UBookIt, Backoffice, Tests (Xml 10.0.10). All three stay. (The script's
  own summary counter printed 0 each time because the audit writes its errors to a stream the
  capture missed; the `::error` lines printed directly are the evidence.)*
- [x] 2.3 Run `scripts/ci/Assert-NoVulnerablePackages.ps1` the way `audit.yml` runs it. Verify: it
  passes, and the ImageSharp TestSite exception is still reported as accepted, not stale.
  *Done: as 2.1, the ImageSharp exception reported as accepted, not stale.*
## 3. Prove what ships

- [x] 3.1 Pack again and diff each package's nuspec dependency groups against 1.2 (design D3).
  Verify: the only differences are the added `System.Security.Cryptography.*` `10.0.13`
  dependencies, and each of them was in that project's graph in 1.1.
  *Done: `pack-after` (5 nupkgs) diffed against `deps-before.txt`. The ONLY differences:
  `UBookIt.Backoffice` + System.Security.Cryptography.Xml 10.0.13; `UBookIt.Persistence` +
  System.Security.Cryptography.Pkcs 10.0.13; `UBookIt.Web` + System.Security.Cryptography.Pkcs
  10.0.13. Each package was in that project's graph in 1.1.*
- [x] 3.2 Build and test: client first (`npm run build`), then `dotnet build UBookIt.slnx -c Release`
  and the suites. Verify: 0 errors and **0 warnings** (the NU1902/NU1903 advisories are gone); unit,
  rendering, integration and client counts equal the baselines (unit 2129 on main, or 2130 if
  `backoffice-localization-fallback` has merged first; rendering 1178; integration 210; client 355).
  Any packaging guard that fails is handled per design D3 and recorded here.
  *Done (before `backoffice-localization-fallback` merges, so main's baselines): client 355;
  `dotnet build UBookIt.slnx -c Release --no-incremental` **0 Warning(s), 0 Error(s)**; unit
  2129, rendering 1178, integration 210; `openspec validate --all --strict` 27. No packaging guard
  failed.*
- [x] 3.3 Which copy loads (design D4): `dotnet publish src/UBookIt.TestSite -c Release` to a scratch
  folder, and record the file version of `System.Security.Cryptography.Xml.dll` and `…Pkcs.dll` in
  the output. Verify: recorded here, with what it implies for the changelog sentence, without
  claiming more than was observed.
  *Done 2026-10-14, and it overturned D4 for `Xml`: see 6.1 (Xml.dll absent, pruned; Pkcs.dll
  10.0.1326) and 6.2 (the same after (B)). The run-time half of D4 is in 3.4.*
- [x] 3.4 Live: start the TestSite and confirm the backoffice section loads, a booking can be placed
  from the booking page, and it appears in the backoffice. (DataProtection protects the anti-forgery
  token the booking form relies on, so this exercises the neighbourhood of the framework references.)
  Also, per design D4, start it with host tracing (`COREHOST_TRACE=1`) on this machine's unpatched
  10.0.9 runtime, and record the path the host resolves for `System.Security.Cryptography.Pkcs.dll`
  and `…Xml.dll`. Verify: screenshots or recorded observations, and the two resolved paths.
  *Done 2026-10-15, after the rebase, from this worktree's Release build (`UBookIt.TestSite.exe`
  with `COREHOST_TRACE=1`). Frameworks resolved: Microsoft.AspNetCore.App **10.0.9** (unpatched),
  Microsoft.NETCore.App 10.0.11. Final TRUSTED_PLATFORM_ASSEMBLIES:
  `Pkcs` = the app's own
  `bin/Release/net10.0/runtimes/win/lib/net10.0/System.Security.Cryptography.Pkcs.dll`,
  file **10.0.1326.50504** (the framework's 10.0.926 entry was processed at fx level 1 and not
  added); `Xml` = the shared framework's
  `C:/Program Files/dotnet/shared/Microsoft.AspNetCore.App/10.0.9/System.Security.Cryptography.Xml.dll`,
  file **10.0.926.27113** (the deps.json has no asset for `Xml`, pruned). So on an unpatched
  runtime a site runs the patched `Pkcs` and the vulnerable `Xml`, exactly as D4/D6 state.
  Booking page: booked Closure Live Room, Thursday 15 October 2026 16:00-17:00, as "Crypto Check"
  (`crypto-check@example.test`); "Booking confirmed", reference **7VXV-F3ZR** (anti-forgery token
  accepted). Backoffice over HTTPS (Chris signed in): the uBookIt section loads (Resources list in
  English), and Bookings lists 7VXV-F3ZR, Oct 15 2026 4:00-5:00 PM, Closure Live Room, Booked
  directly, Confirmed. TestSite stopped afterwards.*

## 4. Port to dev/v18 (same sitting)

- [ ] 4.1 Branch from `dev/v18`, cherry-pick, then repeat 1.1–1.3 and 2.2–3.4 there, because Umbraco
  18.2.1's graph is measured separately (design D2). Verify: v18's own minimal set, its nuspec
  diff, 0 warnings, and suites at v18's baselines (unit 2141, or 2142 with the localization change).

## 5. Sweep and hand over

- [ ] 5.1 Search docs, README, CHANGELOG, CLAUDE.md, `openspec/specs/**` and the CI scripts for
  sentences this falsifies (for example, "0 warnings" claims, statements about which advisories
  the audit accepts, or "the packages depend only on Umbraco"). Verify: each hit listed with its
  decision.
- [ ] 5.2 Record in the deferred-obligations memory: the pins, their exit condition (design D5),
  and that the release changelog owes the .NET runtime advice (D4). Verify: the entry exists.
- [ ] 5.3 Handover for the release change (`17.3.2` / `18.2.2`, shared with
  `backoffice-localization-fallback`): the changelog names the five advisories, says the update
  raises the minimum of two Microsoft packages already in the site's graph, states 3.3's observed
  result honestly, and advises taking the October .NET servicing update. Verify: written here.
- [ ] 5.4 QA: one `qa-review` subagent for this change, reused across rounds, handed the claims to
  verify (audit before and after, nuspec diff, counts, publish observation).

## 6. Design correction (2026-10-14, late): Xml is the runtime's, not the package's — option (B)

- [x] 6.1 FINDING while doing 3.3 (`dotnet publish` of the TestSite): `System.Security.Cryptography.
  Xml.dll` is ABSENT from a published site; only the shared framework's copy runs (installed runtime
  10.0.9, file 10.0.926, vulnerable). Cause, read from `obj/project.assets.json`: the .NET 10 SDK
  prunes `System.Security.Cryptography.Xml` `(,10.0.32767]` from every project that references
  `Microsoft.AspNetCore.App` (UBookIt.Web, the TestSite, every Umbraco site). `Pkcs` is NOT pruned:
  10.0.13 (file 10.0.1326) ships in the site and is newer than the framework's (10.0.926). So the
  original `Xml` pin in Backoffice would have been inert at every real site, and design D4's
  expectation was wrong for `Xml`.
- [x] 6.2 Chris chose (B), 2026-10-14: no `Xml` pin; `UBookIt.Backoffice` and `UBookIt` declare
  `<FrameworkReference Include="Microsoft.AspNetCore.App" />` (as `UBookIt.Web` already does), and
  `UBookIt.Tests` too (non-packable). The `Xml` `PackageVersion` is removed; `Pkcs` stays pinned in
  Persistence and Web.
  *Verify:* audit exit 0, "No unaccepted advisories", 10 projects, Debug and Release. (Before
  adding it to UBookIt and UBookIt.Tests, those two still showed Xml 10.0.10: a framework reference
  is not inherited through a project reference for pruning.) Pack diff against `pack-before`
  (dependencies AND framework references): exactly `UBookIt` + frameworkReference
  Microsoft.AspNetCore.App, `UBookIt.Backoffice` + the same, `UBookIt.Persistence` + Pkcs 10.0.13,
  `UBookIt.Web` + Pkcs 10.0.13. Release `--no-incremental`: 0 warnings, 0 errors. Unit 2129,
  rendering 1178, integration 210, client 355, openspec 27. Re-published TestSite: Xml.dll absent
  (framework's), Pkcs.dll file 10.0.1326.
- [x] 6.3 Update the artifacts to (B) BEFORE anything else. Proposal What Changes
  and Impact; design D1/D2 (no Xml pin), D4 corrected with 6.1's observation, new D7 (framework
  reference; Xml is fixed only by the site's .NET runtime); spec: the scenario "An installing site
  resolves the fixed version" needs the case of a package the site's shared framework provides
  (then the site's runtime supplies it, and the requirement asks the package to declare the
  framework rather than pin). Then 3.4 live (TestSite from THIS worktree), 4.1 v18 port, 5.x sweep
  and handover. The release changelog must say: update the site's .NET 10 runtime to the October
  servicing release, which is the ONLY fix for the three Xml advisories (find the exact runtime
  version from Microsoft's release notes first, don't guess).
  *Done 2026-10-15, after rebasing onto main `87c1f49` (the localization change merged and
  archived). Proposal: a paragraph saying the two packages differ, What Changes, Not breaking,
  Capabilities and Impact rewritten for (B). Design: Context gains the pruning and the 10.0.13
  runtime facts; D2 records the measured set; D3 diffs framework references too; D4 corrected; D5
  narrowed to `Pkcs`; the new decision is **D6**, not D7 (the code comments cited D7 with no D6, so
  they were renumbered). Spec: a paragraph and a scenario, "An assembly the shared framework
  supplies is left to the site's runtime". The runtime version was looked up, not guessed:
  Microsoft's `release-metadata/10.0/releases.json` lists 10.0.13 (2026-10-13, security) as
  fixing CVE-2026-96960, -96959, -96958 (Xml) and CVE-2026-84008, -70162 (Pkcs), which are the CVE
  ids of the five GHSAs according to GitHub's advisory API. The proposal's claim that Umbraco's web
  packages declare `Microsoft.AspNetCore.App` was checked: `Umbraco.Cms.Web.Common` and
  `Umbraco.Cms.Api.Common` 17.7.1 nuspecs both do.*
