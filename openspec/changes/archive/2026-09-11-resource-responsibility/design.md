# Design — resource / service responsibility

## Context

Internal booking mail today: `BookingEmailHandler` (Persistence) sends `InternalPlaced` /
`InternalCancelled` to the flat `BookingNotificationSettings.InternalRecipients` list, gated by
presence-is-the-switch and `CanSendRequiredEmail()`. A booking carries `Claims` (resource ids,
always at least one) and `Service` (nullable `ServiceAttribution`), so every booking can be
mapped to the things it touches. The backoffice edits resources and services in **dashboard
views inside the uBookIt section** (`resources-view` / `services-view` hosting list + editor
elements) — there are no Umbraco workspaces to extend. The CMS client we already depend on
(`@umbraco-cms/backoffice` ^17.5.x) ships `<umb-user-input>` and `<umb-user-group-input>`
picker elements.

Settled with Chris (2026-09-11): assignable parties are backoffice users and/or Umbraco user
groups (no uBookIt group entity); recipients are the union of the flat list and the resolved
responsible parties; events, message content and the no-PII guarantee are unchanged.

## Goals / Non-Goals

**Goals:**

- Per-resource and per-service responsibility, edited where resources and services are edited.
- Send-time resolution to email addresses that fails soft: a dangling or disabled assignment
  is skipped, never a thrown fault in the mail path.
- Fully additive: schema, API surface, and behaviour for a site that configures nothing.

