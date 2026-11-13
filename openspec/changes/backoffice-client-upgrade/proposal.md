## Why

The backoffice client on **both lines** still builds against `@umbraco-cms/backoffice` **17.5.3**, while the
server floor is Umbraco 17.8.0 on `main` and 18.3.0 on `dev/v18`. That is the type declarations the
section is checked against, not what runs. At runtime the host supplies the real backoffice, so the
section is type-checked against an Umbraco up to a whole major older than the one it runs in.

It cannot move today. From 17.8.x and 18.x, `@umbraco-cms/backoffice` peer-requires
`@hey-api/openapi-ts >=0.99.0 <1.0.0`. We generate the typed API client with `^0.85.2`, so npm refuses
the install (ERESOLVE). `umbraco-floor-17-8` found this on `dev/v18` and split it off as "v18 only".
The explore of 2026-11-13 found that **17.8.1 carries the same peer**, so `main` is in the same
position, and this change covers both lines.

Now, because the Umbraco 19 port needs the same generator (19.0.0-rc1's templates pin `^0.99.0`). Doing
it first on lines that already work keeps the generator's diff and Umbraco 19's apart, so a broken
client after the port can be attributed.

The packages also declare **Node >= 24.13 and npm >= 11**, and have since 18.0.0 (17.8.1 and 19 too).
CI pins Node 22. npm only warns because `engine-strict` is off, but we build with a Node the
package does not claim to support.

## What Changes

**Both lines** (`main` first, then ported to `dev/v18`):

- **Upgrade the generator.** `@hey-api/openapi-ts` `^0.85.2` → `^0.99.0`.
- **Upgrade the backoffice types.** `@umbraco-cms/backoffice` `^17.5.3` → `^17.8.1` on `main`, and
  `^18.3.1` on `dev/v18`.
- **Update `scripts/generate-openapi.js` for 0.99:**
  - `runtimeConfigPath` `'../hey-api'` → `'./src/hey-api'`. 0.99 resolves it from the working
    directory, so the old value generates an import of `../../../hey-api` and the client fails to
    compile (TS2307).
  - replace the deprecated `asClass` / `classNameBuilder` with
    `operations: { strategy: 'byTags', containerName: '{{name}}Service' }`. On `dev/v18` this
    produced an SDK byte-identical to the deprecated options (measured).
- **Regenerate `src/api`** from each line's own running TestSite, and review the diff.
- **Node 24.** `node-version: 22` → `24` in `ci.yml` and `publish.yml`. This matches the Node major
  Umbraco builds its own backoffice with (`.nvmrc` = `24` in 19.0.0-rc1). Install Node 24 on
  the development machine, which today has 22.13.0 on PATH; Chris approved this. Correct the stale
  "Node LTS Version 20.17.0+" in `src/UBookIt.Backoffice/README.txt`.
- **Add a guard that holds CI's Node to the backoffice package's declared engine**, so a later
  package update that raises the requirement fails a test, not a warning nobody reads.

Nothing a site, an editor or a consumer of either API sees is meant to change. The generated runtime
is rebuilt, so the **shipped backoffice bundle changes**, and the next release's CHANGELOG owes it a
line (see Impact).

## Non-goals

- **The Umbraco 19 port.** That is its own change on `dev/v19`, which this change precedes.
- **Changing any API, contract model or operation ID.** If regeneration changes `types.gen.ts` or a
  method name, that is a finding to stop on, not something to absorb. On `dev/v18`, `types.gen.ts` came
  out identical and all 37 method names were unchanged.
- **Raising the Umbraco floor.** This moves client-side type declarations only. The NuGet floor stays
  `[17.8.0,18.0.0)` / `[18.3.0,19.0.0)`. A release for next week's Umbraco security patches is a
  separate change.
- **Releasing.** No version bump here. The release change carries the CHANGELOG entry.
- **Moving the Node pin into an `.nvmrc`.** `continuous-integration` requires the Node major to be
  fixed *in the CI definitions*, and the workflow files already satisfy that.
- **Node 26.** It is the newest LTS, but Umbraco builds on 24. We follow the package.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

None. `continuous-integration` *The toolchain is pinned* requires the Node major to be fixed in the CI
definitions, but names no version. Moving the pin from 22 to 24 keeps that requirement satisfied
without changing it. No behaviour any other spec describes is meant to change, and the live check (design D4)
is how that is verified. The change sets `skip_specs: true`.

## Impact

- **Client, both lines:** `src/UBookIt.Backoffice/Client/package.json`, `package-lock.json`,
  `scripts/generate-openapi.js`, and the regenerated `src/api/**`. Section code is not expected to
  change: on `dev/v18`, `tsc` (strict), vitest 391/391 and the Vite build were green with no edits.
  That was **measured offline on `dev/v18` only**. `main` is measured at apply.
- **CI, both lines:** `.github/workflows/ci.yml`, `.github/workflows/publish.yml`.
- **Tests:** a new Node-engine guard in `UBookIt.Tests`. `GeneratedClientTests` and
  `OpenApiTransformerTests` read the generated files with patterns, and must stay green **unchanged**:
  editing one to fit the new output would hide exactly what they exist to catch.
- **Shipped output:** the `UBookIt.Backoffice` package's bundle under
  `wwwroot/App_Plugins/UBookItBackoffice`. The generated fetch runtime changed **on the error path**
  (how `throwOnError` is resolved and how error interceptors are chained), and the section relies on
  failed requests *throwing* (`thrownStatus`, tidy-2026-10). That is checked live on both lines.
- **Documentation:** no `docs/*.md` page changes. Nothing in `docs/` states the Node version or the
  generator; the only Node statement is the contributor-facing `src/UBookIt.Backoffice/README.txt`,
  which is not packed or published. The next release's CHANGELOG entry must say the backoffice client
  was regenerated against the current Umbraco types, with no intended behaviour change.
- **Development machine:** Node 24 (and its npm 11) on PATH. Visual Studio uses its own Node and is
  unaffected.
- **Supersedes** the deferred obligation "v18's client to `@umbraco-cms/backoffice` 18.x is its own
  change", which said v18 only.
