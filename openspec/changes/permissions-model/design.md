# Design — permissions model

## Context

The spike (memory `ubookit-permissions-spike`, roadmap decision 2) verified the chain:
`entityUserPermission` client manifest → toggles in *User Groups → Default permissions* →
`IUserGroup.Permissions : ISet<string>` free strings, mapped verbatim by
`UserGroupPresentationFactory` — no server whitelist — → readable from the current user's
groups server-side; the current-user presentation passes fallback permissions through
unfiltered, so the client can condition on them too. Section authorization today is one
policy (`Constants.SectionAccessPolicy`, `UBookItSectionHandler`) on the shared controller
base. Settled in explore: three verbs, seed-once over legacy-full-access, settings screen
deferred to 17.1.0.

## Goals / Non-Goals

**Goals:** granularity within the section with the section as the unremovable outer gate;
an upgrade that strips nobody's access; toggles-mean-what-they-say steady state; server as
the single truth with client hiding as convenience.

**Non-Goals:** per-entity grants, settings screen, changes to Sensitive data, any second
section (see proposal).

## Decisions

### D1. Three policies over one handler, layered on the section policy

Verb constants live beside the section constants in `UBookIt.Backoffice`. Three ASP.NET
authorization policies — `UBookIt.Policy.BookingsRead`, `…BookingsManage`, `…Configure` —
each requiring the section requirement AND a verb requirement, evaluated by one
`UBookItVerbHandler` that reads the current backoffice user's groups and takes the union
of their `Permissions`. **Manage implies Read inside the handler's rule for the Read
requirement** (`Read` passes on `UBookIt.Bookings.Read` OR `UBookIt.Bookings.Manage`),
not by duplicating verbs onto groups — one place owns the implication. Actions gain
`[Authorize(Policy = …)]` per verb; the controller base keeps the section policy, so an
action nobody annotates stays section-gated (never anonymous) — fail-closed at the
section, and a totality guard (below) makes "nobody annotated it" a named test failure.

*Alternative considered*: one policy taking the verb as a parameter via requirements per
endpoint — more machinery for the same three policies; rejected.

### D2. Endpoint classification is total and guarded

Every management action carries exactly one verb policy (or an explicit
`[UBookItSectionOnly]`-style marker for anything deliberately verb-free — none is
expected). A reflection guard enumerates every action on the management controller base
and fails on one carrying neither, naming it — the delivery-exposure discipline, applied
to authorization. Classification of today's surface:

- `BookingsRead`: `ListBookings`, `FindBookingsByBooker`*, plus responsibility GETs? No —
  responsibility read/write are configuration: `Configure`.
- `BookingsManage`: `CancelBooking`, `ConfirmBooking`, `DeclineBooking`.
- `Configure`: resource CRUD + types/capabilities lists, service CRUD + preview,
  responsibility GET/PUT.
- *`FindBookingsByBooker` and `EraseBooker` keep their Sensitive-data policy AND gain
  `BookingsRead` — Sensitive data stays the decisive gate for contact details; the read
  verb decides whether bookings are visible at all.

### D3. The seed: one shot, boot-time, marker in a package table

`uBookItFlag` table (Key `nvarchar` PK, `AppliedUtc`) — a general one-shot marker store,
additive migration. A boot handler registered **after** `RunUBookItMigrations` on the
same notification (registration order is execution order for the same notification type
— verified at apply, and the handler tolerates a missing table by logging and retrying
next boot rather than failing): if the `permissions-seed` flag is absent, then for every
user group holding the uBookIt section alias and **zero** verbs starting `UBookIt.`, add
all three verbs via `IUserGroupService`; on full success, write the flag. Partial
failure logs loudly and leaves the flag absent so the next boot retries; the
zero-uBookIt-verbs condition keeps the retry idempotent and keeps the seed's hands off
any group an admin has since edited. Groups without the section are never touched;
Umbraco's built-in admin group is treated like any other (it holds the section only if
a site granted it).

*Alternative considered and rejected in explore*: "no uBookIt verb = legacy full
access" — no writes, but toggling off a group's last verb would silently grant
everything, and the toggles would not mean what they show.

### D4. Client: one manifest entry per verb, one entity type, hide-don't-disable

The existing `umbraco-package` manifest gains three `entityUserPermission` entries under
a single uBookIt entity type, with labels/descriptions localized alongside the section's
strings — the group editor renders them; no custom UI. Our section views read the
current user's fallback permissions from the current-user context: the Bookings view and
its action buttons need Read/Manage, Resources and Services views need Configure; a view
the user cannot use is hidden rather than disabled (a visible-but-dead surface invites
"it's broken" reports). Deep links and direct API calls hit the server policies — the
client is convenience, the server is the truth, and the docs say so.

### D5. Responses: 403, exactly as the section refusal

The verb refusal takes whatever the section policy's authenticated-refusal already
returns (403 via the standard authorization flow) — no distinguishing body. Unlike the
delivery API there is no absence design here: the caller is an authenticated backoffice
user, and "you lack a permission" is not information leakage inside the backoffice.

### D6. Thin end-to-end proof first

Apply task 1 wires ONE verb (`Configure`) through one manifest entry, one policy on one
endpoint, and verifies the toggle appears and the endpoint flips — live — before the
full set is built. The spike verified source, not runtime; this is the runtime check,
early, where a surprise is cheap.

## Risks / Trade-offs

- [Seed ordering vs migrations] → registered after `RunUBookItMigrations`; tolerates a
  missing table (log + retry next boot); flag written only on full success.
- [A future endpoint ships unclassified] → the D2 totality guard names it; the base
  policy keeps it non-anonymous meanwhile.
- [Admin group surprise: a fresh install's admin ticks the section, sees shell, no data]
  → the seed covers groups that held the section BEFORE 0.10.0; for NEW grants the docs
  state "tick the section, then tick what they may do" — and the group editor shows the
  toggles right there. Accepted as the correct steady state.
- [Umbraco changes the fallback-permission pass-through] → the client hiding degrades
  (views hidden or shown wrongly); the server policies are unaffected — the truth
  survives, the convenience wobbles. Live check covers current behaviour.
- [Verb strings are unvalidated server-side by design] → the constants live in one file,
  used by manifest generation guard + policies + seed; a guard asserts the manifest and
  the constants agree, so a typo cannot split the system into two vocabularies.

## Migration Plan

Additive migration (`AddFlags`) for the marker table; the seed performs the data
migration at boot as above. Rollback: the standard additive policy; unseeding is never
done (removing verbs is an admin action).

## Open Questions

None blocking.
