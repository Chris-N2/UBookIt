## 1. Machine and baselines (before anything changes)

- [x] 1.1 Tell Chris, then install Node 24 machine-wide with winget, naming the version explicitly:
      `OpenJS.NodeJS.LTS` now resolves to 26 (design D7). Verify:
      - `node --version` reports `v24.x` with x >= 13;
      - `npm --version` reports 11.x;
      - in the main worktree, a fresh `npm ci` in `Client/` prints no `EBADENGINE` warning for any
        direct dependency.

      **Done 2026-11-13.** Chris told, UAC approved. `winget install --id OpenJS.NodeJS.LTS --version
      24.22.1 --exact` replaced `OpenJS.NodeJS.22` 22.13.0 in `C:\Program Files\nodejs`. `node` v24.22.1,
      `npm` 11.19.1. `npm ci` on `main` (still 0.85.2 / 17.5.3): no `EBADENGINE` line. New with npm 11.19:
      a warning that `esbuild@0.28.1`'s postinstall is "not yet covered by allowScripts". It is a
      warning only: `npx esbuild --version` gives 0.28.1, because the binary comes from the optional
      platform package. Also reported: 41 audit findings (32 moderate, 7 high, 2 critical) in the
      dev tree before the upgrade; re-counted after 3.1 for comparison, and not in this change's scope.
- [x] 1.2 Record each line's baseline **before** touching it, `main` then `dev/v18`. On each:
      - build the client first, then a clean Release build: 0 warnings, 0 errors;
      - record the unit, integration and rendering totals, and the vitest total.

      Run the two lines one at a time, never chained, because the machine has 16 GB. Verify: the
      four figures per line are recorded here, and each unit total matches that line's figure in
      memory, or the difference is explained.

      **Done 2026-11-13, under Node 24.22.1.** `npm ci`, then `npm run build` and `npm test`, then
      `dotnet build UBookIt.slnx -c Release --no-incremental` (after deleting `obj/Release`), then
      each suite with `--no-build`:

      | Line | Build | Unit | Integration | Rendering | Vitest |
      |---|---|---|---|---|---|
      | `main` (`192b955`) | 0 warnings, 0 errors | 2496 | 222 | 1211 | 391 |
      | `dev/v18` (`57ad4f1`) | 0 warnings, 0 errors | 2490 | 222 | 1211 | 391 |

      Unit, integration and rendering totals match memory. Memory's client figure (389) is stale: 391
      was already the explore's count on `dev/v18`, and both lines give 391 here.
- [x] 1.3 On `main` (branch `backoffice-client-upgrade`), with the TestSite running, save the live
      backoffice OpenAPI document to the scratchpad. Regenerate with the **current** 0.85.2 from it
      (design D1). Verify: `git diff --stat -- src/UBookIt.Backoffice/Client/src/api` is empty. If it
      is not, stop and report: the committed client is stale against its own server.

      **Done.** Document saved to the scratchpad (`apply\main-ubookitbackoffice.json`, 130,414 bytes).
      `npm run generate-client` under 0.85.2: `git diff --stat` empty and `git diff --quiet` exit 0.
      `git status` lists `types.gen.ts` as `M`, but that is line endings only (LF written, CRLF
      checked out); there is no content difference.
- [x] 1.4 On `main`, run the live check of design D4 (steps 1–4) **before** the upgrade, and record
      what each step showed. This is the comparison point for 4.2.

      **Done**, on the TestSite with Chris logged in (0.85.2 / 17.5.3 bundle):
      1. Bookings, window 2026-10-16 to 2026-11-15: GET `bookings?...` 200, 7 rows, "Showing 1–7 of
         7". *Extra point, found by accident:* a 67-day window gives GET 400, and the section shows
         the server's message inline: "The window 2026-09-10 to 2026-11-15 covers 67 days, which
         exceeds the maximum of 31." No toast.
      2. Closure 2027-03-10 "CLU live check (delete me)": POST 200, and the row is listed. Delete:
         DELETE 200, and the list is back to its original 10 rows.
      3. Holiday 404. **Method:** with the Closures view open, `window.fetch` is replaced for the path
         `/umbraco/ubookitbackoffice/api/v1/closures/holidays` only (preview GET and import POST).
         It answers with the server's own sourceless 404 body:
         `{type:"NotFound", title:"This site has no public holiday source.", status:404}`,
         `application/problem+json`. Script: scratchpad `apply\holiday-404-stub.js`. Because the
         generated client resolves `globalThis.fetch` per request (in both 0.85.2 and 0.99), the real
         runtime and Umbraco's interceptors handle the failure; only the transport is faked. (Truly
         deregistering the source needs it to vanish while the screen is open, which tidy-2026-10 could
         not reproduce live.) **Result:** Import → Fetch holidays makes one stubbed request. The whole
         "Public holidays" panel is withdrawn, with no import control left and no toast. After a reload
         with the real fetch, the panel is back.
      4. Closure from 2027-03-10 to 2027-03-05: POST 400. The section shows "A closure cannot end
         (2027-03-05) before it starts (2027-03-10).", with `_errorControl` = `closure-end-date`, and
         **no toast**. Design D4 expected a backoffice error notification as well. None appears on the
         un-upgraded line, so "as before" means section message only, no toast. The list is still 10
         rows, with no `CLU` row.

      Console: no errors from the section.

