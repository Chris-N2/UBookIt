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
- [ ] 1.3 On `main` (branch `backoffice-client-upgrade`), with the TestSite running, save the live
      backoffice OpenAPI document to the scratchpad. Regenerate with the **current** 0.85.2 from it
      (design D1). Verify: `git diff --stat -- src/UBookIt.Backoffice/Client/src/api` is empty. If it
      is not, stop and report: the committed client is stale against its own server.
- [ ] 1.4 On `main`, run the live check of design D4 (steps 1–4) **before** the upgrade, and record
      what each step showed. This is the comparison point for 4.2.

## 2. Hand-written changes (on `main`; cherry-picked in 5)

- [ ] 2.1 Update `scripts/generate-openapi.js` per design D2: `runtimeConfigPath: './src/hey-api'`, and
      `operations: { strategy: 'byTags', containerName: '{{name}}Service' }` in place of
      `asClass` / `classNameBuilder`. Verify: the script contains neither deprecated option, and the
      generation in 3.2 prints no deprecation warning.
- [ ] 2.2 Set `node-version: 24` on the `actions/setup-node` step in `.github/workflows/ci.yml` and
      `.github/workflows/publish.yml`. Correct the Requirements line of
      `src/UBookIt.Backoffice/README.txt` from "Node LTS Version 20.17.0+" to Node 24.13 or later,
      saying it follows `@umbraco-cms/backoffice`'s declared engine. Verify: a grep of the repo,
      excluding `openspec/changes/archive` and `node_modules`, finds no `node-version: 22` and no
      `20.17`.
- [ ] 2.3 Add the Node-engine guard to `UBookIt.Tests` per design D5. Verify:
      - green after 3.1;
      - **it can fire:**
        - setting either workflow's `node-version` back to `22` turns it red, with a message naming
          the workflow, the pin and the requirement;
        - a non-major pin (`24.x`) turns it red as unreadable, not as passing;
        - each mutation is reverted, and the revert is confirmed by content comparison;
      - the non-empty checks hold: zero `setup-node` steps found, or zero direct dependencies with
        an `engines.node`, fails the test.

## 3. Upgrade and regenerate on `main`

- [ ] 3.1 In `Client/`, run `npm install --save-dev @hey-api/openapi-ts@^0.99.0
      @umbraco-cms/backoffice@^17.8.1`. Do not use `--legacy-peer-deps` or `--force`. Verify:
      - no ERESOLVE and no `EBADENGINE`;
      - `npm ls @hey-api/openapi-ts @umbraco-cms/backoffice` shows 0.99.x and 17.8.x;
      - the resolved versions are recorded here.
- [ ] 3.2 With the TestSite running, run `npm run generate-client`. Verify, against 1.3:
      - `types.gen.ts` is unchanged, or every difference is listed here and judged against design D3;
      - the `UBookItBackofficeService` method names are identical (sorted list diff);
      - `client.gen.ts` imports `'../hey-api'`;
      - the diff stat is recorded.
- [ ] 3.3 Typecheck, test and build the client: `npx tsc --noEmit`, `npm test`, `npm run build`. Verify:
      - all green, with the vitest total equal to 1.2's;
      - **any section-code edit made to compile against 17.8.1** is listed here with its reason. A
        fix that is not narrow is raised with Chris before it is made.
- [ ] 3.4 Delete `wwwroot/App_Plugins/UBookItBackoffice` and `src/UBookIt.Backoffice/obj/Release`.
      Then do a clean Release build (0 warnings, 0 errors), and run the unit, integration and
      rendering suites. Verify:
      - totals equal 1.2's, plus the guard's own tests from 2.3;
      - `GeneratedClientTests` and `OpenApiTransformerTests` pass **without edits** (design D3);
      - `git diff -- tests/UBookIt.Tests/GeneratedClientTests.cs tests/UBookIt.Tests/OpenApiTransformerTests.cs`
        is empty.

## 4. Live check on `main`

- [ ] 4.1 Start the TestSite on the rebuilt output, and confirm in the browser that the uBookIt
      section loads with no console error from the section's own scripts. Verify: a screenshot, and
      a console read filtered to the section.
- [ ] 4.2 Run design D4 steps 1–4 again and compare each with 1.4. Verify:
      - each step's outcome matches 1.4: the list loads; the closure is created and deleted; the
        holiday-source 404 withdraws the panel; the invalid closure shows the same notification and
        message;
      - any difference is a finding, recorded here and raised before continuing;
      - the closure from step 2 is confirmed gone.

## 5. Port to `dev/v18` (worktree `..\uBookIt-v18`, branch `backoffice-client-upgrade-v18`)

- [ ] 5.1 Branch from `origin/dev/v18`, and cherry-pick the hand-written commits of section 2 and the
      openspec artifacts. Do **not** cherry-pick `package.json`, `package-lock.json` or `src/api`
      (design D6). Verify: `git diff origin/dev/v18 --stat` lists only hand-written files.
- [ ] 5.2 Repeat 1.3 and 1.4 on `dev/v18`, the baseline regeneration and the pre-upgrade live check,
      against the v18 TestSite (`/umbraco/openapi/ubookitbackoffice.json`). Verify as there.
- [ ] 5.3 Repeat 3.1–3.4 with `@umbraco-cms/backoffice@^18.3.1`. Verify as there, against `dev/v18`'s
      own 1.2 figures. Also verify that the regenerated `sdk.gen.ts` matches the explore's prediction:
      the same 37 names, and `types.gen.ts` identical.
- [ ] 5.4 Repeat 4.1–4.2 on the v18 TestSite. Verify as there.

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
