# Design — release-readiness-fixes

## D1 — The allow-list is Web-layer configuration, bound at startup

`FrontendSettings` in `UBookIt.Web`, section `UBookIt:Frontend`, one member:
`PreservedQueryParameters` (list of parameter *names*, default empty). It mirrors
`DeliveryApiSettings`' shape and home: this is a rendering concern, so it does not touch
`SiteBookingSettings` in Core — Core stays free of "how the page is drawn" knowledge.
Empty default means an unconfigured site renders byte-identical markup to today, which is
the property that lets this land beside 17.0.0 without being a behaviour change for
anyone who has not asked for it.

## D2 — Preservation is a pure function, computed once, at the component boundary

`PreservedQuery.Compute(query, allowList)` — a static pure function in
`UBookIt.Web.Rendering` taking the request's query pairs and the configured names,
returning ordered (name, value) pairs:

- **Name matching is `OrdinalIgnoreCase`**, matching how ASP.NET query lookup behaves —
  a site that configures `Culture` and sends `culture` should not silently lose it.
- **Multi-valued parameters keep every value**, each as its own pair, in request order —
  a preserved parameter must round-trip exactly, not be flattened to its first value.
- **uBookIt's own query keys are excluded even if listed.** A hidden input duplicating a
  live control's name would submit both values and leave the winner to model binding —
  the exact accident `BookingKeys` documents against `ubDate`/`ubDateOther`. The
  exclusion set is derived from `BookingKeys` itself (the `*Query` constants), not
  restated, and a guard asserts the derived set equals the reflected set so a future key
  cannot silently escape it.
- **Unlisted parameters are dropped.** Blanket preservation is declined on the record:
  every preserved value is visitor-controlled input echoed into the markup, and an
  unbounded reflector is not made safe by encoding — the bound is the point. The
  encoding itself comes free: the pairs render through Razor `@`, like every other
  attribute value in these views.

The ViewComponents compute the pairs from `Request.Query` and pass them through
`BookingFlowInput` / the catalogue build — the same route every other request-derived
value already takes — so the flows and view models stay `HttpContext`-free, and the
rendering suite can drive every case by setting the model.

## D3 — One partial renders it for both forms

`_PreservedQuery.cshtml` renders the pairs as `<input type="hidden">` and is included by
both GET forms (`Catalogue.cshtml`, `_DateAndLength.cshtml`). One implementation, so the
two forms cannot diverge — the date form already diverged from the catalogue once (it
preserves the subject token; the catalogue preserves nothing), and that divergence is
half of why this class existed. The POST step is out of scope (proposal Non-goals) but
its behaviour is *verified* during apply and recorded: `BeginUmbracoForm` posts to the
page URL, and whether that URL retains the query decides whether the fix is complete for
the whole flow or the POST needs its own follow-up entry.

## D4 — The PII-guard fix redacts the token, never the needle

For every `DoesNotContain("Ada", …)` site whose haystack legitimately carries a GUID,
the *known* GUID(s) — the booking/resource ids the test itself created — are replaced
with a placeholder before matching, exactly as `BookingEmailTests` already replaces the
booking reference. Deliberately NOT "strip everything GUID-shaped": redacting only the
tokens the test put there keeps the guard able to see a leak that happens to be
GUID-shaped, and makes each redaction a statement of what is legitimately present.
The class is enumerated at HEAD by sweep (seven sites exist today; the 2026-09-10 table
knew three), each classified in tasks as fixed or out-of-class with its reason, and the
sweep re-run after — a finding enumerates a sample.

## D5 — `directlyBookable` is settled by measurement, with both outcomes writable

Protocol, on the running TestSite: management-API `GET /resources/{id}` → alter one
unrelated field → `PUT` the read-back body → `GET` again → assert the flag survived; then
the same through the backoffice editor if the API round-trip is clean (the two
reproductions were manual, so the editor path must be measured before declaring
non-reproduction). If it reproduces: stop, write the delta against `resource-management`,
fix under it. If it does not: the deferred-obligations entry is rewritten to record
non-reproduction, the verified-at-HEAD chain, and the likely original cause (the TestSite
harness picks the first resource by list order — same symptom, no defect). Either way
the record stops claiming something unverified.

## D6 — What is deliberately not built

- No per-form or per-component allow-list. One site-wide list: the parameters a page
  carries (analytics, culture, paging) are properties of the site, not of which form
  happens to be on the page.
- No preservation of fragment or path state — a GET form never had those.
- No attempt to preserve across the confirmation PRG redirect in this change; measured
  and recorded per D3, acted on only if measurement says the surviving gap is real.