## 2. Hand-written changes (on `main`; cherry-picked in 5)

- [x] 2.1 Update `scripts/generate-openapi.js` per design D2: `runtimeConfigPath: './src/hey-api'`, and
      `operations: { strategy: 'byTags', containerName: '{{name}}Service' }` in place of
      `asClass` / `classNameBuilder`. Verify: the script contains neither deprecated option, and the
      generation in 3.2 prints no deprecation warning.

      **Done** (`3058276`). Neither deprecated option is configured. `grep "asClass\|classNameBuilder"`
      matches only line 48, the comment that says what `operations` replaces.
      3.2's generation printed no deprecation warning: the output has no "deprecat" anywhere.
- [x] 2.2 Set `node-version: 24` on the `actions/setup-node` step in `.github/workflows/ci.yml` and
      `.github/workflows/publish.yml`. Correct the Requirements line of
      `src/UBookIt.Backoffice/README.txt` from "Node LTS Version 20.17.0+" to Node 24.13 or later,
      saying it follows `@umbraco-cms/backoffice`'s declared engine. Verify: a grep of the repo,
      excluding `openspec/changes/archive` and `node_modules`, finds no `node-version: 22` and no
      `20.17`.

      **Done** (`3058276`). `git grep -E "node-version: 22|20\.17"` outside the archive finds only this
      change's own artifacts, which quote the old text. A wider `git grep -i "node 22"` finds nothing
      in docs or specs.
- [x] 2.3 Add the Node-engine guard to `UBookIt.Tests` per design D5. Verify:
      - green after 3.1;
      - **it can fire:**
        - setting either workflow's `node-version` back to `22` turns it red, with a message naming
          the workflow, the pin and the requirement;
        - a non-major pin (`24.x`) turns it red as unreadable, not as passing;
        - each mutation is reverted, and the revert is confirmed by content comparison;
      - the non-empty checks hold: zero `setup-node` steps found, or zero direct dependencies with
        an `engines.node`, fails the test.

      **Done.** `NodeEngineGuardTests`, 26 tests, built to amended D5 (`||` of `>=` and `^`). Green
      after 3.1 (26/26, and within 3.4's 2522). **It can fire** (after 3.1, lockfile at 17.8.1
      `>=24.13`). Each mutation was a single exact replacement, checked to have changed the file's
      hash, and each revert was confirmed by the hash matching the original:
      - `ci.yml` `24`→`22`: red, ".github/workflows/ci.yml job 'verify' pins node-version 22, but
        @umbraco-cms/backoffice 17.8.1 requires Node '>=24.13'."
      - `publish.yml` `24`→`22`: red, the same message naming `publish.yml` job 'pack'.
      - `ci.yml` `24`→`24.x`: red as unreadable, "cannot read node-version '24.x'", not passing.

      **Non-empty checks:** `No_pins_fails` and `No_engine_ranges_fails` hold over `Problems`. The real
      test also asserts that `ci.yml` and `publish.yml` each contribute a pin, and
      `The_lockfile_reading_finds_the_backoffice_package` asserts that the package the guard exists
      for is read. Unreadable ranges (`~`, `<`, x-range, hyphen, `^0.x`, compound `>= <`, empty) and
      pins (`24.x`, `lts/*`, `24.13.0`, absent) each have a case.

## 3. Upgrade and regenerate on `main`

