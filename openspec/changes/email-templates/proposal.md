# email-templates (roadmap 0.7.0) — developer-facing

## Why

A site can already replace what uBookIt emails, but only by taking over delivery entirely.
`SendEmailNotification` carries an **immutable copy** of the message (`NotificationEmailModel`,
every property get-only), so a handler cannot rewrite a subject or a body — its only lever is
`HandleEmail()`, after which `EmailSender` returns without sending. A site that wants nothing
more than different wording therefore inherits sending, retries, the off-by-default gating and
the no-PII-to-internal-recipients rule as well.

Templates separate wording from delivery: the package keeps the policy it is tested for, and
the site owns what the message says.

**Developer-facing only.** Editors editing content in the backoffice is deliberately deferred
past 17.0.0 — it needs the admin-only settings screen and the permissions work gated on a
spike, and the editor route cannot express email styling anyway, which is the half that
actually matters. Most people installing this are developers.

## What Changes

- **A narrow rendering port, `IBookingTemplateRenderer`, defined in `UBookIt.Core`.** It
  renders a model to a body and nothing else. Everything the composer decides today — audience,
  erased booker, what-was-booked and its failure fallback, the PII rules — **stays in
  `UBookIt.Persistence` where it is tested**, and only rendering crosses the seam.
- **The port is injected nullably, and that removes the ordering problem entirely.**
  `UBookIt.Persistence` registers no renderer; `UBookIt.Web` registers one; the composer takes
  `IBookingTemplateRenderer?`. Absent means today's plain text, present means try a template.
  Nothing is replaced, so nothing depends on composer order — which matters here because a
  composer-ordering assumption has already produced a CRITICAL on this project. Umbraco's own
  `EmailSender` injects `INotificationHandler<SendEmailNotification>?` for exactly this reason.
- **Two published models, and the split is a guarantee rather than tidiness.**
  `BookerMessageModel` carries the booker's contact details; `InternalMessageModel` **has no
  member for them at all**, so a template cannot leak a booker's name, address or telephone
  number to a configuration-file address list. Today that is a promise about what the package
  does; with one shared model it would silently become a promise about what template authors
  do.
- **Six templates, at `~/Views/Partials/UBookIt/Emails/<Name>.cshtml`**: `BookerPlaced`,
  `BookerConfirmed`, `BookerDeclined`, `BookerCancelled`, `InternalPlaced`,
  `InternalCancelled`. Confirm and decline are booker-only, per 0.6.0.
- **Each template is optional, with per-template fallback**, plus a boot check reporting which
  were found. This copies `theming`, including its stated residue: falling back per item is
  what the framework does anyway, and what the check changes is the silence.
- **A package-supplied typed base page**, `UBookItEmailPage<TModel>`, exposing `Subject` and
  `IsHtml` as typed properties backed by `ViewData`. A template writes
  `@{ Subject = "…"; IsHtml = true; }` rather than indexing a dictionary, so a typo is a
  compile error. `ViewData` remains the transport because the renderer owns the dictionary it
  passes in and can read it back reliably.
- **`IsHtml` defaults to `false` and is never inferred.** The shipped defaults are plain text;
  an author writing HTML sets the flag. Content sniffing — Umbraco's own health check does
  `Contains("<") && Contains("</")` — is rejected: guessing at intent is the failure mode this
  project keeps paying for, and the wrong guess is silent.
- **The models expose structure, not the pre-flattened strings the composer builds today.**
  `ServiceName` and a `ResourceNames` collection rather than `"Treatment Room, Ada"`;
  `LocalStart`/`LocalEnd` as `DateTimeOffset` already converted to the booking's own zone, plus
  `TimeZoneId`, rather than invariant-culture strings. Being able to loop is why Razor was
  chosen over localization keys, and a joined string takes that back.
- **Documentation of a platform limit we cannot work around.** Umbraco's `EmailMessage` carries
  one `Body` and an `IsBodyHtml` flag, and `EmailMessageExtensions.ToMimeMessage` sets
  `builder.HtmlBody` **or** `builder.TextBody`, never both. **There is no multipart/alternative
  through `IEmailSender`**, so an HTML template has no plain-text part. Stated in the spec and
  the docs rather than discovered by a site whose text-only recipients get nothing.

## Non-goals

- **No editor-facing template or content editing.** Deferred past 17.0.0 by decision. Two
  constraints it places on this change, both honoured here rather than deferred with it: the
  models must read as a **token vocabulary a later editor UI could expose**, and nothing may
  hard-code that a template is the only possible source of content — a stored body must be able
  to take precedence later, the way a theme takes precedence over a shipped view.
- **No email templates in the theme mechanism.** A theme is defined as different *controls* for
  the booking flow; email is a different medium with a different renderer and is not part of
  that flow. Umbraco Forms likewise keeps `ITheme` and `IEmailTemplate` separate. A themed site
  supplies both — which costs little, because email styling is its own problem and a site's
  ordinary CSS rarely survives an email client.
- **No layout, partial or section support promised.** What a template may call is whatever
  Razor gives it; the package promises the model, the base page and the discovery path, and
  claims nothing about composing templates out of other files.
- **No multipart/alternative, no attachments, no per-recipient culture.** The first is
  impossible through `IEmailSender` (above); the third has nothing to read a culture from,
  since a booker has none recorded.
- **No change to when messages are sent, to whom, or under what configuration.** This change
  is about content only. Gating, audiences and the erased-booker rule are untouched.

## Capabilities

### New Capabilities

- `email-templates`: what a template is, where it lives, what it receives, how one is
  discovered and reported, what a template may set, and which of the package's guarantees
  survive a template and which pass to its author.

### Modified Capabilities

- `booking-emails`: message content may come from a site-supplied template; the
  status-derived-wording guarantee **narrows to the views the package ships**, on the same
  reasoning as the accessibility narrowing — we do not take responsibility for text we did not
  write. The guarantees that must NOT narrow are called out explicitly: gating, audiences, the
  erased-booker rule, and no booker contact details in an internal message.
- `packaging`: the documentation obligations gain the template mechanism and the
  single-body limitation.

## Impact

- `UBookIt.Core`: `IBookingTemplateRenderer`, `BookerMessageModel`, `InternalMessageModel`,
  and the template-name constants — all **public API frozen at 17.0.0**, which is why the model
  shape is a design decision rather than an implementation detail.
- `UBookIt.Persistence`: `BookingMessageComposer` gains the nullable renderer and builds the
  models; its existing behaviour is the fallback and must be unchanged when no template
  applies.
- `UBookIt.Web`: `RazorBookingTemplateRenderer`, `UBookItEmailPage<TModel>`, registration, and
  the boot check. **The renderer must work with no ambient `HttpContext`** — a background send
  has none, the retention job already runs that way, and reminders would — stated as a
  requirement with a test rather than left to be discovered in a job.
- Docs: `docs/notifications.md` gains the template mechanism; a template author needs the model
  reference, the six names and the single-body limitation.
- No schema change, no new dependency, no change to the delivery or management APIs.
