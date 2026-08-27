# uBookIt

Open-source booking system package for Umbraco. There is no good open-source
booking system for Umbraco; this aims to be it.

## Invariants — these survive every context and every spec

1. **Umbraco 17+ (LTS) only.** Single .NET target matching Umbraco 17's
   supported runtime. No multi-targeting for earlier Umbraco majors. v13 and
   the v14–16 STS line are explicitly out of scope.

2. **Zero DevExpress in this repository.** No `PackageReference`, no npm
   dependency, no CDN script tag, no copied source. The DevExpress EULA
   forbids redistribution, so any DevExpress presence makes the repo
   unbuildable for unlicensed users. A DevExpress-based front-end
   (`UBookIt.UI.DevExpress`) is a **separate package in a separate repo**
   that consumes only the public contracts defined here. QA review enforces
   this with a dependency scan; a violation is an automatic reject.

3. **Backoffice is native Umbraco 17.** Lit web components + the Umbraco UI
   Library (`uui-*`), TypeScript, Vite. No third-party widget frameworks in
   the backoffice, ever. The backoffice is for configuration and management
   of bookable resources, availability, and reservations — not for fancy
   rendering.

4. **The front-end contract is data, not widgets.** Alternative UI
   implementations plug in at the level of **API endpoints + strongly-typed
   view models**. Never introduce widget-level abstractions
   (`IScheduler`, `ICalendarControl`, etc.) — they become either
   lowest-common-denominator or a reimplementation of a vendor API. If a
   proposed abstraction wraps a UI component rather than describing data or
   behaviour, reject it at propose time.

5. **Default rendering is dependency-free and accessible — for the part we
   actually ship.** The shipped front-end (ViewComponents/Razor + minimal
   JS) uses semantic HTML and meets **every WCAG 2.2 AA criterion
   determined by markup**. Accessibility is a differentiator for this
   package, not a checkbox: booking UIs are notorious accessibility
   failures.

   The claim names its own boundary, because uBookIt is a component inside
   somebody else's page and WCAG conformance is a property of a *page*:

   - **Ours, always.** Labelling, grouping, programmatic relationships,
     keyboard operability, reading and focus order — and each flow stays
     usable with **no author stylesheet applied at all**.
   - **Ours by default, theirs on override.** Contrast (1.4.3, 1.4.11),
     focus appearance (2.4.11, 2.4.13) and target size (2.5.8) are decided
     by CSS. The shipped stylesheet meets them by deciding no colour of its
     own and leaving focus to the browser; a site that overrides a colour
     token owns that contrast.
   - **Never ours.** Reflow, text spacing, bypass blocks, page title, page
     language, and the document's heading outline the flow's `<h2>` sits in.

   This is a narrowing, not a lowering — **we do not take responsibility for
   code we did not write.** It is honest rather than an escape hatch only
   because of the last clause of the first bullet: since the markup is
   operable with no CSS at all, no stylesheet a site ships can make a flow
   *inoperable*, only harder to read. Never trade that clause away to
   simplify the wording.

6. **OpenSpec drives all changes.** explore → propose → apply → qa-review.
   No code without an approved change. Never modify anything under
   `openspec/` archive. QA review (`qa-review` skill) always runs in a
   fresh context or subagent — the context that implemented a change never
   reviews it.

## Architecture shape (initial proposal — refine via OpenSpec, don't treat as spec)

```
src/
  UBookIt.Core/          Domain: resources, availability rules, slots,
                         bookings, services, abstractions. Keep Umbraco
                         types out of here where practical.
  UBookIt.Persistence/   EF Core entities, migrations, repositories.
  UBookIt.Backoffice/    Management API controllers + backoffice client
                         (TypeScript/Lit/Vite).
  UBookIt.Web/           Delivery API + default ViewComponent/Razor
                         rendering.
tests/
  UBookIt.Tests/         Unit + integration tests.
```

A headless consumer (React/Vue/mobile) must be able to build a full booking
flow against the delivery API without referencing `UBookIt.Web`.

## Conventions

- Nullable reference types enabled everywhere; warnings are errors in CI.
- Public API surface is a compatibility promise once published. Additive
  changes preferred; a breaking change must be called out explicitly in its
  spec.
- EF Core migrations are additive. Destructive schema changes require
  explicit spec approval and an upgrade path from the previous package
  version.
- Management API endpoints require backoffice authorization policies.
  Delivery endpoints validate all input; booking form submissions use
  anti-forgery protection.
- No secrets, connection strings, or personal data in logs or test
  fixtures.

## Workflow

OpenSpec is installed via `@fission-ai/openspec` (CLI: `openspec`); skills
live in `.claude/skills/`, slash commands under `/opsx:*`.

1. `openspec-explore` (`/opsx:explore`) — investigate, gather context, no
   decisions.
2. `openspec-propose` (`/opsx:propose`) — write the change spec; get human
   approval.
3. `openspec-apply-change` (`/opsx:apply`) — implement against the
   approved spec.
4. `qa-review` — **new session or subagent**, adversarial review against
   the spec. Findings go back through apply; QA never fixes code itself.
5. `openspec-archive-change` (`/opsx:archive`) — after QA approval,
   archive the change and sync main specs.

### Rewriting a requirement destroys guarantees silently

A `## MODIFIED Requirements` entry **replaces its requirement wholesale** —
body and every scenario. Anything the old version guaranteed and the new one
forgets to restate is deleted from the spec, with nothing in the diff that
looks like a deletion. A grep for stale names cannot see it, because the
sentence is inside a requirement you are legitimately replacing.

So whenever a delta modifies a requirement, **diff the guarantees, not the
prose**:

1. List every scenario and every SHALL in the requirement as it stands in
   `openspec/specs/`.
2. For each, decide explicitly: carried forward, deliberately dropped, or
   superseded by a stronger claim. Carried-forward guarantees need a scenario
   in the new version — the same behaviour, reworded for the new shape, not
   assumed to survive.
3. A deliberate drop belongs in the proposal, stated as a removal with its
   reason. Silence is not a decision.

This is distinct from the sync-time grep for *sibling* specs the change
falsifies (which has found something on four consecutive changes and is still
worth doing). That grep looks outward at requirements you are not touching;
this looks inward at the one you are replacing.

Real case, ⑧a: change ⑧ guaranteed the readout describe the **type** when a
role required no capabilities, so it could never refer to capabilities the
editor had not named. ⑧a replaced that requirement with a three-stage chain,
did not restate the guarantee, and shipped a summary asserting "N of those have
the required capabilities" for a configuration requiring none. QA caught it;
the change's own falsified-sentence grep did not, and could not.