**Non-Goals:** (from the proposal, restated for this document's scope)

- No permissions semantics of any kind; no responsibility-based filtering of backoffice data.
- No uBookIt-defined groups, group email addresses, or per-assignment preferences.
- No change to when internal mail is sent or what it says.

## Decisions

### D1. The mapping is a Persistence concern; Core stays out entirely

A responsible party is a backoffice identity, which `UBookIt.Core` (zero references) cannot
represent meaningfully. The assignment store, the resolver, and the Umbraco user lookup all
live in `UBookIt.Persistence`, which already references `Umbraco.Cms.Core` (it hosts
`BookingEmailHandler` and the notification handlers today) — `IUserService` is reachable there.

*Alternative considered*: an opaque `ResponsibleParties` value on the Core `Resource`/`Service`
aggregates (kind + Guid pairs). Rejected: it would touch `Resource.Create`/`Service.Create`,
every rehydration path and their tests, to give Core a value it can never interpret or
validate — pure freight. The domain aggregates do not know they are watched, exactly as they
do not know they are emailed about.

### D2. Storage: one table, subject + party, both polymorphic by discriminator

New row `ResponsibilityRow` → table `uBookItResponsibility`:

| column | meaning |
|---|---|
| `SubjectType` (string, `resource`/`service`) | what the assignment is on |
| `SubjectId` (Guid) | the resource or service id |
| `PartyType` (string, `user`/`group`) | what is assigned |
| `PartyKey` (Guid) | the Umbraco user key or user group key |

Primary key: the four columns compound (an assignment either exists or does not — no identity,
no payload, idempotent writes). Index on (`SubjectType`, `SubjectId`) for the editor read and
the resolver. **No FK to the resource/service tables**: FKs cannot span to Umbraco's user
tables anyway (different schema ownership, and group keys live in Umbraco's storage), so
symmetry — plus the delete rule below — argues for none at all.

Deleting a resource or service deletes its assignment rows in the same operation (store-level,
mirroring how capability rows go with their owner). A dangling **party** (deleted user/group)
is expected and handled at read time; a dangling **subject** is not allowed to persist.

*Alternative considered*: two tables (`uBookItResourceResponsibility`,
`uBookItServiceResponsibility`) with real FKs to their owners. Workable, but doubles the
store, the entity set and the migration for no query we run — nothing ever joins across
subject types, and the resolver always queries by explicit (`SubjectType`, id-list) pairs, so
the discriminator can never mix subjects accidentally. Revisit only if a real FK need appears.

### D3. Resolution: a resolver beside the email handler, one Umbraco query per send

New `ResponsibleRecipientResolver` (Persistence, internal):

1. Read assignment rows for `SubjectType = service, SubjectId = booking.Service.ServiceId`
   (when attributed) and `SubjectType = resource, SubjectId IN booking.Claims` — one query.
2. Split into user keys and group keys.
3. Resolve users: `IUserService.GetAsync(key)` per key (the set is small); keep those whose
   state allows sending (D4) and whose email is non-empty.
4. Resolve group members: `IUserService.FilterAsync` with
   `UserFilter { IncludedUserGroups = groupKeys, IncludeUserStates = allowed states }` — one
   paged call, page size generous and iterated to exhaustion so a large group is not silently
   truncated.
5. Return distinct addresses (case-insensitive — SMTP local-part case sensitivity is a
   theoretical nicety; a person assigned twice must not be mailed twice).

`BookingEmailHandler` then sends the internal message to
`InternalRecipients ∪ resolved`, deduplicated the same way. The **gate changes**: today
`toSite` requires `HasInternalRecipients`; it becomes "the union is non-empty", evaluated
before composing. Everything else in the handler — ordering, the booker direction, the events
— is untouched.

A dangling party key (user/group no longer exists) resolves to nothing, silently, by
construction: `GetAsync` misses and `FilterAsync` matches nobody. No log noise at send time —
the editor UI is where staleness is surfaced (D6).

*Alternative considered*: resolving at assignment time (storing addresses). Rejected outright:
addresses go stale invisibly, and it would copy a fact Umbraco owns.

### D4. Which user states receive mail

Send to **Active**, **Inactive** (created, never yet logged in — a real colleague with a real
inbox), and **LockedOut** (transient, expires; a lockout must not cost the site a booking
notification). Skip **Disabled** (an administrator ended their access deliberately) and
**Invited** (no accepted account; the address may never have been verified). This is a
spec-level rule, stated in the `responsibility` spec so it cannot drift.

### D5. Management API: its own endpoints, `resource-management` untouched

On the existing section-authorized controller base:

- `GET  /responsibility/{subjectType}/{id}` → the assignments, each annotated with what it
  currently resolves to: display name, and `exists`/`state` so the client can mark dangling
  or disabled parties. Returns assignments for a subject even if the subject itself has been
  deleted concurrently (the client simply won't ask).
- `PUT  /responsibility/{subjectType}/{id}` → replace the subject's assignment set wholesale
  (the same replace-not-merge shape resource capabilities use). Validates subject exists and
  party keys are well-formed Guids; does **not** reject dangling party keys on write — a save
  that races a user deletion must not fail the whole editor save.

Picker enumeration needs no new endpoints: `umb-user-input` / `umb-user-group-input` talk to
Umbraco's own management APIs with the operator's session.

*Alternative considered*: folding assignments into the resource/service update payloads.
Rejected: it would modify `resource-management`'s CRUD requirement (a wholesale MODIFIED
replacement with all its guarantee-diff risk) for no gain, and couple two saves that have
different failure modes.

### D6. Backoffice client: a responsibility panel in both editors

`resource-editor.element.ts` and `services-editor.element.ts` each gain a "Responsibility"
box containing `<umb-user-input>` and `<umb-user-group-input>` bound to the assignment sets,
loaded/saved through the D5 endpoints via the generated hey-api client. A party the GET marks
as dangling or disabled renders with an explicit tag (e.g. "no longer exists" / "disabled")
rather than disappearing — an operator must be able to see that Studio 2's contact went away.
Copy states the purpose plainly: *"who is emailed about bookings — this does not grant or
restrict access."*

Accessibility: the panel follows the editors' existing patterns; the uui traps from the
bookings-screen change apply (uui-label is not a label; no uui-* control carries
aria-describedby) — the pickers are CMS-owned composites, but any labelling we add around
them must be real labels.

### D7. Documentation

`docs/notifications.md` gains the two-tier recipient model (flat list unchanged as the
site-wide tier, empty list still a full opt-out; responsibility as the targeted tier; union,
dedup, skipped states). `docs/backoffice.md` gains the assignment UI. Both state the
not-permissions boundary once, plainly.

## Risks / Trade-offs

- [Group membership is read at send time, so adding someone to a group changes who is mailed
  with no uBookIt action] → intended behaviour, but it is the Workflow-documented surprise;
  the docs state it explicitly so it is a feature read about, not discovered.
- [`FilterAsync` paging: a naive single page silently truncates a big group] → iterate to
  exhaustion; test with a page-size-1 fake to prove the loop, not the happy path.
- [The `toSite` gate moving from `HasInternalRecipients` to "union non-empty" touches the
  handler's short-circuit ordering — the settings-only fast path must not start querying the
  database on sites with nothing configured] → resolve lazily: check the flat list and the
  existence of any assignment rows for the booking's subjects before user resolution; the
  no-assignments case costs one indexed query only when the flat list alone is empty… and
  even that only after the host-can-send check, preserving today's "asked before the host is"
  order for the directions that are configured. The exact short-circuit order is a design
  point the implementation must state and test, because the current ordering comments are
  load-bearing.
- [An operator assumes responsibility grants backoffice access ("I assigned her, why can't
  she see the section?")] → the not-permissions sentence in the editor copy and both docs;
  nothing else can fix a naming problem.
- [Emailing group members individually differs from Workflow's group-inbox model, which some
  users may expect] → recorded as an explicit non-goal with the workaround (flat list, or a
  backoffice user for the shared inbox); additive upgrade path exists.
- [The boot check reports "sending asked for on a host that cannot send" from configuration
  only, so an assignments-only site with broken mail gets no boot warning] → deliberate, found
  at apply time: assignments are runtime data, editable at any moment after boot, so boot is
  not the diagnosable moment for that tier — and the check may run before migrations have
  created the table on a fresh install. The boundary is stated in `docs/notifications.md`
  rather than half-fixed with a query that races the migration pipeline.

## Migration Plan

One additive EF migration (`AddResponsibility`) creating `uBookItResponsibility`, applied by
the existing package-private startup pipeline. No data migration: existing sites have no
assignments and behave identically. Rollback = the standard one for this package: the table
is ignored by older code and additive-only policy means we never drop it.

## Open Questions

None blocking. One deliberate deferral: whether `Invited` users should be offered by the
picker at all (Umbraco's picker shows them; we skip them at send). Accepted as a minor
inconsistency — hiding them would mean forking the picker, which costs more than the
confusion it prevents; the GET annotation marks their state in the panel instead.
