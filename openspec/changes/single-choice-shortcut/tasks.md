## 1. The setting (Core + Persistence)

- [ ] 1.1 Add `SkipSingleChoiceCatalogue` (`bool`, `init`, default `true`) to `SiteBookingSettings`, with remarks on its meaning and fallback direction. Verify: builds with 0 warnings, and no existing constructor or `with` usage changes.
- [ ] 1.2 Add `SkipSingleChoiceCatalogueSettingKey = "UBookIt:SkipSingleChoiceCatalogue"`, plus `IsSkipSingleChoiceCatalogueConfigured` (presence) and `ResolveSkipSingleChoiceCatalogue` (absent or unreadable → true), to `UBookItPersistenceComposer`, and set the property in `ResolveSettings`. Verify: unit tests for absent → on, `"false"` → off, `"true"` → on, `"banana"` → on, each from configuration AND from the store via `EffectiveConfiguration`.
- [ ] 1.3 Add `ErrorIfSkipSingleChoiceCatalogueUnreadable` to `RunUBookItMigrations.Report`. Verify: tests that an unreadable value logs one error naming the key, and that an absent or readable value logs nothing (persistence delta scenarios).

## 2. The backoffice

- [ ] 2.1 Add the `SettingCatalogue.All` entry (Editable, Boolean, operator block) and the `SettingText` mapping. Verify: `SettingCatalogueTests` and `SettingsControllerTests` cover the key as editable and not restart-bound; a write by a verb holder is accepted and stored; a non-boolean value is refused by the existing validator.
- [ ] 2.2 Add the `skipSingleChoiceCatalogueLabel` and `skipSingleChoiceCatalogueDescription` terms to `en-us.ts` (copy in design D5). Verify: `npm run build` and `npm test` pass (baseline 355 + new). The existing term-key and manifest guards pass, and are confirmed to cover the new key.
- [ ] 2.3 Live check on the TestSite (v17): the settings tab shows the setting as an editable checkbox with its configured and effective values. Toggling it, then reloading the booking page, changes what renders without a restart. Record this by screenshot or description in the handover.

## 3. The front end

- [ ] 3.1 Add the internal pure rule (design D1, e.g. `SoleChoice(CatalogueModel, bool)`) beside `FlowEntry`. Verify: unit tests for 0, 1 service, 1 resource and 2 entries, each with the setting on and off. Only "1 entry + on" yields a subject.
- [ ] 3.2 Add the second public constructor taking `SiteBookingSettings`, marked `[ActivatorUtilitiesConstructor]`, keeping the existing one (it behaves as on). Add the existing constructor's shape to `PublishedCallShapeTests`. Verify: build, and the call-shape test passes.
- [ ] 3.3 In `InvokeAsync`'s no-subject branch, after `BuildAsync`, dispatch to the sole entry's flow with its token via `ServiceAsync` / `ResourceAsync` when the rule yields a subject. Change nothing else. Verify: 3.4.
- [ ] 3.4 Rendering tests through the real component and host (`UBookIt.Tests.Rendering`). Each scenario in the `default-frontend` delta needs at least one test:
  - one service, setting on: service date step, `<h2>` names the service, no catalogue markup;
  - one resource, setting on: resource step;
  - one service plus one non-direct resource: service flow;
  - one service plus one direct resource: catalogue with both;
  - zero entries: the empty catalogue;
  - one entry, setting **off**: catalogue (this also proves the activator chose the new constructor, design D4);
  - an author-named subject and a `ubBook` subject, with the setting on and off: unchanged;
  - the skipped flow's date-step form and POST carry the subject token;
  - a skipped flow's confirmation is reached through the real PRG round trip;
  - a sole entry with no availability: its flow's own explanation.

  Verify: all pass. **Mutate the dispatcher** (always skip; never skip; skip on `>= 1`; drop the token) and confirm that each mutation fails at least one test. Revert after.
- [ ] 3.5 Live check on the TestSite (v17) with the settings store and catalogue arranged for each case: one entry (skips), two entries (catalogue), setting off (catalogue). Complete a booking through the skipped flow and confirm the confirmation page and its URL.

## 4. Docs

- [ ] 4.1 `docs/booking-page.md`: the "With nothing else specified the page shows the catalogue" paragraph and its URL table state the single-choice skip and the setting that turns it off. Verify by rereading against the delta.
- [ ] 4.2 `docs/configuration.md`: add the key to the *Editable in the backoffice* table and the fallback table ("on", "error, if something was written"). Reread line 26 ("behaves exactly as it did before") and qualify it if this release makes it misleading. Verify: any doc/README guard tests (pinned sentences, link guards) pass.
- [ ] 4.3 `docs/theming.md`: one sentence saying that a themed `Catalogue` view is not rendered for a catalogue of one while the setting is on. Delivery API docs (`docs/delivery-api.md`): one sentence saying the skip is a front-end behaviour, and that a headless client decides for itself. Verify by rereading.
- [ ] 4.4 Falsified-sentence sweep: grep `openspec/specs`, `docs`, `README.md` and the XML comments in `src/UBookIt.Web` for sentences that assume the bare booking page always shows the catalogue (`catalogue`, `list of one`, `nothing else specified`), including `BookingFlowViewComponent`'s own class remarks. Fix each one, or record why it still holds. List the results in the handover.

## 5. Validate and QA (v17)

- [ ] 5.1 Full verification on `main`'s branch: client build, then `dotnet build` (Release, 0 warnings), unit (baseline 2131), integration (210), rendering (1178), client (355). Report the new counts. Run `openspec validate single-choice-shortcut --strict` and CI's exact openspec command after the last commit.
- [ ] 5.2 QA review (`qa-review`) in a fresh subagent, reused across rounds. Hand it the claims (build state, counts, live checks) to verify, not to trust. Treat each round's fixes as new code.

## 6. Port to dev/v18

- [ ] 6.1 After v17 QA approval, cherry-pick onto a branch from `dev/v18` in worktree `..\uBookIt-v18`. Resolve any line differences, and run the generators (client build) against a running v18 TestSite. Verify: the same suites at the v18 baselines (unit 2143, integration 210, rendering 1178, client 355) plus new tests, 0 warnings, and the live checks 2.3 and 3.5 repeated on v18.
- [ ] 6.2 QA review of the v18 branch: the same reviewer if it is still in this session, otherwise a fresh one told it is a port.

## 7. Release obligations (recorded for the release change, not done here)

- [ ] 7.1 Record in the deferred-obligations memory what the `17.4.0` / `18.3.0` changelog entry must say. Under *What you have to do*: "a site whose booking page offers exactly one service or resource now goes straight to it; to keep showing the catalogue, untick *Skip the catalogue when there is only one choice* on the settings tab, or set `UBookIt:SkipSingleChoiceCatalogue` to `false`". Also note that Chris's own site needs it off before or at upgrade. Verify: the memory entry exists.
