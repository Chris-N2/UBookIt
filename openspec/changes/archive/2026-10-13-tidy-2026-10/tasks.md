## 0. Start

- [x] 0.1 The branch `tidy-2026-10` was cut from `origin/main` at `799f156`, with the upstream unset,
  when this was proposed. Commit the proposal on it. Verify: `openspec validate tidy-2026-10
  --strict` passes, and `git log origin/main..` shows only the proposal commit.
  *Done:* proposal `2eb0d41` on `tidy-2026-10`, the only commit above `origin/main`. Strict
  validation passes.
- [x] 0.2 Record the guarantee diff for the one MODIFIED requirement (`packaging`, *The package
  declares which Umbraco majors it accepts*) against `openspec/specs/` on `main`, per design D9:
  every SHALL and scenario carried, one scenario added, nothing dropped. Verify:
  `ChangeDeltaIntegrityTests` passes, and the record is written here.
  *Done 2026-10-13, at propose time (design D9); re-checked here against `main`'s
  `openspec/specs/packaging/spec.md`.*
  - *4 scenarios, all carried verbatim: unbounded fails; read from the packed nuspec; excludes the
    next major and admits its own; uBookIt's own dependencies exempt.*
  - *3 SHALLs, all carried verbatim: the upper bound; present in the packed nuspec; binds
    releases from here on.*
  - *Added: the scenario "The newest version of each line tells a resolver which line it is".*
  - *Changed prose only:*
    - *"lists … which is false" → "was able to describe";*
    - *"exists only in prose" → "would live only in prose";*
    - *a new paragraph saying the feed now carries the bound.*

  *Nothing dropped. The delta is `dev/v18`'s requirement byte for byte.*
- [x] 0.3 Baselines on `main` before any edit: build the client, then a clean Release build, then
  each suite per project. Expected: unit 2108, integration 210, rendering 1178, client 346, 0
  warnings. Verify: the numbers are recorded here. If they differ, explain why before going on.

  *Done:*
  - *client 346 (16 files);*
  - *clean Release build, 0 warnings, 0 errors;*
  - *unit 2108, rendering 1178, integration 210.*

  *All four match the record.*
## 1. Closed-period hints (design D1)

- [x] 1.1 In `resource-editor.element.ts` `#renderClosedPeriods`, render each period's To hint and
  Note hint inside its own fieldset, under its field, as `cp-${index}-to-hint` and
  `cp-${index}-note-hint`. Point each input's `aria-describedby` at its own period's hint. Remove
  the shared pair after the list. Verify: the client builds.
  *Done.* The To hint sits under the period's From/To row. The Note hint sits inside the note's
  `.field`, which is where this editor puts a hint (as `resource-directly-bookable-hint` is).
- [x] 1.2 Update `ClosureAccessibilityTests.Closed_period_inputs_are_labelled_and_their_hints_referenced`.
  It must assert that each period's To and Note name an indexed hint inside the same fieldset, and
  that no shared `cp-to-hint`/`cp-note-hint` remains. Verify: it passes, and it **fails** with
  1.1's markup reverted.
  *Done.* The guard extracts one period's `<fieldset>`…`</fieldset>` template from
  `#renderClosedPeriods`. It asserts that both `aria-describedby` references and both indexed hint
  ids are inside it, and that `cp-to-hint` and `cp-note-hint` appear nowhere in the file.

  **Shown to fire** (each restored afterwards):
  - M1, the file reverted to HEAD's markup: fails.
  - M1b, the note hint moved after `</fieldset>`: fails.
- [x] 1.3 Live check in the backoffice, with two or more closed periods. Each period's hints sit
  under its own fields. Read the shadow DOM: every `aria-describedby` id resolves within the same
  root, and all ids are unique after adding and removing a period. Verify: a screenshot plus the DOM
  readout, recorded here.

  *Done 2026-10-13, on the main TestSite (Meeting Room A, nothing saved).*
  - *Three periods added: each To hint renders under its period's From/To row, and each Note
    hint inside the note's field. Screenshot taken.*
  - *Shadow DOM readout: each input's `aria-describedby` names `cp-<i>-to-hint` or
    `cp-<i>-note-hint`, and every id resolves inside the same fieldset.*
  - *Middle period removed: the remaining two re-index as `cp-0` and `cp-1`, with every reference
    still inside its own fieldset.*
  - *No duplicate ids at any point, and no shared `cp-to-hint` or `cp-note-hint`.*
