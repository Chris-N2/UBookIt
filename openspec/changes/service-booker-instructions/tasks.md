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

- [ ] 8.1 Port the change to `dev/v18` on branch `service-booker-instructions-v18` (worktree `..\uBookIt-v18`). Regenerate the v18 client with its own generator, and confirm the migration is equivalent on both lines (design 8). Verify: the v18 unit, integration, rendering and client suites are green at their baselines plus the new tests.
- [ ] 8.2 Run the upgrade path on real SQL Server: create a database with the 17.x build including this change, then start the 18.x build against it. Verify: the column exists once, existing services read back unchanged, and no migration fails or repeats.

## 9. Verification and handover

- [ ] 9.1 Run the full suites on `main`: unit (2203 baseline), integration (210), rendering (1178), client (356). Verify: green, no skips, counts equal the baselines plus the new tests.
- [ ] 9.2 Run `openspec validate service-booker-instructions --strict` after the last commit. Verify: it passes.
- [ ] 9.3 Live check on the TestSite. Give a service instructions with a URL, book it with auto-confirm on, and read the captured email. Then turn approval on, book again, confirm, and read both emails. Verify: the instructions appear only in the confirmed-booking messages, and the service's public read does not contain them.
- [ ] 9.4 Hand over to a fresh QA subagent (`qa-review`), with build state, test counts, the mutation results and the live check stated as claims to verify.
