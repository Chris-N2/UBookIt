## 1. Establish the baseline before changing anything

- [x] 1.1 Capture the **current** generated TypeScript client's exported method names from
      `main` (or from `dev/v18` before the port), so D3's question has something to compare
      against. Verify the list is non-empty — a comparison against nothing proves nothing.
- [x] 1.2 Record the **current** delivery and backoffice OpenAPI documents from a running v17
      TestSite: path count, the operation IDs, and which paths appear when the delivery API is
      off. **Done late — after the port, on `main` at `eced809`, once Chris offered the v17 site
      — and doing it late is why it could be a comparison instead of a baseline.**

- [x] 1.3 Confirm the spike's measurement still holds at HEAD of `dev/v18`: restore succeeds,
      and `dotnet build` produces exactly the four errors in the two named files. If the number
      has changed, the design is built on a stale measurement and stops here.

**§1.2 results — the v17 baseline, captured on `main` after the port, and it settles more than
it was asked to.**

Both documents read off a running v17 TestSite at `eced809`, then diffed against the v18
documents captured in §4. **The two lines describe the same API:**

| | v17 backoffice | v18 backoffice | v17 delivery | v18 delivery |
|---|---|---|---|---|
| paths | 20 | **20** | 11 | **11** |
| operations | 30 | **30** | 11 | **11** |
| operations with an `operationId` | 30 | **30** | 0 | **0** |
| operation-id set | — | **identical** | — | — |
| security scheme | `Backoffice-User` | `Backoffice-User` | `Backoffice-User` | **none** |
| operations carrying `security` | 30 | 30 | 0 | 0 |
| `integer`/`string` unions | **0** | 0 *(after the fix)* | **0** | 0 *(after the fix)* |
| delivery paths with the API **off** | — | — | **0**, endpoint 404 | **0**, endpoint 404 |

**CORRECTED AFTER QA ROUND 1: "the two lines describe the same API" was FALSE, and it is the
THIRD claim in this change to fail the same way.** The table above is built entirely from
*name-and-count* instruments — path counts, operation counts, the operation-id set, exported
method names. Every one of them is structurally blind to a change in schema **shape**, so the
generated TypeScript could differ materially while all four agreed. It does, in three ways:

| change | what it is |
|---|---|
| eleven request bodies `body?:` → `body:` | **a correction.** A `[FromBody]` parameter *is* required; Swashbuckle described it as optional and .NET's generator does not. Callers already pass one, which is why the TypeScript still compiles |
| `SettingResponseModel.effectiveValue` / `.configuredValue` optional → required-and-nullable | **more precise.** "always present, may be null" rather than "may be absent, may be null" |
| `ProblemDetails` lost its additional-properties index signature | **inert, checked rather than assumed.** Every error reader casts through `unknown` with its own local shape — `api-errors.ts` deliberately so, because it normalises three different failure shapes — so nothing read extension members through this type |

**None of these is a regression and all three needed deciding rather than discovering.** The
first two are now pinned by `OpenApiTransformerTests.The_generated_client_requires_a_body_on_every_write`,
so a future regeneration that loosens them fails instead of passing quietly.

**The lesson is [[a-guard-scoped-wrongly-reports-green]] for the third time in one change**, and
it is sharper here than the memory states it: *counting things and comparing names is not
comparing contracts.* Two documents can have identical paths, identical operations, identical
operation ids and identical method names, and still promise different things.

**The row that matters most is the unions one, and it is the row nobody asked for.** v17 emitted
**zero** — so `{"type": ["integer","string"]}` was a v18 regression introduced by the generator
swap, and the JSON-options fix and schema transformer **restore v17's behaviour** rather than
imposing a new opinion. Before this capture that was a well-reasoned inference from Umbraco's own
documents; now it is measured against the thing the port is supposed to preserve. §6.4's earlier
caveat — that "11 delivery paths" was an absolute reading rather than a comparison — is
discharged: it is 11 on both.

**Three differences, and all three are the host or an improvement:**

1. **OpenAPI 3.0.4 → 3.1.1.** The host's choice. Its visible consequence is the nullable union
   order that §4 adjusted a guard for.
2. **The v18 backoffice document adds document-root `security`** on top of the per-operation
   requirements v17 already had. Belt and braces — a stronger statement, not a weaker one.
3. **The v17 delivery document declared a `Backoffice-User` security scheme in `components` that
   no operation ever referenced. The v18 one declares no scheme at all.** This is a genuine
   improvement to the `delivery-api` guarantee: the anonymous document no longer carries a
   backoffice scheme as vestigial furniture. Worth noticing that nobody would have found this by
   reading source — it only exists in the generated artifact.


## 2. Port the backoffice composer

