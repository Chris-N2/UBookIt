## 1. The port and the classification rule

- [x] 1.1 Add `IPublicHolidaySource` and the `PublicHoliday` record to `UBookIt.Core`, taking an inclusive window and a cancellation token; verify by a test asserting the port carries no region, country or locale parameter, since that absence is the design decision rather than an omission
- [x] 1.2 Add the failure codes this change owns (a source that failed, a holiday that cannot be imported) to `FailureCodes`; verify with a test asserting each literal value, as the codes are a published contract
- [x] 1.3 Implement the classification rule as a pure function over the source's holidays and the existing closure dates, producing rows of new / already closed / cannot import with a reason; verify with unit tests for each state, needing no database, network or HTTP
- [x] 1.4 Collapse same-date duplicates in that rule, first name winning, reporting the collapse; verify with a test that two holidays on one date yield one row carrying the first name **and** a reported collapse — a test that only counts rows would pass while the operator learns nothing
- [x] 1.5 Drop holidays dated outside the requested window; verify with a test over a source that returns one either side
- [x] 1.6 Verify the rule treats an over-long name as unimportable with its reason rather than truncating it, and that the reason names the label length rather than a generic failure

## 2. Reading a source

- [x] 2.1 Add the read that calls a registered source over a window and returns its holidays, passing cancellation through; verify with a fake source that records the token it was given
- [x] 2.2 Report a source that throws as a **source failure**, distinct from an empty result; verify with two tests — a throwing source and a source returning none — asserting the two produce different, distinguishable answers
- [x] 2.3 Verify no code path in the package calls a source except in response to an operator request: no job, no startup hook, no composer call. Assert it by scanning for callers rather than by reading, since "nothing schedules it" is the change's central claim

## 3. Management API

- [x] 3.1 Add the preview endpoint in the `ubookitbackoffice` swagger group, taking the window and returning the classified rows plus any collapse or source-failure report; verify with endpoint tests over a fake source covering all three row states
- [x] 3.2 Add the import endpoint taking the chosen dates and names, creating each through the existing closure management store; verify only the chosen rows are created
- [x] 3.3 Re-validate at import rather than trusting the preview, reporting per row where a date has since been taken while still creating the rest; verify by creating a closure between a preview and an import and asserting the rest still land
- [x] 3.4 Gate both endpoints on `UBookIt.Settings`; verify through the **real policy engine** (the `PermissionsTests` harness), not by reading `[Authorize]` attributes — an attribute test would pass with the policy registered against the wrong verbs
- [x] 3.5 Refuse both endpoints when no source is registered, so absence is not merely a client-side hide; verify with a controller built without a source
- [x] 3.6 Verify the preview creates nothing: call it twice and assert the closure list is unchanged after both
- [x] 3.7 Regenerate the OpenAPI client and verify the generated TypeScript compiles and carries the new endpoints

## 4. Backoffice client

- [x] 4.1 Add the import panel to the Closures view with the window fields defaulting to today through the end of next year; verify the default window with a fixed clock, at an instant where "next year" is unambiguous
- [x] 4.2 Render the three row states, with only new rows selectable and selected by default, and both other states showing why they are not; verify with client tests over the pure selection state
- [x] 4.3 Send only the ticked rows on confirm, derived from the whole list each time; verify that unticking and re-ticking produces the same set as never having ticked — full replacement, like the opt-out set
- [x] 4.4 Show the import control only where a source is registered, and render nothing about importing otherwise; verify both branches
- [x] 4.5 Report a source failure distinctly from an empty result in the UI; verify the two render differently, since conflating them tells an operator their calendar is clear when it is unknown
- [x] 4.6 Add the localization terms; verify with the element-terms guard, which fails on a term an element asks for and `en-us.ts` lacks
- [x] 4.7 Accessibility: every checkbox individually labelled with its date and name, the window inputs labelled, failures announced and associated, full keyboard operability with visible focus; verify at source level and confirm live in task 6

## 5. The TestSite implementation — the seam's proof

- [x] 5.1 Implement `IPublicHolidaySource` in `UBookIt.TestSite` against the `gov.uk` bank-holiday feed, mapping `title` to the name and handling the `notes` field that marks a substitute day; verify it is registered in the TestSite's own composer and **not** packed
- [x] 5.2 Verify the package's own test suites never call the live feed — the fake source is what tests use; assert no test project references the gov.uk host
- [x] 5.3 Verify the port expressed the real feed without the package learning what a substitution is: the substitute-day handling lives entirely in the TestSite implementation

