## Why

`17.3.0` and `18.2.0` shipped closure date ranges, and the maintainer's own site went live on
`18.2.0`. Using it in production showed two rough edges. A pile of small debts has also built up:
each one is cheap, and each one is something tidying loses to feature work if it isn't batched.
The project's standing preference is to spend these in a patch rather than carry them. This is that
patch's content, on both lines.

Every item was checked against the code on 2026-10-13. Each is still open, and none changes a
public API.

## What Changes

**Product (ships in the packages)**

1. **The Closed periods hints sit under their own fields.** In the resource editor, the To hint and
   the Note hint are rendered once, after every period, so the ids stay unique. Every period's
   inputs point at them, but on screen they float under the whole section as stray text (seen live
   on the maintainer's site). Each period now renders its own hints, under its own fields, with
   ids indexed by the period's position. The accessible association is unchanged in kind: each
   input still names its hint, but now the hint it names is the one beside it.
2. **The holiday import summary pluralises.** "Created 1 closures. 0 rows were skipped." becomes
   "Created 1 closure." When nothing was skipped, the skipped sentence is left out. When rows were
   skipped, the count is pluralised correctly. Umbraco's localization accepts a function as a term
   value, so this needs no plural machinery of our own. (The note recording this debt said the
   client had none. It doesn't need any.)
3. **A refused holiday request no longer blames the holiday source.** A 401 or 403 on the preview
   currently says "The site's holiday source could not be reached". That sends an operator to debug
   a feed that is fine, when their session ended or their permission was withdrawn. A refusal
   becomes a third outcome with its own message, on the preview and on the import. As today, it
   does not withdraw the panel: a refusal is not an absence.

**Documentation (ships in the packed readme and `docs/`)**

4. **The booking-page window: a closure that starts later doesn't move it.** `docs/backoffice.md`
   already says the list starts after a closure that covers *today*. It doesn't say the other half,
   which surprised the maintainer: a closure starting tomorrow leaves the list starting today, and
   its dates simply show nothing available. One sentence.
5. **The v18 readme's version example names the 17 line only.** "uBookIt `17.x` is for Umbraco 17"
   is the only example of the major following the Umbraco major. On the 18 line it reads as though
   the reader is on the wrong package. Both lines get the same sentence, naming both majors.
6. **`docs/configuration.md` states "2048" as a literal.** No guard ties it to
   `SettingRow.MaxValueLength`, so changing the constant would leave the docs wrong with the suite
   green. A documentation guard now derives the number from the constant.

**Specification (main only)**

7. **`main`'s packaging spec still says the Umbraco bound "exists only in prose"** and that
   "nothing a resolver reads has ever contradicted" the claim to support both majors. Both have
   been false since `17.1.2`/`18.0.0`, and `dev/v18` corrected them in `18.1.0`. `main` takes
   `dev/v18`'s wording, so the two lines' versions of the requirement become identical.

**CI (repository only, nothing ships)**

8. **A skipped client test passes CI.** `Assert-TestResults.ps1` reads .NET TRX only, so a vitest
   `it.skip` or `it.todo` goes green. That breaks `continuous-integration`'s "A skipped test fails
   the run", which says *any* test. The client suite now writes a machine-readable report, and the
   run fails on a skipped or todo test, naming it, and on a report that ran zero tests. Once that
   holds, the readme's skip claim, currently scoped to ".NET test projects", is widened.
9. **The readme link guards check only `blob/` links.** A `tree/<ref>/` folder link pinned to a
   branch would pass both guards. The requirement already covers it ("every link … that addresses
   a document in this repository"), so this is the guard catching up with its own requirement.
10. **The audit never sees a finding that exists only in the Release graph.** `audit.yml` restores,
    and `dotnet list` reads, Debug only, while CI builds and `publish.yml` packs Release. The audit
    now restores and lists both configurations.

**Release**

- **This is the content of patch releases `17.3.1` and `18.2.1`.** It goes on `main` first, then is
  ported to `dev/v18`. Item 7 does not port, because `dev/v18` already carries that wording.
  Items 8–10 must land on both lines because of the CI parity check. **The release itself is a
  separate change**, as `release-17-3-0` was, so the version bump, changelog and publication don't
  hold up this change's QA.
- No breaking change. No public type, member, endpoint, setting or migration is added or changed.
  Two user-visible strings change (items 2 and 3) and one is added (item 3). All are backoffice
  wording.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `packaging`: *The package declares which Umbraco majors it accepts*. The prose moves to the past
  tense, and the scenario *The newest version of each line tells a resolver which line it is* is
  added. This is `dev/v18`'s text verbatim. Main only.
- `public-holidays`: an ADDED requirement. A refusal of the operator's request is reported as a
  refusal, neither as a failure of the source nor as the source's absence. It's ADDED rather than
  MODIFIED so that *A source's failures and oddities are the package's to absorb* is not replaced
  wholesale for one new case.

Items 1, 2, 4, 5 and 6 change no requirement. Items 8–10 bring the implementation into line with
requirements that already say the right thing, so they change no spec either.

## Non-goals

- **The single-choice shortcut** (skipping a choice of one resource or service). That is the next
  feature, a behaviour change, and a minor.
- **The other open debts.** These stay where they are:
  - the 403 runbook heading, which waits on an observed workflow failure;
  - the unexplained `PackageCompositionTests` failure, which waits on a captured repro;
  - the hidden-Settings-tab convention.
  None of them was verified ready for this batch, and widening a tidy is how a tidy stops being
  one.
- **The jurisdiction-leak sweep's `.cshtml` blind spot.** It is safe while closures have no public
  surface, and that is recorded as the reason it should change *with* such a surface.
- **Translating the new wording.** The package ships `en-US` only.

## Impact

- `src/UBookIt.Backoffice/Client/src/section/resource-editor.element.ts`: per-period hints.
- `src/UBookIt.Backoffice/Client/src/section/holiday-fields.ts`, `closures-view.element.ts`,
  `localization/en-us.ts`: the third failure outcome, the refusal term, and the pluralised summary.
- `tests/UBookIt.Tests/ClosureAccessibilityTests.cs`, `holiday-fields.test.ts`: updated and new
  guards.
- `docs/backoffice.md`, `docs/configuration.md`, `README.md`: item 4, the guarded 2048, and item 5
  (and the skip claim, item 8).
- `tests/UBookIt.Tests/VersionTruthTests.cs` (the readme link guards) and a documentation guard for
  the 2048.
- `.github/workflows/ci.yml`, `.github/workflows/audit.yml`, `scripts/ci/Assert-TestResults.ps1`
  (or a sibling script), `src/UBookIt.Backoffice/Client/package.json`: items 8 and 10. These are
  under the parity check, so both lines' PRs go red until the first one merges. That is expected,
  and the second merge restores parity.
- `openspec/specs/packaging/spec.md` (main) and `openspec/specs/public-holidays/spec.md` (both lines),
  at archive.
- No new dependency. The vitest JSON reporter is built into vitest.