- [x] 2.1 Replace the `Configure<SwaggerGenOptions>` block, `UBookItBackofficeOperationSecurityFilter`
      and the `IOperationIdHandler` registration with `builder.AddBackOfficeOpenApiDocument(...)`
      per design D1. Verify by building `UBookIt.Backoffice` alone — its two of the four errors
      should be gone.
- [x] 2.1a **A user-visible string changed, and a change declaring "no behaviour change" has to
      say so.** The backoffice document's title was `"UBook It Backoffice Backoffice API"` — a
      typo carried since the composer was first written — and the port set it to
      `"uBookIt Backoffice API"`. It is a fix and it is visible in the Swagger UI, so it is
      recorded rather than left to be noticed. **Silence is not a decision**, and this was the
      second unrecorded user-visible difference this change produced.

- [x] 2.2 Keep `Constants.ApiName` as the document name. **Restated, because the task as
      written asked for something that is false and it was ticked anyway.** The *document name*
      is unchanged (`ubookitbackoffice`), which is the part this change controls and the part a
      rename would break. **The route moved and could not be kept**: Umbraco 17 served
      `/umbraco/swagger/{name}/swagger.json`, Umbraco 18 serves `/umbraco/openapi/{name}.json`.
      That is the host's, not ours; `Client/package.json`'s `generate-client` URL follows it.
      Anything a site had bookmarked does break, and saying so is the point of the task.
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
- [x] 3.3 Verify the delivery document still carries **no security requirement** on its
      operations, by inspecting the generated document rather than the source.

**§1–3, §5 results — and the spike's measurement was an undercount, necessarily.**

It reported four errors in two files. True, and incomplete: **test projects depend on `src`, so
while `src` was broken the compiler never reached them.** Once the composers built, three more
appeared — Umbraco 18 added `IUmbracoContext.Elements`, an `IUserGroupService` performing-user
overload and an `IEmailSender` scheduling overload, all implemented by our test doubles. Seven
errors in five files, not four in two. **A build-error count is only a count of the errors the
compiler got far enough to find.**

**The backoffice composer's `Compose` went from ~40 lines to 8.** The delivery one uses the framework's own
`AddOpenApi` with a `ShouldInclude` keyed on the `[MapToApi]` attribute the controllers already
carried — one source of truth rather than a namespace check duplicated in the composer.
`AddUmbracoOpenApiDocument` exists in the assembly but is **not usable by a package, and now the
reason is known rather than guessed at**: it is generic — `AddUmbracoOpenApiDocument<TConfigure>`
— which is why four non-generic call attempts failed to resolve, and its type argument must derive
from `ConfigureUmbracoOpenApiOptionsBase`, which is **internal to Umbraco**. Four guesses found
nothing; reading the shipped XML documentation and then the assembly metadata found both facts in
two calls. **Read the metadata on the first failure, not the fifth.**

**2.3 — `CustomOperationHandler` is PORTED, not deleted — and the first version of this very
paragraph claimed the opposite, on no evidence.** It recorded "deleted, on evidence: the 30
exported method names are unchanged" *before* the client had ever been regenerated against a v18
document. When it was, the names had regressed exactly as D3 feared: `cancelBooking` →
`postBookingsByIdCancel`, `listBookings` → `getBookings`, `placeBookingOnBehalf` → `postBookings`.
Umbraco 18's built-in conventions are route-derived, not action-derived.

So the handler is ported as `ActionNameOperationIdTransformer`, an `IOpenApiOperationTransformer`
reading the same source of truth as the v17 `IOperationIdHandler` — the action's route value.
**The registration seam is `ConfigureOpenApiOptions`, not the document builder:**
`AddOperationTransformer` is an extension on `OpenApiOptions`, and a first attempt calling it
directly on `BackOfficeOpenApiDocumentBuilder` did not compile (CS1061). The builder's members
were read out of the shipped XML documentation rather than guessed at a fifth time.

**Evidence, gathered after the port:** the live v18 document at
`/umbraco/openapi/ubookitbackoffice.json` carries 30 operations with action-shaped ids
(`CancelBooking`, `ListBookings`, `PlaceBookingOnBehalf`, …), and the regenerated client's 30
exported method names are byte-identical to the v17 baseline captured in §1.1.

**One more difference, checked and inert:** `client.gen.ts`'s `baseUrl` gained a trailing
slash on regeneration. `hey-api.ts` spreads `umbHttpClient.getConfig()` *after* `...config`, so
the generated value is overwritten before any request is made. Noted here so the next reader
does not re-investigate it.

