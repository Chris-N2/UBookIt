# Control the delivery API's exposure

Roadmap **0.9.0**. Decisions below were settled with Chris in the explore on 2026-09-12.

## Why

The delivery API registers unconditionally, so every install — including a site using only
the shipped Razor front end — exposes anonymous availability and **booking placement**
endpoints it never asked for and gains nothing from. The prompting concern is concrete: a
site owner discovering an open API they never knew they had. No amount of request
validation can answer it (an anonymous public API has no way to know who is calling —
Origin/Referer are client-supplied, CORS restricts browsers rather than callers, API keys
are out of scope and would sit in public JavaScript anyway); **removing the surface can**,
and honest documentation covers the rest.

## What Changes

- **Two independent switches, both OFF by default**: delivery **reads** (resource and
  service discovery, availability, slots, bookable-starts, retention read) and delivery
  **placement** (anonymous booking placement, direct and via service). Startup-bound
  configuration, like every other uBookIt setting. Both on is the full headless story;
  reads-only serves a site showing availability but taking bookings only through its own
  pages; both off — the default — exposes no anonymous endpoint at all.
- **BREAKING — the default flips.** Today the API is always on; after this change an
  untouched install (fresh or upgraded) serves none of it, and an existing headless
  consumer must set the switches. Taken deliberately: it matches the package's
  off-until-asked philosophy (the emails precedent), it is the answer to the prompting
  concern, and pre-17.0.0 is the last cheap moment. Called out in README and docs as a
  breaking change, not discovered.
- **A disabled direction is absent**: requests receive 404 with nothing distinguishing
  "disabled" from "never existed", and disabled operations do not appear in the delivery
  Swagger document.
- **The shipped front end is unaffected**, as a matter of spec rather than luck: the
  `default-frontend` capability requires the Razor flow to render from the Core ports
  in-process, and a repo-wide search confirms nothing outside the delivery controllers
  references the delivery routes.
- **README gains an up-front security section** and the docs a fuller page: the API is
  anonymous **by design**; why origin validation cannot exist for it; volume defence is
  the host's rate limiting or edge (never claimed as DDoS protection, which no package
  can provide); per-request cost is bounded by `MaxQueryRangeDays` in Core for every
  caller path; and the anonymous-placement risk (junk bookings holding real slots) is
  named honestly, with approval mode (`AutoConfirm` off) as the business-level
  mitigation. Claims guarded per the house `DocumentationAssert` pattern.

## Capabilities

### New Capabilities

*(none — exposure is the delivery API's own concern)*

### Modified Capabilities

- `delivery-api`: gains a requirement that the API is **off until a site turns it on**,
  per direction, with the absent-when-disabled and Swagger behaviour; and the existing
  requirement **"Anonymous access and auth stance"** is modified so its
  reachable-anonymously guarantee is scoped to an enabled direction (its auth-stance
  SHALLs — no backoffice policy, no anti-forgery, body-only identity — carry unchanged;
  guarantee-diff discipline applies to the wholesale replacement).

## Impact

- `UBookIt.Core`: `SiteBookingSettings` (or a sibling settings record) gains the two
  flags; no domain behaviour changes.
- `UBookIt.Web`: the enforcement point for the two directions over the existing delivery
  controllers, plus Swagger document filtering. Exact mechanism is design's question
  (startup route/application-model removal vs a request-time gate).
- `UBookIt.Backoffice`, `UBookIt.Persistence`: untouched.
- Docs: README security section (up front, per Chris), a delivery-API exposure section in
  docs (home decided at design — likely a new `docs/delivery-api.md`, since no page
  covers the API today), `docs/booking-page.md` cross-reference if it mentions the API.
- Tests: direction-gate behaviour both ways, 404 indistinguishability, Swagger hiding,
  and documentation guards.
- **Breaking change**: existing delivery API consumers stop working until the site sets
  the switches. No schema or storage impact; no change to the management API.

## Non-goals

- **No API keys, tokens, or caller authentication of any kind** — out of scope by
  roadmap decision, and impossible to make meaningful for an anonymous public API.
- **No origin validation** (Origin/Referer/CORS-as-security) — documented as unable to
  exist rather than half-built.
- **No per-caller cap on availability queries — declined, with the reasoning recorded**:
  per-request amplification is already bounded by `MaxQueryRangeDays` in Core for every
  caller path, so a per-caller cap would only bound request *volume*, which is rate
  limiting — the thing this change documents as belonging to the host.
- **No rate limiting, throttling, or DDoS claims of any kind.**
- **No change to the management API or the shipped Razor flow.**
- **No per-endpoint granularity** beyond the two directions; no runtime (no-restart)
  toggling.
