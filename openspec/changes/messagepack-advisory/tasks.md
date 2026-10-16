## 1. Measure, then declare (main)

- [x] 1.1 Before any edit, record per packable project (`UBookIt`, `UBookIt.Backoffice`,
  `UBookIt.Web`, `UBookIt.Persistence`, `UBookIt.Core`), for Debug and Release, the MessagePack
  version resolved and whether `dotnet list package --vulnerable --include-transitive` reports
  GHSA-qhrr-8q5h-9q3h. Pack all five into a scratch folder as the "before" nuspecs. Verify: the
  table is recorded here, and it matches the proposal's expected set or the difference is
  explained.
  *Done (2026-10-16), the same in Debug and Release; it matches the expected set:*

  | Project | MessagePack | Advisory |
  |---|---|---|
  | `UBookIt` | 3.1.8 | yes |
  | `UBookIt.Backoffice` | 3.1.8 | yes |
  | `UBookIt.Web` | 3.1.8 | yes |
  | `UBookIt.Persistence` | none | no |
  | `UBookIt.Core` | none | no |

  The packs used `publish.yml`'s command: `dotnet pack UBookIt.slnx -c Release
  -p:ContinuousIntegrationBuild=true`. They were run with **`-m:1`**: the first, parallel attempt
  failed with "the process cannot access the file `UBookIt.Web.17.3.2.nupkg`". That is a write
  collision in the output folder; why two pack steps wrote Web at once was not investigated.
  `publish.yml`'s pack, on a CI runner, has not hit it (17.3.2's run packed all five). Serial
  packing produced all 5 `.nupkg` and 4 `.snupkg`.
- [x] 1.2 Add `<PackageVersion Include="MessagePack" Version="3.1.9" />` to
  `Directory.Packages.props`, in its own `ItemGroup`, with a comment: the advisory and its date,
  the route through Umbraco, why direct references and not transitive pinning, and the exit
  (design D4). Add `<PackageReference Include="MessagePack" />` to `UBookIt.Backoffice` and
  `UBookIt.Web`, each with a one-line comment pointing at the entry. Verify: Release build,
  0 warnings.
  *Done:* Release build 0 warnings, 0 errors; the NU1902 warnings the pack had shown are gone.
- [x] 1.3 Measure again (Debug and Release). Verify:
  - every packable project resolves MessagePack 3.1.9, or has none in its graph;
  - `UBookIt.Persistence` and `UBookIt.Core` have no MessagePack;
  - the audit script reports no GHSA-qhrr-8q5h-9q3h anywhere, and still reports the 5 ImageSharp
    TestSite findings as accepted;
  - removing either reference, one at a time, brings the advisory back for that project (the set is
    minimal). Revert after.
  *Done:*
  - `UBookIt`, Backoffice and Web resolve 3.1.9 with no advisory; Persistence and Core still have
    none, in both configurations.
  - The audit exits 0: "5 finding(s) accepted by a suppression … No unaccepted advisories".
  - **Minimal:** without Backoffice's reference, Backoffice reports the advisory, while Web and
    `UBookIt` do not. Without Web's, Web reports it, while Backoffice and `UBookIt` do not.
    `UBookIt` stays fixed in both runs, because it reaches 3.1.9 through whichever reference
    remains. Both references are needed because Backoffice and Web are each shipped on their own.
  - Files restored; the diff stat was identical before and after.
- [x] 1.4 Pack all five again and diff the nuspecs against 1.1's, parsing them as XML (design D3).
  Verify: the only difference is a `MessagePack` dependency at `3.1.9` in `UBookIt.Backoffice` and
  `UBookIt.Web`, apart from the commit id. Show that the comparison can fail: for example, it should
  report the change when run against a nuspec with the reference removed.
  *Done:*
  - The script compares each package's dependency groups (id, version, include/exclude per
    framework) and its framework references. The only differences: `UBookIt.Backoffice` and
    `UBookIt.Web` each **ADDED** `MessagePack 3.1.9 exclude=Build,Analyzers`.
  - `UBookIt` (3 entries), Persistence (4) and Core (0) are identical.
  - **It can fail:** reversed, it reports the same two as REMOVED; against itself it reports 0.
  - **Bug found and fixed:** the first version reported 4 packages as "missing". PowerShell's
    `$before` / `$after` overwrote the `$Before` / `$After` folder parameters, because names are not
    case-sensitive.

## 2. Suites and the runtime smoke test (main)

- [x] 2.1 Stop any TestSite. Clean the client assets, build the client, then
  `CI=true dotnet build UBookIt.slnx -c Release --no-incremental`. Verify: 0 warnings, 0 errors.
  *Done:* no TestSite was running; client assets cleaned and rebuilt; clean CI-mode build 0
  warnings, 0 errors.
- [x] 2.2 Run the suites per project. Verify, 0 skipped: unit **2185**, integration **210**,
  rendering **1178**, client **356**; CI's exact openspec command passes after the last commit.
  *Done, 0 skipped:* client **356**, unit **2185**, integration **210**, rendering **1178**. CI's
  `npx @fission-ai/openspec@1.13.0 validate --all --strict`, run after commit `c5fcb50`: 27 passed,
  0 failed. (Corrected in QA round 1: this line first said the result was "recorded with the
  commit", and the commit message does not record it.)
- [x] 2.3 Smoke test (design D5): start the TestSite on its usual dev database. Check that
  `bin/Debug/net10.0/MessagePack.dll` is version 3.1.9. Fetch the home page and the booking page
  (HTTP 200, expected heading), and open one backoffice uBookIt view. Record the result.
  *Done (2026-10-16):*
  - `bin/Debug/net10.0/MessagePack.dll` is file version 3.1.9.0, product version `3.1.9+aa16e71…`.
  - `/` returned 200, "Book Closure Live Room"; `/book/` returned 200, "Choose what to book". Both
    are published content served through the HybridCache.
  - **The backoffice view was NOT opened:** the browser extension was not connected in this
    session. Instead, `/umbraco` returned 200; uBookIt's API document
    (`/umbraco/swagger/ubookitbackoffice/swagger.json`) returned 200; and its settings endpoint
    refused an unauthenticated call with 401.
  - The site's log had no ERR or FTL lines. The site was stopped afterwards.
- [x] 2.4 Confirm NU1605 behaviour for the changelog: in a scratch project that references uBookIt's
  Web project and MessagePack 3.1.8 directly, restore and record whether it is an error or a
  warning. Verify: the result recorded here, with the SDK version.
  *Done, with SDK 10.0.301 and a `Microsoft.NET.Sdk.Web` project:* it is an **error**. Restore
  reported `error NU1605: Warning As Error: Detected package downgrade: MessagePack from 3.1.9 to
  3.1.8`, through `Site -> UBookIt.Web -> MessagePack (>= 3.1.9)` and `Site -> MessagePack
  (>= 3.1.8)`. The SDK treats NU1605 as an error by default, so the changelog's advice holds: a
  site that references MessagePack directly below 3.1.9 must raise that reference.

- [x] 2.5 QA round 1 (main): **APPROVE WITH NITS**, all seven claims verified independently,
  nothing to fix before merge. The reviewer also:
  - compared MessagePack 3.1.8's and 3.1.9's public members by reflection: 1361 each, differing
    only in how 4 nested type names print, so no binding break is possible;
  - confirmed the analyzer set for Backoffice and Web is unchanged;
  - confirmed MessagePack is not in Web's `packagesToPrune`;
  - found that a parallel pack into a fresh folder succeeded, so 1.1's collision was probably local
    to that folder.

  All three nits were taken:
  - the `Directory.Packages.props` comment now says Umbraco's packages (Web.Common, HybridCache and
    others) bring 3.1.8, rather than Web.Common alone;
  - 2.2 now records the openspec result;
  - the v18 port's smoke test publishes one content node and fetches it, so MessagePack's
    serialize path is exercised too (3.1).

## 3. Port to dev/v18

- [ ] 3.1 After main's QA approval, cherry-pick onto a branch from `dev/v18`
  (`messagepack-advisory-v18`, worktree `..\uBookIt-v18`). Re-measure 1.1 and 1.3 on Umbraco
  18.2.1's graph, rather than assuming main's set. Verify: the same checks as 1.3 and 1.4; the
  v18 baselines (unit 2197, integration 210, rendering 1178, client 356); 0 warnings; the 2.3
  smoke test on the v18 TestSite, **plus publishing one content node and fetching it** (QA round 1
  NIT). The `Directory.Packages.props` comment's versions must be v18's: Umbraco 18.2.1, and an
  exit naming 18.3.0.

## 4. Review

- [ ] 4.1 QA (`qa-review`) in one subagent, reused across rounds and across both lines. Hand it the
  claims (the measurements, the nuspec diff, counts, smoke tests) to verify, not trust. Treat each
  round's fixes as new code.

## 5. Close out

- [ ] 5.1 Update the deferred-obligations memory. The MessagePack section records the declaration
  and its exit, merged with the `Pkcs` exit: the floor raise to stable `17.8.0` / `18.3.0` removes
  both.
- [ ] 5.2 After merge on both lines, archive on both. Then re-scope `release-17-4-0` /
  `release-18-3-0`:
  - remove their "on hold" notes;
  - add the advisory to the changelog entry: what it is, that updating uBookIt fixes it with no
    Umbraco upgrade, and the NU1605 advice from 2.4;
  - redo their audit task (now expected to pass) and their QA.
