## Context

Change ⑥ delivered the services domain, persistence, and management API but explicitly deferred the backoffice UI (⑥ design D5: *"the editor will mirror the existing resource collection/editor closely"*). This change is that fast-follow, so the dominant constraint is **consistency with what change ③ built** rather than novelty: `resources-view.element.ts` (55 lines, routes list↔editor via component state), `resource-list.element.ts` (217 lines, `uui-table` + paging + delete), `resource-editor.element.ts` (520 lines, `uui-box` groups, `role="alert"` error summary that receives focus on failed save).

Current state relevant to the work:

- The TypeScript client is **generated** by hey-api (`npm run generate-client`, pointed at `https://localhost:44348/umbraco/swagger/ubookitbackoffice/swagger.json`). `sdk.gen.ts` contains **zero** service operations today, so nothing can be written against ⑥'s endpoints until it is regenerated against a running site.
- `ServiceRequestModel` is `{ Name, DurationMinutes: int?, Roles: [{ ResourceType, Count }] }`. `ServiceRole` is already `(string ResourceType, int Count)` in Core and in the `uBookItServiceRole` table — multi-role and count > 1 are **v1 factory guards, not schema limits**.
- Failure codes in play: `service-name-required`, `service-role-invalid`, `service-duration-invalid`, `service-not-found`, `type-key-invalid`. Every `DomainFailure` carries a server-authored `Message`, and `toApiErrors` (`section/api-errors.ts`) already normalizes the three failure shapes into a flat `{code, message, field}[]`.
- Resource type is a free-text key normalized to kebab-case (`^[a-z0-9]+(-[a-z0-9]+)*$`). **No endpoint anywhere lists the types in use** — verified across the management and delivery controllers and `IResourceStore`.
- `resource-list.element.ts:63` uses `window.confirm`. Change ③'s QA logged replacing it with a `uui` modal as a pre-release obligation.

## Goals / Non-Goals

**Goals:**

