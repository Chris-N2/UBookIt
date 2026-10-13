## Context

The proposal (*What Changes*) lists ten independent items. Most are a line or a sentence. This
document records only the decisions where there was a real choice, and the constraints a fresh
apply session would otherwise rediscover.

Constraints that apply throughout:

- **Patch-eligible only.** Nothing may add or change a public type, member, endpoint, setting or
  migration (CLAUDE.md *Conventions*). Every product item here is backoffice markup or wording.
- **Both lines.** `main` first, then a port to `dev/v18`. The recurring defect on this project is a
  fix that reaches one line only. So the port is a task with its own checks, not an afterthought.
- **CI files are under the parity check** (`Assert-LineParity.ps1`). A PR changing them on one line
  is red until the other line carries the same change.

## Goals / Non-Goals

**Goals:** close each item so that it can't silently regress. Where an item had no guard, it gets
one, and the guard is shown to fire.

**Non-Goals:** see the proposal's *Non-goals*. Also, this change does not restructure
`Assert-TestResults.ps1`, the audit script or the readme guards beyond what each item needs.

## Decisions

### D1. Closed-period hints: one pair per period, indexed by position

Each period's fieldset renders `cp-${index}-to-hint` under its To input and `cp-${index}-note-hint`
under its Note input, and each input's `aria-describedby` names its own period's hint. The shared
`cp-to-hint`/`cp-note-hint` paragraphs after the list are removed.

- *Alternative rejected: keep one shared hint and move it above the list.* That still separates a
  hint from the field it describes, and it's what produced the stray text to begin with.
- *Alternative rejected: drop the hints from the To field.* The To hint carries real information
  ("defaults to From").

Index-based ids are safe here because the hint and its input are rendered **in the same template
pass, from the same index**. The ⑨-2a hazard was an index into a *filtered* list used as an
identity, and that doesn't arise here. Ids must stay unique when periods are added and removed, and
`ClosureAccessibilityTests` checks this along with resolution.

**Repeated hint text** (n identical "The last day closed…" paragraphs) is accepted. It's the same
pattern the opening-hours rows already use, and each is announced only with its own input.

### D2. Pluralisation: one function-valued term

`importSummary` becomes a function `(created, skipped) => string` in `en-us.ts`. Umbraco's
`UmbLocalizationEntry` is `string | ((...args) => string)`, and `localize.term` calls a function
with the arguments. It:

- says "Created 1 closure." / "Created N closures.";
- appends "1 row was skipped." / "N rows were skipped." only when `skipped > 0`.

- *Alternative rejected: two or four string terms chosen by the caller.* That splits one sentence's
  grammar between the element and the dictionary, and a translator could not reorder it.
- *Verify on v18:* the 18 line's backoffice package must expose the same `UmbLocalizationEntry`
  type. Checked at port time, not assumed.

A vitest test asserts the four shapes (1/N created × 0/1/N skipped, with the "0 skipped omitted"
case). It calls the term value directly, so it needs no DOM.

### D3. Refusal: a third `PreviewFailure` outcome, shared by preview and import

`previewFailure(status)` returns `"absent"` for 404, **`"refused"` for 401 and 403**, and
`"failed"` otherwise (including `undefined`).

- Preview: `refused` sets a refusal state that renders the new term `importRefused` in place of
  `importSourceFailed`. The panel stays, and nothing is withdrawn.
- Import: `refused` shows `importRefused` in the error slot instead of `importFailed`, focused the
  same way.

One term covers both statuses, with wording such as: "The site refused this request. Your session
may have ended, or you may no longer have permission to change closures. This is not a problem with
the holiday source."

- *Alternative rejected: separate 401 and 403 messages.* Umbraco's HTTP interceptor already handles
  401 by prompting re-login and retrying, so a 401 reaching this code means that path was declined
  or failed. The operator's next step is the same either way (sign in again, or ask an
  administrator), and the spec requires naming both causes rather than guessing.
- *Alternative rejected: reuse `notPermittedWrite`.* That sentence asserts missing permission as
  fact, which is false after an expired session.

The existing test *does not call a refusal an absence* keeps its point (not `absent`) and is
tightened to expect `refused`. A source failure must still map to `failed`, and the 500, 502 and
`undefined` cases stay.

### D4. Docs for the booking-page window

One sentence is added after the existing paragraph in `docs/backoffice.md`. In substance: a closure
that starts after today leaves the list starting today, and its dates show nothing available. No
pin: the paragraph it extends isn't pinned, and the sentence restates behaviour the
`site-closures` spec already fixes.

### D5. The 2048 guard derives from the constant

A test in the existing documentation-truth family asserts that `docs/configuration.md` states
`SettingRow.MaxValueLength`'s value, formatted, **exactly once**. It also asserts that no other
"N characters the store holds" figure appears. `SettingRow` is `internal`: the test reads it the
way `SettingsControllerTests` already does (`InternalsVisibleTo` is in place).

- *Alternative rejected: remove the number from the docs.* A site author needs the figure, and
  "the store's limit" tells them nothing.