**Two instruments failed silently while checking this**, which is the reusable part: `grep -oP`
is unavailable in this locale and printed a warning to stderr while returning **zero names**, and
an earlier pattern's `(?=\()` lookahead never matched because the generated methods are generic
(`cancelBooking<ThrowOnError…>`). Both produced an empty list, and an empty list diffs against a
30-line baseline as "everything removed" — a result that reads as a catastrophic finding rather
than as a broken instrument. **A comparison tool must assert its own extraction is non-empty
before its output means anything** — the same rule §1.1 already stated about the baseline, owed
equally by the thing being compared.

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

- [x] 4.1 Run the `delivery-api` guards. Specifically the OpenAPI scenarios: a disabled endpoint
      does not appear in the document, and the delivery document is separate from the backoffice
      one. These are the acceptance criteria for the whole change.
- [x] 4.2 Verify the disabled-endpoint behaviour **against a generated document on a site with
      the delivery API off**, not only against the unit guard — the mechanism may have been
      coupled to Swashbuckle's pipeline, and that coupling would not show in a test that never
      generates a document.
- [x] 4.3 Re-generate the TypeScript client and run the client suite. Verify the generation
      itself succeeded rather than reusing a stale `client.gen.ts`.

**§4, §6 results — verified on a running Umbraco 18 site, not inferred.**

**4.1 / 4.2 — `delivery-api`'s OpenAPI guarantees hold, and the second one was checked the way
the task demanded rather than the cheap way.** With both delivery directions switched off by
environment override (so the dev config is not left mutated), the generated delivery document
carries **0 paths** and `GET /umbraco/ubookit/api/v1/resources` returns **404** — absent, not
refused. This is the evidence the design asked for: the mechanism is
`DeliveryApiExposureConvention` clearing `ApiExplorer.IsVisible`, so no `ApiDescription` is ever
produced, and it was never Swashbuckle's to lose. With the API on, the delivery document is
separate from the backoffice one (11 paths vs 30 operations) and **carries no `security` on any
operation and none at the document root** (3.3).

**An integer was not being described as one — found by the TypeScript compiler, not by reading
the document.** With the client regenerated, `npm run build` failed with fourteen errors all of
one shape: `Type 'string | number' is not assignable to type 'number'`. The cause is upstream of
us. `Microsoft.AspNetCore.OpenApi` generates schemas from the **global HTTP `JsonOptions`** —
Microsoft documents these as the only JSON options that influence OpenAPI, MVC's having *none* —
and Umbraco configures those globally with `JsonNumberHandling.AllowReadingFromString`. So every
`int` was emitted as `{"type": ["integer", "string"]}` with a numeric-string pattern: a true
statement about a minimal API reading those options, and a **false** one about our controllers,
which are MVC and do not.

Umbraco's own documents do not have this, which is what proved it was ours. The two documents are
fixed differently because only one of them has a supported seam:

- **Backoffice** — `WithJsonOptions(Constants.JsonOptionsNames.BackOffice)` on the document
  builder. This is precisely what the parameter is for: *"match the serialization conventions of
  the API endpoints the document describes."*
- **Delivery** — that seam is Umbraco-internal for a non-backoffice document and `OpenApiOptions`
  exposes no serializer of its own, so a **schema transformer** narrows the union the serializer
  settings widened: the `String` member is removed, `null` is preserved, and a schema that was
  never widened is untouched.

Confirmed on the live documents afterwards: backoffice **0 unions / 21 plain integers**, delivery
**0 unions / 16 plain integers** with nullable ints still `["null", "integer"]`.

**One guard adjusted, and the distinction matters: adjusted, not weakened.** `GeneratedClientTests`
asserted `service?: BookedServiceModel | null`. Umbraco 18 emits OpenAPI 3.1, whose nullable shape
is `{"type": ["null", "object"]}`, so the generator writes `null | BookedServiceModel` — the same
type, the other order. The assertion now accepts either order and still requires all three things
it always required: optional, nullable, and **one object**. Union order is the generator's
business; the contract is ours.

**6.1–6.3 — 1809 / 167 / 1168 / 290, every suite at the `main` baseline, nothing deleted or
weakened. 0 warnings in a clean Release build. `openspec validate --all --strict` 23/23.**

**6.4 — the port did NOT stay inside the five files the task predicted, and the task was wrong
rather than the port.** The prediction was written from the spike, and §3's note already records
why the spike undercounted: test projects never compiled while `src` was broken. The actual
divergence from `main`, excluding this change's own OpenSpec artifacts, is 21 files — the five
predicted, plus the nine test files whose doubles or fixtures Umbraco 18 touched
(`ReadOnlyUserGroup` suppressions, `IUmbracoContext.Elements`, the `IEmailSender` and
`IUserGroupService` overloads), `UBookIt.Web.csproj`'s `InterceptorsNamespaces`, the TestSite's
own `UserSecretsId`, and `CLAUDE.md`.

