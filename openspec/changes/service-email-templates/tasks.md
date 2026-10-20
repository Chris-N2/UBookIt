## 1. Domain

- [x] 1.1 Add to `Service` (design 2): `TemplateSet`, `WithTemplateSet(string?)`, the static `NormalizeTemplateSet` and `IsValidTemplateSetName`, `TemplateSetMaxLength = 64`, and `TemplateSetField = "templateSet"`. Add `FailureCodes.ServiceTemplateSetInvalid`. `WithBookerInstructions` and `WithTemplateSet` each preserve the other's value. Verify with unit tests for:
  - trim; whitespace-only becomes `null`; `Remote-Consult` becomes `remote-consult`;
  - accepted: `remote`, `in-person`, `a`, `a1-b2`, and 64 characters;
  - rejected: 65 characters, `../partials`, `a/b`, `a\b`, `.`, `in person`, `-remote`, `remote-`, `re--mote`, `café`;
  - `IsValidTemplateSetName` agreeing with `NormalizeTemplateSet` on every one of those inputs, both accepted and rejected (design 2; the renderer relies on the two agreeing);
  - every other member of a returned service equal to the original, chained in both orders with the instructions.
- [x] 1.2 Extend `PublishedCallShapeTests` with every new public member on `Service`, `BookingMessageModel` and `RazorBookingTemplateRenderer`. Confirm the shapes of `Service.Create`, `IBookingTemplateRenderer.RenderAsync`, `PathFor`, `IsSupplied` and the boot check's `Run` are still pinned unchanged. Verify: the guard passes. A local mutation adding an overload `PathFor(string, BookingMessageKind)` turns it red; revert the mutation afterwards.

  **Apply record:** three call-shape tests (`null` literals, an initializer with and without the new model members, method-group conversions) plus `Each_published_name_has_exactly_one_overload` over 13 names. `PublishedSignatureTests` gains `The_renderer_seam_keeps_its_published_signatures` (the port's `RenderAsync`, `PathFor`, `IsSupplied`, and `Run`'s return type, all by exact types). The mutation turned exactly the overload-count case for `PathFor` red. **Worth knowing:** the method-group shape `Func<BookingMessageKind, string> f = PathFor` still compiled with the overload present, so the call shapes alone could not have seen it. The overload count is what does.

## 2. Persistence

- [x] 2.1 Add the nullable `TemplateSet` property to the service row. `ToDomain` maps it through `NormalizeTemplateSet`, giving `null` on failure and never `.Value` on a failed result (design 6); `ApplyScalars` writes it. Verify with integration tests:
  - `remote` round-trips in a fresh context;
  - `NULL` is stored for none;
  - a raw-SQL ` Remote ` reads back as `remote`;
  - a raw-SQL `../x` reads back as no set, and the service still loads with its other members intact.
- [x] 2.2 Scaffold the migration `AddServiceTemplateSet` (`nvarchar(64) NULL`, no index, with the column casing taken from the snapshot). Verify with integration tests:
  - the migration's `UpOperations` are exactly one nullable `AddColumnOperation` on the service table, with max length 64;
  - an upgrade of a database seeded with pre-migration services and bookings leaves every existing row unchanged and the set `NULL`.

## 3. Management API

- [x] 3.1 Add `templateSet` to `ServiceRequestModel` and `ServiceResponseModel`, always present in the response. Wire create and update through `NormalizeTemplateSet`, so its failure is reported in one 400 with any `Create` or instructions failures (design 7). Verify with contract tests for every scenario in the `services` delta's "A service may name an email template set":
  - the round-trip;
  - lower-casing and trimming;
  - null rather than an omitted member;
  - each rejected input, with its code and field;
  - 64 accepted and 65 rejected;
  - a blank name and `../x` reported together;
  - removal by null or blank;
  - two services sharing a set.
- [x] 3.2 Regenerate the backoffice client. Verify: the generated types carry `templateSet`, and the client build is clean.

  **Apply record:** hand-edited first to break the build cycle (`templateSet?: string | null` on both service models), then regenerated with `npm run generate-client` against the running TestSite. The output was identical to the hand edit apart from line endings (`git diff --ignore-all-space` empty), so the committed copy was kept. The client builds and tsc is clean.

