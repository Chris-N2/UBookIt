## 1. Baseline and setup (worktree `..\uBookIt-v19`, branch `run-on-umbraco-19` from `dev/v19` `55e1a18`)

- [x] 1.1 Record the starting point before changing anything:
      - `git stash list` shows the explore spike as `stash@{0}`;
      - `dev/v19` equals `origin/dev/v18` `55e1a18`;
      - database `uBookIt_v19` is ONLINE, and `uBookIt_v18` is untouched (both queried);
      - the v18 secrets id `a220bfe8…` still points at `uBookIt_v18`.

      `dev/v18`'s totals stand as this line's baseline: unit 2516, integration 222, rendering 1211,
      vitest 391, from backoffice-client-upgrade 5.3. Verify: the four facts are recorded here.

      **Done 2026-11-13.**
      - `stash@{0}` is "On dev/v19: v19 RC1 spike 2026-11-13 (explore, throwaway)".
      - `dev/v19` = `origin/dev/v18` = `55e1a18`. This change's first commit is `214b570`.
      - Both databases are ONLINE. `uBookIt_v18`'s Umbraco state is `{AE533AF6-…}`, unchanged from
        before the explore (the explore's copy had the same state before its upgrade).
        `uBookIt_v19` is at `{17EA0D28-…}`, i.e. upgraded to 19.
      - Secrets: `a220bfe8…` points at `uBookIt_v18`, and `80f44b82…` at `uBookIt_v19`.
      - Baseline: dev/v18's 2516 / 222 / 1211 / 391.

- [x] 1.2 Seed `umbraco-feedback.md` in this change folder from the explore (design D9). Cover:
      - the upgrade page's silence on how a package's own Management API controllers should
        authenticate;
      - the undocumented `CanSendRequiredEmail` removal;
      - .NET 11's OpenAPI 3.2 default for documents registered with plain `AddOpenApi`, against
        Umbraco's 3.1 pin;
      - the misleading "session timed out" prompt when a package endpoint returns 401;
      - the database-irreversibility note, which is good and worth keeping.

      Verify: the file exists, and each entry says what happened, how it was found and what would
      have helped.

      **Done.** `umbraco-feedback.md` has an overall verdict and four entries, each with what
      happened, how it was found and what would have helped:
      1. package-owned controllers lock editors out;
      2. `CanSendRequiredEmail` removed without a note;
      3. plain `AddOpenApi` documents move to OpenAPI 3.2;
      4. `IRuntimeState.CurrentMigrationVersion` added.

      It also has a "what worked well" section, which includes the database-irreversibility note.
      `IRuntimeState` (entry 4) was found by the explore's build, so it is included here too.


## 2. Retarget

- [x] 2.1 Set `global.json` to SDK `11.0.100`, every `TargetFramework` to `net11.0`, and in
      `Directory.Packages.props`:
      - Umbraco `[19.0.0-rc1,20.0.0)`;
      - `Microsoft.EntityFrameworkCore.SqlServer`/`.Design` and `Microsoft.AspNetCore.OpenApi` `11.0.0`;
      - comments updated to name 19 / .NET 11.

      Verify: `dotnet restore` succeeds with no NU1605/NU1608. `git grep "net10.0"` outside tests and
      the archive finds nothing.

      **Done.**
      - `global.json` SDK `11.0.100`; 10 csprojs `net11.0`.
      - Umbraco `[19.0.0-rc1,20.0.0)` ×7.
      - EF Core SqlServer/Design and OpenApi `11.0.0`; the three comments updated.
      - `dotnet restore UBookIt.slnx` restored all 10 projects, with no NU1605/NU1608.

      **`git grep net10.0` outside tests and the archive: two exceptions, both left deliberately and
      recorded as owed:**
      - `docs/theming.md:22`, the example theme RCL's `<TargetFramework>`. This is line wording, owed
        by `release-19-0-0` (proposal Non-goals). On 19 it must read `net11.0`, because Umbraco 19
        packages are net11.0-only.
      - `scripts/verify-install.ps1:198`, `bin/Debug/net10.0`. **A pre-existing defect on dev/v18
        too:** the script creates its site with `dotnet new umbraco -r LTS`, an Umbraco **17** site,
        then installs the branch's declared version. So on any non-LTS line it installs an 18 or 19
        package on a 17 site. Changing only the path would make it more wrong. Owed: a per-line
        template choice, on dev/v18 and dev/v19, in its own change.

      **Instrument note:** `[IO.File]::ReadAllText`/`WriteAllText` dropped the UTF-8 BOM from
      `tests/UBookIt.Tests/UBookIt.Tests.csproj` (a spurious first-line diff). It was restored by
      comparing every changed file's first bytes with HEAD; that was the only file affected.

- [x] 2.2 Set `Directory.Build.props` to `<Version>19.0.0-rc1</Version>` (design D5). Verify: recorded
      here. Its guard consequences are handled in 4.3.

      **Done.** `<Version>19.0.0-rc1</Version>`, previously `18.6.0`.

- [x] 2.3 Commit the TestSite's own `UserSecretsId` `80f44b82-1c52-4294-8c73-12962fc8ab26`, and update
      the csproj comment to cover main / v18 / v19 (design D8). Verify: the secrets for that id name
      `Database=uBookIt_v19`, and v18's id still names `uBookIt_v18`.

      **Done.** `<UserSecretsId>80f44b82-1c52-4294-8c73-12962fc8ab26</UserSecretsId>`. The comment now
      says every line has its own id, and that this line inherited v18's id and was given its own by
      the port. The secrets check is the same as 1.1's: `80f44b82…` points at `uBookIt_v19`, and
      `a220bfe8…` at `uBookIt_v18`.


## 3. Source changes

- [x] 3.1 `UBookItAuthorizationComposer`: all three policy sites use
      `policy.AddAuthenticationSchemes(OpenIddict…, Umbraco.Cms.Core.Constants.Security.BackOfficeAuthenticationType)`
      (design D1). A comment cites RC1's `BackOfficeAuthPolicyBuilderExtensions` and design D2's
      CSRF stance. Verify: `git grep -c BackOfficeAuthenticationType` gives 3 in that file, and no
      `AuthenticationSchemes.Add(` remains.

      **Done.** All three sites now call `policy.AddAuthenticationSchemes(OpenIddict…,
      Umbraco.Cms.Core.Constants.Security.BackOfficeAuthenticationType)`.

      The class remarks are rewritten. They had said "The OpenIddict validation scheme is not
      optional", which is the 18-era rule and false on 19. They now say:
      - every policy names both schemes, and why each one is needed;
      - what happens without the cookie scheme (a 401, read as an expired session);
      - that the pair is RC1's `BackOfficeAuthPolicyBuilderExtensions.AddAuthenticationSchemes`;
      - design D2's CSRF stance.

      `BackOfficeAuthenticationType` appears 3 times in code (plus once in the remarks), and
      `AuthenticationSchemes.Add(` appears 0 times.

- [x] 3.2 Replace `CanSendRequiredEmail()` with `IsEmailConfigured()` at the 3 call sites (design D3).
      Verify: `git grep CanSendRequiredEmail -- src` finds nothing, or only a comment that explains
      the rename.

      **Done.** The 3 call sites (`BookingEmailHandler:148`, `UBookItNotificationBootCheck:80`,
      `HostMailAvailability:35`) now call `IsEmailConfigured()`.

      The comment at `BookingEmailHandler:132`, "a host whose … throws, which Umbraco's own default
      sender does", was **re-verified before it was renamed**: RC1's
      `NotImplementedEmailSender.IsEmailConfigured()` throws `NotImplementedException`, as 18's
      `CanSendRequiredEmail()` did. So the claim still holds, and only the method name changed.

      `git grep CanSendRequiredEmail -- src` finds nothing.

- [x] 3.3 `UBookItDeliveryApiComposer`: `options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1`, with a
      comment naming Umbraco's own pin (design D4). Verify: the document fetched in 7.1 declares
      `3.1.x`.

      **Done (code).** The first line inside the delivery document's `AddOpenApi` options is now
      `options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1;`, with a comment that:
      - names the .NET 11 default;
      - records the observed 3.1.1 → 3.2.0 move;
      - names Umbraco's own pin (`ConfigureUmbracoOpenApiOptionsBase`,
        `BackOfficeOpenApiDocumentBuilder`);
      - states the rule: follow Umbraco.

      The verification belongs to 4.4 (the guard, including its mutation) and 7.1 (the live
      document).

