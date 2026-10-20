## 1. Domain

- [ ] 1.1 Add to `Service` (design 2): `TemplateSet`, `WithTemplateSet(string?)`, the static `NormalizeTemplateSet` and `IsValidTemplateSetName`, `TemplateSetMaxLength = 64`, and `TemplateSetField = "templateSet"`. Add `FailureCodes.ServiceTemplateSetInvalid`. `WithBookerInstructions` and `WithTemplateSet` each preserve the other's value. Verify with unit tests for:
  - trim; whitespace-only becomes `null`; `Remote-Consult` becomes `remote-consult`;
  - accepted: `remote`, `in-person`, `a`, `a1-b2`, and 64 characters;
  - rejected: 65 characters, `../partials`, `a/b`, `a\b`, `.`, `in person`, `-remote`, `remote-`, `re--mote`, `café`;
  - `IsValidTemplateSetName` agreeing with `NormalizeTemplateSet` on every one of those inputs, both accepted and rejected (design 2; the renderer relies on the two agreeing);
  - every other member of a returned service equal to the original, chained in both orders with the instructions.
- [ ] 1.2 Extend `PublishedCallShapeTests` with every new public member on `Service`, `BookingMessageModel` and `RazorBookingTemplateRenderer`. Confirm the shapes of `Service.Create`, `IBookingTemplateRenderer.RenderAsync`, `PathFor`, `IsSupplied` and the boot check's `Run` are still pinned unchanged. Verify: the guard passes. A local mutation adding an overload `PathFor(string, BookingMessageKind)` turns it red; revert the mutation afterwards.

## 2. Persistence

- [ ] 2.1 Add the nullable `TemplateSet` property to the service row. `ToDomain` maps it through `NormalizeTemplateSet`, giving `null` on failure and never `.Value` on a failed result (design 6); `ApplyScalars` writes it. Verify with integration tests:
  - `remote` round-trips in a fresh context;
  - `NULL` is stored for none;
  - a raw-SQL ` Remote ` reads back as `remote`;
  - a raw-SQL `../x` reads back as no set, and the service still loads with its other members intact.
- [ ] 2.2 Scaffold the migration `AddServiceTemplateSet` (`nvarchar(64) NULL`, no index, with the column casing taken from the snapshot). Verify with integration tests:
  - the migration's `UpOperations` are exactly one nullable `AddColumnOperation` on the service table, with max length 64;
  - an upgrade of a database seeded with pre-migration services and bookings leaves every existing row unchanged and the set `NULL`.

## 3. Management API

- [ ] 3.1 Add `templateSet` to `ServiceRequestModel` and `ServiceResponseModel`, always present in the response. Wire create and update through `NormalizeTemplateSet`, so its failure is reported in one 400 with any `Create` or instructions failures (design 7). Verify with contract tests for every scenario in the `services` delta's "A service may name an email template set":
  - the round-trip;
  - lower-casing and trimming;
  - null rather than an omitted member;
  - each rejected input, with its code and field;
  - 64 accepted and 65 rejected;
  - a blank name and `../x` reported together;
  - removal by null or blank;
  - two services sharing a set.
- [ ] 3.2 Regenerate the backoffice client. Verify: the generated types carry `templateSet`, and the client build is clean.

## 4. Backoffice editor

- [ ] 4.1 Add the **Email template set** field to the Details group of the service editor, after the instructions (design 7). It is a native `<input>` with `label[for]`, and its hint is wired through `aria-describedby`. A `templateSet` failure gets `aria-invalid` and its error id added to `aria-describedby`. After a successful save, the form refreshes from the response. Add the English localization keys. Verify with client tests that:
  - the value round-trips through load and save;
  - `Remote` displays as `remote` after a save;
  - the field is labelled and described;
  - the invalid-set failure renders against this field only;
  - form state is kept on failure.

  Then write the guarantee diff for "Workspace editor for a service" into this task. The mechanical diff run at propose found all 5 scenarios and every SHALL carried, with the Details list a superset. Re-run it against the delta as it stands at apply.

## 5. Messages

