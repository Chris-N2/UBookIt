## Context

- **The renderer seam** (`src/UBookIt.Core/Notifications/BookingTemplateRenderer.cs`) is
  `IBookingTemplateRenderer.RenderAsync(BookingMessageKind kind, BookingMessageModel model, CancellationToken)`.
  It is public and frozen at 17.0.0. Nothing registers a default: `UBookIt.Web` registers
  `RazorBookingTemplateRenderer`, and the composer takes it nullably.
- **`RazorBookingTemplateRenderer`** (`src/UBookIt.Web/Emails/`) resolves one path per kind:
  `TemplateFolder` = `~/Views/Partials/UBookIt/Emails/`, and `PathFor(kind)` = `{folder}{kind}.cshtml`.
  It returns `Rendered`, `NotSupplied` or `Failed`, and never throws. `IsSupplied(kind)` serves the
  boot check.
- **The composer** (`src/UBookIt.Persistence/Notifications/BookingMessageComposer.cs`):
  - has two public constructors. The second, taking `IServiceStore` last, was added by
    `service-booker-instructions`, and `UBookItPersistenceComposer` registers it through an
    explicit factory;
  - reads the service only in `ForBookerAsync`, only for a `Confirmed` booking
    (`BookerInstructionsAsync`), and swallows any failure except cancellation;
  - already maps the renderer's `Failed` to its own wording, with one log line.
- **`ServiceAttribution(Guid ServiceId, string DisplayName)`** is recorded on the booking at
  placement. So `ServiceId` survives a deleted service.
- **`IServiceStore`** has `GetAsync(id)` and a paged `ListAsync(skip, take)`.
- **`ServiceRowMapper.ToDomain`** chains `.WithBookerInstructions(row.BookerInstructions).Value`.
- **The resource-read cost theory** (`BookingTemplateCompositionTests.What_a_message_costs_in_store_reads`)
  covers audience × shape × renderer. It is built on the 3-parameter constructor, so it sees no
  service store at all.
- **`UBookItEmailTemplateBootCheck`** is a public sealed class. It is a synchronous
  `INotificationHandler<UmbracoApplicationStartedNotification>` with a public `Run()` that returns
  `IReadOnlyList<(BookingMessageKind, bool)>`.

## Goals / Non-Goals

**Goals:**
- A site can give one service, or a group of services, its own email content per message without
  branching on names or ids.
- No published member changes shape. In particular, `IBookingTemplateRenderer` keeps its single
  method.
- The set name can never steer the view engine outside the email folder.
- A failing set view, or a service that cannot be read, never yields another service's wording.

**Non-Goals:** resource ids and resource sets; a backoffice preview of what a set supplies;
re-checking sets between restarts. See `proposal.md`.

## Decisions

### 1. The set travels on the model; the renderer interface is untouched

`BookingMessageModel` (the abstract base, so both audiences get them) gains two members:
- `public Guid? ServiceId { get; init; }`, from `booking.Service?.ServiceId`;
- `public string? TemplateSet { get; init; }`, from the service as read.

`RazorBookingTemplateRenderer` reads `model.TemplateSet`.

*Rejected:* a default interface method `RenderAsync(kind, model, set, ct)` that forwards to the
old one. That is two public overloads for one idea, and an overload has already broken source
compatibility once (2026-10-11).

*Rejected:* a second interface `IBookingTemplateSetRenderer`. Two registrations would have to
stay in step, which is the ordering risk the nullable-injection design exists to avoid.

*Accepted cost:* a site's own `IBookingTemplateRenderer` ignores sets until it reads the member.
The docs say so. That is the right default for code the package did not write.

### 2. The domain rule lives on `Service`, beside the instructions

`Service` gains:
- `public string? TemplateSet { get; }`;
- `public DomainResult<Service> WithTemplateSet(string? templateSet)`;
- `public static DomainResult<string?> NormalizeTemplateSet(string? templateSet)`, which both the
  instance method and the API validator use, so one request can report every failure (the same
  pattern as `NormalizeBookerInstructions`);
- `public static bool IsValidTemplateSetName(string? name)`, the predicate the renderer re-checks
  against (decision 4);
- `public const int TemplateSetMaxLength = 64`, `public const string TemplateSetField = "templateSet"`;
- `FailureCodes.ServiceTemplateSetInvalid = "service-template-set-invalid"`.

Normalisation runs in this order: trim; empty becomes `null`; `ToLowerInvariant()`; then
validation against `^[a-z0-9]+(?:-[a-z0-9]+)*$` and the length. Lowering happens **before**
validation, so `Remote` is accepted as `remote` and `Café` is still rejected. One oddity is
harmless: the Kelvin sign `K` (U+212A) lowers invariantly to an ASCII `k`, so it ends up valid.

`Create` is unchanged, `WithBookerInstructions` copies the set across, and `WithTemplateSet`
copies the instructions. `PublishedCallShapeTests` gains each new public member.

