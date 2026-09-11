# Resource / service responsibility

Roadmap **0.8.0**. Decisions below were settled with Chris in the explore on 2026-09-11.

## Why

Internal booking notifications go to one flat list of addresses in configuration
(`BookingNotificationSettings.InternalRecipients`) — every recipient hears about every booking.
The settings type itself records that this is knowingly the wrong shape for a site where
different people are responsible for different resources or services. This change builds the
model of who owns what, so the people responsible for a resource or service are the ones told
when it is booked or a booking on it is cancelled — which is also the population that must act
on a pending booking under 0.6.0's approval flow.

## What Changes

- A resource or a service can be assigned **responsible parties**: backoffice **users** and/or
  Umbraco **user groups**, edited from the existing resource and service editors in the
  backoffice section. Membership is only ever **read** — this decides who is emailed, and is
  explicitly **not** a permissions feature. (A uBookIt-defined group entity, Umbraco
  Workflow-style with a group email address, is deliberately deferred as a possible additive
  later step — see Non-goals.)
- Internal message recipients become the **union of two independent tiers**:
  - the existing flat `InternalRecipients` list — site-wide, sent whenever present, and an
    empty list remains a full opt-out (unchanged, already documented);
  - the responsible parties resolved from the booking — the users assigned to, plus the members
    of the groups assigned to, the booking's service (if any) **or any resource it claims**.
    Union, not override, between service and resource responsibility.
  Recipients are deduplicated by address. Neither tier switches the other off: presence is the
  switch, per tier, matching the existing settings philosophy.
- Resolution happens at send time. Users who are disabled or locked out are skipped, as are
  assignments whose user or group no longer exists; a dangling assignment is surfaced in the
  editor UI rather than hidden, and skipped silently at send.
- New management API endpoints to read and write a resource's or service's responsible parties,
  and to enumerate assignable users/groups as the pickers need — under the existing backoffice
  section authorization.
- New persistence: one additive table mapping subject (resource/service id) to party
  (user/group key), applied by the existing package-private migration pipeline.
- **When mail is sent does not change**: internal messages remain Placed and Cancelled only;
  this change alters who receives them, not when or what. The internal message content is
  unchanged and still structurally carries no booker personal data (the model has no members
  for it), so responsibility assignment never intersects the Sensitive Data control — recorded
  here so review can see it was considered rather than missed.

## Capabilities

### New Capabilities

- `responsibility`: assigning responsible backoffice users and user groups to resources and
  services; storing those assignments; resolving a booking to the responsible parties' email
  addresses (union of service and claimed-resource assignments, skipping disabled and dangling
  accounts); the management endpoints and backoffice editing surface for the assignments.

### Modified Capabilities

- `booking-emails`: the requirement that the site's own people are told ("the booker and the
  site are told independently") changes its recipient definition — from "the configured
  internal recipients" to the two-tier union above. The flat list's presence-is-the-switch
  semantics and the booker-direction rules are unchanged and must be restated in the
  replacement (guarantee-diff discipline applies).
- `persistence`: the schema-shape requirement gains the responsibility assignment table —
  round-trip scenarios, and deletion semantics when the owning resource or service is deleted.

## Impact

- `UBookIt.Core`: little to none — the assignment references backoffice identities, which Core
  cannot know about; the mapping and resolution live in Persistence. (Exact seam decided in
  design.)
- `UBookIt.Persistence`: new entity + additive migration; a resolver used by
  `BookingEmailHandler` when composing the internal send list; Umbraco user/group lookup.
- `UBookIt.Backoffice`: new management endpoints (section-authorized, as all are); resource and
  service editors gain a responsibility panel using the CMS's shipped pickers
  (`umb-user-input`, `umb-user-group-input` in `@umbraco-cms/backoffice`).
- `UBookIt.Web`: no delivery API or rendering changes.
- Docs: `docs/notifications.md` (recipient resolution) and `docs/backoffice.md` (assignment UI).
- No breaking changes to public API surface; migration is additive; upgrading with no
  assignments configured behaves exactly as today.

## Non-goals

- **Not permissions.** Responsibility never grants, denies, or filters anything a user can see
  or do. The permissions model is 0.10.0, gated on its spike.
- **No uBookIt-defined group entity** (Workflow-style groups with a shared group email
  address). Deferred; adding one later as a third assignable party kind would be additive. The
  consequence is accepted: a per-area shared inbox (e.g. `weddings@venue.com`) is not
  expressible in this change except by making it a backoffice user or using the flat list.
- **No change to which events produce internal mail** (Placed and Cancelled only), to message
  content, or to the booker's own messages.
- **No responsibility-based backoffice filtering** ("bookings I am responsible for") — not
  foreseen as wanted; revisit only on demand.
- **No per-assignment notification preferences** (e.g. a user opting out of cancellations).
