## 1. Baseline (main)

- [ ] 1.1 Record the audit before any change: `dotnet list UBookIt.slnx package --vulnerable
  --include-transitive`, as Debug and as Release (`-p:Configuration=Release` on restore). Verify:
  the findings match proposal.md's table, or the table is corrected first.
- [ ] 1.2 Pack every packable project (Release) into a scratch folder and extract each nuspec's
  dependency groups as the "before" set (design D3). Verify: one dependency list per package is
  saved, outside the repository.
- [ ] 1.3 Read `System.Security.Cryptography.Xml` 10.0.13's nuspec (NuGet flat container). Record
  whether it depends on `…Pkcs` and at what version (design D2). Verify: recorded in this task.

## 2. Pin (main)

- [ ] 2.1 Add `PackageVersion` entries for `System.Security.Cryptography.Xml` and
  `System.Security.Cryptography.Pkcs` at `10.0.13` in `Directory.Packages.props`, with a comment
  naming the five advisories and the exit condition (design D5). Add `PackageReference`s to the
  expected set (design D2). Verify: restore succeeds.
- [ ] 2.2 Minimise: for each added `PackageReference`, remove it and re-run the audit (Debug and
  Release). Keep it only if its removal brings an advisory back. Verify: the final set and the
  reason each reference stays are recorded here, and the audit reports no vulnerable package in
  ANY project, test projects and TestSite included.
- [ ] 2.3 Run `scripts/ci/Assert-NoVulnerablePackages.ps1` the way `audit.yml` runs it. Verify: it
  passes, and the ImageSharp TestSite exception is still reported as accepted, not stale.

## 3. Prove what ships

- [ ] 3.1 Pack again and diff each package's nuspec dependency groups against 1.2 (design D3).
  Verify: the only differences are the added `System.Security.Cryptography.*` `10.0.13`
  dependencies, and each of them was in that project's graph in 1.1.
- [ ] 3.2 Build and test: client first (`npm run build`), then `dotnet build UBookIt.slnx -c Release`
  and the suites. Verify: 0 errors and **0 warnings** (the NU1902/NU1903 advisories are gone); unit,
  rendering, integration and client counts equal the baselines (unit 2129 on main, or 2130 if
  `backoffice-localization-fallback` has merged first; rendering 1178; integration 210; client 355).
  Any packaging guard that fails is handled per design D3 and recorded here.
- [ ] 3.3 Which copy loads (design D4): `dotnet publish src/UBookIt.TestSite -c Release` to a scratch
  folder, and record the file version of `System.Security.Cryptography.Xml.dll` and `…Pkcs.dll` in
  the output. Verify: recorded here, with what it implies for the changelog sentence, without
  claiming more than was observed.
- [ ] 3.4 Live: start the TestSite and confirm the backoffice section loads, a booking can be placed
  from the booking page, and it appears in the backoffice. (DataProtection signs the anti-forgery
  token the booking form relies on, so this exercises the pinned `Xml` package's neighbourhood.)
  Verify: screenshots or recorded observations.

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