*Rejected:* rejecting upper case instead of lowering it. The maintainer chose lowering on
2026-10-20. The editor shows the stored value after a save, so the folder name the editor must
create is never hidden.

### 3. The composer reads the service once per message, and a failed read means its own wording

A private `ReadServiceAsync(booking, ct)` replaces `BookerInstructionsAsync` as the only service
read. It returns one of three things: *not read*, *read* (`Service?`, null when deleted), or
*failed*. Both `ForBookerAsync` and `ForSiteAsync` call it.

It reads **only when** `_services` is not null, the booking has a `ServiceAttribution`, and
either `SupportsTemplates` holds or this is a booker message for a `Confirmed` booking. Any other
case is *not read*.

| audience | shape | renderer | status | service reads |
|---|---|---|---|---|
| booker | direct | any | any | 0 |
| booker | service | none | Confirmed | 1 |
| booker | service | none | Requested | 0 |
| booker | service | registered | any | 1 |
| site | direct | any | any | 0 |
| site | service | none | any | 0 |
| site | service | registered | any | 1 |

That is at most **one** read per message. Instructions and set come from the same read, so they
cannot disagree.

How each outcome is used:
- **Instructions** = `Confirmed && read ? service?.BookerInstructions : null`. This is today's rule
  exactly; it is now derived from a read that may happen for other reasons.
- **Set** = `read ? service?.TemplateSet : null`.
- **Failed** logs a warning once, with the booking id only. The message then uses the composer's
  own wording **without calling the renderer**. Where `SupportsTemplates` is false, the fallback
  was going to be used anyway, so the only visible effect is the missing instructions, as today.
- **A composer built through the 17.0.0 constructor** has no service store, so the result is
  *not read*. The set is null, `ServiceId` is still set, and lookup proceeds from the view for
  every service. A store that is absent is not a read that failed.

The failure path is new, so the existing resource-read theory is kept as it stands, and a
**second theory** asserts the table above. It runs over audience × shape × renderer × status,
uses a counting service store, and uses the same total-dispatch-plus-subject pattern. The
expectations differ between the two audiences, so a dispatch collapsed to one method fails on
counts as well as on subject. The resource-read theory also gains a 4-parameter-constructor
variant, to prove the new read did not move resource reads.

### 4. The renderer's chain, and the set name checked again

`RazorBookingTemplateRenderer.RenderAsync`:

1. If `model.TemplateSet` is not null:
   - if `!Service.IsValidTemplateSetName(set)`, log an error (kind and set; the set is site
     configuration, not personal data) and return `Failed`. The model is public and anything can
     construct it, so the renderer does not trust the composer to have validated the name;
   - find `SetPathFor(set, kind)` = `{TemplateFolder}{set}/{kind}.cshtml`;
   - if found, render it. Any exception means `Failed`, and the chain stops there;
   - if the lookup itself throws, the result is `Failed`.
2. Otherwise, or where the set has no view, the existing `PathFor(kind)` path runs unchanged.

New public members are `SetPathFor(string templateSet, BookingMessageKind kind)` and
`IsSuppliedInSet(string templateSet, BookingMessageKind kind)`. They get **new names, not
overloads** of `PathFor`/`IsSupplied`, for the reason in decision 1. Both throw
`ArgumentException` on an invalid name. They are not on the sending path, which goes through
`RenderAsync`, and a caller passing a bad name is a programming error.

Because the composer already turns `Failed` into its own wording, decision 4 of the explore
("a failing set view goes to the built-in wording") needs no composer change.

### 5. The set report is a separate boot check

A new `UBookItEmailTemplateSetBootCheck` is not folded into the existing class: changing that
class's interface, constructor or `Run()` return type would change a published shape.

