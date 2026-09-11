# email-templates — design

## Context

`BookingMessageComposer` (`UBookIt.Persistence`) turns a booking and a `BookingEvent` into a
`BookingMessage(Subject, Body)` in plain text. It decides the audience's wording, resolves what
was booked (a service's snapshot name, else the claimed resources' names, else nothing), formats
the interval in the booking's own zone with invariant culture, and — for the internal audience —
withholds every booker contact detail and substitutes a backoffice link. Those decisions are
guarded by tests and by two capabilities' requirements.

Three facts established by reading source rather than assuming, each of which closes a design
question before it can be asked:

1. **`SendEmailNotification` cannot change wording.** Its `Message` is an immutable converted
   copy; the only lever is `HandleEmail()`, which makes `EmailSender` return without sending. So
   the existing seam does not make this feature redundant.
2. **There is no multipart/alternative through `IEmailSender`.** `EmailMessage` has one `Body`
   and an `IsBodyHtml` flag; `ToMimeMessage` sets `HtmlBody` **or** `TextBody`. An HTML template
   therefore has no plain-text part, and that is a platform limit, not a scope decision.
3. **Umbraco core has no email templating to copy.** Its four emails are HTML documents inside
   CDATA in localization XML with `%n%` tokens. Umbraco **Forms** invented the Razor-partial
   convention this change follows, and ships it in-package.

Two constraints from this repository:

- `UBookIt.Persistence` references Core and nothing else, and the email path lives there so a
  headless consumer gets email without the Razor front end. Razor cannot go there.
- A composer-ordering assumption already produced a CRITICAL here (`UBookItThemeRegistration`
  records it, and concludes "the mechanism is still not trusted" — hence its boot check).

## Goals / Non-Goals

**Goals:** a site replaces any of six message bodies with a Razor partial, without inheriting
delivery; the package's data-protection guarantees survive that; the published contract is fit
to be frozen at 17.0.0; and the feature is invisible — in behaviour and in cost — to a site that
uses no templates.

**Non-Goals:** editor-facing content, theme integration, multipart bodies, attachments,
per-recipient culture, layouts/sections as a promise, any change to when or to whom messages go.

## Decisions

### 1. The seam is rendering only, and it is entered through a nullable dependency

```
UBookIt.Core         IBookingTemplateRenderer        ← port
                     BookerMessageModel              ← frozen 17.0.0
                     InternalMessageModel

UBookIt.Persistence  BookingMessageComposer          ← ALL policy stays here
                       audience · erased booker · what-was-booked · PII
                       builds the model
                       asks IBookingTemplateRenderer? ─── null, or no template
                       falls back to today's plain text ◄──┘

UBookIt.Web          RazorBookingTemplateRenderer    ← registered here
                     UBookItEmailPage<TModel>
```

**Why not swap the composer.** A Razor implementation of the whole composer would move the
erased-booker check, the PII rules and the what-was-booked fallback into template-land. All are
tested where they are; none should become a template author's responsibility.

**Why nullable injection rather than replacement.** Nothing is replaced, so nothing depends on
composer order. `UBookIt.Persistence` registers no renderer at all; `UBookIt.Web` registers one;
the composer resolves `IBookingTemplateRenderer?`. Precedent is in the platform: Umbraco's
`EmailSender` injects `INotificationHandler<SendEmailNotification>?` purely to detect presence,
and that is what makes `CanSendRequiredEmail()` answer correctly — a fact this package's own
gating already depends on.

Alternative considered and rejected: `AddUnique` from the Web composer, ordered with
`ComposeAfter`. It works only while the ordering holds, and `UBookItThemeRegistration` records
in detail why ordering assumptions are not trusted in this repository.

### 2. Two models, because the guarantee must be structural

`booking-emails` guarantees an internal message carries no booker name, address or telephone
number — because a configuration-file address list is not the **Sensitive data** group, and
routing personal data to it bypasses a control the package built deliberately.

With one model and a nullable booker, that guarantee would quietly become *"unless a site writes
a template"*. `InternalMessageModel` therefore has **no member** for contact details. A template
cannot render what it cannot reach, and the compiler enumerates the sites a reviewer would
otherwise have to — the same reasoning `Booker.Contact`'s nullability and `PlacedBooking` already
record.

A site that genuinely wants a booker's details in internal mail still can: subscribe to
`BookingPlacedNotification` and send its own message. What it cannot do is get the package to do
it.

### 3. The models publish structure, and double as a future token vocabulary

`ServiceName` plus a `ResourceNames` collection, not a joined string — a service booking resolves
to several resources and `default-frontend` requires naming all of them, so a template must be
able to loop. That looping is precisely why Razor was chosen over localization keys, and
pre-flattening takes it back.

`LocalStart`/`LocalEnd` as `DateTimeOffset` **already converted to the booking's own zone**, plus
`TimeZoneId`. Converted by the package because getting it wrong is a real defect and the zone
rule is a requirement; formatted by the template because presentation is the author's. The
invariant-culture formatting the composer does today remains the fallback's business only.

