# Release-readiness fixes

## Why

17.0.0 — the first full release — must not ship *known* defects into the compatibility
promise. Three items on the deferred-obligations record qualify, all flagged rather than
fixed at the time (Chris, 2026-09-10) and now scheduled deliberately (Chris, 2026-09-14):
the shipped GET forms delete the host page's query string; three-now-seven PII guards fail
on ~2% of full-suite runs because a hex-only needle matches inside a random GUID; and a
recorded management-API defect (a read-modify-write PUT silently clearing
`directlyBookable`) that can no longer be reproduced statically and must be settled live
rather than left claiming to be a defect. This change is the fix half of release
readiness; the release itself (version bump, README truth, numbering policy, publish) is
the next change.

## What Changes

- **Preserve the host page's query string across the shipped GET forms** — decided as
  option (b) of the recorded three (Chris, 2026-09-14): a **configured allow-list of
  parameter names, empty by default**, rendered as hidden inputs by both GET forms
  (`Catalogue.cshtml` and `_DateAndLength.cshtml` — the sweep found the class is two
  forms, not one: the date form already preserves uBookIt's own subject token and
  discards everything else). Empty default means shipped behaviour is unchanged; a site
  opts its own parameter names in. **Blanket preservation of unknown parameters is
  declined with reasoning recorded** — it turns the form into an arbitrary reflector —
  and POST-redirect-GET is declined because it trades away the flow's deliberate
  shareable-URL property without dodging the same reflection question. uBookIt's own
  booking keys are excluded from preservation even if listed, so no control's value can
  be duplicated by a hidden input. Documented where the booking page is documented.
- **Fix the flaky PII guards by redacting the legitimate token, never weakening the
  needle.** Enumerate the class at HEAD (seven `DoesNotContain("Ada", …)` sites exist
  now; the 2026-09-10 table knew three): for every site whose haystack carries a GUID,
  remove the GUID(s) that are legitimately present from the haystack before matching,
  exactly as `BookingEmailTests.No_log_line_carries_a_booker` already does for the
  booking reference. `"Ada"` stays the needle: a payload carrying a bare first name is a
  real leak the guard must still catch.
- **Settle the `directlyBookable` round-trip on the record.** At HEAD both wire models,
  the mapper (both directions) and the client editor carry the flag, and nothing in that
  chain has changed since the 2026-09-10 note — so reproduce it live (management-API GET
  → change one unrelated field → PUT → GET) and either fix what reproduces or record the
  non-reproduction with its likely explanation (the TestSite harness picks the first
  resource by list order, which matches the observed symptom), so the
  deferred-obligations entry stops claiming a live defect either way.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `default-frontend`: **ADDED** requirement — the shipped GET forms preserve a
  site-configured allow-list of the host page's query parameters, empty by default,
  never uBookIt's own keys, values encoded on render. (An addition, not a modification
  of any existing requirement, so no guarantee-diff is owed.)

The PII-guard work changes tests only and the `directlyBookable` item is an
investigation with a recorded disposition; neither changes any requirement. If the live
round-trip DOES reproduce a defect, its fix gets its own delta against
`resource-management` before code is written for it.

## Non-goals

- The 17.0.0 release itself: version bump, README/docs version claims, the
  breaking-change numbering policy, NuGet publish, the GitHub move. Next change.
- Any preservation default beyond empty. Guessing at other sites' parameter names is
  wrong in both directions.
- Preserving query state across the POST step. `BeginUmbracoForm` posts to the page URL;
  whether that URL retains the query is verified during apply and recorded, but the POST
  path is not this change's surface unless verification finds it broken — in which case
  it is reported, not silently widened into scope.
- The other deferred obligations (installation regression guard, DOM test environment,
  email template precedence, etc.) — recorded decisions, unchanged by this change.

## Impact

- `src/UBookIt.Web`: the two GET-form views; a small options class for the allow-list
  (render concern, so it lives in Web, not Core); the view-model layer that hands the
  preserved pairs to the views; `docs/booking-page.md`.
- `tests/UBookIt.Tests.Rendering`: rendering guards for both forms (present, excluded,
  encoded, absent-by-default).
- `tests/UBookIt.Tests`: the enumerated PII-guard sites; unit tests for the preservation
  computation.
- No schema change, no management-API contract change (unless the live round-trip
  reproduces — see above), no client change.
