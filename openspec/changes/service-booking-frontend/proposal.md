## Why

The package can book services and cannot show anyone how. Every service capability
built since ⑥ — roles, counts, capabilities, multi-role assignment, the pin — is
reachable only through the anonymous delivery API. A site owner who installs uBookIt
and configures "Massage: one room, one therapist" has no way to put that on a page.

The shipped front end still books exactly what it booked in ⑤: one resource, by id.
That is now the *narrower* of the two things the domain supports, and it is the only
one a visitor can reach without someone writing a client.

## What Changes

- **A second booking flow, for services**, alongside the existing single-resource
  flow. Same shape — choose a date and length, see the starts that admit it, give
  contact details, submit, confirm — over `IServiceBookingService` instead of
  `IBookingService`, and reporting the several resources a service resolves to
  rather than one.

- **A visitor picks what to book, and the two kinds look alike.** A catalogue lists
  the services and the directly-bookable resources together, because "Massage" and
  "Meeting Room A" are both just things to book; which one is a service is our
  concern, not the visitor's. Both kinds are already published, so this composes
  public reads rather than adding a source of truth.

- **Entry at any point in the flow.** A site owner with one service links straight
  to it; one with several renders the catalogue. The catalogue is therefore optional
  rather than a mandatory front door — a site whose booking page says "Book a
  massage" should not make a visitor choose "massage" from a list of one.

- **A service that cannot be booked says which kind of cannot.** `conflict` (busy
  now, try another time) and `service-unavailable` (this can never be booked as
  configured) are already distinct in the domain and must stay distinct on the page.
  Rendering them alike is the failure the withholding-resource requirement exists to
  prevent, arriving through a third door.

- **The visitor-facing message SHALL NOT carry the pool diagnostic.** ⑨-2a's
  shortfall report names roles and counts, and exists for the backoffice. A visitor
  is told a service cannot be booked, not which role is short of what.

- **Booking length, accessibility, anti-forgery, PRG, and input-preserving failure
  handling are the same for both flows**, and the WCAG 2.2 AA requirement is
  restated to bind both rather than "the flow".

- **No resource is named to the visitor.** Every service booking this change places
  is unpinned — `pinnedResourceId` is never sent. See Non-goals.

- **BREAKING (unpublished): `BookingViewComponent`'s constructor changes** from
  `(IResourceStore, IAvailabilityQueryService, SiteBookingSettings, TimeProvider)`
  to `(ResourceBookingFlow)`, and the type now requires the rendering composer to
  have run. Called out because the conventions require it, not because anything is
  known to break: the component is resolved from the container and invoked by name
  from a template, which is the only documented way to use it, and both are
  unchanged. Only code constructing it directly is affected — and the package is
  unpublished, so no such code exists outside this repository. The alternative,
  keeping the old constructor as a shim, would mean two ways to build the same
  component and a second wiring to keep in step.

## Capabilities

### New Capabilities

None. The default front end is one capability and gains requirements; the delivery
API and service booking are untouched.

### Modified Capabilities

- `default-frontend`: gains the service flow, the catalogue and entry points, and
  the honest-refusal rules for a service that cannot be booked. The WCAG requirement
  is modified to bind both flows; every other existing requirement is untouched and
  continues to describe the resource flow.

## Non-goals

- **Choosing who fulfils the booking.** Explored and deliberately deferred to a
  follow-up (⑩-1). The pin names the *booking*, not a role, so for a multi-role
  service the honest list of pinnable resources includes the rooms — a choice no
  visitor wants to make, offered next to the one they do. Doing it properly needs a
  site owner to mark which role visitors may choose from, which is a domain,
  persistence, backoffice and delivery change; bundling it would hold the flow
  hostage to it. This change therefore books "anyone available", always, and
  `pinnedResourceId` stays exercised by headless consumers meanwhile. **The pin
  control is the reason ⑩-1 exists; it is not forgotten.**

- **JavaScript.** Unchanged: server-rendered, standard form submissions, every step
  a round trip. The existing flow's no-JS guarantee is the baseline, not a stretch
  goal, and the catalogue and service steps meet it the same way.

- **Overridable styling.** Chris has brand assets earmarked as a test fixture for
  making the default rendering restyleable without forking the views. Real, and a
  separate concern from *what* is rendered; raise it once this flow exists to
  restyle.

- **A "starts at which this resource can be assigned" query.** Still has no consumer
  and still would not, since the flow chooses time before who (resource-pin design
  D6). ⑩-1 does not need it either: it filters *who* at an already-chosen start.

- **Changing the resource flow.** Its requirements are not modified, its behaviour
  is not modified, and the change is expected to leave its tests untouched.

## Impact

**`UBookIt.Web` rendering** — the substance of the change. A service flow beside the
existing resource flow, a catalogue, and shared building blocks below the seam. The
existing `BookingViewComponent`, `BookingSurfaceController` and `BookingFormBuilder`
gain siblings; the markup most likely to be got subtly wrong — date and length, your
details, the error summary — is rendered from one place by both flows rather than
copied, because two copies drift and only one gets fixed.

**Core, Persistence, Delivery API, Backoffice** — unchanged. Everything this change
renders is already published or already reachable in-process. No new endpoint, no
migration, no new domain type. **This is the argument that the change is rendering
only**; if it stops being true during apply, that is a signal the scope has drifted
and belongs in a follow-up rather than in a widened ⑩.

**Spec risk is low but not zero.** Almost all of it is ADDED requirements, which
cannot silently delete a guarantee. The one MODIFIED requirement — accessibility —
is three scenarios and is modified deliberately, so that one WCAG bar binds both
flows rather than two bars drifting apart. Its guarantees must still be diffed by
hand, per the standing rule.

**Tests** — the new ground is a flow that books several resources at once and a
refusal that must distinguish "not now" from "not ever". A suite that only proves a
service books on a good day would pass an implementation that reports every failure
as "no times available", which is precisely the defect this project has now shipped
guards against three times.