**Show it fires:** temporarily change the doc's number, or the constant in a scratch build, and see
the test fail.

### D6. Readme link guards accept `tree/` as a repository address

Both guards in `VersionTruthTests` derive `blobPrefix`. They now recognise
`<repo>/blob/<ref>/` **and** `<repo>/tree/<ref>/` as addressing this repository, and apply the same
checks: https, ref derived from the declared version, and the path present in the working tree (a
directory for `tree/`).

- The *exists in the working tree* half must check `Directory.Exists` for a `tree/` link and
  `File.Exists` for a `blob/` link. A folder link to a file, or a file link to a folder, is an
  offender.
- **Show it fires:** add a `tree/main/docs` link to a scratch copy of the readme, or as a test
  input if the guard is factored to take text. Both guards must fail, naming it.

Today the readme has no `tree/` links, so this changes no current output. That's why the
fire-check is mandatory.

### D7. Client skips fail CI

`npm test` gains a JSON report: `vitest run --reporter=default --reporter=json
--outputFile.json=<results dir>/vitest.json`, passed in CI so that local runs are unchanged. A
small step, `Assert-ClientTestResults.ps1` beside `Assert-TestResults.ps1`, reads it and fails
when any of the following holds:

- the file is missing;
- `numTotalTests` is 0;
- any assertion result has status `skipped`, `pending` or `todo`. Each one is named with its file
  and full name.

It runs `if: !cancelled()`, like the .NET check.

- *Alternative rejected: fold it into `Assert-TestResults.ps1`.* That script's whole contract is
  "every expected .NET project reported, by TRX". Mixing in a second format and a second notion of
  "expected" makes both harder to reason about.
- *Alternative rejected: JUnit XML.* Vitest's JUnit output marks skips as `<skipped/>` but folds
  `todo` into the same element. JSON names each status, so the failure message can say which it
  was.
- `.only` is already refused: vitest's `allowOnly` defaults to false when `CI` is set.

**Show it fires:** on a scratch branch, add `it.skip` to one client test and run the script against
the produced report locally. It must fail and name the test. Then do the same with `it.todo`.

Then the README sentence widens from "any .NET test project … skipped" to cover the client suite.
The README is pinned in places, so update any pin in the same commit.

### D8. The audit restores and lists both configurations

`audit.yml` restores the line once per configuration (Debug, Release), and
`Assert-NoVulnerablePackages.ps1` runs `dotnet list … --vulnerable` once per configuration. It
unions the findings and keeps the project and configuration with each.

- **Suppression evaluation is already per configuration** (`$configurations = @('Debug','Release')`)
  and does not change. What changes is that *findings* are now gathered from both, so a
  Release-only finding can no longer be invisible.
- `dotnet list` reads the assets file, which is per restore rather than per configuration. Where
  the graphs coincide, the second restore gives the same answer. Where they differ (a conditional
  `PackageReference`), the last restore wins. So the script restores and lists **per
  configuration, in sequence**: restore Debug, list; restore Release, list. A union taken from one
  restore would silently be one configuration only.
- A finding seen in both configurations is reported once, listing both.
- **Show it fires:** add a test fixture JSON (the script already takes an optional pre-captured
  JSON input) where a finding exists only in the Release capture, and see it fail.

### D9. Packaging spec: take `dev/v18`'s text verbatim

The delta is `dev/v18`'s requirement byte for byte, including "`18.1.0` included". That makes the
two lines' specs identical, which is the point. "`18.1.0` included" is still true now `18.2.0`
exists, so it is not stale, just an example.

**Guarantee diff (CLAUDE.md):** `main`'s requirement has four scenarios and three SHALL sentences.
All are carried verbatim. One scenario is added. The changed prose is two paragraphs:

- the first moves from present to past tense ("lists … which is false" becomes "was able to
  describe");
- the second, "exists only in prose", becomes "would live only in prose", followed by the new
  paragraph saying the feed now carries the bound.

Nothing is dropped. On `dev/v18`, the same delta replaces the requirement with identical text, so
the port carries no packaging delta at all (see tasks).

## Risks / Trade-offs

- **[A port that drifts]** → The port re-runs every guard on `dev/v18`. The packaging delta is
  removed from the port branch rather than archived as a no-op, and the port's tasks list each item
  and where it was checked.
- **[CI parity makes both PRs red until one merges]** → Expected, and recorded in the PR
  descriptions. Merge `main`'s first.
- **[D8 doubles audit time]** → Restore and list take seconds per line, and the audit is weekly and
  on demand.
- **[D7: vitest's JSON shape changes in a minor]** → The script fails closed when the file lacks
  `testResults`/`numTotalTests`, with a message naming the vitest version. A silent pass is the only
  unacceptable outcome.
- **[D3 changes a test that pinned the old mapping]** → That test's own comment states the goal
  (a refusal is not an absence). The new expectation is stricter, not looser.

## Migration Plan

None. No schema change and no configuration change. The release change that follows bumps the
versions.
