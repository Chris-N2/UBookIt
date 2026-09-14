# What v1 is

**A site owner can install uBookIt from a feed, configure what can be booked, publish a
booking page, take bookings from visitors, and see and cancel those bookings in the
backoffice — with their own site able to react when a booking is placed or cancelled.**

*(That sentence describes v1. Later versions widened it: 0.5.0 added optional emails the
package sends itself, and 0.6.0 added approval, so a site can also confirm or decline. Both are
off or on-by-default respectively, so what an unconfigured site does is still exactly this.)*

That sentence is the whole test. Everything below either serves it or is deliberately not in
it.

This is a scope decision rather than a specification: the capabilities under `openspec/specs/`
say what the package does, and this says which of them had to exist before it was worth
releasing.

> **This document is now history, and is kept as history.** It was a living record until v1
> shipped; v1 shipped as **`17.0.0`**. Its own instruction was to fold into the README at that
> point, and that was declined deliberately: what it holds is *scope* history — what was
> knowingly left out of v1 and why — which a README should not carry, since a README says what
> the package does rather than what it once chose not to do. Several archived changes cite this
> file, so it stays where they point. **For what uBookIt does today, read
> [the README](../README.md)**; for what it deliberately does not do yet, the README's "What it
> does not do yet" section is the current list and this one is the v1-era snapshot.

## The chain that has to work end to end

A gap anywhere in this list means there is no product, however complete the rest is.

| | | |
|---|---|---|
| 1 | **Install the package** into an Umbraco 17 site from a feed | ✅ |
| 2 | **Configure a bookable resource** — opening hours, exceptions, duration limits, capabilities | ✅ |
| 3 | **Define a service** over resource roles, where a site wants one | ✅ |
| 4 | **Publish a booking page** without writing code | ✅ |
| 5 | **A visitor books**, with or without JavaScript | ✅ |
| 6 | **The owner sees bookings** in the backoffice | ✅ |
| 7 | **The owner cancels** a booking | ✅ |
| 8 | **The site can react** when a booking is placed or cancelled | ✅ |
| 9 | **It is licensed and documented** enough for someone to adopt | ✅ |

**Every step is done.** A site adds one package, `UBookIt`, and gets all four assemblies;
the repository carries an MIT `LICENSE` and a `README.md`; and the first release is
`17.0.0` — the major tracking the Umbraco major it targets, and leaving `0.x` being the
claim that the API has settled. *(It read `0.1.0` while this document was still live: that
was the pre-release number, a claim the package worked rather than that its API had stopped
moving.)*

Step 1 was proven the only way it can be — by installing the built packages into an Umbraco
site created from scratch and using it. `scripts/verify-install.ps1` walks that path and
the `packaging-release` change's `verification.md` records what was observed. What made this necessary
rather than pedantic: the package that existed before **built happily and could not be
consumed at all.** It declared dependencies on `UBookIt.Core` and `UBookIt.Persistence`
packages nobody published, omitted `UBookIt.Web` entirely so the Razor booking page shipped
to nobody, and — found while fixing the rest — carried no backoffice bundle either, because
`wwwroot/App_Plugins` is gitignored and nothing built it. Three silent absences in an
artifact that reported complete success.

`PackageCompositionTests` now guards each of those over the real `.nupkg` files. The
installation itself is **not** automated; that remains a recorded obligation.

### Why 8 is in v1 rather than after it

The package sent no email in the MVP and the site owned its own mail entirely. (That changed in
0.5.0, which added optional confirmation and cancellation emails — still off by default, and still
built on the hook this section is about. The argument below is why the hook shipped when it did.)
But an operator
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

- **Approving or declining a booking.** In v1, placement auto-confirmed; the statuses
  existed in the model so this could be added without a breaking change, but no pathway
  produced them. (That changed in 0.6.0, which added an `UBookIt:AutoConfirm` setting — on
  by default, so the v1 behaviour is still what an unconfigured site gets — and the
  confirm/decline routes into the statuses that were waiting for them.)
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
confirmation dialog say plainly what the package sends. (At the time that statement was an
unconditional one — that the package sends nothing on its own behalf — which 0.5.0's optional
emails made conditional. The documentation was corrected then; the dialog's flat wording was
missed and corrected in 0.6.0, and both now state that the booker is told only where booking
emails are configured.) See [reacting to bookings](notifications.md).

**Packaging** — the package installs into a clean Umbraco 17 site from a local feed and the
booking flow works there, proven by doing it rather than by inspecting a `.nupkg`; every
assembly a consumer needs is in it, including the one that renders the booking page; it
carries a licence, a readme, a description and a version that is not the SDK default; and the
repository carries a `LICENSE` file, because an open-source package that does not say what
you may do with it is not one.