## 4. Backoffice editor

- [x] 4.1 Add the **Email template set** field to the Details group of the service editor, after the instructions (design 7). It is a native `<input>` with `label[for]`, and its hint is wired through `aria-describedby`. A `templateSet` failure gets `aria-invalid` and its error id added to `aria-describedby`. After a successful save, the form refreshes from the response. Add the English localization keys. Verify with client tests that:
  - the value round-trips through load and save;
  - `Remote` displays as `remote` after a save;
  - the field is labelled and described;
  - the invalid-set failure renders against this field only;
  - form state is kept on failure.

  Then write the guarantee diff for "Workspace editor for a service" into this task. The mechanical diff run at propose found all 5 scenarios and every SHALL carried, with the Details list a superset. Re-run it against the delta as it stands at apply.

  **Apply record (guarantee diff, re-run mechanically):** all 5 scenarios of `openspec/specs/services` carried by heading; 5 of 6 SHALL sentences carried verbatim; the sixth (the Details list) is a superset, adding `email template set`. The script's self-test (dropping one scenario from the new list) reported the drop. **One tension recorded for review:** the editor closes on a successful save (`ubookit-saved` returns to the collection view — a carried SHALL), so the new scenario's 'straight after the save' is visible only where the editor stays open: a failed responsibility-panel save with the service already saved. The state IS refreshed from the response in every successful save before closing (design 7), and reopening shows the stored name.

## 5. Messages

- [x] 5.1 Replace `BookerInstructionsAsync` with the single `ReadServiceAsync` returning *not read*, *read* or *failed* (design 3), and call it from **both** `ForBookerAsync` and `ForSiteAsync`. The instructions rule is unchanged. On *failed*, return the composer's own wording without calling the renderer, logging once with the booking id only. Verify with composer unit tests for every scenario in the `booking-emails` delta. Include both "cannot be read" scenarios, with and without a renderer and a supplied view, and assert that the renderer was **not** called on *failed*.
- [x] 5.2 Add `ServiceId` and `TemplateSet` to `BookingMessageModel`, with XML docs in the style of `CancellationUrl` stating what absence means and recommending `TemplateSet` over `ServiceId` for branching. Set both on both models. Verify with tests for every scenario in the `email-templates` delta's "Both models identify the service and its template set": a service booking with a set; one without; a direct booking; a deleted service (id kept, set absent); the internal model carrying both and still no contact details (by reflection).
- [x] 5.3 Add the service-read cost theory over audience × shape × renderer × status, with a counting service store, total dispatch and a subject assertion (design 3, the table). Add a 4-parameter-constructor variant of the existing resource-read theory. Verify: green. Then run three mutations and revert each:
  - make `ForSiteAsync` skip the read: the theory goes red;
  - read twice in `ForBookerAsync`: the theory goes red;
  - read for a `Requested` booker with no renderer: the theory goes red.
- [x] 5.4 A 17.0.0-constructor composer (no service store) with a renderer: verify `TemplateSet` is null, `ServiceId` is set, the renderer is called, and nothing is logged as a failure (design 3, last bullet).

  **Apply record (5.1–5.4):** `ServiceTemplateSetMessageTests` (35 cases) plus a 4-parameter variant of the resource-read theory in `BookingTemplateCompositionTests`. Two existing tests in `ServiceBookerInstructionsMessageTests` encoded the read rule this change modifies and were moved onto the **no-renderer** path, which is where their scenarios now live: "A service that cannot be read costs the instructions alone" (the delta's scenario now says "on a site that supplies no content") and "The service is not read for a message that could not carry them" (design 3's table: booker/service/none/Requested → 0). Their with-renderer counterparts are new tests. Two guards were extended deliberately: `PermittedInternalMembers` gains `ServiceId` and `TemplateSet` (site configuration, nothing about the person), and the durable-storage surface records `ServiceRow.TemplateSet` with its decision. The failed-read log line now reads "…sent in uBookIt's own wording, without anything read from the service", which is true for both audiences. Mutations, each reverted by an edit: (1) `ForSiteAsync` skipping the read: 6 red, including both site/renderer theory cells; (2) a second read in `ForBookerAsync`: 5 red, including three theory cells and the logged-once assertion; (3) reading for a `Requested` booker with no renderer: the theory cell and the instructions suite's no-read test red; (4) a failed read still calling the renderer: both booker cells of the unreadable-service theory red; (5) the instructions' status check dropped (now that a renderer reads for every status): 5 instructions tests red.

