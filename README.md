# uBookIt

A booking system for Umbraco 17.

Configure what can be booked, publish a page, and take bookings — without writing code.
Visitors can complete a booking **with JavaScript turned off**, and your site is told when a
booking is placed or cancelled so it can send whatever it wants to send.

```
dotnet add package UBookIt
```

Then run the site. uBookIt installs its schema on first boot.

**One step you will otherwise look for:** the **uBookIt** section is not visible until you
grant it, the same as any other Umbraco section — *Users → User Groups → (a group) → Sections*.
Grant it deliberately rather than to everyone: it decides who can see the bookings a site has
taken. Who can see the **name and email address** of the person who booked is a second
question, answered by Umbraco's built-in **Sensitive data** group — and note that only the
site's original super user is in that group to begin with, so a newly created administrator
sees those details hidden until you add them. See [the backoffice docs](docs/backoffice.md).

> **uBookIt is at `17.0.0`, and the public API is now a promise.** Leaving `0.x` is that
> promise — treat the contracts as settled from here.

### What the version number means

**The major tracks the Umbraco major**, as Umbraco packages are conventionally versioned:
uBookIt `17.x` is for Umbraco 17, and the uBookIt for a later Umbraco carries that Umbraco's
major. You never have to remember which uBookIt went with which CMS.

**That means the major is not a breaking-change signal, and this is where uBookIt departs
from Semantic Versioning.** The major is spent on the CMS version, so:

| | |
|---|---|
| **`17.x.y` → `17.x.z`** (patch) | Never breaks. Fixes and internal changes only. |
| **`17.x.0` → `17.y.0`** (minor) | New features — and the only place a breaking change may appear. |
| **`17` → a later major** | A different Umbraco — and the API may change with it, since the CMS it targets did. |

Breaking changes are avoided: the public interface is kept as consistent as possible, and
additions are preferred to changes. Where one is genuinely unavoidable it lands in a **minor**
release, is called out explicitly rather than left to be discovered, and ships with sensible
defaults or a documented upgrade path so a site that already works keeps working. A patch
release never carries a breaking change.

## Requirements

| | |
|---|---|
| **Umbraco** | 17.x (LTS). Not 13, and not the 14–16 STS line. |
| **.NET** | 10.0 |
| **Database** | **SQL Server.** SQLite is not supported — including the SQLite database a `dotnet new umbraco` site gives you by default. |

The SQLite exclusion is a real constraint rather than an untested configuration: uBookIt's schema and
its availability queries are written for SQL Server, and a site on SQLite will fail at
migration time rather than quietly misbehave.

## What it does

- **Bookable resources** — opening hours, date exceptions, minimum and maximum duration, and
  capabilities describing what a resource can do.
- **Services** composed of resource *roles*, so "a haircut" can mean "one stylist and one
  chair" and uBookIt works out which combinations are actually free.
- **A booking page** you create like any other page. Point it at the whole catalogue, or at
  one service or resource. It uses your site's layout.
- **A booking flow** that works without JavaScript, and a JSON delivery API if you would
  rather build your own front end. **The API is off by default** — an untouched install
  exposes no anonymous endpoint, and each half (reads and booking placement) is enabled by
  its own setting. When you do turn it on it is anonymous by design, it cannot know who is
  calling (no header check or CORS policy can make an anonymous API know that), and volume
  protection belongs to your host's rate limiting or edge — uBookIt makes no
  DDoS-protection claim. The details, including the breaking change if you were already
  using the API, are in [the delivery API](docs/delivery-api.md).
- **A Bookings section** in the backoffice for seeing bookings, cancelling them, and — where
  you have asked for bookings to be approved rather than confirmed on the spot — confirming
  or declining them. **With permissions per user group**: seeing bookings, acting on them,
  and configuring resources and services are separate grants within the section, ticked in
  the ordinary Umbraco group editor.
- **Approval, if you want it.** `UBookIt:AutoConfirm` is on by default, so bookings confirm
  immediately; turn it off and each one waits for somebody to confirm or decline it, holding
  its time meanwhile.
- **Optional emails** to the person who booked and to your own people — a site-wide list,
  plus the users and groups made **responsible** for each resource or service — **off until
  you ask for them**, because a mail server is not permission to write to your customers.
- **Notifications** when a booking is placed, confirmed, declined or cancelled, so your site
  can send its own messages, log, push to a CRM, or anything else.
- **Restyling** through CSS custom properties, or **theming** by replacing the views
  entirely with your own Razor class library.

### Accessibility is a feature here, not a checkbox

Booking interfaces are notorious accessibility failures, so uBookIt's shipped flow is built
to meet **every WCAG 2.2 AA criterion that markup determines** — labelling, grouping,
programmatic relationships, keyboard operability, reading and focus order — and every step
stays usable with **no stylesheet applied at all**.

That claim names its own boundary, because uBookIt is a component inside *your* page and WCAG
conformance is a property of a page. Text contrast, focus appearance and target size are
decided by CSS, the shipped stylesheet sets no text colour of its own, and non-text contrast
for decorative borders is explicitly **not** claimed. The full account, including what becomes
yours the moment you override a token or supply a theme, is in
[the booking page docs](docs/booking-page.md#accessibility-what-we-hold-and-what-becomes-yours).

## What it does not do yet

On the record as decisions, not gaps somebody discovers:

- **Amending** a booking's time, or **taking a booking on someone's behalf**.
- **Finding a booking without knowing roughly when it is.** The backoffice list is windowed
  by date — but you can find every booking holding a given email address, which is how an
  erasure request is honoured. There is no search by name or reference.
- **More than one booking at a time for the same resource.** A resource is claimed
  exclusively for its interval — a room that seats twenty is one bookable thing, not twenty.
- **Recurring bookings, payment, cancellation windows**, and any language beyond `en-US`.

## Documentation

- [The booking page](docs/booking-page.md) — creating it, the URL parameters, styling, the
  deployment note about committing the installed template, and the accessibility statement
- [The backoffice](docs/backoffice.md) — resources, availability, services and bookings
- [The delivery API](docs/delivery-api.md) — turning it on, what anonymous means, and
  where volume protection belongs
- [Reacting to bookings](docs/notifications.md) — the notifications and how to handle them
- [Writing a theme](docs/theming.md) — replacing the rendering with your own views

## The packages

You install `UBookIt`, which contains no code and exists to bring the rest in. They are
published separately because they are separately useful, and because a headless consumer
should be able to take the contracts without the backoffice.

| Package | |
|---|---|
| **`UBookIt`** | What you install. Brings everything below. |
| `UBookIt.Core` | The domain — resources, availability, slots, bookings. References nothing. |
| `UBookIt.Persistence` | EF Core storage and the Umbraco migration plan. |
| `UBookIt.Backoffice` | The Bookings section and its Management API. |
| `UBookIt.Web` | The Razor views, the booking page, the delivery API and the stylesheet. |

Installing the individual packages instead is supported, but installing
`UBookIt.Backoffice` without `UBookIt.Web` **fails silently** — the backoffice works and the
booking page renders nothing. Install `UBookIt`.

## Licence

[MIT](LICENSE). Copyright © Norwood Design & Development Ltd.

Built by [Norwood Design & Development](https://www.norwood-development.co.uk).
