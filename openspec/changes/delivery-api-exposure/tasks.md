# Tasks — delivery-api-exposure

Working rules carried forward: verify fixes against `HEAD`, after the last edit, before
claiming; a fix to a named finding prompts "what else is in this class?"; build claims
come from a `--no-incremental` Release build read against the ZERO-warning baseline with
the TestSite stopped; the delta-integrity guard matches raw ordinal substrings, so any
replaced requirement title lives in this file unwrapped on its own line; sweep
wrap-normalised; never write prose through a double-quoted shell string.

## 1. Settings and the convention

- [x] 1.1 `DeliveryApiSettings { EnableReads, EnablePlacement }` in `UBookIt.Web`, both
      false, bound from `UBookIt:DeliveryApi` by the Web composer at startup; registered
      as a singleton alongside the existing settings resolution.
- [x] 1.2 `[DeliveryRead]` / `[DeliveryPlacement]` attributes; classify every existing
      delivery action (placement = `BookingsController.PlaceBooking`,
      `ServicesController.PlaceServiceBooking`; everything else including the
      privacy/retention read = read).
- [x] 1.3 The application-model convention: for each action on a
      `UBookItDeliveryApiControllerBase`-derived controller whose direction is disabled,
      clear the action's selectors and set `ApiExplorer.IsVisible = false`. Register it
      from the Web composer.
- [x] 1.4 The classification totality guard: reflection over every delivery action —
      exactly one direction attribute each, failure names the offender. This is the
      guard that makes "unclassified cannot ship exposed" true.

## 2. Behaviour tests

- [x] 2.1 Application-model tests: better than planned — the harness stands up MVC's
      real pipeline (`AddControllers` + application part + the real convention, no HTTP
      host) and asserts what MVC produced: action DESCRIPTORS (the routing fact — no
      descriptor, no route) and ApiExplorer descriptions, under all four settings
      combinations, against HARD-CODED action-name lists (deriving them from the
      attributes would be circular). A completeness test pins the lists to the real
      assembly, and the surface controllers are asserted present-and-identical in both
      states (both directions of the precondition). (Controllers constructed directly in existing delivery tests
      bypass routing on purpose — exposure is asserted at the model, where it is
      decided.)
- [x] 2.2 Mutation-check the matrix: run live — GetPrivacy misclassified as placement
      failed 2 tests; the convention's read arm hard-wired to `true` failed 5 including
      the untouched-install test. The "defaults to visible when unclassified" arm is
      protected by the totality guard instead: an unclassified action cannot exist at
      HEAD, so that mutation is unobservable by design and the guard is what makes it
      so. **Trap hit and recorded: reverting mutation A with `git checkout` restored
      the file to HEAD and silently removed the uncommitted direction attribute along
      with the mutation — mutate against a COMMIT, or revert by hand.** Caught because
      the suite went red after the "revert"; re-added and re-verified green.
- [x] 2.3 Swagger: covered at two levels rather than by generating documents in-test —
      the automated matrix asserts ApiExplorer descriptions (the exact input Swashbuckle
      builds the document from) under all four combinations, and the live check read the
      real generated document twice: 11 operations with both directions on, **0 paths**
      with nothing configured. Standing Swashbuckle's generator up inside a unit test
      would re-test Swashbuckle, not us.
- [x] 2.4 Indistinguishability: assert the disabled path produces no route match at the
      application model level (no selector = the host's own 404 — there is no code of
      ours that could answer differently), and record in the test why this is the
      structural form of the "same status, shape and headers" scenario.

## 3. Docs

- [x] 3.1 New `docs/delivery-api.md`: the two switches and their off defaults; anonymous
      by design and what that means; why origin validation cannot exist (client-supplied
      headers; CORS restricts browsers, not callers); volume defence is the host's rate
      limiter or edge and the package claims no DDoS protection; `MaxQueryRangeDays` as
      the per-request cost bound on every caller path; anonymous placement risk stated
      honestly with approval mode as the business-level mitigation; the declined
      per-caller cap and its reasoning; the breaking default-flip and the two-line fix.
- [x] 3.2 README: the security story in the feature area, up front (Chris's explicit
      ask) — the API is off by default, what turning it on exposes, and the pointer to
      the full page. Sweep README's existing feature bullets for sentences the flip
      falsifies (the headless/API bullet, if any, must not imply always-on).
- [x] 3.3 `docs/booking-page.md` and other docs cross-references: sweep for any claim
      that the API is available/always there. Swept: booking-page.md has no API mention;
      notifications.md's mention is conditional ("if you have built your own front end")
      and stays true; README's package-table row describes what UBookIt.Web contains,
      which is still true with exposure off. **Found during 4.1 and folded in here:
      Umbraco's own content Delivery API lives at the near-identical
      `Umbraco:CMS:DeliveryApi:Enabled` (the TestSite sets it) — disambiguated in
      docs/delivery-api.md and guarded.**
- [x] 3.4 `DocumentationAssert` guards on the guarantees: off-by-default stated in both
      README and docs page; the no-DDoS-claim boundary; the origin-validation
      impossibility; the breaking-change callout. Wrap-safe, on the document as
      normalised text.

## 4. Dev environment

- [x] 4.1 TestSite `appsettings.Development.json`: enable both directions (the API's
      test bed; not shipped). Confirm the Install Check site inherits nothing and so
      exercises the shipped default.

## 5. Verification

- [x] 5.1 Full .NET + client suites green; `--no-incremental` Release build, ZERO
      warnings, TestSite stopped first.
- [x] 5.2 `openspec validate --all --strict` (20 items); guarantee-diff done at
      delta-writing against the base spec, which no other change has touched since —
      every auth-stance SHALL and all three scenarios carried, reachability scoped to an
      enabled direction. The replaced title, unwrapped for the guard:
      - Anonymous access and auth stance
- [x] 5.3 Live check on the TestSite (2026-09-12): with the dev settings on — reads
      200, swagger 200 with all 11 operations, booking page 200. With the override
      removed (shipped default) — read 404, placement 404, and the response HEADERS
      byte-identical to a made-up route under the same prefix (the indistinguishability
      scenario observed live, not argued); swagger 200 with **0 paths**; the Razor
      booking page still fully rendering. Dev settings restored afterwards (diff vs
      HEAD: line endings only).

## 6. Sync-time greps (run at sync, not before; do not tick until executed)

- [ ] 6.1 Outward sweep, wrap-normalised: sibling specs and Purpose prose for sentences
      the flip falsifies — candidates known now: `delivery-api` Purpose ("public,
      anonymous, versioned delivery API that every booking UI consumes" — needs the
      off-by-default framing); any spec or Purpose stating the API "is" available,
      registered, or exposed unconditionally; `packaging` claims about what an install
      provides; the front-end-contract rationale wherever specs restate it
      ("alternative UIs consume the API" sentences are fine — "the API is there" ones
      are not).
- [ ] 6.2 Falsified-claims sweep over `README.md`, `docs/*.md`, and XML doc comments —
      candidates known now: `UBookItDeliveryApiComposer`'s summary ("the delivery API
      is anonymous"), `UBookItDeliveryApiControllerBase` docs, `Constants` comments,
      and `docs/booking-page.md` if it mentions the API.
