# Tasks — release-readiness-fixes

## 1. Query preservation (design D1–D3, the ADDED requirement)

- [x] 1.1 `FrontendSettings` in `UBookIt.Web` (section `UBookIt:Frontend`,
      `PreservedQueryParameters` defaulting empty), bound where the Web composer wires
      rendering services, mirroring `DeliveryApiSettings`' idiom.
- [x] 1.2 `PreservedQuery.Compute(query, allowList)` — pure, OrdinalIgnoreCase names,
      multi-value in order, uBookIt keys excluded by derivation from `BookingKeys`'
      `*Query` constants. Unit tests for every rule, including the listed-own-key case.
- [x] 1.3 Totality guard: the derived exclusion set equals reflection over
      `BookingKeys`' public const string members named `*Query`, so a future key joins
      the exclusion or fails the guard.
- [x] 1.4 Thread the pairs from both ViewComponents (`BookingFlowViewComponent`,
      `BookingViewComponent`) through `BookingFlowInput` and the catalogue build onto
      the form models and `CatalogueModel`.
- [x] 1.5 Render the pairs as hidden inputs from `Catalogue.cshtml` and
      `_DateAndLength.cshtml` — INLINED in both, not a shared partial: a `_*` partial
      in the shared folder joins the public theming contract, which this change must
      not grow (design D3, revised at apply; a `_PreservedQuery.cshtml` was written
      and deleted for exactly this).
- [x] 1.6 Rendering guards (UBookIt.Tests.Rendering) over BOTH forms: listed parameter
      present, unlisted absent, empty-list renders nothing, own-key never rendered,
      multi-value order, encoding. Mutation-check at least one guard per form against a
      commit (remove the partial include; the guard must fail).
- [x] 1.7 Measure and record the POST step. **MEASURED (2026-09-14), two halves:**
      (a) The POST itself KEEPS the query — `BeginUmbracoForm` takes
      `OriginalRequestUrl.PathAndQuery` as the form action (established by ⑪'s spike
      and asserted in the rendering rig), so parameters that survived the GET step
      ride into the POST by construction. (b) **The PRG redirect after the POST DROPS
      them**: both surface controllers' `BackToFlow` redirect with
      `RedirectToCurrentUmbracoPage(BookingFlowLink.For(...))`, which rebuilds the
      query from uBookIt's own parameters only — so a validation failure's redraw and
      a placement's confirmation both land on a URL without the preserved parameters.
      **REPORTED TO CHRIS, per the proposal's Non-goals — not silently widened.** The
      fix, if wanted, is bounded: the controllers already receive the posted-to URL's
      query, so `BackToFlow` (and the success redirect) could append the computed
      preserved pairs to the rebuilt query; it would need a scenario added to the
      delta first.
- [x] 1.8 Document in `docs/booking-page.md`: the setting, the empty default, the
      own-keys exclusion, and the deliberate refusal to preserve unlisted parameters.
      Guard the load-bearing sentences with `DocumentationAssert`.

## 2. PII-guard flakiness (design D4)

- [x] 2.1 Sweep the test tree at HEAD for hex-only needles matched against
      GUID-carrying haystacks (the seven known `"Ada"` sites plus anything the sweep
      adds). Classify EVERY site in this file: fixed, or out-of-class with the reason.
- [x] 2.2 Apply the redaction fix at each in-class site: replace the ids the test
      itself created with a placeholder before matching; needles unchanged.
- [x] 2.3 Prove determinism: pin an id containing "ada"
      (`20faadab-d4e1-4118-bc8c-d16111111111`) at one previously-flaky site and show
      the fixed assertion passes while the unfixed shape fails.
- [x] 2.4 Re-run the sweep after; update the deferred-obligations memory entry at
      archive time (it currently claims ~2% flaky runs as live).

## 3. `directlyBookable` disposition (design D5)

- [x] 3.1 / 3.2 **RUN LIVE 2026-09-14, DOES NOT REPRODUCE.** Two full round-trips on
      the running TestSite through the backoffice editor — the real client's GET →
      edit one unrelated field (Description) → PUT — against `DBO Granted`
      (05691531-…, flag ON): SQL after each save shows `DirectlyBookable = 1` intact,
      and the description round-tripped both ways (set, then back to null). The
      editor reload between saves also proves the GET half (toggle re-rendered
      ticked). A hand-rolled curl PUT was NOT performed — the backoffice token is not
      reachable from outside the app and IndexedDB access is blocked — accepted
      because the editor's PUT exercises the same wire models, which are symmetric by
      inspection (`ResourceRequestModel`/`ResourceResponseModel` both carry
      `DirectlyBookable`; `ResourceModelMapper` maps it both directions; the client
      loads, renders and sends it), and nothing in that chain has changed since the
      2026-09-10 note.
