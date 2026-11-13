## 1. Baseline and setup (worktree `..\uBookIt-v19`, branch `run-on-umbraco-19` from `dev/v19` `55e1a18`)

- [ ] 1.1 Record the starting point before changing anything:
      - `git stash list` shows the explore spike as `stash@{0}`;
      - `dev/v19` equals `origin/dev/v18` `55e1a18`;
      - database `uBookIt_v19` is ONLINE, and `uBookIt_v18` is untouched (both queried);
      - the v18 secrets id `a220bfe8…` still points at `uBookIt_v18`.

      `dev/v18`'s totals stand as this line's baseline: unit 2516, integration 222, rendering 1211,
      vitest 391, from backoffice-client-upgrade 5.3. Verify: the four facts are recorded here.
- [ ] 1.2 Seed `umbraco-feedback.md` in this change folder from the explore (design D9). Cover:
      - the upgrade page's silence on how a package's own Management API controllers should
        authenticate;
      - the undocumented `CanSendRequiredEmail` removal;
      - .NET 11's OpenAPI 3.2 default for documents registered with plain `AddOpenApi`, against
        Umbraco's 3.1 pin;
      - the misleading "session timed out" prompt when a package endpoint returns 401;
      - the database-irreversibility note, which is good and worth keeping.

      Verify: the file exists, and each entry says what happened, how it was found and what would
      have helped.

## 2. Retarget

- [ ] 2.1 Set `global.json` to SDK `11.0.100`, every `TargetFramework` to `net11.0`, and in
      `Directory.Packages.props`:
      - Umbraco `[19.0.0-rc1,20.0.0)`;
      - `Microsoft.EntityFrameworkCore.SqlServer`/`.Design` and `Microsoft.AspNetCore.OpenApi` `11.0.0`;
      - comments updated to name 19 / .NET 11.

      Verify: `dotnet restore` succeeds with no NU1605/NU1608. `git grep "net10.0"` outside tests and
      the archive finds nothing.
- [ ] 2.2 Set `Directory.Build.props` to `<Version>19.0.0-rc1</Version>` (design D5). Verify: recorded
      here. Its guard consequences are handled in 4.3.
- [ ] 2.3 Commit the TestSite's own `UserSecretsId` `80f44b82-1c52-4294-8c73-12962fc8ab26`, and update
      the csproj comment to cover main / v18 / v19 (design D8). Verify: the secrets for that id name
      `Database=uBookIt_v19`, and v18's id still names `uBookIt_v18`.

## 3. Source changes

- [ ] 3.1 `UBookItAuthorizationComposer`: all three policy sites use
      `policy.AddAuthenticationSchemes(OpenIddict…, Umbraco.Cms.Core.Constants.Security.BackOfficeAuthenticationType)`
      (design D1). A comment cites RC1's `BackOfficeAuthPolicyBuilderExtensions` and design D2's
      CSRF stance. Verify: `git grep -c BackOfficeAuthenticationType` gives 3 in that file, and no
      `AuthenticationSchemes.Add(` remains.
- [ ] 3.2 Replace `CanSendRequiredEmail()` with `IsEmailConfigured()` at the 3 call sites (design D3).
      Verify: `git grep CanSendRequiredEmail -- src` finds nothing, or only a comment that explains
      the rename.
- [ ] 3.3 `UBookItDeliveryApiComposer`: `options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1`, with a
      comment naming Umbraco's own pin (design D4). Verify: the document fetched in 7.1 declares
      `3.1.x`.
- [ ] 3.4 Build the client first, then `dotnet build UBookIt.slnx`. The shipped projects must have 0
      errors and 0 warnings. Verify: recorded here. Any error other than those named in design
      *Context* is recorded and judged before it is fixed.

## 4. Tests

- [ ] 4.1 Implement the new interface members on the test doubles (design D3):
      - `IEmailSender` (5 doubles): `IsEmailConfigured` gives the double's existing answer, and
        `IsEmailAvailableAsync` gives `Task.FromResult(IsEmailConfigured())`;
      - `IRuntimeState.CurrentMigrationVersion` (4 stubs): the value the stub's state implies.

      Verify: every test project compiles.
- [ ] 4.2 Run unit, integration and rendering, one at a time. For **every** failure, record:
      - the message;
      - its class (framework or host-major literal, version-derived, interface member, or other);
      - the fix (design D6).

      An "other" stops apply and is raised with Chris. Verify: the table is here, and no test was
      deleted or skipped.
