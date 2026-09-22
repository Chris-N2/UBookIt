## Why

uBookIt does not build against Umbraco 18. Chris's own site runs Umbraco 18, is still a "coming
soon" holding page, and is about to be the shop window for freelance work — so the package
running on it is the exhibit, and every feature built on `main` reaches that site only once a v18
line exists.

**The blocker turned out to be far smaller than assumed.** Measured, not estimated, by the spike
at `708056f`: **four compile errors in two files**, and nothing else in the repository references
Swashbuckle at all.

The cause is a dependency swap rather than API churn. Umbraco 17 depends on
`Swashbuckle.AspNetCore` — the OpenAPI *generator*. Umbraco 18 depends on
`Microsoft.AspNetCore.OpenApi` and keeps only `Swashbuckle.AspNetCore.SwaggerUI` — the *viewer*.
So `SwaggerGenOptions`, `BackOfficeSecurityRequirementsOperationFilterBase` and
`IOperationIdHandler` no longer exist, and Umbraco 18 ships a first-class replacement that is
shorter than the code it replaces.

## What Changes

**Dependencies** — `Umbraco.Cms.*` 17.6.2 → 18.2.0, and `Microsoft.EntityFrameworkCore.SqlServer`
and `.Design` 10.0.10 → 10.0.11, because Umbraco 18.2.0 requires the latter and restore fails
`NU1605` without it. Both already applied by the spike.

**Two composers, ported to Umbraco 18's OpenAPI registration**

- `UBookIt.Backoffice/Composers/UBookItBackofficeApiComposer.cs` — the `SwaggerGenOptions`
  block, the security operation filter and the custom `OperationIdHandler` collapse into
  `builder.AddBackOfficeOpenApiDocument(name, document => document.WithTitle(…)
  .WithBackOfficeAuthentication())`.
- `UBookIt.Web/Composing/UBookItDeliveryApiComposer.cs` — the same swap, **without**
  authentication: the delivery API is anonymous by design and deliberately carries no security
  requirement on its operations.

**The README versioning table**, which hardcodes `17.x.y → 17.x.z` and reads as false on an
`18.x` line. It becomes version-agnostic.

**Verification that the generated TypeScript client still has usable method names**, because the
custom `OperationIdHandler` existed solely to keep them short and Umbraco's docs say the new
registration "applies the schema and operation ID conventions" itself.

## Non-goals

- **Not the release.** Publishing `18.0.0` is a separate change, on this project's rule that a
  change is a unit of work and a version is a unit of publication. `Directory.Build.props` stays
  at `17.1.1` here — which also makes it **impossible to publish this branch by accident**, since
  that version is already spent.
- **No new features, and no behaviour change.** Every guarantee in the specs must hold on 18
  exactly as it holds on 17. This is a port.
- **Not the block library.** An element type that could be placed in Umbraco 18's block library
  would be the first genuinely v18-only capability, and it is a feature for its own change.
- **No screenshot retake.** Chris's assessment is that the v18 backoffice is near-identical to
  v17; the change adds a look-before-publish step instead of re-capturing on spec.
- **Not `main`.** This lands on `dev/v18` only — except the versioning-table edit, which is true
  of both lines and is called out below.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

None — `skip_specs: true`.

**Verified rather than assumed, because a port is exactly where a silent spec falsification would
hide.** No spec under `openspec/specs/` names an Umbraco version at all — the requirements were
written against behaviour, not against a host release, so none of them becomes false on 18.

The closest thing to a risk is `delivery-api`, which does constrain the OpenAPI document: a
disabled endpoint SHALL NOT appear in it, and the delivery document SHALL be separate from the
backoffice one. **Those requirements are about the document's *content*, not its generator**, so
they need no edit — but they are the acceptance criteria for this port rather than bystanders,
and the change is not done until the guards over them pass unchanged.

## Impact

| | |
|---|---|
| `Directory.Packages.props` | Umbraco 18.2.0, EF Core 10.0.11 *(already in the spike)* |
| `Microsoft.AspNetCore.OpenApi` | **A new declared dependency of two packed packages.** `UBookIt.Web` calls `AddOpenApi`/`AddSchemaTransformer` and `UBookIt.Backoffice` implements `IOpenApiOperationTransformer`; it arrives transitively through Umbraco 18 either way, but a package we compile against belongs in the nuspec rather than resting on someone else's graph. Pinned `10.0.11`, which publishes as `>= 10.0.11` like every other NuGet dependency |
| `UBookIt.Backoffice/Composers/UBookItBackofficeApiComposer.cs` | Ported; ~40 lines become ~5 |
| `UBookIt.Web/Composing/UBookItDeliveryApiComposer.cs` | Ported, anonymous — no auth requirement |
| `README.md` | Versioning table generalised. **Must be cherry-picked to `main`** — it is true of both lines, and leaving it here alone makes two published READMEs disagree |
| Generated TS client | Re-generated; method names verified, not assumed |
| Specs, domain, persistence, rendering | **Untouched.** `UBookIt.Core` references no Umbraco type at all |

No breaking change to uBookIt's own public API. The host requirement changes, which is the whole
point of the line.
