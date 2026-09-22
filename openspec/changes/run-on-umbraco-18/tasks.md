## 1. Establish the baseline before changing anything

- [x] 1.1 Capture the **current** generated TypeScript client's exported method names from
      `main` (or from `dev/v18` before the port), so D3's question has something to compare
      against. Verify the list is non-empty — a comparison against nothing proves nothing.
- [ ] 1.2 Record the **current** delivery and backoffice OpenAPI documents from a running v17
      TestSite: path count, the operation IDs, and which paths appear when the delivery API is
      off. The last is `delivery-api`'s guarantee and the thing most likely to break silently.
- [x] 1.3 Confirm the spike's measurement still holds at HEAD of `dev/v18`: restore succeeds,
      and `dotnet build` produces exactly the four errors in the two named files. If the number
      has changed, the design is built on a stale measurement and stops here.

## 2. Port the backoffice composer

- [x] 2.1 Replace the `Configure<SwaggerGenOptions>` block, `UBookItBackofficeOperationSecurityFilter`
      and the `IOperationIdHandler` registration with `builder.AddBackOfficeOpenApiDocument(...)`
      per design D1. Verify by building `UBookIt.Backoffice` alone — its two of the four errors
      should be gone.
- [x] 2.2 Keep `Constants.ApiName` as the document name. Verify the document is served at the
      same route a v17 site serves it from — a renamed document silently breaks the client
      generation config and anything a site has bookmarked.
- [x] 2.3 **Decide `CustomOperationHandler`'s fate by evidence** (D3): regenerate the client and
      diff the exported method names against §1.1. Keep the handler only if the names got worse,
      and record which happened either way — "the docs said it was unnecessary" is not a finding.

## 3. Port the delivery composer

- [x] 3.1 Replace its `Configure<SwaggerGenOptions>` block with the non-backoffice registration,
      resolving D2's open question by reading the 18.2.0 assembly rather than guessing between
      `AddUmbracoOpenApiDocument`, `AddOpenApi` and `AddOpenApiDocumentToUi`. Record which and why.
- [x] 3.2 **Carry the anonymity comment across.** The absence of `.WithBackOfficeAuthentication()`
      is a deliberate guarantee about a public API, not a line nobody typed — verify the comment
      survives the port and says so.
- [ ] 3.3 Verify the delivery document still carries **no security requirement** on its
      operations, by inspecting the generated document rather than the source.

**§1–3, §5 results — and the spike's measurement was an undercount, necessarily.**

It reported four errors in two files. True, and incomplete: **test projects depend on `src`, so
while `src` was broken the compiler never reached them.** Once the composers built, three more
appeared — Umbraco 18 added `IUmbracoContext.Elements`, an `IUserGroupService` performing-user
overload and an `IEmailSender` scheduling overload, all implemented by our test doubles. Seven
errors in five files, not four in two. **A build-error count is only a count of the errors the
compiler got far enough to find.**

**The backoffice composer went from ~40 lines to 5.** The delivery one uses the framework's own
`AddOpenApi` with a `ShouldInclude` keyed on the `[MapToApi]` attribute the controllers already
carried — one source of truth rather than a namespace check duplicated in the composer.
`AddUmbracoOpenApiDocument` exists in the assembly but was not reachable as `IUmbracoBuilder` or
`IServiceCollection`; after four attempts I stopped guessing and used the documented route.

**2.3 — `CustomOperationHandler` deleted, on evidence.** The 30 exported client method names are
unchanged (`listBookings`, `cancelBooking`, …), so Umbraco 18's built-in operation-ID
conventions produce what the custom handler used to. Had they regressed it would have been
ported to `GenerateOperationId`.

**A new build property is required:** `Microsoft.AspNetCore.OpenApi`'s XML-comment source
generator emits interceptors, which are opt-in per namespace, so `UBookIt.Web` now sets
`InterceptorsNamespaces`. Without it the generated file fails to compile with CS9137.

**An obsolescence is suppressed, not fixed, and it is owed before Umbraco 19.** Umbraco 18
deprecates the `ReadOnlyUserGroup` constructor six test fixtures use. Its replacement takes one
extra nullable int among the start-node ids, and **Umbraco's shipped XML documents only 13 of
that constructor's 14 parameters** — so the new parameter cannot be named. Suppressed with the
reasoning written once in `UBookItSectionAccessTests` and referenced from the other five.

**Finding those six sites took three instruments and two failures**, which is the reusable part:
`grep "new ReadOnlyUserGroup"` missed a fully-qualified call; `Sort-Object -Unique` on the
warning text collapsed six warnings into one line; and a target-typed `=> new(` is invisible to
any search for the type name. **The compiler was the only instrument that could enumerate the
population** — the same fault in three costumes.

