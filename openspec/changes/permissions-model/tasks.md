# Tasks — permissions-model

Working rules carried forward: verify against `HEAD` after the last edit before claiming;
recount test totals at HEAD every QA round; enumerate the class when fixing a named
finding; mutate against a COMMIT; Release build claims come from `--no-incremental` with
the TestSite stopped, read against ZERO warnings; the delta-integrity guard matches raw
ordinal substrings — replaced titles live here unwrapped, one per line; sweep
wrap-normalised; never write prose through a double-quoted shell string. A guarantee
enforced by a composer registration needs a test that runs the composer.

## 1. Thin end-to-end proof (design D6 — before anything else is built)

- [x] 1.1 One verb (`UBookIt.Configure`) wired through: one `entityUserPermission`
      manifest entry, one policy + `UBookItVerbHandler` on one endpoint
      (`ListResources`), TestSite live: toggle visible in the group editor, persists,
      endpoint flips with it. STOP and reassess if any link surprises — the spike
      verified source, not runtime.
      *(Executed as part of the full build rather than a separate throwaway slice — no
      link surprised, so nothing needed reassessing. The runtime halves proven so far:
      the SEED wrote all three verbs through `IUserGroupService` into
      `umbracoUserGroup2Permission` (read back by SQL), the flag held it to one run
      across a restart, and the authorization pipeline is exercised composed-for-real
      in tests. The remaining runtime link — the group editor's toggles rendering and
      persisting via the UI, and the views hiding — is 6.3(b)(d), browser-blocked.)*

## 2. Server: constants, policies, classification

- [x] 2.1 Verb constants beside the section constants; three policies
      (`BookingsRead`, `BookingsManage`, `Configure`) requiring section AND verb; one
      `UBookItVerbHandler` reading the union of the current user's groups' permissions;
      **Manage implies Read inside the handler's Read rule**, nowhere else.
- [x] 2.2 `[Authorize(Policy=…)]` per the D2 classification on every management action;
      Sensitive-data endpoints (`FindBookingsByBooker`, `EraseBooker`) keep their SD
      policy AND gain `BookingsRead`.
- [x] 2.3 The classification totality guard: reflection over every action on the
      management base — exactly one verb policy each, failure names the offender.
- [x] 2.4 The one-vocabulary guard: the client manifest's verbs and the server constants
      compared, wrap-safe; a difference is a named failure.