## 6. Renderer

- [x] 6.1 Implement the chain in `RazorBookingTemplateRenderer.RenderAsync` (design 4), plus `SetPathFor` and `IsSuppliedInSet`. Verify with rendering tests, using real views in a test assembly, for every scenario in "A service's template set is consulted before the content for every service":
  - the set view is used;
  - a kind missing from the set uses the view for every service;
  - a kind supplied nowhere is `NotSupplied`;
  - a throwing set view is `Failed` even though a view for every service exists;
  - the site's own audience is resolved in a set;
  - an invalid set on the model (`../partials`, `Remote`) is `Failed` with no lookup;
  - a set in an RCL assembly is found.
- [x] 6.2 Prove the stop-on-failure rule can fire: mutate the renderer to fall through to the view for every service on a set failure, and confirm the throwing-set-view test goes red. Revert the mutation.

  **Apply record (6.1–6.2):** `EmailTemplateSetRenderingTests` (20 cases) against set `fixture-set` in `UBookIt.Tests.ThemeFixture` (`BookerPlaced`, `InternalPlaced`, and a throwing `BookerMoved` beside the working general one; no `BookerConfirmed`). The fixture is a compiled assembly over an empty content root, so every set view found is found inside an RCL. `ThemeFixtureTests`' exact view list gains the three set views deliberately. The set name `fixture-set` cannot collide with the live check's `remote`, which matters because the TestSite references the fixture. The existing rig was made `internal` and given an optional view-engine wrapper and logger, so both suites share one provider shape. Mutations, each reverted: (a) falling through on a set failure turned the throwing-set-view test red; (b) dropping the name re-check (and building the path by hand) turned all 7 invalid-name cases red.

## 7. Startup report

- [x] 7.1 Add `UBookItEmailTemplateSetBootCheck` (design 5) and register it beside the existing check. Verify with tests for:
  - a set with some kinds supplied (Information, listing both halves);
  - a set supplying nothing (Warning naming the folder and the lower-case rule);
  - a set shared by two services, reported once;
  - no sets (Debug only);
  - a throwing service store (one Warning, no exception);
  - a runtime level other than `Run` (no store read at all);
  - the existing check's output unchanged.

  **Apply record (7.1):** `EmailTemplateSetBootCheckTests` (13 cases, rendering project, real fixture views), plus paging (451 services, the only set on page 3) and ordinal order. The registration is asserted in `BookingTemplateCompositionTests.The_web_composer_registers_the_renderer_and_the_boot_check`. **Ordering, recorded for review:** `RunUBookItMigrations` handles the same notification, and `UBookIt.Web` references only Core, so it cannot `[ComposeAfter]` the persistence composer (and the repo avoids ordering assumptions on principle). If the set check ran first on the boot after a package upgrade, its read would fail on the missing column. That is caught, as the spec's "Unreadable services do not fail startup" requires, and the warning says sets are checked again at the next start. The order actually observed is recorded under 11.2.

- [x] 7.2 *(Added at apply, Chris's decision 2026-10-20; design 5's amendment.)* Run the set check after uBookIt's migrations: add `IUBookItStorageReadyHandler` to Core, have `RunUBookItMigrations` call every registered one after migrating (each caught and logged), and register the set check as one instead of as a started-notification handler. Verify with an integration test through `RunUBookItMigrations.HandleAsync` on a database stopped one migration short, where a handler reads services (selecting the new column), a second throws, and a third still runs. Mutate by notifying before migrating: it must turn red.

  **Apply record:** `StorageReadyHandlerTests` (integration, 1). The mutation (notifying before migrating) turned it red: the first handler's read failed on the missing column. The registration test now asserts the service type is `IUBookItStorageReadyHandler`, that there is no other registration, and that the runner's constructor takes `IEnumerable<IUBookItStorageReadyHandler>`, which is the seam's other half. `OnStorageReadyAsync` joins the overload-count pin. `StartupReportTests` and `EffectiveConfigurationTests` pass `[]` for the new internal constructor parameter. A failed migration rethrows before the handlers, by construction; that has no test, because forcing EF Core's migrator to fail mid-run without hanging on a connection timeout is not cheap. **Live re-check:** see 11.2's addendum.