- Make services fully manageable by a backoffice editor, with no HTTP client required.
- Close the free-text resource-type hole *before* ⑦ builds eligibility resolution on top of it.
- Keep the UI shaped so ⑧ (capabilities) and ⑨ (multi-role) extend it additively.
- Discharge two recorded obligations (⑥ D5, ③'s `window.confirm`) rather than letting them age.
- Add no new dependency and no schema change.

**Non-Goals:**

- Everything in the proposal's Non-goals — booking via a service, capabilities, multi-role, URL-addressable routing, a resource-type registry, visual polish, and a human assistive-technology audit.
- Refactoring the resource editor beyond the single confirm-dialog call site.

## Decisions

### D1: A new `GET resources/types` endpoint, not client-side derivation

The service role's `ResourceType` must match a resource's `Type` exactly for ⑦'s `Eligible(role)` to return anything. A typo yields a service that silently resolves to the empty set, with nothing in the system reporting it — a bug class with no diagnostic. A picker removes it.

Three ways to populate that picker were considered:

| Option | Cost | Verdict |
|---|---|---|
| Free text (mirror the resource editor) | zero | Rejected — propagates the wart into the very place it does damage |
| Client derives the type set by paging `listResources` | front-end only | Rejected — pages the entire resource collection to compute a `Set`; fine at 20 resources, wrong at 2000 |
| **A dedicated endpoint returning distinct types + counts** | one port method, one projection, one action | **Chosen** |

- **Why**: it is a grouped projection over an existing table — no migration, one round trip, and it scales with the number of *types* rather than the number of resources. ⑦ will want type-based filtering regardless, so this is not speculative work.
- **Placement**: `ListTypesAsync` goes on **`IResourceManagementStore`**, not `IResourceStore`. The only consumer is a backoffice endpoint, and change ③'s containment rule keeps management concerns off the read port that anonymous delivery callers depend on. ⑦ may later add a read-side equivalent for eligibility; that is honest additive duplication, preferable to pre-building ⑦'s read path now.
- **Counts included** so the "no resources have this type" hint costs no second request.
- **Deterministic ordering** (by type key) so the picker does not shuffle between loads.

### D2: An editable combobox, not a strict dropdown

Defining "Sports massage → `physiotherapist`" before any physiotherapist resource exists is legitimate — setup order is the editor's business, not ours. A strict dropdown would block it and push editors back to the API.

So: choose from the types in use, **or** type a new key. An unmatched key produces a **non-blocking informational hint** ("No resources currently have this type"), never a validation error. Server-side `type-key-invalid` still rejects genuinely malformed keys, surfaced like any other failure.

- **Alternative rejected**: strict dropdown plus a separate "add new type" flow. More UI for a case the combobox handles in one control, and it implies a type registry that deliberately does not exist.

**Control choice, settled during implementation:** a native `<input list>` bound to a `<datalist>`, not `uui-combobox`. Checked against the installed package: `uui-combobox`'s value must correspond to one of its `uui-combobox-list` options — text typed into its search field never becomes the value — so it structurally cannot express "a type no resource has yet", which is the entire escape hatch this decision exists to preserve. The native pairing gives suggestion-plus-free-text with built-in keyboard and assistive-technology behaviour, and the resource editor already labels native `date`/`time`/`number` inputs the same way (`label[for]` within the shadow root), so this is consistent rather than novel. `uui` remains preferred wherever a `uui` control actually fits.

### D3: Services as a second `sectionView`, mirroring the existing routing wart

Verified against the installed `@umbraco-cms/backoffice@17.5.3`: the Packages section's Packages/Installed/Created tabs are `sectionView` extensions (`Umb.SectionView.Packages.*`) with the same `meta.pathname`/`icon` shape our manifest already uses. (`dashboard` remains a distinct extension type in v14+ — the welcome panels in Content — and is not what we want here.) Adding a second entry to `section/manifest.ts` is ~15 lines and the tab strip appears for free.

The services view mirrors `resources-view.element.ts`'s component-state list↔editor routing **deliberately**, accepting that an open editor is not deep-linkable or refresh-safe.

- **Why**: two views with one consistent behaviour beats two views with two. Fixing it properly means moving both onto Umbraco routing/workspaces — a larger change, better done once the section has more than two views (Bookings and Settings are coming) so the cost is paid once.
- **Trade-off**: the wart is now duplicated. Recorded as a follow-up rather than fixed silently.

### D4: Duration as an explicit two-way choice

`DurationMinutes: null` means "defer to each resource's `MinDuration`" — real behaviour that an empty number box communicates not at all.

```
  ┌─ Duration ──────────────────────────────────────┐
  │  ( ) Use each resource's minimum duration       │  → null
  │  (•) Fixed duration:  [  60 ] minutes           │  → 60
  └─────────────────────────────────────────────────┘
```

A radio pair, with the number input **disabled rather than hidden** in the defer state so the layout does not jump. The editor cannot preview the inherited value (it depends on which resource is resolved at booking time, which is ⑦'s job), so the deferral option carries explanatory text instead of a computed number.

### D5: Requirements as a list-of-one, `count` hidden

v1 enforces exactly one role of count 1, but ⑨ ("1 room but 2 physios") needs N roles with counts and the data model already supports it.

```
  ┌─ Service Requirements ──────────────────────────┐
  │  ┌───────────────────────────────────────────┐  │
  │  │ Resource type   [ masseur           ▾ ]   │  │ ⑨ adds rows here
  │  └───────────────────────────────────────────┘  │ ⑧ adds capabilities here
  │  (no add/remove control in v1)                  │
  └─────────────────────────────────────────────────┘
```

Rendering the single role as a one-item list means ⑨ adds a button and a `.map()` rather than restructuring the group, and the editor's mental model ("a service *requires things*") is right from day one. `count` is not rendered — a field that accepts only one value is worse than no field — and is hard-sent as `1`.

- **Naming**: the group is labelled **"Service Requirements"** in the UI. "Roles" stays the domain term but collides with backoffice *user* roles in an editor's head, so it does not appear on screen.

### D6: One shared confirm helper, both lists converted

Verified against the installed package rather than assumed:

- `UMB_CONFIRM_MODAL` — `@umbraco-cms/backoffice/modal`, data `{ headline, content, color?: 'positive'|'danger'|'warning', cancelLabel?, confirmLabel? }`.
- `umbOpenModal(host, token, args)` → `Promise<ModalValue>`, same entry point.
- **The trap**: cancelling **rejects** the promise (`modal.context.js` holds a `#submitRejecter`); it does not resolve `false`. A helper that only `await`s will throw an unhandled rejection every time a user backs out.

So the helper wraps `umbOpenModal` in `try/catch` and returns a boolean — cancellation is a normal outcome, not an error. Both `resource-list` and `services-list` call it.

- **Why now**: doing services with a modal while resources keeps a native dialog is the one genuinely bad option — divergence in a section the same person uses. Converting both is a handful of lines beyond what we write anyway, and discharges ③'s obligation.
- **Scope discipline**: the helper does confirmation and nothing else. No other cleanup of `resource-list.element.ts`.

### D7: Render server-authored failure messages generically

`resource-list.element.ts:78-80` maps `resource-in-use` to a hard-coded English string. The services list will not do this: `DomainFailure` already carries a server-authored `Message` and `toApiErrors` already flattens it, so the list renders `error.message` with a generic fallback only when no message is present.

- **Why**: nothing references a service yet, so delete is unconditional — but ⑦/⑨ will plausibly add a `service-in-use` guard. Generic rendering means that code surfaces meaningfully with no client change. It is also *less* code than the resource list's approach.

### D8: Regenerate the client as a first task, not a side effect

`npm run generate-client` requires the TestSite running at `https://localhost:44348`. It rewrites all of `src/api/**`, so it must land as its own reviewable step before any UI work, and the diff must be checked for unrelated drift (the generator also picks up any other endpoint changes since the last run).

## Risks / Trade-offs

- **[The types endpoint makes a "small front-end change" full-stack]** → Accepted deliberately (D1). Mitigated by scope: one port method, one grouped projection, one controller action, no migration. Task ordering puts it early so the front-end work is never blocked on it.
- **[Cancel-rejects trap in `umbOpenModal`]** → Identified before coding (D6) rather than discovered as an unhandled rejection; the helper's `try/catch` is the whole mitigation, and a cancel path is an explicit spec scenario.
- **[Client regeneration produces unrelated drift]** → Regenerate as an isolated first commit (D8) and review the diff; anything unexpected is a signal, not noise to skim past.
- **[Duplicating the non-routable list↔editor pattern]** → Consciously accepted (D3) and recorded as a follow-up, so a future routing change fixes two known sites rather than discovering one.
- **[The combobox lets a typo through anyway]** → By design (D2); the hint is the mitigation. A hard block would break the legitimate define-before-you-hire case, which is worse.
- **[Accessibility regressions in new UI]** → The resource editor's proven patterns are reused verbatim: `label` property on `uui` controls (a visible `uui-label[for]` cannot pierce the shadow root), focus moved to the `role="alert"` summary on failed save, group errors associated via `aria-describedby`. The radio group and combobox are the two genuinely new control types and need explicit keyboard verification.
- **[Localization scope creep]** → Only `en-US` ships, matching resources; the block exists so third-party language packs are possible, not so we translate anything now.

## Migration Plan

No data migration, no schema change, no EF Core migration — the new endpoint is a projection over existing tables. Deployment is a package upgrade; a site that never opens the services view is unaffected. Rollback is reverting the package: no persisted state depends on this change. The one additive endpoint means an older client against a newer server is fine, and a newer client against an older server would simply fail to populate the picker — not a state a shipped package encounters.

## Open Questions

None blocking. Two deliberately parked:

- Whether the section moves to Umbraco routing/workspaces (D3) — revisit when a third view is added.
- Whether ⑦ needs a read-port equivalent of `ListTypesAsync` for eligibility resolution (D1) — decide there, with ⑦'s access patterns visible.
