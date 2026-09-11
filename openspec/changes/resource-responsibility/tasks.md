# Tasks — resource-responsibility

Working rules carried from previous changes: verify fixes against `HEAD`, not the working
tree; a fix to a named finding prompts "what else is in this class?" and the answer goes in
the commit; build claims come from a `--no-incremental` Release build read against a ZERO
warning baseline; measure text in characters, not bytes; never write prose through a
double-quoted shell string.

## 1. Persistence: table, store, resolver

- [ ] 1.1 Add `ResponsibilityRow` to `Entities/Rows.cs` and map it in `UBookItDbContext` as
      `uBookItResponsibility` — four-column compound PK (`SubjectType`, `SubjectId`,
      `PartyType`, `PartyKey`), index on (`SubjectType`, `SubjectId`); discriminators are
      short normalized strings (`resource`/`service`, `user`/`group`).
- [ ] 1.2 Add the `AddResponsibility` migration; confirm it is additive and the snapshot
      matches; integration-test the fresh-database and re-run scenarios per the persistence
      delta.
- [ ] 1.3 Responsibility assignment store (internal, Persistence): read by subject; wholesale
      replace by subject (idempotent — writing the same assignment twice stores once);
      subject-must-exist enforced here so the API and any future caller share it.
- [ ] 1.4 Extend the resource and service delete paths to remove assignment rows in the same
      operation; integration tests per the "do not outlive their subject" requirement,
      including that a recycled id inherits nothing.
- [ ] 1.5 `ResponsibleRecipientResolver`: one query for the booking's subjects (service
      attribution + all claims), user resolution via `IUserService.GetAsync`, group members
      via `FilterAsync` with `IncludedUserGroups` + allowed states, **paged to exhaustion**;
      state rule Active/Inactive/LockedOut in, Disabled/Invited out; skip empty addresses;
      case-insensitive dedup. Unit-test with a fake user service, including a page-size-1
      fake proving the paging loop, and a mutation check on the state rule (each excluded
      state individually — enumerate the class, not one sample).

## 2. Sending: the union in BookingEmailHandler

- [ ] 2.1 Rework the `toSite` gate: flat list presence OR any assignment rows exist for the
      booking's subjects (cheap indexed existence check), full resolution only after the
      host-can-send check. Preserve and re-state the load-bearing ordering comments
      ("asked before the host is"; a site with nothing configured must not touch the
      database or the mail host).
- [ ] 2.2 Send the internal message to `InternalRecipients ∪ resolved`, deduplicated
      case-insensitively; booker direction untouched; events untouched.
- [ ] 2.3 Handler-level tests for the delta scenarios: responsibility alone enables the site
      direction; union not precedence; one-person-many-routes gets one message; nothing
      assigned + no list + booker off sends nothing; erased-booker behaviour unchanged with
      resolved recipients. Vary fixtures so no two scenarios pass for the same reason.

## 3. Management API

- [ ] 3.1 `ResponsibilityController` on the section-authorized base: GET by subject
      (assignments annotated with display name, exists, user state), PUT wholesale replace
      (404 on missing subject; dangling party keys accepted). Models in
      `Models/ResponsibilityModels.cs`.
- [ ] 3.2 Endpoint tests: anonymous rejected (same terms as every other endpoint — reuse the
      existing authorization test pattern), round-trip, dangling-marked GET, missing-subject
      PUT, replace-not-merge.
- [ ] 3.3 Regenerate the typed client (hey-api) and commit the generated files.

## 4. Backoffice client

- [ ] 4.1 Responsibility panel in `resource-editor.element.ts` and
      `services-editor.element.ts` (dashboard views, not workspaces): `<umb-user-input>` +
      `<umb-user-group-input>` bound to the assignment set, loaded on open, saved with the
      editor's save. Shared element if the two editors can host one cleanly; duplication is
      acceptable over a premature abstraction.
- [ ] 4.2 Mark dangling/skipped-state assignments visibly (deleted, disabled, invited) using
      the GET annotations; they must not silently vanish from the display.
- [ ] 4.3 The not-permissions sentence in the panel copy, localized like the section's other
      strings. Labelling around the pickers uses real labels (uui-label is not a label; no
      uui-* control carries aria-describedby — see the bookings-screen handover).
- [ ] 4.4 Client tests for the panel's states; full client build + test run.

## 5. Docs

- [ ] 5.1 `docs/notifications.md`: the two-tier model — flat list unchanged and still the
      full opt-out for its tier, responsibility as the targeted tier, union, dedup, the state
      rule, and that group membership is read at send time (the Workflow-documented
      surprise, stated so it is read about rather than discovered).
- [ ] 5.2 `docs/backoffice.md`: the assignment UI, and the not-permissions boundary stated
      once, plainly.
- [ ] 5.3 Guard the not-permissions claim structurally where the docs state it
      (`DocumentationAssert` on the document as normalised text, not raw substrings — a
      wrapped sentence must not defeat it).

## 6. Verification

- [ ] 6.1 Full .NET suite + client tests green; `--no-incremental` Release build read
      against the ZERO-warning baseline, output not piped through anything that can eat the
      warnings line.
- [ ] 6.2 `openspec validate --strict` across all specs; delta guarantee-diff re-checked
      against `openspec/specs/booking-emails/spec.md` as it stands at apply time, for both
      wholesale replacements — "The package sends nothing until a site asks it to" and
      "The booker and the site are told independently" — every SHALL and scenario carried
      or superseded, none dropped.
- [ ] 6.3 Live check on the TestSite: assign a user to a resource in the backoffice, place a
      booking, confirm the `.eml` set in the pickup directory (clear it first; the `Date:`
      header is authoritative, not mtime): user's address present, flat list still present,
      dedup correct. Repeat with the user disabled → address absent.

## 7. Sync-time greps (run at sync, not before; do not tick until executed)

- [ ] 7.1 Outward sweep: grep sibling specs and Purpose prose for sentences this change
      falsifies — candidates known now: any statement that internal recipients are "the
      configured list", that configuration alone decides sending, or counts of
      customisation tiers; also `booking-emails` Purpose lines 19/422 region re-read in
      full.
- [ ] 7.2 Falsified-claims sweep over `README.md`, `docs/*.md` and XML doc comments —
      including `BookingNotificationSettings.InternalRecipients`' remark that a flat list
      is "knowingly the wrong shape", which this change answers and should now say so.
