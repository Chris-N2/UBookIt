## Why

uBookIt collects a person's name, email address and phone number, keeps them, shows them to some
backoffice users and not others, erases them on request, and — since change ③ — may erase them on
a timer. **It tells the person none of this.** The booking form asks for contact details and says
only *"We'll send your booking confirmation here."*

A notice covering what is collected, why, how long it is kept and who can see it is expected of
any site collecting personal data, whatever its lawful basis, and it is the last change of roadmap
0.3.0. It comes after retention deliberately: the notice has to state the retention period, and
until ③ there was no period to state.

**This is not a consent tickbox**, and that is settled rather than being decided here. The lawful
basis for holding a booker's details is performance of a contract — you cannot deliver a booking
without them — and consent that cannot be refused is not consent. Worse, if consent *were* the
basis, withdrawing it would oblige erasure and make a site's own booking records revocable on
request.

## What Changes

- **The booking form states what happens to the details it asks for**, in both flows, rendered
  from one new shared partial.
- **The package writes the facts it can keep true, and the site links its own policy.** The
  sentences about what is collected, why, how long and who sees it are generated from what the
  package actually knows and does. `UBookIt:PrivacyPolicyUrl`, when configured, adds a link to
  the site's own privacy policy. *Decided with sign-off; the alternatives are in the design.*
- **The retention sentence is derived from `RetentionDays`, never authored**, so it cannot claim a
  period the code does not keep. With retention off — the default — it says plainly that no
  automatic removal period is set, rather than going silent. *Decided with sign-off.*
- **The notice reaches the view model, not just the markup.** `IBookingFormView` gains it as
  structured data, so a theme is handed it on the same terms as everything else it receives.
- **The delivery API publishes the retention period as a number**, so a headless consumer can
  state it accurately. It publishes no prose. *Decided with sign-off.*

### BREAKING — published contract

**`IBookingFormView` gains a member.** The `theming` capability makes the package's shared
partials and view model types a published compatibility contract precisely so that a change to
either is called out as breaking, and this is that call-out. In practice a theme *consumes* the
view model rather than implementing it, so a theme that ignores the new member still compiles and
still renders — but the promise is about the type, not about how much it happens to hurt.

`SiteBookingSettings` also gains an optional property, which is additive.

## Capabilities

### New Capabilities

- `privacy-notice`: what the booking form tells a person about the personal data it collects —
  that a notice is shown at the point of collection in every flow the package ships; that it
  states what is collected, why, how long and who can see it; that the retention statement is
  derived from the configured period rather than authored, and says something true when no period
  is configured; that the package writes only sentences it can keep true and the site supplies its
  own policy by link; that the notice is data on the view model rather than markup alone; and that
  it is not a consent mechanism and does not gate submission.

### Modified Capabilities

- `delivery-api`: gains a read publishing the site's retention period, so a consumer building its
  own booking UI can state accurately how long the data it collects will be kept. It is the one
  fact about that data which only the package holds.
- `persistence`: the composition requirement enumerates what the composer registers and how each
  setting behaves when absent or unreadable; `UBookIt:PrivacyPolicyUrl` joins that list, and an
  unusable URL has to behave like the other settings that cannot be read.

## Impact

**Code**

- `UBookIt.Core`: `SiteBookingSettings` gains `PrivacyPolicyUrl`.
- `UBookIt.Web`: a `PrivacyNoticeView` on `IBookingFormView`; a new shared partial
  `_PrivacyNotice.cshtml` called from `_YourDetails.cshtml`; a delivery endpoint publishing the
  retention period; the settings resolution for the new key.
- `docs/`: the setting, what the notice says and cannot say, and — for theme authors — that a
  theme replacing the details view owns whether the notice renders at all.

**Contracts**

A new shared partial is a new published building block a theme may call. The delivery API gains
one endpoint under the existing versioned route.

**Systems**

None. No schema change, no migration, no background work, no new dependency.

## Non-goals

- **A consent tickbox.** Settled above and by the roadmap. A configurable *"require an explicit
  acknowledgement"* option is worth supporting for site owners whose legal advice insists — but it
  is a form control with validation, error-summary integration, input preservation across the
  Post-Redirect-Get round trip and its own accessibility obligations, which is a change in its own
  right. **Deferred with sign-off**, not forgotten.
- **Marketing consent.** Genuinely free choice and therefore genuine consent, but separable and
  not needed by 0.3.0.
- **Authoring the site's own privacy policy.** The package links to it; it does not host, render
  or validate it.
- **Publishing the notice's prose on the delivery API.** A headless consumer writes its own page
  and its own wording; shipping untranslatable English into a data contract would make it a
  compatibility promise to localise later. The retention *number* is published instead — the only
  fact such a consumer cannot otherwise obtain.
- **Localisation.** The package's shipped views are English throughout; the notice is consistent
  with that and does not fix it. Worth a change of its own, package-wide.
- **A rich-text or content-node source for the notice.** Considered and declined at explore: it
  wants the admin settings screen that is not due until roadmap 0.9.0, and a content GUID in
  `appsettings` is worse than a URL until there is a picker.
- **Making a theme render the notice.** A theme replacing the details view owns its markup, on
  exactly the terms `theming` already states. The package documents the consequence rather than
  inventing an enforcement it cannot honour.
