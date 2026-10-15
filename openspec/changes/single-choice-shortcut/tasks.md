## 1. The setting (Core + Persistence)

- [x] 1.1 Add `SkipSingleChoiceCatalogue` (`bool`, `init`, default `true`) to `SiteBookingSettings`, with remarks on its meaning and fallback direction. Verify: builds with 0 warnings, and no existing constructor or `with` usage changes.
- [x] 1.2 Add `SkipSingleChoiceCatalogueSettingKey = "UBookIt:SkipSingleChoiceCatalogue"`, plus `IsSkipSingleChoiceCatalogueConfigured` (presence) and `ResolveSkipSingleChoiceCatalogue` (absent or unreadable → true), to `UBookItPersistenceComposer`, and set the property in `ResolveSettings`. Verify: unit tests for absent → on, `"false"` → off, `"true"` → on, `"banana"` → on, each from configuration AND from the store via `EffectiveConfiguration`.
- [x] 1.3 Add `ErrorIfSkipSingleChoiceCatalogueUnreadable` to `RunUBookItMigrations.Report`. Verify: tests that an unreadable value logs one error naming the key, and that an absent or readable value logs nothing (persistence delta scenarios).

## 2. The backoffice

- [x] 2.1 Add the `SettingCatalogue.All` entry (Editable, Boolean, operator block) and the `SettingText` mapping. Verify: `SettingCatalogueTests` and `SettingsControllerTests` cover the key as editable and not restart-bound; a write by a verb holder is accepted and stored; a non-boolean value is refused by the existing validator.
- [x] 2.2 Add the `skipSingleChoiceCatalogueLabel` and `skipSingleChoiceCatalogueDescription` terms to `en-us.ts` (copy in design D5). Verify: `npm run build` and `npm test` pass (baseline 355 + new). The existing term-key and manifest guards pass, and are confirmed to cover the new key.
- [x] 2.3 Live check on the TestSite (v17): the settings tab shows the setting as an editable checkbox with its configured and effective values. Toggling it, then reloading the booking page, changes what renders without a restart. Record this by screenshot or description in the handover. **Done 2026-10-15**, on a fresh database `UBookIt_SingleResourceTest` (unattended install). The setting appears after *Privacy policy link* with its label and description, ticked by default, and not marked as needing a restart. Unticking it stored the value and showed "Overriding — the site's configuration sets no value for this" with *Reset to configured value*; the next request rendered the catalogue of one. Reset ticked it again.

## 3. The front end

- [x] 3.1 Add the internal pure rule (design D1, e.g. `SoleChoice(CatalogueModel, bool)`) beside `FlowEntry`. Verify: unit tests for 0, 1 service, 1 resource and 2 entries, each with the setting on and off. Only "1 entry + on" yields a subject.
- [x] 3.2 Add the second public constructor taking `SiteBookingSettings`, marked `[ActivatorUtilitiesConstructor]`, keeping the existing one (it behaves as on). Add the existing constructor's shape to `PublishedCallShapeTests`. Verify: build, and the call-shape test passes.
- [x] 3.3 In `InvokeAsync`'s no-subject branch, after `BuildAsync`, dispatch to the sole entry's flow with its token via `ServiceAsync` / `ResourceAsync` when the rule yields a subject. Change nothing else. Verify: 3.4.
- [x] 3.4 Component tests through the real component (`UBookIt.Tests/SingleChoiceDispatchTests.cs`). **Changed during apply:** the rendering rig renders views, never a component, and its project cannot reach the in-memory stores. So these drive `BookingFlow`, activated by MVC's own `IViewComponentActivator` over the real flows and catalogue, and assert the view chosen and its model. That the views render `<h2>Book …</h2>` and the hidden `Subject` field is already the rendering suite's to prove, and it does. The PRG step is followed through `BookingFlowLink.AfterSubmission` with the token from the model. Each scenario in the `default-frontend` delta needs at least one test:
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

  Verify: all pass. **Mutate the dispatcher** (always skip; never skip; skip on `>= 1`; drop the token) and confirm that each mutation fails at least one test. Revert after. **Done:** six mutations, each caught. The failure counts were: no `[ActivatorUtilitiesConstructor]` 16 (activation throws on the ambiguous constructors); always skip 2; never skip 11; `>= 1` 3; drop the token 4; dispatcher ignores the setting 9. All were reverted, and the diff stat was unchanged afterwards.
