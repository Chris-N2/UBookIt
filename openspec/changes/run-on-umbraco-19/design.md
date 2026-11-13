## Context

See proposal.md, *Why*. Everything below was measured in the explore spike of 2026-11-13. The spike
was the `uBookIt-v19` worktree at dev/v18 `55e1a18`, retargeted to RC1. It is kept as
`stash@{0}` ("v19 RC1 spike 2026-11-13"), and the apply may consult it, not just pop it:
- **Restore and build:** after retargeting, the shipped projects had exactly 3 compile errors, all
  `CanSendRequiredEmail`. The test projects had 12 more: 4 `IEmailSender` doubles (× 2 members) and
  4 `IRuntimeState` stubs (`CurrentMigrationVersion`). *(Corrected at apply: this said 5 doubles.
  Once those errors were fixed, the compiler's next pass also showed 6 `ReadOnlyUserGroup`
  constructor errors; see D6's note.)*
- **Run:** Umbraco's unattended upgrade migrated a copy of v18's database to 19 on first boot.
- **Auth:** the API-user path (client credentials) worked unchanged. The editor path failed (401
  from uBookIt beside 200 from Umbraco, in one page load). The scheme fix below then made both pass,
  and anonymous requests still got 401.
- **OpenAPI:** both uBookIt documents were content-identical to v18's (normalised diff). The only
  differences were the server URL's trailing slash and the `openapi` field: backoffice 3.1.2,
  delivery **3.2.0**.
- **Client:** `@umbraco-cms/backoffice` 19.0.0-rc1 installs alongside hey-api 0.99 with no conflict;
  `tsc` and vitest are clean.

Sources read: the RC1 tag's source in `ref/Umbraco-CMS-19-rc1`; RC1's nuspecs (EF Core 11.0.0,
`Microsoft.AspNetCore.OpenApi` 11.0.0, OpenIddict 7.7.2); and Umbraco's version-specific upgrade
page.

## Goals / Non-Goals

**Goals:**
- Build and run on Umbraco 19 RC1 with **no behaviour change**. Every guarantee in `openspec/specs`
  holds as it does on 18, for an editor with a cookie and for an API user with a token.
- Keep `dev/v19`'s difference from `dev/v18` inside the surface the host forces. The cheap
  cherry-picks between lines depend on it.

**Non-Goals:**
- Using anything 19 newly offers (for example the `IsEmailAvailableAsync` reachability check). A
  port takes what it must, not what it could.

## Decisions

### D1. Name both schemes, exactly as Umbraco does

Every policy uses
`policy.AddAuthenticationSchemes(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme,
Umbraco.Cms.Core.Constants.Security.BackOfficeAuthenticationType)`. That is the body of RC1's own
`BackOfficeAuthPolicyBuilderExtensions.AddAuthenticationSchemes`. The type name is fully qualified
because uBookIt has its own `Constants`.

*Rejected: the cookie scheme alone.* It drops API users, which the explore showed still work only
through OpenIddict.

*Rejected: relying on a default scheme.* The policies name their schemes today, and each one should
say which identities it admits.

The three `AddPolicy` sites stay three. Folding them into a helper is a tidy, not a port.

### D2. The CSRF stance is Umbraco's, and it is stated, not assumed

A cookie-authenticated API is exposed to cross-site request forgery. Umbraco 19's own startup
warning says the Management API "has no antiforgery tokens of its own", and that `SameSite` is what
stops a cross-site request carrying the cookie. The cookie is configured once, for every path
(`Path=/`, `HttpOnly`, `SameSite=Strict` by default). So uBookIt's `/umbraco/ubookitbackoffice/api`
routes get exactly the protection `/umbraco/management/api` gets, no more and no less.

Consequences:
- uBookIt adds no anti-forgery mechanism of its own. One would protect only part of the backoffice
  and make the overall stance harder to state.
- The docs say so: a site that sets `AuthCookieSameSite=None` exposes uBookIt as much as Umbraco.
  *(Amended at apply: this was owed by `release-19-0-0` until D10 folded the RC's documentation in.
  It is now in `docs/getting-started.md` section 1 and the CHANGELOG entry, task 4.3.)*
- The delivery API is untouched by this. Its policies are not these, and its "ambient identity is
  not trusted" guarantee is checked live (D7).

### D3. `CanSendRequiredEmail()` becomes `IsEmailConfigured()`, and nothing more

RC1's `EmailSender.IsEmailConfigured()` has the same body as 18's `CanSendRequiredEmail()`: SMTP
host, or pickup directory, or a notification handler registered. So the rename preserves behaviour.

*Rejected: `IsEmailAvailableAsync`.* It may do network I/O, and all three call sites are
configuration decisions (a boot check, the email handler's guard, and whether the booking form
promises an email).

The test doubles implement `IsEmailConfigured` with their current answer. They implement
`IsEmailAvailableAsync` as `Task.FromResult(IsEmailConfigured())`, so no double claims a
reachability uBookIt never asks about.

### D4. The delivery document is pinned to OpenAPI 3.1, following Umbraco's own pin

Our delivery document uses the framework's `AddOpenApi`, so on .NET 11 it took the new 3.2 default.
Umbraco 19 pins its own documents to `OpenApiSpecVersion.OpenApi3_1`
(`ConfigureUmbracoOpenApiOptionsBase`, `BackOfficeOpenApiDocumentBuilder`).

The delivery document is the contract headless consumers generate clients from, so it should not
move version because a framework default moved. Chris's rule: "stick at 3.1 unless Umbraco itself
moves". The registration therefore sets `options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1`,
with a comment naming Umbraco's pin as the reason.

A guard in `OpenApiTransformerTests`, which already builds both documents, asserts that each
declares `3.1.x`. It must fail on the unpinned registration; that is checked at apply.

### D5. The version is `19.0.0-rc1`, and only guard-named literals follow it

Chris's choice. `PackageCompositionTests.The_bound_admits_this_major_and_excludes_the_next` and the
`packaging` requirement *A package description names the Umbraco major its line targets* both
derive the Umbraco major from the declared version. At `18.6.0` the first fails against the 19
range, and the descriptions would say Umbraco 18 on a 19 build.

Unlike the 18 port, which kept a spent version so it could not be published by accident, this line
carries an unspent prerelease version. The protection now comes from elsewhere: `dev/v19` is not
pushed, and publishing needs a release tag that only Chris pushes.

~~Any version guard that fails after the bump is satisfied by changing the literal it names; line
wording is left to `release-19-0-0`.~~ *Superseded at apply by D10.* The bump made the guards ask
for more than literals: a CHANGELOG entry, and floor statements for a prerelease floor. Chris chose
to fold the RC's documentation into this change.

### D10. The RC's documentation is written here; publication stays in the release change (amended at apply, Chris, 2026-11-13)

The 4.2 run showed that `19.0.0-rc1` cannot go green without release paperwork:
- `ChangelogTests` requires an entry for the declared version;
- `UmbracoFloorDocumentationTests` requires the README and Getting started to state the declared
  floor, and its parser could not read a prerelease bound;
- `VersionTruthTests` requires the README's links, images and version sentence to name the version.

Chris also now wants the RC published for real-world testing. So this change writes all of the
RC's documentation:
- **`CHANGELOG.md`:** a `## 19.0.0-rc1` entry, undated. Released entries are dated at publication;
  `Every_released_version_is_dated` reads only archived releases, so an undated entry is right
  until `release-19-0-0-rc1`. It leads with *What you have to do*: Umbraco 19.0.0-rc1 or later,
  .NET 11, the backoffice sign-in change and what uBookIt did about it, and that this is a
  prerelease. The header's "`17.x` … `18.x`" sentence gains 19.
- **README and Getting started:**
  - the floor reads `19.0.0-rc1 or later`;
  - **the install command must select a prerelease**: NuGet's `"19.*"` excludes prereleases, so it
    becomes `--version "19.*-*"`, and the text says why;
  - the version sentence, and the links and images pinned to the `19.0.0-rc1` ref.
- **`docs/theming.md`:** `net11.0`. **`docs/publishing.md`:** its version literals.
- **The SameSite sentence** (D2), on the page that covers backoffice configuration.
- **`UmbracoFloorDocumentationTests.DeclaredFloor`** reads `[19.0.0-rc1,`. A floor phrase such as
  "19.0.0-rc1 or later" must still be matched, and the guard must still fire when a page states a
  different floor, which is checked by mutation.

`release-19-0-0-rc1` keeps the rest: dating the entry, checking the artifacts, the tag, and
publication. It follows the CI-for-three-lines change.

### D11. Two storage guards skip EF Core 11's `SET NOCOUNT OFF;` preamble, and nothing else (Chris, 2026-11-13)

EF Core 11's SQL Server provider sends `SET NOCOUNT OFF;` at the start of an `ExecuteUpdate`
command. The erase guard still records exactly one command, so its *one statement, no read first*
guarantee holds. But it asserts the command text starts with `UPDATE`. The move guard selects
commands that start with `UPDATE`, and finds none.

Both guards strip exactly that leading text (case-insensitive, then trimmed) before matching, and
nothing more, with a comment saying why.
- *Rejected: matching `UPDATE` anywhere.* A read placed before the update would then pass.
- *Rejected: dropping the text assertion.* The single-command assertion alone cannot tell
  an `UPDATE` from a `SELECT`.

Each guard is shown still able to fire: a reverted mutation that puts a read before the write must
fail it.

### D6. Guards that encode a framework or host major move, but each one is listed and must still fire

Some tests contain `net10.0` or a Umbraco 18 bound as data: `AuditScriptTests`' fixtures,
`PackageCompositionTests`' `lib/net10.0` entry, the bound's examples. A port legitimately changes
those values. That is different from adjusting a guard to agree with whatever the build now does,
which this project treats as a defect.

*Note added at apply:* host type **signature** changes in test fixtures belong to the same class
as interface members. `ReadOnlyUserGroup`'s constructor gained `startDocumentBlueprintId` and
`hasAccessToInvariantForVariant`, and its shorter overloads were removed.

So every test edit in this change is recorded in tasks.md with:
- the failing message;
- whether it is a host- or framework-major literal, or something else;
- for a guard, proof that it can still fire.

Anything that is not a major literal or an interface member stops apply and is raised with Chris.

### D7. Verification is local and mirrors CI, plus a live check of both identities

There is no CI for `dev/v19` yet (proposal, Non-goals). The apply runs locally what CI runs:
- every suite;
- a Release `--no-incremental` build with 0 warnings;
- `openspec validate --all --strict` with the pinned CLI;
- the Node-engine guard.

Integration tests run on LocalDB, as they always do locally.

Live, on the RC1 TestSite:
1. **API user** (scratchpad `v19-auth-probe.ps1`): anonymous gives 401; a client-credentials token
   gives 200 on a read and a verb-gated write.
2. **Editor** (Chris logged in):
   - the backoffice-client-upgrade D4 set: bookings list, closure create and delete, the holiday
     404/500 control, and an invalid closure's message;
   - a sensitive-data endpoint (find a booking by email);
   - **no "session timed out" prompt** anywhere.
3. **Refusal:** an editor whose group lacks a uBookIt verb still gets 403, not 401. This proves the
   cookie principal reaches the requirement handlers, not only the authentication step.
4. **Delivery**, with the editor's cookie present in the same browser:
   - an anonymous read gives 200;
   - a placement records the booker from the body only (*Ambient identity is not trusted for
     writes*);
   - both OpenAPI documents declare 3.1.x.

### D8. The TestSite's database is the explore's copy

`uBookIt_v19` exists already: v18's database copied with `BACKUP … COPY_ONLY` and `RESTORE`, then
upgraded to 19 on first boot. The spike's secrets id (`80f44b82-…`) points at it.

The change commits that `UserSecretsId`, with the csproj comment updated to name all three lines,
and leaves the secrets file and database as they are. Re-copying from v18 would only repeat the
upgrade.

### D9. `umbraco-feedback.md` is part of the change

Chris wants to tell Umbraco how smooth the upgrade was. The notes live in this change's folder, so
they are archived with it. They are seeded from the explore and added to during apply: each
friction point with what happened, how it was found, and what would have helped. They are notes for
Chris to edit, not a report sent anywhere.

## Risks / Trade-offs

- **[Cookie identity differs subtly from the token identity in the requirement handlers]** The
  explore exercised only the section requirement plus a read. → D7 steps 2–3: a write, a
  sensitive-data endpoint, and a 403 for a missing verb, all as an editor.
- **[A guard is "fixed" into agreement]** The port changes several test literals. → D6: each edit
  recorded and classified, and anything not a major literal or interface member stops apply.
- **[No CI on the line]** Local runs can differ from the Linux/SQL-container CI. → Run everything CI
  runs, locally. The CI change for three lines runs the full set on Linux before `dev/v19` is
  first pushed.
- **[RC churn]** 19.0.0 final may change APIs again. → The final bump is a separate small change.
  It rebuilds and reruns this change's checks, and any further break is recorded in the feedback
  notes.
- **[The v18 database]** → Already defused: the v19 TestSite has its own id and database (D8). No
  apply step starts a TestSite from the v18 worktree.

## Migration Plan

Nothing is published and nothing is pushed.
- **Rollback:** delete the `run-on-umbraco-19` branch (and the `uBookIt_v19` database, if wanted).
- **Merge:** after QA, `dev/v19` takes the branch by a local merge, since there is no remote.

## Open Questions

None that change the approach. Whether 19.0.0 final changes anything this change touches is the
follow-up's question.
