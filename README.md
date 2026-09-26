# uBookIt

Bookings for Umbraco 17: rooms, desks, studios, equipment, and appointments that need a person
and a place at the same time. You set up what can be booked in the backoffice, and visitors book
on a page of your own site, in your own layout. Every step works with JavaScript turned off.

![A booking page on a site called Fairfield Studios: the site's own header and navigation across the top, then a heading reading Book Studio session and a grouped list of radio buttons headed "Dates with availability for 30 minutes in the next 30 days", one per day from Monday 21 September 2026 onward, the first already selected. The list continues below the visible area.](https://raw.githubusercontent.com/Chris-N2/UBookIt/17.2.2/docs/images/booking-flow.png)

## Get started

```
dotnet add package UBookIt
```

Run the site, and uBookIt installs its schema on first boot. It needs **SQL Server** (not SQLite)
and Umbraco 17.6.2 or later. The full requirements are further down.

**Then grant the section.** Like any Umbraco section, **uBookIt** stays hidden until you grant it
to a user group: *Users → User Groups → (a group) → Sections*. Grant it deliberately, because it
decides who can see the bookings your site has taken.

The booker's **name and email address** are a second question, and Umbraco's built-in
**Sensitive data** group answers it. At first that group holds only the site's original super
user, so an administrator you create later sees those details hidden until you add them. See
[the backoffice docs](https://github.com/Chris-N2/UBookIt/blob/17.2.2/docs/backoffice.md).

## What it does

**For the people booking**

- Book from a page on your site: the whole catalogue, one service, or one resource.
- Choose from the dates and start times that are actually free, for the length they need.
- Finish without JavaScript.
- Receive a confirmation email and cancel from a link in it, if you turn those on.

**For your staff**, in the backoffice

- Set up bookable resources: opening hours, date exceptions, minimum and maximum lengths, and
  capabilities that describe what each one can do.
- Build services from roles. For example, "a haircut" can mean one stylist and one chair, and
  uBookIt works out which combinations are free.
- Close the whole organisation on chosen dates, from one list, and open individual resources anyway.
- Import public holidays from a source your own code supplies. uBookIt ships no holiday data.
- See, find, move, cancel, confirm and decline bookings. Find one by its reference, or every
  booking made with an email address.
- Record a booking taken over the telephone, on the caller's behalf.
- Choose whether bookings confirm immediately or wait for somebody to approve them.
- Decide who can do what, with permissions per user group in Umbraco's ordinary group editor.
- See and change uBookIt's settings in one screen, rather than only in `appsettings.json`.

**For developers**

- Handle notifications when a booking is placed, confirmed, declined or cancelled, to send your
  own messages, write to a log, or push to a CRM.
- Build your own front end on a JSON delivery API.
- Restyle with CSS custom properties, or replace the views entirely with a theme in your own
  Razor class library.

### Off until you turn it on

A new install starts with all four of these off, or granted to nobody:

- **The delivery API.** The API is off by default, and reads and booking placement are turned on
  separately. Once on, the API is anonymous by design, and uBookIt makes no DDoS-protection
  claim: volume protection belongs to your host. See
  [the delivery API](https://github.com/Chris-N2/UBookIt/blob/17.2.2/docs/delivery-api.md).
- **Emails**, to the booker and to your own people. That means a site-wide list, plus the users
  and groups made responsible for each resource or service. A mail server is not permission to
  write to your customers.
- **Self-service cancellation.** It rides the booker's confirmation email, so it needs those
  emails on too. Read [the configuration notes](https://github.com/Chris-N2/UBookIt/blob/17.2.2/docs/configuration.md)
  first, because the link is the credential.
- **The settings screen.** It needs the *Change site settings* permission, which nobody has,
  administrators included, until you grant it.

## What it looks like

**Choosing a time, and giving your details.** The start times for the chosen day wrap across
the page rather than running down it. Every field is labelled, and the notice explaining what
the site does with the details sits where the details are asked for.

![The lower half of the same booking page: a How long do you need? selector reading 30 minutes, a Show times button, and a fieldset headed "Available start times on Monday 21 September 2026 for 30 minutes" whose nine radio options from 12:30 to 16:30 wrap across two rows. Below it a Your details fieldset holds labelled Name, Email and optional Phone fields, a note saying the site will email you about your booking, two paragraphs explaining what the details are used for and how long they are kept, and a Book button.](https://raw.githubusercontent.com/Chris-N2/UBookIt/17.2.2/docs/images/booking-form.png)

**The Bookings screen in the backoffice.** Find a booking by reference or email address, choose a
date window, filter by status, and act on a row.

![The uBookIt Bookings screen inside the Umbraco backoffice: a Find box for a reference or email address, From and To date fields both set to 5 October 2026, status checkboxes for Requested, Confirmed, Cancelled and Declined, and a New booking button for recording one taken by telephone. Below them a table of four bookings shows reference, date and time, booker name and email, resources, service and status, each row offering Move and Cancel actions.](https://raw.githubusercontent.com/Chris-N2/UBookIt/17.2.2/docs/images/bookings-screen.png)

**Availability, per resource.** Opening hours are windows on each weekday. Add as many as a day
needs, for a lunch break or a split shift.

![The Opening hours panel of a resource in the uBookIt backoffice, with one section per day of the week. Monday through Friday are shown and the remaining days continue below the picture; each day holds a From and a To time field reading 09:00 and 17:00, a Remove window link beside them, and an Add window link for that day underneath.](https://raw.githubusercontent.com/Chris-N2/UBookIt/17.2.2/docs/images/availability.png)

## Accessibility is a feature here, not a checkbox

Booking interfaces are notorious accessibility failures. So uBookIt's shipped flow is built to
meet **every WCAG 2.2 AA criterion that markup determines**: labelling, grouping, programmatic
relationships, keyboard operability, and reading and focus order. Every step stays usable with
**no stylesheet applied at all**.

That claim names its own boundary, because uBookIt is a component inside *your* page, and WCAG
conformance is a property of a page. Text contrast, focus appearance and target size are decided
by CSS. The shipped stylesheet sets no text colour of its own, and non-text contrast for
decorative borders is explicitly **not** claimed. The full account, including what becomes yours
the moment you override a token or supply a theme, is in
[the booking page docs](https://github.com/Chris-N2/UBookIt/blob/17.2.2/docs/booking-page.md#accessibility-what-we-hold-and-what-becomes-yours).

## What it does not do yet

These limits are deliberate, and they're listed here so you know before you install:

- **One booking per resource at a time.** A resource is claimed for the whole interval, so a room
  that seats twenty is one bookable thing, not twenty.
- **A move changes when, not what.** If a booking's resource is busy at the new time, the move is
  refused. It isn't given a different resource.
- **No search by the booker's name.** You can find a booking by its reference, or every booking
  made with an email address, which is how an erasure request is honoured. Searching by name is a
  search over personal data, and uBookIt deliberately doesn't offer one.
- **No recurring bookings, payment or cancellation windows**, and no language other than `en-US`.

## How it's built

uBookIt is built with AI assistance, under a spec-driven workflow that is in the repository for
anyone to read:

- **Specs first.** Every change starts as a written proposal, design and task list, in
  [OpenSpec](https://github.com/Fission-AI/OpenSpec). No code is written until the change is
  approved.
- **Independent review.** A separate agent that did not write the change reviews it
  adversarially against its spec. It can reject the change, and when it does, the findings go
  back through implementation. The reviewer never fixes code itself.
- **CI that can't pass by skipping.** Every commit to a release line is built and tested on Linux
  against SQL Server. The run fails if any test suite didn't report or any test was skipped.
- **Documentation under test.** The test suite checks key claims in this readme: the version it
  states, the versioning policy, the delivery API's default, and every link into the repository.
- **Gated releases.** A release is packed from its tagged commit on a clean CI runner, and its
  packages are verified there. Nothing is published until the maintainer approves.

The project's rules are in [`CLAUDE.md`](https://github.com/Chris-N2/UBookIt/blob/17.2.2/CLAUDE.md),
the same file the AI agents working on it read. The release procedure is in
[the publishing runbook](https://github.com/Chris-N2/UBookIt/blob/17.2.2/docs/publishing.md).

## Versions and the API promise

> **uBookIt is at `17.2.2`, and the public API is a promise.** That doesn't mean nothing will ever
> change. It means a change to a published contract is deliberate, is named before you meet it,
> and never arrives in a patch.

**The major tracks the Umbraco major**, as Umbraco packages are conventionally versioned: uBookIt
`17.x` is for Umbraco 17. That means **the major is not a breaking-change signal, and this is
where uBookIt departs from Semantic Versioning**:

| | |
|---|---|
| **patch** (`x.y.1` → `x.y.2`) | Never breaks. Fixes and internal changes only. |
| **minor** (`x.1.0` → `x.2.0`) | New features, and the only place a breaking change may appear. |
| **major** (`17.x` → `18.x`) | A different Umbraco. The API may change with it, since the CMS it targets did. |

Breaking changes are avoided. Where one is genuinely unavoidable, it lands in a minor release. It
is called out in [the changelog](https://github.com/Chris-N2/UBookIt/blob/17.2.2/CHANGELOG.md),
where each release opens with what upgrading asks of you, and it ships with sensible defaults or a
documented upgrade path so a site that already works keeps working. A patch release never carries
a breaking change.

## Requirements

| | |
|---|---|
| **Umbraco** | **17.6.2 or later, and not 18 or anything after it** — from `17.1.2` the packages say so, so your package manager refuses a mismatch instead of installing one. Not 13, and not the 14–16 STS line. For Umbraco 18, install uBookIt `18.x`. |
| **.NET** | 10.0 |
| **Database** | **SQL Server.** SQLite is not supported, including the SQLite database a `dotnet new umbraco` site gives you by default. |

SQLite is excluded for a real reason, not because it's untested: uBookIt's schema and
availability queries are written for SQL Server, and a site on SQLite fails at migration time
rather than quietly misbehaving.

## Documentation

- [The booking page](https://github.com/Chris-N2/UBookIt/blob/17.2.2/docs/booking-page.md): creating it, URL parameters,
  styling, deployment, and the accessibility statement
- [The backoffice](https://github.com/Chris-N2/UBookIt/blob/17.2.2/docs/backoffice.md): resources, availability,
  services, closures, bookings and permissions
- [Configuration](https://github.com/Chris-N2/UBookIt/blob/17.2.2/docs/configuration.md): every setting, and what
  turning each one on means
- [The delivery API](https://github.com/Chris-N2/UBookIt/blob/17.2.2/docs/delivery-api.md): turning it on, what
  anonymous means, and where volume protection belongs
- [Reacting to bookings](https://github.com/Chris-N2/UBookIt/blob/17.2.2/docs/notifications.md): the notifications
  and how to handle them
- [Writing a theme](https://github.com/Chris-N2/UBookIt/blob/17.2.2/docs/theming.md): replacing the rendering with
  your own views

## The packages

Install `UBookIt`. It contains no code of its own and brings in the rest, which are published
separately so that a headless consumer can take the contracts without the backoffice.

| Package | |
|---|---|
| **`UBookIt`** | What you install. Brings everything below. |
| `UBookIt.Core` | The domain: resources, availability, slots, bookings. References nothing. |
| `UBookIt.Persistence` | EF Core storage and the Umbraco migration plan. |
| `UBookIt.Backoffice` | The Bookings section and its Management API. |
| `UBookIt.Web` | The Razor views, the booking page, the delivery API and the stylesheet. |

Installing the packages individually is supported. But installing `UBookIt.Backoffice` without
`UBookIt.Web` **fails silently**: the backoffice works, and the booking page renders nothing.
Install `UBookIt`.

## Licence

[MIT](https://github.com/Chris-N2/UBookIt/blob/17.2.2/LICENSE). Copyright © Norwood Design & Development Ltd.

Built by [Norwood Design & Development Ltd.](https://www.norwood-development.co.uk).
