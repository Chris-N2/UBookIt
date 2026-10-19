## Why

A booker often needs something that only applies to the service they booked, such as a link to
join an online consultation, or directions and parking for an in-person one. Today the only way
to say it is to supply a Razor template for the whole message. That template has to work out
which service the booking is for from the service's **name**, which breaks silently when the
service is renamed.

The maintainer's own site shows the gap. It has two services, "Online consultation" and
"In-person consultation", both served by one resource, so either booking blocks the other. That
already works. What the site cannot do is send the online booker the join link without writing a
template.

## What Changes

- **A service gains optional "Instructions for the booker".** This is plain text, set in the
  service editor and through the Management API.
  - Leading and trailing whitespace is trimmed, and a value that is only whitespace means none.
  - Line breaks are kept.
  - The length is validated with a stable failure code. It never fails at the database.
- **The instructions are stored in an additive, nullable column** on the service table. The
  migration only adds that column.
- **The instructions reach the booker only when the booking is confirmed** at the moment the
  message is composed:
  - the placement message on an auto-confirming site;
  - the confirmation message;
  - the move message for a confirmed booking.

  They are never sent with a request still awaiting approval, a decline or a cancellation, and
  never in a message to the site's own recipients. That keeps a site that requires approval from
  handing a join link to people it has not yet accepted.
- **uBookIt's own wording includes them**, so a site needs no template to use the feature.
- **`BookerMessageModel` gains `BookerInstructions`**, so a supplied template can place them
  itself. It is absent wherever the message does not carry them, so a view written before the
  member existed renders unchanged.
- **The instructions are read at send time**, not recorded at placement. A move message sent
  after the service's link changed carries the new link. This is unlike the service **name**,
  which keeps its recorded-at-placement rule.
- **The delivery API never exposes them.** A standing join link published on an anonymous
  endpoint stops being something only bookers hold.
- **Both lines**, `main` (17.x) and `dev/v18` (18.x). This is additive public API, so it ships in
  a **minor** release, aimed at `17.5.0`/`18.4.0`.

## Non-goals

- **Instructions on a resource.** Decided at explore as service only for now. A booking placed
  directly against a resource carries none.
- **Showing the instructions on the booking confirmation page.** It is an obvious follow-on, but
  it brings front-end work and its own review surface.
- **Templates per service** (`Emails/<set>/<Kind>.cshtml`) and service or resource ids on the
  models. That is the next change, `service-email-templates`.
- **Creating a meeting per booking** (Teams through Microsoft Graph, `.ics` attachments). A fixed
  link in the instructions covers the stated need without a Microsoft dependency.
- **Rich text or markup.** The text is plain. A URL in it is linked by mail clients, and a site's
  own HTML template encodes it like any other value.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `services`: **adds** a requirement for the instructions: the domain rules, the validation code,
  and the Management API round-trip. **Modifies** "Workspace editor for a service", whose Details
  section names only the service name.
- `booking-emails`: **adds** a requirement stating when the booker's message carries the
  instructions, and the narrowing for supplied content.
- `email-templates`: **adds** a requirement for `BookerInstructions` on the booker's model.
  **Modifies** "A site can supply the content of a message without owning its delivery" (added at
  sync, after QA): its sentence "a site that supplies nothing SHALL be unaffected … exactly what it
  sent before this capability existed" became false as written for a site that writes instructions
  and supplies no views. It is narrowed to "unaffected *by this capability*" — the package's own
  wording may grow with the capabilities that define each message. A deliberate narrowing of a
  sentence that was over-broad, not a removal of a guarantee.
- `delivery-api`: **adds** a requirement that the service read model never carries the
  instructions.
- `persistence`: **adds** a requirement for the additive nullable column.

## Impact

- **Core:** the `Service` aggregate gains the instructions through an additive method, not a
  changed `Create` signature (see `design.md`). `BookerMessageModel` gains one optional member.
- **Persistence:** one EF migration adding a nullable column, the row mapping, and the message
  composer, which reads the service at send time.
- **Backoffice:** the Management API request and response models, the service editor (a textarea
  in Details) and its localization keys. Both clients are regenerated.
- **Web:** none. The delivery models are unchanged, and a test pins that.
- **Docs:** `docs/notifications.md` covers what the messages carry and what a template is given.
  `docs/backoffice.md` covers the service editor.
- **Tests:** unit tests for the domain and composer rules, the call-shape guard for `Service`,
  persistence integration (round-trip and migration), Management API and delivery API contract
  tests, and client tests for the editor field.
- **No breaking change.** Every addition is optional, and every existing member keeps its name,
  type and meaning.
