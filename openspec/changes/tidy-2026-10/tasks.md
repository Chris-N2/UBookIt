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
- [ ] 1.3 Live check in the backoffice, with two or more closed periods. Each period's hints sit
  under its own fields. Read the shadow DOM: every `aria-describedby` id resolves within the same
  root, and all ids are unique after adding and removing a period. Verify: a screenshot plus the DOM
  readout, recorded here.

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
- [ ] 2.4 Live check: import 1 holiday with 0 skipped, and N with ≥ 2 skipped where the source
  allows; read the summary. Provoke a 403, for example by removing *Change site settings* from the
  test user's group while the screen is open, then preview. Verify: the refusal message, with the
  panel still present; recorded here.

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

- [ ] 7.1 QA (`qa-review`) in one subagent, reused across rounds. Hand each round's fixes over as
  new code, with every claim to verify rather than trust. Verify: APPROVE.
- [ ] 7.2 Port to `dev/v18` on `tidy-2026-10-v18`, from `origin/dev/v18`, by cherry-pick.
  **Drop the `packaging` delta** from the port's change folder: `dev/v18` already has that text, so
  archiving it there would be a no-op MODIFIED. Check that the v18 backoffice package's
  `UmbLocalizationEntry` accepts a function. Re-run 0.3's suites and every guard named in 1–5 on
  `dev/v18`, plus 1.3 and 2.4 live on the v18 TestSite. Verify:
  - the records match `main`'s;
  - `git diff origin/dev/v18 -- .github scripts/ci` equals `main`'s CI diff;
  - the 7.1 reviewer has checked the port.
- [ ] 7.3 PRs into `main` and `dev/v18`. Both are expected red on parity until the first merges.
  Merge `main`'s first, then re-run the v18 PR. Verify: both green after the second run.
- [ ] 7.4 Archive on each line, syncing `public-holidays` on both and `packaging` on `main` only.
  Re-run 6.2's grep after the sync. Verify: `openspec validate --all --strict` passes on both lines,
  and `main`'s and `dev/v18`'s *The package declares which Umbraco majors it accepts* are
  identical (`diff` of the extracted requirement is empty).
- [ ] 7.5 Hand over to the release change (`release-17-3-1` / `release-18-2-1`). Its changelog
  must name the two visible wording changes (items 2 and 3) and the corrected hint placement.
  Verify: a note in `ubookit-deferred-obligations` striking each item discharged here.
