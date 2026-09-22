# uBookIt

A booking system for Umbraco 18.

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
sees those details hidden until you add them. See [the backoffice docs](https://github.com/Chris-N2/UBookIt/blob/18.0.0/docs/backoffice.md).

> **uBookIt is at `18.0.0`, and the public API is a promise.** The promise is not that nothing
> will ever change — `17.1.0` itself added members to five published interfaces, which is why it
> is a minor. It is that a change to a published contract is deliberate, is named before you meet
> it, and never arrives in a patch. What each release asks of a site that is upgrading is the
> first thing in [the changelog](https://github.com/Chris-N2/UBookIt/blob/18.0.0/CHANGELOG.md).

### What the version number means

**The major tracks the Umbraco major**, as Umbraco packages are conventionally versioned:
uBookIt `17.x` is for Umbraco 17, and the uBookIt for a later Umbraco carries that Umbraco's
major. You never have to remember which uBookIt went with which CMS.

**That means the major is not a breaking-change signal, and this is where uBookIt departs
from Semantic Versioning.** The major is spent on the CMS version, so:

| | |
|---|---|
| **patch** (`x.y.1` → `x.y.2`) | Never breaks. Fixes and internal changes only. |
| **minor** (`x.1.0` → `x.2.0`) | New features — and the only place a breaking change may appear. |
| **major** (`17.x` → `18.x`) | A different Umbraco — and the API may change with it, since the CMS it targets did. |

Breaking changes are avoided: the public interface is kept as consistent as possible, and
additions are preferred to changes. Where one is genuinely unavoidable it lands in a **minor**
release, is called out explicitly rather than left to be discovered, and ships with sensible
defaults or a documented upgrade path so a site that already works keeps working. A patch
release never carries a breaking change.

**"Called out explicitly" means
[the changelog](https://github.com/Chris-N2/UBookIt/blob/18.0.0/CHANGELOG.md)**, where each release
opens with what upgrading asks of you before it says what you gain.

## Requirements

| | |
|---|---|
| **Umbraco** | 18.x. Not 13, not the 14–16 STS line, and not 17 — uBookIt `17.x` is the release for Umbraco 17. |
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
  using the API, are in [the delivery API](https://github.com/Chris-N2/UBookIt/blob/18.0.0/docs/delivery-api.md).
- **A Bookings section** in the backoffice for seeing bookings, cancelling them, moving them
  to a new time, and — where you have asked for bookings to be approved rather than confirmed
  on the spot — confirming or declining them. **With permissions per user group**: seeing bookings, acting on them,
  and configuring resources and services are separate grants within the section, ticked in
  the ordinary Umbraco group editor.
- **Finding a booking** from that screen by its **reference** — the code the caller is
  holding — or by the **email address** it was made with, without knowing when it is. The
  reference needs only *See bookings*; the email search asks about a person, so it needs
  Umbraco's **Sensitive data** group too.
- **Taking a booking over the telephone.** An operator can record one on somebody's behalf
  from the Bookings screen, with the notice and horizon rules waived — you are the one the
  site trusts to decide. Needs *Act on bookings* **and** Sensitive data, because you are
  typing another person's details.
- **Self-service cancellation**, so a booker can call a booking off from a link in their
  confirmation email instead of ringing you. **Off by default**, and it needs booker emails
  on — the link rides that message, so without it the feature stays absent rather than
  half-working. Read [the configuration notes](https://github.com/Chris-N2/UBookIt/blob/18.0.0/docs/configuration.md) first: the link is the credential.
- **A settings screen**, so uBookIt's configuration is visible in one place rather than only
  in `appsettings.json`. Settings that can only come from configuration are shown read-only
  with where to set them. It needs the *Change site settings* permission, which is
  deliberately granted to nobody — not even administrators — until you grant it.
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

## What it looks like

**The booking flow, inside somebody else's page.** The header, navigation and typeface belong
to the site; uBookIt supplies the markup and one stylesheet, and sets no text colour of its own,
so the flow takes the site's. It works with JavaScript turned off.

![A booking page on a site called Fairfield Studios: the site's own header and navigation across the top, then a heading reading Book Studio session and a grouped list of radio buttons headed "Dates with availability for 30 minutes in the next 30 days", one per day from Monday 21 September 2026 onward, the first already selected. The list continues below the visible area.](https://raw.githubusercontent.com/Chris-N2/UBookIt/18.0.0/docs/images/booking-flow.png)

**Choosing a time, and giving your details.** The start times for the chosen day render as a
wrapping run rather than a long column, every field is labelled, and the notice explaining what
the site does with the details sits where the details are asked for.

![The lower half of the same booking page: a How long do you need? selector reading 30 minutes, a Show times button, and a fieldset headed "Available start times on Monday 21 September 2026 for 30 minutes" whose nine radio options from 12:30 to 16:30 wrap across two rows. Below it a Your details fieldset holds labelled Name, Email and optional Phone fields, a note saying the site will email you about your booking, two paragraphs explaining what the details are used for and how long they are kept, and a Book button.](https://raw.githubusercontent.com/Chris-N2/UBookIt/18.0.0/docs/images/booking-form.png)

**The Bookings screen in the backoffice.** Find a booking by reference or email address, choose a
date window, filter by status, and act on a row.

![The uBookIt Bookings screen inside the Umbraco backoffice: a Find box for a reference or email address, From and To date fields both set to 5 October 2026, status checkboxes for Requested, Confirmed, Cancelled and Declined, and a New booking button for recording one taken by telephone. Below them a table of four bookings shows reference, date and time, booker name and email, resources, service and status, each row offering Move and Cancel actions.](https://raw.githubusercontent.com/Chris-N2/UBookIt/18.0.0/docs/images/bookings-screen.png)

**Availability, per resource.** Opening hours are windows on each weekday — add as many as a day
needs, for a lunch break or a split shift.

![The Opening hours panel of a resource in the uBookIt backoffice, with one section per day of the week. Monday through Friday are shown and the remaining days continue below the picture; each day holds a From and a To time field reading 09:00 and 17:00, a Remove window link beside them, and an Add window link for that day underneath.](https://raw.githubusercontent.com/Chris-N2/UBookIt/18.0.0/docs/images/availability.png)

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
[the booking page docs](https://github.com/Chris-N2/UBookIt/blob/18.0.0/docs/booking-page.md#accessibility-what-we-hold-and-what-becomes-yours).

## What it does not do yet

On the record as decisions, not gaps somebody discovers:

- **Changing which resources a booking claims.** A move changes when, not what: a booking
  whose resource is busy at the new time is refused rather than given a different one.
- **Finding a booking by the booker's name.** You can look one up by its reference, and find
  every booking holding a given email address — which is how an erasure request is honoured —
  but searching by name is a search over personal data that uBookIt deliberately does not
  offer.
- **More than one booking at a time for the same resource.** A resource is claimed
  exclusively for its interval — a room that seats twenty is one bookable thing, not twenty.
- **Recurring bookings, payment, cancellation windows**, and any language beyond `en-US`.

## Documentation

- [The booking page](https://github.com/Chris-N2/UBookIt/blob/18.0.0/docs/booking-page.md) — creating it, the URL parameters, styling, the
  deployment note about committing the installed template, and the accessibility statement
- [The backoffice](https://github.com/Chris-N2/UBookIt/blob/18.0.0/docs/backoffice.md) — resources, availability, services and bookings
- [The delivery API](https://github.com/Chris-N2/UBookIt/blob/18.0.0/docs/delivery-api.md) — turning it on, what anonymous means, and
  where volume protection belongs
- [Reacting to bookings](https://github.com/Chris-N2/UBookIt/blob/18.0.0/docs/notifications.md) — the notifications and how to handle them
- [Writing a theme](https://github.com/Chris-N2/UBookIt/blob/18.0.0/docs/theming.md) — replacing the rendering with your own views

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

[MIT](https://github.com/Chris-N2/UBookIt/blob/18.0.0/LICENSE). Copyright © Norwood Design & Development Ltd.

Built by [Norwood Design & Development Ltd.](https://www.norwood-development.co.uk).