- [x] 3.5 Live check on the TestSite (v17) with the settings store and catalogue arranged for each case: one entry (skips), two entries (catalogue), setting off (catalogue). Complete a booking through the skipped flow and confirm the confirmation page and its URL. **Done 2026-10-15** on the fresh database. Setup: one service "Massage" (1 × room, 60 min) and "Treatment Room" (service only), with a published Booking Page.
  - **One entry, setting on:** the bare `/book/` rendered "Book Massage" with no catalogue. The date form carried `ubBook=s:…` as a hidden field; the next step was `/book/?ubBook=s%3A…&ubDate=2026-10-16`, and the POST carried the same `Subject`.
  - **Booking:** "Booking confirmed", reference SKZT-CJJM (Massage, Treatment Room, Fri 16 Oct 09:00–10:00). The redirect target was `/?ubBook=s%3A…&ubDate=2026-10-16&ubMins=60`; the page is the only root node, so Umbraco serves it at `/`.
  - **Setting off (stored):** "Choose what to book" offering Massage alone, with no restart. This is also live proof that Umbraco activates the settings constructor, because the published four-argument one would have skipped.
  - **Two entries** (Treatment Room made bookable on its own), **setting on:** the catalogue offered Massage and Treatment Room.
  - The one-resource case was not run live. It is covered by `SingleChoiceDispatchTests`, and it will be the v18 port's live case on Chris's own site's database.

## 4. Docs

- [x] 4.1 `docs/booking-page.md`: the "With nothing else specified the page shows the catalogue" paragraph and its URL table state the single-choice skip and the setting that turns it off. Verify by rereading against the delta.
- [x] 4.2 `docs/configuration.md`: add the key to the *Editable in the backoffice* table and the fallback table ("on", "error, if something was written"). Reread line 26 ("behaves exactly as it did before") and qualify it if this release makes it misleading. Verify: any doc/README guard tests (pinned sentences, link guards) pass.
- [x] 4.3 `docs/theming.md`: one sentence saying that a themed `Catalogue` view is not rendered for a catalogue of one while the setting is on. Delivery API docs (`docs/delivery-api.md`): one sentence saying the skip is a front-end behaviour, and that a headless client decides for itself. Verify by rereading.
- [x] 4.4 Falsified-sentence sweep: grep `openspec/specs`, `docs`, `README.md` and the XML comments in `src/UBookIt.Web` for sentences that assume the bare booking page always shows the catalogue (`catalogue`, `list of one`, `nothing else specified`), including `BookingFlowViewComponent`'s own class remarks. Fix each one, or record why it still holds. List the results in the handover.

- [x] 4.4a **Sweep result, recorded.** Fixed: `FlowEntry.NamedByAuthor`'s remarks; `BookingFlowViewComponent`'s class remarks; the `/book` line in `booking-page.md`. **Found during the live check, missed by the sweep** (which did not search `package.xml`): the Booking Page document type's description said only "or a single service or resource when the URL names one". Corrected in `src/UBookIt.Web/Packaging/package.xml`. This is safe because `BookingPagePackageMigrationPlan` is a custom plan with a fixed state id, so the manifest never re-imports on an existing site. New installs get the new text, and existing installs keep the old description, which is now incomplete but not false about anything they can do. Everything else the grep found is either inside the requirement being modified or still true (the catalogue's accessibility, its GET form, `README.md`'s "the whole catalogue, one service, or one resource").
- [x] 4.5 Guarantee diff for every MODIFIED requirement, checked against `openspec/specs/` as it stands:

  **default-frontend — "A site may enter the flow at any point"**
  - SHALL let an author start at the catalogue, a service or a resource: carried forward word for word.
  - SHALL NOT require the catalogue to be traversed first: carried forward word for word.
  - Each entry point independently renderable, and the same flow from that point on: carried forward word for word.
  - Scenario *A site starts at one service*: carried forward word for word.
  - Scenario *A site starts at the catalogue*: **deliberately narrowed** to "two or more choices, or the skip setting off". The proposal states this narrowing; with one choice and the setting on, the visitor is taken to that choice, which is the point of the change.
  - Scenario *The entry point does not change what follows*: carried forward word for word. It is strengthened for the skip by *A skipped catalogue is the same flow as a chosen one*.
  - Nothing is dropped.

  **site-settings — "Settings are tiered, and the tier is enforced by the server"**
  - The tier list: carried forward, with `UBookIt:SkipSingleChoiceCatalogue` added to Editable. Every other key keeps its tier.
  - The deliberate omissions (`PreservedQueryParameters`, the theme), the tier-boundary rule, read-only refusal, the self-service cancellation reasoning, "no editable setting destroys data", restart-bound marking, and the dependency statement: all carried forward word for word.
  - All six scenarios are carried forward word for word, and one is added.
  - Nothing is dropped.