**`CLAUDE.md` is drift and is a SECOND cherry-pick obligation, alongside the README table.** The
invariant-1 amendment describing the LTS/STS branching policy is a statement about the project,
true of both lines, and it currently exists only on `dev/v18`. Left there it does what D6 warns
about for the README: the two branches disagree about the project's own policy, in the file that
tells every future context what the rules are.

## 5. The versioning table (design D6)

- [x] 5.1 Make `README.md`'s versioning table version-agnostic so it is true on both lines.
      Verify `VersionTruthTests` still passes — the table sits in a guarded, packed document.
- [x] 5.2 **Record the cherry-pick obligation prominently**: this edit is true of `main` too, and
      left here alone it makes two published READMEs disagree about the package's own policy.
      Branch flow is `main` → `dev/v18`, so it will also conflict at the next merge forward.
- [!] 5.3 **`CLAUDE.md`'s invariant 1 is the same obligation, and it cannot be closed from this
      branch.** The LTS/STS amendment is a statement about the project, not about the v18 line,
      and it currently lives only on `dev/v18`. So does the README versioning table (5.1).

      **Both are cherry-picks onto `main`, which is a commit on another branch that Chris
      pushes** — so this change can record the obligation and cannot discharge it. Marked `[!]`
      rather than left as an unticked `[ ]`, because an open checkbox reads as work forgotten
      and this is work *located*: two files, both already written, waiting on a branch this
      change does not own. QA round 1 was right to object to the bare box.

      Until they land, the two branches disagree about the package's own versioning policy in a
      file packed into every NuGet package, and about the project's governing invariant in the
      file that tells every future context what the rules are.

## 6. Verification

- [x] 6.1 Build the client first, then each test project sequentially in Release with the
      TestSite stopped. Record counts against the `main` baseline of 1809 / 167 / 1168 / 290 —
      **any drop is a guarantee that stopped being checked**, not a test that became irrelevant.
- [x] 6.2 Clean Release build, **0 warnings**.
- [x] 6.3 `openspec validate --all --strict`.
- [x] 6.4 Confirm the port stayed inside the coupled surface: `git diff main...dev/v18 --stat`
      should touch the two composers, `Directory.Packages.props`, the README table and the
      generated client — and nothing else. Anything further is drift between the lines.

## 7. Live check on Umbraco 18

The compiler cannot see any of this, and it is the whole point of the branch.

- [x] 7.1 Run the TestSite against Umbraco 18. Verify the backoffice section loads, the Bookings
      screen lists, and a booking can be placed, moved and cancelled.
- [x] 7.2 Open both Swagger/OpenAPI documents in the browser and confirm they render, are
      separate, and that the backoffice one requires authentication while the delivery one does
      not.
- [x] 7.3 Complete a booking through the **front-end flow with JavaScript disabled** — the
      package's headline guarantee, and nothing about the OpenAPI port should touch it, which is
      exactly why it is worth confirming rather than assuming.
- [x] 7.4 **Look at the backoffice and compare it to the shipped screenshots** (design D5).
      Record whether Chris's assessment held. If it did not, the release change retakes them.

**§7 results so far — and two of the four are BLOCKED ON CHROME, not done.**

**7.2 — verified, and more strongly than "they render".** The two documents are separate and
say opposite things about authentication, which is the guarantee rather than the rendering:

| | backoffice | delivery |
|---|---|---|
| security scheme | `Backoffice-User` | **none declared** |
| document-root `security` | `[{Backoffice-User: []}]` | **absent** |
| operations carrying `security` | 30 of 30 | **0** |
| unauthenticated request to an endpoint | **401** | **200** |

The last row is the one worth having: the document's claim and the server's behaviour were
checked against each other rather than either being taken on its own.

**7.3 — a booking was placed with no JavaScript, and curl is the strictest possible client
for that claim** — it cannot run a script even by accident. Walked the real flow: GET the page,
choose a date and duration, read the rendered start times, POST the details form with its
antiforgery token and `ufprt`. Response: **"Booking confirmed", reference `8WZR-Q9P8`** (dev-site
residue, 09:15 on 28 September 2026). The OpenAPI port touches nothing here, which is exactly
why confirming it beats assuming it.

**Chris has already reported a difference that bears on 7.4, and it narrows D5 rather than
overturning it.** His note: the v18 backoffice has *"a border radius set on the buttons, so
they're quite rounded at the edges rather than the square edges of v17"*. That splits the four
packed screenshots in two:

- `booking-flow.png` and `booking-form.png` show the **front end**, which is our own markup on
  the site's own styling. Unaffected by anything in the backoffice chrome.
- `bookings-screen.png` and `availability.png` show the **backoffice**, and both are full of
  `uui-button`s. On an 18 site those render rounded while the shipped images show them square.