**These members are the token vocabulary a later editor UI would expose.** They are named for a
reader, not for the shipped views' convenience, and this is why: 17.0.0 freezes them.

### 4. A typed base page, carried on HttpContext.Items

`UBookItEmailPage<TModel> : RazorPage<TModel>` exposes `Subject` and `IsHtml` as typed
properties. The author writes `@{ Subject = "…"; IsHtml = true; }`; the renderer reads the values
back off the context it created.

Typed properties because a magic-string key is a silent failure and this contract is frozen.

**The transport is `HttpContext.Items`, and this paragraph originally said `ViewData` — which
apply proved false.** The reasoning had been that the renderer owns the `ViewDataDictionary` it
passes in, so reading it back is reliable. It is not: MVC activates a `RazorPage<TModel>` with a
**copy**, so a template's `Subject = "…"` lands in the copy and never reaches the renderer's
scope. A stated subject silently became no subject, and only a round-trip test showed it. The
context is a single shared instance and the renderer creates a fresh one per message, so nothing
leaks between renders. The author's surface is unchanged — two typed properties either way.

Requiring `@inherits` is precedent-compatible — Forms requires
`@inherits UmbracoViewPage<FormsHtmlModel>`. A `_ViewImports.cshtml` cannot help: it would have
to live in the **site's** folder, and `UBookIt.Web` deliberately ships none.

`IsHtml` defaults to `false` and is never inferred. Sniffing (`Contains("<") && Contains("</")`,
which Umbraco's health check does) guesses at intent and fails silently; the default matches the
shipped plain-text messages, and an author writing HTML is deliberate enough to say so.

### 5. Discovery, fallback and a boot check

Six names at `~/Views/Partials/UBookIt/Emails/<Name>.cshtml`: `BookerPlaced`,
`BookerConfirmed`, `BookerDeclined`, `BookerCancelled`, `InternalPlaced`, `InternalCancelled`.
Audience-first so the two groups sort together.

Each is independently optional; a missing one falls back to the shipped plain text. A boot check
reports which were found, mirroring `theming` — including its honest residue: per-item fallback
is what the framework does anyway, and what the check changes is the **silence**, not the
fallback.

### 6. Rendering with no ambient request

A background send has no `HttpContext`. **No such sender exists yet** — the retention sweep runs
unattended but erases bookers and sends nothing — so this requirement is anticipatory: it exists
so that the first thing which does send from a timer does not discover it in production. Core's only view-to-string recipe (`PartialViewBlockEngine`) resolves the view engine from
`httpContext.RequestServices`, which is that implementation's choice rather than a framework
limit — `tests/UBookIt.Tests.Rendering/Support/ViewRenderer.cs` in this repository already
renders precompiled views from a plain `ServiceCollection`. Templates therefore inherit
`RazorPage<T>`, **not** `UmbracoViewPage<T>`, which avoids the `IUmbracoContextAccessor` and
`IPublishedUrlProvider` plumbing that rig had to stub.

Stated as a requirement with a test, because it is the class of thing that works in development
and fails in a job.

### 7. What narrows, and what must not

The `booking-emails` guarantee that *"what the message says about the booking's state SHALL be
derived from that state"* narrows to **the messages the package composes**. A template author
owns their own words, on exactly the reasoning the accessibility narrowing records: we do not take
responsibility for text we did not write.

**Enumerated so the narrowing cannot be read wider than it is** — these do NOT narrow, because
none of them is about wording: the conjunction that gates sending; which audiences each event
addresses; that an erased booker is never written to; and that an internal message carries no
booker contact details, which §2 makes structural precisely so a template cannot touch it.

## Risks / Trade-offs

- **[A template makes a promise the site cannot keep]** → Unavoidable and owned by the author,
  as with a theme. Mitigated by documenting it and by the models carrying `Status` and the event,
  so a correct template *can* be written.
- **[An HTML template silently loses the plain-text part]** → Platform limit; stated in the spec
  and the docs rather than left to be discovered.
- **[The frozen model turns out wrong at 17.0.0]** → Cost accepted; mitigated by naming members
  for a reader and by the editor-facing constraint in the proposal's non-goals.
- **[Renderer works in a request and fails in a job]** → A test with no `HttpContext`, and no
  `UmbracoViewPage` dependency.
- **[Registration silently does nothing]** → Nullable injection has no ordering dependency, and
  the boot check reports the outcome rather than trusting the mechanism.
- **[Fallback masks a broken template]** → A template that **throws** must not be treated as
  absent; see tasks. Falling back silently on an exception would turn an author's bug into an
  email nobody can explain.

## Migration Plan

Purely additive: no schema change, no new dependency, no API change. A site with no templates
sends byte-identical messages to today — which is a test, not an aspiration.

## Open Questions

None blocking. Settled in explore: developer-facing only, templates outside theming, the six
names, typed base page over `ViewData`, `IsHtml` defaulting to false.