## 2. Holiday import wording (design D2, D3)

- [x] 2.1 Make `importSummary` a function term in `en-us.ts`: "closure"/"closures"; the skipped
  sentence only when `skipped > 0`, singular or plural. Verify: vitest covers created 1/N ×
  skipped 0/1/N, and the 0 case has no skipped sentence.
  *Done.* `importSummary` is a function term. Umbraco's `UmbLocalizationEntry` is
  `string | ((...args) => string)`, and the localizer calls a function with the arguments. Tests:
  - created 0, 1 and 4;
  - the skipped sentence omitted when nothing was skipped;
  - "1 row was skipped" and "2 rows were skipped".

  **Side effect:** the dictionary is no longer string-valued as a whole. So the 14 test casts
  `terms as Record<string, Record<string, string>>`, across 6 files, now go through `unknown`.
  Every section those casts read is still all strings, and the reason is noted beside the term.

  **Shown to fire:** L1 (always mention skipped rows) fails 2 tests; L2 (no plural) fails 2.
- [x] 2.2 `previewFailure` returns `"refused"` for 401 and 403. Add the `importRefused` term.
  Render it for a refused preview (the panel stays) and a refused import (in the error slot,
  focused). Verify: `holiday-fields.test.ts` expects `refused` for 401/403, `absent` for 404 and
  `failed` for 500, 502 and `undefined`. Each expectation fails under a mutation that maps the
  status elsewhere.
  *Done.* `previewFailure` maps:
  - 404 → absent;
  - 401 and 403 → refused;
  - anything else → failed (500, 502, 400 and undefined are tested).

  `previewOutcome` takes the failure kind instead of a boolean, and the view holds
  `_holidayFailure` in place of `_holidaySourceFailed`. A refused preview renders `importRefused`
  in the same `role="alert"` paragraph; a refused import puts `importRefused` in the error slot.

  **Mutations, each caught:**
  - H1, 403 → failed;
  - H2, 401 → absent;
  - H3, a refusal outranked by rows.
- [x] 2.3 Cover each `public-holidays` scenario of *A refused request is reported as a refusal*.
  Use a test through the element's own decision function where one exists, or record which
  scenario is verified live and why it cannot be unit-tested. Verify: a scenario-to-test map is
  recorded here.
  *Scenario map:*
  - *"A refused preview does not blame the source":*
    - `previewFailure` maps 401/403 to refused (tested);
    - `ClosureAccessibilityTests.Both_holiday_calls_word_a_refusal_as_a_refusal` (new) pins that
      the view routes `refused` to `importRefused`;
    - "the refusal message" tests show it is not the source-failure term and never says "could
      not be reached".
  - *"A refused preview does not withdraw the feature":* `previewFailure(401|403)` is not
    `absent`, and only `absent` calls `#withdrawImport`.
    `Both_holiday_calls_treat_a_404_as_an_absent_source` still counts exactly two such calls.
  - *"A refused import is reported as a refusal":* the new seam test's import assertion. "Creates
    nothing" is the server's refusal, already covered by `public-holidays` "Configure alone reaches
    neither".
  - *"A source failure is still reported as one":* `previewFailure(500|502|400|undefined)` is
    `failed`, and `previewOutcome(_, "failed")` is `failed`.
  - *What the element actually shows is checked live only (2.4). No client test in this project
    renders a Lit element.*
