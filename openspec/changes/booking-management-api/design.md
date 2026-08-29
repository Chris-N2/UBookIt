## Context

`IBookingManagementStore` and `BookingQuery` landed last change; nothing can reach them.
This adds the HTTP surface and the generated client, leaving the screen to the change
after.

The controller shape is not in question — `ResourcesController` and `ServicesController`
establish it: a shared base carrying `[BackOfficeRoute]`, `[Authorize]` and `[MapToApi]`,
purpose-built DTOs, `skip`/`take` query parameters, and `ApiResults` mapping domain
failures to status codes. Two questions are genuinely open, and both were surfaced by
asking what an operator actually does rather than by reading the existing controllers.

## Goals / Non-Goals

**Goals:**

- One endpoint that the Lit screen can be built against without reimplementing any rule.
- The timezone rule applied once, where the timezone is already known.
- Authorization that matches the section the endpoint belongs to.
- No domain type on the wire, per the existing requirement.

**Non-Goals:**

- The screen. Any mutation. Search by booker. Recording a booking's service.

## Decisions

### D1. The window is site-local dates in, UTC instants out

The endpoint takes `from` and `to` as **dates**, resolves them against
`SiteBookingSettings.TimeZoneId`, and passes the resulting instants to
`BookingQuery.Create`.

An operator asking for "this week" means the site's week. The port takes UTC instants
because bookings are instants. Somebody must convert, and there are only two candidates:

*Rejected: the client converts.* Every consumer then needs the site's timezone, the IANA
database, and the same DST reasoning — and the package already ships a delivery API a
headless client uses, so "the client" is not one client. Two implementations of a DST
rule is one more than the number that can be right.

*Chosen: the API converts.* The site zone is a server-side setting the endpoint already
has. The conversion is `TimeZoneInfo.ConvertTimeToUtc` on the start of `from` and the
start of the day **after** `to`, which keeps the window half-open while letting the caller
name an inclusive pair of dates — "Monday to Sunday" meaning all of Sunday, which is what
anyone means.

The DST consequence is stated rather than discovered: on a spring-forward day a site-local
day is 23 hours and on a fall-back day 25, so a seven-date window is not always 168 hours.
That is correct — it is the week the operator sees.

**It also broke the guardrail, and the fix went into the port rather than the endpoint.**
`BookingQuery.Create` compared raw elapsed time, so a window of exactly the maximum number
of dates resolved to slightly more than that many days whenever it contained a fall-back —
with the default of 31, *"show me October"* was refused on every European site, annually.
The first attempt here bounded dates at the endpoint *and* re-checked the converted span,
which produced the same refusal with a better message; that is a nicer way to say no to
something that should have been a yes.

So the port now counts **whole** days, a partial day over does not count, and the endpoint
bounds dates only. A window the endpoint accepts is one the port accepts. This relaxes a
guarantee shipped and QA-approved in the previous change, so it carries a `MODIFIED` delta
rather than a quiet edit.

### D2. Authorization moves to the package's own section, and this is a fix rather than an addition

`UBookItBackofficeApiControllerBase` authorizes on
`AuthorizationPolicies.SectionAccessContent` while the package ships its own section. Both
directions are wrong: uBookIt-without-Content is refused, Content-without-uBookIt is
allowed. It has been survivable while the payload was resource and service configuration;
the booking list returns **booker names and email addresses**, which is the first personal
data any uBookIt endpoint exposes, and inheriting the wrong policy by default is how that
sort of exposure normally happens.

The mechanism mirrors Umbraco's own. `AllowedApplicationHandler` is `internal`, but it
does one thing — `IAuthorizationHelper.TryGetUmbracoUser(...)` then
`user.AllowedSections.ContainsAny(...)` — and both of those are public API. So the package
writes the same handler against its own alias.

Registration is from a composer, and **the policy must add the OpenIddict validation
scheme**; a backoffice API policy without it rejects an authenticated user with no useful
diagnostic. That is the documented shape for a custom backoffice policy.

*Applied to the shared base, so all three controllers change together.* Leaving resources
and services on the wrong policy while bookings uses the right one would mean two
authorization stories in one section, and the mismatch would be even harder to notice
next time.

