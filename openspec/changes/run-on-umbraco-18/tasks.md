## 1. Establish the baseline before changing anything

- [x] 1.1 Capture the **current** generated TypeScript client's exported method names from
      `main` (or from `dev/v18` before the port), so D3's question has something to compare
      against. Verify the list is non-empty — a comparison against nothing proves nothing.
- [~] 1.2 **NOT DONE as written, and the substitute is weaker in one specific way — recorded
      rather than quietly ticked.** Capturing the v17 documents needed a running v17 TestSite,
      which by the time the port was under way meant switching branch, rebuilding the v17 client
      and booting against `main`'s database. What stood in for it: §1.1's **method-name**
      baseline from the v17-generated client (which is downstream of the v17 operation IDs, so it
      pins them transitively), and the v18 measurements taken directly — 30 operations, 11
      delivery paths, 0 paths with the API off. **What is genuinely unverified is the v17
      *path count*,** so "11 delivery paths" is an absolute reading rather than a comparison.
      The disabled-endpoint guarantee does not depend on it: 0 is 0 on either version.
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
- [x] 3.3 Verify the delivery document still carries **no security requirement** on its
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
- [ ] 5.3 **`CLAUDE.md`'s invariant 1 is the same obligation and was not noticed until §6.4's
      confinement check.** The LTS/STS amendment is a statement about the project, not about the
      v18 line, and it lives only on `dev/v18`. Cherry-pick it to `main` with the README table.

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
- [ ] 9.3 Once set, confirm `main` still boots against its own database. The whole point of the
      split is that it does, and that is worth proving rather than assuming.

      **Partially evidenced, and the gap is named.** The two `UserSecretsId`s are confirmed
      different (`db95506d-…` on `main`, `a220bfe8-…` on `dev/v18`) and **both stores exist and
      are populated**, so the configuration isolation the split was built for is real and
      `main`'s connection string was not overwritten. Umbraco 18 then migrated only the database
      the v18 store points at. **What is still unproven is the thing the task actually asks:
      that `main` BOOTS.** That needs a v17 client build and a v17 TestSite run, and "the
      configuration looks right" is not the same claim — which is exactly the substitution this
      change has already been caught making once. Owed before the branch is trusted.

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
