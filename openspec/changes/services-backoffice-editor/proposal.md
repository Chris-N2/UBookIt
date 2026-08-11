## Why

Change ⑥ shipped services as a **management API only**, deferring the backoffice UI to "an immediate fast-follow" (⑥ design D5). Until that lands, defining a service requires an authenticated HTTP client — services are effectively unusable by the editors the package is built for, and the ⑥ capability cannot be demonstrated or dogfooded. This change is that fast-follow: the Lit collection + workspace editor for services, mirroring the resource editor that change ③ established.

It also removes a latent correctness trap before booking-via-service (⑦) is built. A service role names a resource **type** key, and that key is currently free text with no registry anywhere in the system. A typo (`masseus` for `masseur`) produces a service that silently resolves to zero eligible resources, with nothing anywhere reporting the mistake. Introducing the type picker now — while the services UI is being written anyway — means ⑦ inherits a constrained key instead of a class of silent-failure bug reports.

## What Changes

- **New management endpoint `GET resources/types`** returning the distinct resource type keys in use with a count of resources per type, backed by a new `IResourceManagementStore.ListTypesAsync`. Additive; no schema change.
- **Services collection view** — a second `sectionView` in the existing uBookIt section, beside Resources: a semantic `uui-table` of services (name, requirement summary, duration) with paging and create/edit/delete affordances, mirroring the resource collection view.
- **Services workspace editor** — Details (name), **Service Requirements** (the single role, rendered as a list-of-one), and Duration, with the same error-summary + focus-management accessibility behaviour the resource editor established.
- **Duration is an explicit choice**, not an empty box: a radio pair between "use each resource's minimum duration" (sends `null`) and "fixed duration of N minutes". The null semantics are currently invisible in the API contract alone.
- **Resource type is chosen from an editable combobox** — pick a type already in use, or type a new one. Naming a type no resource currently has is legitimate (define the service before hiring the person), so an unknown type produces a **non-blocking inline hint**, never a validation error.
- **`window.confirm` replaced by the Umbraco confirm modal** in **both** the resources and services collection views, via one shared helper. This discharges an outstanding pre-release obligation recorded by change ③'s QA review.
- **`ubookitServices_*` localization block** in `en-us.ts`, so the new UI is translatable by third-party language packs rather than hard-coding English.
- **Regenerate the hey-api TypeScript client** — `sdk.gen.ts` currently contains no service operations, so nothing in the UI can call ⑥'s endpoints until it is regenerated against the running site.

## Capabilities

### New Capabilities

None. This change adds UI and one read endpoint over capabilities that already exist.

### Modified Capabilities

- `services`: gains backoffice UI requirements — a collection view, a workspace editor, the duration affordance, the requirement (role) affordance, and the accessibility baseline. No change to the service domain model, its persistence, or the existing CRUD endpoint contracts.
- `resource-management`: gains the `GET resources/types` endpoint requirement, and its collection-view requirement is amended so delete confirmation uses an accessible in-page modal rather than a native browser dialog.

## Impact

- **New code**: `UBookIt.Core` (`ListTypesAsync` on the management store port, a `ResourceTypeUsage` read shape); `UBookIt.Persistence` (a grouped projection implementing it); `UBookIt.Backoffice` (a `types` action on `ResourcesController`, a response model, and the new client elements `services-view` / `services-list` / `services-editor` plus a shared confirm helper and manifest entries). Tests in `UBookIt.Tests` / `UBookIt.Tests.Integration`.
- **Modified code**: `resource-list.element.ts` (confirm modal only), `section/manifest.ts`, `localization/en-us.ts`, and the generated `src/api/**` client.
- **No changes** to the service or resource domain models, the delivery API, the default front-end, or the database schema. **No EF Core migration** — the new endpoint is a projection over existing tables.
- **Public API surface**: one additive management endpoint. No breaking change, no destructive schema change.
- **Dependencies**: none added. The confirm modal comes from `@umbraco-cms/backoffice/modal`, already a dependency.
- **Discharges**: change ⑥ design D5 (the deferred editor) and change ③'s QA `window.confirm` obligation.

## Non-goals

- **Booking via a service** (⑦) — eligibility resolution, union availability, and candidate-loop placement remain out of scope. This change only makes ⑥'s *definition* capability usable by humans.
- **Capabilities on roles** (⑧) and **multiple roles per service** (⑨). The Requirements section renders exactly one role with no add/remove affordance; `count` is not surfaced and is always sent as `1`. Both are deliberately shaped so the later slices add to the UI rather than rewrite it.
- **URL-addressable editor routing.** The existing resources view routes list↔editor with component state, so an open editor is not deep-linkable or refresh-safe. The services view mirrors that wart deliberately for consistency; fixing it means moving both views onto Umbraco routing/workspaces, which is its own change and better done once the section has more than two views.
- **Resource types as first-class entities.** A type stays an extensible free-form key; this change only surfaces the keys already in use. A type registry would contradict the model established in ⑥ and the resource domain.
- **Visual/styling polish** beyond matching the existing section. Accessibility is in scope; aesthetics are not.
- **Human screen-reader and keyboard-only audit** of the backoffice — still the pre-release obligation carried from ③, not discharged here. Structural accessibility is verified; a human assistive-technology pass is not.
- **A `service-in-use` delete guard.** Nothing references a service yet, so deletion stays unconditional; the list renders server-authored failure messages generically so a future code surfaces without a client change.