**Nothing is shipped, so this is a correction rather than a migration.** The package has
never been released: there are no installs, no configured user groups, and nobody relying
on the old policy. Framing it as a breaking change would have been an over-claim in the
opposite direction from the usual one — warning about a disruption that cannot occur — and
it would have put an upgrade note in the documentation for an upgrade nobody can perform.

### D3. What task 1 measures, because the design above is conditional on it

The Umbraco 17 source settles that there is **no server-side section registry** and that
`AllowedSections` is a bare string collection whose display mapping *"falls back to the
alias"* for sections it does not know. That is consistent with a package alias living
there, and is not proof.

Unmeasured, because the answers are in the running backoffice rather than in C#:

1. Whether the user-group editor **offers** `UBookIt.Section` as a grantable section.
2. Whether granting it puts a value into `IUser.AllowedSections` that the handler can
   compare against — and in what form. Built-in sections store a short alias (`content`)
   while the manifest name is `Umb.Section.Content`; uBookIt's manifest declares
   `alias: "UBookIt.Section"`, which is shaped like a name rather than like a stored
   alias.

**If the section is not grantable, stop and report.** The fallbacks — a server-side
section registration, or the documented `RequireRole` against a user-group alias — change
what a site administrator must configure, which is Chris's decision and not an
implementation detail. This is the same discipline as the theming change's D8: name the
unmeasured claim, measure it before building on it, and treat a negative result as a
design question rather than something to work around.

**Measured, and both answers were yes.** The user-group editor lists `uBookIt Section`
alongside the built-ins, and `umbracoUserGroup2App` on the running site stores it as
`UBookIt.Section` — the manifest alias verbatim, where Umbraco's own sections store short
lowercase names (`content`, `media`, `users`). The two shapes genuinely differ, so the
constant is the manifest's `alias` and not its `name`, and a test ties the two together.

Worth noting that the inference from `SectionMapper`'s fallback reached the same answer.
It was still right to measure: an inference that happens to be correct is
indistinguishable, before the fact, from one that is not — and this project has twice
shipped a defect that began as a plausible reading of a framework.

### D4. The generated TypeScript client ships with this change

Precedent, not preference: the committed client has moved with every prior API change
(`fd3fc66`, `217051f`). It makes this diff noisier and leaves the next change as pure UI
work, which is the better split of the two.

### D5. The response carries exactly what the port returns

`BookingSummary` was shaped for a list row and the DTO mirrors it — id, interval, time
zone, status, created, booker name and email, and the claimed resources with their names.
No field is added at the HTTP layer, because a field the port cannot supply is a field the
screen would have to obtain some other way, and that is how a second read path starts.

The interval is serialised as instants with the booking's own `TimeZoneId` alongside, so
the screen can render local time without guessing the zone.

### D6. The containment requirement is widened, and its guarantee is not

`resource-management` says management controllers "SHALL depend only on **the resource
management port** and validated Core services". A bookings controller violates that by
existing — it depends on `IBookingManagementStore`.

Widened to the management **ports**, plural. What the requirement was actually written for
— that no controller or API-layer type touches `IBookingStore` or `Booking.Rehydrate`, so
HTTP callers cannot reach raw booking storage — is carried through verbatim, and this
change satisfies it: the controller goes through the management port and
`BookingQuery.Create`, neither of which is raw storage.

## Risks / Trade-offs

- **The section may not be grantable** → D3; measured first, stop-and-report.
- **Existing endpoints change who may call them** → immaterial: nothing is released, so
  there is no install to disturb. Recorded because the change is real, not because anyone
  is exposed to it.
- **DST makes a date window a variable number of hours** → D1; the guardrail is evaluated
  on dates before conversion so the error a caller sees matches the dates they sent.
- **The regenerated client inflates the diff** → accepted, per D4.
- **The endpoint returns personal data** → it is what a booking list shows; the
  authorization fix is the mitigation, and nothing logs the payload.

## Open Questions

- Whether the endpoint should also accept explicit UTC instants for a headless caller that
  has already done its own conversion. Deferred: no such caller exists, and adding a
  second window representation before one does is a guess about a client that may never
  arrive.
- Whether the booking list belongs in the existing uBookIt section as a third section view
  or somewhere else in the backoffice. A screen question, and the next change's to make.
