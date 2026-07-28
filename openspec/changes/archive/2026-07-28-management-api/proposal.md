# Proposal: management-api

## Why

uBookIt now has a tested domain and durable SQL Server persistence, but no way for anyone to *create* a bookable resource except inserting rows by hand. This change delivers the package's first user-facing surface: a native Umbraco backoffice section where editors manage resources and their availability, backed by a versioned Management API. It is the prerequisite for change ④ (the delivery API needs resources to exist) and the first visible proof of invariant 3 — a backoffice that is native Lit/uui, not a third-party widget bolt-on.

## What Changes

- **Management API** (in `UBookIt.Backoffice`, replacing the template's example controller) under the existing `ubookitbackoffice` swagger group:
  - Resource CRUD: paged list, get by id, create, update (details, opening hours, exceptions, constraints), delete.
  - All endpoints require backoffice authorization (project convention); all input is validated server-side through the Core factories, and stable domain failure codes map to RFC 7807 problem details.
  - Delete semantics: a resource with any booking claims cannot be deleted (the claims FK is `Restrict`); the API returns a structured conflict-style failure rather than a 500.
- **Additive resource write surface**: a new Core port for resource management writes (create/update/delete + paged listing), implemented in `UBookIt.Persistence`. Discharges the QA-mandated obligation from the persistence archive: the write path SHALL enforce at-most-one-exception-per-resource-per-date (the DB index is non-unique by design; reads tolerate duplicates by grouping, so writes must prevent them — including under concurrency).
- **HTTP-caller containment** (second carried QA obligation): management endpoints depend only on the new management service/port and the existing validated services; raw `IBookingStore` access and `Booking.Rehydrate` are not reachable from any controller.
- **Backoffice client** (Lit + `@umbraco-cms/backoffice` + TypeScript, replacing the example dashboard): a **uBookIt section** using Umbraco's collection→workspace idiom (UX agreed with the project owner):
  - Collection view: flat `uui-table` of resources (name, type, availability summary) with create/delete affordances and paging. No treeview — v1 resources are deliberately flat.
  - Workspace editor per resource: groups for Details, Opening hours (per-weekday window rows with add/remove), Exceptions (date plus closure/override windows), and Constraints.
  - Typed API client regenerated from the swagger document via the existing hey-api script.
  - Markup is semantic and WCAG 2.2 AA-conscious from the start (uui components, labelled inputs, keyboard operability); *visual polish* is explicitly deferred, accessibility is not.
- Tests: integration coverage for the new write paths (round-trips, exception-uniqueness enforcement including racing writers, delete-with-claims refusal), unit coverage for API-model ↔ domain mapping and failure-code → problem-details mapping; the client build stays clean.
- No breaking changes intended; no new EF migrations expected (the schema already holds everything this change edits).

## Capabilities

### New Capabilities

- `resource-management`: the management surface — API endpoint behaviour (auth, validation, paging, failure mapping, delete semantics) and the backoffice section (collection view, workspace editor, accessibility expectations).

### Modified Capabilities

- `persistence`: added requirements for the resource write store — create/update/delete/list operations, write-path exception-uniqueness enforcement, and delete-refusal semantics for claimed resources.

## Non-goals

- **Bookings/reservations management UI**: nothing can create bookings until ④ ships the delivery API; managing reservations in the backoffice is its own later change.
- **Delivery API (④) and default front-end rendering (⑤)**.
- **Resource hierarchy/grouping**: the domain is flat in v1; a tree can arrive additively if grouping ever enters the domain.
- **Backoffice localization beyond Umbraco's standard mechanism**: ships with a single `en-US` localization file; more languages are contributions.
- **Automated browser/E2E tests**: manual verification against the TestSite in this change; browser automation is noted for the future CI change.
- **Visual design polish**: plain-but-correct screens now; a design pass is future work ("pretty it up later" — owner's words).

## Impact

- `src/UBookIt.Backoffice`: example controller and dashboard removed; Management API controllers, API models, and mapping added; `Client/` gains the section, collection view, workspace elements, and regenerated API client. No new NuGet dependencies expected; any new npm devDependency must be named with its license in the specs.
- `src/UBookIt.Core`: additive management port (interface + service) for resource writes; no changes to existing types.
- `src/UBookIt.Persistence`: implements the new port; no schema change anticipated (uniqueness enforced in the write transaction, consistent with the persistence QA record).
- `tests/`: unit + integration additions as above.
- Public API surface: additive only (new port, new service, new API models). The Management API's HTTP contract becomes a compatibility surface for the separate DevExpress UI repo to consume via public contracts — naming here matters.