## 6. Live verification

- [x] 6.1 Build the client, then the solution, and verify zero warnings in Release
- [x] 6.2 On the running TestSite, preview a window against the live `gov.uk` feed and verify the rows classify correctly against the closures already present (one of which should be a date the feed also returns, to exercise *already closed*)
- [x] 6.3 Untick one row, confirm, and verify exactly the ticked closures were created — then verify the unticked date is offered again by a second preview
- [x] 6.4 Verify an imported closure behaves as any other: rename it, opt a resource out of it, and confirm availability for that date disappears and returns exactly as it does for a hand-typed closure
- [x] 6.5 Verify the import is absent on a site with no source — by unregistering it in the TestSite composer, restarting, and confirming both the control and the endpoints are gone
- [x] 6.6 Verify the verb split live against a Configure-only user: the closure list is readable, the preview and import are refused
      *Observed by the site owner, signed in as a user whose only uBookIt grant is `UBookIt.Configure` (the `Perm Test` group, set to Configure alone with `See bookings` off), and evidenced by screenshot:*
      - *All ten closures render with their dates and names — `Configure` reads the list.*
      - *The Actions column is absent entirely and `Show past closures` is the only control: no add, no edit, no delete.*
      - *The read-only state is explained, naming the grant and where to give it: "You do not have permission to change closures. An administrator can grant it in Users → User Groups → Default permissions, by ticking 'Change site settings'. It is not granted automatically, including on upgrade."*
      - ***No import control and no second explanation** — that one sentence covers importing too, because importing is a way of changing closures. This is the scenario "The verb explanation covers the import without naming the source", observed rather than inferred.*
      - *Gated twice in the client: the source probe is only requested when the user may write (`closures-view.element.ts:116`), so a Configure-only session never asks whether a source exists at all, and the panel separately requires the probe to have answered true (`:419`).*

## 7. Documentation

- [x] 7.1 Document the operator's side in `docs/backoffice.md` — what the preview offers, that unticking is not remembered, and that an imported closure is an ordinary one
- [x] 7.2 Document the developer's side: the port, that registration is optional and absence is total, that a source must respect cancellation, and that regions are the source's business — with the TestSite implementation as the worked example
- [x] 7.3 Verify the docs state that the package ships no holiday data for any country and will not be adding any

## 8. Spec sync

- [x] 8.1 Re-run the sibling-falsification sweep over every spec, looking for sentences about closures, verbs or scheduled work that this change makes untrue; verify by listing what was checked and what was found
- [x] 8.2 Re-check the two "modifies nothing" claims in the proposal — `site-closures` and `permissions` — against the specs as they now stand, and record the result either way
- [x] 8.3 Run `openspec validate --strict` and the full suites, and verify `ChangeDeltaIntegrityTests` passes
- [ ] 8.4 At sync, correct `site-closures`' Purpose sentence "so that a later public-holiday feed has somewhere to write" — this is a host-implemented port read on an operator's action, not a feed that writes; recorded in `sweep.md`
- [x] 8.5 **MODIFIED** `permissions` / *Access within the section is decided by four verbs* — the `UBookIt.Settings` bullet gains both halves of an import; the closure-split rationale gains a fourth act and the reason previewing sits with the writes; the verb's reach gains invoking the site's own code. Guarantees diffed: 18 scenarios in, 21 out, none dropped; every SHALL carried forward verbatim; the verb count stays four
- [x] 8.6 **MODIFIED** `site-closures` / *The closures view* — the action enumeration becomes conditional on the site and the user's verb, and the keyboard scenario covers the import. Guarantees diffed: 4 scenarios in, 5 out, none dropped; the upcoming-by-default rule, the no-automatic-deletion rule and the accessibility bar all carried forward verbatim
- [x] 8.7 **MODIFIED** `site-closures` / *Closures are read and written by different verbs* — "Both" becomes "Each of these" over four acts; the read-but-not-write explanation rule is distinguished from this change's absent-capability rule. Guarantees diffed: 7 scenarios in, 10 out, none dropped; all three verb rules and the server-enforcement rule carried forward verbatim
- [x] 8.8 **MODIFIED** `site-closures` / *Closures are managed through versioned management endpoints* — the endpoint enumeration gains preview, import and probe, and the 404 rule covers the absent-source case. Guarantees diffed: 4 scenarios in, 6 out, none dropped; the purpose-built-DTO rule, the no-domain-types rule, the server-side upcoming filter and the stable-code rule carried forward verbatim
