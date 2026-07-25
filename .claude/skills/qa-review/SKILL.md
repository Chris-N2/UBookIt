---
name: qa-review
description: Adversarial QA review of an implemented OpenSpec change in uBookIt. Use whenever a change has completed openspec-apply-change and needs verification, or whenever the user asks to QA, review, verify, check, or sign off work. Must run in a fresh context or subagent that did not write the code under review — if this session implemented the change, tell the user to start a new session or spawn a subagent instead of proceeding.
---

# QA Review

## Stance

You did not write this code. Assume it contains defects and your job is to
find them. Do not extend the implementer the benefit of the doubt; verify
behaviour against the **spec**, not against what the code or its comments
claim. You report findings — you never fix code. Fixes go back through
`openspec-apply-change` so the review stays independent.

## Inputs

The change ID (or path) of the OpenSpec change to review. If not given,
ask. Read the change's spec, design notes, and task list in full before
looking at any code.

## Process

Work through every gate. A gate marked **hard fail** forces a REJECT
verdict regardless of everything else.

### 1. Spec conformance

- Extract every acceptance criterion / requirement from the spec into a
  checklist.
- For each: locate the implementing code and the covering test. A
  criterion with no covering test is a finding (major).
- Flag behaviour present in the code but absent from the spec
  (scope creep) and behaviour in the spec but absent from the code.

### 2. Build and tests

- `dotnet build` — must succeed with zero warnings (nullable warnings are
  errors per CLAUDE.md).
- `dotnet test` — all tests pass. Note any skipped tests and why.
- New or changed public behaviour without new or changed tests is a
  finding (major).

### 3. Dependency hygiene — **hard fail**

- Scan for DevExpress in any form:
  ```
  grep -ri "devexpress\|devextreme" --include="*.csproj" --include="*.props" \
    --include="package.json" --include="*.lock" --include="*.cshtml" \
    --include="*.ts" --include="*.html" src/
  ```
  Any hit anywhere in the repo → REJECT.
- Check no widget-level abstraction has been introduced (interfaces or
  base classes whose purpose is to wrap a UI component, e.g. `IScheduler`,
  `ICalendarRenderer`). These violate invariant 4 → REJECT.
- New third-party dependencies: confirm each was named in the spec and has
  a license compatible with the package's open-source license.

### 4. Public API surface

- Diff the public API surface against the previous state. Every addition,
  change, or removal must be intentional per the spec. Breaking changes not
  declared in the spec are a finding (critical).

### 5. Persistence

- Migrations are additive; no column drops, renames, or type narrowing
  without explicit spec approval.
- An upgrade from the previous package version applies cleanly.

### 6. Security

- Management API endpoints carry backoffice authorization policies.
- Delivery endpoints validate input server-side; model validation is not
  the only line of defence for anything security-relevant.
- Booking form submissions have anti-forgery protection.
- No secrets, connection strings, or personal data in logs, exceptions
  surfaced to users, or test fixtures.

### 7. Accessibility (any change touching rendered UI)

Applies to default front-end markup and backoffice views alike:

- Semantic elements: real `<button>`, `<a>`, `<form>`, headings in order.
  No click handlers on divs.
- Every input has an associated label; error messages are linked via
  `aria-describedby` and announced on submit failure.
- Full keyboard operability: tab order sensible, no traps, visible focus.
- Focus is managed on dynamic content (slot selection, validation errors,
  dialog open/close).
- ARIA only where native semantics can't do the job; no redundant or
  contradictory roles.
- Text and interactive-element contrast meets WCAG 2.2 AA. Date/time
  pickers are operable without a pointer.
- Backoffice: prefer `uui-*` components (they carry accessible behaviour);
  flag hand-rolled controls that duplicate an existing uui component.

### 8. Performance sanity

- Availability queries: no N+1 patterns; date-range lookups hit an index.
- No unbounded result sets on delivery endpoints (paging or capped ranges).

## Report format

```
VERDICT: APPROVE | APPROVE WITH NITS | REJECT

Findings:
[CRITICAL] file:line — description; which spec criterion or invariant it violates
[MAJOR]    ...
[MINOR]    ...
[NIT]      ...

Must fix before merge: (list, or "none")
Spec criteria verified: n/m (list any unverifiable and why)
```

REJECT if any hard-fail gate trips or any CRITICAL finding stands.
APPROVE WITH NITS only when every remaining item is genuinely cosmetic.
