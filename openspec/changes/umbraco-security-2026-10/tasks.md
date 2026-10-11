## 1. The audit exception (`main` first; the script is identical on both lines)

- [x] 1.1 Add `-Report <path>` to `scripts/ci/Assert-NoVulnerablePackages.ps1` (design D4). With it,
      the script reads that JSON instead of running `dotnet list`, and everything after the read
      is unchanged. Verify: running it with no `-Report` against the restored solution behaves
      exactly as before. It fails today, naming the five ImageSharp findings in `UBookIt.TestSite`.
- [x] 1.2 Implement design D3's four outcomes:
      - accepted findings produce a `::notice` and are counted;
      - stale suppressions fail;
      - a suppression in a packable project fails;
      - an unmatched finding fails, as before.

      Read suppressions and `IsPackable` from each project's own file as XML, and state that
      limit, and why it errs safe, in the script's header. The summary line states the accepted
      count. Verify by 1.3's tests.
- [x] 1.3 Add a test class to `UBookIt.Tests` that runs the script through `pwsh` with `-Report`
      against fixtures written to a temp directory. Cover one case per row of D3, plus:
      - a clean report, which passes;
      - an unconsulted source, which fails;
      - no projects, which fails;
      - a suppression in one project while the same advisory is found in another (spec: *A
        suppression does not reach another project*).

      Each test asserts the exit code AND the output names the thing it should. Verify: green,
      and each new branch in the script, when disabled by hand, turns at least one test red
      (record which). *(14 tests. Mutations, each caught: accept-never-matches -> 4 tests; stale check removed -> the stale test; packable check removed -> 3; packable-may-accept -> the matching-packable test; conditional suppression honoured -> the conditional test; conditional IsPackable honoured -> its test; normalisation removed -> the case/slash test. Apply found a real bug on 1.1's first run: the parsed report was assigned to ``, which is the `[string]` `-Report` parameter under case-insensitive names, so every property read empty. Renamed to `` with a comment.)*
- [x] 1.4 Declare the five ImageSharp advisories as `NuGetAuditSuppress` in
      `src/UBookIt.TestSite/UBookIt.TestSite.csproj`, under a comment giving the reason (design
      D2). Verify: a restore shows no NU190x warnings, and the script with no `-Report` exits 0,
      reporting 5 accepted findings, all in `UBookIt.TestSite`. *(Measured: 0 NU190x on restore. The script exits 0: `audited 10 project(s) in UBookIt.slnx; 5 finding(s) accepted`.)*

## 2. The Umbraco floor on `main`

- [x] 2.1 Move all seven `Umbraco.Cms.*` entries in `Directory.Packages.props` to
      `[17.7.1,18.0.0)`. Verify: restore resolves 17.7.1 for every `Umbraco.Cms*` package
      (check `project.assets.json` or `dotnet list package`), and nothing resolves 18.x.
      *(`dotnet list --include-transitive`: 73 `Umbraco.Cms*` references, all 17.7.1. **Found at
      apply:** restore failed with NU1605. Umbraco.Cms.Persistence.EFCore 17.7.1 requires
      Microsoft.EntityFrameworkCore.SqlServer >= 10.0.11 against our 10.0.10 pin. Both EF Core
      pins (SqlServer, Design) moved to 10.0.11 together, so shipped `UBookIt.Persistence` now
      declares >= 10.0.11, the same requirement 17.7.1 imposes. The migrations'
      `ProductVersion "10.0.10"` annotations are generation-time history and stay.)*
- [x] 2.2 `README.md`: the opening's floor sentence and the Requirements table row say `17.7.1`.
      Verify: no README sentence states `17.6.2` as the floor, and the README guards pass.
- [x] 2.3 Run the design D5 checklist on `main`:
      - a clean `--no-incremental` Release build with 0 warnings (record the count);
      - client regeneration against the running TestSite, with the diff read and recorded
        (expected empty);
      - the four suites, separately and in order;
      - the live pass: the section and workspaces register, and a booking is placed on the front
        end and seen on the Bookings screen;
      - the vulnerable-package listing shows only the 5 accepted findings.

      Verify: each result recorded here. *(2026-10-11, `main`:
      - clean `--no-incremental` Release build: **0 warnings, 0 errors**;
      - client regenerated against the running TestSite: the files differ only in line endings,
        and `git diff --ignore-cr-at-eol` is empty, so the API surface is unchanged across
        17.6 → 17.7 (the working copies were restored);
      - client 335, unit 2019 (2005 + 14), integration 194, rendering 1168, all passing;
      - live, in Chrome: the uBookIt section with all five views, and Resources rendering its data;
        booking PX85-4CZR placed on the harness for Closure Live Room, Mon 12 Oct 11:00–12:00,
        and found by reference on the Bookings screen as Confirmed; no console errors;
      - the audit script against the solution exits 0, with 5 accepted, all in `UBookIt.TestSite`.)*
- [x] 2.4 `openspec validate --all --strict`. Verify: all pass. *(27/27.)*

## 3. Port to `dev/v18`

- [x] 3.1 Branch `umbraco-security-2026-10-v18` from `dev/v18`. Cherry-pick the script, tests and
      change folder. Re-do the floor by hand as `[18.2.1,19.0.0)`, and the README on v18's
      wording, including "upgrade `Umbraco.Cms` itself to 18.2.1 first". Add the same TestSite
      suppressions, if v18's graph has the same five findings; check, don't assume. Verify:
      `git diff` between the two branches for `scripts/ci/` is empty. *(Proposal and audit
      commits cherry-picked cleanly; the floor and README were redone by hand. The README had four
      `18.2.0` floor mentions: the opening, the table row, "a site below", and "upgrade … first".
      EF Core was already 10.0.11 on this line. v18's graph has the same five ImageSharp findings
      in `UBookIt.TestSite`, so the cherry-picked suppressions apply as they stand. The
      `scripts/ci/` and `AuditScriptTests.cs` diff against `main`'s branch is empty. Note: the
      branch was created tracking `origin/dev/v18` and the upstream was unset straight away, so a
      bare `git push` cannot land on `dev/v18`.)*
- [x] 3.2 Run the design D5 checklist on v18, as in 2.3, at v18's baselines (unit 2017 plus this
      change's test delta). Verify: each result recorded. *(2026-10-11, v18:
      - 73 `Umbraco.Cms*` references, all 18.2.1;
      - clean `--no-incremental` Release build: **0 warnings, 0 errors**;
      - client regenerated from the v18 OpenAPI endpoint: empty content diff ignoring CR, so the
        surface is unchanged across 18.2.0 → 18.2.1;
      - client 335, unit 2031 (2017 + 14), integration 194, rendering 1168, all passing;
        openspec 27/27;
      - the audit script exits 0, with 5 accepted, all in `UBookIt.TestSite`;
      - live, TestSite restarted on this branch: section and all five views, Resources rendering
        v18's own data; booking 2SMJ-2MJC placed for DBO Granted, Mon 12 Oct 09:30–10:00, and found
        by reference on the Bookings screen as Confirmed. No console errors after load. The four
        errors logged during the restart and re-login (SignalR 1006, token refresh, icon fetch,
        auth-view `_subject`) are all in Umbraco core and timestamped before the pass.
      - **Observed, not investigated, not attributed:** a direct navigation to
        `/umbraco/section/ubookit/view/bookings` rendered with Resources highlighted and an empty
        body; clicking the Bookings tab worked. Never tried on 17 or before this change, so whether
        it is new is unknown.)*

## 4. QA, merge, archive

- [ ] 4.1 QA in a fresh subagent, covering both branches in one review. Hand over the claims and
      the evidence, to be verified rather than trusted. Verify: APPROVE.
- [ ] 4.2 Chris pushes both branches and opens PRs. CI is expected red on parity until both
      branches carry the script (design D6), and goes green once both are pushed. Verify: run IDs
      recorded.
- [ ] 4.3 After both merge, run the `audit` workflow on demand. Verify: green, with 5 accepted
      notices per line, and its run ID recorded.
- [ ] 4.4 Archive on each line, syncing the MODIFIED `continuous-integration` requirement
      *Vulnerability advisories are audited separately from the build*, after an outward search of `openspec/specs` and `docs/` for any
      sentence the floor raise or the exception falsifies. Verify: `openspec validate --all
      --strict` passes on both lines, and the search result is recorded.
- [ ] 4.5 Then the releases: `17.2.3` and `18.1.3` as their own release changes, carrying the
      version bump, the changelog entry naming the Umbraco advisories and the new floor, and the
      first scripted audit exception.
