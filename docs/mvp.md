# What v1 is

**A site owner can install uBookIt from a feed, configure what can be booked, publish a
booking page, take bookings from visitors, and see and cancel those bookings in the
backoffice — with their own site able to react when a booking is placed or cancelled.**

That sentence is the whole test. Everything below either serves it or is deliberately not in
it.

This is a scope decision rather than a specification: the capabilities under `openspec/specs/`
say what the package does, and this says which of them have to exist before it is worth
releasing. It is a living record until v1 ships, and then it becomes the README's account of
what the package is for.

## The chain that has to work end to end

A gap anywhere in this list means there is no product, however complete the rest is.

| | | |
|---|---|---|
| 1 | **Install the package** into an Umbraco 17 site from a feed | ❌ not built |
| 2 | **Configure a bookable resource** — opening hours, exceptions, duration limits, capabilities | ✅ |
| 3 | **Define a service** over resource roles, where a site wants one | ✅ |
| 4 | **Publish a booking page** without writing code | ✅ |
| 5 | **A visitor books**, with or without JavaScript | ✅ |
| 6 | **The owner sees bookings** in the backoffice | ✅ |
| 7 | **The owner cancels** a booking | ✅ |
| 8 | **The site can react** when a booking is placed or cancelled | ✅ |
| 9 | **It is licensed and documented** enough for someone to adopt | ❌ not built |

Steps 2–8 are done. **Only packaging is left** — step 1 and step 9, one change — and step 1
is proven by installing the built package into a clean Umbraco site, not by building a
`.nupkg`. The current one builds happily and cannot be consumed: it declares dependencies on
`UBookIt.Core` and `UBookIt.Persistence` packages that do not exist, and omits `UBookIt.Web`
entirely, so the Razor booking page ships to nobody.

### Why 8 is in v1 rather than after it

The package sends no email and will not in v1 — the site owns its own mail. But an operator
cancelling a booking is a customer who will otherwise **turn up anyway**: the row changes in
the backoffice and nothing reaches the person who booked. A cancel button with no way for the
site to tell anyone is worse than no cancel button, so the hook ships with the verb that
needs it.

What v1 provides is the **notification**, not the message: uBookIt raises an event, the site
decides what to do with it. That is the same choice made everywhere else in this package —
publish the data and the seam, never the widget or the channel.

## Explicitly not v1

Named so that "the package cannot do X" is a decision on the record rather than a gap
somebody discovers.

- **Approving or declining a booking.** Placement auto-confirms. The statuses exist in the
  model so this can be added without a breaking change, but no pathway produces them.
- **Amending a booking's time.** The shape of that operation is a cancellation and a new
  booking.
- **Taking a booking on someone's behalf** — a phone booking. Bookings arrive through the
  front-end flow.
- **Finding a booking without knowing its date.** The management read port is windowed by
  design; searching by booker name, email or reference is a different query with different
  indexing.
- **Filtering the backoffice list by resource or service.** Both are supported by the
  endpoint and neither has a control; they belong with a screen that has somewhere to put
  them.
- **Emails, templates or SMTP configuration.** See above: the site owns the channel.
- **More than one booking at a time for the same resource.** A resource is claimed
  exclusively for its interval. A room that seats twenty is one bookable thing, not twenty,
  and capacity is a model change rather than a setting.
- **Recurring bookings, payment, cancellation windows, and any language beyond `en-US`.**

## What "done" looks like for the remaining change

~~**Cancel and notify**~~ — done. An operator can cancel a booking they can see; the status
becomes `Cancelled` and it stops holding its time; the site receives a notification for both
placement and cancellation carrying the booking; and both the backoffice documentation and the
confirmation dialog say plainly that the package notifies nobody by itself. See
[reacting to bookings](notifications.md).

**Packaging** — the package installs into a clean Umbraco 17 site from a local feed and the
booking flow works there, proven by doing it rather than by inspecting a `.nupkg`; every
assembly a consumer needs is in it, including the one that renders the booking page; it
carries a licence, a readme, a description and a version that is not the SDK default; and the
repository carries a `LICENSE` file, because an open-source package that does not say what
you may do with it is not one.
