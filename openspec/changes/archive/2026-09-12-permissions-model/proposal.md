# The permissions model

Roadmap **0.10.0**, the last row before 17.0.0. Decisions settled with Chris in the explore
on 2026-09-12; the mechanism was established by the permissions spike (roadmap open
decision 2, closed the same day).

## Why

The uBookIt section is all-or-nothing: one grant governs the section and every management
endpoint, so a site cannot say "may see and act on bookings, but not reconfigure resources
and services". The spike established that Umbraco 17 supports granular permission verbs
within a section — an `entityUserPermission` client manifest renders toggles in the user
group editor, the verbs persist as free strings on `IUserGroup.Permissions` with no server
whitelist, and a server handler can check them — so the granularity can be built without
the section-split fallback.

## What Changes

- **Three verbs**, small on purpose and mapped to real surfaces:
  - `UBookIt.Bookings.Read` — the bookings list and its reads. Booker contact details
    remain governed by the **Sensitive data** group on top; verbs do not touch that
    control.
  - `UBookIt.Bookings.Manage` — cancel, confirm, decline. Manage implies Read
    server-side: managing what you cannot see is incoherent.
  - `UBookIt.Configure` — resources, services, and responsibility assignment as one verb.
    Resources and services are too related for split granularity anyone asked for;
    finer verbs would be additive later.
- **The section grant remains the outer gate.** Verbs refine within it and never
  substitute for it; a verb held by a group without the section grants nothing.
- **Erasure and find-by-booker keep their Sensitive data gate**, now alongside
  `UBookIt.Bookings.Read` — Sensitive data remains the control that matters there.
- **A one-time seed at boot**: each user group holding the uBookIt section grant and no
  uBookIt verb receives all three, once, marked done in a package-private table — so an
  existing install's groups keep exactly the access they had, and an admin later emptying
  a group's verbs stays emptied. **Chosen over "no verb means full access"** because that
  reading makes toggling off a group's last verb a silent full grant — an indefensible
  trap. (No uBookIt install exists in the wild yet beyond our own; the seed is still
  built properly because 17.0.0 makes upgrades real.)
- **The backoffice client** registers the `entityUserPermission` manifest (toggles appear
  in the group editor with no custom UI) and hides dashboards and controls the current
  user's verbs do not cover; the server remains the truth.
- The apply opens with a **thin end-to-end proof** — one manifest entry, one toggle, one
  server read — before the full set is built, per the spike's residual risk.

## Capabilities

### New Capabilities

- `permissions`: the verb set and what each governs; Manage implies Read; the section
  grant as the unremovable outer gate; the one-time seed and its marker; the group
  editor surface; client hiding as convenience with the server as truth.

### Modified Capabilities

- `resource-management`: the authorization requirement's scenario "Access to the
  package's section is sufficient" is falsified — section access remains necessary and
  the policy's basis, but endpoint access now also requires the relevant verb.
  Wholesale replacement with guarantee-diff discipline.
- `sensitive-data`: the second-gate requirement's framing "a user with section access
  alone reaches the bookings list" gains the read verb; the guarantee that Sensitive
  data is a second gate, never a replacement, carries unchanged.
- `persistence`: an ADDED requirement for the seed-marker table (the retention-index
  precedent — a new concern, not a schema-shape replacement).

## Impact

- `UBookIt.Backoffice`: verb constants, three authorization policies + a handler beside
  the existing section handler, `[Authorize]` policies on the affected actions;
  client manifest entry and view/control hiding.
- `UBookIt.Persistence`: the marker table (additive migration) and the boot-time seed
  handler (runs after the package's migrations, via `IUserGroupService`).
- `UBookIt.Core`, `UBookIt.Web`: untouched — the delivery API and the Razor flow have no
  backoffice user.
- Docs: `docs/backoffice.md` gains the verbs, the seed, and the toggle surface;
  README's backoffice bullet if it implies all-or-nothing.
- No breaking change for existing installs (the seed preserves access); the new
  behaviour for a *new* group is that the section grant alone shows the section shell
  and grants no data access until verbs are ticked.

## Non-goals

- **No settings screen** — deferred past 17.0.0 with its motivating consumer
  (editor-editable email content); it will lead the 17.1.0 roadmap and its verb slots in
  then.
- **No per-entity granular permissions** (per-resource/per-service grants) — the verbs
  are group-level; Umbraco's granular document-style permissions are not used.
- **No change to the Sensitive data control** — it is neither weakened nor replaced.
- **No writes to Umbraco data beyond the one-shot seed**, which runs once, only adds
  verbs, and only to groups already holding the section.
- **No finer split of Configure**, additive later if ever needed.
