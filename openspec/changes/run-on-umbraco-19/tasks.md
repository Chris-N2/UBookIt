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
        packages are net11.0-only. *(Superseded at apply: D10 folded the RC's documentation into
        this change, and 4.3 made it `net11.0`.)*
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

- [x] 4.3 The RC's documentation (design D10; amended at apply, Chris, 2026-11-13; it replaces the
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

      **Done.** Edits applied by a staged script: every anchor count was checked before any write,
      and BOM and line endings were preserved.

      - **README:**
        - intro "Umbraco 19";
        - install `--version "19.*-*"`, with the reason (`-*` admits the prerelease);
        - floor `19.0.0-rc1 or later` (×2), "below 19.0.0-rc1", "to 19.0.0-rc1 first";
        - "and not 20", and 17/18 directed to their lines;
        - .NET 11.0;
        - version sentence `19.0.0-rc1`;
        - line majors now include `19.x`;
        - 20 `blob/…` links and 4 raw image URLs pinned to `19.0.0-rc1`.
      - **Getting started:**
        - the requirements row (19.0.0-rc1 or later, below 20, 17/18 → their lines), .NET 11.0,
          "below 19.0.0-rc1";
        - the install command `19.*-*`, with the reason;
        - line majors including `19.x`;
        - **the SameSite paragraph** (design D2), in section 1, *Check the requirements*.
      - **`docs/theming.md`:** `net11.0`.
      - **`docs/publishing.md`:** "uBookIt is at `19.0.0-rc1`"; the raw image URL ×2; the
        `git tag` and `git push` examples. Historical mentions (`17.2.1`/`18.1.1` and others) were
        left alone. Its `dev/v18`-only description of the lines is left for the CI-for-three-lines
        change.
      - **`CHANGELOG.md`:**
        - the header's line-major sentence gains 19;
        - a new `## 19.0.0-rc1` entry, undated. It leads with *What you have to do*: the 19 line
          only, `19.*-*`, upgrade Umbraco first, back up the database. Then *Why this release was
          needed*: the cookie sign-in and the SameSite stance, linked to Getting started. Then *Also
          in this release*: OpenAPI 3.1, no behaviour change.
      - **`UmbracoFloorDocumentationTests`:**
        - one `Version` pattern (SemVer prerelease: dot-separated non-empty identifiers) used by
          both the declared floor and the stated phrasings;
        - the install command expects `"{major}.*-*"` when the floor is a prerelease, and
          `"{major}.*"` otherwise;
        - new `A_prerelease_floor_is_read_whole_in_every_phrasing` and
          `A_release_and_its_prerelease_are_different_floors`. **The second caught my first
          pattern** (`[0-9A-Za-z.]*`) reading "below 19.0.0-rc2." with its full stop; it was fixed
          to SemVer's shape, and the comment records it.

      **Verify:** `ChangelogTests`, `VersionTruthTests`, `UmbracoFloorDocumentationTests` and
      `DocumentationLinkTests` give 38/38. **Can fire** (mutating `README.md`, which is read at
      runtime):
      - F1: one floor phrase changed to `19.0.0 or later` → **FAIL**, "README.md states Umbraco
        19.0.0, but Directory.Packages.props declares 19.0.0-rc1 as the floor."
      - F2: install changed to `"19.*"` → **FAIL**, "…does not select uBookIt's 19.x line with
        "19.*-*"…"

      **Instrument failure, recorded:** the restore function was named `R`, which is a built-in
      PowerShell alias (`Invoke-History`), and aliases win over functions. So neither restore ran,
      and the "unmutated" control ran against F2's file and failed. The README's 4.3 edits were not
      yet committed, so `git checkout` was not an option. Each mutation had been written from the
      original text, so the file held exactly F2. I counted the F2 form (1), the intended form (0)
      and the F1 form (0), then reversed F2's one replacement. **Proof of the restore:**
      `verify43.py` re-applied the 4.3 edits to HEAD in memory, and all four doc files compare
      **IDENTICAL** to the worktree (line endings normalised). The docs guards are 38/38 on the
      restored files.