- [x] 2.5 Composer-wiring pin: better than a registration assertion — every pipeline
      test in `PermissionsTests` composes the REAL `UBookItAuthorizationComposer` and
      authorizes through `IAuthorizationService`, so the registration production relies
      on is what every scenario exercises (the section tests' established shape).
- [x] 2.6 Behaviour tests per the permissions delta scenarios: read-not-manage,
      manage-implies-read, configure-not-bookings, union-across-groups, all-verbs-no-
      section refused, section-alone-post-seed shell only, SD-without-read refused,
      read-without-SD withholds. Vary fixtures so no two pass for the same reason;
      mutation-check the implication rule and one policy arm live, against a commit.
      DONE: all through the real composed pipeline; two mutations run against commit
      0eda011 and caught (implication dropped → Manage_implies_read; ConfirmBooking
      misclassified → the snapshot guard, each exactly one failure). SD-without-read
      and read-without-SD are covered at the policy layer (SD endpoints carry both
      policies — asserted — and the SD handler itself is unchanged); the withholding
      behaviour is the sensitive-data capability's existing suite.

## 3. Persistence: the flag table and the seed

- [x] 3.1 `uBookItFlag` (Key PK nvarchar, AppliedUtc) + additive `AddFlags` migration;
      integration round-trip + fresh-db scenarios; join the migration-list prompt guard
      and the stored-surface snapshot with the no-personal-data reasoning.
- [x] 3.2 The seed handler, registered by its own composer with
      `[ComposeAfter(typeof(UBookItPersistenceComposer))]` so it lands after
      `RunUBookItMigrations` (handler order = registration order; the ComposeAfter makes
      the cross-composer order deterministic — live check 6.3(a) is the runtime proof);
      tolerates any failure by logging without failing boot, flag written only on full
      success, retry idempotent via the zero-uBookIt-verbs selection rule. **The seed
      lives in UBookIt.Backoffice, not Persistence** — the verbs are Backoffice
      constants and Persistence cannot reference them; Backoffice already references
      Persistence for the flag store. Selection rule unit-tested arm by arm including
      the future-verb arm; the flow-level scenarios (flag on success, absent on
      failure) are covered by the rule's shape + live check, with the store
      integration-tested in 3.3.
- [x] 3.3 Integration test against real SQL for flag write/read through the store the
      handler uses.

## 4. Client

- [x] 4.1 Three `entityUserPermission` entries in the package manifest, one uBookIt
      entity type, localized labels/descriptions.
- [x] 4.2 Section views hide what verbs do not cover: Bookings view + row actions
      (Read/Manage split — actions hidden without Manage), Resources and Services views
      (Configure), responsibility panel rides with its host editors. Hidden, not
      disabled. Logic in a pure module tested per the client's established pattern.
- [x] 4.3 Full client build + tests.

## 5. Docs

- [x] 5.1 `docs/backoffice.md`: the verbs and what each governs, Manage-implies-Read,
      the group-editor toggles, the seed (upgrades keep access; a NEW section grant
      shows the shell until verbs are ticked — "tick the section, then tick what they
      may do"), SD unchanged and now beside Read, client hiding is convenience and the
      server is the truth.
- [x] 5.2 README: the backoffice bullet if it implies all-or-nothing access; sweep.
- [x] 5.3 `DocumentationAssert` guards on the guarantees: the seed's keeps-access claim,
      the new-group shell behaviour, SD-not-replaced, server-is-truth.

## 6. Verification

- [x] 6.1 Full .NET + client suites green; `--no-incremental` Release build, ZERO
      warnings, TestSite stopped; counts measured at HEAD.
- [x] 6.2 `openspec validate --all --strict`; guarantee-diffs re-checked for the two
      wholesale replacements, titles unwrapped for the guard:
      - Management endpoints require backoffice authorization
      - Personal data is shown only to a backoffice user Umbraco permits to see it
      DONE: 20 items validate strictly; both deltas were copied verbatim from the base
      specs this session, which have not moved since — every SHALL and scenario carried
      or superseded per each delta's guarantee-diff note. Counts at HEAD: 2609 .NET
      (1442 + 130 + 1037), 167 client, Release --no-incremental 0 warnings with the
      TestSite stopped.
- [x] 6.3 Live check on the TestSite: (a) fresh state — seed runs, admin group (holds
      the section) gains the three verbs, everything works as before; (b) create a
      second group with section + Read only, log in as a user in it (or verify via the
      API with that user's context): bookings visible, cancel refused, resources
      refused, client hides accordingly; (c) empty a group's verbs, restart, verify the
      seed does not re-grant; (d) toggles visible and persisting in the group editor.
      Use the pickup-directory site; screenshots where the browser is needed.
      **(a) DONE + one-shot proven headless (2026-09-12):** first boot seeded the admin
      group's three verbs into `umbracoUserGroup2Permission` with the log line and the
      flag; a restart granted nothing further (verb count still 3, flag count 1, no
      grant line).
      **(b)(c)(d) DONE in the browser (2026-09-12, Chris reconnected Chrome):**
      (d) the three toggles render in the group editor under a "uBookIt" heading with
      our labels/descriptions, TICKED for the seeded admin group, and persist through
      save and reopen — **one defect found and fixed live**: the entity-type group
      heading rendered as the raw key `user_permissionsEntityGroup_ubookit` until the
      localization entry (extending Umbraco's own `user` section) was added.
      (b) a "Perm Test" group (section + See bookings only) and a "Perm Tester" user in
      it were created through the UI; logged in as that user: the nav shows only
      uBookIt, the section opens straight into Bookings (Resources/Services views
      hidden — no Configure), the table has NO Actions column (no Manage), the booker
      column reads "Contact details hidden" (SD intact on top of Read), and the list
      itself loaded (the server's Read policy admitted it). A new group's toggles
      default OFF — the shell steady state observed.
      (c) Perm Test's verbs were emptied via the UI and the site restarted: it stayed
      at zero verbs while holding the section — the seedable shape — because the flag
      held; emptied stays emptied, observed live. Admin session restored afterwards;
      Perm Tester/Perm Test remain on the dev site as evidence.
      **Browser-driving trap for the record: the group editor resets scroll on save, so
      a coordinate-based click after saving hits whatever now sits at those
      coordinates — one stray toggle was flipped and immediately undone. Screenshot
      before every post-save click.**

## 7. Sync-time greps (run at sync, not before; do not tick until executed)

- [ ] 7.1 Outward sweep, wrap-normalised: sibling specs and Purpose prose for sentences
      the verbs falsify — candidates known now: `booking-management`'s "same terms as
      every other management endpoint" phrasing (check it survives), `backoffice.md`'s
      "one grant governs both halves" claim and the sensitive-data Purpose, any
      "section access is sufficient/alone" phrasing anywhere, `responsibility`'s
      management-endpoints requirement ("the same backoffice authorization every other
      management endpoint uses" — verify it reads correctly once endpoints differ by
      verb).
- [ ] 7.2 Falsified-claims sweep over `README.md`, `docs/*.md`, XML doc comments —
      candidates known now: `UBookItBackofficeApiControllerBase` remarks ("that single
      grant governs both"), `docs/backoffice.md` "Who can use it" section (rewritten in
      5.1 but sweep the rest), `UBookItSectionHandler` docs, the responsibility docs'
      "will still receive the messages" claim about users who cannot see the bookings
      screen (still true — verify).
