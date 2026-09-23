# The delivery API

uBookIt has a JSON delivery API for building your own booking front end — a React or Vue
site, a mobile app, anything that would rather consume data than render our Razor views.
It covers resource and service discovery, availability, and booking placement, and it is
versioned under `/umbraco/ubookit/api/v1/`.

> Every uBookIt setting, which of them can be changed in the backoffice and which are
> configuration-only, and what happens when a value cannot be read, is in
> **[Configuration](configuration.md)**.


**It is off by default. Both halves of it.** A fresh install — and an upgrade that
changes no configuration — serves none of these endpoints. Turning them on is one
setting each:

```json
{
  "UBookIt": {
    "DeliveryApi": {
      "EnableReads": true,
      "EnablePlacement": true
    }
  }
}
```

| Setting | What it exposes |
|---|---|
| `EnableReads` | Resource and service discovery, availability, slots, bookable starts, and the privacy/retention read. |
| `EnablePlacement` | Anonymous booking placement — direct against a resource, or via a service. |

The two are independent. Both on is the full headless story. `EnableReads` alone suits a
site that shows availability from its own JavaScript but takes bookings only through its
own pages. Neither — the default — is right for a site using only the shipped booking
page, which does not use this API at all: it renders on the server, and it works
identically whatever these settings say.

`EnablePlacement` without reads is legal but has one property to accept knowingly: a
failed placement still says *why* it failed — a resource not eligible for a service, a
slot already taken — so a caller holding resource ids can learn eligibility and occupancy
facts from failures even though the reads that would publish those facts are off. Bounded
in practice (with reads off there is no way to enumerate the ids to ask about), and
stable failure codes are worth more than hiding them — but if that trade reads wrong for
your site, enable both directions or neither.

Settings are read at startup, so changing them needs an application restart.

> **Not the same thing as Umbraco's Delivery API.** Umbraco ships its own content
> Delivery API, controlled by `Umbraco:CMS:DeliveryApi:Enabled`. That setting and this
> one are unrelated: `Umbraco:CMS:DeliveryApi` governs Umbraco's content API,
> `UBookIt:DeliveryApi` governs uBookIt's booking API, and neither switches the other.

> **Upgrading from a version where the API was always on?** This default was flipped
> deliberately before the first full release — it is a breaking change for existing API
> consumers, and the fix is the two settings above. Nothing else about the API changed.

## A disabled endpoint does not exist

A request to a direction you have not enabled gets your site's ordinary "not found"
response — the same one a made-up URL gets — and the endpoint does not appear in the
API's OpenAPI document. Deliberately not a 403 or a "this feature is disabled" message:
those tell an anonymous stranger that the package is installed and that the endpoint
exists to be switched on. Absence says nothing.

## What "anonymous" means, honestly

When enabled, the delivery API is **anonymous by design**. Its purpose is to let a
stranger on the public internet see availability and place a booking — that is what a
booking system is for — so there is no login, no API key, and no cookie, and a booking's
contact details come from the request body alone.

That has a consequence worth stating plainly: **the API cannot know who is calling, and
neither could any code we might add.** The mechanisms that sound like they would help do
not:

- The `Origin` and `Referer` headers are written by the caller. A browser fills them in
  honestly; a script sets them to anything, or nothing. Checking them stops nobody who
  matters and breaks legitimate non-browser consumers.
- CORS is a *browser* policy about what cross-origin web pages may read. It does not
  restrict a script, a bot, or a server — none of which ask a browser's permission.
- An API key embedded in your public front-end JavaScript is readable by anyone who
  views source, which is to say it is not a secret.

So uBookIt does not pretend to validate callers, and this page is where that is said
rather than discovered. What protects an enabled API is the same thing that protects
the rest of your site:

- **Volume is your host's job.** Rate limiting, bot filtering and denial-of-service
  protection belong to the infrastructure in front of your application — a reverse
  proxy, a WAF, an edge service such as Cloudflare or Azure Front Door. uBookIt makes
  **no DDoS-protection claim of any kind**: in-process throttling still requires every
  request to reach and occupy your application, so nothing a package ships can stand in
  for the edge.
- **Per-request cost is bounded by the package.** The expensive operation here is an
  availability query, which walks its date range day by day. `UBookIt:MaxQueryRangeDays`
  (default 31) caps how much work any single request can ask for, on every caller path —
  API and server-rendered alike. There is deliberately no per-caller quota on top: a
  quota per caller is rate limiting under another name, and rate limiting is your
  host's, where it can be enforced before the request costs you anything.
- **Junk bookings are a business risk, not just a technical one.** An enabled placement
  endpoint lets a script hold real slots with invented details — and a determined
  script can do the same through any public booking form, ours included. The mitigation
  that actually works is `UBookIt:AutoConfirm: false`, which makes every placement a
  **request** an operator confirms or declines: junk holds nothing for longer than your
  review, and it never reads as a confirmed sale. Sites that enable placement and leave
  auto-confirm on should do so knowingly.

## What the shipped booking page needs from this

Nothing. The Razor booking flow renders on the server from the package's own services —
that is a stated guarantee, not an implementation detail — so leaving both settings off
costs it nothing, and turning them on changes nothing about it.

## Closed dates look like any other unavailable date

A site can mark dates on which the whole organisation is shut. This API says nothing about
them: a closed date carries no availability, and there is no closure, label, or reason
anywhere in the resource read model or in a refusal. A placement on such a date is refused
with `outside-open-hours`, exactly as a request at three in the morning would be.

That is deliberate. A closure's name is the site's own operational note — "Stocktake",
"Directors' away day" — and this API is anonymous, so anything it returns is public. A
consumer that wants to explain why a date is unavailable has to get that from the site, not
from here.

## Supplying your own resource store

`IResourceStore` is a published port, so a host may implement its own. If you do, **applying
the site's closures to the resources you hydrate is yours to do**, exactly as their opening
hours and exceptions already are: availability is computed from the resource you return, and
a resource returned without its closures is one the package will happily offer on a date the
site has closed. The shipped SQL Server store does this; nothing in the domain can do it on
your behalf, because the domain never loads a resource.