- [x] 2.4 Live check: import 1 holiday with 0 skipped, and N with ≥ 2 skipped where the source
  allows; read the summary. Provoke a 403, for example by removing *Change site settings* from the
  test user's group while the screen is open, then preview. Verify: the refusal message, with the
  panel still present; recorded here.

  *Done 2026-10-13, on the main TestSite (GOV.UK source). This check FOUND A DEFECT (below).*
  - *Summary: the December 2026 window, with Boxing Day (substitute day) unticked, imported
    Christmas Day only. The screen showed "Created 1 closure." in `role="status"`, with no
    skipped sentence. It left a 2026-12-25 closure in the TestSite data.*
  - *"Rows were skipped" is not reachable live: the GOV.UK source yields no rejected rows. It is
    covered by the unit tests in 2.1 only.*
  - *Refusal, first attempt: FAILED. Chris removed "Change site settings" with the screen open,
    then I fetched. The screen read "The site's holiday source could not be reached". The server
    had refused correctly: the log shows OpenIddict `insufficient_access` forbidden, and a direct
    call with the backoffice token returns 403.*
    - ***Cause:** the generated client THROWS on a non-2xx. Calling `previewHolidays` in the page
      threw `{status: 403, title: "You do not have the necessary permissions…", type:
      "Unauthorized"}`, so the code only reached `catch`, which always set "failed".*
    - *The returned-response branch therefore never ran, so **the 404 → withdraw rule from
      `public-holidays` "A source that goes away while the screen is open" was broken too**,
      from the holiday import onward. Pre-existing, not introduced here. `find-fields.ts`
      `isMiss` already records the same fact for bookings.*
    - ***Fix:** `thrownStatus` (holiday-fields.ts). Both calls now decide a failure once, from
      the returned response or the thrown value, and no bare `catch` remains. 3 vitest tests use
      the measured thrown shape. `ClosureAccessibilityTests.Both_holiday_calls_classify_a_thrown_failure_by_its_status`
      pins the seam.*
  - *Refusal, second attempt, on the rebuilt bundle:*
    1. *Chris restored the permission. I reloaded, opened the panel and set the window.*
    2. *Chris removed the permission again, and I fetched.*
    3. *HTTP 403. `_holidayFailure` = refused. The panel stays and the source is still offered.
       The alert reads "The site refused this request. Your session may have ended, or you may
       no longer have permission to change closures. This is not a problem with the holiday
       source." Screenshot taken.*
  - *Also observed: once the permission is removed, a reload correctly shows the screen read-only
    with no import panel, and never asks the source.*
  - *Not reproduced live: the 404 withdraw path, which needs the source deregistered while the
    screen is open. It is covered by the same `thrownStatus` + `previewFailure(404)` decision and
    the seam test.*
## 3. Documentation (design D4, D5; proposal items 4–6)

- [x] 3.1 `docs/backoffice.md`: one sentence after *During a closure, the booking page looks past
  it*, saying that a closure starting after today leaves the list starting today. Verify: read in
  place.
  *Done:* "**Only a closure covering today moves the list.** One that starts tomorrow, or later,
  leaves it starting today, and the closed dates within it simply show nothing available."
- [x] 3.2 `README.md`: "uBookIt `17.x` is for Umbraco 17" becomes a sentence naming both majors,
  identical on both lines. Verify: every README pin still passes, and the README guards pass.
  *Done:* "uBookIt `17.x` is for Umbraco 17 and `18.x` for Umbraco 18." The pin "The major
  tracks the Umbraco major" is unaffected, and the README guards pass.
- [x] 3.3 A guard that `docs/configuration.md` states `SettingRow.MaxValueLength` exactly once, as
  its decimal value. Verify: it passes, and it fails when the doc's number is changed in a scratch
  edit (restored afterwards).

  *Done:* `SettingLengthDocumentationTests`, 2 tests.
  - One `SaysOnce`s the sentence, with the constant interpolated.
  - One holds every figure of the form "N characters" (3+ digits) in the document to the
    constant, so a second, stale statement of the limit cannot sit beside the pinned one.

  **Shown to fire** (each restored afterwards):
  - M2, the doc says 4000: both tests fail.
  - M2b, a second "4000 characters" sentence: the second test fails.
## 4. Readme link guards (design D6)

- [x] 4.1 Both readme link guards in `VersionTruthTests` treat `<repo>/tree/<ref>/` as a repository
  address, with the same ref and existence checks: a directory for `tree/`, a file for `blob/`.
  Verify: both pass on the current readme, and both fail on a `tree/main/docs` link added in a
  scratch edit, naming it (restored afterwards).

  *Done:* `VersionTruthTests.RepositoryAddress` recognises both `blob/` and `tree/`, and both
  guards use it. The resolve guard checks `Directory.Exists` for `tree/` and `File.Exists` for
  `blob/`.

  **Shown to fire** (each restored afterwards). This corrects design D6:
  - M3, a `tree/main/docs` link: the RELEASE guard fails. The RESOLVE guard passes, and it should,
    because its remit is path existence and `docs/` exists. D6's "both guards must fail" was
    wrong for this input. Before this change, neither guard looked at a `tree/` link at all.
  - M3b, `tree/17.3.0/README.md` (a file addressed as a folder): the RESOLVE guard fails.
  - M3c, a control link `tree/17.3.0/docs`: both guards pass.