## 8. Delivery API

- [x] 8.1 Add the wire-level exclusion test (design 8), with a marker set name absent from the `GET /services/{id}` and `GET /services` bodies, and the members and values identical to the same service without a set. Verify: green. A local mutation copying the set into a read-model member turns it red; revert the mutation.

  **Apply record:** `ServiceTemplateSetDeliveryTests` (2), marker `zz-marker-set-7q`. Mutation: `DeliveryModelMapper` appending the set to the published `Name` turned both red; reverted.

## 9. Documentation

- [x] 9.1 `docs/notifications.md`: add the "Template sets" section (design 9). Add `ServiceId` and `TemplateSet` to "What your template is given". Verify: each sentence maps to a scenario or a design decision, and the documentation tests pass.
- [x] 9.2 `docs/backoffice.md`: describe the field. Verify: the documentation tests pass.

## 10. The other line

- [x] 10.1 Port the change to `dev/v18` on branch `service-email-templates-v18`. Regenerate the v18 client with its own generator, and confirm the migration is equivalent on both lines (design 10). Verify: the v18 unit, integration, rendering and client suites are green at their baselines plus the new tests, and the build is clean with `--no-incremental`.

  **Apply record:** branch `service-email-templates-v18` in `..\uBookIt-v18`, from `dev/v18` `efcd199`, with the three commits cherry-picked. The only conflict was `types.gen.ts`, resolved in v18's generator style (`templateSet?: null | string`). Regenerating against the running v18 TestSite (`/umbraco/openapi/ubookitbackoffice.json`) gave identical output apart from line endings. The `Migrations` folder, snapshot included, has no diff against main's change branch, and a probe `migrations add` on v18 came out with empty `Up`/`Down` (removed). Clean Release `--no-incremental` build, 0 warnings. Unit **2414** = 2257 + 157, integration **221**, rendering **1211**, client **387**, no skips. The v18 dev database's own first boot then applied `AddServiceTemplateSet`, and the set check ran after it with no error: the 7.2 fix live on a second real database.
- [x] 10.2 Run the upgrade path through the package's startup path: create a database with the 17.x build including this change, then start the 18.x build against it. Verify: the column exists once, existing services read back unchanged, and no migration fails or repeats.

  **Apply record:** scratch LocalDB database `uBookItSetUpgrade`. Main's TestSite installed Umbraco and applied all 19 uBookIt migrations unattended, with no error from the set check (its read would have met a missing table before 7.2). The database was then rolled back one migration (`dotnet ef database update AddServiceBookerInstructions`) and a service seeded by SQL. **Booting main again with the column pending logged "applied 1 database migration(s): …AddServiceTemplateSet" and THEN the set check's Debug line, with no error: the live re-check of 7.2 on an upgrade boot.** The service was given set `remote`. **Starting the v18 TestSite against it failed in Umbraco's own core upgrade** (`V_18_0_0.AddElements.EnsureAdminGroupElementPermissions`, a bulk insert hitting an execution timeout), twice, before any uBookIt code ran. That is an Umbraco 17→18 core-upgrade problem on this scratch install, outside this change. So, as in service-booker-instructions 8.2, v18's own EF migrator was run against the 17.x database (`dotnet ef database update --connection`): "No migrations were applied". History 19 → 19 with `AddServiceTemplateSet` once, the column present once (`YES`, 64), and the service row byte-identical before and after (`remote` and the instructions intact). The scratch database was dropped.

## 11. Verification and handover

- [x] 11.1 Run the full suites on `main` against the baselines: unit 2263, integration 214, rendering 1178, client 370. Verify: green, no skips, counts equal the baselines plus the new tests, and a `--no-incremental` build has 0 warnings.

  **Apply record (main, after 7.2):** clean Release `--no-incremental` build, 0 warnings, 0 errors. Unit **2420** = 2263 + 157: 156 counted by a filter over the new tests at `ab70072`, plus 1 overload-pin case in 7.2. Integration **221** = 214 + 7 (6 store/migration + 1 storage-ready). Rendering **1211** = 1178 + 33. Client **387** = 370 + 17. No skips. `openspec validate --all --strict` (1.13.0): 27 passed.