## 5. Validate and QA (v17)

- [x] 5.1 Full verification on `main`'s branch: client build, then `dotnet build` (Release, 0 warnings), unit (baseline 2131), integration (210), rendering (1178), client (355). Report the new counts. Run `openspec validate single-choice-shortcut --strict` and CI's exact openspec command after the last commit.
- [x] 5.2 QA review (`qa-review`) in a fresh subagent, reused across rounds. Hand it the claims (build state, counts, live checks) to verify, not to trust. Treat each round's fixes as new code. **Round 1 REJECT (1 MAJOR); round 2 APPROVE WITH NITS, both taken** (see the remediation sections below).

## 6. Port to dev/v18

- [x] 6.1 After v17 QA approval, cherry-pick onto a branch from `dev/v18` in worktree `..\uBookIt-v18`. Resolve any line differences, and run the generators (client build) against a running v18 TestSite. Verify: the same suites at the v18 baselines (unit 2143, integration 210, rendering 1178, client 355) plus new tests, 0 warnings, and the live checks 2.3 and 3.5 repeated on v18. **Done on branch `single-choice-shortcut-v18`**; the full record is in that branch's copy of this file. In brief: the cherry-picks applied clean; unit 2197; checked live on Chris's own site's database, including a booking through the skipped resource flow (SHTH-RDZ2).
- [x] 6.2 QA review of the v18 branch: the same reviewer if it is still in this session, otherwise a fresh one told it is a port. **APPROVE (2026-10-15)**: the change's files are byte-identical to this branch's.

## 7. Release obligations (recorded for the release change, not done here)

- [x] 7.1 Record in the deferred-obligations memory what the `17.4.0` / `18.3.0` changelog entry must say. Under *What you have to do*: "a site whose booking page offers exactly one service or resource now goes straight to it; to keep showing the catalogue, untick *Skip the catalogue when there is only one choice* on the settings tab, or set `UBookIt:SkipSingleChoiceCatalogue` to `false`". Also note that Chris's own site needs it off before or at upgrade. Verify: the memory entry exists.

## QA round 1 remediation (REJECT: 1 MAJOR, 5 MINOR, 3 NIT)

- [x] R1.1 MAJOR: no test pinned "a named subject wins" on a catalogue of ONE. Added `An_author_named_subject_beats_the_skip_on_a_catalogue_of_one` and `A_subject_in_the_url_beats_the_skip_on_a_catalogue_of_one` (each run with the setting on and off), naming the service-only room on a one-service site. QA's ordering mutation (compute the sole entry before checking the subject) now fails 2 tests. The mutation set was rerun at seven mutations, all caught, and the files were restored.
- [x] R1.2 MINOR: `BookingFlowViewComponent` class `<code>` block qualified for a catalogue of one.
- [x] R1.3 MINOR: `package.xml`'s "with nothing named it renders the catalogue" comment qualified.
- [x] R1.4 MINOR: comments citing a non-existent rendering test now name `SingleChoiceDispatchTests` (`BookingFlowViewComponent` constructor remarks, `SingleChoiceTests` class remarks).
- [x] R1.5 MINOR: `booking-page.md`'s opening sentence qualified.
- [x] R1.6 NITs: `theming.md` names the unavailable and confirmation views too; the TestSite harness comment is qualified; the test's activator comment says what it does and does not show.
- [x] R1.7 Task 7.1 recorded in the deferred-obligations memory.

## QA round 2: APPROVE WITH NITS (both taken)

- [x] R2.1 NIT: the two catalogue-of-one named-subject tests now assert `Reason == NotOfferedIndividually`, which is what their comment says, rather than only the model type.
- [x] R2.2 NIT: `theming.md`'s edited paragraph rewrapped.
