## 0. Start

- [ ] 0.1 The branch `tidy-2026-10` was cut from `origin/main` at `799f156`, with the upstream unset,
  when this was proposed. Commit the proposal on it. Verify: `openspec validate tidy-2026-10
  --strict` passes, and `git log origin/main..` shows only the proposal commit.
- [ ] 0.2 Record the guarantee diff for the one MODIFIED requirement (`packaging`, *The package
  declares which Umbraco majors it accepts*) against `openspec/specs/` on `main`, per design D9:
  every SHALL and scenario carried, one scenario added, nothing dropped. Verify:
  `ChangeDeltaIntegrityTests` passes, and the record is written here.
- [ ] 0.3 Baselines on `main` before any edit: build the client, then a clean Release build, then
  each suite per project. Expected: unit 2108, integration 210, rendering 1178, client 346, 0
  warnings. Verify: the numbers are recorded here. If they differ, explain why before going on.

## 1. Closed-period hints (design D1)

- [ ] 1.1 In `resource-editor.element.ts` `#renderClosedPeriods`, render each period's To hint and
  Note hint inside its own fieldset, under its field, as `cp-${index}-to-hint` and
  `cp-${index}-note-hint`. Point each input's `aria-describedby` at its own period's hint. Remove
  the shared pair after the list. Verify: the client builds.
- [ ] 1.2 Update `ClosureAccessibilityTests.Closed_period_inputs_are_labelled_and_their_hints_referenced`.
  It must assert that each period's To and Note name an indexed hint inside the same fieldset, and
  that no shared `cp-to-hint`/`cp-note-hint` remains. Verify: it passes, and it **fails** with
  1.1's markup reverted.
- [ ] 1.3 Live check in the backoffice, with two or more closed periods. Each period's hints sit
  under its own fields. Read the shadow DOM: every `aria-describedby` id resolves within the same
  root, and all ids are unique after adding and removing a period. Verify: a screenshot plus the DOM
  readout, recorded here.

## 2. Holiday import wording (design D2, D3)

- [ ] 2.1 Make `importSummary` a function term in `en-us.ts`: "closure"/"closures"; the skipped
  sentence only when `skipped > 0`, singular or plural. Verify: vitest covers created 1/N ×
  skipped 0/1/N, and the 0 case has no skipped sentence.
- [ ] 2.2 `previewFailure` returns `"refused"` for 401 and 403. Add the `importRefused` term.
  Render it for a refused preview (the panel stays) and a refused import (in the error slot,
  focused). Verify: `holiday-fields.test.ts` expects `refused` for 401/403, `absent` for 404 and
  `failed` for 500, 502 and `undefined`. Each expectation fails under a mutation that maps the
  status elsewhere.
- [ ] 2.3 Cover each `public-holidays` scenario of *A refused request is reported as a refusal*.
  Use a test through the element's own decision function where one exists, or record which
  scenario is verified live and why it cannot be unit-tested. Verify: a scenario-to-test map is
  recorded here.
- [ ] 2.4 Live check: import 1 holiday with 0 skipped, and N with ≥ 2 skipped where the source
  allows; read the summary. Provoke a 403, for example by removing *Change site settings* from the
  test user's group while the screen is open, then preview. Verify: the refusal message, with the
  panel still present; recorded here.

## 3. Documentation (design D4, D5; proposal items 4–6)

- [ ] 3.1 `docs/backoffice.md`: one sentence after *During a closure, the booking page looks past
  it*, saying that a closure starting after today leaves the list starting today. Verify: read in
  place.
- [ ] 3.2 `README.md`: "uBookIt `17.x` is for Umbraco 17" becomes a sentence naming both majors,
  identical on both lines. Verify: every README pin still passes, and the README guards pass.
- [ ] 3.3 A guard that `docs/configuration.md` states `SettingRow.MaxValueLength` exactly once, as
  its decimal value. Verify: it passes, and it fails when the doc's number is changed in a scratch
  edit (restored afterwards).

## 4. Readme link guards (design D6)

- [ ] 4.1 Both readme link guards in `VersionTruthTests` treat `<repo>/tree/<ref>/` as a repository
  address, with the same ref and existence checks: a directory for `tree/`, a file for `blob/`.
  Verify: both pass on the current readme, and both fail on a `tree/main/docs` link added in a
  scratch edit, naming it (restored afterwards).

## 5. CI (design D7, D8)

- [ ] 5.1 Client JSON report in CI and `scripts/ci/Assert-ClientTestResults.ps1`: fail when the
  report is missing, has zero tests, or has any skipped/pending/todo result, naming each. Fail
  closed on an unrecognised shape. Wire it into `ci.yml` with `if: !cancelled()`. Verify: run
  locally against a real report (passes), an `it.skip` report and an `it.todo` report (each fails,
  naming the test), and a report with the `testResults` key removed (fails closed).
- [ ] 5.2 Widen the README's skip claim to the client suite, and update its pin if it has one.
  Verify: the README guards pass.
- [ ] 5.3 `audit.yml` and `Assert-NoVulnerablePackages.ps1`: restore and list once per
  configuration, in sequence; union the findings with their configuration; report a finding seen
  in both once. Verify:
  - a pre-captured fixture with a Release-only finding fails;
  - the current lines pass;
  - the existing suppression checks behave unchanged (the fixtures they already use).

## 6. Verify on `main`

- [ ] 6.1 Client build, clean Release build (0 warnings), every suite per project, and
  `openspec validate --all --strict`. Verify: counts recorded here, each moved by exactly the tests
  this change added.
- [ ] 6.2 Outward grep for sentences this change falsifies, in `docs/`, `README.md`,
  `openspec/specs/**`, `CHANGELOG.md` and `CLAUDE.md`. Search for:
  - "only in prose", "ever contradicted";
  - "could not be reached";
  - "rows were skipped";
  - ".NET test project", "skipped";
  - "Debug", "blob/";
  - "2048".

  Verify: each hit is classified, and the list is recorded here.

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
