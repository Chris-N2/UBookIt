## 1. The port and the classification rule

- [ ] 1.1 Add `IPublicHolidaySource` and the `PublicHoliday` record to `UBookIt.Core`, taking an inclusive window and a cancellation token; verify by a test asserting the port carries no region, country or locale parameter, since that absence is the design decision rather than an omission
- [ ] 1.2 Add the failure codes this change owns (a source that failed, a holiday that cannot be imported) to `FailureCodes`; verify with a test asserting each literal value, as the codes are a published contract
- [ ] 1.3 Implement the classification rule as a pure function over the source's holidays and the existing closure dates, producing rows of new / already closed / cannot import with a reason; verify with unit tests for each state, needing no database, network or HTTP
- [ ] 1.4 Collapse same-date duplicates in that rule, first name winning, reporting the collapse; verify with a test that two holidays on one date yield one row carrying the first name **and** a reported collapse — a test that only counts rows would pass while the operator learns nothing
- [ ] 1.5 Drop holidays dated outside the requested window; verify with a test over a source that returns one either side
- [ ] 1.6 Verify the rule treats an over-long name as unimportable with its reason rather than truncating it, and that the reason names the label length rather than a generic failure

## 2. Reading a source

- [ ] 2.1 Add the read that calls a registered source over a window and returns its holidays, passing cancellation through; verify with a fake source that records the token it was given
- [ ] 2.2 Report a source that throws as a **source failure**, distinct from an empty result; verify with two tests — a throwing source and a source returning none — asserting the two produce different, distinguishable answers
- [ ] 2.3 Verify no code path in the package calls a source except in response to an operator request: no job, no startup hook, no composer call. Assert it by scanning for callers rather than by reading, since "nothing schedules it" is the change's central claim

## 3. Management API

- [ ] 3.1 Add the preview endpoint in the `ubookitbackoffice` swagger group, taking the window and returning the classified rows plus any collapse or source-failure report; verify with endpoint tests over a fake source covering all three row states
- [ ] 3.2 Add the import endpoint taking the chosen dates and names, creating each through the existing closure management store; verify only the chosen rows are created
- [ ] 3.3 Re-validate at import rather than trusting the preview, reporting per row where a date has since been taken while still creating the rest; verify by creating a closure between a preview and an import and asserting the rest still land
- [ ] 3.4 Gate both endpoints on `UBookIt.Settings`; verify through the **real policy engine** (the `PermissionsTests` harness), not by reading `[Authorize]` attributes — an attribute test would pass with the policy registered against the wrong verbs
- [ ] 3.5 Refuse both endpoints when no source is registered, so absence is not merely a client-side hide; verify with a controller built without a source
- [ ] 3.6 Verify the preview creates nothing: call it twice and assert the closure list is unchanged after both
- [ ] 3.7 Regenerate the OpenAPI client and verify the generated TypeScript compiles and carries the new endpoints

## 4. Backoffice client

- [ ] 4.1 Add the import panel to the Closures view with the window fields defaulting to today through the end of next year; verify the default window with a fixed clock, at an instant where "next year" is unambiguous
- [ ] 4.2 Render the three row states, with only new rows selectable and selected by default, and both other states showing why they are not; verify with client tests over the pure selection state
- [ ] 4.3 Send only the ticked rows on confirm, derived from the whole list each time; verify that unticking and re-ticking produces the same set as never having ticked — full replacement, like the opt-out set
- [ ] 4.4 Show the import control only where a source is registered, and render nothing about importing otherwise; verify both branches
- [ ] 4.5 Report a source failure distinctly from an empty result in the UI; verify the two render differently, since conflating them tells an operator their calendar is clear when it is unknown
- [ ] 4.6 Add the localization terms; verify with the element-terms guard, which fails on a term an element asks for and `en-us.ts` lacks
- [ ] 4.7 Accessibility: every checkbox individually labelled with its date and name, the window inputs labelled, failures announced and associated, full keyboard operability with visible focus; verify at source level and confirm live in task 6

## 5. The TestSite implementation — the seam's proof

- [ ] 5.1 Implement `IPublicHolidaySource` in `UBookIt.TestSite` against the `gov.uk` bank-holiday feed, mapping `title` to the name and handling the `notes` field that marks a substitute day; verify it is registered in the TestSite's own composer and **not** packed
- [ ] 5.2 Verify the package's own test suites never call the live feed — the fake source is what tests use; assert no test project references the gov.uk host
- [ ] 5.3 Verify the port expressed the real feed without the package learning what a substitution is: the substitute-day handling lives entirely in the TestSite implementation

## 6. Live verification

- [ ] 6.1 Build the client, then the solution, and verify zero warnings in Release
- [ ] 6.2 On the running TestSite, preview a window against the live `gov.uk` feed and verify the rows classify correctly against the closures already present (one of which should be a date the feed also returns, to exercise *already closed*)
- [ ] 6.3 Untick one row, confirm, and verify exactly the ticked closures were created — then verify the unticked date is offered again by a second preview
- [ ] 6.4 Verify an imported closure behaves as any other: rename it, opt a resource out of it, and confirm availability for that date disappears and returns exactly as it does for a hand-typed closure
- [ ] 6.5 Verify the import is absent on a site with no source — by unregistering it in the TestSite composer, restarting, and confirming both the control and the endpoints are gone
- [ ] 6.6 Verify the verb split live against a Configure-only user: the closure list is readable, the preview and import are refused

## 7. Documentation

- [ ] 7.1 Document the operator's side in `docs/backoffice.md` — what the preview offers, that unticking is not remembered, and that an imported closure is an ordinary one
- [ ] 7.2 Document the developer's side: the port, that registration is optional and absence is total, that a source must respect cancellation, and that regions are the source's business — with the TestSite implementation as the worked example
- [ ] 7.3 Verify the docs state that the package ships no holiday data for any country and will not be adding any

## 8. Spec sync

- [ ] 8.1 Re-run the sibling-falsification sweep over every spec, looking for sentences about closures, verbs or scheduled work that this change makes untrue; verify by listing what was checked and what was found
- [ ] 8.2 Re-check the two "modifies nothing" claims in the proposal — `site-closures` and `permissions` — against the specs as they now stand, and record the result either way
- [ ] 8.3 Run `openspec validate --strict` and the full suites, and verify `ChangeDeltaIntegrityTests` passes
