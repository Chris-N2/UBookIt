# uBookIt version roadmap

Where uBookIt goes between `0.1.0` (shipped, MVP) and its first full release.

**The first release is `17.0.0`, not `1.0.0`** — the major tracks the Umbraco major, as Umbraco
packages conventionally do, so that the uBookIt for Umbraco 18 is 18.x and nobody has to
remember which uBookIt went with which CMS.

*Reviewed against the code on 2026-09-03, then reordered and re-scoped after a second pass.
Rows marked **already shipped** or **partly shipped** were checked, not assumed.*

**Ordering principle: everything that protects people's data comes before anything that sends
it anywhere.** The first draft emailed booker names and addresses to an internal list at 0.3.0
and only restricted who could see PII at 0.9.0 — six versions during which the package's largest
new flow of personal data existed without the control over it. Nothing had shipped publicly, so
the reorder cost nothing.

## The plan

| Version | Feature | Notes |
|:--------|:--------|:------|
| 0.2.0 | Use the Sensitive Data group to hide PII | Umbraco ships a built-in **Sensitive Data** user group. uBookIt reads it and redacts booker names and email addresses from anyone not in it. Small, cheap, and it puts the control in place before anything else moves personal data around. |
| 0.3.0 | Data protection: retention, erasure, privacy notice | **Model-affecting, so it belongs early** — erasure must mean *anonymise*, not delete, because an erased booking still occupies its slot and still has to be discussable. That is exactly why the booking reference was built to outlive the person (see the archived `booking-reference` change). Plus a retention policy, and a privacy notice on the booking form. **Not a consent tickbox** — see "On consent" below. |
| 0.4.0 | **Show which dates have availability** | **A front-end feature — the API is already built.** Today the flow is `<input type="date">` with a min/max range: a visitor picks a date blind and only then learns whether anything is free. Instead, show the days that actually have availability over the next ~30 days. A date picker cannot express this; it wants a list of dates. Brought forward because it depends on nothing else and is the most visible improvement on the list. |
| 0.5.0 | Confirmation / cancellation emails | Via `IEmailSender`, Umbraco's own mail abstraction (confirmed present in 17). To the booker and to an internal list. The notifications this hangs off already exist and already carry the whole `Booking`, reference included — nothing new is needed in the domain. **Configured in `appsettings` at this stage, deliberately**: a settings *screen* should be admin-only, which needs permissions we do not have yet, so config-file settings keep this change small and remove the dependency. |
| 0.6.0 | Approval and decline | An `AutoConfirm` option **defaulting to on**, so the default is exactly today's behaviour. When off, placement produces `Requested`; an operator confirms or declines from the backoffice, and the booker is told either way through 0.5.0's email path — which already derives its wording from `Booking.Status` in anticipation of exactly this. `Booking.Confirm()` and `Decline()` exist and are tested; the change builds the routes into them. **Slotted before templates deliberately**: approval adds new message types, and the template scheme should be designed against the complete catalogue rather than retrofitted. *(Decided 2026-09-11 — previously "Not yet slotted".)* |
| 0.7.0 | Email templates | Let a site define email content; partial views rather than an RTE with field tokens. **Caveat: email HTML is not web HTML** (inline styles, tables, no external stylesheet), so this is a separate rendering path, not a reuse of the theming mechanism. **This is the row that most justifies a separate package** — see "Open decisions". |
| 0.8.0 | Resource / service responsibility | Who is responsible for which resource or service, by backoffice user or group — or by groups a site defines itself, as Umbraco Workflow does. **Not permissions: this decides who gets emailed** about what, and supersedes 0.5.0's flat list. Pairs with 0.6.0: who gets emailed about a pending booking is the same question as who must act on it. |
| 0.9.0 | Control the delivery API's exposure | **Reframed from "throttling"** — see "On the public API" below. Chiefly: let a site turn the delivery API **off**, since it is currently registered on every install whether used or not; document that it is anonymous and belongs behind the host's own rate limiting; and consider a per-caller cap on the expensive availability queries. **API keys are out of scope.** |
| 0.10.0 | Permissions model *(gated on a spike)* | Who may create resources and services, who may view and cancel bookings — via Umbraco user groups. **Partly shipped already**: the whole section can be hidden by group today; what is missing is granularity *within* it. **Do a spike first** against the Umbraco source in `ref/` to establish what the backoffice actually permits. If it proves impractical, this waits until after 17.0.0, where it would be purely additive and so allowed by the release policy. The admin-only **settings screen** rides with this. |
| 17.0.0 | First full release | No breaking API changes after this within the Umbraco 17 line, except where genuinely necessary — Umbraco's own policy, e.g. an urgent security fix. |

## On consent

**My earlier wording reintroduced something we had already settled, and it was wrong to.**

The original discussion: an "I agree to my data being stored" tickbox on the booking form. That
is the wrong instrument, because the lawful basis for holding a booker's name and email is
**performance of a contract** — you cannot deliver a booking without them — and consent that
cannot be refused is not consent. Worse, if consent *were* the basis, withdrawing it would
oblige erasure, which would make the site's own booking records revocable on request.

Listing "consent" in 0.3.0 muddied that. What 0.3.0 actually needs is:

- **A privacy notice** on the booking form — what is collected, why, how long it is kept, who
  sees it. Required whatever the lawful basis, and it is not a tickbox.
- **Optional marketing consent**, *only* if a site wants to contact bookers about anything other
  than their booking. That is a genuinely free choice and so a genuine consent — and it is
  separable, so it need not be in 0.3.0 at all.