- [ ] 5.1 Replace `BookerInstructionsAsync` with the single `ReadServiceAsync` returning *not read*, *read* or *failed* (design 3), and call it from **both** `ForBookerAsync` and `ForSiteAsync`. The instructions rule is unchanged. On *failed*, return the composer's own wording without calling the renderer, logging once with the booking id only. Verify with composer unit tests for every scenario in the `booking-emails` delta. Include both "cannot be read" scenarios, with and without a renderer and a supplied view, and assert that the renderer was **not** called on *failed*.
- [ ] 5.2 Add `ServiceId` and `TemplateSet` to `BookingMessageModel`, with XML docs in the style of `CancellationUrl` stating what absence means and recommending `TemplateSet` over `ServiceId` for branching. Set both on both models. Verify with tests for every scenario in the `email-templates` delta's "Both models identify the service and its template set": a service booking with a set; one without; a direct booking; a deleted service (id kept, set absent); the internal model carrying both and still no contact details (by reflection).
- [ ] 5.3 Add the service-read cost theory over audience × shape × renderer × status, with a counting service store, total dispatch and a subject assertion (design 3, the table). Add a 4-parameter-constructor variant of the existing resource-read theory. Verify: green. Then run three mutations and revert each:
  - make `ForSiteAsync` skip the read: the theory goes red;
  - read twice in `ForBookerAsync`: the theory goes red;
  - read for a `Requested` booker with no renderer: the theory goes red.
- [ ] 5.4 A 17.0.0-constructor composer (no service store) with a renderer: verify `TemplateSet` is null, `ServiceId` is set, the renderer is called, and nothing is logged as a failure (design 3, last bullet).

## 6. Renderer

- [ ] 6.1 Implement the chain in `RazorBookingTemplateRenderer.RenderAsync` (design 4), plus `SetPathFor` and `IsSuppliedInSet`. Verify with rendering tests, using real views in a test assembly, for every scenario in "A service's template set is consulted before the content for every service":
  - the set view is used;
  - a kind missing from the set uses the view for every service;
  - a kind supplied nowhere is `NotSupplied`;
  - a throwing set view is `Failed` even though a view for every service exists;
  - the site's own audience is resolved in a set;
  - an invalid set on the model (`../partials`, `Remote`) is `Failed` with no lookup;
  - a set in an RCL assembly is found.
- [ ] 6.2 Prove the stop-on-failure rule can fire: mutate the renderer to fall through to the view for every service on a set failure, and confirm the throwing-set-view test goes red. Revert the mutation.

## 7. Startup report

- [ ] 7.1 Add `UBookItEmailTemplateSetBootCheck` (design 5) and register it beside the existing check. Verify with tests for:
  - a set with some kinds supplied (Information, listing both halves);
  - a set supplying nothing (Warning naming the folder and the lower-case rule);
  - a set shared by two services, reported once;
  - no sets (Debug only);
  - a throwing service store (one Warning, no exception);
  - a runtime level other than `Run` (no store read at all);
  - the existing check's output unchanged.

## 8. Delivery API

- [ ] 8.1 Add the wire-level exclusion test (design 8), with a marker set name absent from the `GET /services/{id}` and `GET /services` bodies, and the members and values identical to the same service without a set. Verify: green. A local mutation copying the set into a read-model member turns it red; revert the mutation.

## 9. Documentation

- [ ] 9.1 `docs/notifications.md`: add the "Template sets" section (design 9). Add `ServiceId` and `TemplateSet` to "What your template is given". Verify: each sentence maps to a scenario or a design decision, and the documentation tests pass.
- [ ] 9.2 `docs/backoffice.md`: describe the field. Verify: the documentation tests pass.

## 10. The other line

- [ ] 10.1 Port the change to `dev/v18` on branch `service-email-templates-v18`. Regenerate the v18 client with its own generator, and confirm the migration is equivalent on both lines (design 10). Verify: the v18 unit, integration, rendering and client suites are green at their baselines plus the new tests, and the build is clean with `--no-incremental`.
- [ ] 10.2 Run the upgrade path through the package's startup path: create a database with the 17.x build including this change, then start the 18.x build against it. Verify: the column exists once, existing services read back unchanged, and no migration fails or repeats.

## 11. Verification and handover

- [ ] 11.1 Run the full suites on `main` against the baselines: unit 2263, integration 214, rendering 1178, client 370. Verify: green, no skips, counts equal the baselines plus the new tests, and a `--no-incremental` build has 0 warnings.
- [ ] 11.2 Live check on the TestSite. **The TestSite references `UBookIt.Tests.ThemeFixture`, which supplies `BookerPlaced`/`Confirmed`/`Moved` views**, so set views are added locally and removed afterwards, and touched after restoring (see the `service-booker-instructions` handover).
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
- [ ] 11.3 At sync: run the outward grep of sibling specs for sentences this change falsifies. Candidates: "one per message", "one view per message", "published path", "PathFor", "cost the instructions and nothing else", "reads the service", "service read", and the `email-templates` Purpose's "one per message". Then re-run the guarantee diff for both MODIFIED requirements. Verify: every hit is resolved or recorded, here, with its reason.
- [ ] 11.4 Run CI's own command, `npx @fission-ai/openspec@1.13.0 validate --all --strict`, after the last commit on each line. Verify: it passes.
