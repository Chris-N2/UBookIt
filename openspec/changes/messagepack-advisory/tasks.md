## 1. Measure, then declare (main)

- [ ] 1.1 Before any edit, record per packable project (`UBookIt`, `UBookIt.Backoffice`,
  `UBookIt.Web`, `UBookIt.Persistence`, `UBookIt.Core`), for Debug and Release, the MessagePack
  version resolved and whether `dotnet list package --vulnerable --include-transitive` reports
  GHSA-qhrr-8q5h-9q3h. Pack all five into a scratch folder as the "before" nuspecs. Verify: the
  table is recorded here, and it matches the proposal's expected set or the difference is
  explained.
- [ ] 1.2 Add `<PackageVersion Include="MessagePack" Version="3.1.9" />` to
  `Directory.Packages.props`, in its own `ItemGroup`, with a comment: the advisory and its date,
  the route through Umbraco, why direct references and not transitive pinning, and the exit
  (design D4). Add `<PackageReference Include="MessagePack" />` to `UBookIt.Backoffice` and
  `UBookIt.Web`, each with a one-line comment pointing at the entry. Verify: Release build,
  0 warnings.
- [ ] 1.3 Measure again (Debug and Release). Verify:
  - every packable project resolves MessagePack 3.1.9, or has none in its graph;
  - `UBookIt.Persistence` and `UBookIt.Core` have no MessagePack;
  - the audit script reports no GHSA-qhrr-8q5h-9q3h anywhere, and still reports the 5 ImageSharp
    TestSite findings as accepted;
  - removing either reference, one at a time, brings the advisory back for that project (the set is
    minimal). Revert after.
- [ ] 1.4 Pack all five again and diff the nuspecs against 1.1's, parsing them as XML (design D3).
  Verify: the only difference is a `MessagePack` dependency at `3.1.9` in `UBookIt.Backoffice` and
  `UBookIt.Web`, apart from the commit id. Show that the comparison can fail: for example, it should
  report the change when run against a nuspec with the reference removed.

## 2. Suites and the runtime smoke test (main)

- [ ] 2.1 Stop any TestSite. Clean the client assets, build the client, then
  `CI=true dotnet build UBookIt.slnx -c Release --no-incremental`. Verify: 0 warnings, 0 errors.
- [ ] 2.2 Run the suites per project. Verify, 0 skipped: unit **2185**, integration **210**,
  rendering **1178**, client **356**; CI's exact openspec command passes after the last commit.
- [ ] 2.3 Smoke test (design D5): start the TestSite on its usual dev database. Check that
  `bin/Debug/net10.0/MessagePack.dll` is version 3.1.9. Fetch the home page and the booking page
  (HTTP 200, expected heading), and open one backoffice uBookIt view. Record the result.
- [ ] 2.4 Confirm NU1605 behaviour for the changelog: in a scratch project that references uBookIt's
  Web project and MessagePack 3.1.8 directly, restore and record whether it is an error or a
  warning. Verify: the result recorded here, with the SDK version.

## 3. Port to dev/v18

- [ ] 3.1 After main's QA approval, cherry-pick onto a branch from `dev/v18`
  (`messagepack-advisory-v18`, worktree `..\uBookIt-v18`). Re-measure 1.1 and 1.3 on Umbraco
  18.2.1's graph, rather than assuming main's set. Verify: the same checks as 1.3 and 1.4; the
  v18 baselines (unit 2197, integration 210, rendering 1178, client 356); 0 warnings; the 2.3
  smoke test on the v18 TestSite.

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
