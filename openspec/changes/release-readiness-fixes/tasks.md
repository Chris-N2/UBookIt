# Tasks — release-readiness-fixes

## 1. Query preservation (design D1–D3, the ADDED requirement)

- [ ] 1.1 `FrontendSettings` in `UBookIt.Web` (section `UBookIt:Frontend`,
      `PreservedQueryParameters` defaulting empty), bound where the Web composer wires
      rendering services, mirroring `DeliveryApiSettings`' idiom.
- [ ] 1.2 `PreservedQuery.Compute(query, allowList)` — pure, OrdinalIgnoreCase names,
      multi-value in order, uBookIt keys excluded by derivation from `BookingKeys`'
      `*Query` constants. Unit tests for every rule, including the listed-own-key case.
- [ ] 1.3 Totality guard: the derived exclusion set equals reflection over
      `BookingKeys`' public const string members named `*Query`, so a future key joins
      the exclusion or fails the guard.
- [ ] 1.4 Thread the pairs from both ViewComponents (`BookingFlowViewComponent`,
      `BookingViewComponent`) through `BookingFlowInput` and the catalogue build onto
      the form models and `CatalogueModel`.
- [ ] 1.5 Render the pairs as hidden inputs from `Catalogue.cshtml` and
      `_DateAndLength.cshtml` — INLINED in both, not a shared partial: a `_*` partial
      in the shared folder joins the public theming contract, which this change must
      not grow (design D3, revised at apply; a `_PreservedQuery.cshtml` was written
      and deleted for exactly this).
- [ ] 1.6 Rendering guards (UBookIt.Tests.Rendering) over BOTH forms: listed parameter
      present, unlisted absent, empty-list renders nothing, own-key never rendered,
      multi-value order, encoding. Mutation-check at least one guard per form against a
      commit (remove the partial include; the guard must fail).
- [ ] 1.7 Measure and record the POST step: does `BeginUmbracoForm`'s action URL retain
      the page's query string? Record the answer in this file; if the preserved
      parameters die at POST, report it — do not widen scope silently.
- [ ] 1.8 Document in `docs/booking-page.md`: the setting, the empty default, the
      own-keys exclusion, and the deliberate refusal to preserve unlisted parameters.
      Guard the load-bearing sentences with `DocumentationAssert`.

## 2. PII-guard flakiness (design D4)

- [ ] 2.1 Sweep the test tree at HEAD for hex-only needles matched against
      GUID-carrying haystacks (the seven known `"Ada"` sites plus anything the sweep
      adds). Classify EVERY site in this file: fixed, or out-of-class with the reason.
- [ ] 2.2 Apply the redaction fix at each in-class site: replace the ids the test
      itself created with a placeholder before matching; needles unchanged.
- [ ] 2.3 Prove determinism: pin an id containing "ada"
      (`20faadab-d4e1-4118-bc8c-d16111111111`) at one previously-flaky site and show
      the fixed assertion passes while the unfixed shape fails.
- [ ] 2.4 Re-run the sweep after; update the deferred-obligations memory entry at
      archive time (it currently claims ~2% flaky runs as live).

## 3. `directlyBookable` disposition (design D5)

- [ ] 3.1 Start the TestSite; management-API round-trip: GET a resource with the flag
      true → change one unrelated field → PUT the read-back body → GET → flag intact?
- [ ] 3.2 Same round-trip through the backoffice editor (browser), since the original
      reproductions were manual.
- [ ] 3.3 EITHER: reproduces → STOP, write the `resource-management` delta, fix under
      it, with a regression test. OR: does not reproduce → record the disposition here
      and rewrite the deferred-obligations entry at archive time (verified chain at
      HEAD, likely original cause: the TestSite harness picks the first resource by
      list order).

## 4. Verification

- [ ] 4.1 Full suites green at Release (`dotnet test UBookIt.slnx -c Release`), client
      suite green, Release --no-incremental build 0 warnings. Recount totals at HEAD
      for the QA handover — never reuse a previous count.
- [ ] 4.2 `openspec validate --all --strict` clean.

## 5. QA

- [ ] 5.1 QA round(s) — fresh subagent, reused across rounds; report claims for it to
      verify rather than trust; treat each round's fixes as new code. Findings return
      through apply.

## 6. Sync + archive (after QA approval)

- [ ] 6.1 Sync the delta into `openspec/specs/default-frontend/spec.md` (ADDED — no
      guarantee-diff owed, but run the falsified-sentence sweep over sibling specs,
      docs and XML remarks wrap-normalised BY PATTERN over the whole tree, not by
      candidate list — round 4 of `permissions-model` is why).
- [ ] 6.2 Update the deferred-obligations memory (items 2.4 and 3.3).
- [ ] 6.3 Archive the change; merge after QA approval.