## 5. CI (design D7, D8)

- [x] 5.1 Client JSON report in CI and `scripts/ci/Assert-ClientTestResults.ps1`: fail when the
  report is missing, has zero tests, or has any skipped/pending/todo result, naming each. Fail
  closed on an unrecognised shape. Wire it into `ci.yml` with `if: !cancelled()`. Verify: run
  locally against a real report (passes), an `it.skip` report and an `it.todo` report (each fails,
  naming the test), and a report with the `testResults` key removed (fails closed).
  *Done:* `scripts/ci/Assert-ClientTestResults.ps1`, wired into `ci.yml` after `Client - tests`.
  The report goes to `CLIENT_REPORT`, outside `RESULTS_DIR`. **Checked against REAL vitest 4
  output:**
  - the full suite passes ("All 352 client test(s) ran.");
  - a scratch file holding an `it.skip` and an `it.todo`: vitest itself exits 0, and the script
    fails, naming both, as `[skipped]` and `[todo]`;
  - `testResults` removed: fails closed.

  The real statuses are `skipped` and `todo`, as design D7 assumed. A manual check is not a
  regression guard, so `ClientTestResultsScriptTests` was added: 10 tests, run through `pwsh` as
  `AuditScriptTests` are.
- [x] 5.2 Widen the README's skip claim to the client suite, and update its pin if it has one.
  Verify: the README guards pass.
  *Done:* the README now says "The run fails if any .NET test project didn't report, or any
  test, .NET or backoffice client, was skipped." `docs/publishing.md` names the new script. No pin
  covered the old sentence.