**Build: 0 warnings, 0 errors. Suites on Umbraco 18: 1809 / 167 / 1168, no test deleted or
weakened.**

## 4. The guarantees the port must not break

- [ ] 4.1 Run the `delivery-api` guards. Specifically the OpenAPI scenarios: a disabled endpoint
      does not appear in the document, and the delivery document is separate from the backoffice
      one. These are the acceptance criteria for the whole change.
- [ ] 4.2 Verify the disabled-endpoint behaviour **against a generated document on a site with
      the delivery API off**, not only against the unit guard — the mechanism may have been
      coupled to Swashbuckle's pipeline, and that coupling would not show in a test that never
      generates a document.
- [ ] 4.3 Re-generate the TypeScript client and run the client suite. Verify the generation
      itself succeeded rather than reusing a stale `client.gen.ts`.

## 5. The versioning table (design D6)

- [x] 5.1 Make `README.md`'s versioning table version-agnostic so it is true on both lines.
      Verify `VersionTruthTests` still passes — the table sits in a guarded, packed document.
- [x] 5.2 **Record the cherry-pick obligation prominently**: this edit is true of `main` too, and
      left here alone it makes two published READMEs disagree about the package's own policy.
      Branch flow is `main` → `dev/v18`, so it will also conflict at the next merge forward.

## 6. Verification

- [ ] 6.1 Build the client first, then each test project sequentially in Release with the
      TestSite stopped. Record counts against the `main` baseline of 1809 / 167 / 1168 / 290 —
      **any drop is a guarantee that stopped being checked**, not a test that became irrelevant.
- [ ] 6.2 Clean Release build, **0 warnings**.
- [ ] 6.3 `openspec validate --all --strict`.
- [ ] 6.4 Confirm the port stayed inside the coupled surface: `git diff main...dev/v18 --stat`
      should touch the two composers, `Directory.Packages.props`, the README table and the
      generated client — and nothing else. Anything further is drift between the lines.

## 7. Live check on Umbraco 18

The compiler cannot see any of this, and it is the whole point of the branch.

- [ ] 7.1 Run the TestSite against Umbraco 18. Verify the backoffice section loads, the Bookings
      screen lists, and a booking can be placed, moved and cancelled.
- [ ] 7.2 Open both Swagger/OpenAPI documents in the browser and confirm they render, are
      separate, and that the backoffice one requires authentication while the delivery one does
      not.
- [ ] 7.3 Complete a booking through the **front-end flow with JavaScript disabled** — the
      package's headline guarantee, and nothing about the OpenAPI port should touch it, which is
      exactly why it is worth confirming rather than assuming.
- [ ] 7.4 **Look at the backoffice and compare it to the shipped screenshots** (design D5).
      Record whether Chris's assessment held. If it did not, the release change retakes them.

## 8. Record

- [ ] 8.1 Note what the port cost against what the spike predicted — the value of the spike is
      only established by comparing it to the outcome.
- [ ] 8.2 Record whether `OperationIdHandler` survived, and the evidence either way.
- [ ] 8.3 Note anything Umbraco 18 made *easier*, not only what it broke. The registration is
      already shorter than what it replaces; if the port finds more of that, the next major's
      port is cheaper for knowing it.

## 9. The v18 TestSite needs its own database — BLOCKED ON CHRIS

`§4` and `§7` both need a running v18 TestSite, and running one is not free.

**The trap:** user secrets are keyed by `UserSecretsId`, **not by branch**, so both branches were
pointing at one connection string. Starting the v18 TestSite against `main`'s database would let
Umbraco 18 migrate it — after which `main`'s v17 TestSite can no longer boot against it, and the
dev environment for the LTS line is gone without a restore.

- [x] 9.1 Give `dev/v18` its own `UserSecretsId` so the two branches can hold different
      connection strings. New id: `a220bfe8-cd6c-45e8-bd3f-e94f3a2dbd45`, with the reason in the
      csproj so nobody "tidies" it back.
- [ ] 9.2 **Chris:** create the v18 database and set the secret. Credentials are his; I do not
      handle them. **Restoring a copy of the v17 database under a new name is better than an
      empty one** — Umbraco 18 will migrate the copy, `main`'s database is untouched, and the
      live check keeps the resources, services and bookings the dev site already has rather than
      needing them rebuilt.
- [ ] 9.3 Once set, confirm `main` still boots against its own database. The whole point of the
      split is that it does, and that is worth proving rather than assuming.
