# Tasks — resource-responsibility

Working rules carried from previous changes: verify fixes against `HEAD`, not the working
tree; a fix to a named finding prompts "what else is in this class?" and the answer goes in
the commit; build claims come from a `--no-incremental` Release build read against a ZERO
warning baseline; measure text in characters, not bytes; never write prose through a
double-quoted shell string.

## 1. Persistence: table, store, resolver

- [x] 1.1 Add `ResponsibilityRow` to `Entities/Rows.cs` and map it in `UBookItDbContext` as
      `uBookItResponsibility` — four-column compound PK (`SubjectType`, `SubjectId`,
      `PartyType`, `PartyKey`), index on (`SubjectType`, `SubjectId`); discriminators are
      short normalized strings (`resource`/`service`, `user`/`group`).
- [x] 1.2 Add the `AddResponsibility` migration; confirm it is additive and the snapshot
      matches; integration-test the fresh-database and re-run scenarios per the persistence
      delta. *(Migration is one CreateTable + one CreateIndex; the table joined
      `MigrationTests.Fresh_database_has_all_tables`, and the re-run test covers it by
      construction.)*
- [x] 1.3 Responsibility assignment store (public port in Persistence, `SqlResponsibilityStore`
      internal): read by subject; wholesale replace by subject (idempotent — writing the same
      assignment twice stores once); subject-must-exist enforced here so the API and any
      future caller share it. Replace takes the subject's own config app lock, so a replace
      racing the owner's delete serializes — the lock does the job the missing FK would have.
- [x] 1.4 Extend the resource and service delete paths to remove assignment rows in the same
      operation; integration tests per the "do not outlive their subject" requirement,
      including that a recycled id inherits nothing (`Subject_types_do_not_cross_match` covers
      the discriminator half).
- [x] 1.5 `ResponsibleRecipientResolver`: one query for the booking's subjects (service
      attribution + all claims); state rule Active/Inactive/LockedOut in, Disabled/Invited
      out; skip empty addresses; case-insensitive dedup. **Implementation diverged from the
      planned `IUserService.FilterAsync`, deliberately**: FilterAsync requires a requesting
      user (none exists in a background send), applies that user's visibility rules, and
      fails the whole call when any group key is dangling — so groups resolve one by one via
      `IUserGroupService.GetAsync` + `GetAllInGroup`, behind a logic-free
      `IUmbracoUserDirectory` seam (no paging loop exists any more; the planned page-size-1
      test went with it). Unit tests give every state-rule arm its own test with its own
      direction, so flipping any single arm fails a named test; handler mutations (union
      dropped, event gate widened) were run live and caught (3 and 8 failures).

## 2. Sending: the union in BookingEmailHandler

- [x] 2.1 Rework the `toSite` gate: flat list presence OR any assignment rows exist for the
      booking's subjects (cheap indexed existence check), full resolution only after the
      host-can-send check. Preserve and re-state the load-bearing ordering comments
      ("asked before the host is"; a site with nothing configured must not touch the
      database or the mail host).
- [x] 2.2 Send the internal message to `InternalRecipients ∪ resolved`, deduplicated
      case-insensitively; booker direction untouched; events untouched.
- [x] 2.3 Handler-level tests for the delta scenarios: responsibility alone enables the site
      direction; union not precedence; one-person-many-routes gets one message; nothing
      assigned + no list + booker off sends nothing; erased-booker behaviour unchanged with
      resolved recipients. Vary fixtures so no two scenarios pass for the same reason.

## 3. Management API

- [x] 3.1 `ResponsibilityController` on the section-authorized base: GET by subject
      (assignments annotated with display name, exists, user state), PUT wholesale replace
      (404 on missing subject; dangling party keys accepted). Models in
      `Models/ResponsibilityModels.cs`. **Route shape diverged from design D5's
      `/responsibility/{subjectType}/{id}`, deliberately**: nested
      `resources/{id:guid}/responsibility` and `services/{id:guid}/responsibility` match the
      API's existing route family and give the generated client per-subject method names;
      semantics are unchanged. PUT responds with the same annotated read-back a GET returns,
      so the editor holds exactly what a reload would show.
- [x] 3.2 Endpoint tests: anonymous rejected (same terms as every other endpoint — the
      structural base-controller assertion every management controller carries), round-trip,
      dangling-marked GET, missing-subject PUT (both subjects), replace-not-merge,
      unknown-kind 400. The sensitive-data action-classification gate fired and both PUTs
      are recorded as writes in `KnownWrites` and the snapshot, with the decision stated.
- [x] 3.3 Regenerate the typed client (hey-api) and commit the generated files.

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

- [x] 5.1 `docs/notifications.md`: the two-tier model — flat list unchanged and still the
      full opt-out for its tier, responsibility as the targeted tier, union, dedup, the state
      rule, and that group membership is read at send time (the Workflow-documented
      surprise, stated so it is read about rather than discovered).
- [x] 5.2 `docs/backoffice.md`: the assignment UI, and the not-permissions boundary stated
      once, plainly.
- [x] 5.3 Guard the not-permissions claim structurally where the docs state it
      (`DocumentationAssert` on the document as normalised text, not raw substrings — a
      wrapped sentence must not defeat it). Doing so found `DocumentationAssert` narrower
      than its own contract: it promised "quoted" and did not handle backticks, so a claim
      about `InternalRecipients` failed against a document making it verbatim. The backtick
      joined the decoration class and the trim set — stricter for DoesNotSay, and the full
      suite stayed green.

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
