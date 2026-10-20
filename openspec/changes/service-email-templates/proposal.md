## Why

Since `17.5.0`/`18.4.0` a service can carry plain-text instructions for a confirmed booker. That
covers a join link or directions. It does not cover a message that should **read differently** for
one kind of service: an online consultation's confirmation is a different email from an in-person
one, not the same email with a paragraph added.

Today a site gets that only by writing one template per message and branching inside it. It can
branch on the service's **name**, which a backoffice rename breaks silently. Or it can branch on a
service **id**, which the models do not carry. Ids would not help much anyway: services live in
uBookIt's own tables, so the same service has a different id on dev, staging and live, and a view
that hard-codes one works on one environment only.

## What Changes

- **A service gains an optional email template set**: a short name such as `remote`, set in the
  service editor and through the Management API (`templateSet`).
  - Trimmed. Blank means none.
  - **Lower-cased on save.** It becomes a folder name, and folder names are case-sensitive on
    Linux hosts and not on Windows. A site that develops on Windows and deploys to a Linux
    container would otherwise find `Remote` working on one and silently falling back on the other.
  - Only `a–z`, `0–9` and single inner hyphens, at most 64 characters. Anything else is rejected
    with a stable failure code. The name is a path segment, so nothing like `../` can reach the
    view engine.
  - Several services may name the same set.
- **Content is looked up in the set first.** For each message, the lookup is
  `Emails/<set>/<Kind>.cshtml`, then `Emails/<Kind>.cshtml`, then uBookIt's own wording.
  - Only *nothing supplied* moves down that chain.
  - **A set view that fails to render goes straight to uBookIt's own wording**, never to the
    general view. The general view may be written for another kind of service. Sending
    in-person directions to an online booker is worse than neutral wording.
  - A set may be partial: each message is resolved on its own.
  - It applies to **both audiences and every message**, the site's own recipients included. A
    set view for the site's own recipients still gets the model with no booker contact details,
    so that guarantee is unchanged.
- **The set is read from the service when the message is composed**, like the instructions. A
  service's set can therefore change for bookings already made.
  - The read happens once per message and feeds both the instructions and the set.
  - It happens only where content can be supplied at all, so a host without the Razor renderer
    pays nothing new.
  - **If the read fails, the message uses uBookIt's own wording.** It cannot tell which set
    applies, and the neutral wording is the safe choice. This changes one sentence of the
    `booking-emails` instructions requirement (see below).
- **Both message models gain two members**: `ServiceId` (`Guid?`, from the booking's recorded
  attribution) and `TemplateSet` (`string?`, as read). A general view can branch on the set name,
  which is the same on every environment, rather than on an id, which is not.
- **`IBookingTemplateRenderer` does not change.** The set travels on the model it already
  receives. A site's own implementation of the interface keeps working and simply ignores sets.
- **The startup report covers sets.** It lists each set a service names and which messages that
  set supplies. A set that supplies nothing is a **warning** that names the expected folder, so a
  typo such as `remte` is found when the site starts.
- **The delivery API does not publish the set.**
- **Both lines**, `main` (17.x) and `dev/v18` (18.x). This is additive public API plus one
  additive column, so it ships in the next **minor** release on each line.

## Non-goals

- **Resource ids on the models, and template sets on resources.** This deliberately narrows the
  2026-10-19 explore decision, which named both service and resource ids. `ResourceNames` skips a
  resource that cannot be read, so a parallel `ResourceIds` list would not line up with it index
  for index. A view pairing the two would name the wrong resource. A site that wants
  resource-specific wording makes a service over that resource and makes the resource not directly
  bookable.
- **A backoffice hint showing which messages a set supplies.** It would need the Management API to
  query the Razor view engine. The startup report covers the typo case.
- **Detecting a misnamed set between restarts.** A set typed in the editor after startup is
  reported at the next start. The docs say so.
- **Editor-editable email content, per-booking meetings, `.ics` attachments.** Unchanged from
  earlier changes.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `services`: **adds** a requirement for the template set: normalisation, the character rule, the
  failure code, and the Management API round-trip. **Modifies** "Workspace editor for a service" so
  that the Details section has the template-set field. Every existing scenario is carried forward.
- `email-templates`: **adds** the per-set lookup with its fall-through and failure rule, the two
  model members, and the startup report for sets.
- `booking-emails`: **modifies** "A confirmed booker is given the service's instructions". Its
  sentence "a failure to read the service SHALL cost the instructions **and nothing else**" stops
  being true where a site supplies content: the failure now also means uBookIt's own wording is
  used. This is a deliberate narrowing, chosen by the maintainer on 2026-10-20. Every other
  guarantee in that requirement is carried forward with its scenario.
- `persistence`: **adds** a requirement for the additive nullable column.
- `delivery-api`: **adds** a requirement that the public service read model does not carry the
  template set.

## Impact

- **Core:** `Service` gains `TemplateSet` and `WithTemplateSet(...)`, alongside the instructions
  and not through `Create`. The new failure code is `service-template-set-invalid`.
  `BookingMessageModel` gains `ServiceId` and `TemplateSet`. `IBookingTemplateRenderer` is
  unchanged.
- **Persistence:** one migration adding a nullable column, the row mapping, and the composer.
  The composer reads the service once per message where a renderer is registered or the booking is
  confirmed, and on a failed read uses its own wording. **The composer gets no new constructor.**
  The one taking `IServiceStore` already exists, and so does the factory in
  `UBookItPersistenceComposer`.
- **Web:** `RazorBookingTemplateRenderer` resolves the set path first and checks the set name
  again before using it as a path. The email boot check reports sets and so needs to list
  services, without ever failing boot.
- **Backoffice:** the request and response models, the editor field with its hint and error
  placement, the localization keys, and both regenerated clients.
- **Docs:** `docs/notifications.md` (sets, the lookup chain, the lower-case rule, the failure
  rule, branching on `TemplateSet` rather than ids, the startup report) and `docs/backoffice.md`.
- **Tests:**
  - the service-read count as a table over audience, booking shape, renderer and status;
  - the renderer's chain, including a failed set view and a set name that is not a valid path
    segment;
  - the boot check;
  - persistence, Management API and delivery API contract tests;
  - the published call-shape guard;
  - client tests for the field.
- **No breaking change.** Every addition is optional, and every existing member keeps its name,
  type and meaning. The one behaviour that moves is the read-failure path above. It affects only a
  site that supplies content, during a database fault.
