## 1. Domain

- [x] 1.1 Add `Service.BookerInstructions` and `WithBookerInstructions(string?)`, plus the shared static normalise-and-validate helper (design 1). The helper trims, maps empty or whitespace to `null`, preserves line breaks, and rejects more than 2000 characters with `service-booker-instructions-too-long` and field `bookerInstructions`. Add the code to `FailureCodes`. Verify: unit tests for trim, whitespace-only to null, line breaks kept, 2000 accepted, 2001 rejected, and every other member of the returned service equal to the original.
- [x] 1.2 Extend `PublishedCallShapeTests` with the new member, and confirm `Service.Create`'s shape is still pinned unchanged. Verify: the guard passes. A local mutation adding an optional parameter to `Create` turns it red; revert the mutation afterwards.

## 2. Persistence

- [x] 2.1 Add the nullable instructions property to the service row, and map it through `WithBookerInstructions` in `ServiceRowMapper` on read and from the domain on write. Verify: an integration round-trip of a two-line value in a fresh context, plus `NULL` stored for none.
- [x] 2.2 Scaffold the migration `AddServiceBookerInstructions` (`nvarchar(2000) NULL`, matching the table's column naming taken from the snapshot). Verify: an integration test reads the migration's `UpOperations` and asserts exactly one `AddColumnOperation` on the service table, nullable, max length 2000. A second test upgrades a database seeded with pre-migration services and bookings and finds every existing row unchanged and the instructions `NULL`.

## 3. Management API

- [x] 3.1 Add `bookerInstructions` to `ServiceRequestModel` and `ServiceResponseModel`. The response always carries it, as `null` when there are none. Wire create and update through `WithBookerInstructions`, reporting its failure together with any `Create` failures in one 400 (design 1). Verify: contract tests for round-trip, null-not-omitted, the 2001-character 400 with code and field, a blank name and over-long instructions both reported at once, and removal by null or blank.
- [x] 3.2 Regenerate the backoffice client from the updated OpenAPI document. Verify: the generated types carry `bookerInstructions`, and the client build is clean.

## 4. Backoffice editor

- [x] 4.1 Add the "Instructions for the booker" `uui-textarea` to the Details group of `services-editor.element.ts`, with its hint wired through `aria-describedby` and its failure mapped to the field (design 7). Add the English localization keys. Verify: client tests that the value round-trips through load and save, that line breaks survive, that the field is labelled and described, that the too-long failure renders against it, and that form state is kept on failure.
  *Done, with one deviation from design 7:* a native `<textarea>` labelled by `label[for]`, not
  `uui-textarea`, for the reason the resource editor records for its native checkbox — a uui
  component's inner control is not reliably reachable by an `aria-describedby` on its host, and
  the spec's requirement is that the hint is programmatically associated. The decisions live in
  `booker-instructions-fields.ts` (14 vitest cases); the live DOM check is part of 9.3.
  *Guarantee diff — "Workspace editor for a service" (services), replaced wholesale by the
  MODIFIED delta.* Every SHALL in the main-spec body is carried: the workspace editor with Details,
  Service Requirements and Duration (Details now names the instructions beside the name — a
  superset); saving submits the full service through create or update; success returns to an
  accurate collection view; failure surfaces the codes without losing form state. All three
  scenarios ("Creating a service end-to-end", "Editing an existing service", "Validation failure
  preserves the form") are carried verbatim. Nothing dropped; two scenarios added.

## 5. Messages

- [x] 5.1 In `BookingMessageComposer.ForBookerAsync`, read the service only when the booking is `Confirmed` and has a `ServiceAttribution`, with the cancellation-excluding catch that logs the booking id only (design 2). Add the "Instructions for your booking:" section between the closing line and the cancellation link, normalising line endings, and omit it when there are none (design 3). Verify: composer unit tests for every scenario in the `booking-emails` delta, including placed-confirmed, placed-requested, confirmed, moved-confirmed, moved-requested, declined, cancelled, a direct resource booking, a deleted service, a throwing store, current-not-recorded text, ordering against the cancellation link, and no double spacing from CRLF input.
- [x] 5.2 Add `BookerMessageModel.BookerInstructions` with XML docs in the style of `CancellationUrl`, set from the same value as the wording. Verify: tests that a template receives it for a confirmed booking and that it is null for every other case. A reflection test confirms `InternalMessageModel` has no such member.
- [x] 5.3 Prove the composer guard can fire. Feed the instructions to a `Requested` booking, then to the internal model, and confirm the corresponding tests go red. Revert both mutations.

## 6. Delivery API

- [x] 6.1 Add the wire-level exclusion test (design 6). Create a service with instructions containing a distinctive marker, then GET `/services/{id}` and `/services` and assert the marker is absent from both bodies. Also assert the response members and values are identical to the same service without instructions. Verify: the test passes, and a local mutation copying the instructions into a read-model member turns it red. Revert the mutation.

## 7. Documentation

- [x] 7.1 `docs/notifications.md`: say what the booker's message carries, when instructions are included (confirmed only, with the outcome per message), that they are read at send time, that they never reach internal recipients, and that the field is not a place for secrets. Add `BookerInstructions` to "What your template is given", with the same absence wording as `CancellationUrl`. Verify: each sentence maps to a scenario or a design decision, and the documentation tests pass.
- [x] 7.2 `docs/backoffice.md`: describe the new field in the service editor. Verify: the documentation tests pass.

## 8. The other line

- [x] 8.1 Port the change to `dev/v18` on branch `service-booker-instructions-v18` (worktree `..\uBookIt-v18`). Regenerate the v18 client with its own generator, and confirm the migration is equivalent on both lines (design 8). Verify: the v18 unit, integration, rendering and client suites are green at their baselines plus the new tests.
  *Done:* the three main commits cherry-picked cleanly onto `dev/v18` (`a3d59f9`). The migration is
  the same file under the same id on both lines, and `dotnet ef migrations
  has-pending-model-changes` on v18 reports no change. The v18 generator (reading
  `/umbraco/openapi/ubookitbackoffice.json`) differs only in union order (`null | string`),
  committed separately. `--no-incremental` v18 build 0 warnings. v18 unit **2255** (2197 + 58),
  integration **214**, rendering **1178**, client **370**. Live on the v18 dev site over the wire:
  create round-trips with CRLF, delivery GET and list free of the marker, blank name + 2001
  characters → one 400 with both codes, blank clears to `null`. **v18 dev-site residue:** services
  `SBI v18 check` and `SBI v18 check (2)`. The v18 editor was not driven live; its client source is
  identical to main's apart from the generated union order.
- [x] 8.2 Run the upgrade path on real SQL Server: create a database with the 17.x build including this change, then start the 18.x build against it. Verify: the column exists once, existing services read back unchanged, and no migration fails or repeats.
  *Done on LocalDB, through the package's own startup path (`Database.MigrateAsync`, as `dotnet ef
  database update --connection`):* main's build created the database (18 history rows, including
  `AddServiceBookerInstructions`); two services seeded by SQL, one with CRLF instructions; v18's
  build then reported "No migrations were applied". History 18 → 18, the migration recorded once,
  the column present once (`YES`, 2000), every service row byte-identical before and after, the
  CRLF intact. The scratch database was dropped.

## 9. Verification and handover

- [x] 9.1 Run the full suites on `main`: unit (2203 baseline), integration (210), rendering (1178), client (356). Verify: green, no skips, counts equal the baselines plus the new tests.
  *Done:* `--no-incremental` solution build 0 warnings 0 errors. Unit **2261** (+58), integration
  **214** (+4), rendering **1178** (+0, nothing rendered changed), client **370** (+14); no skips.
  The first unit run had three deliberate decision guards fail — see *Apply record* below.
- [x] 9.2 Run `openspec validate service-booker-instructions --strict` after the last commit. Verify: it passes.
  *Done:* CI's own command, `npx @fission-ai/openspec@1.13.0 validate --all --strict`, on both
  lines after their last commits: 27 passed, 0 failed (26 specs + this change).
- [x] 9.3 Live check on the TestSite. Give a service instructions with a URL, book it with auto-confirm on, and read the captured email. Then turn approval on, book again, confirm, and read both emails. Verify: the instructions appear only in the confirmed-booking messages, and the service's public read does not contain them.
  *Done (dev site, `.eml` pickup directory, new files only).* Service `SBI Online consultation`
  (`4e527609-…`, type `room`) with a marker `SBI-LIVE-4c2e`. **The TestSite references
  `UBookIt.Tests.ThemeFixture`, which supplies `BookerPlaced`/`Confirmed`/`Moved` views**, so the
  first run proved only the narrowing (no supplied view rendered the member). For the second run
  the fixture was changed locally and restored from git afterwards: `BookerPlaced`/`BookerMoved`
  moved aside so the package's own wording rendered, `BookerConfirmed` given a line printing
  `Model.BookerInstructions`. Results: placed with auto-confirm (`K6ZB-XQSK`) — package wording
  carries "Instructions for your booking:" after the closing line, lines intact; approval on
  (`NHGC-6ZKT`) — placement and a move while Requested carry none; confirm — the supplied view
  received the text; move while Confirmed — package wording carries it. Internal messages: none.
  No cancellation link appeared (the dev site cannot resolve its own address), so ordering
  against the link is evidenced by the unit test only. Delivery `GET /services/{id}` and the list:
  marker absent. Management API: `bookerInstructions: null` present for a service without;
  blank name + 2001 characters → one 400 carrying both codes. `AutoConfirm` overridden for the run
  and reset (effective `true`, not overridden). **Editor, live shadow DOM:** native textarea in
  Details, `label[for]` in the same root, `aria-describedby` → hint (resolves); after a 2001-char
  save, `aria-invalid="true"`, describedby grows to `…-hint err-booker-instructions`, both
  resolve, summary names the failure, no Details group error, value and name kept; a two-line
  save round-trips (read back over the API, LF line break).
- [x] 9.4 Hand over to a fresh QA subagent (`qa-review`), with build state, test counts, the mutation results and the live check stated as claims to verify.

## Apply record

**Decisions the artifacts did not settle (for QA and Chris):**

1. **`BookingMessageComposer` gained a constructor rather than a widened one.** Design 2 says the
   composer reads the service; it does not say how it gets a store, and the class's three-parameter
   constructor is published. The new one is `(IResourceStore, ILogger, IBookingTemplateRenderer?,
   IServiceStore)` — store LAST and required, so `(r, l)`, `(r, l, t)` and `(r, null, null)` bind
   only to the published one. The persistence composer registers it through an explicit factory:
   left to the container, a site with no renderer registered would get the published constructor
   and lose the instructions silently. A composer built through the published constructor carries
   none. Pinned by `PublishedSignatureTests`, `PublishedCallShapeTests` and
   `ServiceBookerInstructionsMessageTests.The_package_registers_…`.
2. **`SensitiveDataRedactionTests` was narrowed by an exact exemption.** Its name filter treats any
   endpoint parameter containing "booker" as a contact detail and demands the sensitive-data gate;
   `bookerInstructions` is the spec's own wire name and is site configuration. The exemption names
   the two service writes exactly, fails if an entry goes stale, and refuses any action not
   recorded as a write. Mutation: a `BookerNote` property on the same request model still turns
   the guard red. Its storage-surface twin records the new column with its decision.
3. **Native `<textarea>`, not `uui-textarea`** — see 4.1.
4. **The column is `BookerInstructions`** (PascalCase like the table's other columns; design 5
   left the casing to apply).

**Mutations (each reverted, revert confirmed):** `Service.Create` gains an optional parameter →
signature guard red; status check removed from the composer → 6 red; `InternalMessageModel`
member → reflection test red; instructions in the site body → site test red; DI back to
`AddScoped<BookingMessageComposer>()` → seam test red; delivery `Name` += instructions → both
delivery tests red; migration gains an `UPDATE` → shape test and upgrade test red; docs label ≠
`en-us.ts` → docs test red.

**QA round 1 (fresh subagent): APPROVE.** No critical or major finding; 31/33 spec criteria
verified (the two editor scenarios rest on the live shadow-DOM check, which QA could not repeat
without a login). Follow-ups:
- *MINOR, fixed:* the line-ending test fed CRLF only, so on Windows removing `ReplaceLineEndings()`
  stayed green (and real textarea input is LF). Now a theory over CRLF, LF and mixed input, which
  also asserts every `\n` in the body belongs to an `Environment.NewLine` (meaningful only where the
  newline is CRLF; on Linux the `Contains` and `\r\r` checks carry it) and that the model carries
  the text unnormalised. Mutation re-run on Windows: removing the normalisation turns the LF and
  mixed cases red. Unit main **2263**, v18 **2257**.
- *MINOR, accepted:* the null-not-omitted and delivery tests serialise with
  `JsonSerializerOptions.Web`, not the host's pipeline; the live checks (9.3, 8.1) read the real
  wire bodies.
- *For the sync:* `email-templates` "a site that supplies nothing SHALL be unaffected … exactly what
  it sent before" is false as written for a site that writes instructions but supplies no views —
  restate it as "unaffected by this capability" (the cancellation link set the same precedent).
- *For the release notes:* a PUT that omits `bookerInstructions` clears them, so a backoffice tab
  left open across the upgrade, or a script on the old generated types, wipes a service's
  instructions when it saves.
- *Fixed after round 2:* "Added in 17.5.0" read on the v18 line as though 18.0–18.3 had the member;
  it now says "Added in 17.5.0 and 18.4.0" (17.1.0 for `CancellationUrl` predates 18.0, so that
  precedent did not carry over).
- *Left:* the exemption matches the bare identifier `BookerInstructions` under the
  two named actions only; the generated response type marks the member optional (the wire always
  carries it).

**At sync — guarantee diff for "A site can supply the content of a message without owning its
delivery" (email-templates), replaced wholesale by a MODIFIED delta added for QA's finding.**
Carried verbatim: the SHALL to replace the body while the package decides whether, to whom and
under what configuration; the SHALL that content and delivery be separable; the scenario "Content is
replaced without delivery being replaced". Deliberately narrowed (stated in the proposal): "a site
that supplies nothing SHALL be unaffected … exactly what it sent before this capability existed"
becomes "unaffected *by this capability* … exactly the package's own message for that event", and
the scenario "A site that supplies nothing is unchanged" is reworded to match. Nothing dropped.

**QA round 2: APPROVE** — the line-ending fix verified (the length check proven to fire on
Windows independently of `Contains`); the three items above were tidied without re-review.

**Outward grep** *(the email-templates half is superseded by the QA record above: the sentence is
false as written and is restated at sync)*: the composer's own remark ("the one exception is…") was falsified and is
corrected. `email-templates` "a site that supplies nothing SHALL be unaffected … exactly what it
sent before" compares a site with and without supplied content and is still true (a site that
writes no instructions sends what it sent) — left, to be re-checked at sync.
