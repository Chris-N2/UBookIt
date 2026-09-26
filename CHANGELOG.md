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
Umbraco 17. That means the major cannot signal a break, so a break lands in a **minor** and never in
a patch. See [the versioning note](README.md#versions-and-the-api-promise).

---

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
