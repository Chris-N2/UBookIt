## Context

See `proposal.md` — *Why*. The measurement this is built on is the spike commit `708056f` on
`dev/v18`, which bumped the dependencies and let the compiler enumerate the damage rather than
reasoning about it:

- Restore fails `NU1605` until `Microsoft.EntityFrameworkCore.SqlServer` moves 10.0.10 → 10.0.11,
  because `Umbraco.Cms.Persistence.EFCore 18.2.0` requires `>= 10.0.11`.
- The build then produces **four errors in two files**, both composers, all four caused by the
  same dependency swap.

What Umbraco 18 offers instead, read off the shipped assemblies rather than from memory:
`AddBackOfficeOpenApiDocument`, `AddUmbracoOpenApiDocument`, `AddOpenApiDocumentToUi`,
`AddDocumentTransformer`, `AddOperationTransformer`, `AddSchemaTransformer`, `GenerateOperationId`,
`ConfigureUmbracoOpenApiOptionsBase`, `ExcludeFromDefaultOpenApiDocumentAttribute`.

## Goals / Non-Goals

**Goals**

- Build and run on Umbraco 18 with **no behaviour change** — every existing guarantee holds.
- Keep the diff between `main` and `dev/v18` as small as it is today. The branch is valuable
  precisely because 83% of the codebase has no Umbraco coupling; a port that spreads changes
  beyond the coupled surface would forfeit that.

**Non-Goals**

- Not a redesign of how uBookIt describes its APIs. If Umbraco 18's registration is a better
  shape than what it replaces — and it is — take the improvement, but do not go looking for more.
- Not making the 17 line use the new API. Umbraco 17 does not have it.

## Decisions

### D1 — Use Umbraco's first-class registration, not a re-implementation of the filters

The temptation is to port like-for-like: find the new equivalent of an operation filter, of a
security requirement, of an operation-ID handler, and rebuild the same three pieces. Umbraco 18
instead offers one call that does all of it:

```csharp
builder.AddBackOfficeOpenApiDocument(
    Constants.ApiName,
    document => document
        .WithTitle("uBookIt Backoffice API")
        .WithBackOfficeAuthentication());
```

**Rejected: port each piece.** It would reproduce, in a supported host's extension points, work
the host now does itself — and every line of it would be ours to maintain across future majors.

### D2 — The delivery document is registered WITHOUT authentication, and that is a guarantee, not an omission

`UBookItDeliveryApiComposer` today ends its Swagger block with a comment: *"Deliberately no
security operation filter — the delivery API is anonymous, so its operations carry no auth
requirement."* That is a real property of a published, anonymous API and the port must preserve
it deliberately rather than by not typing something.

So the delivery document uses the non-backoffice registration and **the absence of
`.WithBackOfficeAuthentication()` carries a comment explaining it is deliberate** — otherwise the
next person adds it "for consistency".

**Open at apply time, and to be resolved by reading the API rather than guessing:** whether the
right call is `AddUmbracoOpenApiDocument`, plain `AddOpenApi`, or something else. All three names
exist in the 18.2.0 assemblies. The *guarantee* is fixed; the call is not yet.

### D3 — Whether `OperationIdHandler` is still needed is a question to answer, not to assume

That handler exists for one reason: to keep generated TypeScript method names short instead of
the verbose default. Umbraco's docs say `AddBackOfficeOpenApiDocument` "applies the schema and
operation ID conventions" — which *suggests* it is now unnecessary.

**Suggests is not knows.** The test is the generated client: regenerate it and compare the
exported method names against the current ones. If they are still short, the handler goes; if
they are not, it is ported to `GenerateOperationId`. Deleting it because the docs imply it is
redundant would be assuming a property of an artifact nobody looked at.

### D4 — `Directory.Build.props` stays at `17.1.1` on this branch

Publishing `18.0.0` is a separate release change. Leaving the version alone has a second
benefit that is worth more than tidiness: **`17.1.1` is already published, so this branch cannot
be released by accident** — any push of it would be rejected as a duplicate.

### D5 — Screenshots are kept, and looked at before the v18 line publishes

Chris's assessment is that the Umbraco 18 backoffice is near-identical to 17, and the one feature
18 added (the block library) does not touch uBookIt. Recorded as **his assessment, not a
measurement** — so the release change carries a step to open the backoffice on 18 and look, and
retake only if it is visibly wrong. Re-capturing on spec would cost the effort now for a change
nobody has established is needed.

### D6 — The versioning-table edit must be cherry-picked back to `main`

`README.md`'s table hardcodes `17.x`, which is false on the v18 line — but it is *also* imprecise
on `main`, because the policy has always anticipated later majors. The fix is true of both lines.

**Left only on `dev/v18`, it makes two published READMEs disagree about the package's own
versioning policy**, in a file packed into every NuGet package on both lines. Branch flow runs
`main` → `dev/v18`, so a shared-file edit made here will also conflict at the next merge forward.
The obligation is named in `tasks.md` rather than left to be remembered.

## Risks / Trade-offs

- **The generated TS client changes shape and the backoffice breaks at runtime rather than at
  compile time** → regenerate, diff the generated client, and run the client test suite; then a
  live check in a v18 backoffice. The compiler cannot see this one.
- **`delivery-api`'s "a disabled endpoint does not appear in the OpenAPI document" guarantee
  breaks silently** → its guards are the acceptance criteria for the port. If the mechanism that
  hides disabled endpoints was coupled to Swashbuckle's pipeline, this is where it surfaces.
- **The two lines drift in files that have nothing to do with Umbraco** → keep the port inside the
  coupled surface, and cherry-pick D6's shared edit rather than letting it diverge.
- **Umbraco 18 is STS and reaches EOL in June 2027** → accepted and already decided: the line is
  secondary, `main` stays on LTS, and the branch is dropped at EOL rather than carried.

## Migration Plan

None for consumers of the 17 line: it is untouched. Consumers of the future 18 line install
`18.x` against Umbraco 18 — the same schema, the same API surface, the same behaviour.

Rollback is deleting the branch; nothing is published by this change.

## Open Questions

- **Which registration call the delivery document uses** (D2). Deferrable: the guarantee is
  settled, only the API name is open, and it is answered by reading the assembly at apply time.
- **Whether `OperationIdHandler` survives** (D3). Deferrable for the same reason — it is answered
  by regenerating the client and looking, which is a task rather than a decision.