- [x] 11.2 Live check on the TestSite. **The TestSite references `UBookIt.Tests.ThemeFixture`, which supplies `BookerPlaced`/`Confirmed`/`Moved` views**, so set views are added locally and removed afterwards, and touched after restoring (see the `service-booker-instructions` handover).
  1. Give a service the set `Remote`, and confirm the editor shows `remote` after saving.
  2. Add `Emails/remote/BookerPlaced.cshtml` and `Emails/remote/InternalPlaced.cshtml`, then restart.
  3. Read the startup log for the set report.
  4. Book the service and read the captured `.eml` files.
  5. Make the set view throw, book again, and read the captured messages.
  6. Name a set `remte`, restart, and read the warning.

  Verify:
  - the set content arrives for both audiences;
  - the throwing view yields uBookIt's own wording, not the fixture's general view, and the failure is logged;
  - the warning names the folder;
  - the delivery read carries no set.

  Record the dev-site residue.

  **Apply record (11.2, 2026-10-20, main):** All verified. (1) API: `Remote` stored as `remote`; a blank name plus `../partials` came back as one 400 with both codes. Editor, read from the shadow DOM: the field sits after the instructions, `label[for]` reads "Email template set", and the hint id resolves. A real `../x` save put `aria-invalid` on this field only, grew `aria-describedby` to `hint err-template-set` (both resolve), left the instructions field untouched, and kept every value. Typing `Remote` and saving closed the editor; reopening showed `remote`. (3) The startup report listed `remote` with `BookerPlaced, InternalPlaced` supplied and the other five not. (4) Booking `XW5R-CQ3J`: both `.eml` files carried the set views' subjects and bodies, with set `remote`, the service id and the instructions on the model. (5) With the set's `BookerPlaced` throwing, booking `CQ9G-8999`: the booker got "Your booking is confirmed" in uBookIt's own wording, instructions included, and **not** the fixture's general "Dear …" view. The internal message still came from the set. The render failure was logged naming `remote/BookerPlaced.cshtml`, and no booker detail appeared in the log. (6) Set renamed to `remte`, then a restart: the warning named `remte`, `~/Views/Partials/UBookIt/Emails/remte/` and the lower-case rule. The delivery `GET /services/{id}` and `GET /services` bodies contain neither `remte` nor `templateSet`. **Ordering, observed:** on the first boot (the column still pending), the set check ran **before** `RunUBookItMigrations` and logged two EF Core database errors plus its warning; the migration then applied. Chris chose a migrations-done notification (task 7.2). **Dev-site residue:** service `SET Online consultation` (`125a0394-…`, set `remote`, with instructions); bookings `XW5R-CQ3J` and `CQ9G-8999` (26 Oct 2026, 09:00 and 11:00). The disk views were removed, and the pickup folder was restored to its earlier 51 entries (this run's four `.eml` files are kept in the session scratchpad).

  **Addendum (after 7.2):** On a normal boot with the hook in place, the set report still fires, now called by the migration runner. With the views removed, it correctly warned about `remote`. The residue service's set was then cleared to `null`, so the dev site does not warn on every start; its instructions remain. The upgrade boot itself (column pending) is covered by `StorageReadyHandlerTests` and exercised live by 10.2. The dev database could not be rolled back without reading its secret connection string.
- [ ] 11.3 At sync: run the outward grep of sibling specs for sentences this change falsifies. Candidates: "one per message", "one view per message", "published path", "PathFor", "cost the instructions and nothing else", "reads the service", "service read", and the `email-templates` Purpose's "one per message". Then re-run the guarantee diff for both MODIFIED requirements: `booking-emails`' "A confirmed booker is given the service's instructions" and `services`' "Workspace editor for a service". Verify: every hit is resolved or recorded, here, with its reason.
- [x] 11.4 Run CI's own command, `npx @fission-ai/openspec@1.13.0 validate --all --strict`, after the last commit on each line. Verify: it passes.

  **Apply record:** 27 passed, 0 failed on both lines, after the apply's last commit and again after the commit recording this. **Re-run at sync and archive**, which add commits of their own.
