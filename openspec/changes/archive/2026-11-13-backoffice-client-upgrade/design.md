## Context

See proposal.md, *Why*. The facts the approach rests on come from the explore of 2026-11-13. It was run
on `dev/v18` in scratch copies, against that line's backoffice OpenAPI document saved from its running
TestSite:

- **The method was proven before it was used.** Run with the *current* 0.85.2, against the saved
  document, the generator reproduced the committed `src/api` byte for byte. So every difference 0.99
  produced is the generator's, not the document's.
- With 0.99.0 and `@umbraco-cms/backoffice` 18.3.1:
  - `types.gen.ts` was identical;
  - `sdk.gen.ts` kept `UBookItBackofficeService` and the same 37 static method names, adding explicit
    `RequestResult<…>` return types;
  - `index.ts` moved from `export *` to named exports;
  - `client/**` and `core/**`, the fetch runtime, were rewritten (about 400 lines changed).
- With the generator-script change of D2, `tsc --noEmit` (strict) passed, as did vitest (391/391) and
  the Vite build, with no edit to section code.
- **`main` was not measured.** Its document comes from Umbraco 17's generator (Swashbuckle, OpenAPI
  3.0), not 18's, so its generated diff may differ. Its section code also has to type-check against
  17.8.1, not 18.3.1.

Two constraints from earlier changes shape the approach. **Generated client files can't be
cherry-picked between lines:** each line generates from its own document, and that is why the lines'
`src/api` already differ. And **the section relies on a failed request *throwing*** (`thrownStatus`,
learned in tidy-2026-10). That behaviour comes from Umbraco's interceptors, bound by
`authContext.configureClient(client)`. It is applied *through* the generated runtime, and the
runtime's error path is what changed.

## Goals / Non-Goals

**Goals:**
- Each line's client builds against its own Umbraco line's backoffice package with no ERESOLVE and no
  engine warning, on a Node that package declares support for.
- The generated client's *contract* (types, method names, the operation IDs the guards pin) is
  unchanged, and that is shown by the existing guards passing without edits.
- The changed runtime is shown to behave the same where the section depends on it, by a live
  check on each line.

**Non-Goals:**
- Tidying the section code for anything 0.99 or the newer types make *possible*. If it compiles
  and behaves the same, it stays as it is.
- Pinning exact patch versions of the npm packages. Caret ranges plus the committed lockfile are
  this project's existing convention.

## Decisions

### D1. Each line regenerates from its own running TestSite, and the baseline is proven first

On each line, before upgrading anything, regenerate with the current generator from that line's live
document and confirm it reproduces the committed `src/api`. Then upgrade and regenerate.

*Why:* the baseline is what makes the diff attributable. If the baseline does not reproduce, the
committed client is already stale against its own server, and that is a separate finding to report
before going further. It must not be folded into this diff.

*Rejected:* reusing the explore's saved document. It is a `dev/v18` snapshot, so it is wrong for
`main` and could be stale for `dev/v18` by apply time.

### D2. Use `operations: { strategy: 'byTags', containerName: '{{name}}Service' }`, not the deprecated options

| Option | Result on `dev/v18` | Verdict |
|---|---|---|
| Keep `asClass` + `classNameBuilder` | Works, but warns that it is deprecated | Rejected: likely gone by 1.0, and the same warning would follow us into the v19 port |
| `strategy: 'byTags'`, `containerName: '{{name}}Service'` | SDK **byte-identical** to the deprecated output; no warnings | **Chosen** |
| `strategy: 'single'` | Same class name, but **1** static member: the operations nest differently | Rejected: changes every call site |
| `strategy: 'flat'` (default) | Standalone functions | Rejected: rewrites every call site for no gain |

`byTags` depends on every operation carrying the one `UBookItBackoffice` tag. A future controller in
a different tag would produce a second `…Service` class. `tsc` catches that only when an **existing**
operation moves to the new tag, because its `UBookItBackofficeService.x` call sites break. A **new**
operation under a new tag lands in a new class, and nothing flags it: `GeneratedClientTests` reads
only `types.gen.ts`, `main` has no method-name guard, and `dev/v18`'s
`OpenApiTransformerTests` matches `public static` names across the whole file, whatever class they are
in. That case is harmless, since nothing calls the new class until someone writes the call, and
writing it shows the class name. *(Corrected in QA round 1: this paragraph previously claimed the
method-name guard and `tsc` would both see a second class.)*

`runtimeConfigPath` becomes `'./src/hey-api'`. 0.99 resolves it from the working directory (the
`Client` folder) and computes the import from the output folder, so `'../hey-api'` became
`'../../../hey-api'`.

### D3. The client guards are run unchanged, and a failure stops the change

