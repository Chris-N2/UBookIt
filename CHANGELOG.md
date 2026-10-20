# Changelog

What each release of uBookIt asks of a site that is upgrading, and what it adds.

Entries are per release and lead with **what you have to do**, because that is the part you cannot
afford to skim. Where a release changes nothing you must act on, it says so.

**An entry for a release that has happened is never edited.** Those versions are on nuget.org and
cannot be changed; a changelog that gets rewritten backwards records nothing. If an entry is wrong,
the correction belongs in the next release's entry, saying what was wrong. An entry is *finished*
when its release date is stamped at publication — that stamp completes the entry rather than
revising it, and nothing else about it moves afterwards.

The major tracks the **Umbraco** major, not uBookIt's own breaking changes — uBookIt `17.x` is for
Umbraco 17 and `18.x` for Umbraco 18. That means the major cannot signal a break, so a break lands
in a **minor** and never in a patch.
See [the versioning note](README.md#versions-and-the-api-promise).

---

## 17.5.0 — 2026-10-20

### What you have to do

**Nothing to make it work.** When the site starts, uBookIt adds one column to its own service table
to hold the new instructions. The column is empty for every existing service, so nothing changes
until somebody writes some.

**After upgrading, reload any backoffice tab that was open before**, with a hard reload
(Ctrl+Shift+R, or Cmd+Shift+R on a Mac) so the browser cannot keep the old backoffice code. Saving a
service sends its instructions with it, and a save that sends none clears them. An editor tab still
running the previous version's backoffice code knows nothing about the field, so saving a service
from it after somebody has added instructions would remove them. The same applies to your own
scripts or tools that update services through the Management API: send `bookerInstructions` with the
service, or it is cleared.

**If you supply your own email views, add the instructions to them if you want bookers to get
them.** uBookIt passes them to your view as `Model.BookerInstructions` and does not add them to
your content (see *For developers*).

Otherwise there is no published API removed or changed, so code built against 17.4.x keeps
working.

**Going back to 17.4.x afterwards is safe.** The older version ignores the new column: it applies
no migration, does not read the instructions, and saving a service there leaves them as they were.
No message carries them while you are on 17.4.x, and they apply again when you come forward.

### What changed

**A service can carry instructions for the booker.** In the service editor's Details box there is
a new, optional **Instructions for the booker** field: plain text, up to 2000 characters, with line
breaks kept and spaces or blank lines at either end trimmed. Use it for anything a person who has
booked needs in order to turn up, such as a link to join an online consultation, or directions and
parking for an in-person one.

**They are sent only once a booking is confirmed**, judged by the booking's status when the message
is written:
- the booker's message on placement carries them on a site that confirms bookings on the spot, and
  not on a site that requires approval. A booking your own people record for somebody is always
  confirmed, so its message carries them either way;
- the confirmation message carries them;
- a move message carries them while the booking is confirmed, and not while it still awaits
  approval;
- a decline or a cancellation never carries them, and neither does any message to your own
  recipients.

In uBookIt's own wording they appear under *Instructions for your booking:*, after what was booked
and when, and before the cancellation link. They are read from the service each time a message is
written, so a changed link reaches the booker's next message. Messages already sent are not changed.

**They are not shown on the public site.** The delivery API does not publish them, and uBookIt's
booking pages do not display them. They travel by email, so they are as private as the booker's
inbox: the field is not a place for passwords or other secrets.

### For developers

- **New members.** Every existing signature is unchanged:
  - `Service.BookerInstructions`, with `Service.WithBookerInstructions(string?)` to set it and the
    static `Service.NormalizeBookerInstructions(string?)` holding the rule. `Service.Create` is
    unchanged and gives a service with none;
  - `Service.BookerInstructionsMaxLength` (2000) and `Service.BookerInstructionsField`;
  - `FailureCodes.ServiceBookerInstructionsTooLong`, the stable code
    `service-booker-instructions-too-long`;
  - `BookerMessageModel.BookerInstructions`, `null` wherever the message does not carry them;
  - a second `BookingMessageComposer` constructor, taking an `IServiceStore` as well. It is the one
    the package registers. A composer built with the published constructor sends no instructions.
- **The Management API** carries `bookerInstructions` on a service's request and response. The
  response always includes it, as `null` when there are none.
- **The delivery API is unchanged**, and a test checks its responses never contain the
  instructions.
- **The database** gains one nullable column, `uBookItService.BookerInstructions`, through the
  additive migration `AddServiceBookerInstructions`.
- No dependency changes.

---

## 17.4.0 — 2026-10-16

### What you have to do

**If your booking page offers exactly one thing, check that you want what it now does.** A booking
page whose catalogue would list a single service or resource now goes straight to booking it,
rather than showing a list of one. That is on by default, so an upgraded site with one bookable
thing changes the moment it starts. A site offering two or more things, or none, sees no change.
- To keep showing the catalogue, untick **Skip the catalogue when there is only one choice** on
  uBookIt's settings tab, or set `UBookIt:SkipSingleChoiceCatalogue` to `false` in your
  configuration.
- If your page has text explaining why there is only one thing to choose, either turn the setting
  off or remove that text.

**If your site references MessagePack directly, at a version below 3.1.9, raise that reference to
3.1.9 or later.** uBookIt now requires 3.1.9 (see *What changed*), so a lower direct reference makes
restore fail with a package-downgrade error (NU1605).

Otherwise, **nothing.** There is no migration, no schema change, and no published API removed or
changed, so code built against 17.3.x keeps working.

**Going back to 17.3.x afterwards is safe.** The older version always shows the catalogue. If you
have saved the new setting on the settings tab, the older version ignores the saved value and does
not show it, and it cannot remove it either. Coming forward again, the saved value applies once
more. Going back also gives up the MessagePack fix below: the older version does not require
3.1.9, so your site resolves the 3.1.8 that Umbraco brings, unless it requires a fixed version some
other way, such as its own direct reference.

### What changed

**A catalogue of one is skipped.** When the booking page is opened with nothing chosen, and its
catalogue would list exactly one thing, the page goes straight to booking that thing, exactly as
if the visitor had chosen it:
- the heading names what they are booking;
- every step after the first carries `ubBook=` in its URL, as after a real choice, so a step stays
  linkable if you later add a second thing.

"One" means one entry in the catalogue as it is listed: every service, plus every resource that
can be booked on its own. Whether that one thing has free dates at the moment does not matter; if
it has none, its own page says so. A link or a page that names what to book, by `ubBook=` or by the
service or resource the site's code passes to the component, still decides, whatever the setting
says.

**The setting is on uBookIt's settings tab**, as **Skip the catalogue when there is only one
choice**, and is on by default. Like the other editable settings, a value saved there overrides
configuration, takes effect on the next page request without a restart, and can be reset to the
configured value. A configured value that cannot be read as `true` or `false` is treated as on,
and the site logs an error naming the setting when it starts.

**uBookIt now requires MessagePack 3.1.9 or later.**
[GHSA-qhrr-8q5h-9q3h](https://github.com/advisories/GHSA-qhrr-8q5h-9q3h) (medium), published on
15 October 2026, is a denial-of-service vulnerability in MessagePack before 3.1.9: deserializing
attacker-controlled data into `System.Object` can allocate excessive memory. MessagePack reaches
every site that runs uBookIt through Umbraco, whose 17.7.1 packages depend on 3.1.8, and no stable
Umbraco release yet depends on a fixed version. `UBookIt.Backoffice` and `UBookIt.Web` now declare
MessagePack 3.1.9, so a site that installs or updates uBookIt resolves the fixed package without
upgrading Umbraco. It was already in every site's graph; only its minimum rises.

**New installs get an updated description for the Booking Page document type**, mentioning the
skip. An existing site keeps the description it was installed with: this release adds no migration
step, and an ordinary upgrade does not re-import uBookIt's schema (see *What an upgrade does* in
the booking page docs).

### For developers

- **New members.** Every existing signature is unchanged:
  - `SiteBookingSettings.SkipSingleChoiceCatalogue`, defaulting to `true`;
  - `UBookItPersistenceComposer.SkipSingleChoiceCatalogueSettingKey`;
  - a second `BookingFlowViewComponent` constructor, taking the `SiteBookingSettings` as well. It is
    the one dependency injection uses. The published constructor remains, and a component built
    with it skips a catalogue of one, the setting's default.
- `SettingCatalogue.All` lists the new setting, as editable.
- **`UBookIt.Backoffice` and `UBookIt.Web` gain a dependency on `MessagePack` `3.1.9`** (a minimum).
  `UBookIt` reaches it through them. No other dependency changes.
- **A theme's `Catalogue` view is not rendered for a catalogue of one** while the setting is on:
  the visitor sees that one thing's flow, in your theme's views for it. With the setting off, your
  `Catalogue` view renders as before, however many entries there are.
- **The delivery API is unchanged.** It has no catalogue; a headless client builds its own list
  from the services and resources it reads and decides for itself whether a list of one is worth
  showing.

---

## 17.3.2 — 2026-10-15

### What you have to do

**Run the .NET 10.0.13 runtime or later on your server.** Microsoft released it on 13 October
2026 (ASP.NET Core Runtime 10.0.13) as a security update. Its
[release notes](https://github.com/dotnet/core/blob/main/release-notes/10.0/10.0.13/10.0.13.md)
list 44 vulnerabilities it fixes in .NET and ASP.NET Core. GitHub rates three of them critical,
two of those in Kestrel, ASP.NET Core's web server. Those are not uBookIt's, but they are in the
runtime your site runs on, so take the update whatever you do about uBookIt. It is also the only
fix for three of the five advisories below, which concern packages uBookIt depends on. No uBookIt
release can supply it, because your site takes that code from the runtime, not from a package.
- On a server that runs your site with an installed runtime, install it there:
  `dotnet --list-runtimes` should list `Microsoft.AspNetCore.App 10.0.13` or later. On a hosting
  platform that provides the runtime for you, check that it has moved to 10.0.13.
- In a container, rebuild on an ASP.NET Core 10.0.13 base image, or a later one.
- A self-contained deployment carries its own runtime, so rebuild it with an SDK that ships
  10.0.13: 10.0.113 or 10.0.402, or later.

**Your own NuGet audit will not tell you about those three.** With the .NET 10 SDK's default
settings, a site that runs on ASP.NET Core does not restore `System.Security.Cryptography.Xml` as
a package: the SDK removes it from the dependency graph, because the runtime provides it. So
`dotnet list package --vulnerable` and restore's own audit stay quiet about it, whichever runtime
the server has. Check the runtime instead.

**If your site rewords uBookIt's backoffice under `en-us`, and has British English users**,
register the same wording under `en` as well (see *What changed*).

Otherwise, **nothing.** No API signature, schema, migration or setting changes. If your site
references `System.Security.Cryptography.Pkcs` directly at a version below 10.0.13, restore reports
a downgrade: raise that reference to 10.0.13.

### What changed

**Five of the advisories Microsoft published on 13 October 2026 are against packages uBookIt
depends on.** All five are denial-of-service, all are fixed in 10.0.13, and they reach
every site that runs uBookIt. No stable Umbraco release fixes them yet; Umbraco's `17.8.0` and
`18.3.0` release candidates (15 October 2026) raise `Pkcs` to 10.0.13, which covers two of the
five.

Three are against `System.Security.Cryptography.Xml`, which your site takes from the ASP.NET Core
runtime. Only updating the runtime fixes them (see *What you have to do*):

- [GHSA-7hqp-7c5m-4vq6](https://github.com/advisories/GHSA-7hqp-7c5m-4vq6) (high)
- [GHSA-c399-9853-6jmj](https://github.com/advisories/GHSA-c399-9853-6jmj) (high)
- [GHSA-r955-8q5j-3h83](https://github.com/advisories/GHSA-r955-8q5j-3h83) (high)

Two are against `System.Security.Cryptography.Pkcs`, which reaches your site as a package, through
Entity Framework Core's SQL Server provider and through MailKit:

- [GHSA-846f-f764-76mx](https://github.com/advisories/GHSA-846f-f764-76mx) (high)
- [GHSA-2fmv-rv2c-59x7](https://github.com/advisories/GHSA-2fmv-rv2c-59x7) (medium)

**uBookIt now requires `System.Security.Cryptography.Pkcs` 10.0.13 or later.** `UBookIt.Persistence`
and `UBookIt.Web` declare it, so a site that installs or updates uBookIt resolves the fixed
package. It was already in every site's graph; only its minimum rises. That package's copy is
newer than the one in any .NET 10 runtime before 10.0.13, and .NET uses the newer of the two, so
it fixes these two advisories even on a server not yet updated. We confirmed that on Windows: a
site on the 10.0.9 runtime loaded the package's copy. The runtime update still matters for the
other three.

**`UBookIt` and `UBookIt.Backoffice` now declare the ASP.NET Core framework**
(`Microsoft.AspNetCore.App`), as `UBookIt.Web` always has. Every Umbraco site already runs on it,
so this asks nothing new of your site.

**uBookIt's backoffice shows its wording in every backoffice language.** Every earlier release
registered uBookIt's wording for US English only. A backoffice user with any other language saw
raw term keys, such as `ubookitResources_create` where the button should say "Create resource",
on every uBookIt screen, and on uBookIt's permissions in Umbraco's user group editor. That
included British English, which Umbraco calls `en`. uBookIt now registers its wording as
Umbraco's fallback English, so every backoffice user sees it in English unless your site supplies
a translation. If you still see keys after upgrading, reload the backoffice once with a hard
refresh.

**If your site supplies its own uBookIt wording**, as an Umbraco localization:
- Wording you registered under `en-us` still reaches your US English users. British English users
  now see uBookIt's own wording rather than keys. To give them yours as well, register the same
  wording under `en`.
- Wording you register under `en` needs nothing special. uBookIt's registration has a weight of
  200, and Umbraco registers higher weights first, so yours, at the default weight or any weight up
  to 100, is registered after it and wins.

---

## 17.3.1 — 2026-10-13

### What you have to do

**Nothing.** No API signature, schema, migration or setting changes. If your site already works,
it goes on working. Every change below is in the backoffice's markup or wording.

### What changed

**The Closed periods hints sit under their own fields.** In a resource's editor, the hints for a
closed period's To and Note fields used to appear once, under the whole group, where they read as
stray text. Each period now shows its own hints under its own fields, and each field is still
described by its hint for a screen reader.

**The holiday import's summary reads naturally.** It used to say "Created 1 closures. 0 rows were
skipped." It now says "Created 1 closure.", and mentions skipped rows only when there were some:
"1 row was skipped." or "3 rows were skipped."

**A refused holiday request says it was refused.** The site refuses a holiday request when you
are no longer allowed to make it, for example because your permission to change closures was
removed while the Closures view was open. A refused preview used to say the site's holiday source
could not be reached, which sent you to check a source that was fine. A refused import used to say
only that the closures could not be created. Both now say: "The site refused this request. Your
session may have ended, or you may no longer have permission to change closures. This is not a
problem with the holiday source."

**The holiday import disappears again if the site stops supplying holidays.** If a developer
removes the site's holiday source while the Closures view is open, the next request is meant to
take the import away, because the site no longer has the feature. Since 17.2.0 it instead
showed a failure: a preview said the source could not be reached, and an import said the closures
could not be created. It now removes the import, as intended.

**If your site overrides uBookIt's backoffice wording**, your translation of the summary still
receives the same two counts (`%0%` created, `%1%` skipped) and keeps working. The refusal is a
new term, `ubookitClosures_importRefused`, so add it to your translation.

**Documentation.** `docs/backoffice.md` now says that only a closure covering today moves the
booking page's list of dates; one starting later leaves the list starting today. The readme now
says which major is for which Umbraco: `17.x` for Umbraco 17, `18.x` for Umbraco 18.

---

## 17.3.0 — 2026-10-11

### What you have to do

**Nothing to make it work.** The database change applies itself when the site starts, as uBookIt's
earlier ones have. It only adds things: one column, and one table. Every closure you already have
becomes a closure of that one day, every opt-out stays as it was, and every date resolves exactly
as it did.

**If your code calls uBookIt directly, read *For developers* below.** Nothing published has been
removed or changed, so code built against 17.2.x keeps working. Two of its behaviours are worth
knowing.

**Going back to 17.2.x afterwards is possible, but avoid changing closures while you are there.**
The older version ignores the extra column and table, so while on it a closure running over
several days applies on its first day only, and closed periods do not apply at all. The older
version also does not know about end dates.
- A closure it **creates** is stored without an end, and comes back as the single day it starts on.
- A closure it **moves** keeps its old end. Moved past that end, it comes back as one day.
  Otherwise it runs from its new start to its old end, so a closure moved earlier covers more
  dates than you meant.
- It checks overlaps by start date only, so it can add a closure — by hand or by importing
  holidays — inside an existing range. Coming forward, the two overlap, and neither can be edited
  until you delete one of them.

Check your closures after coming forward. They all appear in the Closures view, and 17.3.0 refuses
overlaps with them, as with any other closure.

### What changed

**A closure can now cover a run of dates.** In the Closures view, a closure has a **From** and a
**To** date. Leave To empty to close just the From date. Closing the organisation from now until
the new year, or a gym for a two-month refurbishment, is one entry, not one per day. Two closures
may not cover the same day: to close for longer, extend the one you have. A resource opted out of a
closure is opted out of all of it. A closure in progress counts as upcoming.

**A resource can have closed periods of its own.** The resource editor has a new **Closed periods**
group, for runs of dates when one resource is closed: a room being painted, someone on leave. Each
has a From and a To date and an optional note.
- A closed period may not overlap another, or a date the resource has an exception for. You are
  told which, and decide.
- The note is shown only in the backoffice, never to anyone booking. Keep personal details out of
  it.
- A site-wide closure still comes first.
- Bookings already placed keep their times, exactly as with closures.

**The booking page looks past a closure.** When a closure, or a resource's closed period, covers
today, the list of dates with availability starts on the first day after it. On a service's
page it moves only when every resource that could provide the service is closed today, and then
to the first day any of them reopens. It names the dates it
covers, for example "from Monday 21 December 2026 to Tuesday 19 January 2027", not "the next 30
days". It never says why. That does let a visitor infer that the dates before the list are closed
rather than booked; nothing names the closure or its reason. The list still stops at how far ahead
the resource can be booked, so raise that resource's booking horizon if you are taking bookings now
for dates after a long closure.

**A holiday falling inside a closure's range** is shown by the holiday import as already closed.

### For developers

- **The management API gains fields; it loses and renames nothing.**
  - A closure has `endDate`, always present in responses.
  - A resource has `closedPeriods`.
- **On a resource update, a body without `closedPeriods` leaves the stored periods as they are.**
  An empty list clears them. This is unlike the other lists on a resource, and deliberate: code
  written before closed periods existed cannot erase them by not knowing about them.
- **On a closure update, a body without `endDate` makes the closure a single day.** Code written
  before ranges, that renames a range closure an operator created, will shrink it to its start date.
  Send `endDate` with the closure's current end to keep it.
- **New failure codes:** `closure-range-invalid`, `closed-period-invalid`,
  `closed-period-overlap`, `closed-period-conflicts-with-exception`. The existing
  `duplicate-closure-date` now also covers a closure whose range overlaps another's.
- **New members.** Every existing signature is unchanged; each new member sits beside the old one:
  - `SiteClosure.CreateRange`, `.EndDate`, `.Covers` and `.Overlaps`;
  - `ClosedPeriod`, and `AvailabilityConfiguration.CreateWithClosedPeriods`, `.ClosedPeriods` and
    `.ClosedPeriodFor`;
  - `ClosedRanges`;
  - `AvailableDateWindow.Compute` taking the first open date;
  - `BuildForWindow` on both form builders, and `ServiceBookingFormBuilder.FirstOpenDate`.
- **Interfaces with new members.** Each has a default, so an existing implementation still
  compiles:
  - `IResourceManagementStore.UpdateKeepingClosedPeriodsAsync`;
  - `IBookingFormView.ShiftedWindowFrom`.
- **If you supply your own `IResourceStore`,** pass each resource's closed periods into its
  availability, or they read as open. See the delivery API documentation.
- **If your theme replaces `_AvailableDates`,** name the window's dates when `ShiftedWindowFrom` is
  set. See the theming documentation.

## 17.2.3 — 2026-10-11

### What you have to do

**If your site runs Umbraco below 17.7.1, upgrade `Umbraco.Cms` to 17.7.1 first, then update
uBookIt.** uBookIt now requires 17.7.1. Installing it on an older site raises only the Umbraco
packages uBookIt depends on and leaves the rest where they are, so upgrading `Umbraco.Cms` first
keeps every Umbraco package on one version.

Umbraco 17.7.1 also requires newer versions of some of the packages it depends on. For example,
most of its Microsoft.Extensions packages and its Entity Framework Core packages move to 10.0.11,
`Microsoft.Extensions.Caching.Hybrid` to 10.9.0, and Examine to 3.10.0. If your site references
one of those packages directly at a lower version than Umbraco 17.7.1 requires, restore reports a
downgrade: raise that reference to the version Umbraco requires.
`Microsoft.EntityFrameworkCore.SqlServer`, which uBookIt also declares, is one of them.

Otherwise, **nothing.** No API signature, schema or migration changes, and no setting you have to
change.

### What changed

**uBookIt requires Umbraco 17.7.1 or later**, and still not 18. Umbraco 17.7.1 fixes four
advisories Umbraco published on 6 October 2026:

- [GHSA-w5jr-cmfx-544x](https://github.com/umbraco/Umbraco-CMS/security/advisories/GHSA-w5jr-cmfx-544x) (medium)
- [GHSA-7pj9-jmp9-p86q](https://github.com/umbraco/Umbraco-CMS/security/advisories/GHSA-7pj9-jmp9-p86q) (medium)
- [GHSA-h5wj-vpvj-mprp](https://github.com/umbraco/Umbraco-CMS/security/advisories/GHSA-h5wj-vpvj-mprp) (low)
- [GHSA-4c5q-6c64-96j7](https://github.com/umbraco/Umbraco-CMS/security/advisories/GHSA-4c5q-6c64-96j7) (low)

They describe Umbraco itself, not uBookIt's code. Requiring the fixed version means a site that
installs or updates uBookIt is on an Umbraco where they are closed.

**The Entity Framework Core SQL Server provider uBookIt requires is now 10.0.11**, up from 10.0.10,
because Umbraco 17.7.1 requires it.

**Your site may still report advisories against SixLabors ImageSharp 3.1.12.** They reach it
through `Umbraco.Cms` itself, which depends on ImageSharp. No uBookIt package depends on
ImageSharp. When this release was prepared, no Umbraco version took a fixed ImageSharp, so
updating uBookIt does not clear them; that waits on Umbraco.

## 17.2.2 — 2026-09-26

### What you have to do

**Nothing.** No API signature, schema or migration changes, and no setting you have to change.
If your site already works, it goes on working.

### What changed

**A setting value too long to store is refused with a message.** Settings are stored in a column
that holds 2048 characters. The settings screen used to accept a longer value, such as a very
long privacy policy link, and the save then failed at the database instead of being refused.
Now the screen refuses the value and states the limit. That
makes `SettingValidation.IsValid` stricter, with no change to its signature. It refuses only what
the database could never have held, so no value you have stored is affected. For the internal
recipients list, the limit applies to **each address**, because each one is stored separately. A
long list of ordinary addresses is accepted exactly as before.

**The readme has been rewritten**, and it is also the package page. It now leads with what uBookIt
does, who it is for and how to install it, and it adds a section on how the package is built.
Nothing it promised has been withdrawn. The detail it no longer carries is in the documentation
it links to.

## 17.2.1 — 2026-09-25

### What you have to do

**Nothing.** No API signature, schema or migration changes, and no setting you have to change.
If your site already works, it goes on working. If it runs on Linux and its site-relative privacy
policy link was missing, the link appears.

### What changed

**A site-relative privacy policy link now works on Linux.** Setting `UBookIt:PrivacyPolicyUrl` to
a path on your own site, such as `/privacy`, which is the form the backoffice documentation gives,
was refused on Linux hosts. The booking form showed its privacy notice with no link, and every
startup logged an error saying the value could not be used. Windows hosts were unaffected. The
cause was that .NET on Linux reads `/privacy` as a `file:` address, and the package accepts only
`http` and `https` addresses. A site-relative path is now judged as a path on every platform.
Apart from that, nothing that used to be refused is accepted now. The refusals are what they
were, including `//host`, a backslash, a control character, and every scheme except `http` and
`https`, which is what keeps `javascript:` out.

**The settings screen accepts a site-relative policy link.** It used to insist on an absolute
address on every platform, even where the booking form accepted a site-relative one, which was
on Windows hosts. So a Windows site could set the documented form only through configuration, and
a Linux site could not use it at all.

**The settings screen also now refuses something it used to accept.** It stored an absolute
`http` or `https` address containing a backslash or a control character, such as
`https:\\example.com/privacy` or an address with a tab inside it. The booking form has never
rendered such a value; it treated it as no link at all. The screen now refuses it and says what
it accepts. That makes `SettingValidation.IsValid` stricter for this setting, with no change to
its signature. **A value already stored stays stored**, and your pages look exactly as they did,
without that link. Settings are saved one at a time, so it does not stop you saving anything else.
You will only see the message if you edit that setting.

**Only the `UBookIt` package asks to be listed on the Umbraco Marketplace now.** Until this
release every uBookIt package carried the Marketplace's tag, so `UBookIt.Persistence`,
`UBookIt.Web` and `UBookIt.Backoffice` each had a listing of their own. Installing one of them
gives you part of uBookIt, and the part left out fails silently. Install `UBookIt`. Versions
already published keep their tag, so whether those three listings disappear is up to the
Marketplace. The `UBookIt` description now takes its Umbraco major from its own version, and it
no longer says "(LTS)".

## 17.2.0 — 2026-09-23

### What you have to do

**Nothing.** No code change, no configuration change, no schema change you have to act on.
`17.2.0` adds two capabilities and takes nothing away: a site that upgrades and changes no
settings gets the product it had, plus a closures screen it can ignore.

There is a database migration, and it runs on start-up as every uBookIt migration does. It only
adds — a table for the closure list and one for per-resource exemptions — so nothing you have
stored changes shape or goes away.

**This is a minor rather than a patch because it adds capability, not because anything broke.**
uBookIt's major number tracks the *Umbraco* major, so it can never signal a break of ours; a
minor is where anything a site should read about lands.

### What changed

**Site-wide closures.** The dates your whole organisation is shut — a bank holiday, a stocktake,
the week between Christmas and New Year — are now one list in the uBookIt section instead of the
same date typed into every resource's exceptions.

A closure closes **every** resource for that date, including resources you add afterwards, and it
takes precedence over a resource's own weekly hours and its own date exceptions. Bookers simply
find the date unavailable: nothing on the booking page or in the delivery API says a closure
exists or what it is called.

Any single resource can be **opened anyway** on a given closure, from a tick in its own editor —
so "we are closed on Boxing Day except the gym" is one tick rather than a rethink.

**It does not cancel bookings already placed.** A booking on a date you then close keeps its time,
its status and its reference, and goes on holding that slot. Closing a date changes what can be
booked from that moment; it never reaches backwards.

Who can do what: **seeing** the list comes with either *Configure resources and services* or
*Change site settings*; **opening one resource anyway** comes with *Configure*; **adding, editing
or deleting** a closure needs *Change site settings*, because one entry shuts everything you have.
As with every uBookIt verb, that grant is **never given automatically, including on upgrade** —
an administrator ticks it for the people who should have it.

**Public holidays, if your site supplies them.** uBookIt can turn public holidays into closures,
and **ships no holiday data for any country**. Holiday dates come from your own code.

For developers, this release publishes a new interface:

> **`IPublicHolidaySource`** (in `UBookIt.Core.Availability`) — implement it and register it, and
> uBookIt will ask it for the holidays in a date window. It returns a date and a name for each;
> it takes no country or region, because which jurisdiction a site wants is a property of the
> implementation you registered rather than something uBookIt could validate. A worked example
> against the UK government's bank-holiday feed is in `docs/configuration.md`.
>
> **Nothing is required of you.** This is a new interface, not a change to an existing one:
> nothing you have written stops compiling, and a site that implements nothing sees no difference
> at all — where no source is registered the import is absent rather than disabled, with no
> control and no explanation of one.

Where a site *does* register one, an operator on the Closures screen can fetch a window of
holidays and **tick which of them the organisation is actually closed on**. Every new date arrives
ticked, because most organisations are closed on most public holidays, and the work is unticking
the ones yours is open on. Confirming creates exactly the ticked dates, as ordinary closures you
can rename, move, opt a resource out of, or delete.

Nothing is ever imported on a schedule or at start-up — only when somebody asks — and unticking is
not remembered, so a date you decline today is offered again the next time that window is fetched.
Fetching and importing both need *Change site settings*.

### Also in this release

- The backoffice Closures screen states, for a user who may read but not change closures, which
  grant is missing and where an administrator gives it.
- `docs/backoffice.md` gains the operator's side of both features, and `docs/configuration.md` the
  developer's side of the holiday port.

---

## 17.1.2 — 2026-09-22

### What you have to do

**On Umbraco 17: nothing.** No code change, no schema change, no behavioural change. `17.1.2` is
install-compatible with `17.1.1` in both directions.

**On Umbraco 18: your restore will now fail, and that is this release doing its job.** Until now
nothing in uBookIt's package metadata said which Umbraco it was for — a NuGet dependency version
is a *minimum*, so `17.x` asking for `Umbraco.Cms.Web.Website 17.6.2` read as "17.6.2 or
anything later", and Umbraco 18 satisfied it. So `17.x` installed into an Umbraco 18 site
cleanly, built with no errors, **and then the site would not start at all** — an unhandled
`TypeLoadException` during Umbraco's startup, naming Umbraco's own internals rather than
anything of ours.

From `17.1.2` your package manager refuses that combination and tells you why, instead of letting
you discover it on first run. **Install `UBookIt 18.x` on Umbraco 18** — it is the same product
for the newer CMS, with the same features.

### What changed

**Every `Umbraco.Cms.*` dependency now carries an upper bound**, `[17.6.2,18.0.0)`. Umbraco 17.6.2
and every later Umbraco 17 release resolve exactly as before; Umbraco 18 and later do not.

**This does not repair `17.0.0`–`17.1.1`.** A published package keeps the metadata it was
published with, so those four versions remain installable into an Umbraco 18 site forever. If you
are pinning one of them on Umbraco 18, nothing will stop you — but the site will not run.

## 17.1.1 — 2026-09-21

### What you have to do

**Nothing.** No API change, no schema change, no behavioural change; `17.1.1` is
install-compatible with `17.1.0` in both directions.

### What changed

**The package page was denying two features this package already has.** `17.1.0` added booking on
somebody's behalf and lookup by a booking's reference — and the readme packed into all five
packages went on listing both under *What it does not do yet*. It said bookings could only arrive
through the front-end flow, and that there was no search by reference. Both were false the day
`17.1.0` shipped. A packed readme is frozen per version, so correcting it is what this release is
for.

**`docs/backoffice.md` carried three false sentences of its own, and is corrected with it.** It
said twice over that the section could not take a booking on somebody's behalf, and it claimed
the section could not find a person across bookings — false since `0.3.0` added the search by
email address. It did not deny reference lookup; it documented it. Each of those three sentences
sat within 200 lines of a section describing the very feature it denied.

**The readme now shows you what you are installing** — the booking flow on a page, the same
flow's time and details step, the Bookings screen, and a resource's opening hours.

**Smaller corrections in the same pass:** the documentation of how to move a booking had been
filed under the section about recording a telephone booking, and now sits under its own heading;
the version note no longer claims the public API is settled without saying what the promise
actually is; `docs/mvp.md`'s v1-era list gained the notes saying which of its entries have since
shipped; and `docs/publishing.md` records what a release must do for the readme's screenshots to
resolve.

Nothing here reaches your site. If you are on `17.1.0` and not reading the documentation, this
release changes nothing for you at all.

## 17.1.0 — 2026-09-18

### What you have to do

**If your site implements any of uBookIt's ports, it will not compile until you add the new
members.** Five published interfaces gained members, and **none of the members below has a default
implementation**. That is deliberate rather than an oversight: an observer silently deaf to a
booking being moved would be a worse outcome than a compile error.

| Interface | Members added |
|---|---|
| `IBookingObserver` | `BookingMovedAsync` |
| `IBookingStore` | `MoveAsync` |
| `IBookingManagementStore` | `FindByReferenceAsync` |
| `IServiceBookingService` | `MoveAsync`, and **two** `PlaceOnBehalfAsync` overloads — one taking a `BookingRequest`, one a `ServiceBookingRequest` |
| `IBookingService` | `MoveAsync`, `PlaceOnBehalfAsync`, `PlaceForServiceOnBehalfAsync`, `CancelAsVisitorAsync` |

`IBookingObserver` also gained `BookingPlacedOnBehalfAsync`, which **does** have a default
implementation — you do not have to add it, and an observer that ignores it keeps working.

`ICancellationSecretStore` and `ISettingsStore` are both new in this release. A new interface
breaks nothing — nobody can have been implementing it — and the package ships an implementation of
each; you only need your own if you want one.

**Three of the new backoffice features are live as soon as you upgrade**, for users who already
hold the relevant permission. Nothing new is granted, but the verbs your groups already have now
reach further:

| Feature | Who gets it on upgrade |
|---|---|
| Move a booking | any group holding **Act on bookings** |
| Book on behalf of someone | any group holding **Act on bookings** *and* Umbraco's **Sensitive data** |
| Look a booking up by reference | any group holding **See bookings** |

Those are the permission names as the backoffice shows them, under Users → User Groups.

If that is not what you want, review those groups before upgrading. Moving a booking also sends the
booker an email that did not exist before (`BookerMoved`), so a site with booker emails on will
start sending it the first time an operator moves something.

The other two features are off until you act: the settings screen needs a permission nobody holds
yet, and self-service cancellation needs configuration. Both are below.

### What you gain

- **A settings screen in the backoffice.** uBookIt's configuration is visible in one place rather
  than only in `appsettings.json`. Settings that can only come from configuration are shown
  read-only, with where to set them, instead of being hidden. **You will not see it until you grant
  the new *Change site settings* permission to a group** — it is deliberately granted to nobody on
  upgrade.
- **An operator can move a booking**, keeping its reference. The booker is emailed; your own
  internal recipients are not, because the move happened in your backoffice and the bookings screen
  is where its state lives. A service's length rules are enforced on the move, not only on the
  original booking.
- **An operator can book on behalf of someone** — the telephone booking case, where the person
  booking is not the person at a keyboard.
- **An operator can look a booking up** by reference from the bookings screen.
- **A booker can cancel their own booking** from a link in their confirmation email, without
  contacting you. **Off by default**, and it needs two settings:

  ```
  UBookIt:SelfServiceCancellation:Enabled = true
  UBookIt:Notifications:SendBookerEmails  = true
  ```

  The second is not optional: the link rides the confirmation email, so with booker emails off
  there is nowhere for it to go and the feature stays absent rather than half-working. This is the
  only new configuration key in this release.

  Read [the configuration notes](docs/configuration.md) before enabling it. Three consequences are
  worth knowing in advance: **the link is the credential**, so anyone holding it can cancel that
  booking; a booker cannot cancel a booking that has already started; and turning the feature off
  later strands anyone still holding a link. The link's secret travels in a URL, so it reaches your
  web server's access logs — that is a retention decision only you can make.

### Upgrading

No database work. Schema changes are additive and applied by the package's own migrations.

---

## 17.0.1 — 2026-09-15

### What you have to do

Nothing. No API change, no schema change, no behavioural change; `17.0.1` is install-compatible with
`17.0.0`.

### What changed

The packed README in `17.0.0` carried nine **relative** documentation links. nuget.org resolves
those against the package page rather than the repository, so every one of them 404'd. A packed
README is frozen per version along with the rest of the package metadata, so it could not be
corrected in place — a new version was the only fix.

`17.0.0`'s page keeps those dead links permanently. It is deliberately still listed, because
`17.0.1` is what nuget.org serves by default.

---

## 17.0.0 — 2026-09-15

### What you have to do

Nothing — this is the first release, so there is nothing to upgrade from and no contract it can
break.

### What changed

The first release, and the point at which the public API became a promise: leaving `0.x` is that
promise, and the contracts are settled from here.

Requires Umbraco 17.x (LTS), .NET 10, and **SQL Server** — SQLite is not supported, including the
SQLite database a `dotnet new umbraco` site gives you by default.
