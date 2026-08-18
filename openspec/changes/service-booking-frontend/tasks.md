## 1. The seam, before either flow uses it

- [x] 1.1 Extract from `BookingFormBuilder` the parts that are genuinely about *a
      booking form* rather than *a resource*: date bounds and today-in-zone, the
      times projection, the error model, the failed-submission round trip. Keep them
      pure and host-free — that property is why the existing builder is testable, and
      it is the property most easily lost in an extraction.
- [x] 1.2 **The resource flow must not change behaviour while its guts move.** Its
      tests are the guard: they pass before and after the extraction, unmodified. If
      one needs editing to stay green, the extraction changed behaviour — stop and
      look, rather than editing the test.
- [x] 1.3 Shared partials for the markup that must not drift: `_DateAndLength`,
      `_YourDetails`, `_ErrorSummary`, `_Times`. Move the resource flow onto them
      first and confirm its accessibility tests still pass, so the partials are
      proven by the flow that already met the bar before the new flow uses them.

## 2. The service flow

- [x] 2.1 A service flow rendering date + length → times → details → POST →
      confirmation, over the Core service-booking and availability ports in-process.
      Never over HTTP (the resource flow's rule, same reason).
- [x] 2.2 **Write it for several resources from the start.** A single-role service
      resolves to a collection of one, so `.First()` passes every single-role test.
      No code path may assume one resource, including the confirmation.
- [x] 2.3 Length control per design D5: `Variable` renders the control bounded by the
      service's min/max intersected with what its resources permit; `Fixed` renders
      the length as text and submits it hidden. The field is still sent and still
      validated server-side either way.
- [x] 2.4 Anti-forgery, PRG and input-preserving failure handling on the same terms
      as the resource flow — these requirements are not modified, so the assertions
      are the only thing holding them for the new flow.
- [x] 2.5 **`pinnedResourceId` is never sent** (design D7). Not defaulted, not
      hidden-fielded: absent. Assert no code path sets it.

## 3. Refusals — the substance, not the trimming

- [x] 3.1 Map `conflict` and `service-unavailable` to two distinct visitor-facing
      shapes, from the **stable code** and never from message text.
- [x] 3.2 **The covering test is the pair.** A suite proving "a service books on a
      good day" passes an implementation that renders every refusal as "no times
      available". Provoke both refusals and assert the messages differ *and* that the
      deterministic one does not invite a retry.
- [x] 3.3 **Disclosure:** assert the deterministic refusal names no role, no resource
      type, no capability and no count. The tempting implementation pipes ⑨-2a's
      shortfall text straight through, and it reads plausibly — which is why this
      needs a test rather than a review.
- [x] 3.4 Assert the backoffice shortfall report is unchanged by all of the above.

## 4. Catalogue and entry points

- [x] 4.1 Catalogue listing services and `DirectlyBookable` resources together,
      composed from the existing stores. No new stored notion of what is bookable.
- [x] 4.2 A resource withholding direct booking does not appear. The covering test
      needs a withholding resource *and* a permitting one, or it cannot fail.
- [x] 4.3 The dispatcher of design D2: no id → catalogue; `serviceId` → service flow;
      `resourceId` → resource flow; an explicitly supplied id beats the query.
- [x] 4.4 Assert the existing `Booking` component still works when invoked directly,
      unchanged — someone may already have it in a template.
- [x] 4.5 Assert reaching a service flow directly and by way of the catalogue produce
      the same steps and controls from that point on.

## 5. State in the URL

- [x] 5.1 Service/resource, date and length in the query string; each step linkable
      and bookmarkable.
- [x] 5.2 **Assert contact details never appear in a URL** — including on the PRG
      redirect, which is the place a later change would most plausibly leak them.

## 6. Accessibility — one bar, and it is modified

- [x] 6.1 Every clause of the modified WCAG requirement asserted for the **service**
      flow and the **catalogue**, not only the resource flow. The requirement was
      modified precisely so there is one bar; a test suite that only exercises the
      old flow would leave that modification unenforced.
- [x] 6.2 The catalogue as a grouped set with a `legend` naming what is being chosen.
- [x] 6.3 Render each flow with no author stylesheet and confirm reading order and
      operability, as ⑤ did.

## 7. Spec hygiene

- [x] 7.1 **Guarantee-diff the one MODIFIED requirement by hand.** Accessible,
      semantic markup is three scenarios and seven clauses; all three scenarios are
      carried forward reworded to say "either flow", and every clause is restated.
      Read the sync diff's `-` lines rather than trusting the tool, which compares
      scenario titles and cannot tell a rename from a deletion.
- [~] 7.2 The outward grep, **before and after** sync, for sibling specs this change
      falsifies. Candidates — and put every one of them in the command, which is the
      step that failed on a previous change: "single-resource", "single resource",
      "the flow", "ViewComponent", "resource id", "one resource", "no times".
      **BEFORE pass done at apply**: every term in one command over `openspec/specs`,
      excluding `default-frontend` itself. The only hit this change bears on is
      `services/spec.md:238`, resolved as task 7.5 / design D8. Every other hit is
      about the domain, persistence or the delivery API and is untouched.
      **The AFTER pass is still owed, at sync.**
- [ ] 7.3 The `default-frontend` spec's **Purpose** paragraph describes "a
      no-JavaScript single-resource booking flow". It is not a requirement and so
      will not appear in any delta, and it will be wrong the moment this ships.
      Update it at sync time. **STILL OWED** — apply deliberately did not touch
      `openspec/specs/`, and this is the one edit that no delta will make for you.
- [x] 7.4 Check whether `delivery-api` or `service-booking` say anything about the
      default front end that this change falsifies.
- [x] 7.5 **One specific sibling, found at propose time and not yet resolved.**
      `openspec/specs/services/spec.md:238` requires that introducing services "SHALL
      NOT change the existing single-resource booking path in any way" and that "the
      default front-end for direct-resource booking SHALL be unaffected". This change
      moves that flow's guts — a shared builder, shared partials, a dispatcher in
      front — without intending to change its behaviour. Decide explicitly whether
      that requirement is about **behaviour** (in which case it survives, and task
      1.2's unmodified-tests guard is the evidence) or about **the code path** (in
      which case it needs modifying and this is a MODIFIED requirement the proposal
      does not currently list). Do not resolve it by assuming the convenient reading:
      if it needs modifying, say so and add the delta.

## 8. Verification

- [x] 8.1 Clean `dotnet build --no-incremental` with the TestSite stopped. **The
      warning baseline is now ZERO**, not 38 — the Umbraco 17.6.2 patch cleared every
      NU1903 advisory, so any warning at all is a regression introduced by this change.
- [x] 8.2 Unit, integration and client suites green, including every ⑤–⑨ scenario
      unchanged.
- [x] 8.3 Live: book a multi-role service end to end with JavaScript disabled, and
      read the confirmation for both resources.
- [x] 8.4 Live: provoke both refusals and read the two messages.
- [x] 8.5 Live: the catalogue, and entry directly at a service.
- [x] 8.6 **Launch the TestSite detached** (`Start-Process -WindowStyle Hidden`),
      never as a tracked background task; afterwards check port 44348 for orphans.

## 9. Handover

- [x] 9.1 Record what ⑩-1 inherits and what it must add: the role-level flag deciding
      which role a visitor may choose from, the who control bounded by an
      already-chosen start, and the fact that `pinnedResourceId` is absent rather
      than defaulted.
- [x] 9.2 Record whether the shared partials held, or started taking per-flow flags —
      the risk design D1 names.