- [ ] 4.3 Version-guard fallout from 2.2 (design D5): change only the literals a failing version guard
      names. Verify: each one is listed with its guard; no prose that says "Umbraco 18" is changed
      unless a guard names it; and the remaining line-wording obligations are listed for
      `release-19-0-0`.
- [ ] 4.4 Add to `OpenApiTransformerTests` an assertion that the backoffice and delivery documents
      each declare OpenAPI `3.1.x` (design D4). Verify: it passes, and it **fails** with 3.3's line
      removed (a mutation, reverted, with the revert confirmed by content comparison using absolute
      paths).
- [ ] 4.5 Clean Release build (`--no-incremental`, after deleting the bundle folder and `obj/Release`):
      0 warnings, 0 errors. Then all three suites. Verify:
      - the totals are recorded and compared with 1.1's: the differences are only 4.4's assertions
        and any test 4.2 recorded;
      - `GeneratedClientTests`, `OpenApiTransformerTests` and `NodeEngineGuardTests` pass;
      - the Node guard reads backoffice 19.0.0-rc1's `engines.node`.

## 5. Client

- [ ] 5.1 `npm install --save-dev @umbraco-cms/backoffice@19.0.0-rc1` (no `--force` and no
      `--legacy-peer-deps`). Verify:
      - no ERESOLVE and no EBADENGINE;
      - `npm ls` shows 19.0.0-rc1 with hey-api 0.99.0 deduped.
- [ ] 5.2 With the RC1 TestSite running, run `npm run generate-client`. Verify:
      - `types.gen.ts` and the 37 method names are unchanged;
      - every difference is listed (the explore expects at most the `baseUrl` trailing slash).
- [ ] 5.3 `npx tsc --noEmit`, `npm test`, `npm run build`. Verify: green, vitest 391, and no
      section-code edit, or each one listed with its reason.

## 6. Live: identities (design D7, steps 1–3)

- [ ] 6.1 API user, with `v19-auth-probe.ps1` extended by one verb-gated write that leaves no
      residue:
      - anonymous gives 401;
      - a token gives 200 on settings and closures;
      - a closure create then delete gives 200 / 200.

      Verify: the output is recorded.
- [ ] 6.2 Editor, with Chris logged in:
      - the backoffice-client-upgrade D4 set (bookings list and the 67-day refusal; closure create
        and delete; holiday 404 withdraws the panel and 500 keeps it; the invalid closure's
        message);
      - find a booking by email (sensitive data).

      Verify: each step matches what dev/v18 showed in backoffice-client-upgrade 5.4, and **no
      "session timed out" prompt** appears. Network records show uBookIt calls carrying no
      `Authorization` header.
- [ ] 6.3 Refusal through the cookie: Chris removes one uBookIt verb from his group. The matching
      action gives **403**, with the section's refusal message, not 401 and not the sign-in prompt.
      Chris then restores it. Verify: the 403 is recorded, and the permission is confirmed restored.

## 7. Live: delivery and documents (design D7, step 4)

- [ ] 7.1 Fetch both OpenAPI documents and diff them, normalised, against dev/v18's saved copies.
      Verify: the only differences are the `openapi` field (now `3.1.x`) and the server URL's
      trailing slash, or each other difference is listed and judged.
- [ ] 7.2 With the editor's cookie present in the browser:
      - an anonymous delivery read gives 200;
      - a delivery placement with body-only contact details is placed, and its booker reflects the
        body only (*Ambient identity is not trusted for writes*).

      The test booking is cancelled afterwards. Verify: recorded, including the cancellation.

## 8. Close

- [ ] 8.1 Fill in `umbraco-feedback.md` from apply (design D9), and record the explore spike stash's
      fate (dropped once nothing in it is unused). Verify: every friction point met during apply has
      an entry.
- [ ] 8.2 `openspec validate --all --strict` with the pinned CLI, after the last commit. Verify:
      passes.
- [ ] 8.3 QA (`qa-review`): a fresh subagent, reused across rounds, told what is claimed and asked to
      verify it. The live checks are judged by method unless QA asks for a repeat. Verify:
      APPROVE, or APPROVE WITH NITS, with the nits resolved or recorded.
- [ ] 8.4 Merge `run-on-umbraco-19` into `dev/v19` locally (no remote; design *Migration Plan*).
      Record the owed follow-ups in the deferred-obligations record:
      - the 19.0.0-final bump;
      - CI for three lines, before the first push;
      - `release-19-0-0`'s line wording and the SameSite docs sentence.

      Then archive. Verify: `dev/v19` contains the change, and the obligations are written.