- [x] 5.3 `audit.yml` and `Assert-NoVulnerablePackages.ps1`: restore and list once per
  configuration, in sequence; union the findings with their configuration; report a finding seen
  in both once. Verify:
  - a pre-captured fixture with a Release-only finding fails;
  - the current lines pass;
  - the existing suppression checks behave unchanged (the fixtures they already use).

  *Done:* the script restores and lists once per configuration, in sequence. `-ReleaseReport`
  lets a test give Release its own graph. Findings are unioned per project, with the
  configurations each was seen in; a finding seen in every configuration is reported once,
  unqualified. `audit.yml` no longer does its own Debug-only restore.

  **6 new `AuditScriptTests`:**
  - a Release-only finding fails, marked "(in Release only)";
  - a Debug-only finding fails;
  - a finding seen in both is reported once, unqualified;
  - a suppression matched only in Release is accepted, not stale;
  - a project named only by the Release report is audited;
  - a Release report with no vulnerability source fails.

  The 24 existing tests are unchanged and pass.

  **Shown to fire:** M4 (the script reads Debug's report for both configurations) fails 5 of the
  6. The sixth correctly passes, since the mutation leaves both graphs identical.

  **Run for real on `main`:** "audited 10 project(s) in UBookIt.slnx (Debug, Release); 5
  finding(s) accepted by a suppression". These are the TestSite's five ImageSharp advisories,
  each seen in both configurations, and the exit code is 0.
## 6. Verify on `main`

- [x] 6.1 Client build, clean Release build (0 warnings), every suite per project, and
  `openspec validate --all --strict`. Verify: counts recorded here, each moved by exactly the tests
  this change added.
  *Done 2026-10-13:*
  - *client 352 (346 + 6);*
  - *clean Release build, 0 warnings, 0 errors;*
  - *unit 2127 (2108 + 19: 10 client-results, 6 audit, 2 setting-length, 1 refusal seam);*
  - *rendering 1178 and integration 210, both unchanged;*
  - *`openspec validate --all --strict`: 27/27.*

  *The documentation guards were re-run after 6.2's edits (177 passed).*
- [x] 6.2 Outward grep for sentences this change falsifies, in `docs/`, `README.md`,
  `openspec/specs/**`, `CHANGELOG.md` and `CLAUDE.md`. Search for:
  - "only in prose", "ever contradicted";
  - "could not be reached";
  - "rows were skipped";
  - ".NET test project", "skipped";
  - "Debug", "blob/";
  - "2048".

  Verify: each hit is classified, and the list is recorded here.

  *Done. Each hit classified:*
  - *`packaging/spec.md:1013,1017`: this change's own MODIFIED delta fixes it at archive.*
  - *`PackageCompositionTests.cs:362-364`: **FALSIFIED, and missed by the proposal.** A remark
    carried the same two sentences; it is now in the past tense and says the bound has been
    carried since `17.1.2`/`18.0.0`. **The same remark is on `dev/v18` (verified), so the port
    carries this fix.***
  - *`Assert-NoVulnerablePackages.ps1:43`: **falsified by this change** ("Debug (what restore and
    `dotnet list` see)"). Now "Debug (MSBuild's default)".*
  - *`docs/backoffice.md:628` (the source-failure paragraph): **incomplete** after item 3. It now
    adds a sentence for a refusal.*
  - *`public-holidays/spec.md:84,174,186`: still true. A refusal is neither case, and the ADDED
    requirement covers it.*
  - *`continuous-integration/spec.md:21` ("run every .NET test project"): true. The client suite
    is a separate bullet of that requirement.*
  - *"2048" in `CHANGELOG.md:158`: history. In `privacy-notice/spec.md:285`: a true statement in a
    spec, about the same constant, and guarded by nothing. Left alone: editing a spec for a
    hypothetical constant change is the disproportionate edit CLAUDE.md warns against.*
  - *`cp-to-hint`/`cp-note-hint`: none left outside this change's records.*
  - *"rows were skipped": only the new tests.*
## 7. QA and port

- [x] 7.1 QA (`qa-review`) in one subagent, reused across rounds. Hand each round's fixes over as
  new code, with every claim to verify rather than trust. Verify: APPROVE.
  *Round 1: REJECT* (on `b98ed99`). QA re-ran the build, all four suites, validation, M1, H1 and
  the real vitest skip/todo probe, and all reproduced. Every fix below is new code.
  - **1. CRITICAL: the audit's Release pass listed the Debug graph.** `dotnet list package` has
    no configuration option and restores implicitly as Debug before it lists, overwriting the
    `dotnet restore -p:Configuration=Release` that ran just before it. QA reproduced it with a
    probe holding a Release-only `System.Text.Json 8.0.0`: exit 0, "No known advisories".
    - **Why it got through:** the six new fixture tests and M4 prove what the script does WITH
      two graphs, not how it GETS them. My "run for real" record was true, but its findings were
      present in both graphs, so it showed nothing about a Release-only one.
    - *Fix:* each pass sets `Configuration` in the ENVIRONMENT (saved and restored) and lets
      `dotnet list` do its own restore. The separate restore is gone.
    - **Checked against the real CLI**, on QA's probes:
      - Release-only: fails, "(in Release only)";
      - Debug-only: fails, "(in Debug only)";
      - unconditional: fails once, unqualified;
      - `Configuration` is empty in the environment afterwards.
    - **New guard:**
      `AuditScriptTests.A_package_only_the_Release_configuration_references_is_found_by_the_real_listing`
      writes a Release-conditioned probe outside the repository and runs the real CLI. It needs
      nuget.org, as every restore here does.
    - Corrected the script header, the `audit.yml` comment and the `docs/publishing.md` wording.
  - **2. MINOR: the raw-key guard could not see the refusal keys.** `ElementLocalizationKeyTests`
    matches only literal `#term("key")` calls. *Fix:* each branch calls a literal `#term(...)`,
    and the seam test's expected strings are updated.
  - **3. NITs.** Design D7 promised a message naming vitest's version, which the report does not
    carry; the design now says so. Two over-long lines re-wrapped (`README.md`,
    `docs/publishing.md`).
  - **Also found after round 1, in the live check 2.4 (see there):** both holiday calls had
    classified only RETURNED failures, but the client THROWS them. So 401/403 never reached
    "refused", and a 404 never withdrew the panel; the latter is pre-existing, from the holiday
    import. *Fix:* `thrownStatus` and a single decision per call, with 3 vitest tests and a new
    seam test. Re-verified live: 403 shows the refusal message.
  *Round 2: APPROVE WITH NITS on `main` at `9f15c65`.* QA reproduced every claim:
  - clean Release build, 0 warnings; unit 2129, rendering 1178, integration 210; client 355;
    openspec 27/27;
  - the audit against its Rel/Unc/Dbg probes and against `UBookIt.slnx` (5 accepted, exit 0, and
    `Configuration` empty afterwards); no repo file assigns or conditions on `Configuration`.

  Mutations, each caught:
  - the round-1 audit script restored: the real-CLI test fails;
  - `importSourceFailed` renamed: `ElementLocalizationKeyTests` now fails;
  - the import's thrown classification replaced by `"failed"`: the seam test fails.

  It confirmed that `#load` catches its own errors, so a failed reload after a successful import
  shows `loadFailed`, never `importFailed`. Nits:
  - a short line left by a re-wrap in `docs/publishing.md`: taken;
  - the real-CLI audit test needs nuget.org: accepted, and stated in its remarks.

  Still owed: the 7.2 port, against the checklist QA gave (parity of the CI files, the
  PackageCompositionTests remark, the packaging delta dropped, the thrown-status fix, v18's
  `UmbLocalizationEntry`, the real-CLI test on v18).
- [x] 7.2 Port to `dev/v18` on `tidy-2026-10-v18`, from `origin/dev/v18`, by cherry-pick.
  **Drop the `packaging` delta** from the port's change folder: `dev/v18` already has that text, so
  archiving it there would be a no-op MODIFIED. Check that the v18 backoffice package's
  `UmbLocalizationEntry` accepts a function. Re-run 0.3's suites and every guard named in 1–5 on
  `dev/v18`, plus 1.3 and 2.4 live on the v18 TestSite. Verify:
  - the records match `main`'s;
  - `git diff origin/dev/v18 -- .github scripts/ci` equals `main`'s CI diff;
  - the 7.1 reviewer has checked the port.

  *Port done 2026-10-13.* Cherry-picked `main`'s four commits (`a4374b9`,
  `8274ea8`, `358d0f3`, `5e0808a`), then `bd21166` dropped the packaging delta.
  - *Conflicts:* the README keeps v18's `blob/18.2.0` link. `VersionTruthTests` had v18's own
    version text around the guards, so the `RepositoryAddress` change was re-applied by script
    rather than merged.
  - *`UmbLocalizationEntry` on 18:* the runtime calls a function term with its arguments
    (`typeof t == "function" ? t(...e)`), and the live summary below confirms it.
  - *Suites:* client 355; clean Release build 0 warnings; unit 2141 (2120 + 21); rendering 1178;
    integration 210; `openspec validate --all --strict` 27/27. The real audit exits 0.
  - *CI parity:* `.github/workflows/ci.yml`, `audit.yml`, `scripts/ci/Assert-ClientTestResults.ps1`
    and `Assert-NoVulnerablePackages.ps1` are byte-identical to `main`'s.
  - *1.3 live (v18 TestSite):* three periods added. Every `aria-describedby` resolves inside its
    own fieldset, and there are no duplicate ids. After the middle period was removed, the rest
    re-index cleanly. Screenshot taken.
  - *2.4 live, summary:* the January 2027 window offered "2027-01-01 New Year's Day". Importing it
    showed "Created 1 closure." in `role="status"`, with no skipped sentence. It left a
    2027-01-01 closure in the TestSite data.
  - *2.4 live, refusal:* Chris removed "Change site settings" with the screen open (panel open,
    January 2027 window), then I fetched. `_holidayFailure` = refused. The alert reads "The site
    refused this request. …"; the panel and the source are kept. Screenshot taken.
    - A reload with the permission removed shows the screen read-only: only "Show past closures",
      and no import panel. This matches `main`.
    - An earlier reload, during Chris's re-login, showed editing controls without the panel.
      That was before the backoffice had picked up the change; it is not a defect.

  *Port round: APPROVE* (the 7.1 reviewer, on `bbc4d52`), with no findings.
  - *Port equals main:* the net diff matches `main`'s file for file, line for line. The only
    differences are the dropped packaging delta and `tasks.md`; the two conflict files are included.
  - *CI files:* `.github` and `scripts` are byte-identical to `main`'s.
  - *Readme link guards:* re-fired on v18. A `tree/main/docs` link fails the release guard; a
    `tree/` link to a file and a `blob/` link to a folder each fail the resolve guard. A valid
    `tree/18.2.0/docs` link passes both.
  - *Reproduced:* client 355, with `Assert-ClientTestResults` reporting all ran; unit 2141;
    rendering 1178; integration 210; openspec 27/27; the real audit exits 0.
  - *Not reproduced by QA:* the clean Release build, because the v18 TestSite was still running.
    Run by me once it was stopped: client build, then `--no-incremental` Release build, 0
    warnings, 0 errors.
- [x] 7.3 PRs into `main` and `dev/v18`. Both are expected red on parity until the first merges.
  Merge `main`'s first, then re-run the v18 PR. Verify: both green after the second run.

  *Done 2026-10-13.* PR #23 (`main`) and PR #22 (`dev/v18`) merged within a minute of each other,
  so neither PR run was re-run.
  - *PR runs:* the only failure in either was the step "Parity with the other published line".
    That failure was expected: each PR was compared with the other line's origin branch, which did
    not yet carry the CI change. The push runs on both heads (`12e1c8c`, `a812435`) passed in full.
  - *Verify, as amended:* "both green after the second run" was not observed on the PRs. It is
    observed instead on the post-merge push runs of `5269a2b` and `1e05ccd`, which compare the
    two lines after both carry the change. They are recorded with 7.4.
- [x] 7.4 Archive on each line, syncing `public-holidays` on both and `packaging` on `main` only.
  Re-run 6.2's grep after the sync. Verify: `openspec validate --all --strict` passes on both lines,
  and `main`'s and `dev/v18`'s *The package declares which Umbraco majors it accepts* are
  identical (`diff` of the extracted requirement is empty).

  *Done 2026-10-13, as `2026-10-13-tidy-2026-10` on both lines.*
  - *Sync:* `main` updated `packaging` (~1 modified) and `public-holidays` (+1 added). `dev/v18`
    updated `public-holidays` only (+1 added); its `packaging` spec is byte-unchanged.
  - *Packaging:* the extracted requirement *The package declares which Umbraco majors it accepts*
    diffs empty between the lines. The two packaging specs now hold the same 24 requirements with
    identical content. Before the archive this requirement was the only difference.
    - The ORDER of requirements still differs between the lines. That predates this change and
      has no effect on what either spec requires.
  - *`public-holidays`:* identical on both lines after the sync.
  - *`openspec validate --all --strict`:* 26/26 on both lines. That is 27 less this archived
    change.
  - *6.2's grep, re-run on both lines after the sync:* the hits are the same and all true.
    - `packaging/spec.md:1017` now reads "would live only in prose".
    - `public-holidays/spec.md:84` is the 404 rule, which the thrown-status fix makes true again.
    - `public-holidays/spec.md:253` is the new refusal scenario.
    - The "2048" and ".NET test project" hits are as classified in 6.2.
  - *Stale `.openspec-archive.lock` files* left by the CLI on both lines: their pids were not
    running, so both were deleted and not committed.
  - *Post-merge CI:*
    - `dev/v18` `1e05ccd` (run 38440570698): **success**, parity included.
    - `main` `5269a2b` (run 38440483443): **failed on parity only**; every build, test and
      validation step passed. It is a race, not drift: that run started at 17:39:58Z, before PR
      #22 merged into `dev/v18` (17:40:33Z), so it compared against a `dev/v18` without the CI
      change.
    - Run locally against the current `origin/dev/v18`, `Assert-LineParity.ps1 -Line main` exits 0
      ("identical to origin/dev/v18"). **Owed: Chris re-runs run 38440483443** so that `main` is
      green on origin. This archive's own push also starts a fresh run.
- [x] 7.5 Hand over to the release change (`release-17-3-1` / `release-18-2-1`). Its changelog
  must name the two visible wording changes (items 2 and 3) and the corrected hint placement.
  Verify: a note in `ubookit-deferred-obligations` striking each item discharged here.

  *Done 2026-10-13.* The release change (`release-17-3-1` / `release-18-2-1`) inherits these.
  - *Changelog:* name the two wording changes the operator sees: the pluralised import summary,
    and the refusal message for 401/403. Also name the closed-period hints now sitting under each
    period.
  - *Behaviour fix:* holiday failures are now read from the thrown status too. That also restores
    the `public-holidays` rule that a source gone away (404) withdraws the panel, which had been
    broken since the holiday import. This is a bug fix the changelog may name.
  - *Nothing public changed* (no type, member, endpoint, setting or migration), so a patch is
    right.
  - *Obligations:* `ubookit-deferred-obligations` strikes the closed-period hints, the docs line,
    the Release-only audit, the README `tree/` links, client skip detection, the v18 README
    example, the 2048 literal, and holiday items 1 and 2. `main`'s packaging sentences are struck
    at this archive's sync.