- [x] 3.4 Build the client first, then `dotnet build UBookIt.slnx`. The shipped projects must have 0
      errors and 0 warnings. Verify: recorded here. Any error other than those named in design
      *Context* is recorded and judged before it is fixed.

      **Done.** I ran `npm ci` (back to the committed lockfile, since the spike's node_modules held
      19.0.0-rc1) and `npm run build`, then `dotnet build UBookIt.slnx` (Debug).
      - Shipped projects: **0 errors, 0 warnings.** Core, Persistence, Backoffice, Web, `UBookIt`
        and the TestSite all produced their assemblies.
      - Test projects: exactly the 12 errors design *Context* predicts (5 `IEmailSender` doubles × 2
        members, and 4 `IRuntimeState` stubs × `CurrentMigrationVersion`), and nothing else.


## 4. Tests

- [x] 4.1 Implement the new interface members on the test doubles (design D3):
      - `IEmailSender` (4 doubles; the design's "5" was a miscount): `IsEmailConfigured` gives the double's existing answer, and
        `IsEmailAvailableAsync` gives `Task.FromResult(IsEmailConfigured())`;
      - `IRuntimeState.CurrentMigrationVersion` (4 stubs): the value the stub's state implies.

      Verify: every test project compiles.

      **Done.**
      - **`IEmailSender`, 4 doubles:** `StubEmailSender`, `ThrowingEmailSender`,
        `RecordingEmailSender` and `TestEmailSender`.
        - Each declared an orphaned `CanSendRequiredEmail()`, which is **renamed** to
          `IsEmailConfigured()` with its body unchanged (stub value, throw-on-probe, `Probed`
          tracking).
        - `IsEmailAvailableAsync` delegates to it (`TestEmailSender` returns `canSend` directly).
      - **`IRuntimeState.CurrentMigrationVersion`, 4 stubs:** `=> null`, matching each stub's
        `CurrentMigrationState => null`.
      - **Found once those compiled (D6 note): 6 `ReadOnlyUserGroup` fixtures.** RC1 removed the
        shorter overloads and added `startDocumentBlueprintId` and `hasAccessToInvariantForVariant`
        to the full constructor, which 18 had not marked obsolete.
        - The fixtures pass `startDocumentBlueprintId: null`, like their other start nodes.
        - They pass `hasAccessToInvariantForVariant: false`, the value that grants nothing extra;
          no uBookIt requirement reads it. Umbraco's upgrade backfills it from the legacy
          `AllowEditInvariantFromNonDefault`, so there is no single "upgraded" value.
        - The edits were made by a script that preserves BOM, line endings and indentation.
        - `UBookItSectionAccessTests`' constructor comment gained a 19 addendum.
      - `dotnet build UBookIt.slnx`: **0 errors, 0 warnings.**

- [x] 4.2 Run unit, integration and rendering, one at a time. For **every** failure, record:
      - the message;
      - its class (framework or host-major literal, version-derived, interface member, or other);
      - the fix (design D6).

      An "other" stops apply and is raised with Chris. Verify: the table is here, and no test was
      deleted or skipped.

      **Done; first run (Debug, after 4.1):** unit 2505/2516, integration 220/222, rendering
      1211/1211. No test deleted or skipped.

      | Tests | Message (abridged) | Class | Fix |
      |---|---|---|---|
      | `PackageCompositionTests.The_front_end_rendering_is_in_the_box` | `lib/net10.0/UBookIt.Web.dll` not found | framework literal | → `net11.0`, in 4.6 |
      | `VersionTruthTests` ×3 (links, images, documented version) | pinned to / states `18.6.0`, declared `19.0.0-rc1` | version-derived | 4.3 |
      | `ChangelogTests` ×3 | "declares 19.0.0-rc1, but CHANGELOG.md has no entry" | version-derived, **needs release content** | **raised**, then 4.3 |
      | `UmbracoFloorDocumentationTests` ×4 | "declares no Umbraco.Cms range with an inclusive lower bound" (the parser reads only `\d+.\d+.\d+`) | **other**: the guard cannot read a prerelease floor | **raised**, then 4.3 |
      | `BookerErasureStorageTests.…one_statement_and_reads_nothing_first` | command starts `SET NOCOUNT OFF;`, expected `UPDATE` | **other**: EF Core 11 SQL text | **raised**, then 4.5 |
      | `MoveStorageTests.…one_update_whose_predicate_is_the_status` | no command starts `UPDATE` (`Assert.Single` on empty) | **other**: same cause | **raised**, then 4.5 |

      **Raised with Chris (D6 stop), and decided the same day:**
      1. Fold the RC's documentation into this change (design D10). Chris also now wants the RC
         published.
      2. The storage guards skip exactly the `SET NOCOUNT OFF;` preamble (design D11).

      Proposal, design and tasks were amended before any of it was implemented.

- [ ] 4.3 The RC's documentation (design D10; amended at apply, Chris, 2026-11-13; it replaces the
      original "version literals only" task):
      - a `CHANGELOG.md` `## 19.0.0-rc1` entry, undated, leading with *What you have to do*, and the
        header's line-major sentence extended to 19;
      - README and Getting started:
        - the floor is `19.0.0-rc1 or later`;
        - the install command is `--version "19.*-*"`, with the reason given;
        - the version sentence, and the links and images pinned to `19.0.0-rc1`;
      - `docs/theming.md` `net11.0`; `docs/publishing.md`'s literals;
      - the SameSite sentence (design D2);
      - `UmbracoFloorDocumentationTests.DeclaredFloor` reads a prerelease bound.

      Verify:
      - `ChangelogTests`, `VersionTruthTests` and `UmbracoFloorDocumentationTests` pass;
      - the floor guard **fails** when one page states a different floor (a reverted mutation);
      - `DocumentationLinkTests` passes.
- [ ] 4.4 Add to `OpenApiTransformerTests` an assertion that the backoffice and delivery documents
      each declare OpenAPI `3.1.x` (design D4). Verify: it passes, and it **fails** with 3.3's line
      removed (a mutation, reverted, with the revert confirmed by content comparison using absolute
      paths).
- [ ] 4.5 The two storage guards skip EF Core 11's leading `SET NOCOUNT OFF;` and nothing else
      (design D11; Chris, 2026-11-13). Verify:
      - `BookerErasureStorageTests` and `MoveStorageTests` pass;
      - **each still fires**: a reverted mutation that issues a read before the write in
        `SqlBookingStore` turns it red;
      - the revert is confirmed by content comparison.
- [ ] 4.6 Clean Release build (`--no-incremental`, after deleting the bundle folder and `obj/Release`):
      0 warnings, 0 errors. Then all three suites. Verify:
      - the totals are recorded and compared with 1.1's: the differences are only 4.3's, 4.4's and
        4.5's test edits and any test 4.2 recorded;
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