**Amended at apply (2026-10-20, Chris's decision).** As first designed, it handled
`UmbracoApplicationStartedNotification`. `RunUBookItMigrations` handles the same notification, and
nothing controls the order. The live check saw the set check run first on the start after an
upgrade: its service read failed on the not-yet-added column, logging two database errors and the
"could not be checked" warning. `UBookIt.Web` references only Core, so it cannot order itself after
the persistence composer, and Core references nothing, so it cannot declare an Umbraco
notification. So:
- Core gains `public interface IUBookItStorageReadyHandler { Task OnStorageReadyAsync(CancellationToken); }`
  (`UBookIt.Core.Stores`), free of Umbraco types;
- `RunUBookItMigrations` takes `IEnumerable<IUBookItStorageReadyHandler>` and calls each one after
  migrating and reporting. A failed migration rethrows before reaching them. Each handler is caught
  on its own, except for cancellation, and logged, so one cannot fail boot or stop the next;
- the set check implements that interface and is registered with
  `AddScoped<IUBookItStorageReadyHandler, UBookItEmailTemplateSetBootCheck>()`.

*Rejected:* a notification type in Persistence, which would make `UBookIt.Web` reference
Persistence and EF Core. *Rejected:* accepting and documenting the noise. The new interface is
additive public API, frozen once shipped.

What it does:
- skips entirely unless `IRuntimeState.Level == RuntimeLevel.Run`;
- pages through `IServiceStore.ListAsync` and collects distinct non-null `TemplateSet` values in
  ordinal order;
- for each set, asks `IsSuppliedInSet` for every kind;
- logs **Information** for a set that supplies anything, with both halves (supplied and not), in
  the existing check's style;
- logs a **Warning** for a set that supplies nothing. The warning names the folder and says that
  set folders are lower case;
- with no sets named, logs one Debug line;
- on any exception except cancellation, logs one Warning ("sets could not be checked") and
  returns.

It never touches the existing check's output. A public `RunAsync()` returns the per-set results
for tests.

### 6. Persistence

- **Migration `AddServiceTemplateSet`:** adds `templateSet nvarchar(64) NULL` to the service
  table, following the existing column casing (apply verifies it against the snapshot). There is
  no index.
- **`ServiceRow.TemplateSet`** is a nullable string.
- **`ToDomain`** maps through `Service.NormalizeTemplateSet`. On failure the set is `null`, and
  the mapper never calls `.Value` on a failed result. The instructions mapping does call `.Value`,
  and a stored over-long value would throw there. This change does not alter the instructions
  mapping (out of scope), but the set mapping deliberately does not copy that pattern. A comment
  says why.
- **`ApplyScalars`** writes the set.
- **Guard:** the migration's `Up` operations are exactly one `AddColumnOperation`, read from the
  operations rather than the source text.

### 7. Management API and the editor

- **API:** the request and response models gain `templateSet`, which is always present in the
  response. The create and update validators call `NormalizeTemplateSet` independently of
  `Create`, so `service-name-required` and `service-template-set-invalid` arrive together.
- **Editor:** in the Details group, after the instructions, a native `<input type="text">`.
  It is native for the same reason as the instructions `<textarea>`: `aria-describedby` cannot
  reach a `uui-input`'s inner control. It has:
  - the label **Email template set**;
  - a hint, linked by `aria-describedby`: *Optional. Emails for this service use the views in the
    folder `Views/Partials/UBookIt/Emails/<set>/` where they exist. Saved in lower case; letters,
    numbers and hyphens only.*;
  - the error for `templateSet` placed against it, with `aria-invalid`, and the error id added to
    `aria-describedby`;
  - after a successful save, the form state is refreshed from the response, so `Remote` shows as
    `remote`.
- **Localization:** English keys added. The localization fallback covers other cultures.
- **Clients:** regenerated on each line.

### 8. The delivery exclusion is checked on the wire

This is the same technique as `service-booker-instructions` decision 6. A service whose set is a
distinctive marker (for example `zz-marker-set-7q`) is read through `GET /services/{id}` and
`GET /services`, and the marker must appear in neither body. A mutation copying the set into any
read-model member must turn it red.

### 9. Docs

`docs/notifications.md` gains a "Template sets" section covering:
- the folder layout and the lookup chain;
- the lower-case rule and why;
- the two failure rules (a failing set view, and an unreadable service, both use uBookIt's own
  wording);
- branching on `Model.TemplateSet` rather than `Model.ServiceId`, and why;
- the startup report, and that it is taken at startup;
- that a custom `IBookingTemplateRenderer` must read `TemplateSet` itself to support sets.

`docs/backoffice.md` covers the field.

### 10. Both lines

Applied on `main` and ported to `dev/v18` in the same sitting. The client generator and the
migration scaffolding are re-run on v18. The migration must be equivalent across lines, so a site
upgrading from 17.x to 18.x finds the column exactly once. The release note says that a PUT
omitting `templateSet` clears it, as with `bookerInstructions`.

## Risks / Trade-offs

- **A database fault costs a templated site its own wording for that message.** This was chosen
  over sending wording meant for another service. The fault is logged, and the message still
  carries the reference and the time.
- **One more service read per message for every service booking on a host with the renderer**,
  which in practice means every site using `UBookIt.Web`. It is a single-row primary-key read
  with the instructions read folded into it. The cost theory pins it at exactly one, so a second
  read cannot creep in.
- **A typo'd set is caught only at startup.** A set entered after startup falls back silently
  until the next restart. That is accepted for now; the documentation says so, and a backoffice
  preview is a possible follow-on.
- **Views in an RCL assembly** are found through the view engine, so set folders inside an
  assembly work the same as on disk. The boot check asks the view engine, so it sees them too.
  Case still matters to the view engine's file providers on Linux, which is the reason for the
  lower-case rule.
- **The scenario-heading diff cannot see a changed scenario body.** `booking-emails`'s
  "The service cannot be read" keeps its heading but now says "on a site that supplies no
  content". This was checked by hand, and the other case has its own new scenario.
