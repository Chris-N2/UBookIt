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
a patch. See [the versioning note](README.md#what-the-version-number-means).

---

## 18.0.0 — 2026-09-22

### What you have to do

**Move your site to Umbraco 18 first, and install this instead of — not alongside — `17.x`.**
uBookIt's major tracks the Umbraco major, so `18.0.0` is not an upgrade of `17.1.1`; it is the
same product built for a different CMS. A site staying on Umbraco 17 stays on `17.x` and is not
missing anything: the two lines ship the same features.

**Nothing else.** No schema change, no API change, no behavioural change. Your bookings,
resources, services and settings are untouched, and the database migrates on Umbraco 18's own
terms rather than uBookIt's.

### What changed

**uBookIt runs on Umbraco 18.** Umbraco 18 generates its OpenAPI documents through
`Microsoft.AspNetCore.OpenApi` rather than Swashbuckle, which is the entire reason `17.x` cannot
run there — a `17.x` package on an Umbraco 18 site references types the host no longer ships.

**Every package now says which Umbraco it is for.** A NuGet dependency version is a *minimum*, so
until now nothing in the package metadata distinguished "needs Umbraco 17" from "needs Umbraco 17
or anything later" — which is why uBookIt has been listed as running on versions it does not run
on. Every `Umbraco.Cms.*` dependency now carries an upper bound excluding the next major, so your
package manager refuses a mismatch instead of installing one.

**The package page's links point at this release.** They named a branch before, which on a
project with two published lines meant a reader could be sent to the other line's documentation.
They now name `18.0.0`, so what you read is what this version shipped.

**The Bookings screenshot was retaken on Umbraco 18**, because 18 rounds the backoffice's buttons
and the shipped image still showed 17's square ones.

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
