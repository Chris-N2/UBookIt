## 1. Fix the guard before it can hide the defect

The scoped-lifetime change in group 3 creates a captive dependency that the existing guard cannot
see. Doing this first means the guard is red for the right reason before anything relies on it.

- [x] 1.1 Rewrite `BookerRetentionTests.The_job_captures_no_scoped_dependency` to derive the scoped service set from the composed `IServiceCollection` registrations instead of the hardcoded six-type array, so it asserts "holds nothing registered as scoped"
- [x] 1.2 Prove the rewritten guard fails against a job that captures a scoped service — add the capture temporarily, watch it go red, remove it. A guard that has never failed is an untested assertion
- [x] 1.3 Confirm the rewritten guard still passes on the unchanged `BookerRetentionJob` (which at this point holds `SiteBookingSettings` while it is still a singleton, and so is legitimately clean)

## 2. Storage

- [x] 2.1 Define the settings store port in `UBookIt.Core` (read all, set one, remove one), following the `IFlagStore` shape; document that a key with no row means "not overridden" and that no row ever holds personal data
- [x] 2.2 Add the settings entity and `DbSet` in `UBookIt.Persistence`, keyed uniquely on the configuration key
- [x] 2.3 Add the additive EF migration; verify it alters and drops nothing
- [x] 2.4 Implement the SQL store
- [x] 2.5 Tests: one row per key on repeated writes, removal restores fall-through, an empty table is indistinguishable from the previous version

## 3. Two-source resolution

- [x] 3.1 Build the effective-configuration composition: site `IConfiguration` as the base layer, stored rows composed over it via `AddInMemoryCollection` so the last source wins
- [x] 3.2 Change the `SiteBookingSettings` registration from singleton to scoped, resolving `ResolveSettings` over the effective configuration. **`ResolveSettings` itself must not change** — if it needs editing, the layering is wrong
- [x] 3.3 Make `BookerRetentionJob` resolve `SiteBookingSettings` inside the scope it already creates per batch, removing it from the constructor. Guard 1.1 should now be the thing that catches a regression here
- [x] 3.4 Tests: a stored value overrides a configured one; an unstored key falls through; an environment-variable value shows through when nothing is stored
- [x] 3.5 Tests: the same unreadable text supplied as a stored value and as a configured value resolves identically and logs identically — one test per fallback (retention, auto-confirm, privacy link, time zone, max query range)
- [x] 3.6 Test: two scopes either side of a stored change see old and new respectively, with no restart
- [x] 3.7 Run the full suite — every existing availability, bookings, email, privacy and retention test is the regression surface for the lifetime change

## 4. The tier declaration

- [x] 4.1 Declare each setting's key, tier and type once, server-side, as the single source both the API and the client read
- [x] 4.2 Add per-setting write validation (type, and for the time zone that the id resolves)
- [x] 4.3 Test: the declaration covers every key the package reads, so a setting cannot be added without being tiered
- [x] 4.4 Test: writing a tier-3 key is refused even with the settings verb held

## 5. The verb

- [x] 5.1 Add `Verbs.Settings` and `VerbPolicies.Settings` to `UBookIt.Backoffice.Constants`, and register the policy in `UBookItAuthorizationComposer`
- [x] 5.2 Add the verb to the client `permission-verbs.ts` vocabulary with a `canManageSettings` predicate, and to the `entityUserPermission` manifest
- [x] 5.3 Leave `UBookItPermissionSeed.AllVerbs` at three, and record in its remarks why the settings verb is excluded on fresh and upgraded installs alike
- [x] 5.4 Test: the existing server/client vocabulary guard sees the fourth verb on both sides
- [x] 5.5 Tests: `Configure` alone does not reach the settings; `Settings` alone reaches neither resources nor bookings; an Umbraco administrator without the verb is refused
- [x] 5.6 Tests: the seed grants the three verbs and never `Settings`, on a fresh seed and on an upgrade

## 6. Management API