`GeneratedClientTests` and `OpenApiTransformerTests` assert on the generated files. *(Corrected at
apply: `OpenApiTransformerTests`, which pins the SDK's method names, exists on `dev/v18` only; it
came with that line's OpenAPI generator swap. On `main`, `GeneratedClientTests` (two tests over
`types.gen.ts`) is the only automated guard, so 3.2's sorted method-name comparison is what checks
the names there.)* If either goes
red after regeneration, the client changed in a way the explore did not predict. The response is to
stop and report what changed. **Adjusting a pattern to match the new output is not allowed in this
change**: that would make the guard agree with whatever the generator now does, which is the failure
it exists to prevent. The one legitimate exception is a pattern that no longer matches *anything*
(each guard's non-empty check would report "the pattern is broken"). That would be raised with Chris
as a finding first, not fixed in passing.

### D4. A live check on each line, aimed at the changed error path

The generated runtime is shipped and no automated test exercises it against Umbraco's interceptors.
The vitest suite tests section logic with the client mocked. On each line, with a backoffice user
logged in, the check covers:

1. **Happy path:** the bookings list loads (a generated GET through the configured client).
2. **A write that succeeds:** create a site closure, then delete it. The site's data ends where it
   began.
3. **A failure that must throw:** the case tidy-2026-10 fixed. A 404 from the holiday source
   withdraws the import panel through `thrownStatus`. If that throw no longer arrives, the panel
   stays, and the check can see it.
4. **A refusal the user is shown:** a request the server refuses (a 400 from an invalid closure),
   with the backoffice's error notification and the section's own message both as before.

Each check is recorded against the same check on the *un-upgraded* line, run first. That makes it a
comparison, not a description, and shows that the check can fail.

### D5. A guard holds CI's Node to the backoffice package's declared engine

This gap was open for five months without anyone noticing: 18.0.0 declared Node >= 24.13 while CI ran 22.
A test in `UBookIt.Tests`:
- parses `ci.yml` and `publish.yml` as YAML (YamlDotNet, which the workflow guards already use) and
  reads every `actions/setup-node` step's `node-version`;
- reads `engines.node` for each of the client's **direct** `devDependencies` from
  `package-lock.json`, the versions actually installed;
- asserts that each pinned major satisfies **every** direct dependency's range. `setup-node` resolves
  a bare major `M` to that major's latest release, so the question per range is "does the latest
  `M.x.y` satisfy it?". A range is alternatives joined by `||`; the pin passes if any alternative
  admits it:
  - `>=a[.b[.c]]` admits `M` when `M >= a`. A pin of `24` satisfies `>=24.13`;
  - `^a[.b[.c]]` (with `a > 0`) admits `M` when `M == a`.

**It fails rather than passes on anything it cannot read:**
- no `setup-node` step found, or no direct dependency carrying an `engines.node`;
- a `node-version` that is not a bare major;
- an alternative in any other form (`~`, `<`, `x`-ranges, hyphen ranges, `^0.x`).

*Amended at apply (Chris, 2026-11-13).* As proposed, the guard required each range to be a single
`>=` bound. But four of the client's eight direct devDependencies declare compound ranges (vite
`^20.19.0 || >=22.12.0`, vitest `^20.0.0 || ^22.0.0 || >=24.0.0`, chalk, node-fetch), so that guard
would have been red on day one. Interpreting `||` of `>=` and `^` covers every range the direct
dependencies use today. It also means a vite or vitest that raises its floor turns the guard red,
not only the backoffice package.

Direct dependencies only, because they are the ones we chose and can act on. Its ability to fire is
shown at apply: setting a pin back to `22` turns it red.

*Rejected:* `engine-strict=true` in an `.npmrc`. It fails `npm ci` itself, but only once someone runs
it on an old Node. It also checks transitive packages, and it says nothing about what CI is *pinned* to.

### D6. Order: `main` first, then `dev/v18`; only hand-written files are cherry-picked

These are hand-written and cherry-picked from `main` to `dev/v18`:
- `scripts/generate-openapi.js`;
- the two workflows;
- `README.txt`;
- the guard;
- the openspec artifacts.

These are produced on each line separately, by its own `npm install` and D1 run:
- `package.json` (which differs only in the backoffice version);
- `package-lock.json`;
- `src/api/**`.

### D7. Node 24 on the development machine, through winget, at apply, with Chris told first

`winget`'s `OpenJS.NodeJS.LTS` now installs **26**, not 24, so the install names the version
explicitly. It replaces the 22.13.0 on PATH, which this repo was the only known user of. Visual Studio's
own Node is separate and unaffected. The step is a machine-wide change, so it is announced before it
runs, not folded silently into a task.

## Risks / Trade-offs

- **[`main`'s section code may not type-check against 17.8.1]** The explore only checked 18.3.1. →
  Measured as the first step on `main` (D1). A type error is fixed narrowly, or brought back to Chris
  if the fix is not narrow.
- **[`main`'s regenerated diff may differ from `dev/v18`'s]** Different generator, different OpenAPI
  version. → D1's baseline-then-diff, and D3's unchanged guards.
- **[The error path behaves differently live]** The highest-consequence risk: the section would
  misclassify failures, as it did before tidy-2026-10. → D4, compared against the un-upgraded line.
- **[Stale hashed assets after rebuilding the client]** A known trap. → Delete both the `wwwroot`
  bundle folder and `obj/<config>`, and check each line's unit total against its baseline before
  believing a run.
- **[Lockfile churn hides something]** `npm install` under npm 11 rewrites much of `package-lock.json`.
  → The review covers `package.json` and `src/api`. The lockfile is accepted as produced, with its
  resolved top-level versions recorded in tasks.
- **[A caret range drifts]** `^0.99.0` admits 0.99.x only (0.x caret semantics), and
  `^17.8.1` / `^18.3.1` stay within their major. → Acceptable. The lockfile fixes what CI installs.

## Migration Plan

Nothing is deployed and nothing persists. Rollback is reverting the commits on each line. The
development machine's Node can be rolled back with winget if needed. The shipped bundle changes only
when a release carries it, and that release's CHANGELOG names the change (proposal, *Impact*).