So D5's "near-identical" holds, and a reader of the `18.x` README would still meet a small
visible mismatch in two of four images. That is a decision for the release change rather than
this one — **retake the two backoffice shots for the `18.x` line, leaving `17.x`'s own alone** is
the obvious answer, but it is Chris's call and it costs a v18 backoffice session to do.

**7.1 — done in a v18 backoffice, and it is the check that matters most**, because the
backoffice is the client of every operation ID this change nearly broke. All four workspace views
render and all four call the regenerated client successfully:

| view | what it proves |
|---|---|
| Resources | `listResources`, and the resource editor's opening-hours panel |
| Services | loads |
| Bookings | `listBookings`, `findBookingByReference`, `listBookableSubjects` |
| Settings | `getSettings`, including the read-only configuration-sourced rows |

The full booking lifecycle was driven through the UI on booking `8WZR-Q9P8` — the one placed by
§7.3's no-JavaScript walk-through, so the two halves of the system were exercised against the
same record:

1. **Found** by reference from the Bookings screen.
2. **Moved** to 14:00. The first attempt, to 11:15, was *refused* — "Something else is booked at
   that time. Choose another." — which is worth as much as the success: a domain failure
   travelled the `errors[]` envelope through the new OpenAPI stack and rendered in the dialog.
3. **Cancelled**, status going to Cancelled and its actions disappearing.
4. **Placed on behalf** through *New booking* → reference `K3RT-FR4D`, 28 Sep 2026 14:00. This is
   `placeBookingOnBehalf`, one of the three methods the missing transformer had renamed.

**7.4 — Chris's assessment held for three of the four shipped screenshots, and the fourth needs
retaking for the `18.x` line only.** Compared against the images in `docs/images/`:

| screenshot | on Umbraco 18 |
|---|---|
| `booking-flow.png` | **unaffected** — front end, our markup on the site's styling |
| `booking-form.png` | **unaffected**, same reason |
| `availability.png` | **indistinguishable.** The panel is fieldsets, time inputs and *links* — it contains no `uui-button`, so the change does not reach it |
| `bookings-screen.png` | **visibly different.** *New booking* and every *Move*/*Cancel* render fully pill-shaped on 18 against v17's small corner radius |

So the difference is real, cosmetic, and confined to **one image** rather than the four a blanket
retake would have cost. Layout, columns, typography, colour and the section chrome are otherwise
identical. **Recommendation for the release change: retake `bookings-screen.png` on an 18 site
for the `18.x` packed README, and leave `17.x`'s own image alone** — a packed README is frozen
per version, so the two lines can hold different images without either becoming wrong. Chris's
call.

**This vindicates D5's shape rather than its content.** The decision was to look before
publishing instead of re-capturing on spec; looking found one image to retake instead of four.

## 8. Record

- [x] 8.1 Note what the port cost against what the spike predicted — the value of the spike is
      only established by comparing it to the outcome.
- [x] 8.2 Record whether `OperationIdHandler` survived, and the evidence either way.
- [x] 8.3 Note anything Umbraco 18 made *easier*, not only what it broke. The registration is
      already shorter than what it replaces; if the port finds more of that, the next major's
      port is cheaper for knowing it.

## 9. The v18 TestSite needs its own database — UNBLOCKED

`§4` and `§7` both need a running v18 TestSite, and running one is not free.

**The trap:** user secrets are keyed by `UserSecretsId`, **not by branch**, so both branches were
pointing at one connection string. Starting the v18 TestSite against `main`'s database would let
Umbraco 18 migrate it — after which `main`'s v17 TestSite can no longer boot against it, and the
dev environment for the LTS line is gone without a restore.

- [x] 9.1 Give `dev/v18` its own `UserSecretsId` so the two branches can hold different
      connection strings. New id: `a220bfe8-cd6c-45e8-bd3f-e94f3a2dbd45`, with the reason in the
      csproj so nobody "tidies" it back.
- [x] 9.2 **Chris:** create the v18 database and set the secret. Credentials are his; I do not
      handle them. **Restoring a copy of the v17 database under a new name is better than an
      empty one** — Umbraco 18 will migrate the copy, `main`'s database is untouched, and the
      live check keeps the resources, services and bookings the dev site already has rather than
      needing them rebuilt.
- [x] 9.3 Once set, confirm `main` still boots against its own database. The whole point of the
      split is that it does, and that is worth proving rather than assuming.

      **Proved, not inferred.** `main` at `eced809` was checked out, its v17 client rebuilt, and
      the TestSite started: `/umbraco` **200**, site root **200**, no `SqlException` in the log.
      Umbraco 18 migrated only the database the `dev/v18` secret store points at, and the LTS
      line's dev environment is intact. The earlier "both secret stores exist and are populated"
      evidence was the right *shape* of argument and still not this claim — which is the whole
      reason it was not allowed to stand in.

**§8 — what the spike predicted against what the port cost.**

The spike predicted **four errors in two files** and that is what it could see. The port touched
**21 files** and cost three defects the spike could not have found, each invisible to the thing
that found the previous one:

| found by | defect |
|---|---|
| the compiler | four composer errors — the spike's whole prediction |
| the compiler, *after* `src` built | three more in test doubles; a build-error count is a count of the errors the compiler reached |
| **regenerating the client** | operation IDs regressed to route-derived names. No compiler sees this; the docs said it would not happen |
| **the TypeScript compiler** | every `int` described as `integer|string`. No C# compiler sees this either |

**The reusable shape: a port's remaining risk lives in generated artifacts, and each generator
must be run to find it.** Three of the four were found by running something and looking at what
came out — never by reading source. The one thing the spike measured directly is the one thing
it got right.

**8.2 — `OperationIdHandler` SURVIVED, ported to `ActionNameOperationIdTransformer`.** Evidence
in §2.3, including the fact that this change first recorded the opposite on no evidence at all.

**8.3 — what Umbraco 18 made easier, which is most of it.**

- **The backoffice composer went from ~40 lines to 8.** Four separate Swashbuckle concerns —
  document registration, a security operation filter, schema conventions, an operation-ID
  handler — collapse into one chained call plus the one transformer the host does not do for us.
- **`WithJsonOptions` exists at all**, and its documentation states the exact rule the delivery
  document had to work around by hand: a document should be generated with the serialization
  conventions of the endpoints it describes. Umbraco saw this problem and gave the backoffice a
  seam for it.
- **`[MapToApi]` became the single source of document membership.** On 17 the delivery composer
  could have used a namespace check; on 18 `ShouldInclude` reads the attribute the controllers
  already carry.
- **And the thing to fix before the next port:** the non-backoffice equivalent,
  `AddUmbracoOpenApiDocument<T>`, is unusable by packages because its options base class is
  internal. That is worth raising upstream — it is the difference between a delivery-style
  document being three lines and being a hand-written schema transformer.

## 10. QA round 1 — findings and what they cost

**REJECT, five MAJOR, no CRITICAL.** The reviewer re-ran the build, all four suite counts,
`--strict`, the DevExpress hard-fail gate and the branch-containment check, and proved the
adjusted `GeneratedClientTests` guard still fires by mutating `types.gen.ts` two ways. It also
hit §2.3's empty-extraction trap independently, on its first attempt, which is the strongest
evidence yet that the trap is real rather than a one-off slip.

**What was wrong, and the shape it shared.** Three of the five MAJORs were *claims*, not code —
and all three failed the same way, which is why the count matters more than any one of them:

1. **"The two lines describe the same API"** — asserted from instruments that count names and
   cannot see schema shape. Corrected in §1.2 above; eleven request bodies and two settings
   fields had changed optionality and `ProblemDetails` had lost an index signature.
2. **"Umbraco's XML documents only 13 of 14 parameters, so the new one cannot be named"** — the
   XML half was true and the conclusion false. The parameter is `startElementId`; it is in the
   assembly metadata that makes every *other* argument nameable, and its source is checked into
   this repository at `ref/Umbraco-CMS-main/…/ReadOnlyUserGroup.cs:114`. **All six `CS0618`
   suppressions are gone and the self-invented "OWED BEFORE UMBRACO 19" debt with them** — 0
   warnings is now achieved by fixing rather than hiding.
3. **"The other three call sites point here"** — there were five. Moot now the suppressions are
   deleted, but it is the same fault in miniature: a population described without being counted.

**The other two were missing guards over exactly the behaviour this change had already broken
once**, which is the more serious category:

4. **The schema transformer had no test**, and its comment claimed a property — "only ever
   narrows a union the serializer widened" — that the code could not implement, because
   "has String and has Integer" equally describes an authored union, whose `Pattern` it would
   also have discarded. **Fixed by making the code true rather than the comment weaker:** the
   serializer's widening always carries the pattern `^-?(?:0|[1-9]\d*)$` (measured: 60
   occurrences across both documents, one distinct pattern), so that pattern is now the
   signature it matches on. Extracted to `SerializerWidenedNumberSchemaTransformer` so it can
   be tested without generating a document.
5. **Nothing failed if `ActionNameOperationIdTransformer` were deleted** — the defect this
   change shipped, corrected, and left undetectable. Now guarded against the committed
   `sdk.gen.ts`.

**Eight tests added in `OpenApiTransformerTests`, and every one demonstrated by mutation rather
than by passing** — because a guard that has only ever been seen green is a guard nobody has
tested:

| mutation | result |
|---|---|
| drop the pattern check (over-narrow an authored union) | **1 failed** |
| assign `Integer` instead of clearing the `String` flag (loses `null`) | **2 failed** |
| leave the pattern behind on a narrowed integer | **1 failed** |
| `listBookings` → `getBookings` in `sdk.gen.ts` (*the real regression*) | **1 failed** |
| a request body back to `body?:` | **1 failed** |
| **break the extraction itself** so the name list comes back empty | **1 failed** |

The last row is the one worth keeping. An empty extraction satisfies every "is absent" assertion
and reads as a healthy client — the precise failure that made this change report "names
unchanged" from a list of nothing, twice. **§2.3 wrote that lesson down and did not bank it; the
non-empty assertion is now the guard rather than the paragraph.**

**Also fixed:** an unclosed `</remarks>` that would become CS1570 the moment any project sets
`GenerateDocumentationFile`; §2.2 restated, because it asked for a route that *could not* be kept
and was ticked anyway (the document *name* is unchanged, the host's route moved); the line-count
claim reconciled to 8 in all three places it appeared; and `Microsoft.AspNetCore.OpenApi` given
explicit `PackageReference`s in both projects that compile against it, rather than relying on it
arriving transitively through Umbraco.

**Counts after the fixes: 1817 / 167 / 1168 / 290** — up eight, all new, none altered — **0
warnings in a clean Release build, `openspec validate --all --strict` 23/23.**

## 11. QA round 2 — the fix relocated the gap, twice

**REJECT again, two MAJORs, and both were doc blocks.** Worth stating plainly: round 1's central
finding was three false claims, round 1's fixes were written up in two new paragraphs, and
**both paragraphs were false.** The fault did not recur despite the fix; it recurred *in* the
fix, which is what CLAUDE.md means by treating each round's fixes as new code.

**1. The schema transformer was still unguarded at the seam.** The reviewer deleted
`options.AddSchemaTransformer<...>()` from the composer, rebuilt in Release and ran everything:
**0 warnings, every test green.** Six unit tests asked the transformer what it does to a schema
and not one asked whether it runs. MAJOR 4 was not closed; it was **relocated** - from "no test"
to "no test of the only thing that can silently disappear".

Worse, `OpenApiTransformerTests`' own remarks asserted a deliberate two-kinds design - *"a unit
test cannot see a transformer that was never registered; the artifact guard can"* - which is
true of the operation-ID transformer and **false of this one**, because nothing is generated
from the delivery document. A comment asserting a property the code does not have, inside the
doc block written to announce the fix for a comment asserting a property the code did not have.

Fixed with `The_schema_transformer_is_registered_on_the_delivery_document`: it composes the real
composer, resolves `IOptionsMonitor<OpenApiOptions>.Get("ubookitdelivery")` and reads the
internal `SchemaTransformers` list - the shape `DeliveryApiExposureTests` already uses for the
exposure convention. Reflection into an internal field is deliberate; the alternative is no
guard at all over a line whose removal is otherwise undetectable.

**2. The transformer's stated failure direction named a detector that does not exist.** It said
a changed host pattern would "break the TypeScript build loudly". **Nothing generates TypeScript
from the delivery document** - the only `generate-client` target is the backoffice document, and
that one is fixed by `WithJsonOptions` rather than by this transformer. The fourteen TypeScript
errors that started the whole investigation came from the backoffice client. So the failure it
called loud is silent.

That paragraph now also states what it does **not** cover: the pattern is `System.Text.Json`'s
*integral* one, so a `decimal`, `double` or `float` on a delivery model would be widened with a
different pattern and published as a string/number union unnoticed. Verified latent rather than
live - no delivery model carries a non-integral number today.

**Mutation evidence for both fixes**, because a guard seen only green is a guard nobody tested:

| mutation | result |
|---|---|
| **delete the registration line** - the reviewer's own mutation | **1 failed** |
| rename the internal `SchemaTransformers` field the guard reflects on | **1 failed** |

The second matters as much as the first: a reflection guard that silently found nothing would
pass vacuously forever, so the instrument asserts itself before it asserts anything else.

**Minors, all taken:**

- **The `body` guard enumerated a sample, not the population** - seven of eleven, and blind to a
  twelfth write endpoint nobody has added yet. Replaced with the class assertion: every `body`
  member in `types.gen.ts` is required except the generated `body?: never`, so **no other
  optional body may exist**. That is [[a-finding-enumerates-a-sample]], caught inside a guard
  written one round earlier to fix a different instance of the same thing.
- **Thirteen files had gained a UTF-8 BOM** across rounds 1 and 2 - including
  `Directory.Packages.props` and both csprojs, all shared with `main`, none Umbraco-18-forced.
  That was my tooling, not a decision. The repo's convention is no BOM (333 `.cs` files without,
  35 with), so all thirteen are stripped and the shared files are byte-identical to `main` again
  apart from their real edits.
- **`Microsoft.AspNetCore.OpenApi` added to the proposal's Impact table.** It is now a declared
  dependency of two packed packages and changes both nuspecs.
- **The two cherry-picks are recorded in [[ubookit-deferred-obligations]]**, not only in §5. The
  reviewer accepted `[!]` as accurate marking and then named the real risk underneath it: a task
  box inside an archived change is read by nothing.
- **The backoffice document's title change recorded** (2.1a) and **the `client.gen.ts` trailing
  slash** noted as checked-and-inert, so neither is rediscovered.

**Counts after round 2: 1818 / 167 / 1168 / 290** - one more than round 1, the registration
guard - **0 warnings in a clean Release build, `openspec validate --all --strict` 23/23.**

## 12. QA round 3 — the guard was right and its paragraph argued the sibling away

**REJECT, one MAJOR, and it is the same shape a third time.** Round 2 closed the schema
transformer's registration gap. The doc block written to explain that fix then reasoned that the
*other* transformer was already covered: *"asking the committed client covers the operation-ID
transformer, because that one's effect is baked into a committed artifact."* True of the effect.
**False of the registration**, which is the thing that disappears.

The reviewer deleted `.ConfigureOpenApiOptions(... AddOperationTransformer<...>())` from the
backoffice composer, rebuilt in Release and ran everything: **0 warnings, 1818 and 167 green.**
The committed client catches that regression only after somebody regenerates, and a person
deleting a registration has no reason to. **That is exactly how this change shipped its original
defect** — and it was still possible after two rounds of fixing it.

**The pattern across three rounds, stated once because it is the actual finding:** each round
fixed the case it was given and wrote a paragraph explaining the fix, and each paragraph made a
confident claim about a case nobody had checked. Round 1: "the two lines describe the same API".
Round 2: "a unit test cannot see a transformer that was never registered; the artifact guard
can". Round 3: the same sentence, applied to the sibling. **The code was never the recurring
defect. The prose about the code was** — and prose is not checked by anything, which is why the
fix each time has been to turn the claim into a guard.

**Closed rather than documented as a limitation.** The reviewer flagged an honest caveat: it had
not established that `AddBackOfficeOpenApiDocument` can be composed under
`ServicesOnlyUmbracoBuilder` the way `AddOpenApi` can, and said that if it could not, the right
outcome was to *state* the gap rather than weaken the guard. **It can** — probed directly, the
backoffice document composes in isolation and registers five operation transformers, ours among
them. So there is no caveat to write.

Both registrations are now asserted through one helper, `RegisteredTransformers`, and the
instrument is asserted before either of them by a `[Theory]` over both internal field names —
because a renamed field makes each list come back empty, and an empty list reads as "no
unexpected transformer" rather than as a broken test.

**Mutation evidence:**

| mutation | result |
|---|---|
| delete the **operation-ID** registration — round 3's mutation | **1 failed** |
| delete the **schema** registration — round 2's mutation | **1 failed** |
| rename the internal field the helper reads | **2 failed** (the guard *and* its instrument theory) |

The reviewer also ran a vacuity attack worth recording: it swapped type registration for the
**lambda overload**, leaving the list non-empty with our type absent. The guard fails — it
distinguishes "some transformer" from "our transformer".

**Also taken:**

- **`proposal.md:83` still said "~40 lines become ~5".** Round 2 claimed the line count was
  "reconciled in all three places". There were **four**, and the one missed is in the artifact
  that survives archive and that a future reader meets first. Fourth instance of
  [[a-finding-enumerates-a-sample]] in this change; all four occurrences now agree on 8.
- **The reflection paragraph overstated its own case** — "the alternative is no guard at all"
  when a source scan was offered and exists. Rewritten as the real trade: a scan asserts the
  call is *written*, this asserts it *took effect*, and a scan would pass happily if the call
  moved into a branch that never runs. The reviewer's verdict on the trade was to keep the
  reflection, for that reason and because the blast radius is a test rather than a package.
- **The `body` guard's `\S+` could let `body?: X | undefined;` escape both patterns** — matching
  neither the required floor nor the loosened check. `[^;]+` closes it. hey-api does not emit
  that shape today.

**Counts after round 3: 1821 / 167 / 1168 / 290** — three more than round 2 (the second
registration guard and the two instrument theory cases) — **0 warnings in a clean Release build,
`openspec validate --all --strict` 23/23.**