- [x] 6.1 Add the settings controller: read all settings (effective value, configured value, tier, restart-bound flag), write one, remove one — all behind `VerbPolicies.Settings`
- [x] 6.2 Include the read-only delivery API exposure settings in the read, sourced from `IConfiguration` rather than from `DeliveryApiSettings` — `UBookIt.Backoffice` does not reference `UBookIt.Web` and must not start. The theme is not presented at all (no configuration key; a code registration in the site's own composer)
- [x] 6.3 Tests: every action refuses without the verb; the read reports the configured value beside the effective one; remove restores fall-through

## 7. Backoffice screen

- [x] 7.1 Add the settings view and register it in the section manifest. NOT conditioned on the settings verb — per design Decision 8 the tab is shown to all section holders so its absence can be explained; the view itself renders the empty state and the server refuses the data
- [x] 7.2 Render tier 1 and 2 as editable controls, tier 3 as read-only with the restart-bound ones marked
- [x] 7.3 Show the configured value wherever a stored value overrides it, with a reset action
- [x] 7.4 Add the static time-zone consequence statement, **programmatically associated with the control** — `uui-*` controls carry no `aria-describedby` and `uui-label` is not a `<label>` (⑱), so verify the association in the rendered DOM rather than assuming it from proximity
- [x] 7.5 Add the section empty state for a user holding the section but not the settings verb, naming where the verb is granted
- [x] 7.6 Tests: the consequence statement reads no booking, resource or availability data; the reset action removes rather than stores; the client renders no editable control for a tier-3 setting

## 8. Documentation

- [x] 8.1 Write `docs/configuration.md`: every key, its tier, its default, its fallback behaviour, and which source wins
- [x] 8.2 Point the scattered references in `notifications.md`, `delivery-api.md`, `theming.md` and `booking-page.md` at it rather than restating
- [x] 8.3 Document the upgrade note: the settings screen is invisible until an administrator grants `UBookIt.Settings`, and why it is not granted automatically
- [x] 8.4 Document that `RetentionDays`, `MaxQueryRangeDays`, delivery API exposure and the theme stay config-only, with the reason for each

## 8a. Re-diff the wholesale replacements

Each of these replaces a requirement outright, deleting whatever it forgets to restate. The
delta files carry a disposition table per requirement; these tasks confirm the shipped code
matches it.

- [x] 8a.1 Re-diff `Package composition registers persistence and Core services` (persistence): confirm all six SHALL blocks and all eleven original scenarios are restated, and that the only intended changes are the settings' source and lifetime plus the strengthened captive-dependency guard
- [x] 8a.2 Re-diff `Access within the section is decided by four verbs` (permissions, renamed from "three verbs"): confirm Manage-implies-Read, the union-across-groups rule and all four original scenarios survive the replacement
- [x] 8a.3 Re-diff `Existing section-granted groups are seeded once` (permissions): confirm the touch-nothing-else rule, the failure-without-failing-startup rule and the idempotent-retry condition all survive, and that the only addition is the settings verb's exclusion

## 9. Verify

- [x] 9.1 Full Release build, zero warnings
- [x] 9.2 Full suite green; report the count against the 2711 baseline
- [x] 9.3 `openspec validate --all --strict`
- [x] 9.4 Drive the screen in the running TestSite: grant the verb, change a tier-1 setting, reset it. **NOT DONE — the Chrome extension is not connected in this session, so the UI was never driven.** Outstanding for QA or a later session. What WAS proven live against the running site and real SQL Server, without the browser: the migration applied, and a stored value took effect on the very next request with no restart — `{"retentionDays":null}` (configuration) → `365` (stored) → `90` (updated) → `null` (row removed, configuration showing through again). The table was left empty. What remains unverified is the rendered screen itself: the controls, the reset button, the consequence statement's placement, and the empty state
- [x] 9.5 Confirm with a deliberate mutation that guard 1.1 still fails when the job captures a scoped service, now that the real change is in place