A configurable "require an explicit tickbox" option is worth supporting anyway, because some
site owners' legal advice will insist on one. Support it; do not make it the default.

## On the public API

The concern is *"something finds an unsecured API and hits it with millions of requests"*. Three
honest observations:

**A package cannot prevent a DDoS.** That is edge infrastructure — Cloudflare, Azure Front Door,
a WAF. In-process rate limiting still requires every request to reach the application and be
processed far enough to be rejected, so it protects the *database* rather than the *server*.
Anything we ship should be described as protecting our own expensive work, not as DDoS defence,
or we would be making a promise the code cannot keep.

**Our endpoints do amplify, though, and that part is ours.** The availability query walks the
requested range day by day, so a cheap request produces expensive work — far more so than a
static page. `MaxQueryRangeDays` (default **31**) already caps the worst case per request, which
is the main lever and it is already in place.

**The cheapest and largest lever is not throttling at all: it is that the delivery API is on by
default.** `UBookItDeliveryApiComposer` registers it unconditionally, so a site using only the
Razor front end still exposes anonymous availability and placement endpoints it never asked for
and gains nothing from. **An opt-out removes the surface entirely** for those sites, which no
amount of rate limiting can match. That, plus documenting the API as anonymous and expecting the
host's rate limiter in front of it, is the honest shape of 0.9.0.

## What is already built

Checked against the code, so nobody rebuilds it.

- **The availability API 0.4.0 needs already exists.** `GET /services/{id}/bookable-starts?from=&to=`
  returns bookable start times across a date range, the zone they are expressed in, and — when
  the list is empty — a structural reason why. Equivalents exist for a single resource
  (`/resources/{id}/bookable-starts`, `/free-time`, `/slots`). **0.4.0 is the Razor front end
  consuming what is already there.**
- **Section-level access control (part of 0.10.0).** A user group is granted the **uBookIt
  Section**, and that single grant governs both whether the section appears *and* whether that
  user may call the management API. Access to another section does not grant uBookIt. Documented
  in `docs/backoffice.md`. What remains is finer grain — e.g. view bookings but not configure
  resources.

## Two mechanisms worth knowing before starting

**0.2.0's group is membership, not a flag.** `IUser.HasAccessToSensitiveData()` tests membership
of a built-in group with a fixed key. We cannot create, rename or reconfigure it — but we do not
need to: the instruction to a site owner is one sentence, *"add anyone who should see booker
contact details to the Sensitive Data group"*, rather than ticking something on every user
record. Since Umbraco provides it out of the box, using it is the right call. Our work is
reading it, redacting **server-side**, and documenting that sentence.

**0.4.0 must work without JavaScript.** The shipped front end is server-rendered and operable
with JS disabled, and that is a stated invariant. Fortunately a list of available dates is
*easier* to do accessibly than a date picker — a set of links or radios, one per day, no widget.

Two costs to design around: a 30-day availability query walks the range day by day, which is
exactly the expense `MaxQueryRangeDays` was added to bound — so the intended window fits inside
the existing guardrail with a day to spare, but it is real work per render and may want caching.
And it is a *second* query on a page that currently only lists times.

## Not yet slotted

Wanted, and currently on no version. Recorded so they are decisions rather than omissions.

*(Approval/decline was here until 2026-09-11; it is now slotted as **0.6.0** — see the table.
The working notes that lived in this section, kept because the change will need them:
`Booking.Confirm()` and `Booking.Decline()` exist, are covered by `StatusMachineTests`, and both
transition **from `Requested`** — but nothing produces `Requested`, because `BookingService`
hard-codes `Confirmed` at placement. The status machine is done; every route into it is missing.
What the change actually needs: the setting, placement branching on it, service operations,
management API endpoints, a backoffice affordance, and the messages. 0.5.0 already anticipates
it: the email wording is derived from `Booking.Status` rather than hard-coded, precisely so that
adding `Requested` as a reachable state cannot silently turn "your booking is confirmed" into a
lie in a customer's inbox. Decline — previously its own bullet here — rides with it: confirm and
cancel both get an email, and decline is the third thing a customer needs telling about.)*

- **Move / amend a booking's time.** Currently a cancellation and a new booking, which loses the
  reference the customer is holding.
- **Descriptions in the UI.** `Resource` has a `Description` and the delivery API returns it;
  **`Service` has no description at all, and no shipped view renders either** — verified.
- **Show a resource's capacity** so a booker can pick something big enough. Display only — not
  the same as letting one resource hold several bookings at once, which was considered and
  deliberately rejected.

## Open decisions

1. **Does the email work ship as a separate package?** **Correcting myself: I cited DevExpress
   as the precedent and that was wrong.** DevExpress is separate because its licence forbids
   redistribution in an open-source package — a legal constraint, not an architectural
   principle. The architectural principle does exist, but independently, and it is stated in
   `docs/mvp.md`: *"publish the data and the seam, never the widget or the channel"*. On that
   footing: **emails alone are probably not worth a package**, since `IEmailSender` is Umbraco's
   own and the notification seam already exists. **Templates are the stronger case** — a
   template editor, a rendering path and its own settings is a feature in its own right, and
   `docs/notifications.md` currently states plainly that uBookIt sends nothing itself. Deciding
   when templates are proposed (now 0.7.0) rather than earlier is fine; the seam means either
   choice stays possible.
2. **Does the permissions spike come back feasible?** Run it against the Umbraco source in
   `ref/` before committing 0.10.0 to a release. If the backoffice cannot express granularity
   within a section without fighting it, say so and defer — it is additive, so it is allowed
   after 17.0.0.