- [x] 3.1 In `Client/`, run `npm install --save-dev @hey-api/openapi-ts@^0.99.0
      @umbraco-cms/backoffice@^17.8.1`. Do not use `--legacy-peer-deps` or `--force`. Verify:
      - no ERESOLVE and no `EBADENGINE`;
      - `npm ls @hey-api/openapi-ts @umbraco-cms/backoffice` shows 0.99.x and 17.8.x;
      - the resolved versions are recorded here.

      **Done.** No ERESOLVE and no `EBADENGINE`. `npm ls` resolves `@hey-api/openapi-ts@0.99.0` and
      `@umbraco-cms/backoffice@17.8.1`, with the backoffice's own peer `@hey-api/openapi-ts@0.99.0
      deduped`. "added 11, removed 97, changed 60". `npm audit` moved from 41 findings (32 moderate,
      7 high, 2 critical) to 12 (3 moderate, 8 high, 1 critical). That is dev-tree only, recorded for
      comparison, and not in scope. The esbuild allowScripts warning is unchanged from 1.1.
- [x] 3.2 With the TestSite running, run `npm run generate-client`. Verify, against 1.3:
      - `types.gen.ts` is unchanged, or every difference is listed here and judged against design D3;
      - the `UBookItBackofficeService` method names are identical (sorted list diff);
      - `client.gen.ts` imports `'../hey-api'`;
      - the diff stat is recorded.

      **Done**, against the live `main` TestSite under 0.99.0:
      - **`types.gen.ts`: one line differs, and it is type-equivalent.** `ProblemDetails`'s index
        signature `[key: string]: unknown | string | null | ... | undefined` became
        `[key: string]: unknown`. `unknown` already absorbs that union, so the type is the same. It is
        main's Swashbuckle (OpenAPI 3.0) document producing a redundant union that 0.99 collapses; on
        `dev/v18`'s Microsoft.AspNetCore.OpenApi document the explore saw no difference. Judged against
        D3: not a contract change, and the guards in 3.4 decide.
      - **Method names:** sorted lists before and after are 37 and 37, and `Compare-Object` finds no
        difference. `export class UBookItBackofficeService` is unchanged.
      - **`sdk.gen.ts`:** with whitespace stripped and the 37 added `: RequestResult<XResponses,
        XErrors, ThrowOnError>` return types removed, the class body is byte-identical (12,981 bytes
        each, `cmp` equal). The rest is the `Options` type gaining `TResponse` and `ClientMeta`, and
        the multi-line `security` blocks collapsed onto one line.
      - `client.gen.ts` imports `'../hey-api'` and now annotates `client: Client`. `index.ts` moved to
        named exports, as predicted.
      - Diff stat: 16 files, +428 / −710 (`client/**` and `core/**` runtime rewritten).
- [x] 3.3 Typecheck, test and build the client: `npx tsc --noEmit`, `npm test`, `npm run build`. Verify:
      - all green, with the vitest total equal to 1.2's;
      - **any section-code edit made to compile against 17.8.1** is listed here with its reason. A
        fix that is not narrow is raised with Chris before it is made.

      **Done.** `npx tsc --noEmit` exit 0; vitest 391/391 (equal to 1.2); `npm run build` succeeds.
      **No section-code edit** was needed to compile against 17.8.1.
- [x] 3.4 Delete `wwwroot/App_Plugins/UBookItBackoffice` and `src/UBookIt.Backoffice/obj/Release`.
      Then do a clean Release build (0 warnings, 0 errors), and run the unit, integration and
      rendering suites. Verify:
      - totals equal 1.2's, plus the guard's own tests from 2.3;
      - `GeneratedClientTests` and `OpenApiTransformerTests` pass **without edits** (design D3);
      - `git diff -- tests/UBookIt.Tests/GeneratedClientTests.cs tests/UBookIt.Tests/OpenApiTransformerTests.cs`
        is empty.

      **Done.** TestSite stopped, bundle folder and `obj/Release` deleted, then `dotnet build
      UBookIt.slnx -c Release --no-incremental`: 0 warnings, 0 errors. The bundle was rebuilt (39
      files). Unit **2522** = 2496 + 26 (the guard's own tests from 2.3), integration **222**,
      rendering **1211**: all equal to 1.2's. `GeneratedClientTests` run alone gives 2/2 passed. The
      diff of `GeneratedClientTests.cs` / `OpenApiTransformerTests.cs` is empty.
      **`OpenApiTransformerTests` does not exist on `main`**: it is `dev/v18`-only (design D3,
      corrected). On `main` the method names are checked by 3.2's comparison, not by a test.

## 4. Live check on `main`

- [x] 4.1 Start the TestSite on the rebuilt output, and confirm in the browser that the uBookIt
      section loads with no console error from the section's own scripts. Verify: a screenshot, and
      a console read filtered to the section.

      **Done.** `obj/Debug` deleted as well, then `dotnet run` on the rebuilt output, with the session in
      a fresh tab. Bookings view screenshot:
      `C:\Users\cnorw\AppData\Local\Temp\claude-chrome-screenshots-x4R5b5\screenshot-1794569078789-0.jpg`.
      The section's scripts come from the rebuilt bundle. Console errors over the whole session: **none
      from the section**. Two came from Umbraco's own auth module at first load,
      `[UmbAuthClient] Token request failed: 400` (`packages/core/auth/index.js`). They are attributed
      to the session's refresh token surviving a TestSite restart, not to this change; 5.2's
      pre-upgrade restart on `dev/v18` is the check on that attribution.
- [x] 4.2 Run design D4 steps 1–4 again and compare each with 1.4. Verify:
      - each step's outcome matches 1.4: the list loads; the closure is created and deleted; the
        holiday-source 404 withdraws the panel; the invalid closure shows the same notification and
        message;
      - any difference is a finding, recorded here and raised before continuing;
      - the closure from step 2 is confirmed gone.

      **Done. Every step matches 1.4:**
      1. Bookings, 2026-10-16 to 2026-11-15: GET 200, 7 rows, "Showing 1–7 of 7". The 67-day window
         gives GET 400 and the same inline message, with no toast.
      2. Create 2027-03-10 "CLU live check (delete me)": POST 200, rows 10 → 11. Delete: DELETE 200,
         back to 10 rows, 0 `CLU`.
      3. Holiday 404 (same stub): one stubbed GET. The panel is withdrawn, with neither Import nor
         Fetch controls left and no toast. **Control, new in 4.2:** the same stub answering **500**
         keeps the panel and shows "The site's holiday source could not be reached, so there is
         nothing to show. This is not the same as there being no holidays…". So the withdrawal is the
         404 being classified from the thrown status, not every failure removing the panel. *(1.4 did
         not run this control. It is run on `dev/v18` before and after.)*
      4. Closure 2027-03-10 to 2027-03-05: POST 400, "A closure cannot end (2027-03-05) before it
         starts (2027-03-10).", `_errorControl` = `closure-end-date`, no toast. Cancelled, 10 rows,
         0 `CLU`.

      No difference from 1.4, so no finding. The step-2 closure is confirmed gone (row count and name
      search).

## 5. Port to `dev/v18` (worktree `..\uBookIt-v18`, branch `backoffice-client-upgrade-v18`)

- [x] 5.1 Branch from `origin/dev/v18`, and cherry-pick the hand-written commits of section 2 and the
      openspec artifacts. Do **not** cherry-pick `package.json`, `package-lock.json` or `src/api`
      (design D6). Verify: `git diff origin/dev/v18 --stat` lists only hand-written files.

      **Done.** `git checkout --no-track -b backoffice-client-upgrade-v18 origin/dev/v18` (`57ad4f1`),
      with no upstream. Cherry-picked `635d50b` (artifacts) and `3058276` (hand-written) without
      conflict. `git diff origin/dev/v18 --stat` lists 9 files, all hand-written or openspec:
      the two workflows, `generate-openapi.js`, `README.txt`, `NodeEngineGuardTests.cs`, and the 4
      artifact files. The later artifact edits on `main` are brought across as files at close (6.1),
      since `main`'s upgrade commit mixes them with generated files.
- [x] 5.2 Repeat 1.3 and 1.4 on `dev/v18`, the baseline regeneration and the pre-upgrade live check,
      against the v18 TestSite (`/umbraco/openapi/ubookitbackoffice.json`). Verify as there.

      **Done.**
      - **Baseline regeneration.** The cherry-picked script already carries the 0.99 options, so the
        baseline ran with `origin/dev/v18`'s script (`git checkout origin/dev/v18 -- scripts/...`),
        restored to HEAD afterwards and confirmed by `git diff HEAD` being empty. The document was
        saved (`apply\v18-ubookitbackoffice.json`, 80,667 bytes). 0.85.2: `git diff --stat` empty,
        `git diff --quiet` exit 0, and the `M` entries are line endings only.
      - **Pre-upgrade live check.** The v18 TestSite has its own database; Chris logged in.
        1. Bookings, default window 2026-11-09 to 2026-11-15: GET 200, 1 row, "Showing 1–1 of 1". The
           67-day window gives GET 400 with the same inline message and no toast.
        2. This site has no site closures. Create 2027-03-10 "CLU live check (delete me)": POST 200,
           rows 0 → 1. Delete: DELETE 200, rows 1 → 0.
        3. Holiday 404 stub: one GET. The panel is withdrawn, with no Import or Fetch control and no
           toast. **500 control:** the panel stays, showing "The site's holiday source could not be
           reached…", with no toast.
        4. Closure 2027-03-10 to 2027-03-05: POST 400, the same message on `closure-end-date`, no
           toast. Cancelled; 0 rows, 0 `CLU`.
      - **Console:** `[UmbAuthClient] Token request failed: 400` appeared here too, after this
        **un-upgraded** TestSite restarted. That confirms 4.1's attribution: it comes from the restart
        and session, not from this change. Nothing came from the section.
- [x] 5.3 Repeat 3.1–3.4 with `@umbraco-cms/backoffice@^18.3.1`. Verify as there, against `dev/v18`'s
      own 1.2 figures. Also verify that the regenerated `sdk.gen.ts` matches the explore's prediction:
      the same 37 names, and `types.gen.ts` identical.

      **Done** (`dev/v18` commit `a1ecad1`).
      - 3.1: no ERESOLVE and no `EBADENGINE`. `@hey-api/openapi-ts@0.99.0` and
        `@umbraco-cms/backoffice@18.3.1` (its peer `0.99.0 deduped`). `npm audit` gives 12 findings
        (3 moderate, 8 high, 1 critical), as on `main`.
      - 3.2: no deprecation line. Method names 37 / 37 with no difference. `types.gen.ts` is
        **identical** (`git diff --quiet` exit 0). `client.gen.ts` imports `'../hey-api'`. Diff stat:
        15 files, +427 / −709. **Matches the explore's prediction exactly:** `diff -r
        --strip-trailing-cr` of `src/api` against the explore's offline `api-099-byTags` output exits 0.
      - 3.3: `tsc --noEmit` exit 0, vitest 391/391, build succeeds. **No section-code edit.**
      - 3.4: bundle and `obj/Release` deleted, clean Release build with 0 warnings and 0 errors. Unit
        **2516** = 2490 + 26, integration **222**, rendering **1211**, all equal to `dev/v18`'s 1.2.
        `GeneratedClientTests` + `OpenApiTransformerTests` + `NodeEngineGuardTests` give 40/40, and
        both client-guard files have an empty diff.
      - Guard fires on this line's lockfile: `ci.yml` `24`→`22` gives red, ".github/workflows/ci.yml
        job 'verify' pins node-version 22, but @umbraco-cms/backoffice 18.3.1 requires Node '>=24.13'."
        The file was confirmed mutated, and the revert confirmed by hash. **Instrument note:** a first
        attempt reported `mutated: False` and a green test. `[IO.File]` resolves relative paths
        against the process directory, not PowerShell's location, so it had read and written `main`'s
        `ci.yml` (restored to identical content, and `git status` on `main` confirmed it clean). It was
        re-run with an absolute path. The `main` mutations in 2.3 were unaffected: there the process
        directory and PowerShell's location were the same worktree, and every run reported
        `mutated: True`.
- [x] 5.4 Repeat 4.1–4.2 on the v18 TestSite. Verify as there.

      **Done.** `obj/Debug` deleted, then the TestSite run on the rebuilt output. The page loads
      `entrypoint-CeZYdjkU.js` and `closures-view.element-U9tWRkR7.js`, which are the rebuilt
      bundle's files. Screenshot:
      `C:\Users\cnorw\AppData\Local\Temp\claude-chrome-screenshots-x4R5b5\screenshot-1794570268292-1.jpg`.
      **Every step matches 5.2:**
      1. GET 200, 1 row, "Showing 1–1 of 1". The 67-day window gives 400 with the same inline
         message.
      2. POST 200, rows 0 → 1. DELETE 200, rows 1 → 0.
      3. 404 stub: one GET, panel withdrawn, no toast. 500 control: panel stays with "could not be
         reached".
      4. POST 400, the same message on `closure-end-date`, no toast. 0 rows, 0 `CLU`.

      Console: no errors at all; the session survived this restart. No finding.

## 6. Close

- [ ] 6.1 On both branches, run CI's exact command: `openspec validate --all --strict`, using the
      pinned CLI version. Run it after the last commit. Verify: it passes on each line.
- [ ] 6.2 QA (`qa-review`): a subagent that did not write this change, reused across rounds. Tell it
      what is claimed and ask it to verify, not trust: the build state, the totals, the live results.
      Verify: APPROVE, or APPROVE WITH NITS, with any nits resolved or recorded.
- [ ] 6.3 Push both branches and open both PRs. Merge once CI is green on each, including Node 24
      in the logs, and QA has approved. Verify: both merged; `main` and `dev/v18` CI green after the
      merge.
- [ ] 6.4 Record the obligations this leaves:
      - the next release's CHANGELOG line (proposal, *Impact*), on both lines;
      - the deferred obligation "v18's client to 18.x is its own change" marked superseded.

      Verify: both written in the deferred-obligations record. Then archive the change.