- [x] 3.3 Disposition: **not a live defect.** Likely original cause, recorded in the
      same memory entry that reported it: the TestSite harness picks the FIRST
      resource by list order, so a rename changes which resource the front end
      shows — producing exactly the observed symptom ("vanished from the front end
      while the backoffice still lists it") with no data change at all.
      Deferred-obligations memory to be rewritten at archive (6.2).

## 3b. The submission-redirect fix (approved by Chris 2026-09-14, delta amended)

- [x] 3b.1 Delta: requirement gains the whole-flow sentence and two scenarios
      (submission redirect; component-named flow's redirect).
- [x] 3b.2 `BookingFlowLink` carries the preserved tail (`For(..., preserved:)` +
      `Carrying` for the subjectless branch) so "every query string the controllers
      produce is built by one vocabulary" holds; both surface controllers compute the
      pairs from the POSTed-to URL's query and pass them through. Location-header
      reflection stated and bounded in the remarks (allow-list + percent-encoding).
- [x] 3b.3 Docs flipped from "but not the redirect after it" to the whole-flow claim,
      WITH its guard — the pair moved together.
- [x] 3b.4 Unit tests: appended-in-order + encoded, empty-tail byte-identity,
      preserved-only query, and the source-level wiring guard over both controllers.
- [x] 3b.5 **LIVE end-to-end (2026-09-14), new binaries, TestSite configured with
      `PreservedQueryParameters: ["utm_source", "culture"]`:** `/book?utm_source=e2e`
      → catalogue Continue → URL kept `utm_source` → date form "Show times" → kept →
      time + details + Book (POST) → confirmation at
      `/book/?ubBook=…&ubDate=2026-09-21&ubMins=30&utm_source=e2e`. Every step of the
      requirement observed on a real page. Left behind on the dev site: booking
      **CQ8C-72JQ** (E2E Preserved, 2026-09-21 09:30, DBO Granted) and the
      TestSite config entry (kept deliberately — it documents the setting in dev,
      like the delivery-API flags beside it).

## 4. Verification

- [x] 4.1 Full suites green at Release (`dotnet test UBookIt.slnx -c Release`), client
      suite green, Release --no-incremental build 0 warnings. Recount totals at HEAD
      for the QA handover — never reuse a previous count.
- [x] 4.2 `openspec validate --all --strict` clean.

## 5. QA

- [x] 5.1 QA round(s) — fresh subagent, reused across rounds; report claims for it to
      verify rather than trust; treat each round's fixes as new code. Findings return
      through apply.

## 6. Sync + archive (after QA approval)

- [x] 6.1 Sync the delta into `openspec/specs/default-frontend/spec.md` (ADDED — no
      guarantee-diff owed, but run the falsified-sentence sweep over sibling specs,
      docs and XML remarks wrap-normalised BY PATTERN over the whole tree, not by
      candidate list — round 4 of `permissions-model` is why).
- [x] 6.2 Update the deferred-obligations memory (items 2.4 and 3.3).
- [x] 6.3 Archive the change; merge after QA approval.

## 2.1 sweep record (2026-09-14, at HEAD before this commit)

14 hex-only `DoesNotContain` needles found (`Ada`, `07700`, `01234`, `00000000`).
Classification, every site:

- **Fixed this change** (haystack carries GUIDs; now `GuidRedaction.WithoutGuids`):
  `EraseBookerEndpointTests` 95/97 (payload; both "Ada" and "01234" collide) and 139
  (failure payload); `BookingsEndpointTests` 754-757 (serialized page whose row ids are
  random GUIDs).
- **Already fixed before this change** (local `AnyGuid` copies, now consolidated onto
  the shared helper — three identical private regexes deleted):
  `BookingEmailTests` 343/346 + 495/498; `BookingTemplateCompositionTests` 252/255;
  `UmbracoBookingObserverTests` 143/146.
- **Out of class, deliberately unchanged**: `ServiceFrontendTests:1076`
  (`"00000000"` — the needle IS the no-raw-id assertion; redacting first would make it
  vacuous); `BookingEmailTests` 122/131 (site-message body carries the reference —
  whose alphabet has no A — and a plain URL, no GUID; the sibling awaiting-approval
  test at 343 redacts because ITS body variant can carry one).

Design D4 REVISED at apply: redaction is by GUID SHAPE, not by known token — the
in-repo argument (`BookingEmailTests`, QA-reviewed in ㉘): redacting only the id the
test created fixes today and leaves the class, since the next id added to the line
reintroduces the collision. The safety condition (names/emails/phones are not
GUID-shaped) is asserted by `GuidRedactionTests`.

Determinism proof (2.3): with the erased booking id pinned to QA's
`20faadab-d4e1-4118-bc8c-d16111111111`, the fixed guard passes and the unfixed shape
fails — both measured, then the pin reverted. First mutation attempt was a no-op
(CRLF mismatch in the replace) and was caught by the test NOT failing: a mutation that
changes nothing proves nothing, verify the mutant differs before trusting its verdict.

## QA round 1 — REJECT, and the fix (2026-09-14)

Two MAJORs, both guard blindness: (1) deleting the FrontendSettings AddSingleton left
2684 tests green; (2) dropping the preserved argument from the subject-ful redirect
branch did too — the wiring guard pinned only the subjectless branch's mechanism.

- [x] R1.1 [MAJOR 1] The composer is now composed FOR REAL over an in-memory
      configuration and asserted BY EFFECT: registration resolves, and the configured
      names arrive through the real section key and binder. Second test pins the
      absent-section empty default.
- [x] R1.2 [MAJOR 2] The whole redirect decision extracted to
      `BookingFlowLink.AfterSubmission` (preserved parameter REQUIRED — a call
      without it does not compile) and asserted by OUTPUT branch by branch, including
      the differential guard that the subject-ful result still ENDS with the tail
      (equality with For() alone could agree on the wrong answer). Controllers may
      call nothing but AfterSubmission (seam guard now forbids For/Carrying there)
      and a Singleline regex pins that the argument is computed from Request.Query.
- [x] R1.3 [MINOR] The vacuous `Contains("preserved")` died with the guard it padded.
- [x] R1.4 [NIT] The docs' own-keys sentence is tied to the derived set
      (`The_docs_name_every_own_key` iterates `PreservedQuery.OwnKeys`).
- [x] R1.5 Mutations at commit `5730dc6`, all detected, tree restored: delete
      AddSingleton → both composer tests fail; section key "UBookIt:Frontends" →
      binding test fails; AfterSubmission's subject branch drops the tail → two
      output tests fail; controller passes `[]` instead of Compute → regex guard
      fails. QA's original "remove preserved:" mutation is now UNWRITABLE in the
      controllers — the parameter is required.
      At HEAD after fixes: 2692 .NET (1477 + 130 + 1085) + 167 client, Release
      no-incremental 0 warnings.

## QA round 2 — REJECT (one MAJOR), and the fix (2026-09-14)

- [x] R2.1 [MAJOR] `Compute(Request.Query, [])` — an empty stand-in laundered
      through the pinned function — left all 2692 tests green (QA ran it on the
      service controller). The wiring regex now pins BOTH Compute arguments
      (`Request.Query, _frontendSettings.PreservedQueryParameters`). Chose QA's
      one-line close over moving Compute behind the seam: QA's own analysis rules
      the remaining decay mode (a constructor assigning a fresh FrontendSettings)
      implausible-by-accident and explicitly not required.
- [x] R2.2 Mutation evidence at commit `99cd80f`, QA's exact mutant in BOTH
      controllers, mutant-differs verified before each run ("mutated:" printed from
      a string-compare, the round-1 CRLF no-op lesson): each fails the guard;
      restored; tree clean. Green at HEAD first, then mutated — the guard passes
      unmutated and fails mutated in both.

## Sync (task 6.1) — RUN 2026-09-14

- [x] 6.1 The ADDED requirement landed VERBATIM in
      `openspec/specs/default-frontend/spec.md` (appended; scripted copy of the
      delta's requirement block, no retyping). 21 items validate strictly.
      **Falsified-sentence sweep, wrap-normalised, BY PATTERN over src + tests +
      docs + specs** (patterns: nothing else / nothing a caller typed / Location
      header / flow state in the URL / carries only / not the redirect / byte-for-
      byte / byte-identical / exactly the query string / exactly what it rendered).
      THREE falsifications found and fixed — all the same class, an UNCONDITIONAL
      byte-for-byte identity claim now conditional on no preserved configuration:
      `BookingSurfaceController` BackToFlow summary ("its redirect is byte-for-byte
      what it was before the service flow existed"); `BookingViewModels.FlowToken`
      remark (same sentence about the rendered markup); `Booking/Default.cshtml`'s
      hidden-token comment (same sentence about the redirect). Each now names the
      condition and what changes when it fails.
      Checked and NOT falsified, on the record: `BookingSubject`'s "and nothing
      else, ever" and the Location-header sentences (amended in-change to name the
      preserved tail); "no flow state in the URL" family (preserved parameters are
      HOST state, not flow state — every sentence remains true);
      `The_only_flow_state_in_the_URL_...` test (asserts the BookingKeys enumeration,
      which did not grow); `FrontendSettings`' own unconfigured-site claim (states
      its condition already); "contact details never travel in a URL" (still true —
      uBookIt puts none there; what a site lists is the site's act); the delivery/
      theming/persistence "and nothing else" family (unrelated domains); TestSite
      Program.cs's theming claim (about themes, unaffected).
