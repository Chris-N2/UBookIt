# Design — delivery API exposure

## Context

Six delivery controllers derive from `UBookItDeliveryApiControllerBase` (route base
`umbraco/ubookit/api/v{version}`): availability, resources, services, bookings, privacy.
They are discovered as ordinary MVC controllers from the RCL assembly, so they exist on
every install. Directions do not map to controllers — `ServicesController` carries both
service reads and service *placement* — so any per-direction mechanism must work at the
**action** level. Delivery unit tests construct controllers directly (no test host), so
gating must live where those tests can exercise it deliberately rather than silently
bypass it. Settings elsewhere in the package are resolved once at startup
(`SiteBookingSettings` and the notification resolution), and this change keeps that shape.

Settled with Chris (2026-09-12): two switches (reads / placement), both off by default —
an explicit breaking flip of today's always-on behaviour; disabled means absent (404,
hidden from Swagger); per-caller caps declined; README carries the security story up
front.

## Goals / Non-Goals

**Goals:**

- An untouched install serves no anonymous endpoint; each direction turns on
  independently, at startup, by configuration.
- Disabled is indistinguishable from never-existed: 404 with the host's ordinary
  not-found shape, nothing in the Swagger document, no header or body hinting
  "disabled".
- The classification of every delivery action into a direction is total and guarded —
  an action nobody classified must fail a test, not default to exposed.

**Non-Goals:** caller authentication, origin validation, rate limiting or DDoS claims,
per-endpoint granularity, no-restart toggling (all recorded with reasons in the
proposal).

## Decisions

### D1. Absence by startup convention over action selectors, not a request-time filter

An MVC **application-model convention**, registered by the Web composer, runs once at
startup: for every action on a controller deriving from
`UBookItDeliveryApiControllerBase` whose direction is disabled, it clears the action's
selector models and sets `ApiExplorer.IsVisible = false`. An action without selectors is
never route-matched — requests fall through to the host's ordinary 404 — and ApiExplorer
invisibility removes it from the Swagger document. One mechanism, both behaviours, and
the "absent" claim is structural: there is no code path that could answer differently,
no filter to mis-order, and nothing to leak a distinguishing response.

*Alternatives considered.* A controller feature provider (removing controller types)
cannot express directions — `ServicesController` is in both. A request-time resource
filter honours live configuration but must fabricate its own 404 (a second not-found
shape to keep identical to the host's forever), leaves the operations in Swagger unless
a second mechanism hides them, and runs on every request for a decision that cannot
change between restarts. Startup-bound configuration is also the package's established
shape.

### D2. Directions are declared by attribute, and the classification is guarded

Each delivery action carries exactly one of two attributes (e.g. `[DeliveryRead]` /
`[DeliveryPlacement]`), read by the convention. Explicit rather than inferred from the
HTTP verb, because the verb lies here: the privacy read is a GET but so would be
anything added carelessly, and placement is POST but a future POST-shaped read (the
management API already has one) would be silently misclassified.

The guard enumerates the class: a reflection test walks every action on every delivery
controller and fails if any carries zero or two direction attributes — so a new endpoint
cannot ship unclassified, and the failure message says which. Classification of what
exists today: placement = `BookingsController.PlaceBooking` and
`ServicesController.PlaceServiceBooking`; everything else, the privacy/retention read
included, is a read.

### D3. Settings live in `UBookIt.Web`, resolved once at startup

`DeliveryApiSettings { EnableReads, EnablePlacement }`, both `false`, bound from
`UBookIt:DeliveryApi` by the Web composer and consumed only by the convention and its
tests. Not on `SiteBookingSettings`: Core has no consumer — enforcement is entirely a
Web concern — and putting Web-only knobs on the Core aggregate would grow it for
uniformity's sake. Explicit booleans rather than presence-is-the-switch, because unlike
a recipient list there is no natural "content whose presence means yes"; this matches
`SendBookerEmails`, the other pure on/off.

### D4. The Swagger document stays registered, empty when everything is off

The delivery OpenAPI document remains; disabled operations simply are not in it. An
empty document is honest ("this site exposes none of this") and cheaper than
conditionally unregistering Swashbuckle configuration, which would couple this change to
swagger-ui internals for no reader benefit.

### D5. Documentation: README section up front, and a new `docs/delivery-api.md`

No docs page covers the API today. The new page carries: the switches and their
defaults; that the API is anonymous **by design** and what that means; why origin
validation cannot exist (client-supplied headers; CORS restricts browsers, not
callers); that volume defence is the host's rate limiter or edge and the package
claims no DDoS protection; `MaxQueryRangeDays` as the per-request cost bound on every
caller path; the anonymous-placement risk stated honestly with approval mode as the
business-level mitigation; and the declined per-caller cap with its reasoning. The
README gets the short version **in its feature area, not a footnote** (Chris's explicit
ask), including the breaking default-flip for anyone upgrading a headless site. Claims
guarded by `DocumentationAssert` on the guarantees (not mechanisms), wrap-safe.

### D6. The dev TestSite turns both on

`appsettings.Development.json` on the TestSite enables both directions — it is the API's
test bed and is not shipped. The Install Check site gets nothing and thereby exercises
the shipped default.

## Risks / Trade-offs

- [Existing headless consumers break on upgrade] → deliberate and stated everywhere the
  upgrade is described; pre-17.0.0 is the last cheap moment, and the fix is two
  appsettings lines.
- [A future delivery endpoint added without a direction attribute] → the D2 reflection
  guard fails the build's test run and names the action.
- [Selector-clearing relies on MVC internals staying stable] → it is public,
  documented application-model API (`ActionModel.Selectors`), the same surface Umbraco
  itself builds on; a behaviour change would fail the direction-gate tests loudly.
- [In-process tests constructing controllers directly bypass the convention] → true and
  accepted: those tests exercise contract behaviour, not exposure. Exposure is tested
  through the application model itself (build the model, run the convention, assert
  selectors/visibility) plus the live check against the running TestSite.
- [Site owners flip `EnableReads` and assume placement followed] → the two settings are
  documented side by side and the docs state each direction's scope by endpoint list.

## Migration Plan

No schema or data change. Sites using the delivery API add the two settings; the
release notes / README call the flip out. Rollback is unsetting them.

## Open Questions

None blocking.
