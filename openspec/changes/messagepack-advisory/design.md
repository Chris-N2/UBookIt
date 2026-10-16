## Context

The precedent is `crypto-advisories-2026-10` (archived 2026-10-15). It declared
`System.Security.Cryptography.Pkcs` 10.0.13 in `UBookIt.Persistence` and `UBookIt.Web`. Its
design settled the general method, which became the packaging requirement *A shipped package
declares the fix for an advisory its host cannot yet supply*. This change applies that method to a
second package. Two things make it simpler than the precedent:

- **MessagePack is not in the ASP.NET Core shared framework, and the SDK does not prune it.** So
  there is no framework copy, nothing like D6's `Xml` case, and no question of which copy loads.
  A site loads the package NuGet resolves.
- **It reaches us through Umbraco alone.** `dotnet nuget why` on `UBookIt.Web` (main) shows every
  path going through `Umbraco.Cms.Api.Common`, `Umbraco.Cms.Web.Website`, `Umbraco.Cms.Web.Common`
  and `Umbraco.Cms.PublishedCache.HybridCache`, all 17.7.1, to MessagePack 3.1.8.

## Goals / Non-Goals

**Goals:** every shipped package's resolved graph reaches MessagePack 3.1.9 or later, on both
lines, with the narrowest declaration the requirement allows; the audit is clean of the advisory.

**Non-Goals:** see the proposal.

## Decisions

### D1 — Direct references, not transitive pinning

As the precedent's D1: `CentralPackageTransitivePinningEnabled` would promote every
`PackageVersion` entry it pins into the packed nuspec, declaring EF Core and `Umbraco.Cms.*`
dependencies nobody chose. The requirement forbids that mechanism.

### D2 — Which projects get the reference: measured, smallest set

The rule, from the requirement: declare it only in a packable project whose resolved graph
contains MessagePack at a vulnerable version, and only where no uBookIt project it references
already brings the fix.

Expected, from the project references and the 2026-10-15 audit:
- `UBookIt.Backoffice` (references Core and Persistence; neither has MessagePack): **declare**.
- `UBookIt.Web` (references Core only): **declare**.
- `UBookIt` (references Backoffice and Web): **do not declare**; both bring 3.1.9.
- `UBookIt.Persistence`, `UBookIt.Core`: MessagePack is not in their graphs; **must not** gain it.

The apply measures this on each line, for Debug and Release:
1. Before the change: `dotnet list package --vulnerable --include-transitive` per packable project.
2. After the change: the same list, and the audit script.
3. Then check that removing either reference brings the advisory back.

`dev/v18` resolves through Umbraco 18.2.1, so its set is measured separately, not assumed.

### D3 — The nuspec diff is the proof, read from the packed packages

Pack all five packages before and after, extract each nuspec, and diff the dependency groups and
framework references, parsing them as XML. The only expected difference is a `MessagePack`
dependency at `3.1.9` in `UBookIt.Backoffice` and `UBookIt.Web`. Any other difference is a
defect: it would break "adds nothing that was not already there".

As in the precedent, no new test asserts the pin exists. The scheduled audit enforces the
guarantee, and a test pinning `>= 3.1.9` would become wrong the day D4 removes it. Existing guards
that read packed nuspecs (`PackageCompositionTests` and others) run as they are; if one fails, the
apply reads why and updates the expectation with the reason.

### D3a — Why 3.1.9 and not 3.1.11 or 3.1.12

3.1.9 is the first fixed version, and no known advisory affects it or later. Declaring the first
fixed version raises a site's minimum by the least, as the `Pkcs` declaration did with 10.0.13.
Umbraco's stable `17.8.0` / `18.3.0` will bring 3.1.11 when a site upgrades, and NuGet takes the
higher minimum, so 3.1.9 never holds a site back.

### D4 — The declaration has an exit

The change that raises the Umbraco floor to a stable release resolving MessagePack 3.1.9 or later
(expected: `17.8.0` / `18.3.0`) removes this declaration and the `Pkcs` one together. The
`PackageVersion` entry carries a comment naming the advisory and that exit, as the `Pkcs` one does.
The deferred-obligations memory records it.

### D5 — A runtime smoke test, because HybridCache uses MessagePack

Umbraco's HybridCache serializes published content with MessagePack, so a binary-incompatible
3.1.9 would break content rendering, not only compile. The apply starts each line's TestSite and
fetches a content page and a booking page through the HybridCache. It records the MessagePack
assembly version in the build output (`bin`), so the run is known to have used 3.1.9.

## Risks / Trade-offs

- **[3.1.9 behaves differently from 3.1.8 for Umbraco]** A patch release of the same minor, which
  Umbraco's own RCs move past (3.1.11). → D5's smoke test, on both lines.
- **[A site pins MessagePack lower itself]** NuGet reports a downgrade (NU1605). The .NET SDK
  treats that as an error by default; confirm this at apply rather than restating it. → The
  release changelog says to raise any direct reference to 3.1.9, as `17.3.2` did for `Pkcs`.
- **[The declaration outlives its reason]** → D4.

## Migration Plan

Nothing for a site beyond updating uBookIt. The release changelog states the advisory, and that
updating fixes it without touching Umbraco.
