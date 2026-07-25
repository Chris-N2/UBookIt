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

5. **Default rendering is dependency-free and accessible.** The shipped
   front-end (ViewComponents/Razor + minimal JS) uses semantic HTML and
   meets WCAG 2.2 AA. Accessibility is a differentiator for this package,
   not a checkbox: booking UIs are notorious accessibility failures.

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