- [x] 4.4 Add to `OpenApiTransformerTests` an assertion that the backoffice and delivery documents
      each declare OpenAPI `3.1.x` (design D4). Verify: it passes, and it **fails** with 3.3's line
      removed (a mutation, reverted, with the revert confirmed by content comparison using absolute
      paths).

      **Done.** `OpenApiTransformerTests.Each_document_declares_openapi_3_1(delivery|backoffice)`:
      - it composes the real composer, as its sibling registration guards do;
      - it reads `OpenApiOptions.OpenApiVersion` off the options the framework consumes;
      - it asserts `OpenApi3_1`.

      Both cases pass. **Can fire:** with 3.3's `options.OpenApiVersion = …OpenApi3_1;` replaced by a
      comment, the delivery case **fails** (`Expected: OpenApi3_1, Actual: OpenApi3_2`, the .NET 11
      default the explore saw), and the backoffice case stays green (Umbraco's pin). The file was
      restored byte-for-byte with the hash equal, and `git status src` is clean.

- [x] 4.5 The two storage guards skip EF Core 11's leading `SET NOCOUNT OFF;` and nothing else
      (design D11; Chris, 2026-11-13). Verify:
      - `BookerErasureStorageTests` and `MoveStorageTests` pass;
      - **each still fires**: a reverted mutation that issues a read before the write in
        `SqlBookingStore` turns it red;
      - the revert is confirmed by content comparison.

      **Done.** One helper, `CommandRecordingInterceptor.Statements`, in
      `tests/UBookIt.Tests.Integration/Support/SqlServerFixture.cs`:
      - it is the recorded commands with exactly a leading `SET NOCOUNT OFF;` removed
        (case-insensitive, at the start only, then trimmed);
      - `Commands` stays raw;
      - its remarks say why, and why matching `UPDATE` anywhere was rejected.

      Both guards now read `Statements`: the erase guard's `Assert.Single` and its `StartsWith`, and
      the move guard's `UPDATE` filter.

      **Can fire.** Mutations of `SqlBookingStore.cs`, made byte-for-byte (BOM preserved), each
      confirmed as a mutation by hash and restored with the hash equal:

      | Run | Erase guard | Move guard |
      |---|---|---|
      | unmutated | pass | pass |
      | M1: `AnyAsync` read before the erase's `UPDATE` | **FAIL**, "Assert.Single() … contained 2 items" | pass (it selects `UPDATE`s by design) |
      | M2: the move's `WHERE` without `permitted.Contains(b.Status)` | pass | **FAIL**, "Assert.Contains() … Sub-string not found" |

      `git status --short src` is empty after the restores.

- [x] 4.6 Clean Release build (`--no-incremental`, after deleting the bundle folder and `obj/Release`):
      0 warnings, 0 errors. Then all three suites. Verify:
      - the totals are recorded and compared with 1.1's: the differences are only 4.3's, 4.4's and
        4.5's test edits and any test 4.2 recorded;
      - `GeneratedClientTests`, `OpenApiTransformerTests` and `NodeEngineGuardTests` pass;
      - the Node guard reads backoffice 19.0.0-rc1's `engines.node`.

      **Done.** First, the last port-forced literal:
      `PackageCompositionTests.The_front_end_rendering_is_in_the_box` now expects
      `lib/net11.0/UBookIt.Web.dll` (framework literal, D6). `AuditScriptTests`' `net10.0`
      occurrences are self-contained fixture data (synthetic projects with their own JSON). They
      pass, and are not port-forced, so they are left.

      I deleted `wwwroot/App_Plugins/UBookItBackoffice` and `obj/Release`, ran `npm run build`, then
      `dotnet build UBookIt.slnx -c Release --no-incremental`: **0 warnings, 0 errors.** Release
      suites, one at a time:

      | Suite | Total | vs 1.1 (2516 / 222 / 1211) |
      |---|---|---|
      | Unit | **2520** | +2 from 4.4's `Each_document_declares_openapi_3_1`, +2 from 4.3's prerelease-floor facts |
      | Integration | **222** | equal |
      | Rendering | **1211** | equal |

      `GeneratedClientTests` + `OpenApiTransformerTests` + `NodeEngineGuardTests`: **42/42** (40 +
      4.4's 2). The Node guard reading 19.0.0-rc1's `engines.node` is verified in 5.1, where the
      lockfile moves.


## 5. Client

- [x] 5.1 `npm install --save-dev @umbraco-cms/backoffice@19.0.0-rc1` (no `--force` and no
      `--legacy-peer-deps`). Verify:
      - no ERESOLVE and no EBADENGINE;
      - `npm ls` shows 19.0.0-rc1 with hey-api 0.99.0 deduped.

      **Done.** `"@umbraco-cms/backoffice": "^19.0.0-rc1"`. "changed 1 package", with no ERESOLVE and
      no EBADENGINE. `npm ls` shows `@umbraco-cms/backoffice@19.0.0-rc1` with `@hey-api/openapi-ts@0.99.0`
      deduped. `npm audit`: 12 (3 moderate, 8 high, 1 critical), unchanged from dev/v18.
      **Deferred from 4.6:** the lockfile's backoffice entry is `19.0.0-rc1`, `engines.node >=24.13`,
      and `NodeEngineGuardTests.Every_pinned…` plus `The_lockfile_reading_finds…` pass against it.

- [x] 5.2 With the RC1 TestSite running, run `npm run generate-client`. Verify:
      - `types.gen.ts` and the 37 method names are unchanged;
      - every difference is listed (the explore expects at most the `baseUrl` trailing slash).

      **Done**, against the RC1 TestSite (`/umbraco/openapi/ubookitbackoffice.json`): no deprecation
      line. Method names 37 / 37, with no difference. Diff stat: 2 files, +2/−2. **Both lines are the
      server URL's trailing slash**, which the explore predicted:
      - `client.gen.ts` `baseUrl: 'https://localhost:44348/'` → `'https://localhost:44348'`;
      - `types.gen.ts` `ClientOptions.baseUrl` literal type, the same change.

      That second one is the generated client's own options type, not an API contract type, so it
      is judged harmless against design D3/D6. At runtime `createClientConfig` takes Umbraco's own
      client configuration, which the live checks exercise in section 6.

- [x] 5.3 `npx tsc --noEmit`, `npm test`, `npm run build`. Verify: green, vitest 391, and no
      section-code edit, or each one listed with its reason.

      **Done.** `npx tsc --noEmit` exit 0; vitest **391/391**; `npm run build` succeeds. **No
      section-code edit**: `git diff --stat` of `Client/src` outside `src/api` is empty.


## 6. Live: identities (design D7, steps 1–3)

- [x] 6.1 API user, with `v19-auth-probe.ps1` extended by one verb-gated write that leaves no
      residue:
      - anonymous gives 401;
      - a token gives 200 on settings and closures;
      - a closure create then delete gives 200 / 200.

      Verify: the output is recorded.

      **Done**, using `scratchpad\v19-auth-probe2.ps1` (the explore's probe plus a write). The TestSite
      was restarted after 5.3's bundle rebuild, with `obj/Debug` deleted. The client id and secret are
      read from `~/.claude.json` and never printed.

      | Request | Status |
      |---|---|
      | uBookIt GET settings, no credentials | **401** (`WWW-Authenticate: Bearer`) |
      | token (client credentials) | 200 |
      | uBookIt GET settings, bearer | **200** |
      | uBookIt GET closures, bearer | **200** |
      | Umbraco GET user/current, bearer (control) | 200 |
      | uBookIt POST closure 2027-03-11 "CLU API probe (delete me)", bearer | **200** |
      | uBookIt DELETE that closure, bearer | **200** |
      | closures named "CLU API probe" remaining | **0** |

- [x] 6.2 Editor, with Chris logged in:
      - the backoffice-client-upgrade D4 set (bookings list and the 67-day refusal; closure create
        and delete; holiday 404 withdraws the panel and 500 keeps it; the invalid closure's
        message);
      - find a booking by email (sensitive data).

      Verify: each step matches what dev/v18 showed in backoffice-client-upgrade 5.4, and **no
      "session timed out" prompt** appears. Network records show uBookIt calls carrying no
      `Authorization` header.

      **Done** on 2026-11-13, with the TestSite restarted on the same output and Chris logged in.
      Before step 1, a page-context recorder wrapped `window.fetch`. For every `/ubookitbackoffice/`
      call it logs the method, status, whether an `Authorization` header is present, and the
      credentials mode. **Every recorded uBookIt call had `auth=false` and `credentials: include`**,
      so the cookie carried them. **No "session timed out" prompt** appeared at any point, and the
      console showed no errors. **Every step matches `dev/v18` (backoffice-client-upgrade 5.4):**
      1. Bookings, default window 2026-11-09 to 2026-11-15: 1 row, "Showing 1–1 of 1". Setting From
         to 2026-09-09 gives GET 400 and the same inline message ("…covers 68 days, which exceeds the
         maximum of 31."), with no toast.
      2. This site has no closures. Created 2027-03-10 "CLU live check (delete me)": rows 0 → 1.
         Deleted it: rows 1 → 0.
      3. Holiday stub (scratchpad `apply\holiday-404-stub.js`, layered over the recorder):
         - **404**: one stubbed GET; the panel is withdrawn, no Import or Fetch control is left, and
           0 toasts.
         - **500 control**: the panel stays, showing "The site's holiday source could not be
           reached…". Stub removed afterwards.
      4. Closure 2027-03-10 to 2027-03-05: POST 400 with "A closure cannot end (2027-03-05) before it
         starts (2027-03-10).", `_errorControl` = `closure-end-date`, and 0 toasts. Cancelled; "No
         closures yet".
      5. Find by email `floor-raise-18@example.com`: POST `bookings/find-by-booker` 200 (`auth=false`).
         "Bookings for floor-raise-18@example.com, all dates" shows DTJ3-7MQH.

      *(Added at QA round 1.)*
      - **Booker shown.** In step 1 and step 5, the row's Booker column showed the name and email
        ("Floor Raise Live Check v18", `floor-raise-18@example.com`), not a withheld placeholder.
        So `BookingsController`'s visibility decision, which goes through `IBackOfficeSecurityAccessor`
        rather than the requirement handlers, also resolved the cookie principal.
      - **How the recorder read headers.** It merged `new Headers(request.headers)` from the
        `Request` object (hey-api calls `fetch(Request)`) with any `init.headers`, so `auth=false`
        means neither carried `Authorization`. It also recorded the `Request`'s `credentials`
        (`include`). The wrapper was page-context code in this session and is not kept as a file.

- [x] 6.3 Refusal through the cookie: Chris removes one uBookIt verb from his group. The matching
      action gives **403**, with the section's refusal message, not 401 and not the sign-in prompt.
      Chris then restores it. Verify: the 403 is recorded, and the permission is confirmed restored.

      **Done** on 2026-11-13. The verb used is `UBookIt.Settings` ("Change site settings"). Each probe
      is a cookie-only fetch from the backoffice tab, with no `Authorization` header.
      - **Baseline** (verb held): GET `settings` **200**.
      - **Chris removed the verb** from his group:
        - GET `settings` gives **403**.
        - POST `closures` (2027-03-10 "CLU refusal probe (delete me)") gives **403**, and nothing is
          created.
        - GET `closures` gives **200**. The read is still satisfied by `UBookIt.Configure`, so the
          refusal follows the verb, not a blanket failure.
        - No 401, and no sign-in prompt. The Settings view shows the section's own refusal ("You do
          not have permission to change uBookIt settings. An administrator can grant it in Users →
          User Groups…").
        - So the cookie principal reaches the requirement handlers, not only authentication.
      - **Chris restored the verb:** GET `settings` **200**, the Settings view shows the settings
        again, and GET `closures` **200** with an empty list.


## 7. Live: delivery and documents (design D7, step 4)

- [x] 7.1 Fetch both OpenAPI documents and diff them, normalised, against dev/v18's saved copies.
      Verify: the only differences are the `openapi` field (now `3.1.x`) and the server URL's
      trailing slash, or each other difference is listed and judged.

      **Done** on 2026-11-13. Both documents were fetched from the v19 TestSite
      (`/umbraco/openapi/ubookitbackoffice.json` and `ubookitdelivery.json`, both 200). There was no
      saved dev/v18 delivery copy (only the backoffice one, from backoffice-client-upgrade 5.2). So
      the dev/v18 TestSite (`55e1a18`, its own secrets id and DB `uBookIt_v18`) was run briefly, its
      two documents fetched, and the site stopped. The fresh v18 backoffice document equals the
      saved copy after normalisation. Diffed after normalisation (parsed, keys sorted):

      | Document | Differences, v18 → v19 |
      |---|---|
      | backoffice | `openapi` 3.1.1 → **3.1.2**; `servers[0].url` `https://localhost:44348/` → `https://localhost:44348` |
      | delivery | the same two lines, and nothing else |

      No other difference, so every path, operation and schema is unchanged. 3.1.2 is still 3.1
      (design D4: pinned to 3.1, the patch level comes from Microsoft.OpenApi). The trailing slash is
      the one 5.2 saw in the generated client.

- [x] 7.2 With the editor's cookie present in the browser:
      - an anonymous delivery read gives 200;
      - a delivery placement with body-only contact details is placed, and its booker reflects the
        body only (*Ambient identity is not trusted for writes*).

      The test booking is cancelled afterwards. Verify: recorded, including the cancellation.

      **Done** on 2026-11-13, from the backoffice tab, so the editor's cookie was present, with
      `credentials: 'include'` set explicitly on every call:
      - **Anonymous reads:** delivery GET `resources?take=50` gives **200** (31 resources). GET
        `resources/{DBO Granted}/bookable-starts` 2026-11-23..28 gives **200**.
      - **Placement:** POST `bookings` for DBO Granted, 2026-11-23 10:00Z, 30 min, with booker
        `{ name: "V19 Delivery Check", email: "v19-delivery-check@example.com", phone: null }`.
        Result: **200**, XM59JG6D, Confirmed. The response's booker is the body exactly.
      - **Stored row** (`uBookIt_v19.uBookItBooking`): `MemberKey` **null**, BookerName and BookerEmail
        as the body, BookerPhone null. So the editor's ambient identity was not taken; *Ambient
        identity is not trusted for writes* holds on 19.
      - **Cancelled** from the backoffice: POST `bookings/{id}/cancel` **200** (`auth=false`, by
        cookie). The row now shows **Cancelled**.


## 8. Close

- [x] 8.1 Fill in `umbraco-feedback.md` from apply (design D9), and record the explore spike stash's
      fate (dropped once nothing in it is unused). Verify: every friction point met during apply has
      an entry.

      **Done.** `umbraco-feedback.md` was reconciled against every friction point in this apply's
      records:
      - **Corrected:** entry 2 said five `IEmailSender` doubles. It is **four**, the count design
        Context was corrected to at apply. Recounted: 4 files implement `IsEmailAvailableAsync`.
      - **Added to entry 3:** after the pin, both documents report `3.1.2` (18 reported `3.1.1`).
        That is `Microsoft.OpenApi`'s patch level, and harmless (7.1).
      - **Added to "What worked well":** the live results of 6.1–7.2 (no header, 403 not 401, the API
        user, the delivery API ignoring ambient identity).
      - **New closing section, "not Umbraco's (do not send)":** EF Core 11's `SET NOCOUNT OFF;`
        (D11) and NuGet's floating-version rule for prereleases (D10). Each friction point now has a
        home.
      - Entry 5 (`ReadOnlyUserGroup`) was already added at 1.2.

      **Spike stash dropped.** Each of its 18 files was compared with HEAD: 15 are identical, and the
      other 3 (`Directory.Packages.props`, `UBookItAuthorizationComposer.cs`, `TestSite.csproj`)
      differ only by HEAD's range floors, comments and indentation. Nothing in it is unused.
      `git stash drop` printed `c8445e9c1a6baa937dbd8f47f77e7443102e6506`, which is recoverable with
      `git stash apply <sha>` until gc.

- [x] 8.2 `openspec validate --all --strict` with the pinned CLI, after the last commit. Verify:
      passes.

      **Done.** `npx --yes @fission-ai/openspec@1.13.0 validate --all --strict` gives **28 passed,
      0 failed**, exit 0, after `a24676b` (the 8.1 commit). It was re-run after this record's own
      commit, and the commit is stated in the message of the commit that follows it.

- [ ] 8.3 QA (`qa-review`): a fresh subagent, reused across rounds, told what is claimed and asked to
      verify it. The live checks are judged by method unless QA asks for a repeat. Verify:
      APPROVE, or APPROVE WITH NITS, with the nits resolved or recorded.

      **Round 1 (2026-11-13): REJECT, on one MAJOR; no CRITICAL.** The reviewer independently
      re-verified:
      - client 391, build 0/0;
      - Unit 2520, Integration 222, Rendering 1211, and the 42-test client guard set;
      - the OpenAPI pin mutation;
      - openspec (28 passed);
      - authorization completeness: 6 controllers, 7 policies, both schemes named at all 3 sites;
      - the D2 claims against RC1's source;
      - `skip_specs`;
      - the D11 helper, the floor guard, leftover literals, and the DevExpress scan (pass).

      It accepted 6.1–7.2 on method.

      - **MAJOR, fixed: the cookie scheme had no regression guard.** With the scheme removed from all
        three `AddAuthenticationSchemes` calls, the unit suite passed 2520/2520. The fix:
        - New `AuthenticationSchemeGuardTests`:
          - The theory asserts that each policy's scheme set is **exactly** {OpenIddict,
            `BackOfficeAuthenticationType`}. Its data is the union of every policy constant
            (`Constants` + `Constants.VerbPolicies` by reflection) and every policy named by an
            `[Authorize]` on a backoffice controller or action.
          - A floor fact pins the policy count at 7, with constants ⊆ named and named ⊆ constants,
            so an empty enumeration cannot pass vacuously.
        - The three older guards (`UBookItSectionAccessTests`, `PermissionsTests` verb theory,
          `EraseBookerEndpointTests`) now assert the cookie scheme as well as OpenIddict.
        - **Proven to fire** (scratchpad `mut_cookie.py`; each mutation reverted with
          `git checkout`, and `src` is clean after):

          | Mutation | Fails |
          |---|---|
          | cookie removed at the section site | the guard's `UBookItSectionAccess` case and `The_registered_policy_carries_the_backoffice_authentication_scheme` |
          | cookie removed at the sensitive-data site | the guard's `UBookItSensitiveDataAccess` case and `The_sensitive_data_policy_carries…` |
          | cookie removed in `AddVerbPolicy` | the guard's 5 verb cases and the `PermissionsTests` theory's 3 |
          | a third scheme (`"Extra"`) added in `AddVerbPolicy` | the guard's 5 verb cases |

      - **MINOR, fixed: stale owed items** written before D10.
        - D2's "`release-19-0-0` owes a docs sentence" is amended: the sentence is in getting-started
          and the CHANGELOG.
        - 2.1's theming item is marked superseded by 4.3.
        - 8.4's obligation list now names what is actually owed: the 19.0.0 bump, CI for three lines,
          `release-19-0-0-rc1`, and the verify-install defect.
      - **NIT, fixed:** `proposal.md` said "six" policies; it is seven.
      - **NIT, fixed:** the 6.2 record now states that the booker was shown, and how the recorder read
        headers (from the `Request` object).
      - **NIT, fixed:** in the README Umbraco row, "below 19.0.0-rc1 … raises the Umbraco packages"
        is now "A site on Umbraco 18 or earlier cannot install it at all, because the package targets
        .NET 11. On an earlier 19 prerelease, installing uBookIt raises only…". The floor guard's
        "to 19.0.0-rc1 first" phrasing is kept.

      After the fixes: clean Release `--no-incremental` build, **0 warnings, 0 errors**; Unit
      **2528/2528** (2520 + 8 new). Integration and Rendering are untouched by this round: no
      source change, only tests, README and records.

- [ ] 8.4 Merge `run-on-umbraco-19` into `dev/v19` locally (no remote; design *Migration Plan*).
      Record the owed follow-ups in the deferred-obligations record:
      - the 19.0.0-final bump;
      - CI for three lines, before the first push;
      - `release-19-0-0-rc1`: date the CHANGELOG entry, check the artifacts, and tag (Chris pushes
        the tag);
      - `scripts/verify-install.ps1` (2.1): it uses `-r LTS` and `bin/Debug/net10.0`, so it is
        wrong on dev/v18 and dev/v19 alike, and needs a per-line template choice.

      *(Amended at QA round 1: this list named "`release-19-0-0`'s line wording and the SameSite
      docs sentence", both done here in 4.3 once D10 folded the RC's documentation in, and it
      omitted the verify-install defect 2.1 records as owed.)*

      Then archive. Verify: `dev/v19` contains the change, and the obligations are written.
